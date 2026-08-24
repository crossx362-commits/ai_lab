using System;

namespace AshesToStars
{
    /// <summary>
    /// 장비 착용 레벨 제한(§11). 레시피 <see cref="CraftRecipe.요구레벨"/>이 소비처.
    /// 오너 확정치가 없어 레시피는 전부 0(제한 없음) — 수치표를 만들지 않는다.
    /// QA_NO면 옛 항상 허용. 갑옷으로 시드해 직업 계열과 겹치지 않는다.
    /// </summary>
    public static class EquipLevel
    {
        public const string EnvShow = "QA_EQUIP_LV";
        public const string EnvNo = "QA_NO_EQUIP_LV";
        public const int QaReq = 20;
        public const string QaRecipeId = Equipment.LeatherArmorRecipe;

        static bool _qaSeeded;
        static int _forceReq;

        public static int ForceReq
        {
            get { return _forceReq; }
            set { _forceReq = value < 0 ? 0 : value; }
        }

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool ShowQa
        {
            get
            {
                if (Blocked) return false;
                string raw = Environment.GetEnvironmentVariable(EnvShow);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static string Line()
        {
            if (Blocked) return "착용 레벨 없음";
            int need = ShownNeed();
            if (need > 0) return $"착용 레벨 — Lv{need}(§11)";
            return "착용 레벨 — 제한 없음(§11)";
        }

        static int ShownNeed()
        {
            if (ForceReq > 0) return ForceReq;
            int max = 0;
            for (int i = 0; i < Equipment.Recipes.Length; i++)
            {
                int n = Equipment.Recipes[i].요구레벨;
                if (n > max) max = n;
            }
            return max;
        }

        public static int RequiredOf(GearItem gear)
        {
            if (Blocked || gear == null) return 0;
            if (ForceReq > 0 && gear.RecipeId == QaRecipeId) return ForceReq;
            var rec = Equipment.RecipeOf(gear.RecipeId);
            return rec == null ? 0 : rec.요구레벨;
        }

        public static bool CanWear(CharacterRecord character, GearItem gear)
        {
            if (Blocked) return true;
            if (character == null || gear == null) return false;
            int need = RequiredOf(gear);
            if (need <= 0) return true;
            return character.Level >= need;
        }

        public static string WhyNot(CharacterRecord character, GearItem gear)
        {
            if (CanWear(character, gear)) return "";
            int need = RequiredOf(gear);
            string name = gear == null ? "장비" : gear.Name;
            return $"{name}은 Lv{need}(§11)";
        }

        /// <summary>시각 QA. QA_EQUIP_LV=1이면 선택 캐릭터보다 높은 요구의 가죽 흉갑.</summary>
        public static void SeedQaIfRequested()
        {
            if (!ShowQa) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            var roster = LifeSystem.GetCharacters();
            CharacterRecord ch = null;
            for (int i = 0; i < roster.Count; i++)
            {
                if (roster[i].IsDeleted) continue;
                ch = roster[i];
                break;
            }
            if (ch == null) return;
            ForceReq = Math.Max(QaReq, ch.Level + 1);
            Equipment.TryUnequip(ch, EquipSlot.Armor);
            bool have = false;
            var bag = Equipment.Unequipped();
            for (int i = 0; i < bag.Count; i++)
            {
                if (bag[i].RecipeId == QaRecipeId) { have = true; break; }
            }
            if (!have) Equipment.AddUnequippedForTest(QaRecipeId);
        }

        public static int QaCharIndex()
        {
            var roster = LifeSystem.GetCharacters();
            for (int i = 0; i < roster.Count; i++)
            {
                if (roster[i].IsDeleted) continue;
                return i;
            }
            return roster.Count > 0 ? 0 : -1;
        }

        public static string SeedWhyNot()
        {
            int idx = QaCharIndex();
            if (idx < 0) return "";
            var roster = LifeSystem.GetCharacters();
            var ch = roster[idx];
            var bag = Equipment.Unequipped();
            for (int i = 0; i < bag.Count; i++)
            {
                if (bag[i].RecipeId != QaRecipeId) continue;
                return WhyNot(ch, bag[i]);
            }
            return "";
        }

        public static void ResetForTest()
        {
            _qaSeeded = false;
            _forceReq = 0;
        }
    }
}
