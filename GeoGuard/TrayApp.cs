using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GeoGuard
{
    class TrayApp : ApplicationContext
    {
        readonly NotifyIcon tray;
        readonly SynchronizationContext ui;
        readonly MenuItem statusItem;
        readonly MenuItem notifyToggle;
        readonly MenuItem autoStartToggle;

        GeoDb geoDb;
        AsnDb asnDb;
        AuditWatcher watcher;
        MonitorForm monitor;
        HistoryForm history;
        readonly object stateSync = new object();
        List<string> blockedCcs = new List<string>();
        Dictionary<string, IpRangeSet> sets = new Dictionary<string, IpRangeSet>();
        readonly List<BlockRecord> blockHistory = new List<BlockRecord>();
        readonly HashSet<string> unprotectedCcs = new HashSet<string>();   // 차단 대상이지만 실제 미차단
        readonly HashSet<string> retrying = new HashSet<string>();         // 재시도 루프가 이미 도는 국가
        int busy;

        public TrayApp()
        {
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            ui = SynchronizationContext.Current;

            statusItem = new MenuItem("초기화 중...") { Enabled = false };
            notifyToggle = new MenuItem("차단 알림 표시", delegate(object s, EventArgs e)
            {
                notifyToggle.Checked = !notifyToggle.Checked;
            }) { Checked = true };
            autoStartToggle = new MenuItem("윈도우 시작 시 자동 실행", OnToggleAutoStart);

            var menu = new ContextMenu(new[]
            {
                statusItem,
                new MenuItem("-"),
                new MenuItem("실시간 연결 모니터", delegate(object s, EventArgs e) { OpenMonitor(); }),
                new MenuItem("차단 이력 보기", delegate(object s, EventArgs e) { OpenHistory(); }),
                new MenuItem("상태 자세히 보기", OnShowStatus),
                new MenuItem("-"),
                new MenuItem("차단 국가 설정...", OnSelectCountries),
                new MenuItem("차단 규칙 재적용", delegate(object s, EventArgs e) { RunBusy(ReapplyAll); }),
                new MenuItem("차단 규칙 모두 제거", OnRemoveRules),
                new MenuItem("IP 목록/DB 업데이트", delegate(object s, EventArgs e) { RunBusy(UpdateAll); }),
                new MenuItem("-"),
                notifyToggle,
                autoStartToggle,
                new MenuItem("로그 열기", delegate(object s, EventArgs e)
                {
                    try { Process.Start("notepad.exe", Logger.LogPath); } catch { }
                }),
                new MenuItem("-"),
                new MenuItem("종료", OnExit),
            });

            tray = new NotifyIcon
            {
                Icon = SystemIcons.Shield,
                Text = "GeoGuard - 초기화 중",
                Visible = true,
                ContextMenu = menu
            };
            tray.DoubleClick += delegate { OpenMonitor(); };
            tray.BalloonTipClicked += delegate { OpenMonitor(); };

            autoStartToggle.Checked = IsAutoStartRegistered();

            Task.Run(new Action(Initialize));
        }

        void Initialize()
        {
            Logger.Log("=== GeoGuard 시작 ===");
            try
            {
                var ccs = Config.LoadBlockedCountries();
                lock (stateSync) blockedCcs = ccs;

                SetStatus("차단 국가 IP 목록 로드 중...");
                var newSets = new Dictionary<string, IpRangeSet>();
                var lists = new Dictionary<string, List<string>>();
                var loadFailed = new List<string>();
                foreach (var cc in ccs)
                {
                    try
                    {
                        var cidrs = CountryIpList.LoadOrDownload(cc, false);
                        lists[cc] = cidrs;
                        newSets[cc] = IpRangeSet.FromCidrs(cidrs);
                        Logger.Log(string.Format("[{0}] IP 대역 {1}개 로드", cc, newSets[cc].Count));
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(string.Format("[{0}] 목록 로드 실패: {1}", cc, ex.Message));
                        loadFailed.Add(cc);
                    }
                }
                lock (stateSync) sets = newSets;

                SetStatus("방화벽 규칙 확인 중...");
                FirewallService.RemoveLegacyRules();
                var applied = FirewallService.AppliedCountries();
                foreach (var cc in applied)
                    if (!ccs.Contains(cc))
                        FirewallService.RemoveRulesFor(cc);
                foreach (var cc in ccs)
                {
                    if (!lists.ContainsKey(cc)) continue;
                    if (!applied.Contains(cc))
                    {
                        SetStatus(string.Format("방화벽 규칙 적용 중... ({0})", Countries.Display(cc)));
                        FirewallService.ApplyRulesFor(cc, lists[cc]);
                    }
                }

                // 목록 로드에 실패했고 기존 방화벽 규칙도 없는 국가 = 실제로는 무방비 상태.
                // (기존 규칙이 남아 있으면 목록을 못 받아도 차단은 계속 유효하다.)
                var unprotected = loadFailed.FindAll(
                    delegate(string cc) { return !applied.Contains(cc); });

                SetStatus("GeoIP DB 로드 중...");
                try
                {
                    geoDb = GeoDb.LoadOrDownload(false);
                    Logger.Log(string.Format("GeoIP DB 로드: {0}개 대역", geoDb.Count));
                }
                catch (Exception ex)
                {
                    Logger.Log("GeoIP DB 로드 실패(국가 표시 제한됨): " + ex.Message);
                }

                SetStatus("ASN DB 로드 중...");
                try
                {
                    asnDb = AsnDb.LoadOrDownload(false);
                    Logger.Log(string.Format("ASN DB 로드: {0}개 대역", asnDb.Count));
                }
                catch (Exception ex)
                {
                    Logger.Log("ASN DB 로드 실패(조직 표시 제한됨): " + ex.Message);
                }

                SetStatus("이벤트 감시 시작 중...");
                AuditWatcher.EnableAuditPolicy();
                watcher = new AuditWatcher(IsBlockedIp, OnBlockedEvent);
                watcher.Start();

                UpdateProtectionState();
                Balloon("GeoGuard 실행됨",
                    string.Format("차단 대상: {0}\r\n차단 및 감시가 활성화되었습니다.", BlockedSummary()),
                    ToolTipIcon.Info);

                // 미보호 국가 처리는 감시가 켜진 뒤에 시작한다.
                HandleUnprotectedCountries(unprotected);
            }
            catch (Exception ex)
            {
                Logger.Log("초기화 실패: " + ex);
                SetStatus("초기화 실패 - 로그 확인");
                SetTrayText("GeoGuard - 초기화 실패");
                Balloon("GeoGuard 초기화 실패", ex.Message, ToolTipIcon.Error);
            }
        }

        string BlockedSummary()
        {
            List<string> ccs;
            lock (stateSync) ccs = new List<string>(blockedCcs);
            if (ccs.Count == 0) return "(없음)";
            var names = ccs.Select(new Func<string, string>(Countries.NameOnly)).ToArray();
            string joined = string.Join(", ", names);
            return joined.Length > 40 ? string.Format("{0}개국", ccs.Count) : joined;
        }

        // 그 국가의 IP 목록이 업스트림에 아예 없는 경우(404)와 일시적 네트워크 오류를 구분한다.
        static bool IsMissingUpstreamData(Exception ex)
        {
            for (Exception e = ex; e != null; e = e.InnerException)
            {
                var we = e as WebException;
                if (we == null) continue;
                var resp = we.Response as HttpWebResponse;
                if (resp != null && resp.StatusCode == HttpStatusCode.NotFound) return true;
            }
            return false;
        }

        static string NamesOf(List<string> ccs)
        {
            return string.Join(", ",
                ccs.Select(new Func<string, string>(Countries.NameOnly)).ToArray());
        }

        // 차단 대상인데 IP 목록을 받지 못해 실제로는 차단되지 않은 국가를 처리한다.
        //
        // 설계 근거:
        //  - fail-closed: 정책을 적용하지 못한 상태를 조용히 넘어가지 않는다. 해결될 때까지
        //    트레이 아이콘과 상태 문구를 경고로 고정해, "차단 중"으로 오인할 수 없게 한다.
        //  - 사용자에게 모달로 묻지 않는다. 실패 원인은 대부분 일시적 네트워크 문제인데,
        //    보안 경고창은 클릭 스루로 무시되는 것이 정설이라 판단을 떠넘겨도 보호가 늘지 않는다.
        //  - 대신 자동으로 복구한다. 30초부터 최대 30분까지 지수 백오프로 재시도하므로
        //    부팅 직후 네트워크가 늦게 붙는 경우 등은 스스로 낫는다.
        //  - 사용자 개입이 필요하면 트레이 메뉴에서 언제든 가능하다
        //    ("차단 규칙 재적용" = 즉시 재시도, "차단 국가 설정..." = 차단 해제).
        void HandleUnprotectedCountries(List<string> unprotected)
        {
            if (unprotected.Count == 0) return;

            lock (stateSync)
                foreach (var cc in unprotected) unprotectedCcs.Add(cc);

            Logger.Log("미차단 국가(자동 재시도 시작): " + string.Join(", ", unprotected.ToArray()));
            UpdateProtectionState();
            Balloon("일부 국가가 아직 차단되지 않음",
                string.Format("{0}\r\nIP 목록을 받지 못했습니다. 성공할 때까지 자동으로 다시 시도합니다.",
                    NamesOf(unprotected)),
                ToolTipIcon.Warning);

            foreach (var cc in unprotected) StartAutoRetry(cc);
        }

        void StartAutoRetry(string cc)
        {
            // 같은 국가에 재시도 루프가 두 개 돌지 않게 한다.
            lock (stateSync) { if (!retrying.Add(cc)) return; }

            Task.Run(delegate
            {
                try
                {
                    int delaySec = 30;
                    while (true)
                    {
                        Thread.Sleep(delaySec * 1000);

                        // 그 사이 사용자가 차단 목록에서 빼면 조용히 그만둔다.
                        lock (stateSync)
                        {
                            if (!blockedCcs.Contains(cc)) { unprotectedCcs.Remove(cc); break; }
                        }

                        try
                        {
                            var cidrs = CountryIpList.LoadOrDownload(cc, true);
                            SetsPut(cc, IpRangeSet.FromCidrs(cidrs));
                            FirewallService.ApplyRulesFor(cc, cidrs);
                            lock (stateSync) unprotectedCcs.Remove(cc);
                            Logger.Log(string.Format("[{0}] 자동 재시도 성공 - 차단 적용됨", cc));
                            Balloon("차단 적용됨",
                                Countries.Display(cc) + " 차단이 이제 활성화되었습니다.", ToolTipIcon.Info);
                            break;
                        }
                        catch (Exception ex)
                        {
                            // 404 = 그 국가의 공개 IP 목록 자체가 없다는 뜻이라 재시도해도 소용없다.
                            // 미차단 표시는 남겨두어 보호되는 것처럼 보이지 않게 한다.
                            if (IsMissingUpstreamData(ex))
                            {
                                Logger.Log(string.Format("[{0}] 공개 IP 목록 없음 - 재시도 중단", cc));
                                Balloon("차단할 수 없는 국가",
                                    string.Format("{0} 은(는) 공개된 IP 대역 목록이 없어 차단할 수 없습니다.\r\n" +
                                        "'차단 국가 설정'에서 제외해 주세요.", Countries.Display(cc)),
                                    ToolTipIcon.Error);
                                break;
                            }
                            delaySec = Math.Min(delaySec * 2, 1800);   // 30초 -> 최대 30분
                            Logger.Log(string.Format("[{0}] 자동 재시도 실패, {1}초 후 재시도: {2}",
                                cc, delaySec, ex.Message));
                        }
                    }
                }
                finally
                {
                    lock (stateSync) retrying.Remove(cc);
                    UpdateProtectionState();
                }
            });
        }

        // 미차단 국가가 하나라도 있으면 트레이를 경고 상태로 고정한다.
        void UpdateProtectionState()
        {
            List<string> bad;
            lock (stateSync) bad = new List<string>(unprotectedCcs);

            if (bad.Count == 0)
            {
                string summary = BlockedSummary();
                SetTrayIcon(SystemIcons.Shield);
                SetStatus("보호 활성화됨: " + summary);
                SetTrayText("GeoGuard - 차단 중: " + summary);
            }
            else
            {
                string names = NamesOf(bad);
                SetTrayIcon(SystemIcons.Warning);
                SetStatus("경고: " + names + " 미차단 (재시도 중)");
                SetTrayText("GeoGuard - " + names + " 미차단, 재시도 중");
            }
        }

        // sets 는 통째로 교체(copy-on-write)만 하고 제자리 수정하지 않는다.
        // 이벤트 감시 스레드가 잠금 없이 순회하므로, 제자리 수정하면 순회 도중
        // "컬렉션이 수정되었습니다" 예외로 프로세스가 죽는다.
        void SetsPut(string cc, IpRangeSet set)
        {
            lock (stateSync)
            {
                var copy = new Dictionary<string, IpRangeSet>(sets);
                copy[cc] = set;
                sets = copy;
            }
        }

        void SetsRemove(string cc)
        {
            lock (stateSync)
            {
                if (!sets.ContainsKey(cc)) return;
                var copy = new Dictionary<string, IpRangeSet>(sets);
                copy.Remove(cc);
                sets = copy;
            }
        }

        bool IsBlockedIp(IPAddress ip)
        {
            Dictionary<string, IpRangeSet> localSets;
            List<string> ccs;
            lock (stateSync) { localSets = sets; ccs = blockedCcs; }
            foreach (var kv in localSets)
                if (kv.Value.Contains(ip)) return true;
            GeoDb db = geoDb;
            if (db != null)
            {
                string cc = db.Lookup(ip);
                if (cc != null && ccs.Contains(cc)) return true;
            }
            return false;
        }

        string BlockedCcOf(IPAddress ip)
        {
            Dictionary<string, IpRangeSet> localSets;
            lock (stateSync) localSets = sets;
            foreach (var kv in localSets)
                if (kv.Value.Contains(ip)) return kv.Key;
            GeoDb db = geoDb;
            return db == null ? null : db.Lookup(ip);
        }

        string LookupCountry(IPAddress ip)
        {
            GeoDb db = geoDb;
            if (db != null) return db.Lookup(ip);
            return BlockedCcOf(ip);
        }

        string LookupOrg(IPAddress ip)
        {
            AsnDb db = asnDb;
            return db == null ? null : db.Lookup(ip);
        }

        bool IsBlockedCc(string cc)
        {
            if (cc == null) return false;
            lock (stateSync) return blockedCcs.Contains(cc);
        }

        IpInfoProvider MakeProvider()
        {
            return new IpInfoProvider
            {
                Country = LookupCountry,
                Org = LookupOrg,
                IsBlockedCc = IsBlockedCc
            };
        }

        List<BlockRecord> HistorySnapshot()
        {
            lock (blockHistory) return new List<BlockRecord>(blockHistory);
        }

        void OnBlockedEvent(BlockEvent evt)
        {
            string cc = BlockedCcOf(evt.Remote);
            string name = Countries.NameOnly(cc);

            Logger.Log(string.Format("차단[{0}]: {1} {2} {3}:{4} ({5})",
                cc, evt.App, evt.Direction, evt.Remote, evt.Port, evt.Protocol));

            lock (blockHistory)
            {
                blockHistory.Add(new BlockRecord { Time = DateTime.Now, Evt = evt });
                if (blockHistory.Count > 1000) blockHistory.RemoveRange(0, blockHistory.Count - 1000);
            }

            if (!notifyToggle.Checked) return;
            if (!Throttler.ShouldNotify(evt.App, evt.Remote.ToString())) return;

            string title = evt.Direction == "인바운드"
                ? string.Format("{0}발 접속 시도 차단됨", name)
                : string.Format("{0}행 연결 시도 차단됨", name);
            string body = string.Format("{0}\r\n{1} {2}:{3} ({4})",
                evt.App, evt.Direction, evt.Remote, evt.Port, evt.Protocol);
            Balloon(title, body, ToolTipIcon.Warning);
        }

        // ---- 국가 선택 ----

        void OnSelectCountries(object sender, EventArgs e)
        {
            ui.Post(delegate(object s)
            {
                List<string> current;
                lock (stateSync) current = new List<string>(blockedCcs);
                using (var form = new CountrySelectForm(current))
                {
                    if (form.ShowDialog() != DialogResult.OK) return;
                    // 저장은 ApplySelection 이 실제 적용에 성공한 국가만 기록한다.
                    var chosen = form.SelectedCountries;
                    RunBusy(delegate { ApplySelection(chosen); });
                }
            }, null);
        }

        void ApplySelection(List<string> newCcs)
        {
            SetStatus("차단 국가 변경 적용 중...");
            List<string> oldCcs;
            lock (stateSync) oldCcs = new List<string>(blockedCcs);

            foreach (var cc in oldCcs)
            {
                if (!newCcs.Contains(cc))
                {
                    FirewallService.RemoveRulesFor(cc);
                    SetsRemove(cc);
                    lock (stateSync) unprotectedCcs.Remove(cc);
                }
            }

            var applied = FirewallService.AppliedCountries();
            var succeeded = new List<string>();
            var failed = new List<string>();
            foreach (var cc in newCcs)
            {
                try
                {
                    var cidrs = CountryIpList.LoadOrDownload(cc, false);
                    SetsPut(cc, IpRangeSet.FromCidrs(cidrs));
                    if (!applied.Contains(cc))
                    {
                        SetStatus(string.Format("방화벽 규칙 적용 중... ({0})", Countries.Display(cc)));
                        FirewallService.ApplyRulesFor(cc, cidrs);
                    }
                    succeeded.Add(cc);
                }
                catch (Exception ex)
                {
                    // 한 국가가 실패해도 나머지는 계속 적용하고, 반쯤 적용된 규칙은 되돌린다.
                    Logger.Log(string.Format("[{0}] 적용 실패: {1}", cc, ex.Message));
                    failed.Add(cc);
                    try { FirewallService.RemoveRulesFor(cc); } catch { }
                    SetsRemove(cc);
                }
            }

            // 사용자가 고른 국가는 그대로 유지한다. 적용에 실패한 국가는 설정에서 지우는 대신
            // 미차단으로 표시하고 자동 재시도에 넘겨, 사용자의 의도가 조용히 사라지지 않게 한다.
            lock (stateSync)
            {
                blockedCcs = new List<string>(newCcs);
                foreach (var cc in succeeded) unprotectedCcs.Remove(cc);
            }
            Config.SaveBlockedCountries(newCcs);

            if (failed.Count > 0)
            {
                HandleUnprotectedCountries(failed);
            }
            else
            {
                UpdateProtectionState();
                Balloon("차단 국가 변경됨", "차단 대상: " + BlockedSummary(), ToolTipIcon.Info);
            }
        }

        // ---- 규칙/DB 관리 ----

        void ReapplyAll()
        {
            List<string> ccs;
            lock (stateSync) ccs = new List<string>(blockedCcs);
            int total = 0;
            var failed = new List<string>();
            foreach (var cc in ccs)
            {
                SetStatus(string.Format("방화벽 규칙 적용 중... ({0})", Countries.Display(cc)));
                try
                {
                    var cidrs = CountryIpList.LoadOrDownload(cc, false);
                    SetsPut(cc, IpRangeSet.FromCidrs(cidrs));
                    total += FirewallService.ApplyRulesFor(cc, cidrs);
                    lock (stateSync) unprotectedCcs.Remove(cc);
                }
                catch (Exception ex)
                {
                    Logger.Log(string.Format("[{0}] 재적용 실패: {1}", cc, ex.Message));
                    failed.Add(cc);
                }
            }
            if (failed.Count > 0) HandleUnprotectedCountries(failed);
            else
            {
                UpdateProtectionState();
                Balloon("규칙 적용 완료", string.Format("방화벽 규칙 {0}개 적용됨", total), ToolTipIcon.Info);
            }
        }

        void UpdateAll()
        {
            List<string> ccs;
            lock (stateSync) ccs = new List<string>(blockedCcs);
            int total = 0;
            var failed = new List<string>();
            foreach (var cc in ccs)
            {
                SetStatus(string.Format("업데이트 중... ({0})", Countries.Display(cc)));
                try
                {
                    var cidrs = CountryIpList.LoadOrDownload(cc, true);
                    SetsPut(cc, IpRangeSet.FromCidrs(cidrs));
                    total += FirewallService.ApplyRulesFor(cc, cidrs);
                    lock (stateSync) unprotectedCcs.Remove(cc);
                }
                catch (Exception ex)
                {
                    Logger.Log(string.Format("[{0}] 업데이트 실패: {1}", cc, ex.Message));
                    failed.Add(cc);
                }
            }
            SetStatus("GeoIP/ASN DB 업데이트 중...");
            try { geoDb = GeoDb.LoadOrDownload(true); }
            catch (Exception ex) { Logger.Log("GeoIP DB 업데이트 실패: " + ex.Message); }
            try { asnDb = AsnDb.LoadOrDownload(true); }
            catch (Exception ex) { Logger.Log("ASN DB 업데이트 실패: " + ex.Message); }

            if (failed.Count > 0) HandleUnprotectedCountries(failed);
            else
            {
                UpdateProtectionState();
                Balloon("업데이트 완료", string.Format("방화벽 규칙 {0}개 적용됨", total), ToolTipIcon.Info);
            }
        }

        void OnRemoveRules(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "방화벽에서 GeoGuard 차단 규칙을 모두 제거합니다.\r\n국가 차단이 해제됩니다. 계속할까요?",
                "GeoGuard", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (result != DialogResult.Yes) return;
            RunBusy(delegate
            {
                int n = FirewallService.RemoveAll();
                // 규칙만 지우고 설정을 남기면 재시작 때 되살아나고, 그 사이 트레이는
                // "차단 중"이라 표시된다. 명시적 해제이므로 설정까지 비운다.
                lock (stateSync)
                {
                    blockedCcs = new List<string>();
                    unprotectedCcs.Clear();
                    sets = new Dictionary<string, IpRangeSet>();
                }
                Config.SaveBlockedCountries(new List<string>());
                UpdateProtectionState();
                Balloon("차단 해제", string.Format("규칙 {0}개 제거됨", n), ToolTipIcon.Info);
            });
        }

        void OnShowStatus(object sender, EventArgs e)
        {
            RunBusy(delegate
            {
                int rules = -1;
                try { rules = FirewallService.CountRules(); } catch { }
                int blocked;
                lock (blockHistory) blocked = blockHistory.Count;
                int rangeTotal;
                List<string> ccs;
                lock (stateSync)
                {
                    rangeTotal = sets.Values.Sum(delegate(IpRangeSet s2) { return s2.Count; });
                    ccs = new List<string>(blockedCcs);
                }
                string msg = string.Format(
                    "차단 국가: {0}\r\n방화벽 규칙: {1}\r\n차단 IP 대역: {2}개\r\nGeoIP DB: {3}\r\nASN DB: {4}\r\n이벤트 감시: {5}\r\n차단 감지: {6}건 (시작 이후)\r\n로그: {7}",
                    ccs.Count == 0 ? "(없음)" : string.Join(", ",
                        ccs.Select(new Func<string, string>(Countries.Display)).ToArray()),
                    rules < 0 ? "확인 실패" : rules + "개",
                    rangeTotal,
                    geoDb == null ? "없음" : string.Format("{0}개 대역 ({1:yyyy-MM-dd})", geoDb.Count, GeoDb.CacheDate()),
                    asnDb == null ? "없음" : string.Format("{0}개 대역 ({1:yyyy-MM-dd})", asnDb.Count, AsnDb.CacheDate()),
                    watcher == null ? "중지됨" : "실행 중",
                    blocked,
                    Logger.LogPath);
                ui.Post(delegate(object s)
                {
                    MessageBox.Show(msg, "GeoGuard 상태", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }, null);
            });
        }

        void OpenMonitor()
        {
            ui.Post(delegate(object s)
            {
                if (monitor == null || monitor.IsDisposed)
                {
                    monitor = new MonitorForm(MakeProvider());
                    monitor.Show();
                }
                else
                {
                    if (monitor.WindowState == FormWindowState.Minimized)
                        monitor.WindowState = FormWindowState.Normal;
                    monitor.Activate();
                }
            }, null);
        }

        void OpenHistory()
        {
            ui.Post(delegate(object s)
            {
                if (history == null || history.IsDisposed)
                {
                    history = new HistoryForm(HistorySnapshot, MakeProvider());
                    history.Show();
                }
                else
                {
                    if (history.WindowState == FormWindowState.Minimized)
                        history.WindowState = FormWindowState.Normal;
                    history.Activate();
                }
            }, null);
        }

        // ---- 자동 시작 (작업 스케줄러: 관리자 권한으로 로그온 시 실행) ----

        static bool IsAutoStartRegistered()
        {
            return RunSchtasks("/Query /TN \"GeoGuard\"") == 0;
        }

        void OnToggleAutoStart(object sender, EventArgs e)
        {
            if (autoStartToggle.Checked)
            {
                if (RunSchtasks("/Delete /TN \"GeoGuard\" /F") == 0)
                    autoStartToggle.Checked = false;
            }
            else
            {
                string exe = Application.ExecutablePath;
                int code = RunSchtasks(string.Format(
                    "/Create /TN \"GeoGuard\" /TR \"\\\"{0}\\\"\" /SC ONLOGON /RL HIGHEST /F", exe));
                if (code == 0) autoStartToggle.Checked = true;
                else MessageBox.Show("자동 실행 등록 실패 (schtasks exit " + code + ")", "GeoGuard");
            }
        }

        static int RunSchtasks(string args)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = args,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var p = Process.Start(psi))
                {
                    p.WaitForExit(15000);
                    return p.ExitCode;
                }
            }
            catch { return -1; }
        }

        // ---- 공용 헬퍼 ----

        void RunBusy(Action action)
        {
            if (Interlocked.CompareExchange(ref busy, 1, 0) != 0)
            {
                Balloon("GeoGuard", "다른 작업이 진행 중입니다.", ToolTipIcon.Info);
                return;
            }
            Task.Run(delegate
            {
                try { action(); }
                catch (Exception ex)
                {
                    Logger.Log("작업 실패: " + ex);
                    Balloon("작업 실패", ex.Message, ToolTipIcon.Error);
                }
                finally { Interlocked.Exchange(ref busy, 0); }
            });
        }

        void SetStatus(string text)
        {
            ui.Post(delegate(object s) { statusItem.Text = text; }, null);
        }

        void SetTrayText(string text)
        {
            ui.Post(delegate(object s)
            {
                tray.Text = text.Length > 63 ? text.Substring(0, 63) : text;
            }, null);
        }

        void SetTrayIcon(Icon icon)
        {
            ui.Post(delegate(object s) { tray.Icon = icon; }, null);
        }

        void Balloon(string title, string body, ToolTipIcon icon)
        {
            ui.Post(delegate(object s)
            {
                tray.ShowBalloonTip(5000, title, body, icon);
            }, null);
        }

        void OnExit(object sender, EventArgs e)
        {
            Logger.Log("종료");
            if (watcher != null) watcher.Dispose();
            tray.Visible = false;
            tray.Dispose();
            ExitThread();
        }
    }
}
