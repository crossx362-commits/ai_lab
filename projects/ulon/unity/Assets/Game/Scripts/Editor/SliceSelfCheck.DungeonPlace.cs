using System;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **실내/실외를 이름이 아니라 자리로 가른다**(검수 우선순위 ④, 2026-09-08).
    ///
    /// 예전 판정은 `transform.root.name`에 "Dungeon"이 들어가는지였다. 게이트의 **제외 조건**이
    /// 이름에 걸려 있으면, 누가 뿌리 이름을 갈거나 소품을 다른 뿌리 밑으로 옮기는 순간
    /// 그 게이트는 **아무 말 없이 넓어져 빈 통과**가 된다. 같은 계열로 두 번 데었다(접두사로
    /// 「Watermill」을 물로 셌던 건, 방 제외 조건을 성질 기반으로 바꾼 건).
    ///
    /// 그래서 자를 두 방향으로 잰다:
    /// ㉠ **이름을 바꿔도 판정이 안 바뀐다**(이름에 안 걸려 있다는 증거 — 여기가 핵심이다)
    /// ㉡ **자리를 바꾸면 판정이 바뀐다**(자리를 정말 보고 있다는 증거 — 0이면 실패).
    /// </summary>
    public static partial class SliceSelfCheck
    {
        static void AssertDungeonPlaceByPosition()
        {
            var interior = GameObject.Find(Dungeon1.InteriorObject);
            if (interior == null)
                throw new InvalidOperationException("던전 1 실내가 없습니다 — 못 잰 것을 통과로 적지 않는다.");
            var props = PropNodes(interior);
            if (props.Count == 0)
                throw new InvalidOperationException("던전 자리 판정 대상이 없습니다 — 잰 것이 없습니다(0이면 실패).");

            int inside = 0;
            for (int i = 0; i < props.Count; i++)
                if (GroundFit.WorldBounds(props[i], out Bounds b) && GroundFit.InDungeonPlace(b))
                    inside++;
            if (inside != props.Count)
                throw new InvalidOperationException("지하 방 소품 " + props.Count + "개 중 " + inside +
                    "개만 던전 자리로 읽힙니다 — 방 안의 물건은 전부 던전 자리여야 합니다(§8.2).");
            Debug.Log("[Ulon] 던전 자리 판정 — 방 안 소품 " + inside + "/" + props.Count + "개가 자리로 던전에 든다");
        }

        static void AssertDungeonPlaceByPositionNegativeControl()
        {
            var interior = GameObject.Find(Dungeon1.InteriorObject);
            if (interior == null)
                throw new InvalidOperationException("던전 1 실내가 없어 자리 판정 네거티브 컨트롤을 할 수 없습니다.");
            var props = PropNodes(interior);
            if (props.Count == 0)
                throw new InvalidOperationException("자리 판정 NC 대상이 없습니다(0이면 실패).");
            var victim = props[0];

            // ㉠ 이름을 **실제로** 갈아 본다 — 자리로 재고 있다면 판정은 꿈쩍도 하지 않아야 한다.
            string keepName = interior.name;
            bool sameAfterRename;
            try
            {
                interior.name = "그냥어떤방";
                sameAfterRename = GroundFit.WorldBounds(victim, out Bounds b1) && GroundFit.InDungeonPlace(b1);
            }
            finally { interior.name = keepName; }
            if (!sameAfterRename)
                throw new InvalidOperationException("던전 자리 판정이 **이름에 걸려 있습니다** — 뿌리 이름을 갈았더니 " +
                    victim.name + "이(가) 던전 밖으로 읽혔습니다. 제외 조건이 이름에 걸리면 언젠가 빈 통과가 됩니다.");

            // ㉡ 자리를 **실제로** 들판으로 옮겨 본다 — 자리를 보고 있다면 던전 밖으로 읽혀야 한다.
            var keepPos = victim.position;
            bool flipped;
            try
            {
                victim.position = new Vector3(0f, WorldTerrain.LandBase + 1f, 0f);
                Physics.SyncTransforms();
                flipped = !(GroundFit.WorldBounds(victim, out Bounds b2) && GroundFit.InDungeonPlace(b2));
            }
            finally
            {
                victim.position = keepPos;
                Physics.SyncTransforms();
            }
            if (!flipped)
                throw new InvalidOperationException("던전 자리 판정 네거티브 컨트롤 실패 — " + victim.name +
                    "을 들판 한복판으로 옮겼는데도 던전 자리로 읽힙니다.");
            Debug.Log("[Ulon] 던전 자리 판정 네거티브 컨트롤 통과 — 이름을 갈아도 그대로, 들판으로 옮기면 FAIL");
        }
    }
}
