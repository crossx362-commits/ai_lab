using System;
using UnityEngine;

namespace Ulon.Shared
{
    /// <summary>
    /// 기획 §7.2.1 Classic 2D 이동 리듬. 1타일 = 1m. 수치는 move_speed.json.
    /// 출처: http://wikiwiki.jp/uoemu/Tips9 , 보조 https://github.com/xrip/uo-client
    /// </summary>
    public static class MoveSpeed
    {
        public const string FileName = "move_speed.json";
        public const float FallbackWalkStepSeconds = 0.40f;
        public const float FallbackRunStepSeconds = 0.20f;
        public const float FallbackMountWalkStepSeconds = 0.20f;
        public const float FallbackMountRunStepSeconds = 0.10f;
        public const float FallbackTileMeters = 1f;
        /// <summary>loop#36 이전 ClickMotor 기본값. NC가 이 값으로 돌아가면 게이트가 실패해야 한다.</summary>
        public const float LegacyMetersPerSecond = 4.2f;

        [Serializable]
        class File_
        {
            public float walk_step_s;
            public float run_step_s;
            public float mount_walk_step_s;
            public float mount_run_step_s;
            public float tile_meters;
            public string source;
        }

        static bool loaded;
        static float walkStep = FallbackWalkStepSeconds;
        static float runStep = FallbackRunStepSeconds;
        static float mountWalkStep = FallbackMountWalkStepSeconds;
        static float mountRunStep = FallbackMountRunStepSeconds;
        static float tileMeters = FallbackTileMeters;
        static string source = "";
        static string loadError = "";

        public static bool NcOpen;

        public static string LoadError
        {
            get { EnsureLoaded(); return loadError; }
        }

        public static string Source
        {
            get { EnsureLoaded(); return source; }
        }

        public static float FileWalkMetersPerSecond
        {
            get { EnsureLoaded(); return Mps(walkStep); }
        }

        public static float FileRunMetersPerSecond
        {
            get { EnsureLoaded(); return Mps(runStep); }
        }

        public static float WalkMetersPerSecond => NcOpen ? LegacyMetersPerSecond : FileWalkMetersPerSecond;
        public static float RunMetersPerSecond => NcOpen ? LegacyMetersPerSecond : FileRunMetersPerSecond;
        public static float MountWalkMetersPerSecond => NcOpen ? LegacyMetersPerSecond : Mps(mountWalkStep);
        public static float MountRunMetersPerSecond => NcOpen ? LegacyMetersPerSecond : Mps(mountRunStep);

        public static void Reload()
        {
            loaded = false;
            EnsureLoaded();
        }

        public static float MetersPerSecond(bool running, bool mounted = false)
        {
            if (mounted)
                return running ? MountRunMetersPerSecond : MountWalkMetersPerSecond;
            return running ? RunMetersPerSecond : WalkMetersPerSecond;
        }

        static float Mps(float stepSeconds)
        {
            EnsureLoaded();
            if (stepSeconds <= 0.001f)
                return 0f;
            return tileMeters / stepSeconds;
        }

        static void EnsureLoaded()
        {
            if (loaded)
                return;
            loaded = true;
            walkStep = FallbackWalkStepSeconds;
            runStep = FallbackRunStepSeconds;
            mountWalkStep = FallbackMountWalkStepSeconds;
            mountRunStep = FallbackMountRunStepSeconds;
            tileMeters = FallbackTileMeters;
            source = "";
            loadError = "";
            if (!DataLedger.TryRead(FileName, out File_ parsed, out loadError))
                return;
            if (parsed.walk_step_s > 0.001f)
                walkStep = parsed.walk_step_s;
            if (parsed.run_step_s > 0.001f)
                runStep = parsed.run_step_s;
            if (parsed.mount_walk_step_s > 0.001f)
                mountWalkStep = parsed.mount_walk_step_s;
            if (parsed.mount_run_step_s > 0.001f)
                mountRunStep = parsed.mount_run_step_s;
            if (parsed.tile_meters > 0.001f)
                tileMeters = parsed.tile_meters;
            source = parsed.source ?? "";
        }
    }
}
