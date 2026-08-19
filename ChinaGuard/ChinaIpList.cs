using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace ChinaGuard
{
    // 중국 CIDR 목록 (APNIC 위임 데이터 기반, ipverse/rir-ip 저장소)
    static class ChinaIpList
    {
        const string UrlV4 = "https://raw.githubusercontent.com/ipverse/rir-ip/master/country/cn/ipv4-aggregated.txt";
        const string UrlV6 = "https://raw.githubusercontent.com/ipverse/rir-ip/master/country/cn/ipv6-aggregated.txt";

        static string BaseDir { get { return AppDomain.CurrentDomain.BaseDirectory; } }
        public static string CachePathV4 { get { return Path.Combine(BaseDir, "china-ipv4.txt"); } }
        public static string CachePathV6 { get { return Path.Combine(BaseDir, "china-ipv6.txt"); } }

        public static bool CacheExists()
        {
            return File.Exists(CachePathV4);
        }

        public static DateTime CacheDate()
        {
            return CacheExists() ? File.GetLastWriteTime(CachePathV4) : DateTime.MinValue;
        }

        public static List<string> LoadOrDownload(bool forceDownload)
        {
            if (forceDownload || !CacheExists())
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                using (var wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "ChinaGuard/1.0");
                    string v4 = wc.DownloadString(UrlV4);
                    File.WriteAllText(CachePathV4, v4);
                    try
                    {
                        string v6 = wc.DownloadString(UrlV6);
                        File.WriteAllText(CachePathV6, v6);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log("IPv6 목록 다운로드 실패(무시): " + ex.Message);
                    }
                }
                Logger.Log("중국 IP 목록 다운로드 완료");
            }

            var cidrs = new List<string>();
            AppendLines(CachePathV4, cidrs);
            AppendLines(CachePathV6, cidrs);
            return cidrs;
        }

        static void AppendLines(string path, List<string> target)
        {
            if (!File.Exists(path)) return;
            foreach (var line in File.ReadAllLines(path))
            {
                string t = line.Trim();
                if (t.Length > 0 && !t.StartsWith("#")) target.Add(t);
            }
        }
    }
}
