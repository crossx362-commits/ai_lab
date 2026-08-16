using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>UI 아틀라스가 런타임에서 읽히고, 지정된 모든 조각이 원본 안에 있는지 검사한다.</summary>
    public static class UiAtlasSelfCheck
    {
        public static void Run()
        {
            Debug.Assert(UiAtlas.IsReady, "[UiAtlasSelfCheck] UI 아틀라스를 Resources/ui에서 읽지 못했다");

            foreach (var key in UiAtlas.RequiredKeys)
            {
                var rect = UiAtlas.RectFor(key);
                Debug.Assert(rect.width > 0 && rect.height > 0,
                    $"[UiAtlasSelfCheck] {key}: 빈 영역");
                Debug.Assert(rect.xMin >= 0 && rect.yMin >= 0 &&
                             rect.xMax <= UiAtlas.Width && rect.yMax <= UiAtlas.Height,
                    $"[UiAtlasSelfCheck] {key}: 아틀라스 밖 영역 {rect}");
            }

            Debug.Assert(UiAtlas.ButtonKey(false, false) == "button_normal",
                "[UiAtlasSelfCheck] 기본 버튼 키가 아틀라스 조각과 어긋난다");
            Debug.Assert(UiAtlas.ButtonKey(true, false) == "button_hover",
                "[UiAtlasSelfCheck] 호버 버튼 키가 아틀라스 조각과 어긋난다");
            Debug.Assert(UiAtlas.ButtonKey(false, true) == "button_pressed",
                "[UiAtlasSelfCheck] 눌림이 호버보다 우선해야 한다");
            Debug.Assert(UiAtlas.ButtonKey(true, true) == "button_pressed",
                "[UiAtlasSelfCheck] 호버+눌림은 pressed 조각을 써야 한다");
            Debug.Assert(UiAtlas.RectFor("hp_frame").width > 0,
                "[UiAtlasSelfCheck] 체력바 프레임 조각이 없다");
            Debug.Assert(UiAtlas.RectFor("xp_frame").width > 0,
                "[UiAtlasSelfCheck] 경험치 프레임 조각이 없다");
            Debug.Assert(UiAtlas.RectFor("portrait_frame").width > 0,
                "[UiAtlasSelfCheck] 초상 프레임 조각이 없다");
            Debug.Assert(UiAtlas.RectFor("heart").width > 0 && UiAtlas.RectFor("heart_broken").width > 0,
                "[UiAtlasSelfCheck] 목숨 아이콘이 없다");

            Debug.Assert(UiAtlas.RoleKey("탱") == "tank" && UiAtlas.RoleKey("수호기사") == "tank"
                         && UiAtlas.RoleKey("광전사") == "tank",
                "[UiAtlasSelfCheck] 탱 계열 역할 키가 어긋난다");
            Debug.Assert(UiAtlas.RoleKey("딜") == "damage" && UiAtlas.RoleKey("검사") == "damage"
                         && UiAtlas.RoleKey("궁수") == "damage",
                "[UiAtlasSelfCheck] 딜 계열 역할 키가 어긋난다");
            Debug.Assert(UiAtlas.RoleKey("힐") == "healer" && UiAtlas.RoleKey("사제") == "healer",
                "[UiAtlasSelfCheck] 힐 계열 역할 키가 어긋난다");
            Debug.Assert(UiAtlas.RoleKey("버퍼") == "buffer" && UiAtlas.RoleKey("음유시인") == "buffer"
                         && UiAtlas.RoleKey("정령사") == "buffer",
                "[UiAtlasSelfCheck] 버퍼 계열 역할 키가 어긋난다");

            Debug.Assert(UiAtlas.BuildingKey("대장간") == "building_smith",
                "[UiAtlasSelfCheck] 대장간 건물 키가 어긋난다");
            Debug.Assert(UiAtlas.BuildingKey("경매장") == "building_auction",
                "[UiAtlasSelfCheck] 경매장 건물 키가 어긋난다");
            Debug.Assert(UiAtlas.BuildingKey("영묘") == "building_mausoleum",
                "[UiAtlasSelfCheck] 영묘 건물 키가 어긋난다");
            Debug.Assert(UiAtlas.BuildingKey("수비대") == "building_barracks",
                "[UiAtlasSelfCheck] 수비대 건물 키가 어긋난다");
            Debug.Assert(UiAtlas.BuildingKey("없는건물") == null,
                "[UiAtlasSelfCheck] 모르는 건물은 null이어야 한다");

            Debug.Assert(UiAtlas.HeartKey(0, 0, false) == "heart"
                         && UiAtlas.HeartKey(2, 0, false) == "heart",
                "[UiAtlasSelfCheck] 목숨 3이면 세 칸 모두 온전해야 한다");
            Debug.Assert(UiAtlas.HeartKey(0, 1, false) == "heart"
                         && UiAtlas.HeartKey(2, 1, false) == "heart_broken",
                "[UiAtlasSelfCheck] 사망 1이면 마지막 칸만 깨져야 한다");
            Debug.Assert(UiAtlas.HeartKey(0, 0, true) == "heart_broken"
                         && UiAtlas.HeartKey(2, 3, true) == "heart_broken",
                "[UiAtlasSelfCheck] 삭제는 세 칸 모두 깨져야 한다");

            Debug.Assert(UiAtlas.HeaderKey("Field") == "field" && UiAtlas.HeaderKey("필드") == "field",
                "[UiAtlasSelfCheck] 필드 헤더는 나침반이 아니라 field 조각이어야 한다");
            Debug.Assert(UiAtlas.HeaderKey("Tower") == "tower" && UiAtlas.HeaderKey("탑") == "tower",
                "[UiAtlasSelfCheck] 탑 헤더는 나침반이 아니라 tower 조각이어야 한다");
            Debug.Assert(UiAtlas.HeaderKey("Estate") == "territory"
                         && UiAtlas.HeaderKey("Character") == "characters"
                         && UiAtlas.HeaderKey("WorldMap") == "worldmap",
                "[UiAtlasSelfCheck] 영지·캐릭터·월드맵 헤더 키가 어긋난다");
            Debug.Assert(UiAtlas.HeaderKey("없는화면") == null,
                "[UiAtlasSelfCheck] 모르는 화면은 null이어야 한다");
            Debug.Assert(UiAtlas.HeaderKey("Party") == null,
                "[UiAtlasSelfCheck] 매핑 없는 화면을 worldmap으로 숨기면 안 된다");

            // 편성 화면이 목숨·역할을 글자로만 쓰면 캐릭터 화면과 같은 조각이 소비처 0곳이다.
            var tankFull = UiAtlas.SlotChrome("탱", 0, false);
            Debug.Assert(tankFull.frame == "portrait_frame" && tankFull.role == "tank"
                         && tankFull.heart0 == "heart" && tankFull.heart2 == "heart",
                "[UiAtlasSelfCheck] 편성 명부 크롬(탱·목숨3)이 어긋난다");
            var dpsHurt = UiAtlas.SlotChrome("딜", 1, false);
            Debug.Assert(dpsHurt.role == "damage" && dpsHurt.heart0 == "heart"
                         && dpsHurt.heart2 == "heart_broken",
                "[UiAtlasSelfCheck] 편성 명부 크롬(딜·사망1)이 어긋난다");
            var dead = UiAtlas.SlotChrome("힐", 0, true);
            Debug.Assert(dead.role == "healer" && dead.heart0 == "heart_broken"
                         && dead.heart1 == "heart_broken" && dead.heart2 == "heart_broken",
                "[UiAtlasSelfCheck] 편성 명부 크롬(삭제)이 어긋난다");
            Debug.Assert(UiAtlas.SlotChrome("버퍼", 2, false).role == "buffer",
                "[UiAtlasSelfCheck] 편성 명부 크롬(버퍼)이 어긋난다");
            _ = nameof(UiAtlas.DrawRosterFrame);
            _ = nameof(UiAtlas.DrawRosterMarks);

            Debug.Assert(UiAtlas.ButtonStateSamples.Length == 3,
                "[UiAtlasSelfCheck] 버튼 3상태 견본 개수가 어긋난다");
            Debug.Assert(UiAtlas.ButtonKey(UiAtlas.ButtonStateSamples[0].hover,
                                          UiAtlas.ButtonStateSamples[0].pressed) == "button_normal"
                         && UiAtlas.ButtonKey(UiAtlas.ButtonStateSamples[1].hover,
                                              UiAtlas.ButtonStateSamples[1].pressed) == "button_hover"
                         && UiAtlas.ButtonKey(UiAtlas.ButtonStateSamples[2].hover,
                                              UiAtlas.ButtonStateSamples[2].pressed) == "button_pressed",
                "[UiAtlasSelfCheck] 견본 3칸이 보통·호버·눌림 조각과 어긋난다");
            Debug.Assert(UiAtlas.RectFor("field").width > 0 && UiAtlas.RectFor("tower").width > 0,
                "[UiAtlasSelfCheck] 필드·탑 헤더 조각이 없다");

            Debug.Assert(UiAtlas.RarityKey(GearGrade.Common) == "rarity_common"
                         && UiAtlas.RarityKey(GearGrade.Uncommon) == "rarity_uncommon"
                         && UiAtlas.RarityKey(GearGrade.Rare) == "rarity_rare"
                         && UiAtlas.RarityKey(GearGrade.Heroic) == "rarity_heroic"
                         && UiAtlas.RarityKey(GearGrade.Legendary) == "rarity_legendary",
                "[UiAtlasSelfCheck] 5등급 키가 아틀라스 조각과 어긋난다");
            Debug.Assert(UiAtlas.RaritySamples.Length == 5, "[UiAtlasSelfCheck] 등급 견본이 5종이 아니다");
            Debug.Assert(UiAtlas.RaritySamples[0].label == "일반"
                         && UiAtlas.RaritySamples[4].label == "전설",
                "[UiAtlasSelfCheck] 등급 견본 라벨이 기획서 §11과 어긋난다");
            for (int i = 0; i < UiAtlas.RaritySamples.Length; i++)
            {
                string key = UiAtlas.RarityKey(UiAtlas.RaritySamples[i].grade);
                Debug.Assert(UiAtlas.RectFor(key).width > 0,
                    $"[UiAtlasSelfCheck] 등급 조각 {key} 없음");
            }
            _ = nameof(UiAtlas.DrawRarity);

            Debug.Assert(System.Array.IndexOf(UiAtlas.RequiredKeys, UiAtlas.BossHpFrameKey) >= 0,
                "[UiAtlasSelfCheck] boss_hp_frame 이 RequiredKeys에 없다");
            Debug.Assert(UiAtlas.RectFor(UiAtlas.BossHpFrameKey).width > 0,
                "[UiAtlasSelfCheck] 보스 HP 프레임 조각이 없다");
            Debug.Assert(UiAtlas.PhaseCountForFloor(1) == 2
                         && UiAtlas.PhaseCountForFloor(5) == 2
                         && UiAtlas.PhaseCountForFloor(10) == 3
                         && UiAtlas.PhaseCountForFloor(50) == 4,
                "[UiAtlasSelfCheck] 층별 페이즈 수가 §10-5와 어긋난다");
            Debug.Assert(UiAtlas.BossHpSamples.Length == 3,
                "[UiAtlasSelfCheck] 보스 HP 견본이 3칸이 아니다");
            Debug.Assert(UiAtlas.BossHpSamples[1].current == 4500f
                         && UiAtlas.BossHpSamples[1].phases == 2,
                "[UiAtlasSelfCheck] 1/2 견본이 페이즈 경계와 어긋난다");
            _ = nameof(UiAtlas.DrawBossHp);

            Debug.Log("[UiAtlasSelfCheck] PASS");
        }
    }
}
