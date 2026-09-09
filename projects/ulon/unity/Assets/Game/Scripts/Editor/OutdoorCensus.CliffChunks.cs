using UnityEngine;
using Ulon.Shared;

namespace Ulon.Editor
{
    public static partial class OutdoorCensus
    {
        /// <summary>
        /// **두 자리에서 덩이가 화면에 몇 개로 읽히나**(검수 랩 ㉣ — 「두 자리를 같이 놓고 유도하라」).
        ///
        /// 주기를 화면 없이 정하면 반드시 한쪽이 틀린다: `16`은 **가까운 수직 암벽**이고 `03`은
        /// **먼 둥근 능선**이라, 같은 31m 덩이가 한쪽에서는 화면 절반이고 다른 쪽에서는 손톱만 하다.
        /// 그래서 QA 샷과 **같은 눈·같은 화각**으로 화면 격자에 광선을 쏘아, 맞은 자리의 마스크를
        /// 읽고 **화면에서** 센다: 한 톤이 이어지는 가로 길이(화면 폭의 %)와 그때 표면에서 걸은 거리(m).
        ///
        /// 아무것도 고치지 않고 아무것도 판정하지 않는다 — 세기만 한다.
        /// </summary>
        [UnityEditor.MenuItem("Ulon/Count Cliff Chunks")]
        public static void CountCliffChunksMenu() { CountCliffChunks(); }

        /// <summary>배치에서 이 절만 돌린다(전체 센서스는 길다).</summary>
        public static void RunCliffChunks()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            CountCliffChunks();
            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(0);
        }

        public static void CountCliffChunks()
        {
            var huntEye = new Vector3(VisualSliceBuilder.HuntViewEye.x,
                                      WorldTerrain.HeightAt(VisualSliceBuilder.HuntViewEye.x, VisualSliceBuilder.HuntViewEye.y) + VisualSliceBuilder.HuntViewEyeHeight,
                                      VisualSliceBuilder.HuntViewEye.y);
            var huntTarget = new Vector3(VisualSliceBuilder.HuntViewTarget.x,
                                         WorldTerrain.HeightAt(VisualSliceBuilder.HuntViewTarget.x, VisualSliceBuilder.HuntViewTarget.y) + VisualSliceBuilder.HuntViewTargetHeight,
                                         VisualSliceBuilder.HuntViewTarget.y);
            Scan("16_mountain_ridge", new Vector3(158f, 72f, -86f), new Vector3(78f, WorldTerrain.LandBase + 24f, 26f));
            Scan("03_hunt_mobs", huntEye, huntTarget);
        }

        /// <summary>QA 샷과 같은 화각(55°)·같은 비율(1280×720)로 화면 격자를 훑는다.</summary>
        static void Scan(string shot, Vector3 eye, Vector3 target)
        {
            const int cols = 64, rows = 18;
            const float fov = 55f, aspect = 1280f / 720f;
            var fwd = (target - eye).normalized;
            var right = Vector3.Cross(Vector3.up, fwd).normalized;
            var up = Vector3.Cross(fwd, right);
            float tanY = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            float tanX = tanY * aspect;

            int rockPix = 0, darkPix = 0, lightPix = 0, flips = 0, longestRun = 0;
            float runSurfaceSum = 0f, runSurfaceMax = 0f;
            int runs = 0;
            for (int r = 0; r < rows; r++)
            {
                float sy = (r + 0.5f) / rows * 2f - 1f;          // 화면 아래 −1 ~ 위 +1
                int run = 0;
                bool haveTone = false, toneDark = false;
                Vector3 runStart = Vector3.zero, last = Vector3.zero;
                for (int c = 0; c < cols; c++)
                {
                    float sx = (c + 0.5f) / cols * 2f - 1f;
                    var dir = (fwd + right * (sx * tanX) + up * (sy * tanY)).normalized;
                    if (!March(eye, dir, out Vector3 hit))
                    {
                        haveTone = false; run = 0;
                        continue;
                    }
                    // 바위가 우세한 자리만 센다 — 풀밭에서 덩이를 세면 아무 뜻이 없다.
                    if (WorldSplat.MacroSlopeTan(hit.x, hit.z) < 0.7f)
                    {
                        haveTone = false; run = 0;
                        continue;
                    }
                    rockPix++;
                    bool dark = WorldSplat.DarkCliffAt(hit.x, hit.z) > 0.5f;
                    if (dark) darkPix++; else lightPix++;
                    if (haveTone && dark == toneDark)
                    {
                        run++;
                        last = hit;
                    }
                    else
                    {
                        if (haveTone)
                        {
                            flips++;
                            runs++;
                            float surf = Vector3.Distance(runStart, last);
                            runSurfaceSum += surf;
                            runSurfaceMax = Mathf.Max(runSurfaceMax, surf);
                            longestRun = Mathf.Max(longestRun, run);
                        }
                        haveTone = true; toneDark = dark; run = 1;
                        runStart = hit; last = hit;
                    }
                }
                longestRun = Mathf.Max(longestRun, run);
            }
            float avgSurface = runs > 0 ? runSurfaceSum / runs : 0f;
            NearFace(shot, eye, fwd, right, up, tanX, tanY);
            Debug.Log("[Census] 덩이/화면 " + shot + " — 암벽 픽셀 " + rockPix + "(밝은 " + lightPix + "·어두운 " + darkPix +
                      ") · 톤이 바뀐 횟수 " + flips + " · 한 톤 최장 " + (longestRun * 100f / cols).ToString("0.0") +
                      "% 화면폭 · 한 톤 구간 표면 길이 평균 " + avgSurface.ToString("0.0") + "m·최대 " +
                      runSurfaceMax.ToString("0.0") + "m");
        }

        /// <summary>
        /// **화면 왼쪽 아래 조각만 따로 센다**(랩 ㉣ — 거기만 한 톤으로 남았다). 화면 전체 통계는
        /// 「어딘가는 갈렸다」로 가려진다 — 안 갈린 자리를 지목해서 그 자리의 **값**을 봐야
        /// 「마스크가 균일한 것」인지 「규칙이 그 자리에서 안 도는 것」인지 갈린다.
        /// </summary>
        static void NearFace(string shot, Vector3 eye, Vector3 fwd, Vector3 right, Vector3 up, float tanX, float tanY)
        {
            int n = 0;
            float sum = 0f, min = 1f, max = 0f, steepSum = 0f, distSum = 0f;
            for (int r = 0; r < 10; r++)
                for (int c = 0; c < 24; c++)
                {
                    float sx = -1f + (c + 0.5f) / 24f * 0.66f;       // 화면 왼쪽 2/3까지
                    float sy = -1f + (r + 0.5f) / 10f * 0.9f;        // 아래쪽 절반
                    var dir = (fwd + right * (sx * tanX) + up * (sy * tanY)).normalized;
                    if (!March(eye, dir, out Vector3 hit))
                        continue;
                    float tan = WorldSplat.MacroSlopeTan(hit.x, hit.z);
                    if (tan < 0.7f)
                        continue;
                    float v = WorldSplat.DarkCliffAt(hit.x, hit.z);
                    sum += v; min = Mathf.Min(min, v); max = Mathf.Max(max, v);
                    steepSum += Mathf.Clamp01((tan - 1.0f) / 1.0f);
                    distSum += Vector3.Distance(eye, hit);
                    n++;
                }
            if (n == 0)
            {
                Debug.Log("[Census] 가까운 면 " + shot + " — 표본 0곳(그 자리에 암벽이 안 잡힌다)");
                return;
            }
            Debug.Log("[Census] 가까운 면 " + shot + " — 표본 " + n + "곳 · 마스크 평균 " + (sum / n).ToString("0.00") +
                      " (최소 " + min.ToString("0.00") + "·최대 " + max.ToString("0.00") + ") · 벽 가중 평균 " +
                      (steepSum / n).ToString("0.00") + " · 눈에서 평균 " + (distSum / n).ToString("0") + "m");
        }

        /// <summary>광선을 지형에 맞힌다 — 콜라이더가 아니라 높이 원장을 직접 읽는다(재는 자가 세계를 안 건드린다).</summary>
        static bool March(Vector3 eye, Vector3 dir, out Vector3 hit)
        {
            hit = Vector3.zero;
            float t = 0.5f, step = 0.5f;
            var prev = eye;
            while (t < 400f)
            {
                var p = eye + dir * t;
                if (p.y < WorldTerrain.HeightAt(p.x, p.z))
                {
                    // 뒤로 한 발 물러 반씩 좁힌다 — 격자 한 칸이 곧 화면 한 조각이라 대충 맞히면 톤이 흔들린다.
                    var lo = prev; var hi = p;
                    for (int i = 0; i < 20; i++)
                    {
                        var mid = (lo + hi) * 0.5f;
                        if (mid.y < WorldTerrain.HeightAt(mid.x, mid.z)) hi = mid; else lo = mid;
                    }
                    hit = hi;
                    return Mathf.Abs(hit.x) <= 149f && Mathf.Abs(hit.z) <= 149f;
                }
                prev = p;
                t += step;
                step = Mathf.Min(2f, step * 1.05f);
            }
            return false;
        }
    }
}
