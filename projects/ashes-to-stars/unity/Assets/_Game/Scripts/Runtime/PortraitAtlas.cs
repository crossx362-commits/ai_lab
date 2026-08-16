using System.Collections.Generic;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>파티원 초상화를 한 장의 4열 × 3행 원본에서 잘라 쓰는 레지스트리.</summary>
    public static class PortraitAtlas
    {
        public const int Width = 1447;
        public const int Height = 1087;
        const int Columns = 4;
        const int Rows = 3;
        const float CellWidth = Width / (float)Columns;
        const float CellHeight = Height / (float)Rows;
        const string ResourceKey = "ui/party_portrait_atlas";

        static Texture2D _texture;
        static bool _tried;

        static readonly Dictionary<string, Rect> Pieces = new Dictionary<string, Rect>
        {
            ["tank_knight"] = Cell(0, 0), ["paladin"] = Cell(1, 0),
            ["dwarf_guardian"] = Cell(2, 0), ["dark_knight"] = Cell(3, 0),
            ["rogue"] = Cell(0, 1), ["ranger"] = Cell(1, 1),
            ["fire_mage"] = Cell(2, 1), ["monk"] = Cell(3, 1),
            ["priest"] = Cell(0, 2), ["druid"] = Cell(1, 2),
            ["bard"] = Cell(2, 2), ["strategist"] = Cell(3, 2),
        };

        public static readonly string[] RequiredKeys =
        {
            "tank_knight", "dark_knight", "fire_mage", "priest", "bard",
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

        public static string KeyForJob(string job)
        {
            return job switch
            {
                "탱" => "tank_knight",
                "딜" => "dark_knight",
                "힐" => "priest",
                "버퍼" => "bard",
                "수호기사" => "tank_knight",
                "검사" => "dark_knight",
                "마법사" => "fire_mage",
                "사제" => "priest",
                "음유시인" => "bard",
                _ => "strategist",
            };
        }

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

        static Rect Cell(int column, int row)
        {
            const float inset = 3f;
            return new Rect(column * CellWidth + inset, row * CellHeight + inset,
                CellWidth - inset * 2f, CellHeight - inset * 2f);
        }

        static Rect TextureCoords(Rect source)
        {
            return new Rect(source.x / Width, (Height - source.y - source.height) / Height,
                source.width / Width, source.height / Height);
        }
    }
}
