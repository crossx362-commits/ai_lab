using System;

namespace Ulon.Shared
{
    /// <summary>
    /// 기획 §18.6. 마나는 시간이 지나면 회복되고, 속도는 명상 스킬·INT가 담당한다.
    /// 중갑은 기존 MeditationResolve.HeavyMul(0.5)로 깎는다.
    /// 수치는 mana_regen.json. 클릭 명상은 숙련 상승용으로 따로 둔다.
    /// 출처: https://uo.com/wiki/ultima-online-wiki/skills/magery/
    /// </summary>
    public static class ManaRegen
    {
        public const string FileName = "mana_regen.json";
        public const float FallbackBasePerSecond = 1f;
        public const float FallbackMeditationDiv = 100f;
        public const float FallbackIntDiv = 50f;

        [Serializable]
        class File_
        {
            public float base_per_s;
            public float meditation_div;
            public float int_div;
            public string source;
        }

        static bool loaded;
        static float basePerSecond = FallbackBasePerSecond;
        static float meditationDiv = FallbackMeditationDiv;
        static float intDiv = FallbackIntDiv;
        static string source = "";
        static string loadError = "";

        /// <summary>네거티브 컨트롤 — true면 자연 회복이 없다(옛 동작).</summary>
        public static bool NcOff;

        public static string LoadError
        {
            get { EnsureLoaded(); return loadError; }
        }

        public static string Source
        {
            get { EnsureLoaded(); return source; }
        }

        public static float FileBasePerSecond
        {
            get { EnsureLoaded(); return basePerSecond; }
        }

        public static float FileMeditationDiv
        {
            get { EnsureLoaded(); return meditationDiv; }
        }

        public static float FileIntDiv
        {
            get { EnsureLoaded(); return intDiv; }
        }

        public static float PerSecond(SkillSet skills, StatSet stats, bool heavyArmor)
        {
            if (NcOff)
                return 0f;
            EnsureLoaded();
            float med = skills != null ? skills.Get(SkillId.Meditation) : 0f;
            float intel = stats != null ? stats.Int : 0f;
            float rate = basePerSecond + med / meditationDiv + intel / intDiv;
            if (heavyArmor)
                rate *= MeditationResolve.HeavyMul;
            return rate;
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
            basePerSecond = FallbackBasePerSecond;
            meditationDiv = FallbackMeditationDiv;
            intDiv = FallbackIntDiv;
            source = "";
            loadError = "";
            if (!DataLedger.TryRead(FileName, out File_ parsed, out loadError))
                return;
            if (parsed.base_per_s > 0.001f)
                basePerSecond = parsed.base_per_s;
            if (parsed.meditation_div > 0.001f)
                meditationDiv = parsed.meditation_div;
            if (parsed.int_div > 0.001f)
                intDiv = parsed.int_div;
            source = parsed.source ?? "";
        }
    }
}
