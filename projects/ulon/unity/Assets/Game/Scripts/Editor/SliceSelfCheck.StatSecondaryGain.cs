using System;
using System.IO;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// 기획 §18.2. 스킬 상승 시 주 스탯이 잠기면 부가 오른다.
    /// NC: NcPrimaryOnly 이면 부가 안 올라 빨간불.
    /// 출처: https://uo.com/wiki/ultima-online-wiki/player/stats/skills-stats-and-attributes/
    /// </summary>
    public static partial class SliceSelfCheck
    {
        static void AssertStatSecondaryGain()
        {
            if (SkillGain.NcPrimaryOnly)
                throw new InvalidOperationException("SkillGain.NcPrimaryOnly 가 켜져 있으면 부 스탯이 안 오릅니다.");

            if (StatSet.SecondaryOf(SkillId.Swordsmanship) != StatId.Dex)
                throw new InvalidOperationException("검술 Secondary는 DEX여야 합니다.");
            if (StatSet.SecondaryOf(SkillId.Healing) != StatId.Int)
                throw new InvalidOperationException("치유 Secondary는 INT여야 합니다.");
            if (StatSet.SecondaryOf(SkillId.Magery) != StatId.Str)
                throw new InvalidOperationException("마법 Secondary는 STR여야 합니다.");
            if (StatSet.SecondaryOf(SkillId.Alchemy) != StatId.Dex)
                throw new InvalidOperationException("연금술 Secondary는 DEX여야 합니다.");
            if (StatSet.PrimaryOf(SkillId.Swordsmanship) == StatSet.SecondaryOf(SkillId.Swordsmanship))
                throw new InvalidOperationException("검술 주/부가 같습니다.");

            for (int i = 0; i < (int)SkillId.Count; i++)
            {
                var id = (SkillId)i;
                if (StatSet.PrimaryOf(id) == StatSet.SecondaryOf(id))
                    throw new InvalidOperationException(id + " 주/부가 같습니다.");
            }

            string gainPath = Path.Combine(Application.dataPath, "Game/Scripts/Shared/SkillGain.cs");
            string gain = File.ReadAllText(gainPath);
            if (gain.IndexOf("TryGainFromSkill", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("SkillGain이 TryGainFromSkill을 안 탑니다.");

            var bothUp = new StatSet();
            int strWas = bothUp.Str;
            int dexWas = bothUp.Dex;
            SkillGain.TryRaise(new SkillSet(), SkillId.Swordsmanship, 20f, out _, out _, bothUp);
            if (bothUp.Str != strWas + 1 || bothUp.Dex != dexWas)
                throw new InvalidOperationException("주가 열려 있으면 STR만 올라야 합니다: STR " + bothUp.Str + " DEX " + bothUp.Dex);

            var lockedStr = new StatSet();
            lockedStr.SetLock(StatId.Str, SkillLock.Locked);
            int strLockWas = lockedStr.Str;
            int dexLockWas = lockedStr.Dex;
            SkillGain.TryRaise(new SkillSet(), SkillId.Swordsmanship, 20f, out _, out _, lockedStr);
            if (lockedStr.Str != strLockWas)
                throw new InvalidOperationException("잠긴 STR은 오르면 안 됩니다.");
            if (lockedStr.Dex != dexLockWas + 1)
                throw new InvalidOperationException("STR 잠금이면 검술 상승 시 DEX가 올라야 합니다: " + lockedStr.Dex);

            var bothLocked = new StatSet();
            bothLocked.SetLock(StatId.Str, SkillLock.Locked);
            bothLocked.SetLock(StatId.Dex, SkillLock.Locked);
            int s = bothLocked.Str;
            int d = bothLocked.Dex;
            int n = bothLocked.Int;
            SkillGain.TryRaise(new SkillSet(), SkillId.Swordsmanship, 20f, out _, out _, bothLocked);
            if (bothLocked.Str != s || bothLocked.Dex != d || bothLocked.Int != n)
                throw new InvalidOperationException("주·부가 잠기면 능력치가 오르면 안 됩니다.");

            var healLock = new StatSet();
            healLock.SetLock(StatId.Dex, SkillLock.Locked);
            int intWas = healLock.Int;
            SkillGain.TryRaise(new SkillSet(), SkillId.Healing, 20f, out _, out _, healLock);
            if (healLock.Int != intWas + 1)
                throw new InvalidOperationException("치유 DEX 잠금이면 INT가 올라야 합니다.");

            Debug.Log("[Ulon] 부 스탯 성장 — 주 잠금 시 Secondary TryRaise · 검술 STR→DEX · 치유 DEX→INT");
        }

        static void AssertStatSecondaryGainNegativeControl()
        {
            bool was = SkillGain.NcPrimaryOnly;
            bool red = false;
            try
            {
                SkillGain.NcPrimaryOnly = true;
                try { AssertStatSecondaryGain(); }
                catch (InvalidOperationException) { red = true; }
            }
            finally { SkillGain.NcPrimaryOnly = was; }
            if (!red)
                throw new InvalidOperationException("부 스탯 네거티브 컨트롤 실패 — NcPrimaryOnly 인데 통과했습니다.");
            Debug.Log("[Ulon] 부 스탯 네거티브 컨트롤 통과 — NcPrimaryOnly 이면 FAIL");
        }
    }
}
