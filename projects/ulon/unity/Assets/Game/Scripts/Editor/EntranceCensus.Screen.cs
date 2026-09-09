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

        /// <summary>
        /// **배너가 화면에서 무엇을 가리고, 무엇에 걸린 것으로 보이나**를 센다(고치지 않는다 — 세기만).
        /// 배치로 `RunBannerScreen`을 부른다. 첫 줄에 **무엇을 셌는지 이름과 픽셀 수**를 찍는다 —
        /// 이름 없는 셈은 엉뚱한 것을 세고도 초록불을 낸다(왕관 사고 2026-09-09).
        /// </summary>
        public static void RunBannerScreen()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            Report("07/D1", Dungeon1.RootObject, Dungeon1.EntranceX, Dungeon1.EntranceZ);
            Report("09/D2", Dungeon2.RootObject, Dungeon2.EntranceX, Dungeon2.EntranceZ);
            Report("11/D3", Dungeon3.RootObject, Dungeon3.EntranceX, Dungeon3.EntranceZ);
            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(0);
        }

        /// <summary>입구 한 곳을 화면에서 읽은 결과 — 자가 늘어나 이름을 붙였다(인자 여섯은 못 읽는다).</summary>
        public struct Readout
        {
            public float BannerMouth;     // 배너가 보이는 문구멍을 덮은 몫(배너 한 장씩 재서 **최악**)
            public float BannerLantern;   // 배너와 등불의 상호 겹침
            public float BannerPillar;    // 배너가 기둥과 겹친 몫 — **0이면 아무 데도 안 걸렸다**(가장 낮은 배너)
            public float PillarShow;      // 기둥 픽셀 중 배너 밖으로 보이는 몫 — 낮으면 배너가 걸린 것을 삼켰다
            public float LanternMouth;    // 등불이 보이는 문구멍을 덮은 몫
            public float LanternShow;     // 등불 픽셀 중 **가려지지 않고 보이는** 몫 — 기둥 뒤로 물리면 머리만 남는다
            public string What;           // 무엇을 셌는지(이름과 픽셀 수)
        }

        /// <summary>
        /// **「붙어 보이나」는 거리가 아니라 두 조건이다**(검수 지시 2026-09-09):
        /// ⓐ배너와 기둥이 **겹칠 것** ⓑ그 기둥이 배너 **밖으로 삐져나올 것**.
        /// 거리 0px은 「맞닿았다」이지 「걸려 보인다」가 아니었다 — 화면에서 배너는 여전히 공중에 뜬 천이었다.
        /// ⓑ가 없으면 「배너 뒤에 완전히 숨은 기둥」이 통과한다.
        /// </summary>
        public static bool ReadEntrance(string rootName, float ex, float ez, out Readout r)
        {
            r = new Readout();
            var root = GameObject.Find(rootName);
            if (root == null) { r.What = "(입구 없음: " + rootName + ")"; return false; }
            ShotEye(ex, ez, out Vector3 eye, out Vector3 look);
            var pillars = FindChildren(root.transform, "EntrancePillar");
            var banners = NearPillars(FindChildren(root.transform, "banner"), pillars);
            var lanterns = NearPillars(FindChildren(root.transform, "lantern"), pillars);
            var portal = FindChild(root.transform, VisualSliceBuilder.EntrancePortalObject);
            var frame = FindChild(root.transform, VisualSliceBuilder.EntranceFrameObject);
            if (banners.Count == 0 || portal == null || pillars.Count == 0)
            {
                r.What = "(배너 " + banners.Count + " · 포털 " + (portal == null ? "없음" : "있음") +
                         " · 기둥 " + pillars.Count + " — 셀 수 없다)";
                return false;
            }
            // **한 장씩 잰다 — 합치면 한 장만 걸려 있어도 통과한다**(2026-09-09 화면에서 걸렸다:
            // 왼쪽 배너는 기둥에 걸렸는데 오른쪽 배너는 허공에 떴고, 합친 자는 「38% 겹침」이라 했다).
            // 「최악」을 쓰는 이유: 화면을 망치는 것은 평균이 아니라 가장 나쁜 한 장이다.
            var lantern = Union(lanterns, eye, look);
            var pillar = Union(pillars, eye, look);
            var mouth = VisibleMouth(portal, frame, eye, look);
            var banner = Union(banners, eye, look);
            var blockers = Union(new List<Transform>(pillars) { }, eye, look);
            foreach (var b in banners)
            {
                var one = Draw(b, eye, look);
                for (int i = 0; i < blockers.Depth.Length; i++)
                    if (one.Depth[i] < blockers.Depth[i]) blockers.Depth[i] = one.Depth[i];
            }

            r.BannerPillar = 1f;
            r.PillarShow = 1f;
            r.LanternShow = 1f;
            int worstBannerPx = 0;
            foreach (var b in banners)
            {
                var one = Draw(b, eye, look);
                if (one.Pixels == 0) continue;
                r.BannerMouth = Mathf.Max(r.BannerMouth, CoverShare(one, mouth));
                r.BannerLantern = Mathf.Max(r.BannerLantern, OverlapShare(one, lantern));
                r.BannerPillar = Mathf.Min(r.BannerPillar, OverlapShare(one, pillar));
                worstBannerPx = Mathf.Max(worstBannerPx, one.Pixels);
            }
            foreach (var l in lanterns)
            {
                var one = Draw(l, eye, look);
                if (one.Pixels == 0) continue;
                r.LanternMouth = Mathf.Max(r.LanternMouth, CoverShare(one, mouth));
                // **등불이 스스로 보이나** — 기둥 뒤로 물리면 머리만 삐죽 남는다(화면에서 그렇게 됐다).
                // 기둥과 배너를 **합쳐서 한 번만** 뺀다. 따로 빼면 둘 다 가린 픽셀이 두 번 깎여
                // 「보임 −43%」 같은 값이 나온다(실제로 나왔다 — 음수가 나오면 자를 의심하라).
                r.LanternShow = Mathf.Min(r.LanternShow, 1f - CoverShare(blockers, one));
            }
            r.PillarShow = pillar.Pixels == 0 ? 0f : 1f - CoverShare(banner, pillar);
            r.What = "배너 " + banners.Count + "개(최대 " + worstBannerPx + "px) · 등불 " + lanterns.Count + "개 " +
                     lantern.Pixels + "px · 기둥 " + pillar.Pixels + "px · 보이는 구멍 " + mouth.Pixels + "px";
            return true;
        }

        static void Report(string tag, string rootName, float ex, float ez)
        {
            if (!ReadEntrance(rootName, ex, ez, out Readout r)) { Debug.Log("[입구/화면] " + tag + " " + r.What); return; }
            Debug.Log("[입구/화면] " + tag + " · " + r.What +
                      " · 문 " + (r.BannerMouth * 100f).ToString("0") + "% · 등불겹침 " +
                      (r.BannerLantern * 100f).ToString("0") + "% · 기둥겹침 " + (r.BannerPillar * 100f).ToString("0") +
                      "% · 기둥노출 " + (r.PillarShow * 100f).ToString("0") + "% · 등불이문 " +
                      (r.LanternMouth * 100f).ToString("0") + "% · 등불보임 " + (r.LanternShow * 100f).ToString("0") + "%");
        }

        /// <summary>
        /// **화면에서 실제로 보이는 문구멍**과 그 앞을 덮은 소품의 몫 — 문틀·포털을 뺀 **나머지 전부**가
        /// 후보다(배너만 세면 다른 소품이 새로 들어와도 조용하다).
        /// 광선으로 재던 자(`MouthBlockShare`)와 다투었고 화면이 이겼다: 그 자의 문틀 사각형 가장자리는
        /// 화면에서 **기둥 뒤**여서, 거기 선 배너를 「25% 가림」이라 불렀다(샷에는 가려진 것이 없었다).
        /// </summary>
        public static bool MouthScreenShare(string rootName, float ex, float ez,
                                            out float covered, out string who, out int mouthPixels)
        {
            covered = 0f; who = ""; mouthPixels = 0;
            var root = GameObject.Find(rootName);
            if (root == null) { who = "(입구 없음)"; return false; }
            ShotEye(ex, ez, out Vector3 eye, out Vector3 look);
            var portal = FindChild(root.transform, VisualSliceBuilder.EntrancePortalObject);
            var frame = FindChild(root.transform, VisualSliceBuilder.EntranceFrameObject);
            if (portal == null) { who = "(포털 없음)"; return false; }
            var mouth = VisibleMouth(portal, frame, eye, look);
            mouthPixels = mouth.Pixels;
            if (mouth.Pixels == 0) { who = "(보이는 문구멍이 없다 — 문틀이 다 가린 각이다)"; return false; }
            float worst = 0f;
            foreach (Transform child in root.transform)
            {
                if (child == frame || child == portal) continue;
                var sil = Draw(child, eye, look);
                if (sil.Pixels == 0) continue;
                float share = CoverShare(sil, mouth);
                if (share <= 0.005f) continue;
                covered += share;
                if (share > worst) { worst = share; who = child.name + " " + (share * 100f).ToString("0") + "%"; }
            }
            covered = Mathf.Clamp01(covered);
            if (who == "") who = "없음";
            return true;
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
        /// **자리를 고르기 전에 후보를 전부 재 본다**(고르는 루프 — 고른 결과를 상수로 박지 말고
        /// **규칙**으로 옮겨 적어라: 거리·면·좌우·등불은 **같이 골라진 한 칸**이다).
        /// 씬의 물건을 잠깐 옮겨 재고 **반드시 제자리로 돌린다** — 이 셈은 아무것도 안 고친다.
        /// 놓는 식은 빌더와 **같은 함수**(`VisualSliceBuilder.BannerPose`·`LanternPose`)를 쓴다.
        /// </summary>
        public static void RunBannerProbe()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            Probe("07/D1", Dungeon1.RootObject, Dungeon1.EntranceX, Dungeon1.EntranceZ);
            Probe("09/D2", Dungeon2.RootObject, Dungeon2.EntranceX, Dungeon2.EntranceZ);
            Probe("11/D3", Dungeon3.RootObject, Dungeon3.EntranceX, Dungeon3.EntranceZ);
            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(0);
        }

        static void Probe(string tag, string rootName, float ex, float ez)
        {
            var root = GameObject.Find(rootName);
            if (root == null) { Debug.Log("[배너탐색] " + tag + " 입구 없음"); return; }
            var pillars = FindChildren(root.transform, "EntrancePillar");
            var banners = NearPillars(FindChildren(root.transform, "banner"), pillars);
            var lanterns = NearPillars(FindChildren(root.transform, "lantern"), pillars);
            var portal = FindChild(root.transform, VisualSliceBuilder.EntrancePortalObject);
            if (pillars.Count < 2 || banners.Count == 0 || portal == null)
            {
                Debug.Log("[배너탐색] " + tag + " 못 잼(기둥 " + pillars.Count + " 배너 " + banners.Count + ")");
                return;
            }
            var p1 = pillars[0].position; var p2 = pillars[1].position;
            var side = (p2 - p1); side.y = 0f; side = side.normalized;
            var center = (p1 + p2) * 0.5f;
            var toPortal = portal.position - center; toPortal.y = 0f;
            var inward = toPortal.sqrMagnitude > 0.01f ? toPortal.normalized : Vector3.forward;
            float approachYaw = Mathf.Atan2(inward.x, inward.z) * Mathf.Rad2Deg;

            var home = new List<(Transform t, Vector3 pos, Quaternion rot)>();
            foreach (var b in banners) home.Add((b, b.position, b.rotation));
            foreach (var l in lanterns) home.Add((l, l.position, l.rotation));

            // **셋을 함께 고른다** — 배너만 움직이면 등불이 사이에 끼고, 등불만 움직이면 배너가 허공에 뜬다.
            float[] pushes = { 0.4f, 0.8f, 1.2f };
            float[] faces = { 180f };
            float[] lamps = { 0.6f, 0f, -1.2f };
            float[] laterals = { 0f, -0.8f, -1.17f };   // -1.17 = 기둥 중심(기둥 반폭만큼 당김)
            foreach (float push in pushes)
                foreach (float face in faces)
                    foreach (float lamp in lamps)
                        foreach (float lat in laterals)
                        {
                            // **기둥 배정은 「지금 어디 있나」가 아니라 빌더처럼 부호로 한다.**
                            // 그 시점 위치로 가까운 기둥을 고르면, 앞 판이 옮겨 놓은 자리에 따라
                            // 좌우가 뒤집혀 **프로브가 고른 칸과 빌드 결과가 어긋난다**(실제로 던전 2의
                            // 등불 겹침이 7%로 예측됐는데 빌드에서는 20%였다).
                            for (int bi = 0; bi < banners.Count; bi++)
                            {
                                var b = banners[bi];
                                int sideSign = bi % 2 == 0 ? -1 : 1;
                                var pil = center + VisualSliceBuilder.EntranceSide(approachYaw) *
                                          (VisualSliceBuilder.EntranceDoorHalf * sideSign);
                                VisualSliceBuilder.BannerPose(pil, approachYaw, sideSign, push, lat,
                                                              out Vector3 bp, out float byaw);
                                b.position = bp;
                                b.rotation = Quaternion.Euler(0f, byaw - VisualSliceBuilder.BannerFace + face, 0f);
                            }
                            for (int li = 0; li < lanterns.Count; li++)
                            {
                                int sideSign = li % 2 == 0 ? -1 : 1;
                                var pil = center + VisualSliceBuilder.EntranceSide(approachYaw) *
                                          (VisualSliceBuilder.EntranceDoorHalf * sideSign);
                                lanterns[li].position = VisualSliceBuilder.LanternPose(pil, approachYaw, sideSign, lamp);
                            }
                            Physics.SyncTransforms();
                            if (!ReadEntrance(rootName, ex, ez, out Readout r)) continue;
                            Debug.Log("[배너탐색] " + tag + " 배너 " + push.ToString("0.0") + "m/" + face.ToString("0") +
                                      "°/옆" + lat.ToString("0.0") + "m · 등불 " + lamp.ToString("0.0") +
                                      "m → 문 " + (r.BannerMouth * 100f).ToString("0") +
                                      "% · 등불겹침 " + (r.BannerLantern * 100f).ToString("0") +
                                      "% · 기둥겹침 " + (r.BannerPillar * 100f).ToString("0") +
                                      "% · 기둥노출 " + (r.PillarShow * 100f).ToString("0") +
                                      "% · 등불이문 " + (r.LanternMouth * 100f).ToString("0") +
                                      "% · 등불보임 " + (r.LanternShow * 100f).ToString("0") + "%");
                        }

            foreach (var h in home) { h.t.position = h.pos; h.t.rotation = h.rot; }
            Physics.SyncTransforms();
            Debug.Log("[배너탐색] " + tag + " 제자리로 되돌렸습니다 · approachYaw " + approachYaw.ToString("0.0") + "°");
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
