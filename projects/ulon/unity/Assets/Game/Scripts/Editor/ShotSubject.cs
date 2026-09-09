using System.Collections.Generic;
using Ulon.Shared;

namespace Ulon.Editor
{
    /// <summary>
    /// **샷의 주인공 원장**(검수 「죽은 장 훑기」 착수 승인, 2026-09-09).
    ///
    /// 「이 샷은 무엇을 보여 주는 장인가」를 **글이 아니라 오브젝트 이름으로** 적는다. 이 원장이 없으면
    /// 「죽은 장」은 눈으로만 알 수 있고, 눈은 판마다 다르다(`13_d1_room_cutaway`가 이름만 절단면이고
    /// 화면은 지표 잔디였던 것을 여러 판 못 봤다).
    ///
    /// **이 원장이 못 적는 것**: 지형이 주인공인 조망(`14`·`15`·`16`·`63`)은 대상이 오브젝트가 아니라
    /// **땅**이다. 실루엣 자는 `MeshFilter`만 그리므로 `Terrain`을 못 센다 — 그 샷들은 `null`로 적고
    /// 셈에서 「못 잼」으로 따로 찍는다. **못 재는 것을 0으로 적으면 그것이 곧 빈 통과다.**
    /// </summary>
    public static class ShotSubject
    {
        /// <summary>
        /// **샷의 부류** — 하한은 부류마다 다르다(검수 원장 2026-09-09:
        /// 「한 부류의 정상값이 다른 부류에서는 죽은 값이다」). 실내 절단면이 60%에서 20%로 죽어도
        /// 전역 3% 하한은 통과한다 — `13`이 죽어 있던 그 상태를 못 잡는다.
        /// </summary>
        public enum Kind { CloseUp, Village, Region, Entrance, Interior, Terrain }

        /// <summary>
        /// 부류별 하한 — **각 부류 실측 최저의 절반**(2026-09-09 실측 18장).
        /// 근접 5.8 · 마을 6.3 · 지역 7.4 · 입구 14.7 · 실내 60.5 → 3 / 3 / 3 / 7 / 30.
        /// 여유를 주는 이유는 그대로다(산포가 흔들릴 때마다 우는 자는 곧 꺼진다) — 다만
        /// **여유도 부류마다** 준다.
        /// </summary>
        public static float MinShare(Kind k)
        {
            switch (k)
            {
                case Kind.CloseUp: return 0.03f;
                case Kind.Village: return 0.03f;
                case Kind.Region: return 0.03f;
                case Kind.Entrance: return 0.07f;
                case Kind.Interior: return 0.30f;
                default: return 0f;      // 지형 — 이 자는 못 잰다
            }
        }

        public struct Entry
        {
            public string[] Objects;    // 빈 배열이면 「지형이 주인공이라 못 잰다」
            public Kind Class;
        }

        static Entry E(Kind k, params string[] objects) => new Entry { Objects = objects, Class = k };

        /// <summary>샷 이름 → 주인공과 부류.</summary>
        public static readonly Dictionary<string, Entry> Table = new Dictionary<string, Entry>
        {
            // 던전 실내 — 방과 그 안의 것들.
            { "10_d2_interior", E(Kind.Interior, Dungeon2.InteriorObject) },
            { "12_d3_interior", E(Kind.Interior, Dungeon3.InteriorObject) },
            { "23_d1_corner_playcam", E(Kind.Interior, Dungeon1.InteriorObject) },
            { "08_d1_interior", E(Kind.Interior, Dungeon1.InteriorObject) },
            { "13_d1_room_cutaway", E(Kind.Interior, Dungeon1.InteriorObject) },
            // 사람·짐승 근접 — 그 몸이 주인공이다.
            { "17_boss_closeup", E(Kind.CloseUp, Dungeon3.BossObject) },
            { "22_mob_closeup", E(Kind.CloseUp, Dungeon3.MobObject) },
            { "06_field_boss", E(Kind.CloseUp, FieldBoss.Object) },
            // 야외 지역 — 지역 소품 무리가 주인공이다(땅 색은 다른 자가 본다).
            { "18_meadow", E(Kind.Region, WorldRegions.MeadowObject) },
            { "19_forest", E(Kind.Region, WorldRegions.ForestObject) },
            { "20_mine", E(Kind.Region, WorldRegions.MineObject) },
            { "21_testchamber", E(Kind.Region, WorldRegions.TestChamberObject) },
            // 마을 — 집·시설 무리.
            // 씬의 마을 루트는 `VillageDecor`다 — 첫 판에 `Village`라고 적었더니 자가
            // 「주인공을 못 찾음」이라 외쳤다. **원장이 씬을 따라야지 그 반대가 아니다.**
            { "01_village_square", E(Kind.Village, "VillageDecor") },
            { "02_village_wide", E(Kind.Village, "VillageDecor") },
            // 입구 셋 — 문틀이 주인공이다.
            { "07_d1_entrance", E(Kind.Entrance, Dungeon1.RootObject) },
            { "09_d2_entrance", E(Kind.Entrance, Dungeon2.RootObject) },
            { "11_d3_entrance", E(Kind.Entrance, Dungeon3.RootObject) },
            { "60_d3_entrance_45", E(Kind.Entrance, Dungeon3.RootObject) },
            // 지형이 주인공이라 실루엣 자로는 못 잰다 — 빈 배열로 **못 잼을 명시**한다.
            { "14_world_vista", E(Kind.Terrain) },
            { "15_lake_river", E(Kind.Terrain) },
            { "16_mountain_ridge", E(Kind.Terrain) },
            { "63_pier_cutface", E(Kind.Terrain) },
        };
    }
}
