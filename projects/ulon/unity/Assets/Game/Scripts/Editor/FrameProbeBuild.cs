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
        public static void Run()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../builds/frameprobe"));
            Directory.CreateDirectory(dir);
            string target = Path.Combine(dir, "Ulon.app");

            var opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Game/Scenes/Bootstrap.unity" },
                locationPathName = target,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.Development,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log("[Ulon] 프레임 프로브 빌드 — " + report.summary.result + ", " +
                      report.summary.totalSize + "바이트, " + target);
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("빌드 실패: " + report.summary.result);
        }
    }
}
