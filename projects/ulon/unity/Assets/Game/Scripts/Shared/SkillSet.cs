using System;
using System.Collections.Generic;

namespace Ulon.Shared
{
    public sealed class SkillSet
    {
        public const float IndividualCap = 100f;
        public const float TotalCap = 700f;

        readonly float[] values = new float[(int)SkillId.Count];
        readonly SkillLock[] locks = new SkillLock[(int)SkillId.Count];

        public float Get(SkillId id) => values[(int)id];
        public SkillLock GetLock(SkillId id) => locks[(int)id];

        public void SetLock(SkillId id, SkillLock state) => locks[(int)id] = state;

        public void CycleLock(SkillId id)
        {
            locks[(int)id] = SkillLockMarks.Next(locks[(int)id]);
        }

        public float Total
        {
            get
            {
                float sum = 0f;
                for (int i = 0; i < values.Length; i++)
                    sum += values[i];
                return sum;
            }
        }

        public bool TrySet(SkillId id, float value)
        {
            int idx = (int)id;
            if (locks[idx] == SkillLock.Locked)
                return false;
            float clamped = Math.Clamp(value, 0f, IndividualCap);
            float delta = clamped - values[idx];
            if (delta > 0.0001f)
            {
                float overflow = Total + delta - TotalCap;
                if (overflow > 0.0001f && !DrainDown(idx, overflow))
                    return false;
            }
            values[idx] = clamped;
            return true;
        }

        bool DrainDown(int except, float need)
        {
            for (int i = 0; i < values.Length && need > 0.0001f; i++)
            {
                if (i == except || locks[i] != SkillLock.Down || values[i] <= 0f)
                    continue;
                float take = Math.Min(values[i], need);
                values[i] -= take;
                need -= take;
            }
            return need <= 0.0001f;
        }

        public void ForceSet(SkillId id, float value, SkillLock lockState)
        {
            values[(int)id] = Math.Clamp(value, 0f, IndividualCap);
            locks[(int)id] = lockState;
        }

        public void WriteTo(List<SkillRecord> dest)
        {
            dest.Clear();
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] <= 0f && locks[i] == SkillLock.Up)
                    continue;
                dest.Add(new SkillRecord { Id = i, Value = values[i], Lock = (int)locks[i] });
            }
        }

        public void ReadFrom(IList<SkillRecord> src)
        {
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = 0f;
                locks[i] = SkillLock.Up;
            }
            if (src == null)
                return;
            for (int i = 0; i < src.Count; i++)
            {
                var rec = src[i];
                if (rec.Id < 0 || rec.Id >= values.Length)
                    continue;
                ForceSet((SkillId)rec.Id, rec.Value, (SkillLock)rec.Lock);
            }
        }
    }

    [Serializable]
    public struct SkillRecord
    {
        public int Id;
        public float Value;
        public int Lock;
    }

    [Serializable]
    public struct ItemRecord
    {
        public int Slot;
        public string TemplateId;
        public int Amount;
        public int Uses;
    }

    public static class ItemCatalog
    {
        public const string Pickaxe = "pickaxe";
        public const string Hatchet = "hatchet";
        public const string IronSword = "iron_sword";
        public const string WoodenClub = "wooden_club";
        public const string WoodenBow = "wooden_bow";
        public const string WoodenShield = "wooden_shield";
        public const string Cloth = "cloth";
        public const string Bandage = "bandage";
        public const float MeleeRange = 2.4f;
        public const float ArcheryRange = 8f;

        public static float WeightOf(string id)
        {
            if (id == "iron_ore") return 2f;
            if (id == "wood") return 2f;
            if (id == "resin") return 0.2f;
            if (id == IronSword) return 8f;
            if (id == WoodenClub) return 5f;
            if (id == WoodenBow) return 6f;
            if (id == WoodenShield) return 6f;
            if (id == Pickaxe || id == Hatchet) return 6f;
            if (id == Cloth) return 0.5f;
            if (id == Bandage) return 0.1f;
            return 1f;
        }

        public static float WeightOf(ItemRecord rec)
        {
            int n = rec.Amount < 1 ? 1 : rec.Amount;
            return WeightOf(rec.TemplateId) * n;
        }

        public static float WeightOf(IList<ItemRecord> items)
        {
            float w = 0f;
            if (items == null)
                return 0f;
            for (int i = 0; i < items.Count; i++)
                w += WeightOf(items[i]);
            return w;
        }

        public static int CarryCap(int str) => str * 4 < 10 ? 10 : str * 4;

        public static int StrReqOf(string id) => id == IronSword ? 25 : 0;

        public static int MaxUsesOf(string id)
        {
            if (id == Pickaxe || id == Hatchet) return 20;
            if (id == IronSword) return 40;
            if (id == WoodenClub) return 30;
            if (id == WoodenBow) return 30;
            if (id == WoodenShield) return 30;
            return 0;
        }

        public static bool Stackable(string id) => MaxUsesOf(id) <= 0;

        public static int BuyPrice(string id)
        {
            if (id == Pickaxe || id == Hatchet) return 25;
            if (id == IronSword) return 40;
            if (id == WoodenClub) return 18;
            if (id == WoodenBow) return 22;
            if (id == WoodenShield) return 20;
            if (id == Cloth) return 3;
            if (id == Bandage) return 5;
            if (id == "resin") return 4;
            if (id == "iron_ore") return 6;
            if (id == "wood") return 5;
            return 0;
        }

        public static int SellPrice(string id)
        {
            int buy = BuyPrice(id);
            return buy <= 0 ? 0 : buy / 3;
        }

        public static string ToolFor(SkillId gather)
        {
            if (gather == SkillId.Mining) return Pickaxe;
            if (gather == SkillId.Lumberjacking) return Hatchet;
            return "";
        }

        public static bool Has(IList<ItemRecord> items, string id)
        {
            if (items == null || string.IsNullOrEmpty(id))
                return false;
            for (int i = 0; i < items.Count; i++)
                if (items[i].TemplateId == id && items[i].Amount > 0)
                    return true;
            return false;
        }

        public static bool HasShield(IList<ItemRecord> items) => Has(items, WoodenShield);

        public static bool IsHeavyArmor(string id) => id == "iron_plate";

        public static bool HasHeavyArmor(IList<ItemRecord> items)
        {
            if (items == null)
                return false;
            for (int i = 0; i < items.Count; i++)
                if (IsHeavyArmor(items[i].TemplateId) && items[i].Amount > 0)
                    return true;
            return false;
        }

        public static int EquipmentMagicResist(IList<ItemRecord> items)
        {
            return HasHeavyArmor(items) ? 2 : 0;
        }

        public static string CombatWeaponOf(IList<ItemRecord> items)
        {
            if (Has(items, WoodenBow))
                return WoodenBow;
            if (Has(items, IronSword))
                return IronSword;
            return "";
        }

        public static SkillId CombatSkillOf(string weapon)
        {
            return weapon == WoodenBow ? SkillId.Archery : SkillId.Swordsmanship;
        }

        public static float CombatRangeOf(SkillId skill)
        {
            return skill == SkillId.Archery ? ArcheryRange : MeleeRange;
        }
    }


    public sealed class CraftRecipe
    {
        public string Id;
        public string Ingredient;
        public int Count;
        public string Output;
        public SkillId Skill;
        public float Difficulty;
        public bool CanRepair;
    }

    public static class CraftRecipes
    {
        static readonly CraftRecipe[] All =
        {
            new CraftRecipe
            {
                Id = "iron_sword",
                Ingredient = "iron_ore",
                Count = 2,
                Output = ItemCatalog.IronSword,
                Skill = SkillId.Blacksmithing,
                Difficulty = 15f,
                CanRepair = true
            },
            new CraftRecipe
            {
                Id = "wooden_club",
                Ingredient = "wood",
                Count = 2,
                Output = ItemCatalog.WoodenClub,
                Skill = SkillId.Carpentry,
                Difficulty = 12f
            },
            new CraftRecipe
            {
                Id = "wooden_bow",
                Ingredient = "wood",
                Count = 3,
                Output = ItemCatalog.WoodenBow,
                Skill = SkillId.Carpentry,
                Difficulty = 14f
            },
            new CraftRecipe
            {
                Id = "bandage",
                Ingredient = ItemCatalog.Cloth,
                Count = 1,
                Output = ItemCatalog.Bandage,
                Skill = SkillId.Tailoring,
                Difficulty = 8f
            }
        };

        public static CraftRecipe Find(string id)
        {
            if (string.IsNullOrEmpty(id))
                return All[0];
            for (int i = 0; i < All.Length; i++)
                if (All[i].Id == id)
                    return All[i];
            return null;
        }
    }

    [Serializable]
    public sealed class CharacterSnapshot
    {
        public string AccountId;
        public string CharacterId;
        public string Name;
        public float X, Y, Z;
        public float Hp;
        public int Str = 30;
        public int Dex = 25;
        public int Int = 25;
        public int StrLock;
        public int DexLock;
        public int IntLock;
        public SkillRecord[] Skills = Array.Empty<SkillRecord>();
        public ItemRecord[] Inventory = Array.Empty<ItemRecord>();
        public ItemRecord[] Bank = Array.Empty<ItemRecord>();
        public int Appearance;
        public int[] Spells = Array.Empty<int>();
        public float Mana;
        public bool Ghost;
        public string CorpseId = "";
        public float CorpseX, CorpseY, CorpseZ;
        public ItemRecord[] Corpse = Array.Empty<ItemRecord>();
        public int Gold;
        public int Fame;
        public int Karma;
        public int Notoriety;
        public int MurderCount;
    }

    public static class NotorietyId
    {
        public const int Innocent = 0;
        public const int Criminal = 1;
        public const int Murderer = 2;

        public static string Korean(int n)
        {
            if (n == Criminal)
                return "범죄";
            if (n == Murderer)
                return "살인";
            return "무고";
        }
    }

    public static class GuardZone
    {
        public const float Radius = 16f;

        public static bool Contains(float x, float z)
        {
            return (x * x) + (z * z) <= Radius * Radius;
        }
    }

    public static class CharacterCreate
    {
        public const int StatTotal = 80;
        public const int StatMin = 10;
        public const int StatEachMax = 50;
        public const int SkillTotal = 100;
        public const int SkillEachMax = 50;
        public const int SkillPicks = 3;

        public static string Validate(string name, int str, int dex, int intel, SkillId[] picks, float[] values)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 12)
                return "이름은 1~12자";
            if (str < StatMin || dex < StatMin || intel < StatMin)
                return "스탯 최소 " + StatMin;
            if (str > StatEachMax || dex > StatEachMax || intel > StatEachMax)
                return "스탯 개별 최대 " + StatEachMax;
            if (str + dex + intel != StatTotal)
                return "스탯 총합 " + StatTotal;
            if (picks == null || values == null || picks.Length != SkillPicks || values.Length != SkillPicks)
                return "시작 스킬 " + SkillPicks + "개";
            float sum = 0f;
            for (int i = 0; i < SkillPicks; i++)
            {
                if ((int)picks[i] < 0 || (int)picks[i] >= (int)SkillId.Count)
                    return "잘못된 스킬";
                if (values[i] < 1f || values[i] > SkillEachMax)
                    return "시작 스킬 1~" + SkillEachMax;
                sum += values[i];
                for (int j = 0; j < i; j++)
                    if (picks[j] == picks[i])
                        return "스킬 중복";
            }
            if (Math.Abs(sum - SkillTotal) > 0.01f)
                return "시작 스킬 총합 " + SkillTotal;
            return null;
        }

        public static CharacterSnapshot Build(string accountId, string name, int appearance, int str, int dex, int intel, SkillId[] picks, float[] values)
        {
            string err = Validate(name, str, dex, intel, picks, values);
            if (err != null)
                throw new InvalidOperationException(err);
            var skills = new SkillRecord[SkillPicks];
            for (int i = 0; i < SkillPicks; i++)
                skills[i] = new SkillRecord { Id = (int)picks[i], Value = values[i], Lock = 0 };
            return new CharacterSnapshot
            {
                AccountId = accountId,
                CharacterId = accountId,
                Name = name.Trim(),
                Hp = StatSet.MaxHpOf(str),
                Str = str,
                Dex = dex,
                Int = intel,
                Skills = skills,
                Inventory = StarterItems(picks),
                Bank = Array.Empty<ItemRecord>(),
                Appearance = appearance,
                Spells = HasPick(picks, SkillId.Magery) ? new[] { (int)SpellId.Ember, (int)SpellId.Mend } : Array.Empty<int>(),
                Mana = StatSet.MaxManaOf(intel),
                Gold = 40
            };
        }

        public static ItemRecord[] StarterItems(SkillId[] picks)
        {
            var list = new List<ItemRecord>();
            if (HasPick(picks, SkillId.Swordsmanship))
                list.Add(Tool(ItemCatalog.IronSword));
            if (HasPick(picks, SkillId.Archery))
                list.Add(Tool(ItemCatalog.WoodenBow));
            if (HasPick(picks, SkillId.Parrying))
                list.Add(Tool(ItemCatalog.WoodenShield));
            if (HasPick(picks, SkillId.Mining))
                list.Add(Tool(ItemCatalog.Pickaxe));
            if (HasPick(picks, SkillId.Lumberjacking))
                list.Add(Tool(ItemCatalog.Hatchet));
            if (HasPick(picks, SkillId.Blacksmithing))
                list.Add(new ItemRecord { Slot = list.Count, TemplateId = "iron_ore", Amount = 2 });
            if (HasPick(picks, SkillId.Carpentry))
                list.Add(new ItemRecord { Slot = list.Count, TemplateId = "wood", Amount = 2 });
            if (HasPick(picks, SkillId.Magery))
                list.Add(new ItemRecord { Slot = list.Count, TemplateId = SpellCast.Reagent, Amount = 8 });
            if (HasPick(picks, SkillId.Healing))
                list.Add(new ItemRecord { Slot = list.Count, TemplateId = ItemCatalog.Bandage, Amount = 10 });
            if (HasPick(picks, SkillId.Tailoring))
                list.Add(new ItemRecord { Slot = list.Count, TemplateId = ItemCatalog.Cloth, Amount = 4 });
            for (int i = 0; i < list.Count; i++)
            {
                var it = list[i];
                it.Slot = i;
                list[i] = it;
            }
            return list.ToArray();
        }

        static ItemRecord Tool(string id)
        {
            return new ItemRecord { TemplateId = id, Amount = 1, Uses = ItemCatalog.MaxUsesOf(id) };
        }

        static bool HasPick(SkillId[] picks, SkillId id)
        {
            if (picks == null)
                return false;
            for (int i = 0; i < picks.Length; i++)
                if (picks[i] == id)
                    return true;
            return false;
        }
    }
}
