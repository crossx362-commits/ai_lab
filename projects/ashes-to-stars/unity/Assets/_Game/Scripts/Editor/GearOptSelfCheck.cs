using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>드랍 옵션은 등급별 1~4, 전설만 4. 체력은 강화와 같은 2%/개. QA_NO면 옛 0(§11).</summary>
    public static class GearOptSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Gear Opt Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(GearOpt.EnvShow);
            string no = Environment.GetEnvironmentVariable(GearOpt.EnvNo);
            Environment.SetEnvironmentVariable(GearOpt.EnvShow, null);
            Environment.SetEnvironmentVariable(GearOpt.EnvNo, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            GearOpt.ResetForTest();
            _ = LifeSystem.GetCharacters();

            Check(!GearOpt.Blocked, "기본은 켜짐");
            Check(GearOpt.CountOf(GearGrade.Common) == 1, "일반 1");
            Check(GearOpt.CountOf(GearGrade.Uncommon) == 2, "고급 2");
            Check(GearOpt.CountOf(GearGrade.Rare) == 3, "희귀 3");
            Check(GearOpt.CountOf(GearGrade.Heroic) == 3, "영웅 3");
            Check(GearOpt.CountOf(GearGrade.Legendary) == 4, "전설 4");
            Check(GearOpt.Names.Length == 4, "이름 4");

            var common = Equipment.TryGrantDrop(Equipment.LeatherArmorRecipe, GearGrade.Common);
            Check(common != null && GearOpt.CountOf(common) == 1,
                $"드랍 일반 1 (실제 {GearOpt.CountOf(common)})");
            Check(GearOpt.Format(common).IndexOf("옵션 1", StringComparison.Ordinal) >= 0
                  && GearOpt.Format(common).IndexOf("§11", StringComparison.Ordinal) >= 0,
                $"일반 줄 (실제 {GearOpt.Format(common)})");

            var rare = Equipment.TryGrantDrop(Equipment.LeatherArmorRecipe, GearGrade.Rare);
            Check(rare != null && GearOpt.CountOf(rare) == 3,
                $"드랍 희귀 3 (실제 {GearOpt.CountOf(rare)})");

            var hero = Equipment.TryGrantDrop(Equipment.LeatherArmorRecipe, GearGrade.Heroic);
            Check(hero != null && GearOpt.CountOf(hero) == 3,
                $"드랍 영웅 3 (실제 {GearOpt.CountOf(hero)})");

            var legend = Equipment.TryGrantDrop(Equipment.LeatherArmorRecipe, GearGrade.Legendary);
            Check(legend != null && GearOpt.CountOf(legend) == 4,
                $"드랍 전설 4 (실제 {GearOpt.CountOf(legend)})");
            Check(GearOpt.Format(legend).IndexOf("옵션 4", StringComparison.Ordinal) >= 0,
                $"전설 줄 (실제 {GearOpt.Format(legend)})");

            var seen = new System.Collections.Generic.HashSet<int>();
            bool unique = true;
            for (int i = 0; i < legend.Affixes.Length; i++)
            {
                if (!seen.Add(legend.Affixes[i])) unique = false;
            }
            Check(unique && seen.Count == 4, "전설 옵션이 서로 다르다");

            Check(Mathf.Approximately(GearOpt.HpPerAffix, Equipment.EnhanceHpPerLevel),
                "옵션 체력은 강화와 같은 칸");
            legend.Affixes = new[] { 0, 1, 2, 3 };
            Check(Mathf.Approximately(GearOpt.HpMul(legend), 1f + 4f * Equipment.EnhanceHpPerLevel),
                $"전설 4옵션 ×1.08 (실제 {GearOpt.HpMul(legend)})");
            Check(GearOpt.CombatLine(legend).IndexOf("1.08", StringComparison.Ordinal) >= 0
                  && GearOpt.CombatLine(legend).IndexOf("§11", StringComparison.Ordinal) >= 0,
                $"전설 체력 줄 (실제 {GearOpt.CombatLine(legend)})");
            float withOpt = Equipment.EffectiveHpMul(legend);
            float baseOnly = legend.HpMul
                * (1f + Mathf.Clamp(legend.Enhance, 0, Equipment.MaxEnhance)
                    * Equipment.EnhanceHpPerLevel);
            Check(Mathf.Approximately(withOpt, baseOnly * GearOpt.HpMul(legend))
                  && withOpt > baseOnly,
                $"EffectiveHpMul이 옵션을 곱한다 (실제 {withOpt} / 옛 {baseOnly})");

            Equipment.Flush();
            Equipment.ForgetInMemoryForTest();
            GearOpt.ResetForTest();
            var again = Equipment.Unequipped();
            GearItem loaded = null;
            for (int i = 0; i < again.Count; i++)
                if (again[i].Grade == GearGrade.Legendary) loaded = again[i];
            Check(loaded != null && GearOpt.CountOf(loaded) == 4
                  && GearOpt.Format(loaded).IndexOf("생명", StringComparison.Ordinal) >= 0
                  && GearOpt.Format(loaded).IndexOf("견고", StringComparison.Ordinal) >= 0,
                $"재기동 유지 (실제 {GearOpt.Format(loaded)})");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            GearOpt.ResetForTest();
            var roster = LifeSystem.GetCharacters();
            var tank = roster[0];
            tank.Advancement = AdvancementTier.First;
            tank.Job = "수호기사";
            LifeSystem.PersistRoster();
            GameState.Gain(Economy.LifeItem.CraftHide, Equipment.LeatherArmorHideCost);
            Check(Equipment.TryCraftLeatherArmor(), "제작");
            Check(Equipment.All.Count == 1 && GearOpt.CountOf(Equipment.All[0]) == 0,
                $"제작품은 0 (실제 {GearOpt.CountOf(Equipment.All[0])})");
            Check(string.IsNullOrEmpty(GearOpt.Format(Equipment.All[0])), "제작품 줄 없음");
            Check(Mathf.Approximately(GearOpt.HpMul(Equipment.All[0]), 1f)
                  && Mathf.Approximately(Equipment.EffectiveHpMul(Equipment.All[0]),
                      Equipment.LeatherArmorHpMul),
                "제작품 체력은 옵션 없음");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            GearOpt.ResetForTest();
            _ = LifeSystem.GetCharacters();
            Environment.SetEnvironmentVariable(GearOpt.EnvNo, "1");
            Check(GearOpt.Blocked, "QA_NO");
            Check(GearOpt.CountOf(GearGrade.Legendary) == 0, "QA_NO면 전설도 0");
            var blocked = Equipment.TryGrantDrop(Equipment.LeatherArmorRecipe, GearGrade.Legendary);
            Check(blocked != null && GearOpt.CountOf(blocked) == 0, "QA_NO 드랍 0");
            Check(Mathf.Approximately(GearOpt.HpMul(blocked), 1f), "QA_NO 체력 ×1");
            Check(GearOpt.Line().IndexOf("없음", StringComparison.Ordinal) >= 0,
                $"QA_NO 줄 (실제 {GearOpt.Line()})");
            Check(GearOpt.CombatLine().IndexOf("없음", StringComparison.Ordinal) >= 0,
                $"QA_NO 체력 줄 (실제 {GearOpt.CombatLine()})");
            Environment.SetEnvironmentVariable(GearOpt.EnvNo, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            GearOpt.ResetForTest();
            Environment.SetEnvironmentVariable(GearOpt.EnvShow, "1");
            GearOpt.SeedQaIfRequested();
            Check(GearOpt.ShowQa, "시드 ShowQa");
            Check(GearOpt.Line().IndexOf("전설만 4개", StringComparison.Ordinal) >= 0,
                $"시드 줄 (실제 {GearOpt.Line()})");
            bool seeded = false;
            var all = Equipment.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Grade == GearGrade.Legendary
                    && all[i].RecipeId == Equipment.LeatherArmorRecipe
                    && GearOpt.CountOf(all[i]) == 4)
                    seeded = true;
            }
            Check(seeded, "시드 전설 흉갑 옵션 4");
            Check(GearOpt.LastLine.IndexOf("옵션 4", StringComparison.Ordinal) >= 0,
                $"시드 마지막 줄 (실제 {GearOpt.LastLine})");
            Environment.SetEnvironmentVariable(GearOpt.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string equipSrc = File.ReadAllText(Path.Combine(runtime, "Equipment.cs"));
            string charSrc = File.ReadAllText(Path.Combine(runtime, "CharacterScreen.cs"));
            Check(equipSrc.IndexOf("GearOpt.Apply", StringComparison.Ordinal) >= 0,
                "드랍이 Apply를 읽는다");
            Check(equipSrc.IndexOf("GearOpt.HpMul", StringComparison.Ordinal) >= 0,
                "EffectiveHpMul이 HpMul을 읽는다");
            Check(charSrc.IndexOf("GearOpt.SeedQaIfRequested", StringComparison.Ordinal) >= 0
                  && charSrc.IndexOf("GearOpt.Format", StringComparison.Ordinal) >= 0
                  && charSrc.IndexOf("GearOpt.Line", StringComparison.Ordinal) >= 0
                  && charSrc.IndexOf("GearOpt.CombatLine", StringComparison.Ordinal) >= 0,
                "캐릭터창이 시드·줄·표시·체력을 읽는다");

            _ = nameof(GearOpt.Apply);
            _ = nameof(GearOpt.CountOf);
            _ = nameof(GearOpt.Format);
            _ = nameof(GearOpt.Line);
            _ = nameof(GearOpt.HpMul);
            _ = nameof(GearOpt.CombatLine);
            _ = nameof(GearOpt.SeedQaIfRequested);

            Environment.SetEnvironmentVariable(GearOpt.EnvShow, show);
            Environment.SetEnvironmentVariable(GearOpt.EnvNo, no);
            GearOpt.ResetForTest();
            Equipment.ResetAll();
            GameState.ResetAll();
            LifeSystem.ResetAll();

            if (_fail == 0) Debug.Log("[GearOptSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[GearOptSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[GearOptSelfCheck] FAIL {_fail}건");
        }
    }
}
