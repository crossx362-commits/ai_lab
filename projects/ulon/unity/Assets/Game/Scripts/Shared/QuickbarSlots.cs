using UnityEngine;

namespace Ulon.Shared
{
    /// <summary>기획 §4.2 스킬/마법 퀵바 단축키. HUD·키·게이트가 같이 읽는다. 판정은 서버.</summary>
    public enum QuickbarKind
    {
        Bandage = 0,
        Potion,
        Meditate,
        Spell
    }

    public struct QuickbarSlot
    {
        public QuickbarKind Kind;
        public SpellId Spell;
        public string Name;
    }

    public static class QuickbarSlots
    {
        /// <summary>네거티브 컨트롤 — 켜면 번호 라벨·단축키가 없다.</summary>
        public static bool NcDisable;

        public const int MaxKeys = 9;
        public const int MaxDrawn = 16;

        public static readonly SpellId[] Spells =
        {
            SpellId.Ember, SpellId.Mend, SpellId.Bolt, SpellId.Cleanse, SpellId.Ward,
            SpellId.Bind, SpellId.Weaken, SpellId.Spark, SpellId.Restore, SpellId.Blink, SpellId.Bless
        };

        public static KeyCode KeyOf(int index)
        {
            if (NcDisable || index < 0 || index >= MaxKeys)
                return KeyCode.None;
            return KeyCode.Alpha1 + index;
        }

        public static string Label(int index, string name)
        {
            if (string.IsNullOrEmpty(name))
                name = "";
            if (NcDisable || index < 0 || index >= MaxKeys)
                return name;
            return (index + 1) + " " + name;
        }

        public static int Fill(Spellbook book, QuickbarSlot[] dest)
        {
            if (dest == null || dest.Length == 0)
                return 0;
            int n = 0;
            n = Put(dest, n, QuickbarKind.Bandage, default, "붕대");
            n = Put(dest, n, QuickbarKind.Potion, default, "물약");
            n = Put(dest, n, QuickbarKind.Meditate, default, SkillNames.KoreanOf(SkillId.Meditation));
            if (book == null)
                return n;
            for (int i = 0; i < Spells.Length; i++)
            {
                if (n >= dest.Length)
                    break;
                if (!book.Knows(Spells[i]))
                    continue;
                n = Put(dest, n, QuickbarKind.Spell, Spells[i], SpellNames.KoreanOf(Spells[i]));
            }
            return n;
        }

        static int Put(QuickbarSlot[] dest, int n, QuickbarKind kind, SpellId spell, string name)
        {
            if (n < 0 || n >= dest.Length)
                return n;
            dest[n] = new QuickbarSlot { Kind = kind, Spell = spell, Name = name };
            return n + 1;
        }
    }
}
