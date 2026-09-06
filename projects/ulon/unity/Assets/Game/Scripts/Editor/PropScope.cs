using System;
using System.Collections.Generic;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **소품 게이트가 무엇을 대상으로 삼는가**의 공용 원장(검수 랩 C, 2026-09-07 사각지대 표 1번).
    ///
    /// 전에는 소품 게이트 넷(자격·크기·분포·맨바닥)이 전부 「던전 실내의 직계 자식 중 이름이
    /// `DungeonFurn`으로 시작하는 것」이라는 **한 집합**에 매여 있었다. 그 집합이 어긋나면
    /// (그룹으로 한 겹 싸거나 이름을 바꾸면) 넷이 **동시에** 눈을 감고, 마을·지역 소품은 애초에 밖이었다.
    ///
    /// 그래서 대상을 **구역 목록**으로 만들고, 구역 루트가 없거나 소품이 하한보다 적으면 **시끄럽게 실패**한다
    /// (조용히 0개를 세고 통과하는 길을 막는다).
    /// </summary>
    public static class PropScope
    {
        public struct Zone
        {
            public string Root;          // 씬 루트(또는 실내) 오브젝트 이름
            public string Label;
            public int MinProps;         // 이보다 적으면 실패 — 「빈 통과」 금지
            public bool Indoor;          // 던전 방 규칙(분포·맨바닥)이 걸리는 구역인가
            public float ScaleMaxFrac;   // 플레이어 키 대비 상한(야외는 건물·나무가 있어 느슨하다)
        }

        /// <summary>구역 원장 — 새 소품 구역이 생기면 여기 적어야 게이트가 본다.</summary>
        public static Zone[] Zones()
        {
            return new[]
            {
                new Zone { Root = Dungeon1.InteriorObject, Label = "던전 1 방", MinProps = 20, Indoor = true, ScaleMaxFrac = 0.80f },
                new Zone { Root = Dungeon2.InteriorObject, Label = "던전 2 방", MinProps = 20, Indoor = true, ScaleMaxFrac = 0.80f },
                new Zone { Root = Dungeon3.InteriorObject, Label = "던전 3 방", MinProps = 20, Indoor = true, ScaleMaxFrac = 0.80f },
                // 야외 구역: 집·나무가 정상적으로 크다 — 상한은 「거인 감지」용으로 느슨하게 둔다(플레이어 키 6배).
                new Zone { Root = "VillageDecor", Label = "마을 장식", MinProps = 30, ScaleMaxFrac = 6f },
                new Zone { Root = WorldRegions.MeadowObject, Label = "농경지", MinProps = WorldRegions.Meadow.PropMin, ScaleMaxFrac = 6f },
                new Zone { Root = WorldRegions.ForestObject, Label = "숲", MinProps = WorldRegions.Forest.PropMin, ScaleMaxFrac = 6f },
                new Zone { Root = WorldRegions.MineObject, Label = "광산", MinProps = WorldRegions.Mine.PropMin, ScaleMaxFrac = 6f },
                new Zone { Root = WorldRegions.TestChamberObject, Label = "테스트 공간", MinProps = WorldRegions.TestChamber.PropMin, ScaleMaxFrac = 6f },
                new Zone { Root = "PlainScatter", Label = "평지 산포", MinProps = 40, ScaleMaxFrac = 6f },
            };
        }

        /// <summary>한 구역의 소품 노드 — 실내는 `DungeonFurn*` 직계 자식, 야외는 렌더러를 가진 직계 자식.</summary>
        public static List<Transform> Props(Zone zone, out GameObject root)
        {
            root = GameObject.Find(zone.Root);
            if (root == null)
                throw new InvalidOperationException(zone.Label + "(" + zone.Root + ") 구역 루트가 없습니다 — " +
                    "이름을 바꿨거나 안 만들어졌습니다. 소품 게이트가 조용히 0개를 세고 통과하면 안 되므로 실패로 처리합니다.");
            var list = new List<Transform>();
            for (int c = 0; c < root.transform.childCount; c++)
            {
                var child = root.transform.GetChild(c);
                if (!child.gameObject.activeInHierarchy)
                    continue;
                if (zone.Indoor && !child.name.StartsWith("DungeonFurn", StringComparison.Ordinal))
                    continue;
                if (child.GetComponentInChildren<Renderer>(false) == null)
                    continue;
                list.Add(child);
            }
            if (list.Count < zone.MinProps)
                throw new InvalidOperationException(zone.Label + " 소품이 " + list.Count + "개뿐입니다(하한 " +
                    zone.MinProps + ") — 잰 것이 없거나 구역이 비었습니다(0이면 실패).");
            return list;
        }
    }
}
