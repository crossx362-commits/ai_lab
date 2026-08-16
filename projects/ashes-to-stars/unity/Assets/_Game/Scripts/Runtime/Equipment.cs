using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 장비 6부위 + 강화 +15(§11). 실패해도 장비는 파괴되지 않는다.
    ///
    /// 전투 소비처는 기존 <see cref="HpMulOf"/> 한 줄이다. W3Party는 이 슬라이스에서
    /// 건드리지 않는다 — 출전 계약이 곱한 체력 배율만 읽는다.
    /// </summary>
    public enum EquipSlot { Weapon, Helm, Armor, Gloves, Boots, Accessory }

    /// <summary>§11 위임 5단계. 제작품은 일반. 드랍 등급·랜덤 옵션은 이 슬라이스에 안 넣는다.</summary>
    public enum GearGrade { Common, Uncommon, Rare, Heroic, Legendary }

    [Serializable]
    public sealed class GearItem
    {
        public string Id;
        public EquipSlot Slot;
        public string RecipeId;
        public string Name;
        public float HpMul = 1f;
        public int Enhance;
        public GearGrade Grade = GearGrade.Common;
    }

    public sealed class CraftRecipe
    {
        public string Id;
        public string Name;
        public EquipSlot Slot;
        public Economy.LifeItem Material;
        public int Cost;
        public float BaseHpMul;
    }

    public static class Equipment
    {
        public const string LeatherArmorRecipe = "leather_armor";
        public const string LeatherArmorName = "가죽 흉갑";
        public const int LeatherArmorHideCost = 5;
        public const float LeatherArmorHpMul = 1.15f;
        public const int MaxEnhance = 15;
        public const float EnhanceHpPerLevel = 0.02f;
        public const int EnhanceFailStep = 5;
        public const int DwarfSuccessBonus = 10;
        public const int SlotCount = 6;

        public static readonly CraftRecipe[] Recipes =
        {
            new CraftRecipe { Id = "fang_sword", Name = "송곳니 검", Slot = EquipSlot.Weapon,
                Material = Economy.LifeItem.CraftFang, Cost = 5, BaseHpMul = 1.05f },
            new CraftRecipe { Id = "bone_helm", Name = "유골 투구", Slot = EquipSlot.Helm,
                Material = Economy.LifeItem.CraftBone, Cost = 5, BaseHpMul = 1.04f },
            new CraftRecipe { Id = LeatherArmorRecipe, Name = LeatherArmorName, Slot = EquipSlot.Armor,
                Material = Economy.LifeItem.CraftHide, Cost = LeatherArmorHideCost, BaseHpMul = LeatherArmorHpMul },
            new CraftRecipe { Id = "part_gloves", Name = "부품 장갑", Slot = EquipSlot.Gloves,
                Material = Economy.LifeItem.CraftPart, Cost = 5, BaseHpMul = 1.03f },
            new CraftRecipe { Id = "crystal_boots", Name = "원소 신발", Slot = EquipSlot.Boots,
                Material = Economy.LifeItem.CraftCrystal, Cost = 5, BaseHpMul = 1.03f },
            new CraftRecipe { Id = "demon_charm", Name = "마정 장신구", Slot = EquipSlot.Accessory,
                Material = Economy.LifeItem.CraftDemonite, Cost = 5, BaseHpMul = 1.04f },
        };

        const string K_GEAR = "ats.gear";
        public const string EnvShowUnlock = "QA_SMITH_UNLOCK";
        public const string EnvNoUnlock = "QA_NO_SMITH_UNLOCK";

        static List<GearItem> _items;
        static bool _loaded;
        static bool _unlockQaSeeded;

        public static IReadOnlyList<GearItem> All { get { Load(); return _items; } }

        public static string GradeLabel(GearGrade grade) => grade switch
        {
            GearGrade.Uncommon => "고급",
            GearGrade.Rare => "희귀",
            GearGrade.Heroic => "영웅",
            GearGrade.Legendary => "전설",
            _ => "일반",
        };

        public static string SlotName(EquipSlot slot) => slot switch
        {
            EquipSlot.Weapon => "무기",
            EquipSlot.Helm => "투구",
            EquipSlot.Armor => "갑옷",
            EquipSlot.Gloves => "장갑",
            EquipSlot.Boots => "신발",
            EquipSlot.Accessory => "장신구",
            _ => slot.ToString(),
        };

        public static CraftRecipe RecipeOf(string id)
        {
            for (int i = 0; i < Recipes.Length; i++)
                if (Recipes[i].Id == id) return Recipes[i];
            return null;
        }

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
                    Enhance = p.Length > 5 && int.TryParse(p[5], out int en) ? Mathf.Clamp(en, 0, MaxEnhance) : 0,
                    Grade = p.Length > 6 && Enum.TryParse(p[6], out GearGrade gd) ? gd : GearGrade.Common,
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
                  .Append(g.HpMul.ToString(inv)).Append('\t').Append(g.Enhance)
                  .Append('\t').Append(g.Grade).Append('\n');
            }
            PlayerPrefs.SetString(K_GEAR, sb.ToString());
            PlayerPrefs.Save();
        }

        public static bool UnlockBlocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNoUnlock);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>대장간 해금 = 1차 전직 시점(§13-2). 기본직업만 있으면 제작하지 않는다. QA_NO면 항상 연다.</summary>
        public static bool SmithUnlocked()
        {
            if (UnlockBlocked) return true;
            var roster = LifeSystem.GetCharacters();
            for (int i = 0; i < roster.Count; i++)
            {
                var ch = roster[i];
                if (ch != null && !ch.IsDeleted && ch.Advancement != AdvancementTier.Basic)
                    return true;
            }
            return false;
        }

        public static string LockReason()
        {
            if (SmithUnlocked()) return null;
            return "1차 전직 시 해금 — 기본직업만 있으면 제작하지 않는다(§13-2)";
        }

        public static string LockLine()
        {
            string why = LockReason();
            return string.IsNullOrEmpty(why) ? "대장간 해금(§13-2)" : why;
        }

        /// <summary>시각 QA. QA_SMITH_UNLOCK=1이면 기본직업만 남겨 잠긴 허브를 보여 준다.</summary>
        public static void SeedUnlockQaIfRequested()
        {
            string raw = Environment.GetEnvironmentVariable(EnvShowUnlock);
            if (raw != "1" && !string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase))
                return;
            if (UnlockBlocked) return;
            if (_unlockQaSeeded) return;
            _unlockQaSeeded = true;
            var roster = LifeSystem.GetCharacters();
            for (int i = 0; i < roster.Count; i++)
            {
                var ch = roster[i];
                if (ch == null || ch.IsDeleted) continue;
                if (ch.Advancement == AdvancementTier.Basic) continue;
                ch.Advancement = AdvancementTier.Basic;
            }
            LifeSystem.PersistRoster();
        }

        public static GearItem Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            Load();
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].Id == id) return _items[i];
            return null;
        }

        public static float EffectiveHpMul(GearItem gear)
        {
            if (gear == null || gear.HpMul <= 0f) return 1f;
            return gear.HpMul * (1f + Mathf.Clamp(gear.Enhance, 0, MaxEnhance) * EnhanceHpPerLevel);
        }

        public static int StoneCost(int enhance) => 1 + Mathf.Clamp(enhance, 0, MaxEnhance - 1);

        public static int SuccessPercent(int enhance, RaceId? race = null)
        {
            int pct = 100 - Mathf.Clamp(enhance, 0, MaxEnhance) * EnhanceFailStep;
            if ((race ?? RacePrefs.Get()) == RaceId.드워프) pct += DwarfSuccessBonus;
            return Mathf.Clamp(pct, 5, 100);
        }

        static bool WornByAnyone(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            var roster = LifeSystem.GetCharacters();
            for (int i = 0; i < roster.Count; i++)
                if (roster[i].Wears(id)) return true;
            return false;
        }

        public static List<GearItem> Unequipped()
        {
            Load();
            var list = new List<GearItem>();
            for (int i = 0; i < _items.Count; i++)
                if (!WornByAnyone(_items[i].Id)) list.Add(_items[i]);
            return list;
        }

        public static GearItem Worn(CharacterRecord character, EquipSlot slot)
        {
            if (character == null) return null;
            return Find(character.GetEquipped(slot));
        }

        public static List<GearItem> WornAll(CharacterRecord character)
        {
            var list = new List<GearItem>();
            if (character == null) return list;
            for (int i = 0; i < SlotCount; i++)
            {
                var g = Worn(character, (EquipSlot)i);
                if (g != null) list.Add(g);
            }
            return list;
        }

        public static float HpMulOf(CharacterRecord character)
        {
            if (character == null) return 1f;
            float mul = 1f;
            var worn = WornAll(character);
            for (int i = 0; i < worn.Count; i++)
                mul *= EffectiveHpMul(worn[i]);
            return mul;
        }

        public static GearItem FirstEnhanceable(CharacterRecord prefer = null)
        {
            if (prefer != null)
            {
                var mine = WornAll(prefer);
                for (int i = 0; i < mine.Count; i++)
                    if (mine[i].Enhance < MaxEnhance) return mine[i];
                return null;
            }
            var roster = LifeSystem.GetCharacters();
            for (int i = 0; i < roster.Count; i++)
            {
                if (roster[i].IsDeleted) continue;
                var found = FirstEnhanceable(roster[i]);
                if (found != null) return found;
            }
            return null;
        }

        public static int CountOfRecipe(string recipeId)
        {
            Load();
            int n = 0;
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].RecipeId == recipeId) n++;
            return n;
        }

        /// <summary>파산 압류·자가검사용. 대장간 해금 없이 비장착 장비를 넣는다.</summary>
        public static GearItem AddUnequippedForTest(string recipeId)
        {
            Load();
            var recipe = RecipeOf(recipeId);
            if (recipe == null) return null;
            var gear = new GearItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Slot = recipe.Slot,
                RecipeId = recipe.Id,
                Name = recipe.Name,
                HpMul = recipe.BaseHpMul,
                Enhance = 0,
                Grade = GearGrade.Common,
            };
            _items.Add(gear);
            Save();
            return gear;
        }

        public static bool TryCraftLeatherArmor() => TryCraft(LeatherArmorRecipe);

        public static bool TryCraft(string recipeId)
        {
            Load();
            if (!SmithUnlocked()) return false;
            var recipe = RecipeOf(recipeId);
            if (recipe == null) return false;
            if (GameState.Bag.GetCount(recipe.Material) < recipe.Cost) return false;
            if (!GameState.Consume(recipe.Material, recipe.Cost)) return false;

            _items.Add(new GearItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Slot = recipe.Slot,
                RecipeId = recipe.Id,
                Name = recipe.Name,
                HpMul = recipe.BaseHpMul,
                Enhance = 0,
                Grade = GearGrade.Common,
            });
            Save();
            return true;
        }

        /// <summary>
        /// 강화 시도. false = 시도 자체가 거부(석 부족·상한). true = 석을 썼다.
        /// 실패해도 장비는 남는다(§11). 파괴 분기는 없다.
        /// </summary>
        public static bool TryEnhance(string gearId, out bool success)
        {
            success = false;
            Load();
            var gear = Find(gearId);
            if (gear == null || gear.Enhance >= MaxEnhance) return false;
            int cost = StoneCost(gear.Enhance);
            if (GameState.Bag.GetCount(Economy.LifeItem.EnhanceStone) < cost) return false;
            if (!GameState.Consume(Economy.LifeItem.EnhanceStone, cost)) return false;

            string fail = Environment.GetEnvironmentVariable("QA_ENHANCE_FAIL");
            string ok = Environment.GetEnvironmentVariable("QA_ENHANCE_OK");
            if (fail == "1") success = false;
            else if (ok == "1") success = true;
            else success = UnityEngine.Random.Range(0, 100) < SuccessPercent(gear.Enhance);

            if (success)
            {
                gear.Enhance++;
                Save();
            }
            return true;
        }

        public static bool TryEquip(CharacterRecord character, string gearId)
        {
            Load();
            if (character == null || character.IsDeleted) return false;
            var gear = Find(gearId);
            if (gear == null) return false;

            var roster = LifeSystem.GetCharacters();
            for (int i = 0; i < roster.Count; i++)
            {
                if (roster[i].Wears(gearId))
                    roster[i].SetEquipped(gear.Slot, null);
                if (roster[i] == character)
                    roster[i].SetEquipped(gear.Slot, null);
            }
            character.SetEquipped(gear.Slot, gearId);
            LifeSystem.PersistRoster();
            return true;
        }

        public static bool TryUnequip(CharacterRecord character)
        {
            if (character == null) return false;
            if (!string.IsNullOrEmpty(character.EquippedArmorId))
                return TryUnequip(character, EquipSlot.Armor);
            for (int i = 0; i < SlotCount; i++)
                if (!string.IsNullOrEmpty(character.GetEquipped((EquipSlot)i)))
                    return TryUnequip(character, (EquipSlot)i);
            return false;
        }

        public static bool TryUnequip(CharacterRecord character, EquipSlot slot)
        {
            if (character == null || string.IsNullOrEmpty(character.GetEquipped(slot))) return false;
            character.SetEquipped(slot, null);
            LifeSystem.PersistRoster();
            return true;
        }

        /// <summary>삭제된 캐릭터의 장착 6부위는 함께 사라진다(§4·§11). 가방의 나머지 장비는 남는다.</summary>
        public static bool TryRemove(string gearId)
        {
            Load();
            if (string.IsNullOrEmpty(gearId) || WornByAnyone(gearId)) return false;
            int n = _items.RemoveAll(g => g.Id == gearId);
            if (n > 0) Save();
            return n > 0;
        }

        /// <summary>경매 취소·구매로 가방에 장비를 되돌린다. packed = recipeId|enhance.</summary>
        public static bool RestoreListed(string packed, string label)
        {
            Load();
            if (string.IsNullOrEmpty(packed)) return false;
            var parts = packed.Split('|');
            var rec = RecipeOf(parts[0]);
            if (rec == null) return false;
            int enh = 0;
            if (parts.Length > 1) int.TryParse(parts[1], out enh);
            _items.Add(new GearItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Slot = rec.Slot,
                RecipeId = rec.Id,
                Name = rec.Name,
                HpMul = rec.BaseHpMul,
                Enhance = Mathf.Clamp(enh, 0, MaxEnhance),
            });
            Save();
            return true;
        }

        public static void DestroyEquippedOn(CharacterRecord character)
        {
            Load();
            if (character == null) return;
            var ids = new HashSet<string>();
            for (int i = 0; i < SlotCount; i++)
            {
                string id = character.GetEquipped((EquipSlot)i);
                if (!string.IsNullOrEmpty(id)) ids.Add(id);
            }
            character.ClearEquipped();
            if (ids.Count == 0) return;
            _items.RemoveAll(g => ids.Contains(g.Id));
            Save();
        }

        public static string MaterialSummary()
        {
            return $"{GameState.Label(Economy.LifeItem.CraftHide)} {GameState.Bag.GetCount(Economy.LifeItem.CraftHide)} · " +
                   $"{GameState.Label(Economy.LifeItem.CraftFang)} {GameState.Bag.GetCount(Economy.LifeItem.CraftFang)} · " +
                   $"{GameState.Label(Economy.LifeItem.CraftBone)} {GameState.Bag.GetCount(Economy.LifeItem.CraftBone)} · " +
                   $"{GameState.Label(Economy.LifeItem.EnhanceStone)} {GameState.Bag.GetCount(Economy.LifeItem.EnhanceStone)}";
        }

        /// <summary>
        /// 시각 QA가 캐릭터 화면에 6칸을 보여 주게 제작·장착한다.
        /// DebugAutoPilot을 건드리지 않는다(대화 세션 소유).
        /// </summary>
        public static void SeedCraftedLoadoutForQa(CharacterRecord character)
        {
            if (character == null || character.IsDeleted) return;
            if (character.Advancement == AdvancementTier.Basic)
            {
                character.Advancement = AdvancementTier.First;
                if (character.Job == "탱") character.Job = "수호기사";
                LifeSystem.PersistRoster();
            }
            for (int i = 0; i < Recipes.Length; i++)
            {
                var rec = Recipes[i];
                if (CountOfRecipe(rec.Id) > 0) continue;
                int have = GameState.Bag.GetCount(rec.Material);
                if (have < rec.Cost) GameState.Gain(rec.Material, rec.Cost - have);
                TryCraft(rec.Id);
            }
            var bag = Unequipped();
            for (int i = 0; i < bag.Count; i++)
                TryEquip(character, bag[i].Id);
        }

        public static void Flush() => Save();

        public static void ForgetInMemoryForTest()
        {
            _items = null;
            _loaded = false;
        }

        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(K_GEAR);
            PlayerPrefs.Save();
            _unlockQaSeeded = false;
            ForgetInMemoryForTest();
        }
    }
}
