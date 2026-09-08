using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **그림이 충돌체와 같은 크기인가**(랩 A, 2026-09-09 — 대장 판정).
    ///
    /// 마을 축척 안건을 재다 드러났다: 사람 몸 2.63m인데 `CharacterController` 캡슐은 1.80m —
    /// **그림이 충돌체보다 46% 큰** 사람으로 「문이 사람보다 낮다」를 재고 있었다. 부풀린 자로
    /// 잰 값(2.9배)으로 마을을 키웠으면 두 오차를 곱했을 것이다.
    ///
    /// 실측하니 액터 15체 중 **플레이어만** 어긋났다(나머지 1.00). 크기를 고치는 패스가
    /// **몹 원장(`MobId`)에 묶인 것만** 손대서, 원장 밖인 플레이어는 아무도 안 봤다.
    /// 그래서 자를 원장이 아니라 **모든 배우가 가진 것**(캡슐)에 건다 — 전투 거리·카메라·충돌이
    /// 이미 캡슐을 쓰므로, 그림이 캡슐과 다르면 **화면과 규칙이 다른 세계**가 된다.
    ///
    /// 한도는 **비율**(±10%)이다. 절대 미터로 두면 캐릭터 축척을 바꾸는 순간 그 숫자만 다시 고치게 된다.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        /// <summary>몸/캡슐 허용 어긋남 — 맞추는 패스는 8%에서 손대므로 자는 그보다 넓게 잡는다.</summary>
        const float BodyCapsuleTolerance = 0.10f;

        static void AssertActorBodyMatchesCapsule()
        {
            string reason = BodyCapsuleReason(true);
            if (!string.IsNullOrEmpty(reason))
                throw new InvalidOperationException(reason);
        }

        /// <summary>빨간불 사유(없으면 빈 문자열) — NC가 같은 판정을 재사용한다(자가 둘이면 갈린다).</summary>
        static string BodyCapsuleReason(bool log)
        {
            var actors = VisualSliceBuilder.ActorsToDress();
            if (actors.Length == 0)
                return "액터를 한 명도 못 찾았습니다 — 잰 것이 없습니다(0이면 실패).";
            var offenders = new List<string>();
            var lines = new List<string>();
            int measured = 0;
            for (int i = 0; i < actors.Length; i++)
            {
                var cc = actors[i].GetComponent<CharacterController>();
                if (cc == null)
                    continue;
                float want = cc.height * actors[i].transform.lossyScale.y;
                if (want <= 0.01f || !GroundFit.PersonBounds(actors[i].transform, out Bounds b) || b.size.y < 0.01f)
                    continue;                                  // 몸을 못 재는 것은 아래 「0이면 실패」가 잡는다
                measured++;
                float ratio = b.size.y / want;
                lines.Add(actors[i].name + " " + ratio.ToString("0.00"));
                if (Mathf.Abs(ratio - 1f) > BodyCapsuleTolerance)
                    offenders.Add(actors[i].name + " 몸 " + b.size.y.ToString("0.00") + "m / 캡슐 " +
                                  want.ToString("0.00") + "m = " + ratio.ToString("0.00") + "배");
            }
            if (measured < 3)
                return "몸을 잰 액터가 " + measured + "체뿐입니다 — 잰 것이 없습니다(0이면 실패).";
            if (log)
                Debug.Log("[Ulon] 그림-충돌체 비율 — 액터 " + measured + "체(허용 ±" +
                          (BodyCapsuleTolerance * 100f).ToString("0") + "%) · " + string.Join(", ", lines));
            if (offenders.Count > 0)
                return "그림이 충돌체와 다른 액터 " + offenders.Count + "체: " + string.Join(", ", offenders) +
                       " — 전투 거리·카메라·충돌은 캡슐을 쓰므로 화면과 규칙이 갈립니다. " +
                       "`VisualSliceBuilder.EnsureActorBodyMatchesCapsule`이 전수로 맞춰야 합니다.";
            return "";
        }

        /// <summary>양방향 NC — 한 사람의 그림을 키우면 빨간불, **되돌리면 다시 초록**.</summary>
        static void AssertActorBodyMatchesCapsuleNegativeControl()
        {
            var who = GameObject.Find("Player");
            var target = who != null ? (who.transform.Find("Visual") ?? who.transform) : null;
            if (target == null)
                throw new InvalidOperationException("NC 대상(플레이어 그림)이 없습니다 — 잰 것이 없습니다(0이면 실패).");
            string before = BodyCapsuleReason(true);       // 내역을 찍고 시작한다 — 숫자 없이 빨간불만 남기지 않는다
            if (!string.IsNullOrEmpty(before))
                throw new InvalidOperationException("그림-충돌체 NC 실패 — 손대기 전부터 빨간불입니다: " + before);
            Vector3 kept = target.localScale;
            bool red;
            try
            {
                target.localScale = kept * 1.3f;
                red = !string.IsNullOrEmpty(BodyCapsuleReason(false));
            }
            finally { target.localScale = kept; }
            if (!red)
                throw new InvalidOperationException("그림-충돌체 NC 실패 — 그림을 1.3배로 키웠는데 통과했습니다. 빈 통과입니다.");
            if (!string.IsNullOrEmpty(BodyCapsuleReason(false)))
                throw new InvalidOperationException("그림-충돌체 NC 실패 — 되돌렸는데 빨간불이 남았습니다(계측이 세계를 바꿨습니다).");
            Debug.Log("[Ulon] 그림-충돌체 양방향 NC 통과 — 그림을 1.3배로 키우면 FAIL · 되돌리면 다시 통과");
        }
    }
}
