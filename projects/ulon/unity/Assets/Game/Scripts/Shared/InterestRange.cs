using UnityEngine;

namespace Ulon.Shared
{
    /// <summary>
    /// 기획 §7.3 관심 영역 거리 원장. 수치는 코드가 아니라 interest.json.
    /// 원작 클라 기본 갱신 범위는 18타일(ServUO GetUpdateRange). 이 프로젝트 1타일≈1m.
    /// 기획 §7.3 (2026-09-12 보강): 18타일 = 18m. 원작 합격 수치 확정.
    /// </summary>
    public static class InterestRange
    {
        public const string FileName = "interest.json";
        public const float FallbackMeters = 18f;

        [System.Serializable]
        class File_
        {
            public float sync_range_m;
            public float hide_hysteresis;
            public string source;
        }

        static bool loaded;
        static float fileMeters;
        static string loadError = "";
        static string source = "";

        /// <summary>네거티브 컨트롤 — true면 거리를 사실상 꺼서 월드 전체가 보인다.</summary>
        public static bool NcOpen;

        public static string LoadError
        {
            get { EnsureLoaded(); return loadError; }
        }

        public static string Source
        {
            get { EnsureLoaded(); return source; }
        }

        /// <summary>파일에 적힌 값. NcOpen과 무관. 파일 없으면 0.</summary>
        public static float FileMeters
        {
            get { EnsureLoaded(); return fileMeters; }
        }

        public static float Meters
        {
            get
            {
                if (NcOpen)
                    return 1_000_000f;
                EnsureLoaded();
                return fileMeters > 0f ? fileMeters : FallbackMeters;
            }
        }

        public static void Reload()
        {
            loaded = false;
            EnsureLoaded();
        }

        public static bool CanSee(Vector3 a, Vector3 b)
        {
            float r = Meters;
            return (a - b).sqrMagnitude <= r * r;
        }

        static void EnsureLoaded()
        {
            if (loaded)
                return;
            loaded = true;
            fileMeters = 0f;
            source = "";
            loadError = "";
            if (!DataLedger.TryRead(FileName, out File_ parsed, out loadError))
                return;
            fileMeters = parsed.sync_range_m;
            source = parsed.source ?? "";
        }
    }
}
