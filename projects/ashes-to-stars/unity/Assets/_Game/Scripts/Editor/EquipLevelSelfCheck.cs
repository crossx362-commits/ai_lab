using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>착용 레벨. 레시피 0=제한 없음. QA_NO면 옛 항상 허용(§11).</summary>
    public static class EquipLevelSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Equip Level Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(EquipLevel.EnvShow);
            string no = Environment.GetEnvironmentVariable(EquipLevel.EnvNo);
            Environment.SetEnvironmentVariable(EquipLevel.EnvShow, null);
            Environment.SetEnvironmentVariable(EquipLevel.EnvNo, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            EquipLevel.ResetForTest();

            Check(!EquipLevel.Blocked, "기본은 켜짐");
            Check(EquipLevel.Line().IndexOf("제한 없음", StringComparison.Ordinal) >= 0
                  && EquipLevel.Line().IndexOf("§11", StringComparison.Ordinal) >= 0,
                $"기본 줄 (실제 {EquipLevel.Line()})");

            for (int i = 0; i < Equipment.Recipes.Length; i++)
            {
                Check(Equipment.Recipes[i].요구레벨 == 0,
                    $"{Equipment.Recipes[i].Id} 요구레벨 0 (실제 {Equipment.Recipes[i].요구레벨})");
            }

            var roster = LifeSystem.GetCharacters();
            Check(roster.Count > 0, "명부 1명 이상");
            CharacterRecord lv1 = null;
            for (int i = 0; i < roster.Count; i++)
            {
                if (roster[i].IsDeleted) continue;
                lv1 = roster[i];
                break;
            }
            Check(lv1 != null, "살아 있는 캐릭터");
            if (lv1 != null) lv1.Level = 1;

            var armor = Equipment.AddUnequippedForTest(EquipLevel.QaRecipeId);
            Check(armor != null && armor.RecipeId == EquipLevel.QaRecipeId,
                "가죽 흉갑 시드");
            Check(EquipLevel.CanWear(lv1, armor), "요구 0이면 Lv1도 입는다");
            Check(string.IsNullOrEmpty(EquipLevel.WhyNot(lv1, armor)), "WhyNot 빈 칸");
            Check(Equipment.TryEquip(lv1, armor.Id)
                  && Equipment.Worn(lv1, EquipSlot.Armor) != null,
                "기본 TryEquip 성공");
            Equipment.TryUnequip(lv1, EquipSlot.Armor);

            var rec = Equipment.RecipeOf(EquipLevel.QaRecipeId);
            int oldReq = rec.요구레벨;
            rec.요구레벨 = 10;
            armor = FindBag(EquipLevel.QaRecipeId) ?? Equipment.AddUnequippedForTest(EquipLevel.QaRecipeId);
            Check(!EquipLevel.CanWear(lv1, armor), "authored 10은 Lv1 거부");
            Check(EquipLevel.WhyNot(lv1, armor).IndexOf("Lv10", StringComparison.Ordinal) >= 0
                  && EquipLevel.WhyNot(lv1, armor).IndexOf("§11", StringComparison.Ordinal) >= 0,
                $"authored WhyNot (실제 {EquipLevel.WhyNot(lv1, armor)})");
            int saved = lv1.Level;
            lv1.Level = 10;
            Check(EquipLevel.CanWear(lv1, armor), "authored 10은 Lv10 허용");
            lv1.Level = saved;
            rec.요구레벨 = oldReq;

            EquipLevel.ForceReq = 20;
            armor = FindBag(EquipLevel.QaRecipeId) ?? Equipment.AddUnequippedForTest(EquipLevel.QaRecipeId);
            Check(!EquipLevel.CanWear(lv1, armor), "ForceReq 20은 Lv1 거부");
            Check(!Equipment.TryEquip(lv1, armor.Id)
                  && Equipment.Worn(lv1, EquipSlot.Armor) == null,
                "ForceReq면 TryEquip 실패");
            Check(EquipLevel.WhyNot(lv1, armor).IndexOf("Lv20", StringComparison.Ordinal) >= 0,
                $"ForceReq WhyNot (실제 {EquipLevel.WhyNot(lv1, armor)})");
            lv1.Level = 20;
            Check(EquipLevel.CanWear(lv1, armor), "ForceReq 20은 Lv20 허용");
            Check(Equipment.TryEquip(lv1, armor.Id)
                  && Equipment.Worn(lv1, EquipSlot.Armor) != null,
                "Lv20 TryEquip 성공");
            Equipment.TryUnequip(lv1, EquipSlot.Armor);
            lv1.Level = 1;
            EquipLevel.ForceReq = 0;

            EquipLevel.ResetForTest();
            Environment.SetEnvironmentVariable(EquipLevel.EnvNo, "1");
            EquipLevel.ForceReq = 20;
            armor = FindBag(EquipLevel.QaRecipeId) ?? Equipment.AddUnequippedForTest(EquipLevel.QaRecipeId);
            Check(EquipLevel.Blocked, "QA_NO");
            Check(EquipLevel.CanWear(lv1, armor), "QA_NO면 Lv1도 입는다");
            Check(EquipLevel.Line().IndexOf("없음", StringComparison.Ordinal) >= 0,
                $"QA_NO 줄 (실제 {EquipLevel.Line()})");
            Environment.SetEnvironmentVariable(EquipLevel.EnvNo, null);
            EquipLevel.ResetForTest();

            Environment.SetEnvironmentVariable(EquipLevel.EnvShow, "1");
            EquipLevel.SeedQaIfRequested();
            Check(EquipLevel.ShowQa, "시드 ShowQa");
            Check(EquipLevel.ForceReq >= EquipLevel.QaReq, $"시드 ForceReq {EquipLevel.ForceReq}");
            Check(EquipLevel.Line().IndexOf("Lv", StringComparison.Ordinal) >= 0
                  && EquipLevel.Line().IndexOf("§11", StringComparison.Ordinal) >= 0,
                $"시드 줄 (실제 {EquipLevel.Line()})");
            int ci = EquipLevel.QaCharIndex();
            Check(ci >= 0, "시드 캐릭터");
            bool seededArmor = false;
            var bag = Equipment.Unequipped();
            for (int i = 0; i < bag.Count; i++)
                if (bag[i].RecipeId == EquipLevel.QaRecipeId) seededArmor = true;
            Check(seededArmor, "시드 가방 가죽 흉갑");
            Check(EquipLevel.SeedWhyNot().IndexOf("Lv", StringComparison.Ordinal) >= 0,
                $"시드 거부 (실제 {EquipLevel.SeedWhyNot()})");
            if (ci >= 0)
                Check(Equipment.Worn(LifeSystem.GetCharacters()[ci], EquipSlot.Armor) == null,
                    "시드 캐릭터는 갑옷을 안 입음");
            Environment.SetEnvironmentVariable(EquipLevel.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string equipSrc = File.ReadAllText(Path.Combine(runtime, "Equipment.cs"));
            string charSrc = File.ReadAllText(Path.Combine(runtime, "CharacterScreen.cs"));
            string recSrc = File.ReadAllText(Path.Combine(runtime, "Equipment.cs"));
            Check(equipSrc.IndexOf("EquipLevel.CanWear", StringComparison.Ordinal) >= 0,
                "TryEquip이 CanWear를 읽는다");
            Check(charSrc.IndexOf("EquipLevel.SeedQaIfRequested", StringComparison.Ordinal) >= 0
                  && charSrc.IndexOf("EquipLevel.Line", StringComparison.Ordinal) >= 0,
                "캐릭터창이 시드·줄을 읽는다");
            Check(recSrc.IndexOf("public int 요구레벨", StringComparison.Ordinal) >= 0,
                "CraftRecipe.요구레벨 필드");

            _ = nameof(EquipLevel.CanWear);
            _ = nameof(EquipLevel.WhyNot);
            _ = nameof(EquipLevel.Line);
            _ = nameof(EquipLevel.SeedQaIfRequested);
            _ = nameof(CraftRecipe.요구레벨);

            Environment.SetEnvironmentVariable(EquipLevel.EnvShow, show);
            Environment.SetEnvironmentVariable(EquipLevel.EnvNo, no);
            EquipLevel.ResetForTest();
            Equipment.ResetAll();
            GameState.ResetAll();
            LifeSystem.ResetAll();

            if (_fail == 0) Debug.Log("[EquipLevelSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[EquipLevelSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[EquipLevelSelfCheck] FAIL {_fail}건");
        }

        static GearItem FindBag(string recipeId)
        {
            var bag = Equipment.Unequipped();
            for (int i = 0; i < bag.Count; i++)
                if (bag[i].RecipeId == recipeId) return bag[i];
            return null;
        }
    }
}
