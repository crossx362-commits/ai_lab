using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **머리에 쓴 것·손에 쥔 방패를 이름으로 고르지 않는가**(랩 ②, 2026-09-09).
    ///
    /// 두 자가 이름 원장이었다: 머리 장식은 `*Hat*` 부분일치, 방패는 정확히 `Round_Shield`.
    /// 둘 다 **고르는 자**라 새면 조용하다 — 모자를 놓치면 그 사람의 키가 모자만큼 부풀고
    /// (키 측정에서 빼는 것이 이 자의 일이다), 방패를 잘못 고르면 등에 멘 장식만 남고 손이 빈다.
    /// 새 팩이 `Helmet`·`Hood`·`Kite_Shield`로 오는 순간 둘 다 샌다.
    ///
    /// 그래서 **이름 말고 다른 것으로 재는 자를 하나 더 세웠다**(랩 ①·③과 같은 축):
    /// 머리 장식 = **스킨드가 아닌 메시**가 **몸 위쪽 80% 위**에 앉은 것(`GroundFit.HeadgearByPlace`),
    /// 방패 = **손뼈 아래에 있는 것**이 먼저, 그다음 큰 것(`PickShield`).
    ///
    /// 이 게이트는 그 두 자를 **양방향으로** 흔든다. 이름에 없는 탐침을 머리 위에 얹으면 잡아야 하고
    /// 허리로 내리면 놓아야 한다. 방패는 손에 쥔 쪽을 골라야 하고, 손에서 떼면 다른 쪽을 골라야 한다.
    /// 잰 뒤 세계가 그대로인지도 본다 — 계측이 세계를 바꾸면 그 판의 초록도 빨강도 못 믿는다.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        const string HeadgearProbeName = "SelfcheckHeadProbe";
        const string ShieldProbeName = "SelfcheckShieldProbe";

        static void AssertHeadgearFoundByPlace()
        {
            var who = ProbeActor();
            if (!GroundFit.PersonBounds(who.transform, out Bounds body) || body.size.y < 0.01f)
                throw new InvalidOperationException("사람 몸을 못 쟀습니다 — 잰 것이 없습니다(0이면 실패).");

            var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                probe.name = HeadgearProbeName;                 // 이름 원장에 없는 이름(모자가 아니다)
                probe.transform.SetParent(who.transform, true);
                probe.transform.localScale = Vector3.one * 0.15f;
                probe.transform.position = new Vector3(body.center.x, body.max.y + 0.05f, body.center.z);
                if (!GroundFit.HeadgearByPlace(who.transform, probe.transform))
                    throw new InvalidOperationException("머리 장식 NC① 실패 — 정수리 위에 얹은 " + HeadgearProbeName +
                        "를 쓴 것으로 못 봤습니다. 아직 이름으로만 고르고 있습니다.");

                probe.transform.position = new Vector3(body.center.x, body.min.y + body.size.y * 0.4f, body.center.z);
                if (GroundFit.HeadgearByPlace(who.transform, probe.transform))
                    throw new InvalidOperationException("머리 장식 NC② 실패 — 허리 높이로 내린 " + HeadgearProbeName +
                        "를 여전히 쓴 것으로 봤습니다. 자가 아무나 잡으면 빈 통과입니다.");
                Debug.Log("[Ulon] 머리 장식 자리 양방향 NC 통과 — 이름 밖 물건도 정수리 위면 잡고(" +
                          body.max.y.ToString("0.00") + "m), 허리로 내리면 놓는다");
            }
            finally { UnityEngine.Object.DestroyImmediate(probe); }

            if (GameObject.Find(HeadgearProbeName) != null)
                throw new InvalidOperationException("머리 장식 탐침이 씬에 남았습니다 — 계측이 세계를 바꿨습니다.");
        }

        static void AssertShieldPickedByPlace()
        {
            var who = ProbeActor();
            Transform hand = HandBoneOf(who);
            if (hand == null)
                throw new InvalidOperationException("손뼈를 못 찾았습니다 — 자리로 재는 자가 설 자리가 없습니다.");

            var held = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var slung = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                held.name = ShieldProbeName + "Held";           // 작지만 **손에 쥔** 것
                held.transform.SetParent(hand, false);
                held.transform.localScale = Vector3.one * 0.1f;
                slung.name = "Round_Shield";                    // 이름 원장이 좋아하던 이름, 그러나 손 밖
                slung.transform.SetParent(who.transform, false);
                slung.transform.localScale = Vector3.one * 0.4f;

                var list = new List<Transform> { slung.transform, held.transform };
                var picked = VisualSliceBuilder.PickShieldForCheck(list);
                if (picked != held.transform)
                    throw new InvalidOperationException("방패 자리 NC① 실패 — 손에 쥔 것 대신 " +
                        (picked != null ? picked.name : "없음") + "를 골랐습니다. 아직 이름으로 고르고 있습니다.");

                held.transform.SetParent(who.transform, true);  // 손에서 뗀다 = 등에 멘 것과 같은 상태
                picked = VisualSliceBuilder.PickShieldForCheck(list);
                if (picked != slung.transform)
                    throw new InvalidOperationException("방패 자리 NC② 실패 — 손에서 뗐는데도 " +
                        (picked != null ? picked.name : "없음") + "를 골랐습니다. 자가 자리를 안 봅니다.");
                Debug.Log("[Ulon] 방패 자리 양방향 NC 통과 — 손에 쥔 쪽을 고르고(이름은 반대편에 있었다), 손에서 떼면 큰 쪽을 고른다");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(held);
                UnityEngine.Object.DestroyImmediate(slung);
            }
            if (GameObject.Find(ShieldProbeName + "Held") != null)
                throw new InvalidOperationException("방패 탐침이 씬에 남았습니다 — 계측이 세계를 바꿨습니다.");
        }

        static GameObject ProbeActor()
        {
            var who = GameObject.Find("Player");
            if (who == null || who.GetComponent<CharacterController>() == null)
                throw new InvalidOperationException("플레이어를 못 찾았습니다 — 잰 것이 없습니다(0이면 실패).");
            return who;
        }

        static Transform HandBoneOf(GameObject who)
        {
            foreach (var t in who.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("handslot", StringComparison.OrdinalIgnoreCase))
                    return t;
            foreach (var t in who.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("hand", StringComparison.OrdinalIgnoreCase))
                    return t;
            return null;
        }
    }
}
