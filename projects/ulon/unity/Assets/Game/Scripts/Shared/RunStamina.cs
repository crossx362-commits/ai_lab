using System;

namespace Ulon.Shared
{
    /// <summary>
    /// 기획 §18.2 DEX=Stamina. 달리면 스태미나를 쓰고, 0이면 달릴 수 없다.
    /// 소모/회복 수치는 run_stamina.json. Max는 StatSet.MaxStaminaOf.
    /// 출처: https://uo.com/wiki/ultima-online-wiki/player/stats/skills-stats-and-attributes/
    /// 보조: https://www.uoguide.com/Stamina
    /// </summary>
    public static class RunStamina
    {
        public const string FileName = "run_stamina.json";
        public const float FallbackDrainPerSecond = 4f;
        public const float FallbackRegenPerSecond = 2f;

        [Serializable]
        class File_
        {
            public float drain_per_s;
            public float regen_per_s;
            public string source;
        }

        static bool loaded;
        static float drainPerSecond = FallbackDrainPerSecond;
        static float regenPerSecond = FallbackRegenPerSecond;
        static string source = "";
        static string loadError = "";

        /// <summary>네거티브 컨트롤 — true면 0이어도 달린다(옛 동작).</summary>
        public static bool NcOpen;

        public static string LoadError
        {
            get { EnsureLoaded(); return loadError; }
        }

        public static string Source
        {
            get { EnsureLoaded(); return source; }
        }

        public static float DrainPerSecond
        {
            get
            {
                EnsureLoaded();
                return NcOpen ? 0f : drainPerSecond;
            }
        }

        public static float RegenPerSecond
        {
            get
            {
                EnsureLoaded();
                return regenPerSecond;
            }
        }

        public static float FileDrainPerSecond
        {
            get { EnsureLoaded(); return drainPerSecond; }
        }

        public static float FileRegenPerSecond
        {
            get { EnsureLoaded(); return regenPerSecond; }
        }

        public static bool CanRun(float stamina)
        {
            if (NcOpen)
                return true;
            return stamina > 0.01f;
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
            drainPerSecond = FallbackDrainPerSecond;
            regenPerSecond = FallbackRegenPerSecond;
            source = "";
            loadError = "";
            if (!DataLedger.TryRead(FileName, out File_ parsed, out loadError))
                return;
            if (parsed.drain_per_s > 0.001f)
                drainPerSecond = parsed.drain_per_s;
            if (parsed.regen_per_s > 0.001f)
                regenPerSecond = parsed.regen_per_s;
            source = parsed.source ?? "";
        }
    }
}
