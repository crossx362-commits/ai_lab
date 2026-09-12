using System;

namespace Ulon.Shared
{
    /// <summary>
    /// 기획 §18.2 STR=최대 HP. 살아 있는 몸은 시간이 지나면 HP가 찬다.
    /// 수치는 hp_regen.json. 유령·시체(Hp≤0)는 안 찬다 — 부활 경로가 따로 있다.
    /// 출처: https://www.uoguide.com/Hit_point
    /// </summary>
    public static class HpRegen
    {
        public const string FileName = "hp_regen.json";
        public const float FallbackBasePerSecond = 1f;
        public const float FallbackStrDiv = 50f;

        [Serializable]
        class File_
        {
            public float base_per_s;
            public float str_div;
            public string source;
        }

        static bool loaded;
        static float basePerSecond = FallbackBasePerSecond;
        static float strDiv = FallbackStrDiv;
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

        public static float FileStrDiv
        {
            get { EnsureLoaded(); return strDiv; }
        }

        public static float PerSecond(StatSet stats)
        {
            if (NcOff)
                return 0f;
            EnsureLoaded();
            float str = stats != null ? stats.Str : 0f;
            return basePerSecond + str / strDiv;
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
            strDiv = FallbackStrDiv;
            source = "";
            loadError = "";
            if (!DataLedger.TryRead(FileName, out File_ parsed, out loadError))
                return;
            if (parsed.base_per_s > 0.001f)
                basePerSecond = parsed.base_per_s;
            if (parsed.str_div > 0.001f)
                strDiv = parsed.str_div;
            source = parsed.source ?? "";
        }
    }
}
