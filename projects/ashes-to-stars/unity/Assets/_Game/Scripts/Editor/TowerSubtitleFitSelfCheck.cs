using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    public static class TowerSubtitleFitSelfCheck
    {
        [MenuItem("Ashes to Stars/QA/Tower Subtitle Fit Self Check")]
        public static void Run()
        {
            string saved = Environment.GetEnvironmentVariable(TowerSubtitleFit.EnvNo);
            Environment.SetEnvironmentVariable(TowerSubtitleFit.EnvNo, null);
            if (!TowerSubtitleFit.Enabled) throw new InvalidOperationException("기본 한 줄 맞춤이 꺼져 있다");

            Environment.SetEnvironmentVariable(TowerSubtitleFit.EnvNo, "1");
            if (TowerSubtitleFit.Enabled) throw new InvalidOperationException("QA_NO가 옛 잘림을 복원하지 못한다");
            Environment.SetEnvironmentVariable(TowerSubtitleFit.EnvNo, saved);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string game = File.ReadAllText(Path.Combine(runtime, "GameScreen.cs"));
            string tower = File.ReadAllText(Path.Combine(runtime, "TowerScreen.cs"));
            if (!game.Contains("if (FitHeaderSubtitle) UiPages.LabelFit(subtitleRect, shownSub, subStyle, 10)"))
                throw new InvalidOperationException("제목판 자막이 LabelFit을 소비하지 않는다");
            if (!tower.Contains("FitHeaderSubtitle => TowerSubtitleFit.Enabled"))
                throw new InvalidOperationException("탑 화면이 맞춤 정책을 소비하지 않는다");

            Debug.Log("[TowerSubtitleFitSelfCheck] PASS — 탑 자막 한 줄 맞춤 + QA_NO 옛 잘림");
        }
    }
}
