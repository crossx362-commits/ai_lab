using System;
using System.Collections.Generic;
using Ulon.Client;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **가리는 대신 비치게** — 시야를 막는 건물을 끄면 마을이 빈 흙바닥이 된다(검수 반려 2026-09-07).
    /// 알파만 낮춰 실루엣·그림자를 남긴다. 이 게이트가 세 가지를 강제한다:
    /// ① 알파가 하한 위인가(0으로 내리면 끈 것과 같다) ② 렌더러는 켜져 있는가(실루엣이 남는가)
    /// ③ 페이드를 풀면 **원본 머티리얼로 정확히 복구**되는가(원본을 파괴하지 않았는가).
    /// </summary>
    public static partial class SliceSelfCheck
    {
        static void AssertSightFadeTranslucent()
        {
            var qv = UnityEngine.Object.FindFirstObjectByType<QuarterViewCamera>(FindObjectsInactive.Include);
            float pitch = qv != null ? qv.Pitch : 35f;
            float yaw = qv != null ? qv.Yaw : 45f;
            float dist = qv != null ? qv.Distance : 12f;
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            // 은행(풍차) 뒤 — 실제로 건물이 시야를 막던 자리다.
            var feet = new Vector3(-10f, GroundYAt(new Vector2(-10f, 8f)), 8f);
            var look = feet + Vector3.up * 1.0f;
            var eye = look - rot * Vector3.forward * dist;

            var faded = new List<Renderer>();
            var before = new Dictionary<Renderer, Material[]>();
            DungeonSightFade.Hide(eye, look, DungeonSightFade.DefaultRadius, faded);
            try
            {
                if (faded.Count == 0)
                    throw new InvalidOperationException("은행 뒤에서 걷힌 렌더러가 0개입니다 — 잰 것이 없습니다(0이면 실패).");
                for (int i = 0; i < faded.Count; i++)
                {
                    var rend = faded[i];
                    if (rend == null)
                        continue;
                    if (!rend.enabled)
                        throw new InvalidOperationException(rend.name + "이 꺼져 있습니다 — 이제는 끄는 게 아니라 비치게 합니다(실루엣·그림자가 남아야 합니다).");
                    CheckGhostAlpha(rend.sharedMaterial, rend.name);
                }
                for (int i = 0; i < faded.Count; i++)
                    if (faded[i] != null)
                        Debug.Log("[Ulon] 시야 페이드 대상 — " + faded[i].transform.root.name + "/" + faded[i].name +
                                  " 크기 " + faded[i].bounds.size.ToString("0.00"));
                // 「은행이 무엇으로 보이나」는 대조표에 쓸 사실이다 — 렌더러 목록을 남긴다.
                var bank = GameObject.Find("Banker");
                if (bank != null)
                {
                    var rs = bank.GetComponentsInChildren<Renderer>(true);
                    for (int i = 0; i < rs.Length; i++)
                        Debug.Log("[Ulon] 은행 렌더러 — " + rs[i].name + " 메시 " +
                                  (rs[i].GetComponent<MeshFilter>() != null && rs[i].GetComponent<MeshFilter>().sharedMesh != null
                                   ? UnityEditor.AssetDatabase.GetAssetPath(rs[i].GetComponent<MeshFilter>().sharedMesh) : "(없음)") +
                                  " 크기 " + rs[i].bounds.size.ToString("0.00"));
                }
                Debug.Log("[Ulon] 시야 페이드 반투명 — 은행 뒤에서 " + faded.Count + "개가 알파 " +
                          DungeonSightFade.GhostAlpha.ToString("0.00") + "로 비친다(하한 " +
                          DungeonSightFade.GhostAlphaMin.ToString("0.00") + ", 렌더러는 켜진 채)");
                for (int i = 0; i < faded.Count; i++)
                    if (faded[i] != null)
                        before[faded[i]] = faded[i].sharedMaterials;
            }
            finally
            {
                var copy = new List<Renderer>(faded);
                DungeonSightFade.Restore(faded);
                // ③ 원상복구 — 페이드 사본이 남아 있으면 다음 프레임부터 마을이 계속 반투명이다.
                int restored = 0;
                for (int i = 0; i < copy.Count; i++)
                {
                    var rend = copy[i];
                    if (rend == null)
                        continue;
                    var mats = rend.sharedMaterials;
                    for (int m = 0; m < mats.Length; m++)
                        if (mats[m] != null && mats[m].name.EndsWith("(Ghost)", StringComparison.Ordinal))
                            throw new InvalidOperationException(rend.name + "이 페이드 사본을 그대로 달고 있습니다 — 원상복구가 안 됩니다.");
                    restored++;
                }
                Debug.Log("[Ulon] 시야 페이드 원상복구 — " + restored + "/" + faded.Count + "개 원본 머티리얼로 되돌림");
            }
        }

        static void CheckGhostAlpha(Material m, string who)
        {
            if (m == null)
                throw new InvalidOperationException(who + ": 머티리얼이 없습니다.");
            float a = m.HasProperty("_Color") ? m.color.a : 1f;
            if (a < DungeonSightFade.GhostAlphaMin)
                throw new InvalidOperationException(who + ": 페이드 알파 " + a.ToString("0.00") + "가 하한 " +
                    DungeonSightFade.GhostAlphaMin.ToString("0.00") + " 아래입니다 — 이건 「비침」이 아니라 「사라짐」입니다.");
            if (a >= 1f)
                throw new InvalidOperationException(who + ": 페이드 알파가 " + a.ToString("0.00") + "입니다 — 전혀 비치지 않습니다.");
        }

        /// <summary>
        /// 네거티브 컨트롤 — 알파를 0으로 만든 사본을 같은 검사에 먹이면 빨간불이어야 한다.
        /// (씬을 건드리지 않고 판정 함수 자체를 검사한다.)
        /// </summary>
        static void AssertSightFadeTranslucentNegativeControl()
        {
            var src = new Material(Shader.Find("Standard"));
            bool redZero = false, redOpaque = false;
            var zero = DungeonSightFade.MakeGhost(src, 0f);
            try { CheckGhostAlpha(zero, "NC-투명0"); }
            catch (InvalidOperationException) { redZero = true; }
            var opaque = DungeonSightFade.MakeGhost(src, 1f);
            try { CheckGhostAlpha(opaque, "NC-불투명"); }
            catch (InvalidOperationException) { redOpaque = true; }
            UnityEngine.Object.DestroyImmediate(zero);
            UnityEngine.Object.DestroyImmediate(opaque);
            UnityEngine.Object.DestroyImmediate(src);
            if (!redZero)
                throw new InvalidOperationException("페이드 알파 네거티브 컨트롤 실패 — 알파 0을 통과시켰습니다(사라진 것과 같습니다).");
            if (!redOpaque)
                throw new InvalidOperationException("페이드 알파 네거티브 컨트롤 실패 — 알파 1을 통과시켰습니다(전혀 안 비칩니다).");
            Debug.Log("[Ulon] 페이드 알파 네거티브 컨트롤 — 알파 0(사라짐)과 알파 1(안 비침) 둘 다 빨간불");
        }

        /// <summary>
        /// **근접 샷의 피사체는 자기 자신을 가림으로 세지 않는다**(검수 판정 2026-09-07 1).
        ///
        /// `33_campfire`에서 돌·장작이 반투명 유령으로 찍혔다. 페이드 **사본이 남은 것이 아니라**
        /// (기존 원상복구 게이트는 자기가 걷은 렌더러만 보고, 그건 실제로 복구됐다) — 근접 샷이
        /// look 지점에 **피사체를 놓기 때문**이다. 게임에선 look이 플레이어(블로커 레이어가 아님)라
        /// 안 생기는 일이고, QA 도구만 겪는다. **판정할 대상이 반투명으로 찍히면 그 샷은 증거가 아니다.**
        ///
        /// 그래서 「피사체를 빼면 안 걷히고(정상), 안 빼면 걷힌다(NC)」를 양쪽 다 잰다.
        /// </summary>
        static int GhostedCount(Transform subject)
        {
            int n = 0;
            var rs = subject.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rs.Length; i++)
                if (DungeonSightFade.IsGhosted(rs[i]))
                    n++;
            return n;
        }

        static bool CloseUpView(string objectName, out Transform subject, out Vector3 eye, out Vector3 look)
        {
            eye = Vector3.zero;
            look = Vector3.zero;
            var go = GameObject.Find(objectName);
            subject = go != null ? go.transform : null;
            if (go == null)
                return false;
            var rs = go.GetComponentsInChildren<Renderer>(false);
            bool any = false;
            Bounds box = new Bounds();
            for (int i = 0; i < rs.Length; i++)
            {
                if (rs[i] is ParticleSystemRenderer || !rs[i].enabled)
                    continue;
                if (!any) { box = rs[i].bounds; any = true; }
                else box.Encapsulate(rs[i].bounds);
            }
            if (!any)
                return false;
            look = box.center;
            float dist = Mathf.Max(box.extents.magnitude, 0.6f) / Mathf.Tan(55f * 0.5f * Mathf.Deg2Rad) * 1.35f;
            eye = look - Quaternion.Euler(35f, 45f, 0f) * Vector3.forward * dist;
            return true;
        }

        static void AssertCloseUpSubjectNotGhosted()
        {
            if (!CloseUpView("Campfire", out Transform subject, out Vector3 eye, out Vector3 look))
                throw new InvalidOperationException("근접 샷 피사체 검사 대상(Campfire)을 못 찾았습니다 — 잰 것이 없습니다(0이면 실패).");
            var faded = new List<Renderer>();
            int ghosted;
            try
            {
                DungeonSightFade.Hide(eye, look, DungeonSightFade.DefaultRadius, faded, subject);
                ghosted = GhostedCount(subject);
            }
            finally { DungeonSightFade.Restore(faded); }
            Debug.Log("[Ulon] 근접 샷 피사체 — 화덕을 look 지점에 두고 걷힌 렌더러 " + faded.Count +
                      "개, 그중 피사체 자신 " + ghosted + "개(0이어야 한다)");
            if (ghosted > 0)
                throw new InvalidOperationException("근접 샷 피사체가 자기 시야 페이드에 " + ghosted +
                    "개 물렸습니다 — 판정할 대상이 반투명으로 찍힙니다(33_campfire 반려).");
        }

        static void AssertCloseUpSubjectNotGhostedNegativeControl()
        {
            if (!CloseUpView("Campfire", out Transform subject, out Vector3 eye, out Vector3 look))
                throw new InvalidOperationException("근접 샷 피사체 NC 대상(Campfire)을 못 찾았습니다.");
            var faded = new List<Renderer>();
            int ghosted;
            try
            {
                DungeonSightFade.Hide(eye, look, DungeonSightFade.DefaultRadius, faded);   // 피사체를 안 뺀다
                ghosted = GhostedCount(subject);
            }
            finally { DungeonSightFade.Restore(faded); }
            if (ghosted == 0)
                throw new InvalidOperationException("근접 샷 피사체 네거티브 컨트롤 실패 — 피사체를 안 뺐는데도 걷힌 것이 0개입니다. " +
                    "결함을 못 만들었으니 게이트가 아니라 NC가 틀렸다(화덕이 페이드 레이어에 없나?).");
            Debug.Log("[Ulon] 근접 샷 피사체 네거티브 컨트롤 통과 — 피사체를 안 빼면 " + ghosted + "개가 반투명이 된다");
        }
    }
}
