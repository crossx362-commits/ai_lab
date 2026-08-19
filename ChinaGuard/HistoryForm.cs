using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Windows.Forms;

namespace ChinaGuard
{
    class BlockRecord
    {
        public DateTime Time;
        public BlockEvent Evt;
    }

    // 차단 이력 창: 감시 시작 이후 차단된 중국 IP 통신 시도 목록
    class HistoryForm : Form
    {
        readonly Func<List<BlockRecord>> snapshot;
        readonly IpInfoProvider provider;
        readonly ListView list;
        readonly Label statusLabel;
        readonly Timer timer;
        int lastCount = -1;

        public HistoryForm(Func<List<BlockRecord>> snapshot, IpInfoProvider provider)
        {
            this.snapshot = snapshot;
            this.provider = provider;

            Text = "ChinaGuard - 차단 이력";
            Width = 900;
            Height = 560;
            StartPosition = FormStartPosition.CenterScreen;

            statusLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0)
            };

            list = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                HideSelection = false,
            };
            list.Columns.Add("시간", 130);
            list.Columns.Add("프로세스", 150);
            list.Columns.Add("방향", 80);
            list.Columns.Add("원격 IP", 230);
            list.Columns.Add("포트", 60);
            list.Columns.Add("프로토콜", 70);
            list.Columns.Add("조직 / ISP", 200);

            var infoItem = new MenuItem("IP 정보 보기", OnShowIpInfo);
            var copyItem = new MenuItem("IP 복사", OnCopyIp);
            list.ContextMenu = new ContextMenu(new[] { infoItem, copyItem });
            list.DoubleClick += OnShowIpInfo;

            Controls.Add(list);
            Controls.Add(statusLabel);

            timer = new Timer { Interval = 2000 };
            timer.Tick += delegate { Refresh_(); };
            timer.Start();
            Shown += delegate { Refresh_(); };
            FormClosed += delegate { timer.Stop(); };
        }

        void Refresh_()
        {
            var records = snapshot();
            if (records.Count == lastCount) return;
            lastCount = records.Count;

            var items = new List<ListViewItem>();
            for (int i = records.Count - 1; i >= 0; i--)   // 최신이 위로
            {
                var r = records[i];
                var item = new ListViewItem(r.Time.ToString("MM-dd HH:mm:ss"));
                item.SubItems.Add(r.Evt.App);
                item.SubItems.Add(r.Evt.Direction);
                item.SubItems.Add(r.Evt.Remote.ToString());
                item.SubItems.Add(r.Evt.Port);
                item.SubItems.Add(r.Evt.Protocol);
                string org = provider.Org == null ? null : provider.Org(r.Evt.Remote);
                item.SubItems.Add(org ?? "");
                item.Tag = r.Evt.Remote;
                if (r.Evt.Direction == "인바운드")
                    item.ForeColor = Color.DarkRed;
                items.Add(item);
            }

            list.BeginUpdate();
            list.Items.Clear();
            list.Items.AddRange(items.ToArray());
            list.EndUpdate();

            statusLabel.Text = string.Format("차단 시도 {0}건 (프로그램 시작 이후)", records.Count);
        }

        void OnShowIpInfo(object sender, EventArgs e)
        {
            if (list.SelectedItems.Count == 0) return;
            var ip = (IPAddress)list.SelectedItems[0].Tag;
            string procName = list.SelectedItems[0].SubItems[1].Text;
            using (var f = new IpInfoForm(ip, provider, procName, null))
                f.ShowDialog(this);
        }

        void OnCopyIp(object sender, EventArgs e)
        {
            if (list.SelectedItems.Count == 0) return;
            try { Clipboard.SetText(list.SelectedItems[0].Tag.ToString()); } catch { }
        }
    }
}
