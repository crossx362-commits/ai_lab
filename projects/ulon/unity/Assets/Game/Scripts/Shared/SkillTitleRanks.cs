using System;

namespace Ulon.Shared
{
    /// <summary>
    /// 기획 §3.2 숙련 칭호 구간. 수치는 skill_titles.json.
    /// 30 미만은 접두사 없음. 대표 칭호는 최고 스킬 하나에만 붙는다(TitleOf).
    /// </summary>
    public static class SkillTitleRanks
    {
        public const string FileName = "skill_titles.json";
        public const float FallbackNeophyte = 30f;
        public const float FallbackNovice = 40f;
        public const float FallbackApprentice = 50f;
        public const float FallbackJourneyman = 60f;
        public const float FallbackExpert = 70f;
        public const float FallbackAdept = 80f;
        public const float FallbackMaster = 90f;
        public const float FallbackGrandmaster = 100f;

        [Serializable]
        class File_
        {
            public float neophyte;
            public float novice;
            public float apprentice;
            public float journeyman;
            public float expert;
            public float adept;
            public float master;
            public float grandmaster;
            public string source;
        }

        static bool loaded;
        static float neophyte = FallbackNeophyte;
        static float novice = FallbackNovice;
        static float apprentice = FallbackApprentice;
        static float journeyman = FallbackJourneyman;
        static float expert = FallbackExpert;
        static float adept = FallbackAdept;
        static float master = FallbackMaster;
        static float grandmaster = FallbackGrandmaster;
        static string source = "";
        static string loadError = "";

        /// <summary>네거티브 컨트롤 — true면 숙련 접두사가 없다(옛 직업명만).</summary>
        public static bool NcOff;

        public static string LoadError
        {
            get { EnsureLoaded(); return loadError; }
        }

        public static string Source
        {
            get { EnsureLoaded(); return source; }
        }

        public static string FullPath => DataLedger.PathOf(FileName);

        public static float FileNeophyte { get { EnsureLoaded(); return neophyte; } }
        public static float FileNovice { get { EnsureLoaded(); return novice; } }
        public static float FileApprentice { get { EnsureLoaded(); return apprentice; } }
        public static float FileJourneyman { get { EnsureLoaded(); return journeyman; } }
        public static float FileExpert { get { EnsureLoaded(); return expert; } }
        public static float FileAdept { get { EnsureLoaded(); return adept; } }
        public static float FileMaster { get { EnsureLoaded(); return master; } }
        public static float FileGrandmaster { get { EnsureLoaded(); return grandmaster; } }

        public static string Of(float value)
        {
            if (NcOff)
                return "";
            EnsureLoaded();
            if (value + 0.0001f >= grandmaster) return "그랜드마스터";
            if (value + 0.0001f >= master) return "대가";
            if (value + 0.0001f >= adept) return "달인";
            if (value + 0.0001f >= expert) return "전문가";
            if (value + 0.0001f >= journeyman) return "숙련";
            if (value + 0.0001f >= apprentice) return "견습";
            if (value + 0.0001f >= novice) return "수습";
            if (value + 0.0001f >= neophyte) return "초심자";
            return "";
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
            neophyte = FallbackNeophyte;
            novice = FallbackNovice;
            apprentice = FallbackApprentice;
            journeyman = FallbackJourneyman;
            expert = FallbackExpert;
            adept = FallbackAdept;
            master = FallbackMaster;
            grandmaster = FallbackGrandmaster;
            source = "";
            loadError = "";
            if (!DataLedger.TryRead(FileName, out File_ parsed, out loadError) || parsed == null)
                return;
            if (parsed.neophyte > 0.0001f) neophyte = parsed.neophyte;
            if (parsed.novice > 0.0001f) novice = parsed.novice;
            if (parsed.apprentice > 0.0001f) apprentice = parsed.apprentice;
            if (parsed.journeyman > 0.0001f) journeyman = parsed.journeyman;
            if (parsed.expert > 0.0001f) expert = parsed.expert;
            if (parsed.adept > 0.0001f) adept = parsed.adept;
            if (parsed.master > 0.0001f) master = parsed.master;
            if (parsed.grandmaster > 0.0001f) grandmaster = parsed.grandmaster;
            source = parsed.source ?? "";
        }
    }
}
