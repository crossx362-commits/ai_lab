using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;

namespace ChinaGuard
{
    // 모니터/정보창에서 쓰는 IP 조회 델리게이트 묶음
    class IpInfoProvider
    {
        public Func<IPAddress, string> Country;   // ISO 코드 또는 null
        public Func<IPAddress, string> Org;       // "AS13335 CLOUDFLARENET" 또는 null
    }

    // DB-IP ASN Lite (CC BY 4.0) 기반 IP -> AS번호/조직명 조회
    class AsnDb
    {
        uint[] v4Starts; uint[] v4Ends; string[] v4Org;
        byte[][] v6Starts; byte[][] v6Ends; string[] v6Org;

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
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dbip-asn-lite.csv"); }
        }

        public static bool CacheExists() { return File.Exists(CachePath); }

        public static DateTime CacheDate()
        {
            return CacheExists() ? File.GetLastWriteTime(CachePath) : DateTime.MinValue;
        }

        public static AsnDb LoadOrDownload(bool forceDownload)
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
                    "https://download.db-ip.com/free/dbip-asn-lite-{0:yyyy-MM}.csv.gz", month);
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
                    Logger.Log("ASN DB 다운로드 완료: " + url);
                    return;
                }
                catch (Exception ex)
                {
                    last = ex;
                    month = month.AddMonths(-1);
                }
            }
            throw new Exception("ASN DB 다운로드 실패", last);
        }

        static AsnDb Parse(string path)
        {
            var v4s = new List<uint>(); var v4e = new List<uint>(); var v4o = new List<string>();
            var v6s = new List<byte[]>(); var v6e = new List<byte[]>(); var v6o = new List<string>();
            var pool = new Dictionary<string, string>();

            using (var reader = new StreamReader(path))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Length == 0) continue;
                    // 형식: start,end,asn,org  (org에 쉼표 포함 가능 -> 최대 4조각 분리)
                    string[] parts = line.Split(new[] { ',' }, 4);
                    if (parts.Length < 4) continue;
                    IPAddress start, end;
                    if (!IPAddress.TryParse(parts[0], out start)) continue;
                    if (!IPAddress.TryParse(parts[1], out end)) continue;
                    string org = parts[3].Trim().Trim('"');
                    string label = "AS" + parts[2].Trim() + " " + org;
                    string pooled;
                    if (!pool.TryGetValue(label, out pooled)) { pool[label] = label; pooled = label; }

                    if (start.AddressFamily == AddressFamily.InterNetwork)
                    {
                        v4s.Add(IpRangeSet.ToUInt(start));
                        v4e.Add(IpRangeSet.ToUInt(end));
                        v4o.Add(pooled);
                    }
                    else if (start.AddressFamily == AddressFamily.InterNetworkV6)
                    {
                        v6s.Add(start.GetAddressBytes());
                        v6e.Add(end.GetAddressBytes());
                        v6o.Add(pooled);
                    }
                }
            }

            var db = new AsnDb();
            db.v4Starts = v4s.ToArray(); db.v4Ends = v4e.ToArray(); db.v4Org = v4o.ToArray();
            db.v6Starts = v6s.ToArray(); db.v6Ends = v6e.ToArray(); db.v6Org = v6o.ToArray();
            Sort(db);
            return db;
        }

        static void Sort(AsnDb db)
        {
            int[] idx = MakeIndex(db.v4Starts.Length);
            uint[] starts = db.v4Starts;
            Array.Sort(idx, delegate(int a, int b) { return starts[a].CompareTo(starts[b]); });
            db.v4Starts = Reorder(db.v4Starts, idx);
            db.v4Ends = Reorder(db.v4Ends, idx);
            db.v4Org = Reorder(db.v4Org, idx);

            idx = MakeIndex(db.v6Starts.Length);
            byte[][] starts6 = db.v6Starts;
            Array.Sort(idx, delegate(int a, int b) { return IpRangeSet.CompareBytes(starts6[a], starts6[b]); });
            db.v6Starts = Reorder(db.v6Starts, idx);
            db.v6Ends = Reorder(db.v6Ends, idx);
            db.v6Org = Reorder(db.v6Org, idx);
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
                if (i >= 0 && addr <= v4Ends[i]) return v4Org[i];
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
                if (i >= 0 && IpRangeSet.CompareBytes(addr, v6Ends[i]) <= 0) return v6Org[i];
                return null;
            }
            return null;
        }
    }
}
