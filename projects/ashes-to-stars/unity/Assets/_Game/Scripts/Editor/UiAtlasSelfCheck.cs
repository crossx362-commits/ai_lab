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

            Debug.Log("[UiAtlasSelfCheck] PASS");
        }
    }
}
