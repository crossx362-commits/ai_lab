using System.Collections.Generic;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **물과 뭍이 만나는 선이 톱니로 각지는가**를 세는 자(검수 지시 2026-09-10).
    ///
    /// 앞서 물가 각짐을 셌던 판(`be7893de`)은 **모래↔잔디 경계**를 쟀다 — 도포 경계다.
    /// **물↔뭍 경계는 아직 아무도 안 쟀다.** 이건 그 축이고, **셈만 한다**(원인 이름은 안 붙인다).
    ///
    /// 물 픽셀을 색으로 짐작하지 않는다 — **같은 카메라에서 수면 겹만 끄고 한 번 더 렌더해**
    /// 달라진 자리를 물로 센다(색 문턱은 세계가 바뀌면 죽는다).
    /// 경계는 세로줄마다 **맨 위 물 픽셀**(먼 물가)과 **맨 아래 물 픽셀**(앞 물가)로 뽑는다.
    ///
    /// 눈금 둘을 **나란히** 놓는다(검수 지시):
    /// ① 물가 선의 **톱니 주기와 진폭**
    /// ② 지형 셀 0.586m이 **그 거리에서 몇 px**인가(이웃 픽셀 두 광선의 착지점 거리로 잰다 —
    ///    비스듬한 바닥을 삼각함수로 유도하면 틀린다, `05376e3e`에서 배운 것).
    /// 둘이 같은 눈금이면 톱니는 메시 눈금이고, 아니면 다른 것이다.
    ///
    /// **자를 두 번 고쳤다**(먼저 적어 둔다):
    /// ① *평평한 계단 길이* — 「같은 높이로 이어지는 세로줄 수」는 **선의 기울기**가 정한다.
    ///    45°로 지나가는 선은 아무리 매끈해도 계단 길이가 1이다. 톱니를 안 보고 기울기를 봤다.
    ///    그래서 **31px 상자로 민 매끈한 선을 빼고 남는 나머지**(잔차)를 재고, 그 잔차의
    ///    자기상관에서 **주기**를, 제곱평균으로 **진폭**을 뽑는다 — 기울기와 무관해진다.
    /// ② *맨 위 물 픽셀 = 먼 물가* — `63`에서는 그것이 물가가 아니라 **수평선**이었다
    ///    (계단 175px, 광선이 평면을 못 맞아 m/px가 −1). 광선이 수면에 **400m 안에서** 닿는
    ///    세로줄만 물가로 센다.
    /// ③ *잔차의 진폭·주기* — 화면을 보고서야 알았다. 톱니는 촘촘한 잔결이 아니라 **드문 큰
    ///    직각 노치**다(`64` 오른쪽 강가에 서너 개, 하나가 수십 px). 31px 상자로 밀면 그 큰
    ///    노치가 매끈한 선 쪽에 들어가 **잔차에서 사라진다** — 진폭 1.03px에 주기 없음으로 나왔다.
    ///    **자를 눈에 맞춘다**: 이웃 기울기에서 벗어난 큰 뜀만 노치로 세고, 그 자리를 광선으로
    ///    세계 좌표에 내려 **지형 격자선과 얼마나 어긋나는지**를 잰다. 격자에 붙어 있으면 메시다.
    /// ④ *자리를 수면 평면에 내리기* — 먼 물가는 물 높이에서 만나니 맞지만, **앞을 가리는 둑**의
    ///    실루엣은 물이 아니라 **땅 위**에 있다. 그 선을 수면 평면에 내리면 자리가 통째로 어긋나
    ///    격자 대조가 헛돈다(그래서 첫 판이 「격자 무관」으로 보였다). 경계의 **뭍 쪽 픽셀을
    ///    지형에 직접 쏘아** 실제 닿는 자리를 쓴다.
    /// </summary>
    public static partial class OutdoorCensus
    {
        /// <summary>지형 하이트맵 셀 크기와 격자 원점 — 원장값을 적지 않고 **세계에서 읽는다**.</summary>
        static void TerrainGrid(out float cell, out float splat, out float ox, out float oz)
        {
            var ts = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            if (ts.Length != 1 || ts[0].terrainData == null)
                throw new System.InvalidOperationException("지형이 " + ts.Length +
                    "개입니다 — 격자 원점이 하나가 아니면 이 자는 헛돕니다.");
            var t = ts[0];
            var td = t.terrainData;
            cell = td.size.x / (td.heightmapResolution - 1);
            splat = td.size.x / td.alphamapResolution;      // 도포 칸(높이 셀과 눈금이 다르다)
            ox = t.transform.position.x;
            oz = t.transform.position.z;
            GridTerrain = t;
            Debug.Log("[물가톱니] 지형 — 크기 " + td.size.x + "m · 높이 " + td.heightmapResolution +
                      "(셀 " + cell.ToString("0.000") + "m) · 도포 " + td.alphamapResolution +
                      "(칸 " + splat.ToString("0.000") + "m) · 원점 (" + ox + ", " + oz + ")");
        }

        static Terrain GridTerrain;

        /// <summary>그 자리의 **물가 경사**(도). 노치가 완만한 데 나는지 가파른 데 나는지를 가른다 —
        /// 처방(전이 띠를 얕게 vs 둑을 가파르게)이 정반대로 갈리는 자리다.</summary>
        static float Steep(Vector3 p)
        {
            var td = GridTerrain.terrainData;
            var o = GridTerrain.transform.position;
            return td.GetSteepness(Mathf.Clamp01((p.x - o.x) / td.size.x),
                                   Mathf.Clamp01((p.z - o.z) / td.size.z));
        }

        static int LandHits, PlaneFallbacks;   // 지형에 실제로 닿았나 — 0이면 이 자는 또 눈이 먼 것이다

        /// <summary>경계의 **뭍 쪽 픽셀**을 세계에 내린다 — 지형에 실제로 닿는 자리를 쓰고,
        /// 못 닿으면 수면 평면으로 물러난다(먼 물가는 물 높이에서 만난다).</summary>
        static bool HitLand(Camera cam, int px, int py, float planeY, out Vector3 hit)
        {
            var ray = cam.ScreenPointToRay(new Vector3(px + 0.5f, py + 0.5f, 0f));
            RaycastHit rh;
            if (Physics.Raycast(ray, out rh, 4000f)) { hit = rh.point; LandHits++; return true; }
            PlaneFallbacks++;
            return HitPlane(cam, px, py, planeY, out hit);
        }

        /// <summary>세계 한 점이 눈금 <paramref name="g"/>의 격자선에서 얼마나 떨어졌나(칸 단위, 0~0.5).
        /// 두 축 중 가까운 쪽을 쓴다 — 격자 계단의 모서리는 한 축만 격자선 위여도 된다.
        /// **아무 상관 없는 점들의 중앙값은 0.146**이다(두 균등분포의 최솟값) — 그게 이 자의 0점이다.</summary>
        static float GridOff(Vector3 p, float g, float ox, float oz)
        {
            float fx = Mathf.Repeat(p.x - ox, g) / g;
            float fz = Mathf.Repeat(p.z - oz, g) / g;
            return Mathf.Min(Mathf.Min(fx, 1f - fx), Mathf.Min(fz, 1f - fz));
        }

        public static void RunShoreEdge()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            foreach (var sh in new[] { "64_river_bend", "63_pier_cutface", "15_lake_river" })
                MeasureShoreEdge(sh);
        }

        /// <summary>
        /// **판별 테스트 — 톱니가 셀을 따라가는가**(검수 지시 ⓑ, 2026-09-10).
        ///
        /// 격자 대조는 「노치가 격자선에 안 붙는다」고 말했지만 **그건 자가 한 말**이다.
        /// 굽는 쪽을 실제로 바꿔 세계에게 다시 묻는다: 하이트맵을 **절반(257)**으로 구우면
        /// 셀이 0.586 → 1.172m로 두 배가 된다. **메시가 범인이면 톱니 크기도 두 배**여야 한다.
        /// 개수만 보지 말고 **크기가 셀에 비례하는지**를 본다(검수 조건).
        ///
        /// 절반 판은 **되돌리는 것까지가 이 단계**다 — `finally`에서 원장값으로 다시 굽는다.
        /// </summary>
        public static void RunShoreEdgeHalfRes()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);

            Debug.Log("[물가톱니] ── 원장 해상도(513) ──");
            foreach (var sh in new[] { "64_river_bend", "63_pier_cutface", "15_lake_river" })
                MeasureShoreEdge(sh);

            int keep = VisualSliceBuilder.HeightResOverride;
            try
            {
                VisualSliceBuilder.HeightResOverride = 257;      // 셀 0.586 → 1.172m
                VisualSliceBuilder.EnsureVillageTerrain();
                UnityEditor.AssetDatabase.SaveAssets();
                Debug.Log("[물가톱니] ── 절반 해상도(257) ──");
                foreach (var sh in new[] { "64_river_bend", "63_pier_cutface", "15_lake_river" })
                    MeasureShoreEdge(sh);
            }
            finally
            {
                VisualSliceBuilder.HeightResOverride = keep;
                VisualSliceBuilder.EnsureVillageTerrain();       // 되돌리기
                UnityEditor.AssetDatabase.SaveAssets();
                Debug.Log("[물가톱니] 원장 해상도로 되돌렸다.");
            }
        }

        static void MeasureShoreEdge(string shotName)
        {
            const int W = 1280, H = 720;
            Vector3 eye = Vector3.zero, look = Vector3.zero;
            bool found = false;
            foreach (var s in QaShots.BuildShots())
                if (QaShots.NameOf(s) == shotName) { QaShots.EyeOf(s, out eye, out look); found = true; break; }
            if (!found)
                throw new System.InvalidOperationException("샷 " + shotName + "을 찾지 못했습니다 — 자가 헛돕니다.");

            var go = new GameObject("_shoreCam");
            var cam = go.AddComponent<Camera>();
            cam.transform.position = eye;
            cam.transform.LookAt(look);
            cam.farClipPlane = 4000f;

            var waterRenderers = new List<Renderer>();
            float waterY = 0f;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                var mm = r.sharedMaterial;
                if (mm != null && mm.name.StartsWith("SeaWater")) { waterRenderers.Add(r); waterY = r.bounds.max.y; }
            }
            if (waterRenderers.Count == 0)
                throw new System.InvalidOperationException("수면 겹을 찾지 못했습니다 — 자가 헛돕니다.");

            // **렌더텍스처를 끝까지 붙여 둔다.** 떼면 `ScreenPointToRay`가 배치모드 화면 크기를 쓰고,
            // 화면 좌표 → 세계 좌표가 통째로 어긋난다(첫 판이 그래서 헛돌았다 — 자에도 자가 필요하다).
            var rt = new RenderTexture(W, H, 24);
            cam.targetTexture = rt;
            if (cam.pixelWidth != W || cam.pixelHeight != H)
                throw new System.InvalidOperationException("카메라 픽셀 크기가 " + cam.pixelWidth + "×" +
                    cam.pixelHeight + "입니다 — 화면 좌표를 세계로 못 내립니다.");

            var on = Grab(cam, W, H);
            foreach (var r in waterRenderers) r.enabled = false;
            var off = Grab(cam, W, H);
            foreach (var r in waterRenderers) r.enabled = true;

            var pa = on.GetPixels32();
            var pb = off.GetPixels32();
            var water = new bool[W * H];
            int wet = 0;
            for (int i = 0; i < water.Length; i++)
            {
                int d = Mathf.Abs(pa[i].r - pb[i].r) + Mathf.Abs(pa[i].g - pb[i].g) + Mathf.Abs(pa[i].b - pb[i].b);
                if (d > 12) { water[i] = true; wet++; }
            }
            Object.DestroyImmediate(on);
            Object.DestroyImmediate(off);

            if (wet < 1000)
            {
                cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
                Debug.Log("[물가톱니] " + shotName + " — 물 픽셀 " + wet + "개. 이 샷에는 물이 거의 없다(건너뜀).");
                return;
            }

            // GetPixels32는 아래에서 위로 담긴다 — 화면 위쪽은 y가 큰 쪽이다.
            var far = new int[W];   // 세로줄마다 **맨 위** 물 픽셀(먼 물가)
            var near = new int[W];  // 세로줄마다 **맨 아래** 물 픽셀(앞 물가)
            for (int x = 0; x < W; x++)
            {
                far[x] = -1; near[x] = -1;
                for (int y = H - 1; y >= 0; y--)
                    if (water[y * W + x]) { far[x] = y; break; }
                for (int y = 0; y < H; y++)
                    if (water[y * W + x]) { near[x] = y; break; }
            }

            // 가로줄마다 맨 왼쪽·맨 오른쪽 물 픽셀 — 세로에 가까운 물가는 세로줄 훑기로 못 잡는다.
            var left = new int[H];
            var right = new int[H];
            for (int y = 0; y < H; y++)
            {
                left[y] = -1; right[y] = -1;
                for (int x = 0; x < W; x++) if (water[y * W + x]) { left[y] = x; break; }
                for (int x = W - 1; x >= 0; x--) if (water[y * W + x]) { right[y] = x; break; }
            }

            // 광선이 수면에 400m 안에서 닿는 줄만 물가로 인정한다(수평선을 뺀다).
            Reject(cam, far, W, waterY, false);
            Reject(cam, near, W, waterY, false);
            Reject(cam, left, H, waterY, true);
            Reject(cam, right, H, waterY, true);

            var pooled = new List<float>();
            var control = new List<float>();
            var pooledS = new List<float>();
            var controlS = new List<float>();
            var pooledSlope = new List<float>();
            var controlSlope = new List<float>();
            LandHits = 0; PlaneFallbacks = 0;
            string dFar = Teeth(cam, far, W, waterY, false, pooled, control, pooledS, controlS, pooledSlope, controlSlope, 0, 1);
            string dNear = Teeth(cam, near, W, waterY, false, pooled, control, pooledS, controlS, pooledSlope, controlSlope, 0, -1);
            string dLeft = Teeth(cam, left, H, waterY, true, pooled, control, pooledS, controlS, pooledSlope, controlSlope, -1, 0);
            string dRight = Teeth(cam, right, H, waterY, true, pooled, control, pooledS, controlS, pooledSlope, controlSlope, 1, 0);
            cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(go);

            pooled.Sort(); control.Sort(); pooledS.Sort(); controlS.Sort();
            string verdict = pooled.Count == 0 ? "노치 없음"
                : "노치 " + pooled.Count + "개 — 높이 격자 어긋남 **" + pooled[pooled.Count / 2].ToString("0.000") +
                  "**(물가 전체 " + control[control.Count / 2].ToString("0.000") + " · 무관하면 0.146) · " +
                  "도포 격자 어긋남 **" + pooledS[pooledS.Count / 2].ToString("0.000") +
                  "**(물가 전체 " + controlS[controlS.Count / 2].ToString("0.000") + ")";
            pooledSlope.Sort(); controlSlope.Sort();
            if (pooledSlope.Count > 0)
                verdict += "\n  물가 경사 — 노치 자리 **" + pooledSlope[pooledSlope.Count / 2].ToString("0.0") +
                           "°** · 물가 전체 **" + controlSlope[controlSlope.Count / 2].ToString("0.0") + "°**";
            verdict += "\n  자리 잡기 — 지형에 닿음 " + LandHits + "점 · 수면 평면으로 물러남 " + PlaneFallbacks + "점";

            Debug.Log("[물가톱니] " + shotName + " — 물 " + (wet * 100f / (W * H)).ToString("0.0") + "%\n" +
                      "  " + verdict + "\n" +
                      "  먼 물가 " + dFar + "\n  앞 물가 " + dNear + "\n  왼 물가 " + dLeft + "\n  오른 물가 " + dRight);
        }

        static void Reject(Camera cam, int[] line, int N, float planeY, bool isRow)
        {
            for (int i = 0; i < N; i++)
            {
                if (line[i] < 0) continue;
                Vector3 hit;
                int px = isRow ? line[i] : i, py = isRow ? i : line[i];
                if (!HitPlane(cam, px, py, planeY, out hit) ||
                    Vector3.Distance(hit, cam.transform.position) > 400f)
                    line[i] = -1;
            }
        }

        /// <summary>
        /// **큰 직각 노치**를 세고, 그 자리를 세계 좌표로 내려 지형 격자와 대조한다.
        /// 노치 = 이웃 기울기(앞뒤 8줄 평균)에서 <see cref="NotchPx"/>px 넘게 벗어난 뜀.
        /// 격자 어긋남은 셀 크기로 나눈 값이다 — 0에 가까우면 격자선 위, 0.25 언저리면 무관.
        /// </summary>
        const float NotchPx = 3f;

        static string Teeth(Camera cam, int[] line, int N, float planeY, bool isRow,
                            List<float> pooled, List<float> control,
                            List<float> pooledS, List<float> controlS,
                            List<float> pooledSlope, List<float> controlSlope,
                            int landDx, int landDy)
        {
            // 가장 긴 이어진 토막을 고른다 — 끊긴 데를 이으면 없는 톱니가 생긴다.
            int bs = -1, bl = 0, s = -1, l = 0;
            for (int x = 0; x <= N; x++)
            {
                bool ok = x < N && line[x] >= 0;
                if (ok) { if (s < 0) { s = x; l = 0; } l++; }
                else { if (s >= 0 && l > bl) { bl = l; bs = s; } s = -1; }
            }
            if (bl < 64) return "(이어진 물가가 " + bl + "줄뿐 — 못 잼)";

            float cell, splat, ox, oz;
            TerrainGrid(out cell, out splat, out ox, out oz);

            var jumps = new List<float>();
            var offs = new List<float>();
            var spacing = new List<float>();
            int lastNotch = -1;
            for (int i = 8; i < bl - 8; i++)
            {
                int px = isRow ? line[bs + i] : bs + i, py = isRow ? bs + i : line[bs + i];
                Vector3 hit;
                bool hasHit = HitLand(cam, px + landDx, py + landDy, planeY, out hit);
                float off = 0f;
                float offS = 0f;
                if (hasHit)
                {
                    off = GridOff(hit, cell, ox, oz);
                    offS = GridOff(hit, splat, ox, oz);
                    // **자의 네거티브 컨트롤** — 물가 전체가 우연히 격자 근처면 자가 헛돈다.
                    control.Add(off); controlS.Add(offS); controlSlope.Add(Steep(hit));
                }

                float slope = (line[bs + i + 8] - line[bs + i - 8]) / 16f;   // 이웃 기울기
                float step = line[bs + i + 1] - line[bs + i];
                float dev = Mathf.Abs(step - slope);
                if (dev < NotchPx) continue;
                jumps.Add(dev);
                if (lastNotch >= 0) spacing.Add(i - lastNotch);
                lastNotch = i;
                if (hasHit) { offs.Add(off); pooled.Add(off); pooledS.Add(offS); pooledSlope.Add(Steep(hit)); }
            }

            int ci = bs + bl / 2;
            int ccx = isRow ? line[ci] : ci, ccy = isRow ? ci : line[ci];
            Vector3 a, b;
            float mpp = (HitPlane(cam, ccx, ccy, planeY, out a) && HitPlane(cam, ccx + 1, ccy, planeY, out b))
                        ? Vector3.Distance(a, b) : -1f;
            float cellPx = mpp > 0f ? cell / mpp : -1f;

            string notch = "노치 없음";
            if (jumps.Count > 0)
            {
                jumps.Sort(); spacing.Sort(); offs.Sort();
                notch = "노치 " + jumps.Count + "개 · 크기 중앙값 " + jumps[jumps.Count / 2].ToString("0.0") +
                        "px(최대 " + jumps[jumps.Count - 1].ToString("0.0") + ") · 간격 중앙값 " +
                        (spacing.Count > 0 ? spacing[spacing.Count / 2].ToString("0") : "-") + "px · " +
                        "격자 어긋남 중앙값 " + (offs.Count > 0 ? offs[offs.Count / 2].ToString("0.000") : "-") +
                        "셀(0=격자선 위 · 0.25=무관)";
            }

            return "이어진 " + bl + "줄(" + (isRow ? "y " : "x ") + bs + "~" + (bs + bl - 1) + ") · " + notch +
                   " · 셀 " + cell.ToString("0.000") + "m = **" + cellPx.ToString("0.00") + "px**(" +
                   (mpp * 100f).ToString("0.0") + "cm/px)";
        }

        static bool HitPlane(Camera cam, int px, int py, float planeY, out Vector3 hit)
        {
            hit = Vector3.zero;
            var ray = cam.ScreenPointToRay(new Vector3(px + 0.5f, py + 0.5f, 0f));
            if (Mathf.Abs(ray.direction.y) < 1e-5f) return false;
            float t = (planeY - ray.origin.y) / ray.direction.y;
            if (t <= 0f) return false;
            hit = ray.origin + ray.direction * t;
            return true;
        }

        /// <summary>이미 붙어 있는 렌더텍스처로 찍는다 — 떼지 않는다(위 주석 참조).</summary>
        static Texture2D Grab(Camera cam, int W, int H)
        {
            cam.Render();
            RenderTexture.active = cam.targetTexture;
            var img = new Texture2D(W, H, TextureFormat.RGB24, false);
            img.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            img.Apply();
            RenderTexture.active = null;
            return img;
        }
    }
}
