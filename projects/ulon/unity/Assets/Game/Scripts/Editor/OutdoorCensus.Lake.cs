using UnityEngine;
using Ulon.Shared;

namespace Ulon.Editor
{
    public static partial class OutdoorCensus
    {
        /// <summary>
        /// **호수는 호수로 읽히나 — 자리를 고쳤는데도 화면이 「바다 만」이라서 센다**(2026-09-10).
        ///
        /// `15`의 카메라 자리를 대상에서 유도하게 고쳤고 계측은 **호수 7/7 · 강 7/7**이라고 했다.
        /// 그런데 PNG를 열어 보면 여전히 만과 부두다. **자와 눈이 엇갈릴 때는 세계를 의심한다** —
        /// 「담겼나」와 「그것으로 읽히나」는 다른 물음이기 때문이다(강 셈 머리말에 적어 둔 구멍).
        ///
        /// 재는 것:
        ///   ① **호수 물가가 닫혀 있나** — 중심에서 열두 방위로 나가며 물이 끝나는 거리.
        ///   ①-나 **출구는 목인가** — 호수 물가에서 강 채널의 총폭.
        ///   ② **호수가 바다와 한 몸인가** — 아래 `Measure`의 두 플러드필.
        ///   ③ **고른 자리에서 대상이 화면 어디 찍히나** — 「7/7」이 화면 구석의 몇 픽셀인지 확인한다.
        ///
        /// **이 자가 못 보는 것**: 물 재질·색은 안 본다. ①의 부두 방향 두 방위가 3~4m로 나오는 것은
        /// 결함이 아니라 **잔교 둑**(`RaisePier`)이다 — 그 자리는 일부러 수면 위다.
        /// </summary>
        public static void RunLakeRead()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);

            float sea = WorldTerrain.SeaLevel;

            // ① 물가가 닫혀 있나.
            string edges = "";
            int openDirs = 0;
            for (float a = 0f; a < 360f; a += 30f)
            {
                float dx = Mathf.Cos(a * Mathf.Deg2Rad), dz = Mathf.Sin(a * Mathf.Deg2Rad);
                float end = -1f;
                for (float d = 1f; d <= 90f; d += 1f)
                    if (WorldTerrain.HeightAt(WorldTerrain.LakeX + dx * d, WorldTerrain.LakeZ + dz * d) >= sea)
                    { end = d; break; }
                if (end < 0f) { openDirs++; edges += " " + a.ToString("0") + "°:열림"; }
                else edges += " " + a.ToString("0") + "°:" + end.ToString("0") + "m";
            }
            Debug.Log("[호수] ① 물가 — 중심에서 물이 끝나는 거리(반경 " + WorldTerrain.LakeRadius + "m) ·" +
                      edges + " · 90m까지 안 끝난 방위 " + openDirs + "개");
            Debug.Log("[호수] ①-나 출구 — 목의 총폭 " + (WorldTerrain.ChannelHalfWidth(WorldTerrain.LakeX -
                      WorldTerrain.LakeRadius) * 2f).ToString("0.0") + "m · 하류 강폭 " +
                      (WorldTerrain.RiverHalfWidth * 2f).ToString("0.0") + "m");

            var r = Measure();
            Debug.Log("[호수] ② 몸통(목 안 넘음) — " + r.BodyArea.ToString("0") + "㎡ (원장 면적 " +
                      r.LedgerArea.ToString("0") + "㎡의 " + r.Ratio.ToString("0.00") + "배) · 가장 먼 물 " +
                      r.BodyFar.ToString("0") + "m · 해안 밖까지 " +
                      (r.BodyReachesSea ? "**이어짐 — 만이다**" : "안 이어짐 — 닫힌 호수다"));
            Debug.Log("[호수] ②-라 개구부 — " + r.OpenCount + "곳 · 가장 넓은 것 " +
                      r.OpenWidest.ToString("0.0") + "m");
            Debug.Log("[호수] ②-다 물가 반경 중앙값 — " + r.ShoreMedian.ToString("0.0") + "m (원장 " +
                      WorldTerrain.LakeRadius + "m)");
            Debug.Log("[호수] ②-나 물길 포함 — " + r.AllArea.ToString("0") + "㎡ · 가장 먼 물 " +
                      r.AllFar.ToString("0") + "m · 바다까지 " +
                      (r.RiverReachesSea ? "이어짐(강이 흐른다)" : "**안 이어짐 — 강이 끊겼다**"));

            // ③ 고른 자리에서 대상이 화면 어디 찍히나(0~1 정규화, 0.5가 화면 한가운데).
            var shots = QaShots.BuildShots();
            for (int s = 0; s < shots.Length; s++)
            {
                if (QaShots.NameOf(shots[s]) != "15_lake_river") continue;
                QaShots.EyeOf(shots[s], out Vector3 eye, out Vector3 look);
                Debug.Log("[호수] ③ 눈 (" + eye.x.ToString("0") + ", " + eye.y.ToString("0") + ", " +
                          eye.z.ToString("0") + ") · 봄 (" + look.x.ToString("0") + ", " + look.z.ToString("0") + ")");
                Debug.Log("[호수] ③ 호수 중심 " + ScreenAt(eye, look, new Vector3(WorldTerrain.LakeX, sea, WorldTerrain.LakeZ)));
                for (float x = WorldTerrain.RiverFromX; x >= WorldTerrain.RiverToX; x -= 20f)
                {
                    float cz = WorldTerrain.RiverZ + Mathf.Sin((x - WorldTerrain.RiverFromX) * 0.06f) * 6f;
                    Debug.Log("[호수] ③ 강 x=" + x.ToString("0") + " " + ScreenAt(eye, look, new Vector3(x, sea, cz)));
                }
            }
            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(0);
        }

        /// <summary>「물이 좁은 목인가」를 가르는 반경 — 이만큼 사방이 물이어야 「넓은 물」이다.</summary>
        const float NeckReach = 8f;

        public struct Reading
        {
            public float BodyArea, AllArea, LedgerArea, Ratio, BodyFar, AllFar, ShoreMedian;
            public float OpenWidest;
            public int OpenCount;
            public bool BodyReachesSea, RiverReachesSea;
        }

        /// <summary>
        /// 물가 반경의 **중앙값** — 「`carve`가 원장(21m)대로 팠나」를 직접 묻는다.
        /// 평균이 아니라 중앙값인 것은 **잔교 둑** 방향 두 방위가 3~4m로 나오기 때문이다
        /// (그 자리는 일부러 수면 위다 — 결함이 아니므로 자가 끌려가면 안 된다).
        /// </summary>
        static float ShoreRadiusMedian()
        {
            var ds = new System.Collections.Generic.List<float>();
            for (float a = 0f; a < 360f; a += 30f)
            {
                float dx = Mathf.Cos(a * Mathf.Deg2Rad), dz = Mathf.Sin(a * Mathf.Deg2Rad);
                float end = 90f;
                for (float d = 1f; d <= 90f; d += 0.5f)
                    if (WorldTerrain.HeightAt(WorldTerrain.LakeX + dx * d, WorldTerrain.LakeZ + dz * d) >= WorldTerrain.SeaLevel)
                    { end = d; break; }
                ds.Add(end);
            }
            ds.Sort();
            return ds[ds.Count / 2];
        }

        /// <summary>
        /// **물가 원 위의 개구부** — 「호수가 닫혔나」를 가장 곧게 묻는 자다.
        /// 물가 **바깥** 원(`LakeRadius + OpenRingOut`)을 0.5°씩 돌며 물인 구간을 잇는다:
        /// 봉합됐다면 **출구 하나뿐**이고 그 폭은 강 채널 폭(6m)이다. 옛 규칙에서는 넓은 강 채널이
        /// 그대로 개구부라 16m가 된다. **물가 원 자체에서 재면 안 된다** — 거기는 경계라
        /// 부동소수가 안팎을 오가며 개구부가 93조각으로 부서졌다(첫 판 실측).
        ///
        /// 플러드필로는 이걸 못 가른다 — 목 차단 필터가 옛 강(반폭 7.75m)까지 막아 버려
        /// **옛 세계도 「닫힌 호수」로 보인다**(첫 판에 실제로 그랬고, NC가 엉뚱한 이유로 울었다).
        /// </summary>
        static void OpenSpans(out int count, out float widest)
        {
            const int steps = 720;
            const float OpenRingOut = 1.5f;
            float ring = WorldTerrain.LakeRadius + OpenRingOut;
            var wet = new bool[steps];
            for (int i = 0; i < steps; i++)
            {
                float a = i * Mathf.PI * 2f / steps;
                wet[i] = WorldTerrain.HeightAt(WorldTerrain.LakeX + Mathf.Cos(a) * ring,
                                               WorldTerrain.LakeZ + Mathf.Sin(a) * ring) < WorldTerrain.SeaLevel;
            }
            float arc = 2f * Mathf.PI * ring / steps;
            count = 0;
            widest = 0f;
            int i0 = 0;
            while (i0 < steps && wet[i0]) i0++;      // 뭍에서 시작해야 한 구간이 둘로 안 잘린다
            if (i0 == steps) { count = 1; widest = steps * arc; return; }   // 원 전체가 물
            int run = 0;
            for (int k = 0; k < steps; k++)
            {
                bool w = wet[(i0 + k) % steps];
                if (w) run++;
                else if (run > 0)
                {
                    count++;
                    widest = Mathf.Max(widest, run * arc);
                    run = 0;
                }
            }
            if (run > 0) { count++; widest = Mathf.Max(widest, run * arc); }
        }

        /// <summary>
        /// **호수가 닫혀 있나 · 강이 흐르나 — 게이트와 셈이 같이 쓰는 자**(두 벌을 두면 갈라진다).
        ///
        /// 플러드필을 **두 번** 돌린다. 한 번은 **목을 못 넘게**(넓은 물로만 번짐) — 이게 호수 몸통이고,
        /// 봉합됐다면 원장 면적 언저리에서 멈춘다. 한 번은 **그냥** — 이건 물길을 타고 바다까지 가야 한다.
        /// 그냥 채우는 것 하나만으로는 못 가른다: 폭 6m 출구도 격자에서는 통로라 봉합해도 「만」이라 한다.
        /// </summary>
        public static Reading Measure()
        {
            float sea = WorldTerrain.SeaLevel;
            const float step = 2f, half = 160f;
            int n = Mathf.RoundToInt(half * 2f / step) + 1;
            var water = new bool[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    water[i, j] = WorldTerrain.HeightAt(-half + i * step, -half + j * step) < sea;

            var wide = new bool[n, n];
            int reach = Mathf.RoundToInt(NeckReach / step);
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    wide[i, j] = water[i, j] &&
                                 i - reach >= 0 && i + reach < n && j - reach >= 0 && j + reach < n &&
                                 water[i - reach, j] && water[i + reach, j] && water[i, j - reach] && water[i, j + reach];

            int si = Mathf.RoundToInt((WorldTerrain.LakeX + half) / step);
            int sj = Mathf.RoundToInt((WorldTerrain.LakeZ + half) / step);
            var r = new Reading { LedgerArea = Mathf.PI * WorldTerrain.LakeRadius * WorldTerrain.LakeRadius,
                                  ShoreMedian = ShoreRadiusMedian() };
            OpenSpans(out r.OpenCount, out r.OpenWidest);
            if (!water[si, sj])
                return r;                                   // 중심이 마른 땅 — 부르는 쪽이 0을 보고 판단한다

            int body = Fill(water, wide, si, sj, n, step, half, true, out r.BodyFar, out r.BodyReachesSea);
            int all = Fill(water, wide, si, sj, n, step, half, false, out r.AllFar, out r.RiverReachesSea);
            r.BodyArea = body * step * step;
            r.AllArea = all * step * step;
            r.Ratio = r.BodyArea / r.LedgerArea;
            return r;
        }

        static int Fill(bool[,] water, bool[,] wide, int si, int sj, int n, float step, float half,
                        bool neckBlocks, out float farthest, out bool reachesSea)
        {
            var seen = new bool[n, n];
            var q = new System.Collections.Generic.Queue<Vector2Int>();
            q.Enqueue(new Vector2Int(si, sj));
            seen[si, sj] = true;
            farthest = 0f;
            reachesSea = false;
            int cells = 0;
            var d4 = new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                cells++;
                float wx = -half + c.x * step, wz = -half + c.y * step;
                float rad = new Vector2(wx, wz).magnitude;
                if (rad > farthest) farthest = rad;
                if (rad > WorldTerrain.CoastEnd) reachesSea = true;
                foreach (var d in d4)
                {
                    int ni = c.x + d.x, nj = c.y + d.y;
                    if (ni < 0 || nj < 0 || ni >= n || nj >= n) continue;
                    if (seen[ni, nj] || !water[ni, nj]) continue;
                    if (neckBlocks && !wide[ni, nj]) continue;      // 좁은 물길로는 안 새어 나간다
                    seen[ni, nj] = true;
                    q.Enqueue(new Vector2Int(ni, nj));
                }
            }
            return cells;
        }

        /// <summary>이 점이 화면 어디에 찍히나 — 좌0/우1, 위0/아래1. 화각 55°·16:9.</summary>
        static string ScreenAt(Vector3 eye, Vector3 look, Vector3 p)
        {
            var fwd = (look - eye).normalized;
            var v = p - eye;
            float depth = Vector3.Dot(v, fwd);
            if (depth < 0.5f) return "카메라 뒤";
            var right = Vector3.Cross(Vector3.up, fwd).normalized;
            var up = Vector3.Cross(fwd, right);
            float tanY = Mathf.Tan(55f * 0.5f * Mathf.Deg2Rad);
            float u = 0.5f + Vector3.Dot(v, right) / (depth * tanY * 16f / 9f) * 0.5f;
            float w = 0.5f - Vector3.Dot(v, up) / (depth * tanY) * 0.5f;
            bool inside = u >= 0f && u <= 1f && w >= 0f && w <= 1f;
            float dist = v.magnitude;
            bool blocked = Physics.Raycast(eye, v.normalized, out RaycastHit hit, dist - 0.6f) && hit.distance < dist - 0.6f;
            return "화면 (" + u.ToString("0.00") + ", " + w.ToString("0.00") + ") · 거리 " + dist.ToString("0") +
                   "m · " + (inside ? (blocked ? "가림(" + hit.collider.name + ")" : "보임") : "프레임 밖");
        }
    }
}
