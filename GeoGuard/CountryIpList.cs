using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace GeoGuard
{
    // 국가별 CIDR 목록 (RIR 위임 데이터 기반, ipverse/rir-ip 저장소)
    static class CountryIpList
    {
        const string UrlFormat =
            "https://raw.githubusercontent.com/ipverse/rir-ip/master/country/{0}/ipv{1}-aggregated.txt";

        static string BaseDir { get { return AppDomain.CurrentDomain.BaseDirectory; } }

        static string CachePath(string cc, int ver)
        {
            return Path.Combine(BaseDir, string.Format("iplist-{0}-v{1}.txt", cc.ToLowerInvariant(), ver));
        }

        public static bool CacheExists(string cc)
        {
            return File.Exists(CachePath(cc, 4));
        }

        public static DateTime CacheDate(string cc)
        {
            return CacheExists(cc) ? File.GetLastWriteTime(CachePath(cc, 4)) : DateTime.MinValue;
        }

        public static List<string> LoadOrDownload(string cc, bool forceDownload)
        {
            cc = cc.ToLowerInvariant();
            if (forceDownload || !CacheExists(cc))
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                using (var wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "GeoGuard/1.0");
                    string v4 = wc.DownloadString(string.Format(UrlFormat, cc, 4));
                    File.WriteAllText(CachePath(cc, 4), v4);
                    try
                    {
                        string v6 = wc.DownloadString(string.Format(UrlFormat, cc, 6));
                        File.WriteAllText(CachePath(cc, 6), v6);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(string.Format("{0} IPv6 목록 다운로드 실패(무시): {1}",
                            cc.ToUpperInvariant(), ex.Message));
                    }
                }
                Logger.Log(cc.ToUpperInvariant() + " IP 목록 다운로드 완료");
            }

            var cidrs = new List<string>();
            AppendLines(CachePath(cc, 4), cidrs);
            AppendLines(CachePath(cc, 6), cidrs);
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
