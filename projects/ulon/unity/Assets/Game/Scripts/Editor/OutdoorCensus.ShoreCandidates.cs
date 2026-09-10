using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class OutdoorCensus
    {
        /// <summary>
        /// **물가 톱니 처방 후보를 같은 자리에서 나란히 굽는다**(검수 지시 ⓐ, 2026-09-10).
        ///
        /// 후보 셋 중 **「물가 셀만 잘게」는 뺐다** — 절반 해상도로 **실제로 다시 구워** 셀이
        /// 18.83 → 37.65px(정확히 두 배)가 됐는데도 노치 크기 중앙값이 31.6 → 31.7 ·
        /// 4.4 → 4.1 · 6.6 → 6.1px로 그대로였다. **메시 무죄가 확정**이라 그 후보는 값비싼 헛일이다
        /// (검수 조건: 안 변하면 빼고 나머지 둘만).
        ///
        /// 남은 둘:
        /// **A 수면 가장자리를 부드럽게** — 수면을 반투명으로 만들어 물↔뭍의 딱딱한 색 단차를 무르게.
        /// **B 물가에 얕은 전이 띠** — 물 높이 ±0.8m를 물 쪽으로 눌러 완만한 여울을 만든다.
        ///
        /// 합격선은 눈이다: 「물가가 톱니가 아니라 **물가로** 보이나」. 셈은 참고로 같이 찍는다.
        /// 둘 다 **되돌리는 것까지가 이 단계**다.
        /// </summary>
        public static void RunShoreCandidates()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            string outDir = System.IO.Path.Combine(Application.dataPath, "../../builds/qa/shore");
            System.IO.Directory.CreateDirectory(outDir);
            var shots = new[] { "64_river_bend", "63_pier_cutface" };

            foreach (var sh in shots) ShotTo(sh, System.IO.Path.Combine(outDir, sh + "_cand0.png"));
            Debug.Log("[물가후보] 0 지금 — 찍음");

            float keepA = VisualSliceBuilder.WaterAlphaOverride;
            try
            {
                VisualSliceBuilder.WaterAlphaOverride = 0.70f;
                VisualSliceBuilder.RebuildWater();
                UnityEditor.AssetDatabase.SaveAssets();
                foreach (var sh in shots) ShotTo(sh, System.IO.Path.Combine(outDir, sh + "_candA.png"));
                Debug.Log("[물가후보] A 반투명 수면(0.70) — 찍음");
            }
            finally
            {
                VisualSliceBuilder.WaterAlphaOverride = keepA;
                VisualSliceBuilder.RebuildWater();
                UnityEditor.AssetDatabase.SaveAssets();
            }

            // C — **둑이 물 위로 서는 높이**를 0.05 → 0.45m로 올린다. 물가가 물 높이 바로 위
            // 평지에 앉아 있으면 선이 잔잡음으로 정해져 길게 들쭉날쭉해진다는 가설.
            float keepC = WorldTerrain.ShoreLip;
            try
            {
                WorldTerrain.ShoreLip = 0.45f;
                VisualSliceBuilder.EnsureVillageTerrain();
                UnityEditor.AssetDatabase.SaveAssets();
                foreach (var sh in shots) ShotTo(sh, System.IO.Path.Combine(outDir, sh + "_candC.png"));
                Debug.Log("[물가후보] C 둑 턱 0.45m — 찍음");
                foreach (var sh in shots) MeasureShoreEdge(sh);
            }
            finally
            {
                WorldTerrain.ShoreLip = keepC;
                VisualSliceBuilder.EnsureVillageTerrain();
                UnityEditor.AssetDatabase.SaveAssets();
            }

            bool keepB = WorldTerrain.ShoreBandOn;
            try
            {
                WorldTerrain.ShoreBandOn = true;
                VisualSliceBuilder.EnsureVillageTerrain();
                UnityEditor.AssetDatabase.SaveAssets();
                foreach (var sh in shots) ShotTo(sh, System.IO.Path.Combine(outDir, sh + "_candB.png"));
                Debug.Log("[물가후보] B 얕은 전이 띠(±0.8m) — 찍음");
                foreach (var sh in shots) MeasureShoreEdge(sh);
            }
            finally
            {
                WorldTerrain.ShoreBandOn = keepB;
                VisualSliceBuilder.EnsureVillageTerrain();
                UnityEditor.AssetDatabase.SaveAssets();
                Debug.Log("[물가후보] 원장 지형으로 되돌렸다.");
            }
        }
    }
}
