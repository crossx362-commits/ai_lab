using System.Collections.Generic;
using UnityEngine;
using Ulon.Shared;

namespace Ulon.Editor
{
    public static partial class EntranceCensus
    {
        /// <summary>
        /// **화면 실루엣으로 겹침을 잰다**(검수 지시 2026-09-09 — 「뚫렸나·가렸나는 광선이 아니라
        /// 화면에서 묻는다」). 광선으로 재던 앞 랩의 「샘」 자는 바운드 뒤에 선 옆벽에 먼저 맞아
        /// **NC를 못 넘었다**. 화면에서는 그 문제가 없다 — 카메라가 보는 그대로를 세기 때문이다.
        ///
        /// 렌더 텍스처를 쓰지 않고 **메시 삼각형을 직접 래스터화**한다: 배치 렌더는 조명·그림자·안개가
        /// 섞여 「어느 물건의 픽셀인가」를 못 가르는데, 여기서 필요한 것은 색이 아니라 **누가 어디를
        /// 차지했나**다. 깊이도 같이 담아 **앞뒤를 가른다**(겹침만 세면 뒤에 선 것도 「가렸다」가 된다).
        ///
        /// 눈은 QA 샷과 같다(`QaShots.Orbit`: 8m·20°·fov 55·16:9). 해상도는 320×180 —
        /// 비율 지표라 충분하고, 삼각형이 몇 픽셀뿐인 물건은 애초에 화면에서 안 읽힌다.
        ///
        /// **킷 안에서는 배너를 매달 수 없었다**(2026-09-09, 배너를 걷은 이유 — 다시 시도하기 전에 읽어라):
        /// `banner-red`는 장대+가로대+천이 한 몸이고 **원점이 장대 밑**이라, 기둥 표면에 붙이면
        /// 천이 기둥 옆으로 뻗어 나간다. 기둥 표면·상단에서 유도해 붙여 봤더니 던전 1에서 배너가
        /// **문구멍을 15%** 가리고, 그러면서도 기둥과의 화면 겹침은 **0%**였다 — 즉 「기둥에 붙였는데
        /// 화면에서는 여전히 안 붙어 보인다」. 자리를 81칸 재고, 매달아 보고, 둘 다 안 됐다.
        /// 입구 표식은 아치·어두운 포털·등불·돌길로 이미 선다.
        /// </summary>
        public const int ScreenW = 320;
        public const int ScreenH = 180;
        const float ShotFov = 55f;
        const float ShotDist = 8f;
        const float ShotPitch = 20f;

        /// <summary>QA `Orbit(대상, 8m, 20°)`과 **같은 눈**. 재는 자가 여럿이라 한 자리에 둔다.</summary>
        public static void ShotEye(float ex, float ez, out Vector3 eye, out Vector3 look)
        {
            var hits = Physics.RaycastAll(new Vector3(ex, 500f, ez), Vector3.down, 1000f);
            float gy = float.MaxValue;
            for (int i = 0; i < hits.Length; i++) gy = Mathf.Min(gy, hits[i].point.y);
            if (gy == float.MaxValue) gy = 0f;
            look = new Vector3(ex, gy + 1.2f, ez);
            float rad = ShotPitch * Mathf.Deg2Rad;
            eye = look + new Vector3(-ShotDist * Mathf.Cos(rad), ShotDist * Mathf.Sin(rad) + 1.5f, -ShotDist * Mathf.Cos(rad)) * 0.7071f;
        }

        /// <summary>화면 한 장 분량의 깊이 그림 — 픽셀마다 「가장 앞선 것까지의 거리」(안 찍힌 곳은 ∞).</summary>
        public class Silhouette
        {
            public float[] Depth = new float[ScreenW * ScreenH];
            public int Pixels;
            public Silhouette() { for (int i = 0; i < Depth.Length; i++) Depth[i] = float.MaxValue; }
        }

        struct Cam
        {
            public Vector3 Eye, Right, Up, Fwd;
            public float TanX, TanY;
        }

        static Cam MakeCam(Vector3 eye, Vector3 look)
        {
            var c = new Cam();
            c.Eye = eye;
            c.Fwd = (look - eye).normalized;
            c.Right = Vector3.Cross(Vector3.up, c.Fwd).normalized;
            c.Up = Vector3.Cross(c.Fwd, c.Right);
            c.TanY = Mathf.Tan(ShotFov * 0.5f * Mathf.Deg2Rad);
            c.TanX = c.TanY * (float)ScreenW / ScreenH;
            return c;
        }

        /// <summary>월드 점 → 화면 좌표(픽셀)와 깊이. 카메라 뒤면 false.</summary>
        static bool Project(in Cam c, Vector3 p, out float sx, out float sy, out float depth)
        {
            var v = p - c.Eye;
            depth = Vector3.Dot(v, c.Fwd);
            sx = sy = 0f;
            if (depth < 0.05f) return false;
            float x = Vector3.Dot(v, c.Right) / (depth * c.TanX);
            float y = Vector3.Dot(v, c.Up) / (depth * c.TanY);
            sx = (x * 0.5f + 0.5f) * ScreenW;
            sy = (0.5f - y * 0.5f) * ScreenH;
            return true;
        }

        /// <summary>이 가지(children 포함)의 메시를 화면에 찍는다 — 꺼진 렌더러는 세지 않는다.</summary>
        public static Silhouette Draw(Transform root, Vector3 eye, Vector3 look)
        {
            var sil = new Silhouette();
            if (root == null) return sil;
            var cam = MakeCam(eye, look);
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var r = mf.GetComponent<Renderer>();
                // **꺼진 렌더러를 세는 자는 화면이 아니라 씬을 본다**(원장 2026-09-09).
                if (r == null || !r.enabled || !mf.gameObject.activeInHierarchy) continue;
                var mesh = mf.sharedMesh;
                if (mesh == null) continue;
                var verts = mesh.vertices;
                var m = mf.transform.localToWorldMatrix;
                var sxs = new float[verts.Length];
                var sys = new float[verts.Length];
                var dep = new float[verts.Length];
                var ok = new bool[verts.Length];
                for (int i = 0; i < verts.Length; i++)
                    ok[i] = Project(in cam, m.MultiplyPoint3x4(verts[i]), out sxs[i], out sys[i], out dep[i]);
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    var tris = mesh.GetTriangles(s);
                    for (int t = 0; t + 2 < tris.Length; t += 3)
                    {
                        int a = tris[t], b = tris[t + 1], c2 = tris[t + 2];
                        if (!ok[a] || !ok[b] || !ok[c2]) continue;
                        Raster(sil, sxs[a], sys[a], dep[a], sxs[b], sys[b], dep[b], sxs[c2], sys[c2], dep[c2]);
                    }
                }
            }
            for (int i = 0; i < sil.Depth.Length; i++) if (sil.Depth[i] < float.MaxValue) sil.Pixels++;
            return sil;
        }

        /// <summary>삼각형 하나를 깊이와 함께 채운다(가장 앞선 값만 남긴다).</summary>
        static void Raster(Silhouette sil, float x0, float y0, float d0, float x1, float y1, float d1, float x2, float y2, float d2)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(x0, Mathf.Min(x1, x2))));
            int maxX = Mathf.Min(ScreenW - 1, Mathf.CeilToInt(Mathf.Max(x0, Mathf.Max(x1, x2))));
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(y0, Mathf.Min(y1, y2))));
            int maxY = Mathf.Min(ScreenH - 1, Mathf.CeilToInt(Mathf.Max(y0, Mathf.Max(y1, y2))));
            float area = (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0);
            if (Mathf.Abs(area) < 1e-6f) return;
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float w0 = ((x1 - px) * (y2 - py) - (x2 - px) * (y1 - py)) / area;
                    float w1 = ((x2 - px) * (y0 - py) - (x0 - px) * (y2 - py)) / area;
                    float w2 = 1f - w0 - w1;
                    if (w0 < 0f || w1 < 0f || w2 < 0f) continue;
                    float d = w0 * d0 + w1 * d1 + w2 * d2;
                    int i = y * ScreenW + x;
                    if (d < sil.Depth[i]) sil.Depth[i] = d;
                }
        }

        /// <summary>
        /// `a`가 **`b`보다 앞서서** 가린 픽셀이 `b` 실루엣의 몇 할인가(0~1).
        /// 앞뒤를 안 가르면 뒤에 선 물건도 「가렸다」가 된다 — 화면에서는 그게 거짓말이다.
        /// </summary>
        public static float CoverShare(Silhouette a, Silhouette b)
        {
            if (b == null || b.Pixels == 0) return 0f;
            int hit = 0;
            for (int i = 0; i < a.Depth.Length; i++)
                if (b.Depth[i] < float.MaxValue && a.Depth[i] < b.Depth[i] - 0.02f) hit++;
            return (float)hit / b.Pixels;
        }


        /// <summary>
        /// 두 실루엣의 **상호 겹침**(앞뒤를 안 가린다) — 작은 쪽 기준. 「화면에서 겹쳐 보이나」는
        /// 누가 앞이냐와 무관하다: 뒤에 선 배너도 등불과 한 덩어리로 읽힌다(검수 관찰 2026-09-09).
        /// </summary>
        public static float OverlapShare(Silhouette a, Silhouette b)
        {
            if (a.Pixels == 0 || b.Pixels == 0) return 0f;
            int hit = 0;
            for (int i = 0; i < a.Depth.Length; i++)
                if (a.Depth[i] < float.MaxValue && b.Depth[i] < float.MaxValue) hit++;
            return (float)hit / Mathf.Min(a.Pixels, b.Pixels);
        }

        /// <summary>
        /// 두 실루엣 사이 **화면에서의 최소 거리**(픽셀). 0이면 붙어 보인다 —
        /// 「무엇에 걸린 천인가」는 겹침이 아니라 **닿아 있느냐**로 읽힌다. 떨어져 있으면 공중에 뜬다.
        /// (`Limit`을 넘는 거리는 그 값으로 끊는다 — 멀리 떨어진 것끼리의 정확한 거리는 뜻이 없다.)
        /// </summary>
        public static int ScreenGap(Silhouette a, Silhouette b, int limit = 40)
        {
            if (a.Pixels == 0 || b.Pixels == 0) return limit;
            int best = limit;
            for (int y = 0; y < ScreenH; y++)
                for (int x = 0; x < ScreenW; x++)
                {
                    if (a.Depth[y * ScreenW + x] == float.MaxValue) continue;
                    for (int dy = -best; dy <= best; dy++)
                    {
                        int yy = y + dy;
                        if (yy < 0 || yy >= ScreenH) continue;
                        for (int dx = -best; dx <= best; dx++)
                        {
                            int xx = x + dx;
                            if (xx < 0 || xx >= ScreenW) continue;
                            if (b.Depth[yy * ScreenW + xx] == float.MaxValue) continue;
                            int d = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                            if (d < best) best = d;
                            if (best == 0) return 0;
                        }
                    }
                }
            return best;
        }

        /// <summary>여러 가지를 한 장에 겹쳐 찍는다(기둥 둘처럼 「같은 것 여럿」을 하나로 볼 때).</summary>
        public static Silhouette Union(List<Transform> parts, Vector3 eye, Vector3 look)
        {
            var sil = new Silhouette();
            foreach (var p in parts)
            {
                var one = Draw(p, eye, look);
                for (int i = 0; i < sil.Depth.Length; i++)
                    if (one.Depth[i] < sil.Depth[i]) sil.Depth[i] = one.Depth[i];
            }
            sil.Pixels = 0;
            for (int i = 0; i < sil.Depth.Length; i++) if (sil.Depth[i] < float.MaxValue) sil.Pixels++;
            return sil;
        }

        /// <summary>포털 실루엣에서 **문틀보다 뒤인 픽셀을 걷어낸** 것 — 화면에서 실제로 보이는 구멍.</summary>
        public static Silhouette VisibleMouth(Transform portal, Transform frame, Vector3 eye, Vector3 look)
        {
            var mouth = Draw(portal, eye, look);
            if (frame != null)
            {
                var fs = Draw(frame, eye, look);
                for (int i = 0; i < mouth.Depth.Length; i++)
                    if (mouth.Depth[i] < float.MaxValue && fs.Depth[i] < mouth.Depth[i] - 0.02f)
                        mouth.Depth[i] = float.MaxValue;
                mouth.Pixels = 0;
                for (int i = 0; i < mouth.Depth.Length; i++) if (mouth.Depth[i] < float.MaxValue) mouth.Pixels++;
            }
            return mouth;
        }

        /// <summary>
        /// **입구 소품만 고른다** — 기둥에서 `Near`(4m) 안에 있는 것. 이름만으로 고르면 던전 3처럼
        /// 루트 아래에 **입구와 무관한 배너**가 하나 더 있는 곳에서 그 한 장 때문에 지표가 0%가 된다
        /// (자가 무력해지는 것이 아니라 **엉뚱한 것을 센다** — 왕관 사고와 같은 함정).
        /// </summary>
        public const float NearEntrance = 4f;

        public static List<Transform> NearPillars(List<Transform> parts, List<Transform> pillars)
        {
            var near = new List<Transform>();
            foreach (var p in parts)
                foreach (var q in pillars)
                    if ((p.position - q.position).sqrMagnitude <= NearEntrance * NearEntrance) { near.Add(p); break; }
            return near;
        }

        /// <summary>이 가지의 자식들 중 이름으로 찾은 첫 Transform(없으면 null).</summary>
        public static Transform FindChild(Transform root, string name)
        {
            if (root == null) return null;
            foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                if (tr.name == name) return tr;
            return null;
        }

        /// <summary>이 가지의 자식들 중 이름이 이것으로 시작하는 것 전부.</summary>
        public static List<Transform> FindChildren(Transform root, string prefix)
        {
            var list = new List<Transform>();
            if (root == null) return list;
            foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                if (tr.name.StartsWith(prefix)) list.Add(tr);
            return list;
        }
    }
}
