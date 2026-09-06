using System;
using UnityEngine;
using Ulon.Client;

namespace Ulon.Editor
{
    /// <summary>
    /// §11.2·§18.15 행동 VFX 게이트 — 「효과가 있다」가 아니라 **화면으로** 잰다.
    /// 카메라를 검은 배경으로 두고 효과 하나만 실제로 렌더해서
    /// ① 오프스크린 렌더에 실제로 픽셀이 찍히는가(파티클이 샷에 안 잡히는 흔한 함정)
    /// ② 3종이 서로 **다른 색**인가(색상환 각도)
    /// ③ 3종이 서로 **다른 형태**인가(퍼짐 반경)
    /// 를 확인한다. 네거티브 컨트롤은 템플릿을 실제로 지우고 빨간불을 본다.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        const int VfxW = 320, VfxH = 240;
        const float VfxTestY = 800f;             // 지형 위 허공 — 화면에 다른 것이 들어오지 않는다
        const int VfxMinLitPixels = 200;         // 이보다 적으면 「샷에 안 찍힌다」
        const float VfxHueGapMin = 40f;          // 색상환 각도 차(도)
        const float VfxSpreadGapMin = 0.12f;     // 퍼짐 반경 차(화면 높이 비율)

        struct VfxMeasure
        {
            public int Lit;
            public float Hue;                    // 0~360
            public float Spread;                 // 밝은 픽셀의 평균 반경 / 화면 높이
        }

        public static void AssertActionVfxOnScreen()
        {
            var kinds = new[] { ActionVfx.Kind.Hit, ActionVfx.Kind.Heal, ActionVfx.Kind.Craft };
            var m = new VfxMeasure[kinds.Length];
            for (int i = 0; i < kinds.Length; i++)
            {
                if (ActionVfx.Template(kinds[i]) == null)
                    throw new Exception("VFX 템플릿이 없습니다: " + ActionVfx.ObjectFor(kinds[i]));
                m[i] = MeasureVfx(kinds[i]);
                Debug.Log("[Ulon] VFX " + kinds[i] + " — 픽셀 " + m[i].Lit + ", 색상 " +
                          m[i].Hue.ToString("F0") + "°, 퍼짐 " + m[i].Spread.ToString("F2"));
                if (m[i].Lit < VfxMinLitPixels)
                    throw new Exception("VFX " + kinds[i] + "가 오프스크린 렌더에 거의 안 찍힙니다(" +
                        m[i].Lit + "px < " + VfxMinLitPixels + ") — 화면에 뜬다고 말할 수 없습니다.");
            }

            for (int a = 0; a < kinds.Length; a++)
                for (int b = a + 1; b < kinds.Length; b++)
                {
                    float hue = HueGap(m[a].Hue, m[b].Hue);
                    float spread = Mathf.Abs(m[a].Spread - m[b].Spread);
                    if (hue < VfxHueGapMin && spread < VfxSpreadGapMin)
                        throw new Exception("VFX " + kinds[a] + "·" + kinds[b] + "가 화면에서 구분되지 않습니다 — 색 차 " +
                            hue.ToString("F0") + "°(하한 " + VfxHueGapMin + "), 퍼짐 차 " +
                            spread.ToString("F2") + "(하한 " + VfxSpreadGapMin + ")");
                }
            Debug.Log("[Ulon] 행동 VFX 3종 화면 확인 — 색·형태가 서로 다르고 오프스크린 샷에 찍힌다");
        }

        /// <summary>네거티브 컨트롤: 템플릿을 실제로 지우면 화면에 아무것도 안 남아야 한다(빨간불 확인).</summary>
        public static void AssertActionVfxNegativeControl()
        {
            var template = ActionVfx.Template(ActionVfx.Kind.Hit);
            var parent = template.transform.parent;
            var backup = UnityEngine.Object.Instantiate(template.gameObject, parent);
            backup.SetActive(false);
            string name = template.name;
            UnityEngine.Object.DestroyImmediate(template.gameObject);
            bool caught = false;
            try { AssertActionVfxOnScreen(); }
            catch (Exception e) { caught = true; Debug.Log("[Ulon] VFX 네거티브 컨트롤 빨간불 — " + e.Message); }
            backup.name = name;
            if (!caught)
                throw new Exception("VFX 네거티브 컨트롤 실패 — 템플릿을 지웠는데도 게이트가 통과했습니다.");
            AssertActionVfxOnScreen();      // 복구 확인
        }

        /// <summary>
        /// QA 샷용 — 3종을 나란히 한 번 재생시켜 **같은 화면에서** 색·형태 차이를 눈으로 볼 수 있게 한다.
        /// 반환한 루트는 렌더 직후 호출자가 지운다(다른 샷에 남지 않게).
        /// </summary>
        public static GameObject SpawnVfxTrio(Vector3 center)
        {
            var root = new GameObject("QaActionVfxTrio");
            var kinds = new[] { ActionVfx.Kind.Hit, ActionVfx.Kind.Heal, ActionVfx.Kind.Craft };
            for (int i = 0; i < kinds.Length; i++)
            {
                var template = ActionVfx.Template(kinds[i]);
                if (template == null) continue;
                var pos = center + new Vector3((i - 1) * 1.3f, 0.4f, 0f);
                var go = UnityEngine.Object.Instantiate(template.gameObject, pos, template.transform.rotation, root.transform);
                go.SetActive(true);
                var ps = go.GetComponent<ParticleSystem>();
                if (ps != null) ps.Simulate(0.30f, true, true);
            }
            return root;
        }

        static VfxMeasure MeasureVfx(ActionVfx.Kind kind)
        {
            var template = ActionVfx.Template(kind);
            var pos = new Vector3(0f, VfxTestY, 0f);
            var go = UnityEngine.Object.Instantiate(template.gameObject, pos, template.transform.rotation);
            go.SetActive(true);
            var ps = go.GetComponent<ParticleSystem>();

            var camGo = new GameObject("VfxProbeCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 30f;
            camGo.transform.position = pos + new Vector3(0f, 0f, -4f);
            camGo.transform.LookAt(pos);

            var rt = new RenderTexture(VfxW, VfxH, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(VfxW, VfxH, TextureFormat.RGB24, false);
            var result = new VfxMeasure();
            try
            {
                ps.Simulate(0.30f, true, true);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, VfxW, VfxH), 0, 0);
                tex.Apply();
                RenderTexture.active = null;

                var px = tex.GetPixels();
                float rs = 0f, gs = 0f, bs = 0f, radius = 0f;
                int lit = 0;
                for (int i = 0; i < px.Length; i++)
                {
                    var c = px[i];
                    if (c.r + c.g + c.b < 0.18f) continue;      // 검은 배경
                    lit++;
                    rs += c.r; gs += c.g; bs += c.b;
                    int x = i % VfxW, y = i / VfxW;
                    float dx = (x - VfxW * 0.5f) / VfxH, dy = (y - VfxH * 0.5f) / VfxH;
                    radius += Mathf.Sqrt(dx * dx + dy * dy);
                }
                result.Lit = lit;
                if (lit > 0)
                {
                    float h, s, v;
                    Color.RGBToHSV(new Color(rs / lit, gs / lit, bs / lit), out h, out s, out v);
                    result.Hue = h * 360f;
                    result.Spread = radius / lit;
                }
            }
            finally
            {
                cam.targetTexture = null;
                RenderTexture.active = null;
                UnityEngine.Object.DestroyImmediate(camGo);
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(tex);
                UnityEngine.Object.DestroyImmediate(go);
            }
            return result;
        }

        static float HueGap(float a, float b)
        {
            float d = Mathf.Abs(a - b) % 360f;
            return d > 180f ? 360f - d : d;
        }
    }
}
