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

            Debug.Log("[UiAtlasSelfCheck] PASS");
        }
    }
}
