using System;
using System.Collections.Generic;

namespace ChinaGuard
{
    // Windows 방화벽(WFP)에 차단 규칙 등록/제거 (COM: HNetCfg)
    static class FirewallService
    {
        public const string RulePrefix = "ChinaGuard";
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
                if (name != null && name.StartsWith(RulePrefix)) count++;
            }
            return count;
        }

        public static int ApplyRules(List<string> cidrs)
        {
            RemoveRules();
            dynamic pol = Policy();
            int ruleIndex = 0;
            for (int offset = 0; offset < cidrs.Count; offset += ChunkSize)
            {
                int len = Math.Min(ChunkSize, cidrs.Count - offset);
                string addresses = string.Join(",", cidrs.GetRange(offset, len));
                ruleIndex++;
                AddRule(pol, string.Format("{0} OUT {1:D3}", RulePrefix, ruleIndex), DirOut, addresses);
                AddRule(pol, string.Format("{0} IN {1:D3}", RulePrefix, ruleIndex), DirIn, addresses);
            }
            Logger.Log(string.Format("방화벽 규칙 적용 완료: CIDR {0}개, 규칙 {1}개", cidrs.Count, ruleIndex * 2));
            return ruleIndex * 2;
        }

        static void AddRule(dynamic pol, string name, int direction, string addresses)
        {
            dynamic rule = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FWRule"));
            rule.Name = name;
            rule.Description = "ChinaGuard 자동 생성 - 중국 IP 대역 차단";
            rule.Action = ActionBlock;
            rule.Direction = direction;
            rule.Enabled = true;
            rule.InterfaceTypes = "All";
            rule.Profiles = 0x7FFFFFFF;
            rule.RemoteAddresses = addresses;
            pol.Rules.Add(rule);
        }

        public static int RemoveRules()
        {
            dynamic pol = Policy();
            var names = new List<string>();
            foreach (dynamic r in pol.Rules)
            {
                string name = r.Name;
                if (name != null && name.StartsWith(RulePrefix)) names.Add(name);
            }
            foreach (var name in names) pol.Rules.Remove(name);
            if (names.Count > 0)
                Logger.Log(string.Format("방화벽 규칙 {0}개 제거", names.Count));
            return names.Count;
        }
    }
}
