using System;
using System.Drawing;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ChinaGuard
{
    // IP 주소 상세 정보 창 (국가, 조직, 역방향 DNS, 프로세스)
    class IpInfoForm : Form
    {
        readonly TextBox box;
        readonly IPAddress ip;

        public IpInfoForm(IPAddress ip, IpInfoProvider provider, string procName, string procPath)
        {
            this.ip = ip;

            Text = "IP 정보 - " + ip;
            Width = 560;
            Height = 360;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;

            box = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 10f),
                BackColor = SystemColors.Window,
            };

            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(4)
            };
            var closeBtn = new Button { Text = "닫기", Width = 90 };
            closeBtn.Click += delegate { Close(); };
            var copyBtn = new Button { Text = "IP 복사", Width = 90 };
            copyBtn.Click += delegate
            {
                try { Clipboard.SetText(ip.ToString()); } catch { }
            };
            var copyAllBtn = new Button { Text = "전체 복사", Width = 90 };
            copyAllBtn.Click += delegate
            {
                try { Clipboard.SetText(box.Text); } catch { }
            };
            panel.Controls.Add(closeBtn);
            panel.Controls.Add(copyAllBtn);
            panel.Controls.Add(copyBtn);

            Controls.Add(box);
            Controls.Add(panel);

            string cc = provider.Country == null ? null : provider.Country(ip);
            string org = provider.Org == null ? null : provider.Org(ip);

            box.Text = BuildText(cc, org, "(조회 중...)", procName, procPath);
            box.SelectionStart = 0;
            box.SelectionLength = 0;

            ResolveDnsAsync(cc, org, procName, procPath);
        }

        string BuildText(string cc, string org, string rdns, string procName, string procPath)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("IP 주소     : " + ip);
            sb.AppendLine("주소 체계   : " + (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? "IPv4" : "IPv6"));
            sb.AppendLine("국가        : " + GeoDb.DisplayName(cc));
            sb.AppendLine("조직 / ISP  : " + (org ?? "(정보 없음)"));
            sb.AppendLine("역방향 DNS  : " + rdns);
            if (!string.IsNullOrEmpty(procName))
            {
                sb.AppendLine();
                sb.AppendLine("연결 프로세스: " + procName);
                if (!string.IsNullOrEmpty(procPath))
                    sb.AppendLine("경로        : " + procPath);
            }
            return sb.ToString();
        }

        void ResolveDnsAsync(string cc, string org, string procName, string procPath)
        {
            Task.Run(delegate
            {
                string rdns;
                try
                {
                    var entry = Dns.GetHostEntry(ip);
                    rdns = string.IsNullOrEmpty(entry.HostName) ? "(없음)" : entry.HostName;
                }
                catch
                {
                    rdns = "(등록되지 않음)";
                }
                try
                {
                    BeginInvoke(new Action(delegate
                    {
                        box.Text = BuildText(cc, org, rdns, procName, procPath);
                        box.SelectionStart = 0;
                        box.SelectionLength = 0;
                    }));
                }
                catch { }
            });
        }
    }
}
