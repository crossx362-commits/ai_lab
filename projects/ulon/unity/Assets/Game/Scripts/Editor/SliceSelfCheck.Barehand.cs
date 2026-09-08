using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **원장이 「맨손」이라고 적은 사람은 정말 맨손인가**(2026-09-08, 후드 도적 도입에서 드러남).
    ///
    /// 발단: 치유사를 판금(Knight)에서 후드 로브(RogueHooded)로 바꿨더니 화면에서 **단검을 들고**
    /// 서 있었다. 끄는 코드는 있었지만 그 코드가 「장비 이름」 목록으로 고르는데, 새 모델의 칼은
    /// `Knife`·`Knife_Offhand`라 목록에 없어 **그냥 켜진 채로 지나갔다**.
    /// 이름 목록은 모델을 받을 때마다 새는 자다 — 그래서 이름을 늘리는 것으로 끝내지 않고,
    /// **화면에 켜진 것이 있는지 실물로 재는 자**를 따로 둔다.
    ///
    /// 재는 것: `VisualSliceBuilder.VillagerSpecs`(세우는 자와 같은 원장)의 각 사람에 대해
    /// **켜져 있는 장비 렌더러 이름 집합** = {원장이 적은 것} 인가. 맨손이면 공집합이어야 한다.
    /// 예외 선언: 없음(마을 5역할 전수).
    /// </summary>
    public static partial class SliceSelfCheck
    {
        static List<string> GearOnNames(GameObject who)
        {
            var on = new List<string>();
            foreach (var r in who.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy)
                    continue;
                if (VisualSliceBuilder.IsGearName(r.gameObject.name))
                    on.Add(r.gameObject.name);
            }
            return on;
        }

        static void AssertBarehandVillagers()
        {
            var people = VillagerLook.Villagers();
            if (people.Count == 0)
                throw new InvalidOperationException("마을 사람을 하나도 못 찾았습니다 — 잰 것이 없습니다(0이면 실패).");
            var specs = VisualSliceBuilder.VillagerSpecs;
            var bad = new List<string>();
            var lines = new List<string>();
            int looked = 0;
            for (int s = 0; s < specs.Length; s++)
            {
                GameObject who = null;
                for (int i = 0; i < people.Count; i++)
                    if (VillagerLook.HostOf(people[i]) == specs[s].Host)
                        who = people[i];
                if (who == null)
                    continue;
                looked++;
                var on = GearOnNames(who);
                string want = specs[s].Gear;
                bool ok = want == "" ? on.Count == 0 : on.Count == 1 && on[0] == want;
                lines.Add(specs[s].Host + "=" + (on.Count == 0 ? "맨손" : string.Join("+", on)));
                if (!ok)
                    bad.Add(specs[s].Host + " 원장 「" + (want == "" ? "맨손" : want) + "」인데 화면엔 " +
                            (on.Count == 0 ? "맨손" : string.Join("+", on)));
            }
            if (looked != specs.Length)
                throw new InvalidOperationException("마을 사람 " + looked + "/" + specs.Length +
                    "명만 찾았습니다 — 원장의 사람이 씬에 없습니다(0이면 실패와 같은 이유).");
            Debug.Log("[Ulon] 마을 사람 손 — " + string.Join(" · ", lines));
            if (bad.Count > 0)
                throw new InvalidOperationException("원장과 손이 다릅니다: " + string.Join(", ", bad) +
                    " — 새 모델은 장비 노드 이름이 다릅니다(`ContainsGearName`을 확인하십시오). §8.1");
        }

        /// <summary>NC — 맨손이어야 할 사람 손에 **실제로 장비를 켜면** 빨간불이어야 한다.</summary>
        static void AssertBarehandVillagersNegativeControl()
        {
            var people = VillagerLook.Villagers();
            GameObject victim = null;
            for (int i = 0; i < people.Count && victim == null; i++)
                if (VillagerLook.HostOf(people[i]) == "Healer")
                    victim = people[i];
            if (victim == null)
                throw new InvalidOperationException("맨손 NC 대상(치유사)이 없습니다 — 잰 것이 없습니다(0이면 실패).");
            Transform gear = null;
            foreach (var t in victim.GetComponentsInChildren<Transform>(true))
                if (VisualSliceBuilder.IsGearName(t.name) && t.GetComponent<Renderer>() != null)
                {
                    gear = t;
                    break;
                }
            if (gear == null)
                throw new InvalidOperationException("맨손 NC용 장비 노드를 못 찾았습니다 — 이 모델엔 장비가 없습니다.");
            bool saved = gear.gameObject.activeSelf;
            bool red = false;
            try
            {
                gear.gameObject.SetActive(true);
                try { AssertBarehandVillagers(); }
                catch (InvalidOperationException) { red = true; }
            }
            finally { gear.gameObject.SetActive(saved); }
            if (!red)
                throw new InvalidOperationException("맨손 네거티브 컨트롤 실패 — 치유사 손에 " + gear.name +
                    "을 켰는데 통과했습니다.");
            Debug.Log("[Ulon] 맨손 네거티브 컨트롤 통과 — 치유사 손에 " + gear.name + "을 켜면 FAIL");
        }
    }
}
