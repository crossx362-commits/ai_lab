using System;
using UnityEngine;

namespace Ulon.Shared
{
    /// <summary>
    /// 기획 §18.3 방패술 = 방패 막기 판정.
    /// 확률 = clamp(min, max, 방패술 * shield_mul / 100). 방패가 있을 때만.
    /// 성공 시 피해 0(원작: 막으면 빗나감). 무기만으로 막기·Bushido·DEX 보정은 기획서에 없어 넣지 않음.
    /// 출처: https://uo.stratics.com/content/skills/parrying.shtml (방패 = 방패술*0.3)
    /// </summary>
    public static class ParryChance
    {
        public const string FileName = "parry_chance.json";
        public const float FallbackShieldMul = 0.3f;
        public const float FallbackMin = 0f;
        public const float FallbackMax = 1f;

        [Serializable]
        class File_
        {
            public float shield_mul;
            public float min;
            public float max;
            public string source;
        }

        static bool loaded;
        static float shieldMul = FallbackShieldMul;
        static float minChance = FallbackMin;
        static float maxChance = FallbackMax;
        static string source = "";
        static string loadError = "";

        /// <summary>네거티브 컨트롤 — true면 방패가 있어도 막지 않는다.</summary>
        public static bool NcNeverBlock;

        /// <summary>0 이상이면 그 값을 굴림으로 쓴다. 음수면 Random.value.</summary>
        public static float ForcedRoll = -1f;

        public static string LoadError
        {
            get { EnsureLoaded(); return loadError; }
        }

        public static string Source
        {
            get { EnsureLoaded(); return source; }
        }

        public static float FileShieldMul
        {
            get { EnsureLoaded(); return shieldMul; }
        }

        public static float FileMin
        {
            get { EnsureLoaded(); return minChance; }
        }

        public static float FileMax
        {
            get { EnsureLoaded(); return maxChance; }
        }

        public static float Percent(float parrySkill)
        {
            EnsureLoaded();
            float p = parrySkill * shieldMul / 100f;
            if (p < minChance) p = minChance;
            if (p > maxChance) p = maxChance;
            return p;
        }

        public static bool Blocks(bool hasShield, SkillSet skills, StatSet stats, float difficulty, float roll)
        {
            if (NcNeverBlock)
                return false;
            if (!hasShield || skills == null)
                return false;
            SkillGain.TryRaise(skills, SkillId.Parrying, difficulty, out _, out _, stats);
            return roll < Percent(skills.Get(SkillId.Parrying));
        }

        public static float NextRoll()
        {
            if (NcNeverBlock)
                return 1f;
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
            shieldMul = FallbackShieldMul;
            minChance = FallbackMin;
            maxChance = FallbackMax;
            source = "";
            loadError = "";
            if (!DataLedger.TryRead(FileName, out File_ parsed, out loadError))
                return;
            if (parsed.shield_mul > 0.001f)
                shieldMul = parsed.shield_mul;
            if (parsed.min >= 0f)
                minChance = parsed.min;
            if (parsed.max > 0.001f)
                maxChance = parsed.max;
            source = parsed.source ?? "";
        }
    }
}
