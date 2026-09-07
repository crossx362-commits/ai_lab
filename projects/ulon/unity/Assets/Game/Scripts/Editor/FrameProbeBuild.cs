using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// 프레임 시간 기준선을 재기 위한 **Development 스탠드얼론 빌드**(검수 2026-09-07).
    /// 매 랩 돌리지 않는다 — 기준선을 갱신할 때만. 실행은 `bash tools/frame_probe.sh`.
    /// </summary>
    public static class FrameProbeBuild
    {
        [MenuItem("Ulon/Frame Probe Build")]
        public static void Run() => Build(true);

        /// <summary>
        /// **릴리스 빌드로 다시 잰다**(검수 지시 2026-09-07). Development 빌드는 프로파일러 훅과
        /// 디버그 검사를 달고 돌아서 **p95 튐이 부풀 수 있다** — 「21~24ms 튐」이 게임의 성질인지
        /// 계측기의 성질인지 가르려면 같은 장면을 릴리스로 한 번 더 재야 한다.
        /// 결과는 개발 빌드와 **다른 경로**에 둔다(둘을 나란히 놓고 비교하려고).
        /// </summary>
        [MenuItem("Ulon/Frame Probe Build (Release)")]
        public static void RunRelease() => Build(false);

        static void Build(bool development)
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath,
                development ? "../../builds/frameprobe" : "../../builds/frameprobe_release"));
            Directory.CreateDirectory(dir);
            string target = Path.Combine(dir, "Ulon.app");

            var opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Game/Scenes/Bootstrap.unity" },
                locationPathName = target,
                target = BuildTarget.StandaloneOSX,
                options = development ? BuildOptions.Development : BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log("[Ulon] 프레임 프로브 빌드 — " + report.summary.result + ", " +
                      report.summary.totalSize + "바이트, " + target);
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("빌드 실패: " + report.summary.result);
        }
    }
}
