using System.Collections.Generic;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>생성한 4×4 전투 이펙트 원본을 이름 있는 정적 스프라이트로 제공한다.</summary>
    public static class CombatVfxAtlas
    {
        public const int Width = 1254, Height = 1254;
        static readonly string[] Keys = { "rogue_dash", "ranger_arrow", "druid_thorns", "bard_note", "bard_aura", "druid_regen", "revive", "cleanse", "boss_circle", "boss_cone", "boss_portal", "boss_charge", "critical", "dodge", "damage_reduce", "loot" };
        static Texture2D _texture;
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        public static readonly string[] RequiredKeys = Keys;
        static Texture2D Texture => _texture ??= Resources.Load<Texture2D>("fx/combat_vfx_atlas");
        public static bool IsReady => Texture != null || Resources.Load<Texture2D>("fx/icons/bard_note") != null;
        public static Sprite SpriteFor(string key)
        {
            if (Cache.TryGetValue(key, out var sprite)) return sprite;
            var solo = Resources.Load<Texture2D>("fx/icons/" + key);
            if (solo != null)
            {
                sprite = Sprite.Create(solo, new Rect(0, 0, solo.width, solo.height),
                    Vector2.one * .5f, Mathf.Max(8, solo.height) / 2f);
                Cache[key] = sprite;
                return sprite;
            }
            int i = System.Array.IndexOf(Keys, key); if (i < 0 || Texture == null) return null;
            float cell = Width / 4f; int col = i % 4, row = i / 4;
            sprite = Sprite.Create(Texture, new Rect(col * cell, Height - (row + 1) * cell, cell, cell), Vector2.one * .5f, cell / 2f);
            Cache[key] = sprite; return sprite;
        }
    }
}
