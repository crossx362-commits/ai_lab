using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;

namespace ChinaGuard
{
    // DB-IP Country Lite (CC BY 4.0) 기반 IP -> 국가코드 조회
    class GeoDb
    {
        uint[] v4Starts; uint[] v4Ends; string[] v4Cc;
        byte[][] v6Starts; byte[][] v6Ends; string[] v6Cc;

        public int Count
        {
            get
            {
                return (v4Starts == null ? 0 : v4Starts.Length)
                     + (v6Starts == null ? 0 : v6Starts.Length);
            }
        }

        static string CachePath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dbip-country-lite.csv"); }
        }

        public static bool CacheExists() { return File.Exists(CachePath); }

        public static DateTime CacheDate()
        {
            return CacheExists() ? File.GetLastWriteTime(CachePath) : DateTime.MinValue;
        }

        public static GeoDb LoadOrDownload(bool forceDownload)
        {
            if (forceDownload || !CacheExists()) Download();
            return Parse(CachePath);
        }

        static void Download()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            DateTime month = DateTime.UtcNow;
            Exception last = null;
            for (int i = 0; i < 3; i++)
            {
                string url = string.Format(
                    "https://download.db-ip.com/free/dbip-country-lite-{0:yyyy-MM}.csv.gz", month);
                try
                {
                    string tmp = CachePath + ".tmp";
                    var req = (HttpWebRequest)WebRequest.Create(url);
                    req.UserAgent = "ChinaGuard/1.0";
                    using (var resp = req.GetResponse())
                    using (var gz = new GZipStream(resp.GetResponseStream(), CompressionMode.Decompress))
                    using (var outFs = File.Create(tmp))
                    {
                        gz.CopyTo(outFs);
                    }
                    if (File.Exists(CachePath)) File.Delete(CachePath);
                    File.Move(tmp, CachePath);
                    Logger.Log("GeoIP DB 다운로드 완료: " + url);
                    return;
                }
                catch (Exception ex)
                {
                    last = ex;
                    month = month.AddMonths(-1);
                }
            }
            throw new Exception("GeoIP DB 다운로드 실패", last);
        }

        static GeoDb Parse(string path)
        {
            var v4s = new List<uint>(); var v4e = new List<uint>(); var v4c = new List<string>();
            var v6s = new List<byte[]>(); var v6e = new List<byte[]>(); var v6c = new List<string>();
            var ccPool = new Dictionary<string, string>();

            using (var reader = new StreamReader(path))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Length == 0) continue;
                    string[] parts = line.Split(',');
                    if (parts.Length < 3) continue;
                    IPAddress start, end;
                    if (!IPAddress.TryParse(parts[0], out start)) continue;
                    if (!IPAddress.TryParse(parts[1], out end)) continue;
                    string cc = parts[2].Trim().ToUpperInvariant();
                    string pooled;
                    if (!ccPool.TryGetValue(cc, out pooled)) { ccPool[cc] = cc; pooled = cc; }

                    if (start.AddressFamily == AddressFamily.InterNetwork)
                    {
                        v4s.Add(IpRangeSet.ToUInt(start));
                        v4e.Add(IpRangeSet.ToUInt(end));
                        v4c.Add(pooled);
                    }
                    else if (start.AddressFamily == AddressFamily.InterNetworkV6)
                    {
                        v6s.Add(start.GetAddressBytes());
                        v6e.Add(end.GetAddressBytes());
                        v6c.Add(pooled);
                    }
                }
            }

            var db = new GeoDb();
            db.v4Starts = v4s.ToArray(); db.v4Ends = v4e.ToArray(); db.v4Cc = v4c.ToArray();
            db.v6Starts = v6s.ToArray(); db.v6Ends = v6e.ToArray(); db.v6Cc = v6c.ToArray();
            SortV4(db); SortV6(db);
            return db;
        }

        static void SortV4(GeoDb db)
        {
            int[] idx = MakeIndex(db.v4Starts.Length);
            uint[] starts = db.v4Starts;
            Array.Sort(idx, delegate(int a, int b) { return starts[a].CompareTo(starts[b]); });
            db.v4Starts = Reorder(db.v4Starts, idx);
            db.v4Ends = Reorder(db.v4Ends, idx);
            db.v4Cc = Reorder(db.v4Cc, idx);
        }

        static void SortV6(GeoDb db)
        {
            int[] idx = MakeIndex(db.v6Starts.Length);
            byte[][] starts = db.v6Starts;
            Array.Sort(idx, delegate(int a, int b) { return IpRangeSet.CompareBytes(starts[a], starts[b]); });
            db.v6Starts = Reorder(db.v6Starts, idx);
            db.v6Ends = Reorder(db.v6Ends, idx);
            db.v6Cc = Reorder(db.v6Cc, idx);
        }

        static int[] MakeIndex(int n)
        {
            int[] idx = new int[n];
            for (int i = 0; i < n; i++) idx[i] = i;
            return idx;
        }

        static T[] Reorder<T>(T[] src, int[] idx)
        {
            T[] dst = new T[src.Length];
            for (int i = 0; i < idx.Length; i++) dst[i] = src[idx[i]];
            return dst;
        }

        // 국가코드 반환, 미확인 시 null
        public string Lookup(IPAddress ip)
        {
            if (ip == null) return null;
            if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();

            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                if (v4Starts == null || v4Starts.Length == 0) return null;
                uint addr = IpRangeSet.ToUInt(ip);
                int lo = 0, hi = v4Starts.Length;
                while (lo < hi)
                {
                    int mid = (lo + hi) / 2;
                    if (v4Starts[mid] <= addr) lo = mid + 1; else hi = mid;
                }
                int i = lo - 1;
                if (i >= 0 && addr <= v4Ends[i]) return v4Cc[i];
                return null;
            }
            if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            {
                if (v6Starts == null || v6Starts.Length == 0) return null;
                byte[] addr = ip.GetAddressBytes();
                int lo = 0, hi = v6Starts.Length;
                while (lo < hi)
                {
                    int mid = (lo + hi) / 2;
                    if (IpRangeSet.CompareBytes(v6Starts[mid], addr) <= 0) lo = mid + 1; else hi = mid;
                }
                int i = lo - 1;
                if (i >= 0 && IpRangeSet.CompareBytes(addr, v6Ends[i]) <= 0) return v6Cc[i];
                return null;
            }
            return null;
        }

        static readonly Dictionary<string, string> KoreanNames = new Dictionary<string, string>
        {
            { "KR", "한국" }, { "CN", "중국" }, { "US", "미국" }, { "JP", "일본" },
            { "HK", "홍콩" }, { "TW", "대만" }, { "SG", "싱가포르" }, { "DE", "독일" },
            { "GB", "영국" }, { "FR", "프랑스" }, { "NL", "네덜란드" }, { "RU", "러시아" },
            { "IN", "인도" }, { "AU", "호주" }, { "CA", "캐나다" }, { "BR", "브라질" },
            { "IE", "아일랜드" }, { "SE", "스웨덴" }, { "FI", "핀란드" }, { "VN", "베트남" },
            { "TH", "태국" }, { "ID", "인도네시아" }, { "MY", "말레이시아" }, { "PH", "필리핀" },
        };

        public static string DisplayName(string cc)
        {
            if (cc == null) return "?";
            string name;
            if (KoreanNames.TryGetValue(cc, out name)) return cc + " " + name;
            return cc;
        }
    }
}
