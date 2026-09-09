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
        /// <summary>샷 이름 → 주인공 오브젝트 이름들(빈 배열이면 「지형이 주인공이라 못 잰다」).</summary>
        public static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>
        {
            // 던전 실내 — 방과 그 안의 것들.
            { "10_d2_interior", new[] { Dungeon2.InteriorObject } },
            { "12_d3_interior", new[] { Dungeon3.InteriorObject } },
            { "23_d1_corner_playcam", new[] { Dungeon1.InteriorObject } },
            { "08_d1_interior", new[] { Dungeon1.InteriorObject } },
            { "13_d1_room_cutaway", new[] { Dungeon1.InteriorObject } },
            // 사람·짐승 근접 — 그 몸이 주인공이다.
            { "17_boss_closeup", new[] { Dungeon3.BossObject } },
            { "22_mob_closeup", new[] { Dungeon3.MobObject } },
            { "06_field_boss", new[] { FieldBoss.Object } },
            // 야외 지역 — 지역 소품 무리가 주인공이다(땅 색은 다른 자가 본다).
            { "18_meadow", new[] { WorldRegions.MeadowObject } },
            { "19_forest", new[] { WorldRegions.ForestObject } },
            { "20_mine", new[] { WorldRegions.MineObject } },
            { "21_testchamber", new[] { WorldRegions.TestChamberObject } },
            // 마을 — 집·시설 무리.
            // 씬의 마을 루트는 `VillageDecor`다 — 첫 판에 `Village`라고 적었더니 자가
            // 「주인공을 못 찾음」이라 외쳤다. **원장이 씬을 따라야지 그 반대가 아니다.**
            { "01_village_square", new[] { "VillageDecor" } },
            { "02_village_wide", new[] { "VillageDecor" } },
            // 입구 셋 — 문틀이 주인공이다.
            { "07_d1_entrance", new[] { Dungeon1.RootObject } },
            { "09_d2_entrance", new[] { Dungeon2.RootObject } },
            { "11_d3_entrance", new[] { Dungeon3.RootObject } },
            { "60_d3_entrance_45", new[] { Dungeon3.RootObject } },
            // 지형이 주인공이라 실루엣 자로는 못 잰다 — 빈 배열로 **못 잼을 명시**한다.
            { "14_world_vista", new string[0] },
            { "15_lake_river", new string[0] },
            { "16_mountain_ridge", new string[0] },
            { "63_pier_cutface", new string[0] },
        };
    }
}
