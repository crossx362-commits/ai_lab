using System;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Net;
using System.Xml;

namespace GeoGuard
{
    class BlockEvent
    {
        public string App;
        public string Direction;   // "인바운드" / "아웃바운드"
        public IPAddress Remote;
        public string Port;
        public string Protocol;
    }

    // WFP 차단 감사 이벤트(보안 로그 5157)를 구독해 중국 IP 차단을 실시간 감지
    class AuditWatcher : IDisposable
    {
        // "Filtering Platform Connection" 하위 카테고리 GUID (언어 무관)
        const string AuditSubcategoryGuid = "{0CCE9226-69AE-11D9-BED3-505054503030}";

        EventLogWatcher watcher;
        readonly Func<IPAddress, bool> isChina;
        readonly Action<BlockEvent> onBlock;

        public AuditWatcher(Func<IPAddress, bool> isChina, Action<BlockEvent> onBlock)
        {
            this.isChina = isChina;
            this.onBlock = onBlock;
        }

        public static void EnableAuditPolicy()
        {
            var psi = new ProcessStartInfo
            {
                FileName = "auditpol.exe",
                Arguments = string.Format("/set /subcategory:\"{0}\" /failure:enable", AuditSubcategoryGuid),
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (var p = Process.Start(psi))
            {
                p.WaitForExit(10000);
                if (p.ExitCode != 0)
                    Logger.Log("감사 정책 설정 실패 (exit " + p.ExitCode + ")");
                else
                    Logger.Log("WFP 차단 감사 정책 활성화");
            }
        }

        public void Start()
        {
            var query = new EventLogQuery("Security", PathType.LogName, "*[System[(EventID=5157)]]");
            watcher = new EventLogWatcher(query);
            watcher.EventRecordWritten += OnEvent;
            watcher.Enabled = true;
            Logger.Log("이벤트 감시 시작 (Security 5157)");
        }

        void OnEvent(object sender, EventRecordWrittenEventArgs e)
        {
            if (e.EventRecord == null) return;
            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(e.EventRecord.ToXml());
                var ns = new XmlNamespaceManager(doc.NameTable);
                ns.AddNamespace("ev", "http://schemas.microsoft.com/win/2004/08/events/event");

                string app = GetData(doc, ns, "Application");
                string dirRaw = GetData(doc, ns, "Direction");
                string source = GetData(doc, ns, "SourceAddress");
                string dest = GetData(doc, ns, "DestAddress");
                string destPort = GetData(doc, ns, "DestPort");
                string sourcePort = GetData(doc, ns, "SourcePort");
                string proto = GetData(doc, ns, "Protocol");

                bool inbound = dirRaw != null && dirRaw.Contains("14592");
                string remoteStr = inbound ? source : dest;
                IPAddress remote;
                if (!IPAddress.TryParse(remoteStr, out remote)) return;
                if (!isChina(remote)) return;

                var evt = new BlockEvent
                {
                    App = PrettyAppName(app),
                    Direction = inbound ? "인바운드" : "아웃바운드",
                    Remote = remote,
                    Port = inbound ? sourcePort : destPort,
                    Protocol = proto == "6" ? "TCP" : proto == "17" ? "UDP" : proto
                };
                onBlock(evt);
            }
            catch (Exception ex)
            {
                Logger.Log("이벤트 처리 오류: " + ex.Message);
            }
        }

        static string GetData(XmlDocument doc, XmlNamespaceManager ns, string name)
        {
            var node = doc.SelectSingleNode(
                string.Format("//ev:EventData/ev:Data[@Name='{0}']", name), ns);
            return node == null ? null : node.InnerText;
        }

        static string PrettyAppName(string devicePath)
        {
            if (string.IsNullOrEmpty(devicePath)) return "(알 수 없음)";
            try { return Path.GetFileName(devicePath.Replace('/', '\\')); }
            catch { return devicePath; }
        }

        public void Dispose()
        {
            if (watcher != null)
            {
                watcher.Enabled = false;
                watcher.Dispose();
                watcher = null;
            }
        }
    }
}
