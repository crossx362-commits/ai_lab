using System;
using System.Collections.Generic;

namespace GeoGuard
{
    // Windows 방화벽(WFP)에 국가별 차단 규칙 등록/제거 (COM: HNetCfg)
    static class FirewallService
    {
        public const string RulePrefix = "GeoGuard";
        const string LegacyPrefix = "ChinaGuard";   // 구버전 규칙 정리용
        const int ChunkSize = 400;
        const int ActionBlock = 0;
        const int DirIn = 1;
        const int DirOut = 2;

        static dynamic Policy()
        {
            return Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));
        }

        public static int CountRules()
        {
            dynamic pol = Policy();
            int count = 0;
            foreach (dynamic r in pol.Rules)
            {
                string name = r.Name;
                if (name != null && name.StartsWith(RulePrefix + " ")) count++;
            }
            return count;
        }

        // 현재 방화벽에 규칙이 등록된 국가 코드 목록
        public static HashSet<string> AppliedCountries()
        {
            var result = new HashSet<string>();
            dynamic pol = Policy();
            foreach (dynamic r in pol.Rules)
            {
                string name = r.Name;
                if (name == null || !name.StartsWith(RulePrefix + " ")) continue;
                string[] parts = name.Split(' ');
                if (parts.Length >= 3 && parts[1].Length == 2)
                    result.Add(parts[1].ToUpperInvariant());
            }
            return result;
        }

        public static int ApplyRulesFor(string cc, List<string> cidrs)
        {
            cc = cc.ToUpperInvariant();
            RemoveRulesFor(cc);
            dynamic pol = Policy();
            int ruleIndex = 0;
            for (int offset = 0; offset < cidrs.Count; offset += ChunkSize)
            {
                int len = Math.Min(ChunkSize, cidrs.Count - offset);
                string addresses = string.Join(",", cidrs.GetRange(offset, len));
                ruleIndex++;
                AddRule(pol, string.Format("{0} {1} OUT {2:D3}", RulePrefix, cc, ruleIndex), cc, DirOut, addresses);
                AddRule(pol, string.Format("{0} {1} IN {2:D3}", RulePrefix, cc, ruleIndex), cc, DirIn, addresses);
            }
            Logger.Log(string.Format("[{0}] 방화벽 규칙 적용: CIDR {1}개, 규칙 {2}개",
                cc, cidrs.Count, ruleIndex * 2));
            return ruleIndex * 2;
        }

        static void AddRule(dynamic pol, string name, string cc, int direction, string addresses)
        {
            dynamic rule = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FWRule"));
            rule.Name = name;
            rule.Description = string.Format("GeoGuard 자동 생성 - {0} IP 대역 차단", Countries.Display(cc));
            rule.Action = ActionBlock;
            rule.Direction = direction;
            rule.Enabled = true;
            rule.InterfaceTypes = "All";
            rule.Profiles = 0x7FFFFFFF;
            rule.RemoteAddresses = addresses;
            pol.Rules.Add(rule);
        }

        public static int RemoveRulesFor(string cc)
        {
            return RemoveByPrefix(string.Format("{0} {1} ", RulePrefix, cc.ToUpperInvariant()));
        }

        public static int RemoveAll()
        {
            int n = RemoveByPrefix(RulePrefix + " ");
            n += RemoveByPrefix(LegacyPrefix + " ");
            return n;
        }

        // 구버전(ChinaGuard) 이름 규칙 정리
        public static int RemoveLegacyRules()
        {
            int n = RemoveByPrefix(LegacyPrefix + " ");
            if (n > 0) Logger.Log(string.Format("구버전 규칙 {0}개 정리", n));
            return n;
        }

        static int RemoveByPrefix(string prefix)
        {
            dynamic pol = Policy();
            var names = new List<string>();
            foreach (dynamic r in pol.Rules)
            {
                string name = r.Name;
                if (name != null && name.StartsWith(prefix)) names.Add(name);
            }
            foreach (var name in names) pol.Rules.Remove(name);
            return names.Count;
        }
    }
}
