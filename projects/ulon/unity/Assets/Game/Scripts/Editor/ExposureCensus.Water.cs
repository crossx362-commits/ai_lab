using UnityEngine;
using Ulon.Shared;

namespace Ulon.Editor
{
    public static partial class ExposureCensus
    {
        /// <summary>
        /// **수면의 흰 덩어리 — 원인에 이름을 붙이기 전에 후보를 나란히 재서 가른다**
        /// (검수 관찰 ⓐ, 2026-09-10: 호수 봉합 뒤 `15`의 흰 포화가 0.32% → 1.60%로 다섯 배).
        ///
        /// 화면에서 **물에 구멍이 뚫린 것처럼** 보인다. 후보는 넷이고 서로 배타적이지 않다:
        ///   ⓐ 수면 재질의 **매끄러움**(`_Glossiness` 0.85) — 매끄러울수록 정반사가 좁고 세다
        ///   ⓑ 수면 재질의 **금속기**(`_Metallic` 0.1)
        ///   ⓒ **태양** — 정반사는 광원이 있어야 난다
        ///   ⓓ 수면 자체 — 물을 지우면 남는 포화가 기저다(잔디·모래가 타는 몫)
        /// `07` 과노출에서 쓴 방식 그대로다: **같은 카메라에서 조건만 바꿔 나란히 렌더**한다.
        ///
        /// 합격선은 「**물이 물로 보이고 반짝임이 하이라이트로 읽히나**」이므로 스윕도 같이 찍는다 —
        /// **0으로 만드는 것은 답이 아니다**(물이 죽는다). 판정은 눈이 한다.
        ///
        /// **이 자가 못 보는 것**: 반짝임의 **자리와 모양**이다. 몫이 같아도 한 덩어리로 뭉친 것과
        /// 잔물결처럼 흩어진 것은 화면에서 전혀 다르다 — 그래서 가장 큰 덩어리 크기도 함께 찍는다.
        /// </summary>
        public static void RunWaterGlare()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);

            foreach (string shot in new[] { "15_lake_river", "35_fishing", "14_world_vista" })
            {
                var shots = QaShots.BuildShots();
                int idx = -1;
                for (int i = 0; i < shots.Length; i++)
                    if (QaShots.NameOf(shots[i]) == shot) { idx = i; break; }
                if (idx < 0) { Debug.LogWarning("[물] 샷을 못 찾음: " + shot); continue; }
                QaShots.EyeOf(shots[idx], out Vector3 eye, out Vector3 look);

                var camGo = new GameObject("WaterGlareCam");
                var cam = camGo.AddComponent<Camera>();
                cam.fieldOfView = 55f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 500f;
                var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
                var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
                cam.targetTexture = rt;
                camGo.transform.position = eye;
                camGo.transform.LookAt(look);

                var water = GameObject.Find(VisualSliceBuilder.WaterObject);
                var rend = water != null ? water.GetComponent<Renderer>() : null;
                var mat = rend != null ? rend.sharedMaterial : null;
                if (mat == null)
                {
                    Debug.LogWarning("[물] 수면 재질을 못 찾음 — " + VisualSliceBuilder.WaterObject);
                    Object.DestroyImmediate(camGo); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
                    continue;
                }
                float gloss0 = mat.GetFloat("_Glossiness"), metal0 = mat.GetFloat("_Metallic");
                var sun = SunLight();
                float sun0 = sun != null ? sun.intensity : 0f;

                string line = shot + " —";
                line += " A 지금 " + Blob(cam, rt, tex);
                mat.SetFloat("_Glossiness", 0f);
                line += " · B 매끄러움0 " + Blob(cam, rt, tex);
                mat.SetFloat("_Glossiness", gloss0);
                mat.SetFloat("_Metallic", 0f);
                line += " · C 금속기0 " + Blob(cam, rt, tex);
                mat.SetFloat("_Metallic", metal0);
                if (sun != null) sun.intensity = 0f;
                line += " · D 태양끔 " + Blob(cam, rt, tex);
                if (sun != null) sun.intensity = sun0;
                if (rend != null) rend.enabled = false;
                line += " · E 물없음 " + Blob(cam, rt, tex);   // 기저 아님 — 물을 지우면 **하늘**이 드러난다
                if (rend != null) rend.enabled = true;
                Debug.Log("[물] " + line);

                if (shot == "15_lake_river")
                {
                    string sweep = "";
                    foreach (float g in new[] { 0.85f, 0.7f, 0.55f, 0.4f, 0.25f, 0f })
                    {
                        mat.SetFloat("_Glossiness", g);
                        sweep += " · " + g.ToString("0.00") + " " + Blob(cam, rt, tex);
                    }
                    mat.SetFloat("_Glossiness", gloss0);
                    Debug.Log("[물] 매끄러움 스윕 —" + sweep);
                }

                cam.targetTexture = null; RenderTexture.active = null;
                Object.DestroyImmediate(camGo); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
            }
            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(0);
        }

        /// <summary>
        /// 게이트가 쓰는 값 — 그 샷의 **흰 포화 몫**(RGB 모두 250↑). `gloss`가 NaN이 아니면
        /// 수면 매끄러움을 그 값으로 바꿔 재고 되돌린다(NC용). 샷을 못 찾으면 −1.
        /// 셈과 **같은 렌더·같은 기준**을 쓴다 — 두 벌을 두면 자와 셈이 다른 세계를 본다.
        /// </summary>
        public static float WhiteShareWithGloss(string shotName, float gloss)
        {
            var shots = QaShots.BuildShots();
            int idx = -1;
            for (int i = 0; i < shots.Length; i++)
                if (QaShots.NameOf(shots[i]) == shotName) { idx = i; break; }
            if (idx < 0) return -1f;
            QaShots.EyeOf(shots[idx], out Vector3 eye, out Vector3 look);

            var camGo = new GameObject("WaterGateCam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 500f;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            cam.targetTexture = rt;
            camGo.transform.position = eye;
            camGo.transform.LookAt(look);

            var water = GameObject.Find(VisualSliceBuilder.WaterObject);
            var rend = water != null ? water.GetComponent<Renderer>() : null;
            var mat = rend != null ? rend.sharedMaterial : null;
            float keep = mat != null ? mat.GetFloat("_Glossiness") : 0f;
            if (mat != null && !float.IsNaN(gloss)) mat.SetFloat("_Glossiness", gloss);
            try
            {
                string m = Blob(cam, rt, tex);
                return float.Parse(m.Split('%')[0], System.Globalization.CultureInfo.InvariantCulture) * 0.01f;
            }
            finally
            {
                if (mat != null && !float.IsNaN(gloss)) mat.SetFloat("_Glossiness", keep);
                cam.targetTexture = null; RenderTexture.active = null;
                Object.DestroyImmediate(camGo); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
            }
        }

        /// <summary>
        /// 게이트가 쓰는 값 — 15 화면에서 **호수 수면 ÷ 바다 수면 밝기 비**. `gloss`가 NaN이 아니면
        /// 그 매끄러움으로 재고 되돌린다(NC용). 셈과 **같은 렌더·같은 분류**를 쓴다.
        /// </summary>
        public static float LakeSeaLumaRatio(float gloss)
        {
            var shots = QaShots.BuildShots();
            int idx = -1;
            for (int i = 0; i < shots.Length; i++)
                if (QaShots.NameOf(shots[i]) == "15_lake_river") { idx = i; break; }
            if (idx < 0) return -1f;
            QaShots.EyeOf(shots[idx], out Vector3 eye, out Vector3 look);

            var camGo = new GameObject("WaterToneGateCam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 500f;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            cam.targetTexture = rt;
            camGo.transform.position = eye;
            camGo.transform.LookAt(look);

            var water = GameObject.Find(VisualSliceBuilder.WaterObject);
            var rend = water != null ? water.GetComponent<Renderer>() : null;
            var mat = rend != null ? rend.sharedMaterial : null;
            float keep = mat != null ? mat.GetFloat("_Glossiness") : 0f;
            if (mat != null && !float.IsNaN(gloss)) mat.SetFloat("_Glossiness", gloss);
            try
            {
                Tone(cam, rt, tex, out Vector3 lake, out Vector3 sea, out _, out int nl, out int ns, out _);
                if (nl < 50 || ns < 50) return -1f;
                return Lum(lake) / Mathf.Max(1f, Lum(sea));
            }
            finally
            {
                if (mat != null && !float.IsNaN(gloss)) mat.SetFloat("_Glossiness", keep);
                cam.targetTexture = null; RenderTexture.active = null;
                Object.DestroyImmediate(camGo); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
            }
        }

        /// <summary>
        /// **한 화면에서 같은 물이 두 물감인가**(검수 판정 2026-09-10).
        ///
        /// 흰 포화는 닫혔는데 남은 절반이 결함이었다: 호수 아래쪽 (188,244,247) 대 바다 (83,148,180) —
        /// **같은 재질인데 호수가 바다보다 두 배 밝다.** 출구 목은 바다에 가까우니 재질이 아니라
        /// **자리(정반사 로브)** 탓이고, 화면에서 호수만 **우유빛 얕은 웅덩이**로 읽힌다.
        /// 포화가 0이어도 「한 화면에서 같은 물이 두 물감」이면 상식이 깨진다.
        ///
        /// 그래서 눈금을 바꾼다. 포화 몫이 아니라 **호수 수면색과 바다 수면색의 차**(밝기·채도)다.
        /// 픽셀을 가르는 법: 화면 격자마다 카메라 광선을 쏴 **수면 평면**과 만나는 점을 구하고,
        /// 지형에 먼저 막히면 버린 뒤, 그 점이 호수 반경 안이면 호수·해안 밖이면 바다로 센다.
        /// (같은 화면에서 재야 한다 — 따로 찍은 두 장은 노출·시각이 달라 비교가 안 된다.)
        ///
        /// **이 자가 못 보는 것**: 물결 무늬가 살아 있는지는 못 본다. 차가 0이어도 물이 죽은 판일 수
        /// 있으므로 **합격선은 눈이다** — 「호수와 바다가 같은 물로 보이나」.
        /// </summary>
        public static void RunWaterTone()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);

            var shots = QaShots.BuildShots();
            int idx = -1;
            for (int i = 0; i < shots.Length; i++)
                if (QaShots.NameOf(shots[i]) == "15_lake_river") { idx = i; break; }
            if (idx < 0) { Debug.LogError("[물빛] 15_lake_river를 못 찾았습니다."); return; }
            QaShots.EyeOf(shots[idx], out Vector3 eye, out Vector3 look);

            var camGo = new GameObject("WaterToneCam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 500f;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            cam.targetTexture = rt;
            camGo.transform.position = eye;
            camGo.transform.LookAt(look);

            var water = GameObject.Find(VisualSliceBuilder.WaterObject);
            var rend = water != null ? water.GetComponent<Renderer>() : null;
            var mat = rend != null ? rend.sharedMaterial : null;
            if (mat == null) { Debug.LogError("[물빛] 수면 재질을 못 찾았습니다."); return; }
            float gloss0 = mat.GetFloat("_Glossiness");

            foreach (float g in new[] { gloss0, 0.55f, 0.40f, 0.25f, 0f })
            {
                mat.SetFloat("_Glossiness", g);
                Tone(cam, rt, tex, out Vector3 lake, out Vector3 sea, out Vector3 neck,
                     out int nl, out int ns, out int nn);
                if (nl < 50 || ns < 50)
                {
                    Debug.LogWarning("[물빛] 표본이 호수 " + nl + " · 바다 " + ns + "픽셀뿐입니다 — 못 잽니다.");
                    break;
                }
                Debug.Log("[물빛] 매끄러움 " + g.ToString("0.00") +
                          " — 호수 " + Str(lake) + "(" + nl + "px) · 바다 " + Str(sea) + "(" + ns + "px) · 목 " +
                          Str(neck) + "(" + nn + "px) · **밝기 비 " + (Lum(lake) / Mathf.Max(1f, Lum(sea))).ToString("0.00") +
                          " · 채도 차 " + (Sat(lake) - Sat(sea)).ToString("0.00") + "**");
            }
            mat.SetFloat("_Glossiness", gloss0);

            cam.targetTexture = null; RenderTexture.active = null;
            Object.DestroyImmediate(camGo); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(0);
        }

        static string Str(Vector3 c) =>
            "(" + c.x.ToString("0") + "," + c.y.ToString("0") + "," + c.z.ToString("0") + ")";
        static float Lum(Vector3 c) => (c.x + c.y + c.z) / 3f;
        static float Sat(Vector3 c)
        {
            float mx = Mathf.Max(c.x, Mathf.Max(c.y, c.z)), mn = Mathf.Min(c.x, Mathf.Min(c.y, c.z));
            return mx < 1f ? 0f : (mx - mn) / mx;
        }

        /// <summary>화면 픽셀을 호수·바다·목으로 갈라 평균색을 낸다 — 광선을 수면 평면에 쏜다.</summary>
        static void Tone(Camera cam, RenderTexture rt, Texture2D tex,
                         out Vector3 lake, out Vector3 sea, out Vector3 neck,
                         out int nl, out int ns, out int nn)
        {
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            var px = tex.GetPixels32();

            Vector3 sl = Vector3.zero, ss = Vector3.zero, sn = Vector3.zero;
            nl = ns = nn = 0;
            float y = WorldTerrain.SeaLevel;
            for (int j = 0; j < H; j += 2)
                for (int i = 0; i < W; i += 2)
                {
                    var ray = cam.ScreenPointToRay(new Vector3(i, j, 0f));
                    if (ray.direction.y >= -0.001f) continue;                 // 수면을 향해 내려가는 광선만
                    float t = (y - ray.origin.y) / ray.direction.y;
                    if (t <= 0f) continue;
                    var p = ray.origin + ray.direction * t;
                    // 지형에 먼저 막히면 그 픽셀은 물이 아니다(뭍·언덕이다).
                    if (Physics.Raycast(ray.origin, ray.direction, out RaycastHit hit, t - 0.5f) &&
                        hit.distance < t - 0.5f) continue;
                    var c = px[j * W + i];
                    var v = new Vector3(c.r, c.g, c.b);
                    float dLake = new Vector2(p.x - WorldTerrain.LakeX, p.z - WorldTerrain.LakeZ).magnitude;
                    float dOrigin = new Vector2(p.x, p.z).magnitude;
                    if (dLake < WorldTerrain.LakeRadius - 3f) { sl += v; nl++; }
                    else if (dOrigin > WorldTerrain.CoastEnd + 6f) { ss += v; ns++; }
                    else if (dLake < WorldTerrain.LakeRadius + 25f) { sn += v; nn++; }
                }
            lake = nl > 0 ? sl / nl : Vector3.zero;
            sea = ns > 0 ? ss / ns : Vector3.zero;
            neck = nn > 0 ? sn / nn : Vector3.zero;
        }

        /// <summary>가장 센 방향광 — 태양이다(이름에 기대지 않는다: 씬이 바뀌면 이름은 어긋난다).</summary>
        static Light SunLight()
        {
            Light best = null;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (l != null && l.type == LightType.Directional && (best == null || l.intensity > best.intensity))
                    best = l;
            return best;
        }

        /// <summary>포화 몫 + **가장 큰 흰 덩어리**의 픽셀 수 — 몫이 같아도 뭉친 것과 흩어진 것은 다르다.</summary>
        static string Blob(Camera cam, RenderTexture rt, Texture2D tex)
        {
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            var px = tex.GetPixels32();
            var hot = new bool[px.Length];
            int count = 0;
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                // 검수가 쓴 기준과 **같은 자**로 잰다 — RGB가 **모두** 250↑인 흰 픽셀.
                // (max≥250은 파란 물에서도 B 하나로 걸려 값이 두 배가 된다: 첫 판에 3.38 대 1.60이었다.)
                hot[i] = c.r >= 250 && c.g >= 250 && c.b >= 250;
                if (hot[i]) count++;
            }
            int biggest = 0;
            var seen = new bool[px.Length];
            var q = new System.Collections.Generic.Queue<int>();
            for (int s = 0; s < px.Length; s++)
            {
                if (!hot[s] || seen[s]) continue;
                q.Enqueue(s); seen[s] = true;
                int size = 0;
                while (q.Count > 0)
                {
                    int c = q.Dequeue();
                    size++;
                    int cx = c % W, cy = c / W;
                    if (cx > 0) Push(hot, seen, q, c - 1);
                    if (cx < W - 1) Push(hot, seen, q, c + 1);
                    if (cy > 0) Push(hot, seen, q, c - W);
                    if (cy < H - 1) Push(hot, seen, q, c + W);
                }
                if (size > biggest) biggest = size;
            }
            return (100f * count / px.Length).ToString("0.00") + "%(덩어리 " + biggest + "px)";
        }

        static void Push(bool[] hot, bool[] seen, System.Collections.Generic.Queue<int> q, int i)
        {
            if (hot[i] && !seen[i]) { seen[i] = true; q.Enqueue(i); }
        }
    }
}
