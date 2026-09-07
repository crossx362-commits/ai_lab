using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **장비는 바닥을 뚫지 않는다**(검수 판정 2026-09-07 2: 「41 IronTyrant의 검 끝이 바닥을 뚫는다」).
    ///
    /// 무기 그립 랩과 같은 계열의 구멍이다 — 장비를 **캐릭터 기준으로만** 맞추고 **바닥과의 관계는
    /// 아무도 안 봤다**. 보스는 1.3~1.5배로 커지고 무기는 그 위에 또 1.55~3.5배로 늘어나니,
    /// 손에 제대로 들려 있어도 칼끝이 바닥 밑으로 내려간다. 「스케일 뒤 재착지」와 같은 원인의 다른 얼굴이다.
    ///
    /// 판정은 **칼날 각도가 아니라 최저점**이다 — 각도 게이트는 앞서 안 걸기로 한 결정이 있다.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        /// <summary>바닥 아래로 허용하는 깊이 — 접지 오차(0.05m) 수준. 그 아래는 화면에서 「박혔다」로 읽힌다.</summary>
        const float GearBelowFloorMax = 0.05f;

        static List<Renderer> WeaponRenderers(Transform actor)
        {
            var list = new List<Renderer>();
            var rends = actor.GetComponentsInChildren<Renderer>(false);
            for (int i = 0; i < rends.Length; i++)
            {
                if (rends[i] is ParticleSystemRenderer || !rends[i].enabled)
                    continue;
                string own = rends[i].gameObject.name;
                string parent = rends[i].transform.parent != null ? rends[i].transform.parent.name : "";
                if (VisualSliceBuilder.IsWeaponName(own) || VisualSliceBuilder.IsWeaponName(parent))
                    list.Add(rends[i]);
            }
            return list;
        }

        /// <summary>이 액터가 서 있는 바닥 높이 — 발 게이트와 **같은 자**를 쓴다.</summary>
        static bool FloorUnderActor(Transform actor, out float y)
        {
            y = 0f;
            if (!GroundFit.BodyBounds(actor, out Bounds b))
                return false;
            return GroundFit.SurfaceUnder(actor, b, out y, out _);
        }

        static void AssertGearAboveFloor()
        {
            var actors = ActorTargets();
            if (actors.Count == 0)
                throw new InvalidOperationException("액터를 한 명도 못 찾았습니다 — 잰 것이 없습니다(0이면 실패).");
            int measured = 0, armed = 0;
            var offenders = new List<(string Who, string What, float Below)>();
            for (int i = 0; i < actors.Count; i++)
            {
                var weapons = WeaponRenderers(actors[i]);
                if (weapons.Count == 0)
                    continue;
                armed++;
                if (!FloorUnderActor(actors[i], out float floorY))
                {
                    Debug.Log("[Ulon]   장비 미검사 " + actors[i].name + " — 발 밑에 바닥이 없다");
                    continue;
                }
                for (int k = 0; k < weapons.Count; k++)
                {
                    measured++;
                    float below = floorY - weapons[k].bounds.min.y;      // 양수 = 바닥 아래로 내려간 깊이
                    // **통과도 값으로 남긴다** — 「0건」만 찍으면 통과와 미검사가 로그에서 같아 보인다.
                    Debug.Log("[Ulon]   장비 여유 " + actors[i].name + "/" + weapons[k].gameObject.name + " " +
                              (-below).ToString("0.00") + "m");
                    if (below > GearBelowFloorMax)
                        offenders.Add((actors[i].name, weapons[k].gameObject.name, below));
                }
            }
            Debug.Log("[Ulon] 장비-바닥 — 무장 액터 " + armed + "명, 잰 장비 " + measured + "개, 바닥 아래로 " +
                      GearBelowFloorMax + "m 넘게 내려간 것 " + offenders.Count + "개");
            if (measured == 0)
                throw new InvalidOperationException("잰 장비가 0개입니다 — 무기 이름 규칙이 죽었습니다(0이면 실패).");
            for (int i = 0; i < offenders.Count && i < 10; i++)
                Debug.Log("[Ulon]   장비 매몰 " + offenders[i].Who + "/" + offenders[i].What + " " +
                          offenders[i].Below.ToString("0.00") + "m");
            if (offenders.Count > 0)
                throw new InvalidOperationException("장비 " + offenders.Count + "개가 바닥을 뚫고 내려갔습니다(" +
                    offenders[0].Who + "/" + offenders[0].What + " " + offenders[0].Below.ToString("0.00") +
                    "m) — 장비를 캐릭터 기준으로만 맞추면 바닥과의 관계는 아무도 안 본다.");
        }

        /// <summary>네거티브 컨트롤 — 무기를 **실제로 0.5m 내려** 빨간불을 본 뒤 되돌린다.</summary>
        static void AssertGearAboveFloorNegativeControl()
        {
            var actors = ActorTargets();
            Renderer victim = null;
            float floor = 0f;
            for (int i = 0; i < actors.Count && victim == null; i++)
            {
                var w = WeaponRenderers(actors[i]);
                if (w.Count > 0 && FloorUnderActor(actors[i], out floor))
                    victim = w[0];
            }
            if (victim == null)
                throw new InvalidOperationException("장비-바닥 네거티브 컨트롤 대상이 없습니다 — 바닥 위에 서서 무기를 든 액터가 없습니다.");
            var t = victim.transform;
            var saved = t.position;
            bool red = false;
            try
            {
                // **결함을 실제로 만든다** — 「0.5m 내린다」는 무기가 원래 1.5m 높이면 아무것도 안 만든다
                // (첫 시도가 그랬다: 게이트는 옳게 통과했는데 NC가 자기 실패를 게이트 탓으로 적었다).
                // 최저점이 바닥보다 0.5m 아래로 가도록 내린다.
                float drop = (victim.bounds.min.y - floor) + 0.5f;
                t.position = saved - Vector3.up * drop;
                try { AssertGearAboveFloor(); }
                catch (InvalidOperationException) { red = true; }
            }
            finally { t.position = saved; }
            if (!red)
                throw new InvalidOperationException("장비-바닥 네거티브 컨트롤 실패 — " + victim.name +
                    "의 최저점을 바닥 아래 0.5m로 내렸는데 통과했습니다.");
            Debug.Log("[Ulon] 장비-바닥 네거티브 컨트롤 통과 — " + victim.name + "의 최저점을 바닥 아래 0.5m로 내리자 FAIL");
        }
    }
}
