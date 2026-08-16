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

        /// <summary>호버·눌림을 아틀라스 3상태에 대응한다. 눌림이 호버보다 앞선다.</summary>
        public static string ButtonKey(bool hover, bool pressed)
        {
            if (pressed) return "button_pressed";
            if (hover) return "button_hover";
            return "button_normal";
        }

        /// <summary>패널처럼 늘어나는 조각은 가장자리만 남기고 가운데를 늘린다.</summary>
        public static bool DrawSliced(Rect target, string key, float border = 12f, Color? tint = null)
        {
            var texture = Texture;
            var source = RectFor(key);
            if (texture == null || source.width <= 0 || source.height <= 0) return false;

            float b = Mathf.Min(border, source.width * 0.45f, source.height * 0.45f,
                                target.width * 0.45f, target.height * 0.45f);
            if (b < 1f) return Draw(target, key, tint);

            var saved = GUI.color;
            GUI.color = tint ?? Color.white;
            float sx = source.x, sy = source.y, sw = source.width, sh = source.height;
            float x0 = target.x, x1 = target.x + b, x2 = target.xMax - b, x3 = target.xMax;
            float y0 = target.y, y1 = target.y + b, y2 = target.yMax - b, y3 = target.yMax;
            DrawSrc(new Rect(x0, y0, b, b), new Rect(sx, sy, b, b), texture);
            DrawSrc(new Rect(x1, y0, x2 - x1, b), new Rect(sx + b, sy, sw - 2f * b, b), texture);
            DrawSrc(new Rect(x2, y0, b, b), new Rect(sx + sw - b, sy, b, b), texture);
            DrawSrc(new Rect(x0, y1, b, y2 - y1), new Rect(sx, sy + b, b, sh - 2f * b), texture);
            DrawSrc(new Rect(x1, y1, x2 - x1, y2 - y1), new Rect(sx + b, sy + b, sw - 2f * b, sh - 2f * b), texture);
            DrawSrc(new Rect(x2, y1, b, y2 - y1), new Rect(sx + sw - b, sy + b, b, sh - 2f * b), texture);
            DrawSrc(new Rect(x0, y2, b, b), new Rect(sx, sy + sh - b, b, b), texture);
            DrawSrc(new Rect(x1, y2, x2 - x1, b), new Rect(sx + b, sy + sh - b, sw - 2f * b, b), texture);
            DrawSrc(new Rect(x2, y2, b, b), new Rect(sx + sw - b, sy + sh - b, b, b), texture);
            GUI.color = saved;
            return true;
        }

        /// <summary>프레임 조각 안에 채움 막대를 그린다. 프레임이 없어도 막대는 그린다.</summary>
        public static bool DrawMeter(Rect target, string frameKey, float fill01, Color fill)
        {
            bool framed = Draw(target, frameKey);
            float padX = framed ? 10f : 0f;
            float padY = framed ? 6f : 0f;
            float w = Mathf.Max(0f, (target.width - padX * 2f) * Mathf.Clamp01(fill01));
            if (w > 0f)
            {
                var saved = GUI.color;
                GUI.color = fill;
                GUI.DrawTexture(new Rect(target.x + padX, target.y + padY, w, target.height - padY * 2f), Pixel);
                GUI.color = saved;
            }
            return framed;
        }

        static Texture2D _pixel;
        static Texture2D Pixel
        {
            get
            {
                if (_pixel == null)
                {
                    _pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    _pixel.SetPixel(0, 0, Color.white);
                    _pixel.Apply();
                    _pixel.hideFlags = HideFlags.HideAndDontSave;
                }
                return _pixel;
            }
        }

        static void DrawSrc(Rect dest, Rect source, Texture2D texture)
        {
            if (dest.width <= 0f || dest.height <= 0f || source.width <= 0f || source.height <= 0f) return;
            GUI.DrawTextureWithTexCoords(dest, texture, TextureCoords(source), true);
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
