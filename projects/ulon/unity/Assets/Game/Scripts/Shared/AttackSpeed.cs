using System;

namespace Ulon.Shared
{
    /// <summary>
    /// 기획 §18.2 DEX=Stamina, 공격 속도 보정.
    /// 휘두름 간격(초) = max(min, base − 현재스태미나/나눔).
    /// 기본 DEX25·만땅 35면 옛 고정값 1.1초. 기진이면 느리고, DEX가 높아 스태미나 풀이 크면 빠르다.
    /// 수치는 attack_speed.json. 무기 속도·SSI는 기획서에 없어 넣지 않음.
    /// 출처: https://www.uoguide.com/Swing_Speed https://www.uoguide.com/Dexterity
    /// </summary>
    public static class AttackSpeed
    {
        public const string FileName = "attack_speed.json";
        public const float FallbackBaseSeconds = 1.8f;
        public const float FallbackStamDiv = 50f;
        public const float FallbackMinSeconds = 0.5f;
        public const float FallbackFixedSeconds = 1.1f;

        [Serializable]
        class File_
        {
            public float base_s;
            public float stam_div;
            public float min_s;
            public string source;
        }

        static bool loaded;
        static float baseSeconds = FallbackBaseSeconds;
        static float stamDiv = FallbackStamDiv;
        static float minSeconds = FallbackMinSeconds;
        static string source = "";
        static string loadError = "";

        /// <summary>네거티브 컨트롤 — true면 옛 고정 1.1초(스태미나 무시).</summary>
        public static bool NcFixed;

        public static string LoadError
        {
            get { EnsureLoaded(); return loadError; }
        }

        public static string Source
        {
            get { EnsureLoaded(); return source; }
        }

        public static float FileBaseSeconds
        {
            get { EnsureLoaded(); return baseSeconds; }
        }

        public static float FileStamDiv
        {
            get { EnsureLoaded(); return stamDiv; }
        }

        public static float FileMinSeconds
        {
            get { EnsureLoaded(); return minSeconds; }
        }

        public static float Seconds(float stamina)
        {
            if (NcFixed)
                return FallbackFixedSeconds;
            EnsureLoaded();
            float s = baseSeconds - stamina / stamDiv;
            if (s < minSeconds)
                s = minSeconds;
            return s;
        }

        public static float Seconds(StatSet stats)
        {
            int dex = stats != null ? stats.Dex : StatSet.DefaultDex;
            return Seconds(StatSet.MaxStaminaOf(dex));
        }

        public static float Seconds(StatSet stats, float stamina)
        {
            return Seconds(stamina);
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
            baseSeconds = FallbackBaseSeconds;
            stamDiv = FallbackStamDiv;
            minSeconds = FallbackMinSeconds;
            source = "";
            loadError = "";
            if (!DataLedger.TryRead(FileName, out File_ parsed, out loadError))
                return;
            if (parsed.base_s > 0.001f)
                baseSeconds = parsed.base_s;
            if (parsed.stam_div > 0.001f)
                stamDiv = parsed.stam_div;
            if (parsed.min_s > 0.001f)
                minSeconds = parsed.min_s;
            source = parsed.source ?? "";
        }
    }
}
