using System.Collections.Generic;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>성장·보상에 쓰는 4x4 아이템 아틀라스 레지스트리.</summary>
    public static class ItemAtlas
    {
        public const int Width = 1254;
        public const int Height = 1254;
        const string ResourceKey = "ui/item_atlas";
        const float Cell = Width / 4f;

        static Texture2D _texture;
        static bool _tried;

        static readonly Dictionary<string, Rect> Pieces = new Dictionary<string, Rect>
        {
            ["sword"] = CellAt(0, 0), ["shield"] = CellAt(1, 0), ["staff"] = CellAt(2, 0), ["bow"] = CellAt(3, 0),
            ["helmet"] = CellAt(0, 1), ["armor"] = CellAt(1, 1), ["gloves"] = CellAt(2, 1), ["boots"] = CellAt(3, 1),
            ["ring"] = CellAt(0, 2), ["amulet"] = CellAt(1, 2), ["revival_tea"] = CellAt(2, 2), ["scroll_of_return"] = CellAt(3, 2),
            ["reborn_stone"] = CellAt(0, 3), ["special_job_token"] = CellAt(1, 3), ["gold"] = CellAt(2, 3), ["advancement_material"] = CellAt(3, 3),
        };

        public static readonly string[] RequiredKeys =
        {
            "sword", "shield", "staff", "bow", "helmet", "armor", "gloves", "boots",
            "ring", "amulet", "revival_tea", "scroll_of_return", "reborn_stone", "special_job_token", "gold", "advancement_material",
        };

        static Rect CellAt(int column, int row) => new Rect(column * Cell, row * Cell, Cell, Cell);

        static Texture2D Texture
        {
            get
            {
                if (!_tried) { _tried = true; _texture = Resources.Load<Texture2D>(ResourceKey); }
                return _texture;
            }
        }

        public static bool IsReady => Texture != null;
        public static Rect RectFor(string key) => Pieces.TryGetValue(key, out var rect) ? rect : Rect.zero;

        public static bool Draw(Rect target, string key, Color? tint = null)
        {
            var texture = Texture;
            var source = RectFor(key);
            if (texture == null || source.width <= 0 || source.height <= 0) return false;

            var saved = GUI.color;
            GUI.color = tint ?? Color.white;
            GUI.DrawTextureWithTexCoords(target, texture, new Rect(
                source.x / Width,
                (Height - source.y - source.height) / Height,
                source.width / Width,
                source.height / Height), true);
            GUI.color = saved;
            return true;
        }

        /// <summary>허브 조각이 없으면 아이템 아틀라스를 본다. Row가 UiAtlas만 쓰면 검·물약이 글자가 된다.</summary>
        public static bool DrawHud(Rect target, string key, Color? tint = null)
        {
            if (string.IsNullOrEmpty(key)) return false;
            return UiAtlas.Draw(target, key, tint) || Draw(target, key, tint);
        }

        public static string KeyFor(Economy.LifeItem item) => item switch
        {
            Economy.LifeItem.RevivalTea => "revival_tea",
            Economy.LifeItem.ScrollOfReturn => "scroll_of_return",
            Economy.LifeItem.RebornStone => "reborn_stone",
            Economy.LifeItem.SpecialJobToken => "special_job_token",
            Economy.LifeItem.AdvancementMaterial => "advancement_material",
            Economy.LifeItem.CraftHide => "gloves",
            Economy.LifeItem.CraftFang => "sword",
            Economy.LifeItem.CraftBone => "helmet",
            Economy.LifeItem.CraftPart => "shield",
            Economy.LifeItem.CraftCrystal => "staff",
            Economy.LifeItem.CraftDemonite => "amulet",
            Economy.LifeItem.EnhanceStone => "gold",
            _ => null,
        };

        /// <summary>제작 결과는 재료가 아니라 부위 실루엣으로 읽힌다.</summary>
        public static string KeyForSlot(EquipSlot slot) => slot switch
        {
            EquipSlot.Weapon => "sword",
            EquipSlot.Helm => "helmet",
            EquipSlot.Armor => "armor",
            EquipSlot.Gloves => "gloves",
            EquipSlot.Boots => "boots",
            EquipSlot.Accessory => "amulet",
            _ => null,
        };

        public static string KeyForGear(GearItem gear) =>
            gear == null ? null : KeyForSlot(gear.Slot);

        public static readonly Economy.LifeItem[] SmithMaterials =
        {
            Economy.LifeItem.CraftHide,
            Economy.LifeItem.CraftFang,
            Economy.LifeItem.CraftBone,
            Economy.LifeItem.CraftPart,
            Economy.LifeItem.CraftCrystal,
            Economy.LifeItem.CraftDemonite,
            Economy.LifeItem.EnhanceStone,
        };
    }
}
