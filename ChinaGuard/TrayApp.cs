using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ChinaGuard
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
        IpRangeSet chinaSet;
        AuditWatcher watcher;
        MonitorForm monitor;
        HistoryForm history;
        readonly List<BlockRecord> blockHistory = new List<BlockRecord>();
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
                new MenuItem("차단 규칙 재적용", delegate(object s, EventArgs e) { RunBusy(ReapplyRules); }),
                new MenuItem("차단 규칙 모두 제거", OnRemoveRules),
                new MenuItem("IP 목록/GeoIP DB 업데이트", delegate(object s, EventArgs e) { RunBusy(UpdateLists); }),
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
                Text = "ChinaGuard - 초기화 중",
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
            Logger.Log("=== ChinaGuard 시작 ===");
            try
            {
                SetStatus("중국 IP 목록 로드 중...");
                var cidrs = ChinaIpList.LoadOrDownload(false);
                chinaSet = IpRangeSet.FromCidrs(cidrs);
                Logger.Log(string.Format("중국 IP 대역 {0}개 로드", chinaSet.Count));

                SetStatus("방화벽 규칙 확인 중...");
                if (FirewallService.CountRules() == 0)
                {
                    SetStatus("방화벽 규칙 적용 중...");
                    FirewallService.ApplyRules(cidrs);
                }

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
                watcher = new AuditWatcher(IsChina, OnChinaBlock);
                watcher.Start();

                SetStatus("보호 활성화됨");
                SetTrayText("ChinaGuard - 보호 활성화됨");
                Balloon("ChinaGuard 실행됨", "중국 IP 차단 및 감시가 활성화되었습니다.", ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                Logger.Log("초기화 실패: " + ex);
                SetStatus("초기화 실패 - 로그 확인");
                SetTrayText("ChinaGuard - 초기화 실패");
                Balloon("ChinaGuard 초기화 실패", ex.Message, ToolTipIcon.Error);
            }
        }

        bool IsChina(IPAddress ip)
        {
            if (chinaSet != null && chinaSet.Contains(ip)) return true;
            if (geoDb != null && geoDb.Lookup(ip) == "CN") return true;
            return false;
        }

        string LookupCountry(IPAddress ip)
        {
            if (geoDb != null) return geoDb.Lookup(ip);
            if (chinaSet != null && chinaSet.Contains(ip)) return "CN";
            return null;
        }

        string LookupOrg(IPAddress ip)
        {
            return asnDb == null ? null : asnDb.Lookup(ip);
        }

        IpInfoProvider MakeProvider()
        {
            return new IpInfoProvider
            {
                Country = LookupCountry,
                Org = LookupOrg
            };
        }

        List<BlockRecord> HistorySnapshot()
        {
            lock (blockHistory) return new List<BlockRecord>(blockHistory);
        }

        void OnChinaBlock(BlockEvent evt)
        {
            Logger.Log(string.Format("차단: {0} {1} {2}:{3} ({4})",
                evt.App, evt.Direction, evt.Remote, evt.Port, evt.Protocol));

            lock (blockHistory)
            {
                blockHistory.Add(new BlockRecord { Time = DateTime.Now, Evt = evt });
                if (blockHistory.Count > 1000) blockHistory.RemoveRange(0, blockHistory.Count - 1000);
            }

            if (!notifyToggle.Checked) return;
            if (!Throttler.ShouldNotify(evt.App, evt.Remote.ToString())) return;

            string title = evt.Direction == "인바운드"
                ? "중국발 접속 시도 차단됨"
                : "중국행 연결 시도 차단됨";
            string body = string.Format("{0}\r\n{1} {2}:{3} ({4})",
                evt.App, evt.Direction, evt.Remote, evt.Port, evt.Protocol);
            Balloon(title, body, ToolTipIcon.Warning);
        }

        void ReapplyRules()
        {
            SetStatus("방화벽 규칙 적용 중...");
            var cidrs = ChinaIpList.LoadOrDownload(false);
            chinaSet = IpRangeSet.FromCidrs(cidrs);
            int n = FirewallService.ApplyRules(cidrs);
            SetStatus("보호 활성화됨");
            Balloon("규칙 적용 완료", string.Format("방화벽 규칙 {0}개 적용됨", n), ToolTipIcon.Info);
        }

        void UpdateLists()
        {
            SetStatus("목록 업데이트 중...");
            var cidrs = ChinaIpList.LoadOrDownload(true);
            chinaSet = IpRangeSet.FromCidrs(cidrs);
            int n = FirewallService.ApplyRules(cidrs);
            try { geoDb = GeoDb.LoadOrDownload(true); }
            catch (Exception ex) { Logger.Log("GeoIP DB 업데이트 실패: " + ex.Message); }
            try { asnDb = AsnDb.LoadOrDownload(true); }
            catch (Exception ex) { Logger.Log("ASN DB 업데이트 실패: " + ex.Message); }
            SetStatus("보호 활성화됨");
            Balloon("업데이트 완료",
                string.Format("중국 IP 대역 {0}개, 방화벽 규칙 {1}개 적용", chinaSet.Count, n),
                ToolTipIcon.Info);
        }

        void OnRemoveRules(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "방화벽에서 ChinaGuard 차단 규칙을 모두 제거합니다.\r\n중국 IP 차단이 해제됩니다. 계속할까요?",
                "ChinaGuard", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (result != DialogResult.Yes) return;
            RunBusy(delegate
            {
                int n = FirewallService.RemoveRules();
                SetStatus("차단 해제됨 (규칙 없음)");
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
                string msg = string.Format(
                    "방화벽 규칙: {0}\r\n중국 IP 대역: {1}개 (목록: {2})\r\nGeoIP DB: {3}\r\nASN DB: {4}\r\n이벤트 감시: {5}\r\n차단 감지: {6}건 (시작 이후)\r\n로그: {7}",
                    rules < 0 ? "확인 실패" : rules + "개",
                    chinaSet == null ? 0 : chinaSet.Count,
                    ChinaIpList.CacheExists() ? ChinaIpList.CacheDate().ToString("yyyy-MM-dd") : "없음",
                    geoDb == null ? "없음" : string.Format("{0}개 대역 ({1:yyyy-MM-dd})", geoDb.Count, GeoDb.CacheDate()),
                    asnDb == null ? "없음" : string.Format("{0}개 대역 ({1:yyyy-MM-dd})", asnDb.Count, AsnDb.CacheDate()),
                    watcher == null ? "중지됨" : "실행 중",
                    blocked,
                    Logger.LogPath);
                ui.Post(delegate(object s)
                {
                    MessageBox.Show(msg, "ChinaGuard 상태", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            return RunSchtasks("/Query /TN \"ChinaGuard\"") == 0;
        }

        void OnToggleAutoStart(object sender, EventArgs e)
        {
            if (autoStartToggle.Checked)
            {
                if (RunSchtasks("/Delete /TN \"ChinaGuard\" /F") == 0)
                    autoStartToggle.Checked = false;
            }
            else
            {
                string exe = Application.ExecutablePath;
                int code = RunSchtasks(string.Format(
                    "/Create /TN \"ChinaGuard\" /TR \"\\\"{0}\\\"\" /SC ONLOGON /RL HIGHEST /F", exe));
                if (code == 0) autoStartToggle.Checked = true;
                else MessageBox.Show("자동 실행 등록 실패 (schtasks exit " + code + ")", "ChinaGuard");
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
                Balloon("ChinaGuard", "다른 작업이 진행 중입니다.", ToolTipIcon.Info);
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
