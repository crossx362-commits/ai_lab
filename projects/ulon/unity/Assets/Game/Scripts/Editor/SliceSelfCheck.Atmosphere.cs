using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ulon.Editor
{
    /// <summary>
    /// **대기가 원장 하나에서 오는가** — 하늘·앰비언트·안개 값이 씬에 원장 그대로 새겨져 있는가.
    ///
    /// 「방향광 하나」와 같은 결이다(검수 지시 2026-09-09). 화면 픽셀을 재는 자를 새로 세우지 않는다 —
    /// 하늘 색이 틀렸던 원인은 **값이 세 곳에 갈라져 있어서**였지 픽셀을 안 봐서가 아니었다.
    /// 앰비언트가 `SetupLighting`·`SetupSky`·`CreateBootstrapScene` 세 곳에 각각 적혀 있었고
    /// (0.55,0.58,0.52 / 0.58,0.62,0.55 / 0.55,0.58,0.52) **마지막에 부른 쪽이 이겼다**.
    /// 값이 여럿이면 화면은 그중 하나만 보여 주고 나머지는 거짓말이다.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        static string AtmosphereMismatch()
        {
            if (RenderSettings.ambientMode != AmbientMode.Flat)
                return "앰비언트 모드가 Flat이 아닙니다(" + RenderSettings.ambientMode + ").";
            if (!Near(RenderSettings.ambientLight, VisualSliceBuilder.AmbientLight))
                return "앰비언트 " + RenderSettings.ambientLight + " ≠ 원장 " + VisualSliceBuilder.AmbientLight + ".";
            if (!RenderSettings.fog)
                return "안개가 꺼져 있습니다 — 원경 실루엣이 원장과 달라집니다.";
            if (!Near(RenderSettings.fogColor, VisualSliceBuilder.FogColor))
                return "안개 색 " + RenderSettings.fogColor + " ≠ 원장 " + VisualSliceBuilder.FogColor + ".";
            if (Mathf.Abs(RenderSettings.fogStartDistance - VisualSliceBuilder.FogStart) > 0.5f)
                return "안개 시작 " + RenderSettings.fogStartDistance + "m ≠ 원장 " + VisualSliceBuilder.FogStart + "m.";

            var sky = RenderSettings.skybox;
            if (sky == null)
                return "스카이박스 재질이 없습니다 — 잰 것이 없습니다(0이면 실패).";
            if (sky.shader == null || sky.shader.name != "Skybox/Procedural")
                return "스카이박스 셰이더가 Skybox/Procedural이 아닙니다(" +
                       (sky.shader != null ? sky.shader.name : "없음") + ").";
            float atm = sky.GetFloat("_AtmosphereThickness");
            if (Mathf.Abs(atm - VisualSliceBuilder.SkyAtmosphere) > 0.01f)
                return "대기 두께 " + atm.ToString("0.00") + " ≠ 원장 " + VisualSliceBuilder.SkyAtmosphere +
                       " — 하늘 색을 정하는 것은 이 값 하나다(0.95면 수평선 위가 형광 연두로 뜬다).";
            if (Mathf.Abs(sky.GetFloat("_Exposure") - VisualSliceBuilder.SkyExposure) > 0.01f)
                return "하늘 노출 " + sky.GetFloat("_Exposure") + " ≠ 원장 " + VisualSliceBuilder.SkyExposure + ".";
            if (!Near(sky.GetColor("_SkyTint"), VisualSliceBuilder.SkyTint))
                return "하늘 색조 " + sky.GetColor("_SkyTint") + " ≠ 원장 " + VisualSliceBuilder.SkyTint + ".";
            return null;
        }

        static bool Near(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f;

        static void AssertAtmosphereFromLedger()
        {
            string bad = AtmosphereMismatch();
            if (bad != null)
                throw new InvalidOperationException("대기가 원장과 다릅니다 — " + bad +
                    " 하늘·앰비언트·안개는 `VisualSliceBuilder.EnsureWorldAtmosphere()` 한 곳에서만 정해집니다(§8.2).");
            Debug.Log("[Ulon] 대기 원장 일치 — 앰비언트 " + VisualSliceBuilder.AmbientLight +
                      " · 안개 " + VisualSliceBuilder.FogColor + " 시작 " + VisualSliceBuilder.FogStart +
                      "m · 대기 두께 " + VisualSliceBuilder.SkyAtmosphere + " · 노출 " + VisualSliceBuilder.SkyExposure);
        }

        /// <summary>
        /// 양방향 NC — 옛 두께(0.95, 초록 띠가 뜨던 값)를 넣으면 빨간불, 되돌리면 초록.
        /// 값을 되돌린 뒤 재질을 더럽히지 않는다(재는 자가 세계를 바꾸면 그 판의 초록은 못 믿는다).
        /// </summary>
        static void AssertAtmosphereFromLedgerNegativeControl()
        {
            var sky = RenderSettings.skybox;
            if (sky == null)
                throw new InvalidOperationException("대기 NC 대상이 없습니다 — 스카이박스가 없습니다(0이면 실패).");
            float keep = sky.GetFloat("_AtmosphereThickness");
            bool red;
            try
            {
                sky.SetFloat("_AtmosphereThickness", 0.95f);
                red = AtmosphereMismatch() != null;
            }
            finally
            {
                sky.SetFloat("_AtmosphereThickness", keep);
            }
            if (!red)
                throw new InvalidOperationException("대기 네거티브 컨트롤 실패 — 두께를 0.95(초록 띠)로 바꿨는데 통과했습니다.");
            string back = AtmosphereMismatch();
            if (back != null)
                throw new InvalidOperationException("대기 네거티브 컨트롤 실패 — 되돌렸는데도 빨간불입니다: " + back);
            Debug.Log("[Ulon] 대기 원장 양방향 NC 통과 — 두께 0.95면 FAIL · 되돌리면 다시 통과");
        }
    }
}
