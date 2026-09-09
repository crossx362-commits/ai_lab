using UnityEngine;
using Ulon.Shared;

namespace Ulon.Editor
{
    public static partial class OutdoorCensus
    {
        /// <summary>
        /// **문 뒤 언덕이 무엇으로 칠해졌나**(2026-09-09, `60_d3_entrance_45` 화면).
        ///
        /// 화면에서 본 것: `07`의 언덕은 잔디인데 `60`(D3를 옆 45°에서)의 언덕은 모래로 읽힌다.
        /// 같은 규칙으로 만든 같은 언덕이 자리마다 다른 옷을 입는 것 — 재기 전에는 왜인지 모른다.
        ///
        /// 재는 것: 언덕 반경 안 **구운 알파맵**의 겹별 평균(잔디·바위·모래·자갈·마른풀·절벽)과
        /// 그 자리 경사. 규칙이 아니라 구운 것을 읽는다(「저장됐다 ≠ 반영됐다」).
        /// 대조군으로 **언덕 바깥 고리**(반경 1.0~1.6배)도 같이 센다 — 언덕이 바꾼 것인지 원래 그런 자리인지.
        /// </summary>
        public static void RunMoundSplat()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            var terrain = Terrain.activeTerrain;
            if (terrain == null || terrain.terrainData == null)
            {
                Debug.LogError("[Census] 지형이 없습니다.");
                if (Application.isBatchMode)
                    UnityEditor.EditorApplication.Exit(1);
                return;
            }
            var data = terrain.terrainData;
            int ar = data.alphamapResolution;
            var alpha = data.GetAlphamaps(0, 0, ar, ar);
            Debug.Log("[Census] 문 뒤 언덕 도포 — 언덕 안 vs 바깥 고리(같은 자리 대조)");
            foreach (var d in EntranceGeom.All)
            {
                var front = EntranceGeom.Front(d.X, d.Z, d.Yaw);
                float cx = d.X - front.x * EntranceGeom.MoundOffset, cz = d.Z - front.y * EntranceGeom.MoundOffset;
                Ring(d.Root + " 언덕 안", alpha, ar, cx, cz, 0f, EntranceGeom.MoundRadius);
                Ring(d.Root + " 바깥 고리", alpha, ar, cx, cz, EntranceGeom.MoundRadius, EntranceGeom.MoundRadius * 1.6f);
            }
            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(0);
        }

        static void Ring(string tag, float[,,] alpha, int ar, float cx, float cz, float rIn, float rOut)
        {
            int n = 0;
            float grass = 0f, rock = 0f, sand = 0f, gravel = 0f, dry = 0f, cliff = 0f, slope = 0f, rise = 0f;
            for (float x = cx - rOut; x <= cx + rOut; x += 1f)
            for (float z = cz - rOut; z <= cz + rOut; z += 1f)
            {
                float dist = Mathf.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz));
                if (dist < rIn || dist > rOut)
                    continue;
                grass  += SampleAlpha(alpha, ar, x, z, WorldSplat.Grass);
                rock   += SampleAlpha(alpha, ar, x, z, WorldSplat.Rock);
                sand   += SampleAlpha(alpha, ar, x, z, WorldSplat.Sand);
                gravel += SampleAlpha(alpha, ar, x, z, WorldSplat.Gravel);
                dry    += SampleAlpha(alpha, ar, x, z, WorldSplat.DryGrass);
                cliff  += SampleAlpha(alpha, ar, x, z, WorldSplat.CliffDark);
                slope  += Mathf.Atan(WorldSplat.MacroSlopeTan(x, z)) * Mathf.Rad2Deg;
                rise   += EntranceGeom.MoundRise(x, z);
                n++;
            }
            if (n == 0)
                return;
            Debug.Log("[Census] " + tag + " (" + n + "칸) — 풀 " + (grass / n).ToString("0.00") +
                      " 바위 " + (rock / n).ToString("0.00") + " 모래 " + (sand / n).ToString("0.00") +
                      " 자갈 " + (gravel / n).ToString("0.00") + " 마른풀 " + (dry / n).ToString("0.00") +
                      " 절벽 " + (cliff / n).ToString("0.00") + " · 평균 경사 " + (slope / n).ToString("0.0") +
                      "° · 평균 언덕 " + (rise / n).ToString("0.0") + "m");
        }
    }
}
