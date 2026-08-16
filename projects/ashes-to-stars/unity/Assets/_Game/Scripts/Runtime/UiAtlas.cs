using System.Collections.Generic;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 한 장으로 생성한 UI 원본에서 공통 UI 조각을 잘라 쓰는 레지스트리.
    /// 좌표는 원본 PNG의 좌상단을 (0, 0)으로 한 픽셀 기준이다.
    /// </summary>
    public static class UiAtlas
    {
        public const int Width = 1448;
        public const int Height = 1086;
        const string ResourceKey = "ui/ashes_to_stars_ui_atlas";

        static Texture2D _texture;
        static bool _tried;

        static readonly Dictionary<string, Rect> Pieces = new Dictionary<string, Rect>
        {
            // 상단: 하단 고정바 5종
            ["territory"] = new Rect(12, 0, 120, 122),
            ["field"] = new Rect(140, 0, 135, 126),
            ["tower"] = new Rect(275, 0, 120, 128),
            ["worldmap"] = new Rect(407, 0, 135, 128),
            ["characters"] = new Rect(550, 0, 130, 128),

            // 두 번째 줄: 역할 아이콘
            ["tank"] = new Rect(8, 122, 125, 126),
            ["damage"] = new Rect(137, 122, 138, 126),
            ["healer"] = new Rect(278, 122, 142, 135),
            ["buffer"] = new Rect(426, 122, 145, 130),

            // 아틀라스 하단: 슬롯, 게이지, 패널, 버튼
            ["rarity_common"] = new Rect(8, 800, 80, 92),
            ["rarity_uncommon"] = new Rect(93, 800, 88, 92),
            ["rarity_rare"] = new Rect(185, 800, 91, 92),
            ["rarity_heroic"] = new Rect(280, 800, 89, 92),
            ["rarity_legendary"] = new Rect(373, 800, 96, 92),
            ["hp_frame"] = new Rect(303, 910, 228, 61),
            ["xp_frame"] = new Rect(538, 910, 222, 61),
            ["boss_hp_frame"] = new Rect(762, 905, 355, 71),
            ["panel"] = new Rect(746, 972, 181, 88),
            ["portrait_frame"] = new Rect(1124, 893, 79, 91),
            ["button_normal"] = new Rect(12, 996, 84, 63),
            ["button_hover"] = new Rect(103, 996, 83, 63),
            ["button_pressed"] = new Rect(193, 996, 82, 63),
        };

        public static readonly string[] RequiredKeys =
        {
            "territory", "field", "tower", "worldmap", "characters",
            "button_normal", "button_hover", "button_pressed", "panel", "hp_frame",
            "rarity_common", "rarity_uncommon", "rarity_rare", "rarity_heroic", "rarity_legendary",
        };

        static Texture2D Texture
        {
            get
            {
                if (!_tried)
                {
                    _tried = true;
                    _texture = Resources.Load<Texture2D>(ResourceKey);
                }
                return _texture;
            }
        }

        public static bool IsReady => Texture != null;

        public static Rect RectFor(string key)
        {
            return Pieces.TryGetValue(key, out var rect) ? rect : Rect.zero;
        }

        public static bool Draw(Rect target, string key, Color? tint = null)
        {
            var texture = Texture;
            var source = RectFor(key);
            if (texture == null || source.width <= 0 || source.height <= 0) return false;

            var saved = GUI.color;
            GUI.color = tint ?? Color.white;
            GUI.DrawTextureWithTexCoords(target, texture, TextureCoords(source), true);
            GUI.color = saved;
            return true;
        }

        static Rect TextureCoords(Rect source)
        {
            return new Rect(
                source.x / Width,
                (Height - source.y - source.height) / (float)Height,
                source.width / Width,
                source.height / Height);
        }
    }
}
