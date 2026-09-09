using System.Text;
using UnityEngine;
using Ulon.Shared;

namespace Ulon.Editor
{
    public static partial class OutdoorCensus
    {
        /// <summary>
        /// **호수 절개면이 무엇으로 칠해져 있나 — 세기만 한다**(검수 관찰 2026-09-09: 「`15` 한가운데의
        /// 넓은 탄색 사면이 무늬 없는 민 벽」).
        ///
        /// 「민 벽」은 아직 이름이 아니다. 후보가 셋이고 셋은 서로 다른 수리를 부른다:
        ///   ⓐ **벽인데 벽으로 안 칠해진다** — 경사는 급한데 도포가 풀·모래다(칠하는 규칙이 못 미친다).
        ///   ⓑ **벽 판정이 그 각도를 못 잡는다** — `WallRockAt`이 0에 가깝다(자가 벽이라고 안 부른다).
        ///   ⓒ **바위로 칠해져 있는데도 밋밋하다** — 도포는 바위인데 화면이 매끈하다(무늬·투영 이야기).
        /// 그래서 급경사 표본마다 **경사·벽 판정·구운 도포 상위 겹**을 같이 찍고, 산 절벽을
        /// 같은 자로 재서 나란히 놓는다(산은 화면에서 바위로 읽히므로 **정상 대조군**이다).
        ///
        /// 아무것도 고치지 않는다. **이 자가 못 보는 것**: 화면이다 — 무늬가 늘어나 보이는지는 `15`가 답한다.
        /// </summary>
        public static void RunLakeCut()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            CutFace("호수 둘레", WorldTerrain.LakeX, WorldTerrain.LakeZ, 34f);
            CutFace("산(대조군)", 0f, 105f, 30f);
            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(0);
        }

        static void CutFace(string tag, float cx, float cz, float radius)
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null || terrain.terrainData == null)
            {
                Debug.Log("[Census] " + tag + " — 지형이 없습니다(잰 것이 없다)");
                return;
            }
            var data = terrain.terrainData;
            int ar = data.alphamapResolution;
            var alpha = data.GetAlphamaps(0, 0, ar, ar);
            var sum = new float[WorldSplat.LayerCount];
            int steep = 0, all = 0, wallCalled = 0;
            float slopeMax = 0f, wallSum = 0f;
            float area = 0f;
            const float Step = 1.0f;
            for (float x = cx - radius; x <= cx + radius; x += Step)
                for (float z = cz - radius; z <= cz + radius; z += Step)
                {
                    if (new Vector2(x - cx, z - cz).magnitude > radius) continue;
                    all++;
                    float tan = WorldSplat.MacroSlopeTan(x, z);
                    if (tan < 0.577f) continue;                 // 30° 아래는 사면이 아니다
                    steep++;
                    area += Step * Step;
                    slopeMax = Mathf.Max(slopeMax, tan);
                    float wall = WorldSplat.WallRockAt(x, z);
                    wallSum += wall;
                    if (wall > 0.5f) wallCalled++;
                    for (int i = 0; i < WorldSplat.LayerCount; i++)
                        sum[i] += SampleAlpha(alpha, ar, x, z, i);
                }
            if (steep == 0)
            {
                Debug.Log("[Census] " + tag + " — 30° 넘는 표본 0곳(전체 " + all + ") — 사면이 없다");
                return;
            }
            var names = new[] { "풀", "바위", "모래", "밭흙", "부엽토", "자갈", "길", "돌포장", "마른풀", "그늘바위" };
            var sb = new StringBuilder();
            for (int i = 0; i < WorldSplat.LayerCount && i < names.Length; i++)
            {
                float share = sum[i] / steep;
                if (share >= 0.02f)
                    sb.Append(names[i]).Append(' ').Append((share * 100f).ToString("0")).Append("% · ");
            }
            Debug.Log("[Census] " + tag + " — 30°↑ 표본 " + steep + "곳(약 " + area.ToString("0") + "㎡, 전체 " + all +
                      ") · 가장 가파른 " + (Mathf.Atan(slopeMax) * Mathf.Rad2Deg).ToString("0") + "° · **벽 판정 평균 " +
                      (wallSum / steep).ToString("0.00") + "**(0.5 넘는 곳 " + wallCalled + ") · 도포: " + sb);
        }

        static float SampleAlpha(float[,,] alpha, int ar, float wx, float wz, int layer)
        {
            float half = WorldTerrain.Span * 0.5f;
            int x = Mathf.Clamp(Mathf.RoundToInt((wx + half) / WorldTerrain.Span * (ar - 1)), 0, ar - 1);
            int z = Mathf.Clamp(Mathf.RoundToInt((wz + half) / WorldTerrain.Span * (ar - 1)), 0, ar - 1);
            return alpha[z, x, layer];
        }
    }
}
