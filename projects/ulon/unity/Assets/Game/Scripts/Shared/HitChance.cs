using System;
using UnityEngine;

namespace Ulon.Shared
{
    /// <summary>
    /// 기획 §18.3 명중은 무기 스킬.
    /// 확률 = clamp(min, max, (공격스킬+offset) / ((방어스킬+offset)*2)).
    /// HCI/DCI·레슬링 대체는 기획서에 없어 넣지 않음. 전술은 피해(§18.3)라 방어 스킬에 안 넣음.
    /// 출처: https://uo.stratics.com/content/arms-armor/combat.php
    /// </summary>
    public static class HitChance
    {
        public const string FileName = "hit_chance.json";
        public const float FallbackOffset = 20f;
        public const float FallbackMin = 0f;
        public const float FallbackMax = 1f;

        [Serializable]
        class File_
        {
            public float offset;
            public float min;
            public float max;
            public string source;
        }

        static bool loaded;
        static float offset = FallbackOffset;
        static float minChance = FallbackMin;
        static float maxChance = FallbackMax;
        static string source = "";
        static string loadError = "";

        /// <summary>네거티브 컨트롤 — true면 옛처럼 항상 명중.</summary>
        public static bool NcAlwaysHit;

        /// <summary>0 이상이면 그 값을 굴림으로 쓴다. 음수면 Random.value. 셀프체크는 0.</summary>
        public static float ForcedRoll = -1f;

        public static string LoadError
        {
            get { EnsureLoaded(); return loadError; }
        }

        public static string Source
        {
            get { EnsureLoaded(); return source; }
        }

        public static float FileOffset
        {
            get { EnsureLoaded(); return offset; }
        }

        public static float FileMin
        {
            get { EnsureLoaded(); return minChance; }
        }

        public static float FileMax
        {
            get { EnsureLoaded(); return maxChance; }
        }

        public static float DefendSkill(SkillSet skills)
        {
            if (skills == null)
                return 0f;
            float m = skills.Get(SkillId.Swordsmanship);
            float a = skills.Get(SkillId.Archery);
            if (a > m) m = a;
            float f = skills.Get(SkillId.Fencing);
            if (f > m) m = f;
            float c = skills.Get(SkillId.Mace);
            if (c > m) m = c;
            return m;
        }

        public static float Percent(float attackSkill, float defendSkill)
        {
            EnsureLoaded();
            float atk = attackSkill + offset;
            float def = (defendSkill + offset) * 2f;
            if (def < 0.001f)
                def = 0.001f;
            float p = atk / def;
            if (p < minChance) p = minChance;
            if (p > maxChance) p = maxChance;
            return p;
        }

        public static bool Hits(float attackSkill, float defendSkill, float roll)
        {
            if (NcAlwaysHit)
                return true;
            return roll < Percent(attackSkill, defendSkill);
        }

        public static float NextRoll()
        {
            if (NcAlwaysHit)
                return 0f;
            if (ForcedRoll >= 0f)
                return ForcedRoll;
            return UnityEngine.Random.value;
        }

        public static void Reload()
        {
            loaded = false;
            EnsureLoaded();
        }

        static void EnsureLoaded()
        {
            if (loaded)
                return;
            loaded = true;
            offset = FallbackOffset;
            minChance = FallbackMin;
            maxChance = FallbackMax;
            source = "";
            loadError = "";
            if (!DataLedger.TryRead(FileName, out File_ parsed, out loadError))
                return;
            if (parsed.offset > 0.001f)
                offset = parsed.offset;
            if (parsed.min >= 0f)
                minChance = parsed.min;
            if (parsed.max > 0.001f)
                maxChance = parsed.max;
            source = parsed.source ?? "";
        }
    }
}
