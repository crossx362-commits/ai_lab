using System;
using Ulon.Client;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **동료가 플레이어와 카메라 사이에 서지 않는가**(검수 판정 2026-09-07).
    ///
    /// 야외 시선 게이트의 「최악 축」은 사람·짐승을 뺀다 — 사유는 「움직이니까 구조적 자리가 아니다」.
    /// 그런데 **동료의 자리는 씬 상수**라 그 사유가 성립하지 않았다. 사유가 안 맞는 제외는 결함을
    /// 통째로 사각지대에 둔다. 그래서 ⓐ 그 게이트의 제외에서 동료를 빼고(`OccludedShareAt`),
    /// ⓑ 동료 자체는 **자리 규칙**으로 여기서 따로 잰다.
    ///
    /// **왜 격자 게이트만으로는 부족한가**(다음 사람이 다시 파지 않게): 사람이 어디에 서 있든
    /// **그 바로 뒤 자리**는 가려진다 — 그건 배치 결함이 아니라 사람이 서 있다는 사실이다.
    /// 그래서 격자 축은 동료 반경 2.5m 안의 표본을 빼고(같은 자리에 겹쳐 선 것은 가림이 아니다),
    /// 「동료가 시선 축 위에 있나」는 **각도**로 잰다 — 이건 항상 성립시킬 수 있는 판정이다.
    /// 처방은 투명화가 아니다(「사람·짐승은 투명해지면 더 이상하다」는 기존 판단 유지).
    /// </summary>
    public static partial class SliceSelfCheck
    {
        /// <summary>동료가 카메라→플레이어 축에서 이만큼은 벌어져 있어야 한다.</summary>
        const float CompanionAxisAngleMin = 35f;

        static float CompanionAxisAngle(out Vector3 comp, out Vector3 player)
        {
            comp = Vector3.zero;
            player = Vector3.zero;
            var c = GameObject.Find(VisualSliceBuilder.CompanionObject);
            var p = GameObject.Find("Player");
            if (c == null || p == null)
                return -1f;
            comp = c.transform.position;
            player = p.transform.position;
            var qv = UnityEngine.Object.FindFirstObjectByType<QuarterViewCamera>(FindObjectsInactive.Include);
            float pitch = qv != null ? qv.Pitch : 35f;
            float yaw = qv != null ? qv.Yaw : 45f;
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            // 플레이어에서 카메라로 가는 방향(수평 성분) — 동료가 이쪽에 서면 앞을 가린다.
            var toCam = -(rot * Vector3.forward);
            var flatCam = new Vector2(toCam.x, toCam.z).normalized;
            var flatComp = new Vector2(comp.x - player.x, comp.z - player.z);
            if (flatComp.magnitude < 0.05f)
                return 0f;
            return Vector2.Angle(flatCam, flatComp.normalized);
        }

        static void AssertCompanionOffSightAxis()
        {
            float angle = CompanionAxisAngle(out Vector3 comp, out Vector3 player);
            if (angle < 0f)
                throw new InvalidOperationException("동료나 플레이어를 못 찾았습니다 — 잰 것이 없습니다(0이면 실패).");
            float d = Vector2.Distance(new Vector2(comp.x, comp.z), new Vector2(player.x, player.z));
            Debug.Log("[Ulon] 동료 자리 — 플레이어 " + player.ToString("0.0") + ", 동료 " + comp.ToString("0.0") +
                      ", 거리 " + d.ToString("0.0") + "m, 카메라 축에서 " + angle.ToString("0") +
                      "° (하한 " + CompanionAxisAngleMin.ToString("0") + "°)");
            if (angle < CompanionAxisAngleMin)
                throw new InvalidOperationException("동료가 카메라와 플레이어 사이(축에서 " + angle.ToString("0") +
                    "°)에 서 있습니다 — 하한 " + CompanionAxisAngleMin.ToString("0") +
                    "°. 고정 쿼터뷰라 이 자리는 상시 가림입니다. **투명화가 아니라 자리를 비껴** 세우십시오" +
                    "(`VisualSliceBuilder`의 동료 좌표).");
            // **한 축을 고치고 다른 축으로 새지 않게**(검수 조건 3): 비껴 세운 자리가 울타리·건물 안이거나
            // 못 서는 땅이면 이격은 성공이 아니다. 격자 게이트와 **같은 함수**로 판정한다.
            // **자기 자신은 빼고 잰다** — 동료의 콜라이더가 그 자리에 있으니 그대로 부르면
            // 언제나 「설 수 없는 자리」가 된다(첫 판이 그렇게 빨간불이었다. 계측기가 자기 그림자를 본 것).
            var feet = new Vector3(comp.x, GroundYAt(new Vector2(comp.x, comp.z)), comp.z);
            var compRoot = GameObject.Find(VisualSliceBuilder.CompanionObject).transform;
            var hits = Physics.OverlapCapsule(feet + Vector3.up * 0.4f, feet + Vector3.up * 1.6f, 0.35f,
                ~0, QueryTriggerInteraction.Ignore);
            var blockers = new System.Collections.Generic.List<string>();
            for (int i = 0; i < hits.Length; i++)
            {
                if (IsTerrainCollider(hits[i]) || hits[i].transform.IsChildOf(compRoot))
                    continue;
                blockers.Add(GroundFit.NodePath(hits[i].transform));
            }
            if (blockers.Count > 0)
                throw new InvalidOperationException("동료가 선 자리(" + comp.ToString("0.0") +
                    ")가 막혀 있습니다: " + string.Join(", ", blockers) +
                    " — 가림(한 축)을 고치고 끼임(다른 축)으로 새면 안 됩니다.");
            if (d < 1.2f)
                throw new InvalidOperationException("동료가 플레이어와 " + d.ToString("0.0") +
                    "m밖에 안 떨어져 있습니다 — 겹쳐 서면 각도와 무관하게 가립니다.");
        }

        /// <summary>NC — 동료를 **실제로 시선 축 위에** 세우면 빨간불이어야 한다.</summary>
        static void AssertCompanionOffSightAxisNegativeControl()
        {
            var c = GameObject.Find(VisualSliceBuilder.CompanionObject);
            var p = GameObject.Find("Player");
            if (c == null || p == null)
                throw new InvalidOperationException("동료 자리 NC 대상이 없습니다 — 잰 것이 없습니다(0이면 실패).");
            var saved = c.transform.position;
            bool red = false;
            string message = "";
            try
            {
                var qv = UnityEngine.Object.FindFirstObjectByType<QuarterViewCamera>(FindObjectsInactive.Include);
                var rot = Quaternion.Euler(qv != null ? qv.Pitch : 35f, qv != null ? qv.Yaw : 45f, 0f);
                var toCam = -(rot * Vector3.forward);
                var axis = new Vector3(toCam.x, 0f, toCam.z).normalized;
                c.transform.position = p.transform.position + axis * 2.0f;
                try { AssertCompanionOffSightAxis(); }
                catch (InvalidOperationException e) { red = true; message = e.Message; }
            }
            finally { c.transform.position = saved; }
            if (!red)
                throw new InvalidOperationException("동료 자리 네거티브 컨트롤 실패 — 시선 축 위에 세웠는데 통과했습니다.");
            Debug.Log("[Ulon] 동료 자리 네거티브 컨트롤 통과 — 축 위에 세우면 FAIL: " + message);
        }
    }
}
