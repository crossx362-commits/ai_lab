using System;
using System.Collections.Generic;
using Ulon.Server;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **사람·짐승의 발이 바로 밑 바닥에 닿아 있는가**(검수 판정 2026-09-07 ①).
    ///
    /// 기존 전역 발 높이 게이트(`AssertFootOnGround`)는 **씬 루트와 그 직계 자식**만 모은다 —
    /// 시설 밑에 재부모된 서비스 NPC 넷은 조상 바운드에 섞여 **자기 발이 한 번도 안 재졌다**
    /// (실측 0.04m로 멀쩡했지만, 떠 있어도 초록불이었을 자리다).
    ///
    /// 대상을 넓히면서 규칙을 **하나 줄인다**(검수): 「지표 y와 비교」를 버리고 **발 밑으로 광선을 쏴
    /// 바로 아래 표면**과 견준다. 그러면 마을이든 지하 방이든 같은 규칙 하나로 끝나고,
    /// 「던전 액터는 지표 −5.7m가 정상」이라는 예외가 **아예 없어진다**.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        /// <summary>발과 바로 아래 표면 사이 허용 오차 — 실측 마을 0.00~0.04m, 지하 방 0.0x m.</summary>
        const float ActorFootErrorMax = 0.15f;

        /// <summary>씬의 액터 전수 — **재부모 여부와 무관하게** 잡는다(이번 사고의 본질이 그것이다).</summary>
        static List<Transform> ActorTargets()
        {
            var list = new List<Transform>();
            var actors = UnityEngine.Object.FindObjectsByType<CharacterController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < actors.Length; i++)
                if (actors[i].gameObject.activeInHierarchy)
                    list.Add(actors[i].transform);
            return list;
        }

        static void AssertActorFeetOnSurface()
        {
            var actors = ActorTargets();
            // **0이면 실패** — 재부모로 대상이 조용히 사라지는 것이 이번 사고의 본질이다.
            if (actors.Count == 0)
                throw new InvalidOperationException("액터를 한 명도 못 찾았습니다 — 잰 것이 없습니다(0이면 실패).");
            var offenders = new List<(string Name, float Dy, string What)>();
            int measured = 0;
            for (int i = 0; i < actors.Count; i++)
            {
                // **몸으로 잰다** — 무기·망토를 넣으면 「발」이 칼끝이 된다(검수 의심 3을 재다 드러난 구멍).
                if (!GroundFit.BodyBounds(actors[i], out Bounds b))
                {
                    // **못 잰 것을 조용히 넘기지 않는다** — 통과와 미검사가 로그에서 같아 보이면 그게 빈 통과다.
                    Debug.Log("[Ulon]   발 미검사 " + actors[i].name + " — 보이는 메시가 없다");
                    continue;
                }
                if (!GroundFit.SurfaceUnder(actors[i], b, out float sy, out string what))
                {
                    offenders.Add((actors[i].name, float.NaN, "발 밑에 바닥이 없다"));
                    continue;
                }
                measured++;
                float dy = b.min.y - sy;
                if (Mathf.Abs(dy) > ActorFootErrorMax)
                    offenders.Add((actors[i].name, dy, what));
            }
            Debug.Log("[Ulon] 액터 발-표면 — 대상 " + actors.Count + "명(잰 것 " + measured + "명), 오차 " +
                      ActorFootErrorMax + "m 초과 " + offenders.Count + "명");
            for (int i = 0; i < offenders.Count && i < 10; i++)
                Debug.Log("[Ulon]   발 이탈 " + offenders[i].Name + " " +
                          (float.IsNaN(offenders[i].Dy) ? "" : offenders[i].Dy.ToString("0.00") + "m ") + offenders[i].What);
            if (offenders.Count > 0)
                throw new InvalidOperationException("액터 " + offenders.Count + "명의 발이 바로 아래 표면에서 " +
                    ActorFootErrorMax + "m 넘게 떨어져 있습니다(" + offenders[0].Name + "). 재부모된 NPC도 대상이다.");
        }

        /// <summary>네거티브 컨트롤 — **실제로 서비스 NPC 하나를 0.5m 띄워** 빨간불을 본 뒤 되돌린다.</summary>
        static void AssertActorFeetNegativeControl()
        {
            var victim = GameObject.Find("HealerNpc") ?? GameObject.Find("VendorNpc");
            if (victim == null)
            {
                var all = ActorTargets();
                victim = all.Count > 0 ? all[0].gameObject : null;
            }
            if (victim == null)
                throw new InvalidOperationException("액터 발 네거티브 컨트롤 대상이 없습니다.");
            var saved = victim.transform.position;
            bool red = false;
            try
            {
                victim.transform.position = saved + Vector3.up * 0.5f;
                Physics.SyncTransforms();
                try { AssertActorFeetOnSurface(); }
                catch (InvalidOperationException) { red = true; }
            }
            finally
            {
                victim.transform.position = saved;
                Physics.SyncTransforms();
            }
            if (!red)
                throw new InvalidOperationException("액터 발 네거티브 컨트롤 실패 — " + victim.name + "을 0.5m 띄웠는데 통과했습니다.");
            Debug.Log("[Ulon] 액터 발 네거티브 컨트롤 통과 — " + victim.name + " 0.5m 상승 시 FAIL(재부모된 NPC도 잡힌다)");
        }

        /// <summary>
        /// **망토는 보스만**(검수 판정 2026-09-07 ②). b2905b55에서 「보스 전용 표식」으로 정해 놓고
        /// `DressMob`(잡몹)에만 걸어 뒀더니, 이번 랩에 세운 치유사(Knight)가 망토를 걸치고 나왔다 —
        /// 문서에만 있는 약속은 샌다. 대상은 **씬 액터 전수**, 보스 판정은 `MobCatalog.IsBoss`.
        /// </summary>
        static void AssertCapeIsBossOnly()
        {
            var actors = ActorTargets();
            if (actors.Count == 0)
                throw new InvalidOperationException("액터를 한 명도 못 찾았습니다 — 잰 것이 없습니다(0이면 실패).");
            var wearers = new List<string>();
            var offenders = new List<string>();
            for (int i = 0; i < actors.Count; i++)
            {
                if (!HasVisibleCape(actors[i].gameObject))
                    continue;
                var wb = actors[i].GetComponent<WorldBody>();
                bool boss = wb != null && !string.IsNullOrEmpty(wb.MobId) && MobCatalog.IsBoss(wb.MobId);
                wearers.Add(actors[i].name + (boss ? "(보스)" : ""));
                if (!boss)
                    offenders.Add(actors[i].name);
            }
            Debug.Log("[Ulon] 망토 표식 — 액터 " + actors.Count + "명 중 망토 " + wearers.Count + "명: " +
                      (wearers.Count == 0 ? "없음" : string.Join(", ", wearers)));
            if (offenders.Count > 0)
                throw new InvalidOperationException("보스가 아닌 " + offenders.Count + "명이 망토를 걸쳤습니다(" +
                    string.Join(", ", offenders) + ") — 붉은 망토는 §10.2 보스 표식이다(b2905b55). 표식이 새면 대비가 무너진다.");
        }

        /// <summary>네거티브 컨트롤 — 잡몹에게 망토를 실제로 켜서 빨간불을 본 뒤 되돌린다.</summary>
        static void AssertCapeIsBossOnlyNegativeControl()
        {
            var actors = ActorTargets();
            Transform victim = null;
            Transform cape = null;
            for (int i = 0; i < actors.Count && victim == null; i++)
            {
                var wb = actors[i].GetComponent<WorldBody>();
                if (wb != null && !string.IsNullOrEmpty(wb.MobId) && MobCatalog.IsBoss(wb.MobId))
                    continue;                                   // 보스는 켜도 정상이라 NC가 안 된다
                var all = actors[i].GetComponentsInChildren<Transform>(true);
                for (int k = 0; k < all.Length; k++)
                    if (VisualSliceBuilder.IsCapeName(all[k].name) && !all[k].gameObject.activeSelf)
                    {
                        victim = actors[i];
                        cape = all[k];
                        break;
                    }
            }
            if (cape == null)
                throw new InvalidOperationException("망토 네거티브 컨트롤 대상이 없습니다 — 꺼 둔 망토를 가진 비(非)보스가 없습니다.");

            // **결함을 진짜로 만들어야 NC다** — 오브젝트만 켜면 조상이 꺼져 있거나 렌더러가 꺼진 경우
            // 화면엔 여전히 망토가 없고, 게이트는 「없다」고 옳게 답한다(그러면 NC가 자기 실수를 게이트 탓으로 적는다).
            // 그래서 게이트가 보는 성질(activeInHierarchy && renderer.enabled)이 실제로 참이 될 때까지 켜고,
            // 켠 것을 전부 기억해 되돌린다.
            var turnedOn = new List<GameObject>();
            var enabledRenderers = new List<Renderer>();
            for (var p = cape; p != null && p != victim.parent; p = p.parent)
                if (!p.gameObject.activeSelf) { p.gameObject.SetActive(true); turnedOn.Add(p.gameObject); }
            var capeRends = cape.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < capeRends.Length; i++)
                if (!capeRends[i].enabled) { capeRends[i].enabled = true; enabledRenderers.Add(capeRends[i]); }
            if (!HasVisibleCape(victim.gameObject))
                throw new InvalidOperationException("망토 네거티브 컨트롤 실패 — " + victim.name +
                    "의 망토를 켰는데도 「보이는 망토」가 아닙니다. 결함을 못 만들었으니 게이트 판정이 아니라 NC가 틀렸다.");

            bool red = false;
            try { AssertCapeIsBossOnly(); }
            catch (InvalidOperationException) { red = true; }
            for (int i = 0; i < enabledRenderers.Count; i++)
                enabledRenderers[i].enabled = false;
            for (int i = 0; i < turnedOn.Count; i++)
                turnedOn[i].SetActive(false);
            if (!red)
                throw new InvalidOperationException("망토 네거티브 컨트롤 실패 — " + victim.name + "에게 망토를 켰는데 통과했습니다.");
            Debug.Log("[Ulon] 망토 표식 네거티브 컨트롤 통과 — " + victim.name + "에게 망토를 켜자 FAIL");
        }
    }
}
