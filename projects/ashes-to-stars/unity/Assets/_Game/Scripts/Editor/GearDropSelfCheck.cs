using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>보스 주 드랍은 고급 장비. QA_NO면 옛 0(§10-8).</summary>
    public static class GearDropSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Gear Drop Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(GearDrop.EnvShow);
            string no = Environment.GetEnvironmentVariable(GearDrop.EnvNo);
            Environment.SetEnvironmentVariable(GearDrop.EnvShow, null);
            Environment.SetEnvironmentVariable(GearDrop.EnvNo, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            GearDrop.ResetForTest();
            _ = LifeSystem.GetCharacters();

            Check(!GearDrop.Blocked, "기본은 켜짐");
            Check(GearDrop.Applies(Economy.DropSource.FieldDungeonBoss)
                  && GearDrop.Applies(Economy.DropSource.Tower5Boss)
                  && GearDrop.Applies(Economy.DropSource.Tower10Boss)
                  && GearDrop.Applies(Economy.DropSource.RaidDungeon),
                "보스 네 출처");
            Check(GearDrop.GradeOf(Economy.DropSource.FieldDungeonBoss) == GearDrop.BossGrade
                  && GearDrop.GradeOf(Economy.DropSource.Tower5Boss) == GearGrade.Uncommon
                  && GearDrop.GradeOf(Economy.DropSource.RaidDungeon) == GearGrade.Uncommon,
                "보스는 고급");
            Check(Equipment.GradeLabel(GearGrade.Uncommon) == "고급", "고급 라벨");

            var rng = new Rng(20260818u);
            int before = Equipment.Unequipped().Count;
            var drop = GearDrop.Apply(Economy.DropSource.Tower5Boss, ref rng);
            Check(drop != null && drop.Grade == GearGrade.Uncommon,
                $"5층 고급 (실제 {drop?.Grade})");
            Check(Equipment.Unequipped().Count == before + 1, "가방 +1");
            Check(GearDrop.Format(drop).IndexOf("고급", StringComparison.Ordinal) >= 0
                  && GearDrop.Format(drop).IndexOf("§10-8", StringComparison.Ordinal) >= 0,
                $"줄 (실제 {GearDrop.Format(drop)})");
            Check(Equipment.DisplayName(drop).IndexOf("고급", StringComparison.Ordinal) >= 0,
                $"표시 (실제 {Equipment.DisplayName(drop)})");

            GameState.Gain(Economy.LifeItem.CraftHide, Equipment.LeatherArmorHideCost);
            var roster = LifeSystem.GetCharacters();
            roster[0].Advancement = AdvancementTier.First;
            roster[0].Job = "수호기사";
            LifeSystem.PersistRoster();
            Check(Equipment.TryCraftLeatherArmor(), "제작");
            GearItem crafted = null;
            var bag = Equipment.Unequipped();
            for (int i = 0; i < bag.Count; i++)
                if (bag[i].RecipeId == Equipment.LeatherArmorRecipe && bag[i].Grade == GearGrade.Common)
                    crafted = bag[i];
            Check(crafted != null && crafted.Grade == GearGrade.Common,
                "제작품은 일반");
            Check(Equipment.DisplayName(crafted) == Equipment.LeatherArmorName,
                "일반은 이름만");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            GearDrop.ResetForTest();
            Environment.SetEnvironmentVariable(GearDrop.EnvNo, "1");
            var blockedRng = new Rng(1u);
            Check(GearDrop.Blocked, "QA_NO");
            Check(GearDrop.Apply(Economy.DropSource.Tower5Boss, ref blockedRng) == null,
                "QA_NO면 0");
            Check(Equipment.Unequipped().Count == 0, "QA_NO 가방 비움");
            Check(GearDrop.Line().IndexOf("없음", StringComparison.Ordinal) >= 0,
                $"QA_NO 줄 (실제 {GearDrop.Line()})");
            Environment.SetEnvironmentVariable(GearDrop.EnvNo, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            GearDrop.ResetForTest();
            _ = LifeSystem.GetCharacters();
            while (BagSlots.Used() < BagSlots.Cap)
            {
                if (Equipment.AddUnequippedForTest(Equipment.LeatherArmorRecipe) == null) break;
            }
            var fullRng = new Rng(2u);
            int full = Equipment.Unequipped().Count;
            Check(!BagSlots.CanAddGear(), "가방 가득");
            Check(GearDrop.Apply(Economy.DropSource.RaidDungeon, ref fullRng) == null
                  && Equipment.Unequipped().Count == full,
                "가득이면 거부");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            GearDrop.ResetForTest();
            Environment.SetEnvironmentVariable(GearDrop.EnvShow, "1");
            GearDrop.SeedQaIfRequested();
            Check(GearDrop.ShowQa, "시드 ShowQa");
            Check(GearDrop.Line().IndexOf("고급", StringComparison.Ordinal) >= 0,
                $"시드 줄 (실제 {GearDrop.Line()})");
            bool seeded = false;
            bag = Equipment.Unequipped();
            for (int i = 0; i < bag.Count; i++)
                if (bag[i].Grade == GearGrade.Uncommon
                    && bag[i].RecipeId == Equipment.LeatherArmorRecipe)
                    seeded = true;
            Check(seeded, "시드 가방 고급 흉갑");
            Environment.SetEnvironmentVariable(GearDrop.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string battleSrc = File.ReadAllText(Path.Combine(runtime, "BattleScreen.cs"));
            string resultSrc = File.ReadAllText(Path.Combine(runtime, "ResultScreen.cs"));
            string charSrc = File.ReadAllText(Path.Combine(runtime, "CharacterScreen.cs"));
            string equipSrc = File.ReadAllText(Path.Combine(runtime, "Equipment.cs"));
            Check(battleSrc.IndexOf("GearDrop.Apply", StringComparison.Ordinal) >= 0,
                "보스가 Apply를 읽는다");
            Check(resultSrc.IndexOf("GearDrop.Line", StringComparison.Ordinal) >= 0
                  && resultSrc.IndexOf("DroppedGear", StringComparison.Ordinal) >= 0,
                "결과가 줄·드랍을 읽는다");
            Check(charSrc.IndexOf("GearDrop.SeedQaIfRequested", StringComparison.Ordinal) >= 0
                  && charSrc.IndexOf("Equipment.DisplayName", StringComparison.Ordinal) >= 0,
                "캐릭터창이 시드·표시를 읽는다");
            Check(equipSrc.IndexOf("TryGrantDrop", StringComparison.Ordinal) >= 0,
                "장비가 드랍 지급을 연다");

            _ = nameof(GearDrop.Apply);
            _ = nameof(GearDrop.GradeOf);
            _ = nameof(GearDrop.Line);
            _ = nameof(GearDrop.SeedQaIfRequested);
            _ = nameof(Equipment.TryGrantDrop);

            Environment.SetEnvironmentVariable(GearDrop.EnvShow, show);
            Environment.SetEnvironmentVariable(GearDrop.EnvNo, no);
            GearDrop.ResetForTest();
            Equipment.ResetAll();
            GameState.ResetAll();
            LifeSystem.ResetAll();

            if (_fail == 0) Debug.Log("[GearDropSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[GearDropSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[GearDropSelfCheck] FAIL {_fail}건");
        }
    }
}
