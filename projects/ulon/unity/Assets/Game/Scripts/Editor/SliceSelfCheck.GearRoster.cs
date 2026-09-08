using System;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **장비를 이름표로만 찾지 않는가**(랩 ①, 2026-09-08 — 배우 명단(랩 ③)과 같은 성질의 자리).
    ///
    /// `ContainsGearName`은 Sword·Shield·Axe… 열두 단어짜리 **이름 원장**이다. 모델을 받을 때마다 샌다:
    /// 후드 도적의 칼이 `Knife`라 목록 밖이었고, 「맨손」이어야 할 치유사가 칼을 든 채 화면에 섰다
    /// (`48_person_Healer` 실측). 그때 수리는 **단어를 두 개 더 적는 것**이었다 — 다음 팩이
    /// `Throwable`·`Spellbook`을 들고 오면 그대로 다시 샌다.
    ///
    /// 그래서 자를 하나 더 세웠다: **손뼈(`hand*`·`handslot*`) 밑에 매달린 비스킨드 렌더러는
    /// 이름이 무엇이든 든 것이다**(`VisualSliceBuilder.GearOnActor`). 이 게이트가 그 성질을 지킨다.
    ///
    /// 양방향 NC: 아무 원장에도 없는 이름(`SelfcheckGearProbe`)을 손뼈 밑에 하나 달면 **잡아야 하고**(①),
    /// 그것을 손 밖(액터 루트)으로 옮기면 **안 잡아야 한다**(② — 자가 아무나 잡으면 빈 통과다).
    /// 되돌리는 것까지 확인하고, 잰 뒤 장비 수가 그대로인지도 본다 —
    /// **계측이 세계를 바꾸면 그 판의 초록도 빨강도 못 믿는다**(랩 ③에서 실제로 겪었다).
    /// </summary>
    public static partial class SliceSelfCheck
    {
        const string GearProbeName = "SelfcheckGearProbe";

        static void AssertGearFoundByPlace()
        {
            var who = GameObject.Find("Player");
            if (who == null)
                throw new InvalidOperationException("플레이어를 못 찾았습니다 — 잰 것이 없습니다(0이면 실패).");
            Transform hand = null;
            foreach (var t in who.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("handslot", StringComparison.OrdinalIgnoreCase))
                { hand = t; break; }
            if (hand == null)
                foreach (var t in who.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith("hand", StringComparison.OrdinalIgnoreCase))
                    { hand = t; break; }
            if (hand == null)
                throw new InvalidOperationException("플레이어의 손뼈를 못 찾았습니다 — 자리로 재는 자가 설 자리가 없습니다.");

            int before = VisualSliceBuilder.GearOnActor(who).Count;
            if (before == 0)
                throw new InvalidOperationException("플레이어가 걸친 장비가 0개입니다 — 잰 것이 없습니다(0이면 실패).");

            var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                probe.name = GearProbeName;                     // 어느 이름 원장에도 없는 이름
                probe.transform.SetParent(hand, false);
                probe.transform.localScale = Vector3.one * 0.1f;
                if (!GearProbeFound(who))
                    throw new InvalidOperationException("장비 자리 NC① 실패 — 손뼈 밑에 달린 " + GearProbeName +
                        "를 장비로 못 봤습니다. 아직 이름 원장으로만 찾고 있습니다.");
                Debug.Log("[Ulon] 장비 자리 NC① 통과 — 이름 원장에 없는 " + GearProbeName +
                          "도 손에 쥐면 장비로 잡힌다(" + before + "개 → " + VisualSliceBuilder.GearOnActor(who).Count + "개)");

                probe.transform.SetParent(who.transform, false);   // 손 밖으로 — 든 것이 아니다
                if (GearProbeFound(who))
                    throw new InvalidOperationException("장비 자리 NC② 실패 — 손 밖으로 옮긴 " + GearProbeName +
                        "를 여전히 장비로 봤습니다. 자가 아무나 잡으면 빈 통과입니다.");
                Debug.Log("[Ulon] 장비 자리 NC② 통과 — 손 밖으로 옮기자 장비에서 빠졌다");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probe);
            }
            int after = VisualSliceBuilder.GearOnActor(who).Count;
            if (after != before)
                throw new InvalidOperationException("잰 뒤 장비 수가 " + before + "→" + after +
                    "로 달라졌습니다 — 계측이 세계를 바꿨습니다.");
            Debug.Log("[Ulon] 장비를 자리로도 찾는다 — 플레이어 장비 " + before + "개(이름 원장 + 손자리 합집합)");
        }

        static bool GearProbeFound(GameObject who)
        {
            var gear = VisualSliceBuilder.GearOnActor(who);
            for (int i = 0; i < gear.Count; i++)
                if (gear[i] != null && gear[i].name == GearProbeName)
                    return true;
            return false;
        }
    }
}
