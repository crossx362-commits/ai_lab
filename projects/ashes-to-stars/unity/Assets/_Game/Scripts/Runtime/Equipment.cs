using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 장비 6부위(§11). 첫 슬라이스는 갑옷 1종만 만든다.
    ///
    /// 왜 지금인가: 오너가 §16 영지 3건물을 골랐고, 대장간은 수직 슬라이스의
    /// 장비·제작이라 OUT이 아니다. 화면만 열고 전투가 안 읽으면 또 거짓말이다.
    /// </summary>
    public enum EquipSlot { Weapon, Helm, Armor, Gloves, Boots, Accessory }

    [Serializable]
    public sealed class GearItem
    {
        public string Id;
        public EquipSlot Slot;
        public string RecipeId;
        public string Name;
        public float HpMul = 1f;
    }

    public static class Equipment
    {
        public const string LeatherArmorRecipe = "leather_armor";
        public const string LeatherArmorName = "가죽 흉갑";
        public const int LeatherArmorHideCost = 5;
        public const float LeatherArmorHpMul = 1.15f;

        const string K_GEAR = "ats.gear";

        static List<GearItem> _items;
        static bool _loaded;

        public static IReadOnlyList<GearItem> All { get { Load(); return _items; } }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _items = new List<GearItem>();
            string raw = PlayerPrefs.GetString(K_GEAR, "");
            if (string.IsNullOrEmpty(raw)) return;
            foreach (string line in raw.Split('\n'))
            {
                if (string.IsNullOrEmpty(line)) continue;
                string[] p = line.Split('\t');
                if (p.Length < 5) continue;
                var item = new GearItem
                {
                    Id = p[0],
                    Slot = ParseSlot(p[1]),
                    RecipeId = p[2],
                    Name = p[3],
                    HpMul = float.TryParse(p[4], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float hp) ? hp : 1f,
                };
                _items.Add(item);
            }
        }

        static EquipSlot ParseSlot(string raw) =>
            Enum.TryParse(raw, out EquipSlot slot) ? slot : EquipSlot.Armor;

        static void Save()
        {
            Load();
            var sb = new StringBuilder();
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            for (int i = 0; i < _items.Count; i++)
            {
                var g = _items[i];
                sb.Append(g.Id).Append('\t').Append(g.Slot).Append('\t').Append(g.RecipeId)
                  .Append('\t').Append(g.Name).Append('\t')
                  .Append(g.HpMul.ToString(inv)).Append('\n');
            }
            PlayerPrefs.SetString(K_GEAR, sb.ToString());
            PlayerPrefs.Save();
        }

        /// <summary>대장간 해금 = 1차 전직 시점(§13-2). 기본직업만 있으면 제작하지 않는다.</summary>
        public static bool SmithUnlocked()
        {
            var roster = LifeSystem.GetCharacters();
            for (int i = 0; i < roster.Count; i++)
            {
                var ch = roster[i];
                if (ch != null && !ch.IsDeleted && ch.Advancement != AdvancementTier.Basic)
                    return true;
            }
            return false;
        }

        public static GearItem Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            Load();
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].Id == id) return _items[i];
            return null;
        }

        public static List<GearItem> Unequipped()
        {
            Load();
            var roster = LifeSystem.GetCharacters();
            var worn = new HashSet<string>();
            for (int i = 0; i < roster.Count; i++)
            {
                string id = roster[i].EquippedArmorId;
                if (!string.IsNullOrEmpty(id)) worn.Add(id);
            }
            var list = new List<GearItem>();
            for (int i = 0; i < _items.Count; i++)
                if (!worn.Contains(_items[i].Id)) list.Add(_items[i]);
            return list;
        }

        public static float HpMulOf(CharacterRecord character)
        {
            if (character == null) return 1f;
            var gear = Find(character.EquippedArmorId);
            return gear == null || gear.HpMul <= 0f ? 1f : gear.HpMul;
        }

        public static bool TryCraftLeatherArmor()
        {
            Load();
            if (!SmithUnlocked()) return false;
            if (GameState.Bag.GetCount(Economy.LifeItem.CraftHide) < LeatherArmorHideCost) return false;
            if (!GameState.Consume(Economy.LifeItem.CraftHide, LeatherArmorHideCost)) return false;

            _items.Add(new GearItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Slot = EquipSlot.Armor,
                RecipeId = LeatherArmorRecipe,
                Name = LeatherArmorName,
                HpMul = LeatherArmorHpMul,
            });
            Save();
            return true;
        }

        public static bool TryEquip(CharacterRecord character, string gearId)
        {
            Load();
            if (character == null || character.IsDeleted) return false;
            var gear = Find(gearId);
            if (gear == null || gear.Slot != EquipSlot.Armor) return false;

            var roster = LifeSystem.GetCharacters();
            for (int i = 0; i < roster.Count; i++)
            {
                if (roster[i].EquippedArmorId == gearId)
                    roster[i].EquippedArmorId = null;
            }
            character.EquippedArmorId = gearId;
            LifeSystem.PersistRoster();
            return true;
        }

        public static bool TryUnequip(CharacterRecord character)
        {
            if (character == null || string.IsNullOrEmpty(character.EquippedArmorId)) return false;
            character.EquippedArmorId = null;
            LifeSystem.PersistRoster();
            return true;
        }

        /// <summary>삭제된 캐릭터의 장착 6부위는 함께 사라진다(§4·§11). 가방의 나머지 장비는 남는다.</summary>
        public static void DestroyEquippedOn(CharacterRecord character)
        {
            Load();
            if (character == null || string.IsNullOrEmpty(character.EquippedArmorId)) return;
            string id = character.EquippedArmorId;
            character.EquippedArmorId = null;
            _items.RemoveAll(g => g.Id == id);
            Save();
        }

        public static void ForgetInMemoryForTest()
        {
            _items = null;
            _loaded = false;
        }

        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(K_GEAR);
            PlayerPrefs.Save();
            ForgetInMemoryForTest();
        }
    }
}
