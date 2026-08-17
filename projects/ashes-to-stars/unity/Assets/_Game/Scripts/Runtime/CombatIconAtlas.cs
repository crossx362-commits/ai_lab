using System.Collections.Generic;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 전투 스킬·상태·위험 기믹 아이콘을 한 장의 원본에서 잘라 쓰는 레지스트리.
    /// 원본은 8열 × 6행이며 좌표는 PNG의 좌상단을 (0, 0)으로 한 픽셀 기준이다.
    /// </summary>
    public static class CombatIconAtlas
    {
        public const int Width = 1448;
        public const int Height = 1086;
        const int Columns = 8;
        const int Rows = 6;
        const float CellWidth = Width / (float)Columns;
        const float CellHeight = Height / (float)Rows;
        const string ResourceKey = "ui/combat_icon_atlas";

        static Texture2D _texture;
        static bool _tried;

        static readonly Dictionary<string, Rect> Pieces = new Dictionary<string, Rect>
        {
            ["tank_charge"] = Cell(0, 0), ["tank_taunt"] = Cell(1, 0),
            ["tank_slam"] = Cell(2, 0), ["tank_barrier"] = Cell(3, 0),
            ["tank_last_stand"] = Cell(4, 0), ["tank_guard"] = Cell(5, 0),
            ["aggro"] = Cell(6, 0), ["damage_reduce"] = Cell(7, 0),

            ["damage_slash"] = Cell(0, 1), ["damage_critical"] = Cell(1, 1),
            ["damage_spin"] = Cell(2, 1), ["damage_dodge"] = Cell(3, 1),
            ["damage_arrow_volley"] = Cell(4, 1), ["damage_blink"] = Cell(5, 1),
            ["damage_weakpoint"] = Cell(6, 1), ["damage_execute"] = Cell(7, 1),

            ["heal_staff"] = Cell(0, 2), ["heal_burst"] = Cell(1, 2),
            ["cleanse"] = Cell(2, 2), ["revive"] = Cell(3, 2),
            ["heal_halo"] = Cell(4, 2), ["heal_party"] = Cell(5, 2),
            ["regeneration"] = Cell(6, 2), ["heal_beacon"] = Cell(7, 2),

            ["buffer_aura"] = Cell(0, 3), ["buffer_song"] = Cell(1, 3),
            ["buffer_scroll"] = Cell(2, 3), ["buffer_haste"] = Cell(3, 3),
            ["buffer_defense"] = Cell(4, 3), ["buffer_speed"] = Cell(5, 3),
            ["buffer_cooldown"] = Cell(6, 3), ["buffer_link"] = Cell(7, 3),

            ["warn_circle"] = Cell(0, 4), ["warn_cone"] = Cell(1, 4),
            ["warn_charge"] = Cell(2, 4), ["warn_beam"] = Cell(3, 4),
            ["warn_summon"] = Cell(4, 4), ["warn_heal_check"] = Cell(5, 4),
            ["warn_enrage"] = Cell(6, 4), ["warn_phase"] = Cell(7, 4),

            ["selected"] = Cell(0, 5), ["stunned"] = Cell(1, 5),
            ["silenced"] = Cell(2, 5), ["rooted"] = Cell(3, 5),
            ["knockback"] = Cell(4, 5), ["interrupted"] = Cell(5, 5),
            ["shielded"] = Cell(6, 5), ["low_health"] = Cell(7, 5),
        };

        public static readonly string[] RequiredKeys =
        {
            "tank_charge", "damage_slash", "heal_staff", "buffer_aura",
            "warn_circle", "warn_charge", "warn_summon", "warn_heal_check", "warn_phase",
            "selected", "shielded", "low_health",
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

        // ── 힉스필드 단품 아이콘(2026-08-18) ──────────────────────────────────
        // 옛 아틀라스는 채도 높은 가챠풍이라 할로우 나이트 화풍과 어긋난다(오너 지적).
        // 48칸을 한 장으로 다시 뽑는 대신, 이미 생성해 두고 안 쓰던 단품 이미지
        // 49장(P12 9 + P13 40)을 `art/style_check.py`로 골라 그대로 쓴다 — "버리지 말고
        // 활용"(오너 지시). `PortraitAtlas.SoloOf`와 같은 패턴: solo가 있으면 우선,
        // 없으면 옛 아틀라스로 폴백해서 화면이 비지 않는다.
        static readonly Dictionary<string, bool> _soloMissing = new Dictionary<string, bool>();

        static Texture2D SoloOf(string key)
        {
            if (_soloMissing.TryGetValue(key, out bool missing) && missing) return null;
            var t = Resources.Load<Texture2D>("ui/icons/" + key);
            _soloMissing[key] = t == null;
            return t;
        }

        public static bool Draw(Rect target, string key, Color? tint = null)
        {
            var solo = SoloOf(key);
            var saved = GUI.color;
            GUI.color = tint ?? Color.white;
            if (solo != null)
            {
                GUI.DrawTexture(target, solo, ScaleMode.ScaleToFit, true);
                GUI.color = saved;
                return true;
            }

            var texture = Texture;
            var source = RectFor(key);
            if (texture == null || source.width <= 0 || source.height <= 0)
            {
                GUI.color = saved;
                return false;
            }
            GUI.DrawTextureWithTexCoords(target, texture, TextureCoords(source), true);
            GUI.color = saved;
            return true;
        }

        static Rect Cell(int column, int row)
        {
            const float inset = 3f;
            return new Rect(
                column * CellWidth + inset,
                row * CellHeight + inset,
                CellWidth - inset * 2f,
                CellHeight - inset * 2f);
        }

        static Rect TextureCoords(Rect source)
        {
            return new Rect(
                source.x / Width,
                (Height - source.y - source.height) / Height,
                source.width / Width,
                source.height / Height);
        }
    }
}
