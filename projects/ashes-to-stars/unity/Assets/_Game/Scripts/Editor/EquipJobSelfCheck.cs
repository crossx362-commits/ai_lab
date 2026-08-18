using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>무기는 직업 계열만. QA_NO면 옛 항상 허용(§11).</summary>
    public static class EquipJobSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Equip Job Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(EquipJob.EnvShow);
            string no = Environment.GetEnvironmentVariable(EquipJob.EnvNo);
            Environment.SetEnvironmentVariable(EquipJob.EnvShow, null);
            Environment.SetEnvironmentVariable(EquipJob.EnvNo, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            EquipJob.ResetForTest();

            Check(!EquipJob.Blocked, "기본은 켜짐");
            Check(EquipJob.LineOfJob("탱") == "탱", "기본 탱");
            Check(EquipJob.LineOfJob("수호기사") == "탱", "수호기사=탱");
            Check(EquipJob.LineOfJob("광전사") == "탱", "광전사=탱");
            Check(EquipJob.LineOfJob("검사") == "딜", "검사=딜");
            Check(EquipJob.LineOfJob("궁수") == "딜", "궁수=딜");
            Check(EquipJob.LineOfJob("마법사") == "마딜", "마법사=마딜");
            Check(EquipJob.LineOfJob("소환사") == "마딜", "소환사=마딜");
            Check(EquipJob.LineOfJob("사제") == "힐", "사제=힐");
            Check(EquipJob.LineOfJob("드루이드") == "힐", "드루이드=힐");
            Check(EquipJob.LineOfJob("음유시인") == "버퍼", "음유시인=버퍼");
            Check(EquipJob.Matches(EquipJob.Physical, "탱")
                  && EquipJob.Matches(EquipJob.Physical, "딜")
                  && !EquipJob.Matches(EquipJob.Physical, "힐"),
                "물리는 탱·딜만");

            var rec = Equipment.RecipeOf(EquipJob.FangSwordId);
            Check(rec != null && rec.Slot == EquipSlot.Weapon
                  && rec.JobLine == EquipJob.Physical,
                $"송곳니 검은 물리 (실제 {rec?.JobLine})");
            Check(Equipment.RecipeOf(Equipment.LeatherArmorRecipe).JobLine == ""
                  || string.IsNullOrEmpty(Equipment.RecipeOf(Equipment.LeatherArmorRecipe).JobLine),
                "흉갑은 공용");

            var roster = LifeSystem.GetCharacters();
            CharacterRecord tank = null, dps = null, mage = null, healer = null, buffer = null;
            for (int i = 0; i < roster.Count; i++)
            {
                string line = EquipJob.LineOf(roster[i]);
                if (line == "탱" && tank == null) tank = roster[i];
                if (line == "딜" && dps == null) dps = roster[i];
                if (line == "마딜" && mage == null) mage = roster[i];
                if (line == "힐" && healer == null) healer = roster[i];
                if (line == "버퍼" && buffer == null) buffer = roster[i];
            }
            Check(tank != null && dps != null && mage != null && healer != null && buffer != null,
                "기본 5계열");

            var sword = Equipment.AddUnequippedForTest(EquipJob.FangSwordId);
            var armor = Equipment.AddUnequippedForTest(Equipment.LeatherArmorRecipe);
            Check(EquipJob.CanWear(tank, sword) && EquipJob.CanWear(dps, sword),
                "탱·딜은 송곳니 검");
            Check(!EquipJob.CanWear(healer, sword)
                  && !EquipJob.CanWear(mage, sword)
                  && !EquipJob.CanWear(buffer, sword),
                "힐·마딜·버퍼는 송곳니 검 거부");
            Check(EquipJob.CanWear(healer, armor) && EquipJob.CanWear(tank, armor),
                "흉갑은 힐·탱 공용");
            Check(EquipJob.WhyNot(healer, sword).IndexOf("물리", StringComparison.Ordinal) >= 0
                  && EquipJob.WhyNot(healer, sword).IndexOf("§11", StringComparison.Ordinal) >= 0,
                $"거부 줄 (실제 {EquipJob.WhyNot(healer, sword)})");
            Check(string.IsNullOrEmpty(EquipJob.WhyNot(tank, sword)), "탱 WhyNot 빈 칸");

            Check(Equipment.TryEquip(healer, sword.Id) == false, "힐러 TryEquip 검 거부");
            Check(Equipment.Worn(healer, EquipSlot.Weapon) == null, "힐러 무기 칸 비움");
            Check(Equipment.TryEquip(tank, sword.Id), "탱 TryEquip 검");
            Check(Equipment.Worn(tank, EquipSlot.Weapon) != null
                  && Equipment.Worn(tank, EquipSlot.Weapon).RecipeId == EquipJob.FangSwordId,
                "탱이 검을 참");
            Check(Equipment.TryEquip(healer, armor.Id), "힐러 흉갑");
            Equipment.TryUnequip(tank, EquipSlot.Weapon);
            Equipment.TryUnequip(healer, EquipSlot.Armor);

            tank.Job = "수호기사";
            tank.Advancement = AdvancementTier.First;
            LifeSystem.PersistRoster();
            Equipment.SeedCraftedLoadoutForQa(tank);
            Check(Equipment.Worn(tank, EquipSlot.Weapon) != null
                  && Equipment.Worn(tank, EquipSlot.Weapon).RecipeId == EquipJob.FangSwordId,
                "시드 수호기사도 검을 참 — LastLife 회귀");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            EquipJob.ResetForTest();
            roster = LifeSystem.GetCharacters();
            healer = null;
            tank = null;
            for (int i = 0; i < roster.Count; i++)
            {
                if (EquipJob.LineOf(roster[i]) == "힐" && healer == null) healer = roster[i];
                if (EquipJob.LineOf(roster[i]) == "탱" && tank == null) tank = roster[i];
            }
            Environment.SetEnvironmentVariable(EquipJob.EnvNo, "1");
            Check(EquipJob.Blocked, "QA_NO");
            sword = Equipment.AddUnequippedForTest(EquipJob.FangSwordId);
            Check(EquipJob.CanWear(healer, sword), "QA_NO면 힐러도 검");
            Check(Equipment.TryEquip(healer, sword.Id), "QA_NO TryEquip 허용");
            Check(Equipment.Worn(healer, EquipSlot.Weapon) != null, "QA_NO면 참다");
            Check(EquipJob.Line().IndexOf("아무나", StringComparison.Ordinal) >= 0,
                $"QA_NO 줄 (실제 {EquipJob.Line()})");
            Environment.SetEnvironmentVariable(EquipJob.EnvNo, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            EquipJob.ResetForTest();
            Environment.SetEnvironmentVariable(EquipJob.EnvShow, "1");
            EquipJob.SeedQaIfRequested();
            Check(EquipJob.ShowQa, "시드 ShowQa");
            Check(EquipJob.Line().IndexOf("직업 계열", StringComparison.Ordinal) >= 0,
                $"시드 줄 (실제 {EquipJob.Line()})");
            int hi = EquipJob.QaHealerIndex();
            Check(hi >= 0 && EquipJob.LineOf(LifeSystem.GetCharacters()[hi]) == "힐",
                $"시드 힐러 칸 {hi}");
            bool seededSword = false;
            var bag = Equipment.Unequipped();
            for (int i = 0; i < bag.Count; i++)
                if (bag[i].RecipeId == EquipJob.FangSwordId) seededSword = true;
            Check(seededSword, "시드 가방 송곳니 검");
            Check(Equipment.Worn(LifeSystem.GetCharacters()[hi], EquipSlot.Weapon) == null,
                "시드 힐러는 검을 안 참");
            Environment.SetEnvironmentVariable(EquipJob.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string equipSrc = File.ReadAllText(Path.Combine(runtime, "Equipment.cs"));
            string charSrc = File.ReadAllText(Path.Combine(runtime, "CharacterScreen.cs"));
            string recSrc = File.ReadAllText(Path.Combine(runtime, "Equipment.cs"));
            Check(equipSrc.IndexOf("EquipJob.CanWear", StringComparison.Ordinal) >= 0,
                "TryEquip이 CanWear를 읽는다");
            Check(charSrc.IndexOf("EquipJob.SeedQaIfRequested", StringComparison.Ordinal) >= 0
                  && charSrc.IndexOf("EquipJob.Line", StringComparison.Ordinal) >= 0,
                "캐릭터창이 시드·줄을 읽는다");
            Check(recSrc.IndexOf("JobLine = EquipJob.Physical", StringComparison.Ordinal) >= 0
                  || recSrc.IndexOf("JobLine = \"물리\"", StringComparison.Ordinal) >= 0,
                "송곳니 검 레시피가 물리다");

            _ = nameof(EquipJob.CanWear);
            _ = nameof(EquipJob.WhyNot);
            _ = nameof(EquipJob.Line);
            _ = nameof(EquipJob.SeedQaIfRequested);

            Environment.SetEnvironmentVariable(EquipJob.EnvShow, show);
            Environment.SetEnvironmentVariable(EquipJob.EnvNo, no);
            EquipJob.ResetForTest();
            Equipment.ResetAll();
            GameState.ResetAll();
            LifeSystem.ResetAll();

            if (_fail == 0) Debug.Log("[EquipJobSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[EquipJobSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[EquipJobSelfCheck] FAIL {_fail}건");
        }
    }
}
