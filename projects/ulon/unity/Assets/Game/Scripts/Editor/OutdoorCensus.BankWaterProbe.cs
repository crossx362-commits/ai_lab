using UnityEngine;

namespace Ulon.Editor
{
    public static partial class OutdoorCensus
    {
        /// <summary>
        /// **둑 잔결 자의 상자 안에 물이 들어 있나** — 판별 테스트(2026-09-11).
        ///
        /// 물 재작업 뒤 `AssertBankNotBlocky`가 0.52로 울었다(상한 0.45). 그 자는 `64`의
        /// **화면 고정 상자**(x150~620, y300~590) 안에서 잔결의 이웃 상관을 재는데, 그 상자가
        /// 뭍만 담는다는 보장이 없다. 물이 매끈해지면 상자 안 상관은 **둑과 무관하게** 올라간다.
        ///
        /// 그래서 원인을 이름 붙이기 전에 갈라 본다: **수면 렌더러만 끄고 같은 자를 다시 잰다.**
        /// 물이 상자 밖이면 두 수가 같을 것이고, 물이 범인이면 물을 끈 판이 옛 눈금으로 돌아온다.
        /// </summary>
        public static void RunBankWaterProbe()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);

            VisualSliceBuilder.RebuildWater();

            // (`BankLumpiness`는 2026-09-11부터 **스스로 물을 끄고** 잰다 — 아래 NC는 그 새 눈금이다.)
            float now = BankLumpiness();
            int keepGravel = VisualSliceBuilder.GravelPatternOverride;
            float nc;
            try
            {
                VisualSliceBuilder.GravelPatternOverride = 1;   // 옛 무늬(층리) — 자가 물려야 한다
                VisualSliceBuilder.EnsureVillageTerrain();
                UnityEditor.AssetDatabase.SaveAssets();
                nc = BankLumpiness();
            }
            finally
            {
                VisualSliceBuilder.GravelPatternOverride = keepGravel;
                VisualSliceBuilder.EnsureVillageTerrain();
                UnityEditor.AssetDatabase.SaveAssets();
            }
            Debug.Log("[둑물판별] 물 끄고 잰 이웃 상관 r(1) — 지금 " + now.ToString("0.00") +
                      " · NC 옛 무늬 " + nc.ToString("0.00") + " (상한 0.45)");

            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(0);
        }
    }
}
