using System.Collections.Generic;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>4종 전투 배경을 한 장의 2×2 원본에서 잘라 월드 뒤에 배치한다.</summary>
    public static class BattleBackgroundAtlas
    {
        public const int Width = 1448;
        public const int Height = 1086;
        const string ResourceKey = "ui/battle_background_atlas";
        static Texture2D _texture;
        static bool _tried;

        static readonly Dictionary<string, Rect> Pieces = new Dictionary<string, Rect>
        {
            ["ashen_field"] = new Rect(0, 0, 724, 543),
            ["moon_ruins"] = new Rect(724, 0, 724, 543),
            ["infernal_arena"] = new Rect(0, 543, 724, 543),
            ["celestial_summit"] = new Rect(724, 543, 724, 543),
        };

        public static readonly string[] RequiredKeys = { "ashen_field", "moon_ruins", "infernal_arena", "celestial_summit" };

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

        public static void CreateWorldBackdrop(string key)
        {
            var texture = Texture;
            var rect = RectFor(key);
            if (texture == null || rect.width <= 0 || rect.height <= 0) return;

            var go = new GameObject("BattleBackdrop");
            var renderer = go.AddComponent<SpriteRenderer>();
            // Sprite.Create는 좌하단 원점을 쓰므로 생성 아틀라스의 좌상단 좌표를 뒤집는다.
            var pixels = new Rect(rect.x, Height - rect.y - rect.height, rect.width, rect.height);
            renderer.sprite = Sprite.Create(texture, pixels, new Vector2(.5f, .5f), 20f);
            // 기존 전장 지형은 불투명하므로 완전히 뒤에 두면 생성 배경이 보이지 않는다.
            // 유닛 앞을 막지 않는 옅은 분위기 레이어로 합성한다.
            renderer.sortingOrder = 1000;
            renderer.color = new Color(1f, 1f, 1f, .28f);
            go.transform.position = new Vector3(0, 0, -1f);
            go.transform.localScale = new Vector3(1.12f, 1.12f, 1f);
        }
    }
}
