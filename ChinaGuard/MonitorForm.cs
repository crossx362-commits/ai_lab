using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;

namespace ChinaGuard
{
    // 실시간 연결 모니터: 프로세스/국가/조직 표시, 필터, 정렬, 프로세스 종료, IP 정보
    class MonitorForm : Form
    {
        class ProcInfo
        {
            public string Name;
            public string Desc;
            public string FullPath;
        }

        class RowSorter : IComparer
        {
            public int Column;
            public bool Ascending = true;
            static readonly HashSet<int> NumericCols = new HashSet<int> { 1, 4 }; // PID, 포트

            public int Compare(object x, object y)
            {
                var a = (ListViewItem)x;
                var b = (ListViewItem)y;
                string sa = Column < a.SubItems.Count ? a.SubItems[Column].Text : "";
                string sb = Column < b.SubItems.Count ? b.SubItems[Column].Text : "";
                int result;
                if (NumericCols.Contains(Column))
                {
                    int na, nb;
                    int.TryParse(sa, out na);
                    int.TryParse(sb, out nb);
                    result = na.CompareTo(nb);
                }
                else
                {
                    result = string.Compare(sa, sb, StringComparison.OrdinalIgnoreCase);
                }
                return Ascending ? result : -result;
            }
        }

        readonly IpInfoProvider provider;
        readonly ListView list;
        readonly Label statusLabel;
        readonly CheckBox onlyEstablished;
        readonly TextBox filterBox;
        readonly Timer timer;
        readonly RowSorter sorter = new RowSorter { Column = -1 };
        readonly Dictionary<int, ProcInfo> procCache = new Dictionary<int, ProcInfo>();

        public MonitorForm(IpInfoProvider provider)
        {
            this.provider = provider;

            Text = "ChinaGuard - 실시간 연결 모니터";
            Width = 1250;
            Height = 640;
            StartPosition = FormStartPosition.CenterScreen;

            statusLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0)
            };

            var topPanel = new Panel { Dock = DockStyle.Top, Height = 30, Padding = new Padding(6, 3, 6, 3) };
            var filterLabel = new Label
            {
                Text = "필터:",
                Dock = DockStyle.Left,
                Width = 40,
                TextAlign = ContentAlignment.MiddleLeft
            };
            filterBox = new TextBox { Dock = DockStyle.Left, Width = 220 };
            filterBox.TextChanged += delegate { Refresh_(); };
            onlyEstablished = new CheckBox
            {
                Text = "연결된 것만 보기 (ESTABLISHED)",
                Dock = DockStyle.Left,
                Width = 240,
                Checked = true,
                Padding = new Padding(12, 0, 0, 0)
            };
            onlyEstablished.CheckedChanged += delegate { Refresh_(); };
            topPanel.Controls.Add(onlyEstablished);
            topPanel.Controls.Add(filterBox);
            topPanel.Controls.Add(filterLabel);

            list = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                HideSelection = false,
                ShowItemToolTips = true,
            };
            list.Columns.Add("프로세스", 130);
            list.Columns.Add("PID", 55);
            list.Columns.Add("설명", 190);
            list.Columns.Add("원격 주소", 220);
            list.Columns.Add("포트", 55);
            list.Columns.Add("국가", 120);
            list.Columns.Add("조직 / ISP", 230);
            list.Columns.Add("상태", 100);
            list.ColumnClick += OnColumnClick;

            var infoItem = new MenuItem("IP 정보 보기", OnShowIpInfo);
            var copyIpItem = new MenuItem("IP 복사", OnCopyIp);
            var killItem = new MenuItem("프로세스 강제 종료", OnKillProcess);
            var openDirItem = new MenuItem("파일 위치 열기", OnOpenFileLocation);
            list.ContextMenu = new ContextMenu(new[]
            {
                infoItem, copyIpItem,
                new MenuItem("-"),
                killItem, openDirItem
            });
            list.ContextMenu.Popup += delegate
            {
                bool has = list.SelectedItems.Count > 0;
                infoItem.Enabled = has;
                copyIpItem.Enabled = has;
                killItem.Enabled = has;
                openDirItem.Enabled = has;
            };
            list.DoubleClick += OnShowIpInfo;

            Controls.Add(list);
            Controls.Add(topPanel);
            Controls.Add(statusLabel);

            timer = new Timer { Interval = 2000 };
            timer.Tick += delegate { Refresh_(); };
            timer.Start();
            Shown += delegate { Refresh_(); };
            FormClosed += delegate { timer.Stop(); };
        }

        void OnColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (sorter.Column == e.Column) sorter.Ascending = !sorter.Ascending;
            else { sorter.Column = e.Column; sorter.Ascending = true; }
            list.ListViewItemSorter = sorter;
            list.Sort();
        }

        void Refresh_()
        {
            List<ConnRow> conns;
            try { conns = NativeTcpTable.GetConnections(); }
            catch (Exception ex)
            {
                statusLabel.Text = "연결 조회 실패: " + ex.Message;
                return;
            }

            var selectedKeys = new HashSet<string>();
            foreach (ListViewItem sel in list.SelectedItems)
                selectedKeys.Add(RowKey(sel));

            string filter = filterBox.Text.Trim();
            int total = 0, chinaCount = 0, shown = 0;
            var items = new List<ListViewItem>();
            foreach (var c in conns)
            {
                if (IsLocalOrEmpty(c.Remote)) continue;
                if (onlyEstablished.Checked && c.State != "ESTABLISHED") continue;
                total++;

                string cc = provider.Country == null ? null : provider.Country(c.Remote);
                bool isChina = cc == "CN";
                if (isChina) chinaCount++;

                ProcInfo info = GetProcInfo(c.Pid);
                string org = provider.Org == null ? null : provider.Org(c.Remote);
                string countryDisplay = GeoDb.DisplayName(cc);

                if (filter.Length > 0 && !MatchesFilter(filter, info, c, countryDisplay, org))
                    continue;
                shown++;

                var item = new ListViewItem(info.Name);
                item.SubItems.Add(c.Pid.ToString());
                item.SubItems.Add(info.Desc);
                item.SubItems.Add(c.Remote.ToString());
                item.SubItems.Add(c.RemotePort.ToString());
                item.SubItems.Add(countryDisplay);
                item.SubItems.Add(org ?? "");
                item.SubItems.Add(c.State);
                item.Tag = c;
                item.ToolTipText = info.FullPath;
                if (isChina)
                {
                    item.BackColor = Color.MistyRose;
                    item.ForeColor = Color.DarkRed;
                }
                items.Add(item);
            }

            list.BeginUpdate();
            list.ListViewItemSorter = null;
            list.Items.Clear();
            list.Items.AddRange(items.ToArray());
            foreach (ListViewItem item in list.Items)
                if (selectedKeys.Contains(RowKey(item))) item.Selected = true;
            if (sorter.Column >= 0)
            {
                list.ListViewItemSorter = sorter;
                list.Sort();
            }
            list.EndUpdate();

            statusLabel.Text = string.Format(
                "외부 연결 {0}개 (표시 {1}개)  |  중국(CN) 연결 {2}개  |  갱신: {3:HH:mm:ss}",
                total, shown, chinaCount, DateTime.Now);
        }

        static bool MatchesFilter(string filter, ProcInfo info, ConnRow c, string country, string org)
        {
            return ContainsIgnoreCase(info.Name, filter)
                || ContainsIgnoreCase(info.Desc, filter)
                || ContainsIgnoreCase(c.Remote.ToString(), filter)
                || ContainsIgnoreCase(country, filter)
                || ContainsIgnoreCase(org, filter);
        }

        static bool ContainsIgnoreCase(string haystack, string needle)
        {
            if (string.IsNullOrEmpty(haystack)) return false;
            return haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static string RowKey(ListViewItem item)
        {
            return item.SubItems[1].Text + "|" + item.SubItems[3].Text + ":" + item.SubItems[4].Text;
        }

        void OnShowIpInfo(object sender, EventArgs e)
        {
            if (list.SelectedItems.Count == 0) return;
            var c = (ConnRow)list.SelectedItems[0].Tag;
            ProcInfo info = GetProcInfo(c.Pid);
            using (var f = new IpInfoForm(c.Remote, provider, info.Name, info.FullPath))
                f.ShowDialog(this);
        }

        void OnCopyIp(object sender, EventArgs e)
        {
            if (list.SelectedItems.Count == 0) return;
            var c = (ConnRow)list.SelectedItems[0].Tag;
            try { Clipboard.SetText(c.Remote.ToString()); } catch { }
        }

        void OnKillProcess(object sender, EventArgs e)
        {
            if (list.SelectedItems.Count == 0) return;
            var c = (ConnRow)list.SelectedItems[0].Tag;
            string name = list.SelectedItems[0].Text;

            var result = MessageBox.Show(this,
                string.Format("프로세스 '{0}' (PID {1})을(를) 강제 종료할까요?\r\n저장하지 않은 데이터가 사라질 수 있습니다.", name, c.Pid),
                "프로세스 강제 종료", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (result != DialogResult.Yes) return;

            try
            {
                var proc = Process.GetProcessById(c.Pid);
                proc.Kill();
                Logger.Log(string.Format("프로세스 강제 종료: {0} (PID {1})", name, c.Pid));
                procCache.Remove(c.Pid);
                Refresh_();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "종료 실패: " + ex.Message, "ChinaGuard",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void OnOpenFileLocation(object sender, EventArgs e)
        {
            if (list.SelectedItems.Count == 0) return;
            var c = (ConnRow)list.SelectedItems[0].Tag;
            ProcInfo info = GetProcInfo(c.Pid);
            if (string.IsNullOrEmpty(info.FullPath))
            {
                MessageBox.Show(this, "파일 경로를 확인할 수 없습니다.", "ChinaGuard");
                return;
            }
            try { Process.Start("explorer.exe", "/select,\"" + info.FullPath + "\""); }
            catch { }
        }

        static bool IsLocalOrEmpty(IPAddress ip)
        {
            if (ip == null) return true;
            if (IPAddress.IsLoopback(ip)) return true;
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                byte[] b = ip.GetAddressBytes();
                if (b[0] == 0) return true;
                if (b[0] == 10) return true;
                if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true;
                if (b[0] == 192 && b[1] == 168) return true;
                if (b[0] == 169 && b[1] == 254) return true;
            }
            else
            {
                byte[] b = ip.GetAddressBytes();
                bool allZero = true;
                for (int i = 0; i < b.Length; i++) if (b[i] != 0) { allZero = false; break; }
                if (allZero) return true;
                if (b[0] == 0xFE && (b[1] & 0xC0) == 0x80) return true;  // link-local
                if ((b[0] & 0xFE) == 0xFC) return true;                  // unique local
            }
            return false;
        }

        ProcInfo GetProcInfo(int pid)
        {
            ProcInfo info;
            if (procCache.TryGetValue(pid, out info)) return info;

            info = new ProcInfo { Name = "(종료됨)", Desc = "", FullPath = "" };
            try
            {
                var proc = Process.GetProcessById(pid);
                info.Name = proc.ProcessName;
                try
                {
                    info.FullPath = proc.MainModule.FileName;
                    var ver = FileVersionInfo.GetVersionInfo(info.FullPath);
                    info.Desc = ver.FileDescription ?? "";
                }
                catch
                {
                    // 보호된 시스템 프로세스 등은 경로 조회 불가
                    info.Desc = "(시스템/접근 불가)";
                }
            }
            catch { }

            if (procCache.Count > 500) procCache.Clear();
            procCache[pid] = info;
            return info;
        }
    }
}
