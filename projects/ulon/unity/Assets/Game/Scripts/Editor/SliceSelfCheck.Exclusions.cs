using System;
using System.Collections.Generic;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **예외 전수 감사**(검수 지시 2026-09-09) — 이름으로 대상을 빼는 자리를 한 곳에 선언하고,
    /// **매 판 무엇이 실제로 빠졌는지 이름과 수를 찍는다**.
    ///
    /// 나온 배경: 소품 파고듦 자가 「벽걸이는 뺀다」고 적어 놓고 실제로 봐주던 것은 **돌기둥**이었다
    /// (벽 등불 넷이 기둥 몸통 안에 100% 박혀 있었는데 아무도 못 봤다, `2da94eef`).
    /// 채택 문장: 「예외는 자에 뚫은 구멍이다 — 무엇을 봐주는지 적었으면, 실제로 그것만 봐주는지도 세어 봐라.」
    ///
    /// 그래서 조항마다 **몇 개를 뺐는지 센다**. 0이면 **죽은 예외**(만들어지지도 않는 이름을 봐주는 조항)라
    /// 빨간불이다 — 없는 이름을 봐주는 조항은 다음 사람에게 「그런 것이 있다」고 거짓말한다.
    /// `GroundFit.LastExcluded`가 이미 하던 것을 다른 자에도 같은 방식으로 붙인 것이다.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        /// <summary>이름 접두사로 대상을 빼는 **선언된 예외** 하나.</summary>
        internal sealed class NameExclusion
        {
            public readonly string Gate;      // 어느 자의 예외인가
            public readonly string Why;       // 적힌 대상 — 무엇을 봐준다고 말하는가
            public readonly string[] Prefixes;

            public NameExclusion(string gate, string why, params string[] prefixes)
            {
                Gate = gate;
                Why = why;
                Prefixes = prefixes;
            }

            public bool Excludes(string n)
            {
                for (int i = 0; i < Prefixes.Length; i++)
                    if (n.StartsWith(Prefixes[i], StringComparison.Ordinal))
                        return true;
                return false;
            }
        }

        /// <summary>소품 크기 비율 자의 예외 — 구조물은 사람 키 비율로 재는 대상이 아니다.</summary>
        internal static readonly NameExclusion PropScaleStructureExclusion = new NameExclusion(
            "소품 크기 비율", "기둥은 천장까지·벽 등불은 벽 2m에 있는 것이 정상이라 비율에서 뺀다",
            "DungeonFurnPillar", "DungeonFurnTorch");

        /// <summary>방 소품 접지 자의 예외 — 벽에 거는 것은 바닥에 발이 없다.</summary>
        internal static readonly NameExclusion RoomFurnishFootExclusion = new NameExclusion(
            "방 소품 접지", "벽에 거는 것은 바닥에 발이 없다",
            "DungeonFurnTorch");

        static readonly NameExclusion[] DeclaredExclusions =
        {
            PropScaleStructureExclusion,
            RoomFurnishFootExclusion,
        };

        /// <summary>예외가 실제로 재는 세계 — 세 방의 소품 전부(예외를 쓰는 자들이 도는 집합과 같다).</summary>
        static List<Transform> ExclusionUniverse()
        {
            var all = new List<Transform>();
            string[] rooms = { Dungeon1.InteriorObject, Dungeon2.InteriorObject, Dungeon3.InteriorObject };
            for (int i = 0; i < rooms.Length; i++)
            {
                var interior = GameObject.Find(rooms[i]);
                if (interior != null)
                    all.AddRange(PropNodes(interior));
            }
            return all;
        }

        /// <summary>조항별로 **무엇이 몇 개 빠졌는가**. 죽은 조항이 있으면 사유를, 없으면 null.</summary>
        static string ExclusionCensusReason(bool log, NameExclusion[] list)
        {
            var universe = ExclusionUniverse();
            if (universe.Count == 0)
                return "예외 감사 대상이 없습니다 — 방 소품을 하나도 못 읽었습니다(0이면 실패).";

            var lines = new List<string>();
            var dead = new List<string>();
            for (int e = 0; e < list.Length; e++)
                for (int p = 0; p < list[e].Prefixes.Length; p++)
                {
                    string prefix = list[e].Prefixes[p];
                    var hit = new List<string>();
                    for (int i = 0; i < universe.Count; i++)
                        if (universe[i] != null && universe[i].name.StartsWith(prefix, StringComparison.Ordinal))
                            hit.Add(universe[i].name);
                    lines.Add(list[e].Gate + "/" + prefix + " 제외 " + hit.Count + "개" +
                              (hit.Count > 0 ? ": " + string.Join(",", hit.GetRange(0, Mathf.Min(hit.Count, 6))) +
                                               (hit.Count > 6 ? " 외 " + (hit.Count - 6) : "") : ""));
                    if (hit.Count == 0)
                        dead.Add(list[e].Gate + "의 「" + prefix + "」(적힌 대상: " + list[e].Why + ")");
                }
            if (log)
                Debug.Log("[Ulon] 예외 감사 — 소품 " + universe.Count + "개 중 · " + string.Join(" · ", lines));
            if (dead.Count == 0)
                return null;
            return "죽은 예외가 " + dead.Count + "건 있습니다 — 아무것도 빼지 않는 조항입니다:\n  " +
                   string.Join("\n  ", dead) +
                   "\n없는 이름을 봐주는 조항은 다음 사람에게 「그런 것이 있다」고 거짓말합니다(§8.2).";
        }

        static void AssertExclusionsAlive()
        {
            string bad = ExclusionCensusReason(true, DeclaredExclusions);
            if (bad != null)
                throw new InvalidOperationException(bad);
        }

        /// <summary>양방향 NC — 아무것도 안 빼는 **유령 조항**을 넣으면 빨간불, 빼면 초록.</summary>
        static void AssertExclusionsAliveNegativeControl()
        {
            var ghost = new NameExclusion("예외 감사 NC", "만들어지지도 않는 이름", "DungeonFurnGhost");
            var withGhost = new List<NameExclusion>(DeclaredExclusions) { ghost };
            if (ExclusionCensusReason(false, withGhost.ToArray()) == null)
                throw new InvalidOperationException("예외 감사 네거티브 컨트롤 실패 — 아무것도 안 빼는 조항을 넣었는데 통과했습니다.");
            string back = ExclusionCensusReason(false, DeclaredExclusions);
            if (back != null)
                throw new InvalidOperationException("예외 감사 네거티브 컨트롤 실패 — 되돌렸는데도 빨간불입니다: " + back);
            Debug.Log("[Ulon] 예외 감사 양방향 NC 통과 — 유령 조항을 넣으면 FAIL · 빼면 다시 통과");
        }
    }
}
