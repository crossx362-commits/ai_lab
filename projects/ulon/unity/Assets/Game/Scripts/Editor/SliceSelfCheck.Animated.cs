using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **사람형이 게임에서 움직이는가**(검수 지시 2026-09-07 (a), 랩 ④).
    ///
    /// 스탠드얼론 실측(`tools/idle_check.sh`)에서 훈련사·스켈레톤이 **애니메이터 없이** 서 있었다.
    /// 런타임 `CharacterAnim.StripEmptyAnimators`가 컨트롤러 없는 애니메이터를 지우는데 그 액터에는
    /// 컨트롤러 달린 애니메이터가 하나도 없어 아무것도 안 남았다 — **T포즈가 곧 플레이 화면**이었다.
    ///
    /// 이 게이트는 편집기에서 「컨트롤러가 달려 있는가」를 전수로 본다. 그것만으로 「움직인다」를
    /// 보증하진 못한다(대리 지표의 한계) — 그래서 **실행 중 실측은 `idle_check.sh`가 따로 남기고**,
    /// 여기서는 그 실측이 잡아낸 원인(컨트롤러 부재)이 다시 새는 것을 막는다. 두 개를 함께 읽어라.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        static void AssertActorsAnimated()
        {
            var actors = UnityEngine.Object.FindObjectsByType<CharacterController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (actors.Length == 0)
                throw new InvalidOperationException("사람형이 없습니다 — 잰 것이 없습니다(0이면 실패).");
            var dead = new List<string>();
            for (int i = 0; i < actors.Length; i++)
            {
                var anim = actors[i].GetComponentInChildren<Animator>(true);
                if (anim == null || anim.runtimeAnimatorController == null)
                    dead.Add(actors[i].name + (anim == null ? "(애니메이터 없음)" : "(컨트롤러 없음)"));
            }
            if (dead.Count > 0)
                throw new InvalidOperationException("애니메이션이 안 도는 사람형 " + dead.Count + "체: " +
                    string.Join(", ", dead) + " — 런타임이 컨트롤러 없는 애니메이터를 지우므로 이 액터들은 " +
                    "**게임에서 T포즈로 선다**. `VisualSliceBuilder.EnsureActorAnimators`가 전수로 보정해야 합니다.");
            Debug.Log("[Ulon] 액터 애니메이션 — 사람형 " + actors.Length + "체 전수, 컨트롤러 달린 애니메이터 있음");
        }

        /// <summary>NC — 컨트롤러를 **실제로 떼면** 빨간불이어야 한다.</summary>
        static void AssertActorsAnimatedNegativeControl()
        {
            var victim = GameObject.Find("Trainer");     // 실측에서 실제로 멈춰 있던 액터
            var anim = victim != null ? victim.GetComponentInChildren<Animator>(true) : null;
            if (anim == null)
                throw new InvalidOperationException("애니메이션 NC 대상(훈련사 애니메이터)이 없습니다 — 잰 것이 없습니다(0이면 실패).");
            var keep = anim.runtimeAnimatorController;
            if (keep == null)
                throw new InvalidOperationException("애니메이션 NC가 결함을 못 만들었습니다 — 이미 컨트롤러가 없습니다.");
            bool red = false;
            string message = "";
            try
            {
                anim.runtimeAnimatorController = null;
                try { AssertActorsAnimated(); }
                catch (InvalidOperationException e) { red = true; message = e.Message; }
            }
            finally { anim.runtimeAnimatorController = keep; }
            if (!red)
                throw new InvalidOperationException("애니메이션 네거티브 컨트롤 실패 — 컨트롤러를 뗐는데 통과했습니다.");
            Debug.Log("[Ulon] 액터 애니메이션 네거티브 컨트롤 통과 — 컨트롤러를 떼면 FAIL: " + message);
        }
    }
}
