using UnityEngine;
using Ulon.Shared;

namespace Ulon.Editor
{
    /// <summary>
    /// **입구 근접 샷이 왜 타나 — 후보를 나란히 센다**(검수 큐 2, 2026-09-09).
    ///
    /// 검수 실측: `07` 250↑ 포화 29.2% · R·G 동시 21.7%, `11` 20.4/13.1, `09` 14.9/5.4인데
    /// 야외 광각은 `02` 2.4/0.1 · `18` 2.0/0.5다. **입구 근접만 탄다.**
    /// 원인 이름을 먼저 붙이지 않는다 — 같은 카메라에서 조건만 바꿔 **나란히 렌더해** 가른다:
    ///   A 지금 그대로 · B 입구 점광만 끔 · C 점광 전부 끔 · D 태양만 끔(점광+앰비언트) · E 빛 전부 끔(=알베도).
    /// E는 재질이 원래 얼마나 밝은지를 보여 준다 — 알베도가 이미 흰빛이면 조명을 아무리 낮춰도 탄다.
    ///
    /// 대조군으로 야외 광각(`02`·`18`)을 같은 조건으로 함께 잰다. 「입구만 그런가」를 이 자가 스스로 갈라야 한다.
    /// </summary>
    public static class ExposureCensus
    {
        const int W = 640, H = 360;

        public static void RunEntranceExposure()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);

            // `06`도 표본이다(검수 관찰 2026-09-09 — 「그 화면 오른쪽 아래 잔디도 노랗게 타 있다」).
            string[] want = { "07_d1_entrance", "09_d2_entrance", "11_d3_entrance",
                              "06_field_boss", "02_village_wide", "18_meadow" };
            var shots = QaShots.BuildShots();

            var camGo = new GameObject("ExposureCam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 500f;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            cam.targetTexture = rt;

            var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var keepAmbient = RenderSettings.ambientIntensity;
            try
            {
                for (int s = 0; s < want.Length; s++)
                {
                    int idx = -1;
                    for (int i = 0; i < shots.Length; i++)
                        if (QaShots.NameOf(shots[i]) == want[s]) { idx = i; break; }
                    if (idx < 0) { Debug.LogWarning("[노출] 샷 " + want[s] + "을 목록에서 못 찾았습니다."); continue; }
                    QaShots.EyeOf(shots[idx], out Vector3 eye, out Vector3 look);
                    camGo.transform.position = eye;
                    camGo.transform.LookAt(look);

                    string line = "[노출] " + want[s];
                    line += " · A 지금 " + Measure(cam, rt, tex);

                    line += " · B 입구점광끔 " + WithLights(cam, rt, tex, lights, l => l.name.StartsWith("DungeonEntranceLight"));
                    line += " · C 점광전부끔 " + WithLights(cam, rt, tex, lights, l => l.type != LightType.Directional);
                    line += " · D 태양끔 " + WithLights(cam, rt, tex, lights, l => l.type == LightType.Directional);

                    // E — 빛을 다 끄고 앰비언트만 남긴다(재질이 원래 얼마나 밝은가).
                    line += " · E 알베도 " + WithLights(cam, rt, tex, lights, l => true);
                    Debug.Log(line);
                }
            }
            finally
            {
                RenderSettings.ambientIntensity = keepAmbient;
                for (int i = 0; i < lights.Length; i++) if (lights[i] != null) lights[i].enabled = true;
                cam.targetTexture = null;
                RenderTexture.active = null;
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(tex);
            }
            SweepEntranceLight();
            BlameLights("06_field_boss");
            SweepBossAura();
            Debug.Log("[노출] 읽는 법 — 「250↑ 몫 / R·G 동시 250↑ 몫 / 평균 밝기」. 조건을 껐을 때 크게 내려간 것이 태우는 자다.");
            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(0);
        }

        /// <summary>
        /// **얼마로 낮출 것인가도 센다** — 처방을 눈으로 고르면 다음 사람이 그 수의 근거를 못 찾는다.
        /// 입구 점광 세기를 후보값으로 바꿔 가며 `07`에서 포화 몫을 읽는다. 고르는 기준은
        /// 「야외 광각(`02` 2.3 / `18` 1.8) 언저리로 내려오되 등불이 제 몫을 하는 값」이다.
        /// </summary>
        static void SweepEntranceLight()
        {
            var shots = QaShots.BuildShots();
            int idx = -1;
            for (int i = 0; i < shots.Length; i++)
                if (QaShots.NameOf(shots[i]) == "07_d1_entrance") { idx = i; break; }
            if (idx < 0) return;
            QaShots.EyeOf(shots[idx], out Vector3 eye, out Vector3 look);

            var camGo = new GameObject("ExposureSweepCam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 500f;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            cam.targetTexture = rt;
            camGo.transform.position = eye;
            camGo.transform.LookAt(look);

            var all = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var lamps = new System.Collections.Generic.List<Light>();
            var keep = new System.Collections.Generic.List<float>();
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].name.StartsWith("DungeonEntranceLight"))
                { lamps.Add(all[i]); keep.Add(all[i].intensity); }
            try
            {
                foreach (float v in new[] { 3.2f, 2.4f, 1.8f, 1.2f, 0.8f, 0.5f })
                {
                    for (int i = 0; i < lamps.Count; i++) lamps[i].intensity = v;
                    Debug.Log("[노출] 07 세기 " + v.ToString("0.0") + " → " + Measure(cam, rt, tex));
                }
            }
            finally
            {
                for (int i = 0; i < lamps.Count; i++) lamps[i].intensity = keep[i];
                cam.targetTexture = null; RenderTexture.active = null;
                Object.DestroyImmediate(camGo); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
            }
        }

        /// <summary>
        /// **게이트가 쓰는 한 값** — 이 샷의 250↑ 포화 몫(0~1). 못 찾으면 −1.
        /// 셈과 자가 **같은 함수**를 읽어야 「셈에서는 3.3인데 자는 통과」 같은 어긋남이 안 생긴다.
        /// </summary>
        public static float HotShare(string shotName) => HotShareWithLampIntensity(shotName, float.NaN);

        /// <summary>
        /// 같은 값을 재되, **등불 세기를 잠깐 바꿔** 잰다(NC용). `NaN`이면 지금 세기 그대로.
        /// 끝나면 반드시 되돌린다 — 재는 자가 세계를 바꿔 두면 그다음 자가 다른 세계를 잰다.
        /// </summary>
        public static float HotShareWithLampIntensity(string shotName, float intensity)
        {
            var shots = QaShots.BuildShots();
            int idx = -1;
            for (int i = 0; i < shots.Length; i++)
                if (QaShots.NameOf(shots[i]) == shotName) { idx = i; break; }
            if (idx < 0) return -1f;
            QaShots.EyeOf(shots[idx], out Vector3 eye, out Vector3 look);

            var camGo = new GameObject("ExposureGateCam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 500f;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            cam.targetTexture = rt;
            camGo.transform.position = eye;
            camGo.transform.LookAt(look);

            var lamps = new System.Collections.Generic.List<Light>();
            var keep = new System.Collections.Generic.List<float>();
            if (!float.IsNaN(intensity))
                foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (l != null && l.name.StartsWith("DungeonEntranceLight"))
                    { lamps.Add(l); keep.Add(l.intensity); l.intensity = intensity; }
            try
            {
                string m = Measure(cam, rt, tex);
                return float.Parse(m.Split('/')[0], System.Globalization.CultureInfo.InvariantCulture) * 0.01f;
            }
            finally
            {
                for (int i = 0; i < lamps.Count; i++) lamps[i].intensity = keep[i];
                cam.targetTexture = null; RenderTexture.active = null;
                Object.DestroyImmediate(camGo); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
            }
        }

        /// <summary>
        /// **누가 태우는지 이름을 댄다** — 점광을 하나씩 꺼 보고 포화 몫이 가장 많이 내려간 순으로 적는다.
        /// `06`은 입구 등불과 무관한데도 8.6%가 탔다(입구 점광만 끄면 그대로, 점광 전부 끄면 1.7).
        /// 「어느 부류의 점광인가」를 모르면 처방이 또 짐작이 된다.
        /// </summary>
        static void BlameLights(string shotName)
        {
            var shots = QaShots.BuildShots();
            int idx = -1;
            for (int i = 0; i < shots.Length; i++)
                if (QaShots.NameOf(shots[i]) == shotName) { idx = i; break; }
            if (idx < 0) return;
            QaShots.EyeOf(shots[idx], out Vector3 eye, out Vector3 look);

            var camGo = new GameObject("ExposureBlameCam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 500f;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            cam.targetTexture = rt;
            camGo.transform.position = eye;
            camGo.transform.LookAt(look);
            try
            {
                float baseHot = Hot(Measure(cam, rt, tex));
                var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                var rows = new System.Collections.Generic.List<(string Name, float Drop, float Dist)>();
                for (int i = 0; i < lights.Length; i++)
                {
                    if (lights[i] == null || lights[i].type == LightType.Directional) continue;
                    bool was = lights[i].enabled;
                    lights[i].enabled = false;
                    float drop = baseHot - Hot(Measure(cam, rt, tex));
                    lights[i].enabled = was;
                    if (drop > 0.05f)
                        rows.Add((lights[i].name, drop, Vector3.Distance(lights[i].transform.position, look)));
                }
                rows.Sort((a, b) => b.Drop.CompareTo(a.Drop));
                string line = "[노출] " + shotName + " 태우는 점광(끄면 내려가는 몫) — 지금 " + baseHot.ToString("0.0") + "%";
                for (int i = 0; i < rows.Count && i < 6; i++)
                    line += " · " + rows[i].Name + " −" + rows[i].Drop.ToString("0.0") + "(" + rows[i].Dist.ToString("0") + "m)";
                if (rows.Count == 0) line += " · 하나씩 꺼도 0.05% 넘게 내려가는 것이 없다(여럿이 함께 태운다)";
                Debug.Log(line);
            }
            finally
            {
                cam.targetTexture = null; RenderTexture.active = null;
                Object.DestroyImmediate(camGo); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
            }
        }

        /// <summary>
        /// **오라 세기도 재서 고른다**(검수 조건 2026-09-09 — 「값은 07처럼 스윕으로」).
        /// 보스 근접 셋(`06`·`17`·`41`)에서 같이 읽는다 — 한 샷만 보고 내리면 다른 보스 샷이 어두워진다.
        /// </summary>
        static void SweepBossAura()
        {
            var lights = new System.Collections.Generic.List<Light>();
            var keep = new System.Collections.Generic.List<float>();
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (l != null && l.name.StartsWith("BossAura")) { lights.Add(l); keep.Add(l.intensity); }
            if (lights.Count == 0) { Debug.LogWarning("[노출] 보스 오라를 못 찾았습니다."); return; }

            var shots = QaShots.BuildShots();
            string[] want = { "06_field_boss", "17_boss_closeup", "41_boss3" };
            var cams = new System.Collections.Generic.List<(string Name, Vector3 Eye, Vector3 Look)>();
            for (int i = 0; i < shots.Length; i++)
                for (int k = 0; k < want.Length; k++)
                    if (QaShots.NameOf(shots[i]) == want[k])
                    {
                        QaShots.EyeOf(shots[i], out Vector3 e, out Vector3 l);
                        cams.Add((want[k], e, l));
                    }

            var camGo = new GameObject("ExposureAuraCam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 500f;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            cam.targetTexture = rt;
            try
            {
                foreach (float v in new[] { 3.4f, 2.4f, 1.6f, 1.0f, 0.6f })
                {
                    for (int i = 0; i < lights.Count; i++) lights[i].intensity = v;
                    string line = "[노출] 보스 오라 세기 " + v.ToString("0.0");
                    for (int c = 0; c < cams.Count; c++)
                    {
                        camGo.transform.position = cams[c].Eye;
                        camGo.transform.LookAt(cams[c].Look);
                        // **번짐도 같이 잰다**(검수 관찰: 「오라가 발밑 땅과 그림자까지 초록으로 물들인다」).
                        // 포화만 보면 「덜 타지만 여전히 초록」인 판을 통과시킨다 — 다른 병이다.
                        line += " · " + cams[c].Name + " " + Measure(cam, rt, tex) + " 초록 " + GreenCast(tex) + "%";
                    }
                    Debug.Log(line);
                }
            }
            finally
            {
                for (int i = 0; i < lights.Count; i++) lights[i].intensity = keep[i];
                cam.targetTexture = null; RenderTexture.active = null;
                Object.DestroyImmediate(camGo); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
            }
        }

        /// <summary>초록으로 물든 몫 — G가 R·B보다 뚜렷이(30↑) 큰 픽셀의 비율(%).</summary>
        static string GreenCast(Texture2D tex)
        {
            var px = tex.GetPixels32();
            int green = 0;
            for (int i = 0; i < px.Length; i++)
                if (px[i].g - px[i].r >= 30 && px[i].g - px[i].b >= 30) green++;
            return (100f * green / px.Length).ToString("0.0");
        }

        static float Hot(string measured) =>
            float.Parse(measured.Split('/')[0], System.Globalization.CultureInfo.InvariantCulture);

        static string WithLights(Camera cam, RenderTexture rt, Texture2D tex, Light[] lights, System.Func<Light, bool> off)
        {
            var was = new bool[lights.Length];
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] == null) continue;
                was[i] = lights[i].enabled;
                if (off(lights[i])) lights[i].enabled = false;
            }
            string r = Measure(cam, rt, tex);
            for (int i = 0; i < lights.Length; i++)
                if (lights[i] != null) lights[i].enabled = was[i];
            return r;
        }

        /// <summary>한 판 렌더해 포화 몫을 읽는다 — 250↑ 픽셀, R·G 동시 250↑, 평균 밝기.</summary>
        static string Measure(Camera cam, RenderTexture rt, Texture2D tex)
        {
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            var px = tex.GetPixels32();
            int hot = 0, rg = 0;
            long sum = 0;
            for (int i = 0; i < px.Length; i++)
            {
                int rr = px[i].r, gg = px[i].g, bb = px[i].b;
                int max = rr > gg ? (rr > bb ? rr : bb) : (gg > bb ? gg : bb);
                if (max >= 250) hot++;
                if (rr >= 250 && gg >= 250) rg++;
                sum += (rr + gg + bb) / 3;
            }
            float n = px.Length;
            return (100f * hot / n).ToString("0.0") + "/" + (100f * rg / n).ToString("0.0") + "/" + (sum / n).ToString();
        }
    }
}
