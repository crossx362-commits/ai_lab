using System;
using System.Collections.Generic;
using UnityEngine;
using Ulon.Shared;

namespace Ulon.Editor
{
    /// <summary>
    /// **워프 자리에는 아무도 서 있지 않다**(검수 랩 2026-09-08).
    ///
    /// 발단: 던전 2 도적(`DungeonBandit`)이 출구 워프에서 **0.82m** 앞에 서 있었다.
    /// 그리고 그 수치는 던전 1·3에도 **똑같이 복사돼** 있었다 — 한 자리만 고치면 나머지 둘은
    /// 다음 사람이 다시 만난다(같은 로직이 여러 곳에 살면 재발한다, 원장).
    ///
    /// 재는 것: **워프 목적지 원장**(`SliceSelfCheck.Warp.cs`의 `WarpSpots()`, 던전 출입·문게이트·
    /// GM 워프·은행/상인 옆까지 전부) 각 자리에서 **가장 가까운 몹까지의 거리**.
    /// 하한 `WarpClearMin`은 몸통 반지름(≈0.35m)에 사람 하나가 빠져나갈 여유를 더한 값이다 —
    /// 이보다 가까우면 나가는 사람이 몹 몸 안에서 튀어나온다.
    /// 예외 선언: 없음(전 던전 전수).
    /// </summary>
    public static partial class SliceSelfCheck
    {
        /// <summary>워프 자리에서 몹까지 최소 거리 — 사람 몸 하나가 빠져나갈 여유.</summary>
        public const float WarpClearMin = 2f;

        static List<string> WarpBlockers(float min)
        {
            var bad = new List<string>();
            // **목적지 원장은 하나다** — `SliceSelfCheck.Warp.cs`의 `WarpSpots()`를 그대로 쓴다.
            // 처음엔 여기서 던전 출입구 목록을 새로 적었는데, 원장이 둘이면 새 워프가 생겼을 때
            // 한쪽만 갱신돼 검사가 갈린다(원장: 상태 원장이 둘이면 검사도 갈린다).
            var spots = WarpSpots();
            var bodies = UnityEngine.Object.FindObjectsByType<Ulon.Server.WorldBody>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (bodies.Length == 0)
                throw new InvalidOperationException("워프 자리 검사 대상이 없습니다 — 몸을 하나도 못 찾았습니다(0이면 실패).");
            for (int s = 0; s < spots.Count; s++)
            {
                string worst = null;
                float best = float.MaxValue;
                var at = spots[s].Beside != null
                    ? new Vector2(spots[s].Beside.position.x, spots[s].Beside.position.z)
                    : new Vector2(spots[s].X, spots[s].Z);
                for (int b = 0; b < bodies.Length; b++)
                {
                    if (!bodies[b].IsEnemy)
                        continue;                              // 플레이어·NPC는 다른 축(몹만 본다)
                    var p = bodies[b].transform.position;
                    float d = Vector2.Distance(new Vector2(p.x, p.z), at);
                    if (d < best) { best = d; worst = bodies[b].name; }
                }
                if (worst != null && best < min)
                    bad.Add(spots[s].Name + " ← " + worst + " " + best.ToString("0.00") + "m");
            }
            return bad;
        }

        static void AssertWarpSpotsClear()
        {
            var bad = WarpBlockers(WarpClearMin);
            Debug.Log("[Ulon] 워프 자리 여유 — 자리 " + WarpSpots().Count + "곳 중 몹이 " +
                      WarpClearMin.ToString("0.0") + "m 안에 선 곳 " + bad.Count + "곳" +
                      (bad.Count > 0 ? ": " + string.Join(", ", bad) : ""));
            if (bad.Count > 0)
                throw new InvalidOperationException("워프 자리에 몹이 붙어 있습니다: " + string.Join(", ", bad) +
                    " — 나가는 사람이 몹 몸 안에서 튀어나옵니다. 몹 자리 상수(`Dungeon*.MobX/MobZ`)를 고치십시오.");
        }

        /// <summary>NC — 몹 하나를 **실제로 착지 자리 위로 옮기면** 빨간불이어야 한다.</summary>
        static void AssertWarpSpotsClearNegativeControl()
        {
            var bodies = UnityEngine.Object.FindObjectsByType<Ulon.Server.WorldBody>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Transform victim = null;
            for (int i = 0; i < bodies.Length && victim == null; i++)
                if (bodies[i].IsEnemy)
                    victim = bodies[i].transform;
            if (victim == null)
                throw new InvalidOperationException("워프 자리 NC 대상 몹이 없습니다 — 잰 것이 없습니다(0이면 실패).");
            var saved = victim.position;
            var cc = victim.GetComponent<CharacterController>();
            bool red = false;
            try
            {
                if (cc != null) cc.enabled = false;
                victim.position = new Vector3(Dungeon1.InteriorX, saved.y, Dungeon1.InteriorZ);
                try { AssertWarpSpotsClear(); }
                catch (InvalidOperationException) { red = true; }
            }
            finally
            {
                victim.position = saved;
                if (cc != null) cc.enabled = true;
            }
            if (!red)
                throw new InvalidOperationException("워프 자리 네거티브 컨트롤 실패 — " + victim.name +
                    "을 던전1 착지 자리 위에 세웠는데 통과했습니다.");
            Debug.Log("[Ulon] 워프 자리 네거티브 컨트롤 통과 — 몹을 착지 자리 위에 세우면 FAIL");
        }
    }
}
