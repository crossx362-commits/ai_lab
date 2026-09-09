using UnityEngine;
using Ulon.Shared;

namespace Ulon.Editor
{
    public static partial class OutdoorCensus
    {
        /// <summary>
        /// **이 물이 어디서 와서 어디로 가나 — 세기만 한다**(검수·대장 조건 2026-09-09).
        ///
        /// 판정 기준은 「산자락이 마르다」가 아니라 **「강이 마른 땅에서 뚝 시작하거나 끝나 보이나」**다.
        /// 그래서 강 중심선을 따라 걸으며 **지표가 수면 아래로 내려가는 첫 자리**(물이 보이기 시작하는 곳)와
        /// **마지막 자리**를 찾고, 그 양 끝이 무엇에 닿는지 적는다: 바다에 닿으면 하구고, 마른 땅 한복판이면
        /// 상식 모순이다. 호수도 같은 방식으로 물가와 강의 사이 거리를 잰다.
        ///
        /// 아무것도 고치지 않고 아무것도 판정하지 않는다.
        ///
        /// **이 자가 못 보는 것**: 지표 높이만 읽는다 — 물 메시가 실제로 어디까지 그려지는지(렌더러 범위)와
        /// 화면에서 그 끝이 보이는지는 모른다. 그 둘은 QA 샷을 눈으로 봐야 한다.
        /// </summary>
        public static void RunRiverMouth()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            CountRiverMouth();
            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(0);
        }

        [UnityEditor.MenuItem("Ulon/Count River Mouth")]
        public static void CountRiverMouth()
        {
            float sea = WorldTerrain.SeaLevel;
            // 강 중심선은 지형 원장과 **같은 식**을 쓴다(굽는 쪽과 재는 쪽이 갈리면 숫자가 세계가 아니다).
            float firstWet = float.NaN, lastWet = float.NaN, deepest = 99f, deepestX = 0f;
            int wet = 0, dry = 0;
            for (float x = WorldTerrain.RiverFromX + 1f; x >= WorldTerrain.RiverToX - 1f; x -= 0.5f)
            {
                float cz = WorldTerrain.RiverZ + Mathf.Sin((x - WorldTerrain.RiverFromX) * 0.06f) * 6f;
                float h = WorldTerrain.HeightAt(x, cz);
                if (h < sea)
                {
                    if (float.IsNaN(firstWet)) firstWet = x;
                    lastWet = x;
                    wet++;
                    if (h < deepest) { deepest = h; deepestX = x; }
                }
                else dry++;
            }
            Debug.Log("[Census] 강 — 중심선 표본 " + (wet + dry) + "개 중 수면 아래 " + wet + "개 · 물이 보이는 구간 x " +
                      firstWet.ToString("0.0") + " → " + lastWet.ToString("0.0") + " · 가장 깊은 곳 " +
                      deepest.ToString("0.00") + "m(x " + deepestX.ToString("0.0") + ", 수면 " + sea.ToString("0.0") + ")");

            // **양 끝이 무엇에 닿나** — 물 구간의 바로 바깥에서 지표가 얼마나 솟는지 본다.
            // 하구 쪽이 바다로 이어지면 그 밖도 수면 아래여야 하고, 마른 땅에서 끊기면 급히 솟는다.
            Report("시작 쪽", firstWet, +1f, sea);
            Report("끝 쪽", lastWet, -1f, sea);

            // 호수와 강 사이 — 「호수에서 나온 물이 강이 된다」로 읽히려면 둘이 이어져 있어야 한다.
            float gap = Mathf.Abs(WorldTerrain.LakeX - WorldTerrain.RiverFromX) - WorldTerrain.LakeRadius;
            Debug.Log("[Census] 호수↔강 — 호수 중심 x " + WorldTerrain.LakeX.ToString("0") + "(반지름 " +
                      WorldTerrain.LakeRadius.ToString("0") + ") · 강 시작 x " + WorldTerrain.RiverFromX.ToString("0") +
                      " · 물가에서 강 시작까지 " + gap.ToString("0.0") + "m");
        }

        static void Report(string side, float x, float dir, float sea)
        {
            if (float.IsNaN(x))
            {
                Debug.Log("[Census] 강 " + side + " — 수면 아래 자리가 없다");
                return;
            }
            var sb = new System.Text.StringBuilder();
            for (int i = 1; i <= 6; i++)
            {
                float sx = x + dir * i * 4f;
                float cz = WorldTerrain.RiverZ + Mathf.Sin((sx - WorldTerrain.RiverFromX) * 0.06f) * 6f;
                float h = WorldTerrain.HeightAt(sx, cz);
                sb.Append("x " + sx.ToString("0") + " → " + h.ToString("0.00") + "m" + (h < sea ? "(물)" : "") + "  ");
            }
            Debug.Log("[Census] 강 " + side + " 바깥(수면 " + sea.ToString("0.0") + ") — " + sb);
        }
    }
}
