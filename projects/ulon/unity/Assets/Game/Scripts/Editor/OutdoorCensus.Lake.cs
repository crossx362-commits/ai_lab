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
        ///      한 방위라도 안 끝나면 그 방향으로 물이 새 나간다.
        ///   ② **호수가 바다와 한 몸인가** — 수면 아래 칸을 2m 격자로 채워(플러드필) 호수 중심 덩어리가
        ///      해안(반경 `CoastEnd`) 밖에 닿는지. 닿으면 그건 호수가 아니라 **만**이다.
        ///   ③ **고른 자리에서 대상이 화면 어디 찍히나** — 「7/7」이 화면 구석의 몇 픽셀인지 확인한다.
        ///
        /// **이 자가 못 보는 것**: 물 재질·색은 안 본다. ②가 「이어져 있다」고 해도 그것이 결함인지
        /// 설계인지는 이 자가 못 정한다 — 원장에 강이 `RiverToX = -140`(해안 `CoastEnd = 128` 밖)까지
        /// 뻗어 있으니 **이어짐 자체는 의도**일 수 있다. 이 자는 사실만 찍는다.
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

            // ② 바다와 한 몸인가 — 수면 아래 칸 플러드필.
            const float step = 2f, half = 160f;
            int n = Mathf.RoundToInt(half * 2f / step) + 1;
            var water = new bool[n, n];
            var seen = new bool[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    water[i, j] = WorldTerrain.HeightAt(-half + i * step, -half + j * step) < sea;

            int si = Mathf.RoundToInt((WorldTerrain.LakeX + half) / step);
            int sj = Mathf.RoundToInt((WorldTerrain.LakeZ + half) / step);
            int cells = 0;
            float farthest = 0f;
            bool reachesSea = false;
            if (!water[si, sj])
            {
                Debug.LogWarning("[호수] ② 호수 중심이 물이 아닙니다 — 원장 좌표가 마른 땅입니다.");
            }
            else
            {
                var q = new System.Collections.Generic.Queue<Vector2Int>();
                q.Enqueue(new Vector2Int(si, sj));
                seen[si, sj] = true;
                var d4 = new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
                while (q.Count > 0)
                {
                    var c = q.Dequeue();
                    cells++;
                    float wx = -half + c.x * step, wz = -half + c.y * step;
                    float r = new Vector2(wx, wz).magnitude;
                    if (r > farthest) farthest = r;
                    if (r > WorldTerrain.CoastEnd) reachesSea = true;
                    foreach (var d in d4)
                    {
                        int ni = c.x + d.x, nj = c.y + d.y;
                        if (ni < 0 || nj < 0 || ni >= n || nj >= n) continue;
                        if (seen[ni, nj] || !water[ni, nj]) continue;
                        seen[ni, nj] = true;
                        q.Enqueue(new Vector2Int(ni, nj));
                    }
                }
            }
            Debug.Log("[호수] ② 물 덩어리 — 호수 중심에서 이어지는 수면 아래 칸 " + cells + "개(" +
                      (cells * step * step).ToString("0") + "㎡) · 중심에서 가장 먼 물 " + farthest.ToString("0") +
                      "m · 해안(" + WorldTerrain.CoastEnd + "m) 밖까지 " + (reachesSea ? "**이어짐 — 만이다**" : "안 이어짐 — 닫힌 호수다"));

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
