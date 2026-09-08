using System;
using System.Collections.Generic;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **무기를 쥔 것은 보스만이 아니다** — 보스에만 걸려 있던 물림 자 셋을 일반 배우까지 넓힌다
    /// (검수 지시 2026-09-09). 셋은 보스 자(`SliceSelfCheck.BossTraits`)에서 그대로 물려받는다:
    ///   ㉠ 무기가 **손뼈 아래**에 달려 있는가(킷이 쥐라고 만든 자리)
    ///   ㉡ **손 점이 무기 덩어리 안**인가(가까이 둔 것과 쥔 것은 다르다)
    ///   ㉢ 먼 끝이 **몸통 기둥 밖**인가(안이면 몸에 가로로 꽂힌 막대로 읽힌다)
    /// 한도도 물려받는다 — 안 맞으면 **한도를 늘리지 말고 왜 다른지를 적는다**(검수 조건).
    /// </summary>
    public static partial class SliceSelfCheck
    {
        /// <summary>보스는 `AssertBossTraits`가 이미 잰다 — 두 자가 같은 것을 재면 판정이 갈린다.</summary>
        static bool IsBossActor(string name) =>
            name == Dungeon1.BossObject || name == Dungeon2.BossObject ||
            name == Dungeon3.BossObject || name == FieldBoss.Object;

        static void AssertActorWeaponsHeld()
        {
            var actors = ActorTargets();
            if (actors.Count == 0)
                throw new InvalidOperationException("액터를 한 명도 못 찾았습니다 — 잰 것이 없습니다(0이면 실패).");

            int armed = 0;
            var bad = new List<string>();
            var lines = new List<string>();
            for (int i = 0; i < actors.Count; i++)
            {
                var go = actors[i];
                if (go == null || IsBossActor(go.name))
                    continue;
                var weapons = WeaponRenderers(go.transform);
                if (weapons.Count == 0)
                    continue;
                var cc = go.GetComponent<CharacterController>();
                if (cc == null)
                    continue;
                armed++;
                float bodyH = cc.height * Mathf.Max(go.transform.lossyScale.y, 0.01f);
                var hand = VisualSliceBuilder.FindHandBone(go.gameObject);
                if (hand == null)
                {
                    bad.Add(go.name + " 손 본이 없다");
                    continue;
                }
                var weaponT = weapons[0].transform;
                bool underHand = false;
                for (var t = weaponT; t != null && !underHand; t = t.parent)
                    underHand = t == hand;

                Bounds wb = weapons[0].bounds;
                for (int w = 1; w < weapons.Count; w++)
                    wb.Encapsulate(weapons[w].bounds);
                var padded = wb;
                padded.Expand(bodyH * WeaponGripPadRatio);
                bool held = padded.Contains(hand.position);

                float scale = Mathf.Max(go.transform.lossyScale.x, go.transform.lossyScale.z);
                float bodyR = cc.radius * scale;
                Vector3 axis = go.transform.TransformPoint(cc.center);
                float tipOut = float.PositiveInfinity;
                if (BossFit.WeaponAxis(weaponT, out Vector3 grip, out Vector3 tip))
                    tipOut = new Vector2(tip.x - axis.x, tip.z - axis.z).magnitude;

                lines.Add(go.name + "(" + weaponT.name + ") 손뼈아래 " + (underHand ? "O" : "X") +
                          "·손물림 " + (held ? "O" : "X") + "·날끝 " + tipOut.ToString("0.00") +
                          "m/반경 " + bodyR.ToString("0.00") + "m");
                if (!underHand)
                    bad.Add(go.name + " 무기가 손뼈(" + hand.name + ") 아래에 없다 — 옆에 세워 둔 것은 쥔 것이 아니다");
                else if (!held)
                    bad.Add(go.name + " 손이 무기 덩어리 밖이다(덩어리 " + wb.size.ToString("0.0") +
                            ", 손 " + hand.position.ToString("0.0") + ")");
                else if (tipOut < bodyR)
                    bad.Add(go.name + " 무기 날 끝이 몸통 기둥 안이다(중심에서 " + tipOut.ToString("0.00") +
                            "m < 반경 " + bodyR.ToString("0.00") + "m)");
            }
            if (armed == 0)
                throw new InvalidOperationException("무기를 든 일반 배우가 하나도 없습니다 — 잰 것이 없습니다(0이면 실패).");
            Debug.Log("[Ulon] 배우 무기 물림 — 무장 " + armed + "명: " + string.Join(" | ", lines));
            if (bad.Count > 0)
                throw new InvalidOperationException("배우가 무기를 쥐고 있지 않습니다 — " + bad.Count + "명:\n  " +
                    string.Join("\n  ", bad) + "\n보스에 걸린 자와 같은 자입니다(§8.1).");
        }

        /// <summary>
        /// 양방향 NC — 무기 하나를 몸에서 떼어 옆으로 밀면 빨간불, 되돌리면 초록.
        /// (손뼈 아래에서 빼내면 ㉠이, 그대로 두고 멀리 밀면 ㉡이 문다.)
        /// </summary>
        static void AssertActorWeaponsHeldNegativeControl()
        {
            var actors = ActorTargets();
            Transform victim = null;
            for (int i = 0; i < actors.Count && victim == null; i++)
            {
                var go = actors[i];
                if (go == null || IsBossActor(go.name) || go.GetComponent<CharacterController>() == null)
                    continue;
                var w = WeaponRenderers(go.transform);
                if (w.Count > 0)
                    victim = w[0].transform;
            }
            if (victim == null)
                throw new InvalidOperationException("배우 무기 NC 대상이 없습니다(0이면 실패).");

            var keepParent = victim.parent;
            var keepPos = victim.position;
            var keepRot = victim.rotation;
            bool red;
            try
            {
                victim.SetParent(keepParent != null ? keepParent.root : null, true);
                victim.position = keepPos + new Vector3(3f, 0f, 0f);
                red = ActorWeaponsHeldFails();
            }
            finally
            {
                victim.SetParent(keepParent, true);
                victim.SetPositionAndRotation(keepPos, keepRot);
            }
            if (!red)
                throw new InvalidOperationException("배우 무기 네거티브 컨트롤 실패 — 무기를 손에서 떼어 3m 옆에 놓았는데 통과했습니다.");
            if (ActorWeaponsHeldFails())
                throw new InvalidOperationException("배우 무기 네거티브 컨트롤 실패 — 되돌렸는데도 빨간불입니다.");
            Debug.Log("[Ulon] 배우 무기 물림 양방향 NC 통과 — 손에서 떼면 FAIL · 되돌리면 다시 통과");
        }

        static bool ActorWeaponsHeldFails()
        {
            try
            {
                AssertActorWeaponsHeld();
                return false;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        }
    }
}
