using System;
using System.Collections.Generic;

namespace ChinaGuard
{
    // 같은 (프로세스, IP) 조합의 반복 알림 스팸 방지
    static class Throttler
    {
        static readonly object Sync = new object();
        static readonly Dictionary<string, DateTime> LastNotified = new Dictionary<string, DateTime>();
        public static TimeSpan Window = TimeSpan.FromMinutes(5);

        public static bool ShouldNotify(string app, string remoteIp)
        {
            string key = app + "|" + remoteIp;
            lock (Sync)
            {
                DateTime last;
                DateTime now = DateTime.UtcNow;
                if (LastNotified.TryGetValue(key, out last) && now - last < Window)
                    return false;
                LastNotified[key] = now;

                if (LastNotified.Count > 2000)
                {
                    var stale = new List<string>();
                    foreach (var kv in LastNotified)
                        if (now - kv.Value > Window) stale.Add(kv.Key);
                    foreach (var k in stale) LastNotified.Remove(k);
                }
                return true;
            }
        }
    }
}
