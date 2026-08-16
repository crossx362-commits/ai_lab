using System.Collections.Generic;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>상태이상·강화 효과를 작은 HUD에서도 읽히게 하는 4x4 아이콘 아틀라스.</summary>
    public static class StatusIconAtlas
    {
        public const int Width = 1254;
        public const int Height = 1254;
        const float Cell = Width / 4f;
        const string ResourceKey = "ui/status_icon_atlas";
        static Texture2D _texture;
        static bool _tried;

        static readonly Dictionary<string, Rect> Pieces = new Dictionary<string, Rect>
        {
            ["poison"] = CellAt(0, 0), ["burn"] = CellAt(1, 0), ["freeze"] = CellAt(2, 0), ["stun"] = CellAt(3, 0),
            ["silence"] = CellAt(0, 1), ["curse"] = CellAt(1, 1), ["slow"] = CellAt(2, 1), ["bleed"] = CellAt(3, 1),
            ["shield"] = CellAt(0, 2), ["taunt"] = CellAt(1, 2), ["attack_up"] = CellAt(2, 2), ["defense_up"] = CellAt(3, 2),
            ["regeneration"] = CellAt(0, 3), ["haste"] = CellAt(1, 3), ["vulnerability"] = CellAt(2, 3), ["cooldown"] = CellAt(3, 3),
        };

        public static readonly string[] RequiredKeys =
        {
            "poison", "burn", "freeze", "stun", "silence", "curse", "slow", "bleed",
            "shield", "taunt", "attack_up", "defense_up", "regeneration", "haste", "vulnerability", "cooldown",
        };

        static Rect CellAt(int col, int row) => new Rect(col * Cell, row * Cell, Cell, Cell);
        static Texture2D Texture { get { if (!_tried) { _tried = true; _texture = Resources.Load<Texture2D>(ResourceKey); } return _texture; } }
        public static bool IsReady => Texture != null;
        public static Rect RectFor(string key) => Pieces.TryGetValue(key, out var rect) ? rect : Rect.zero;
    }
}
