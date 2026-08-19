using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace GeoGuard
{
    // CIDR 목록에서 IP 포함 여부를 이진 탐색으로 판정
    class IpRangeSet
    {
        readonly List<uint> v4Starts = new List<uint>();
        readonly List<uint> v4Ends = new List<uint>();
        readonly List<byte[]> v6Starts = new List<byte[]>();
        readonly List<byte[]> v6Ends = new List<byte[]>();

        public int Count
        {
            get { return v4Starts.Count + v6Starts.Count; }
        }

        public static IpRangeSet FromCidrs(IEnumerable<string> cidrs)
        {
            var v4 = new List<KeyValuePair<uint, uint>>();
            var v6 = new List<KeyValuePair<byte[], byte[]>>();

            foreach (var raw in cidrs)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int slash = line.IndexOf('/');
                string ipPart = slash >= 0 ? line.Substring(0, slash) : line;
                IPAddress ip;
                if (!IPAddress.TryParse(ipPart, out ip)) continue;

                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    int prefix = slash >= 0 ? int.Parse(line.Substring(slash + 1)) : 32;
                    uint addr = ToUInt(ip);
                    uint mask = prefix == 0 ? 0u : uint.MaxValue << (32 - prefix);
                    v4.Add(new KeyValuePair<uint, uint>(addr & mask, (addr & mask) | ~mask));
                }
                else
                {
                    int prefix = slash >= 0 ? int.Parse(line.Substring(slash + 1)) : 128;
                    byte[] start = ip.GetAddressBytes();
                    byte[] end = new byte[16];
                    for (int i = 0; i < 16; i++)
                    {
                        int bits = Math.Min(8, Math.Max(0, prefix - i * 8));
                        byte mask = bits == 0 ? (byte)0 : (byte)(0xFF << (8 - bits));
                        start[i] = (byte)(start[i] & mask);
                        end[i] = (byte)(start[i] | ~mask);
                    }
                    v6.Add(new KeyValuePair<byte[], byte[]>(start, end));
                }
            }

            v4.Sort(delegate(KeyValuePair<uint, uint> a, KeyValuePair<uint, uint> b)
            { return a.Key.CompareTo(b.Key); });
            v6.Sort(delegate(KeyValuePair<byte[], byte[]> a, KeyValuePair<byte[], byte[]> b)
            { return CompareBytes(a.Key, b.Key); });

            var set = new IpRangeSet();
            foreach (var kv in v4) { set.v4Starts.Add(kv.Key); set.v4Ends.Add(kv.Value); }
            foreach (var kv in v6) { set.v6Starts.Add(kv.Key); set.v6Ends.Add(kv.Value); }
            return set;
        }

        public bool Contains(IPAddress ip)
        {
            if (ip == null) return false;
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                uint addr = ToUInt(ip);
                int idx = UpperBound(v4Starts, addr) - 1;
                return idx >= 0 && addr <= v4Ends[idx];
            }
            if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            {
                byte[] addr = ip.GetAddressBytes();
                int lo = 0, hi = v6Starts.Count;
                while (lo < hi)
                {
                    int mid = (lo + hi) / 2;
                    if (CompareBytes(v6Starts[mid], addr) <= 0) lo = mid + 1; else hi = mid;
                }
                int idx = lo - 1;
                return idx >= 0 && CompareBytes(addr, v6Ends[idx]) <= 0;
            }
            return false;
        }

        internal static uint ToUInt(IPAddress ip)
        {
            byte[] b = ip.GetAddressBytes();
            return ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3];
        }

        internal static int CompareBytes(byte[] a, byte[] b)
        {
            for (int i = 0; i < a.Length && i < b.Length; i++)
            {
                if (a[i] != b[i]) return a[i] < b[i] ? -1 : 1;
            }
            return a.Length.CompareTo(b.Length);
        }

        static int UpperBound(List<uint> list, uint value)
        {
            int lo = 0, hi = list.Count;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (list[mid] <= value) lo = mid + 1; else hi = mid;
            }
            return lo;
        }
    }
}
