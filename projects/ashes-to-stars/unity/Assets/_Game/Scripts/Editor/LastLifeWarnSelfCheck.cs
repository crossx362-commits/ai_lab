using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>마지막 목숨 경고가 장착 6부위를 이름으로 보여 준다. QA_NO면 이름 없음(§11).</summary>
    public static class LastLifeWarnSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Last Life Gear Warn Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(LastLifeWarn.EnvShow);
            string no = Environment.GetEnvironmentVariable(LastLifeWarn.EnvNo);
            Environment.SetEnvironmentVariable(LastLifeWarn.EnvShow, null);
            Environment.SetEnvironmentVariable(LastLifeWarn.EnvNo, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            PartyState.ResetForTest();
            LastLifeWarn.ResetForTest();

            Check(!LastLifeWarn.HasAny(), "기본 명부는 마지막 목숨이 아니다");
            Check(LastLifeWarn.GearLine().Contains("장착 없음"),
                $"빈 줄 (실제 {LastLifeWarn.GearLine()})");

            var roster = LifeSystem.GetCharacters();
            var ch = roster[0];
            ch.Name = LastLifeWarn.QaName;
            ch.Job = "수호기사";
            ch.Advancement = AdvancementTier.First;
            ch.DeathCount = LastLifeWarn.LastDeaths;
            LifeSystem.PersistRoster();
            Equipment.SeedCraftedLoadoutForQa(ch);
            PartyState.SetSlotsForTest(0);

            Check(LastLifeWarn.HasAny(), "DeathCount=2면 마지막 목숨");
            Check(LastLifeWarn.IsLastLife(ch), "IsLastLife");
            var worn = Equipment.WornAll(ch);
            Check(worn.Count == Equipment.SlotCount,
                $"장착 6칸 (실제 {worn.Count})");
            string line = LastLifeWarn.GearLine();
            string rest = LastLifeWarn.GearRest();
            string all = line + " " + rest;
            Check(line.Contains(LastLifeWarn.QaName), $"이름 (실제 {line})");
            Check(all.Contains("송곳니 검") && all.Contains("유골 투구")
                  && all.Contains("가죽 흉갑") && all.Contains("부품 장갑")
                  && all.Contains("원소 신발") && all.Contains("마정 장신구"),
                $"6부위 이름 (실제 {all})");
            Check(LastLifeWarn.Body().Contains("§11") && LastLifeWarn.Body().Contains("가방"),
                $"본문 가방 유지 (실제 {LastLifeWarn.Body()})");

            var bag = Equipment.AddUnequippedForTest(Equipment.LeatherArmorRecipe);
            Check(bag != null && !line.Contains(bag.Id), "비장착은 경고에 안 나온다");

            LifeSystem.ForgetInMemoryForTest();
            Equipment.ForgetInMemoryForTest();
            PartyState.ResetForTest();
            PartyState.SetSlotsForTest(0);
            roster = LifeSystem.GetCharacters();
            Check(LastLifeWarn.IsLastLife(roster[0]), "재기동 뒤에도 마지막 목숨");
            string again = LastLifeWarn.GearLine() + " " + LastLifeWarn.GearRest();
            Check(again.Contains("송곳니 검") && again.Contains("마정 장신구"),
                $"재기동 뒤 이름 (실제 {again})");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            PartyState.ResetForTest();
            LastLifeWarn.ResetForTest();
            roster = LifeSystem.GetCharacters();
            ch = roster[0];
            ch.DeathCount = LastLifeWarn.LastDeaths;
            LifeSystem.PersistRoster();
            Check(LastLifeWarn.GearLine().Contains("장착 없음"),
                $"빈 장착 (실제 {LastLifeWarn.GearLine()})");

            Environment.SetEnvironmentVariable(LastLifeWarn.EnvNo, "1");
            Check(LastLifeWarn.Blocked, "QA_NO면 차단");
            Check(!LastLifeWarn.GearLine().Contains("송곳니")
                  && LastLifeWarn.GearLine().Contains("§4"),
                $"차단하면 이름 없음 (실제 {LastLifeWarn.GearLine()})");
            Check(!LastLifeWarn.Body().Contains("아래 장착"),
                $"차단 본문 (실제 {LastLifeWarn.Body()})");
            Environment.SetEnvironmentVariable(LastLifeWarn.EnvNo, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            PartyState.ResetForTest();
            LastLifeWarn.ResetForTest();
            Environment.SetEnvironmentVariable(LastLifeWarn.EnvShow, "1");
            LastLifeWarn.SeedQaIfRequested();
            roster = LifeSystem.GetCharacters();
            Check(roster[0].Name == LastLifeWarn.QaName
                  && roster[0].DeathCount == LastLifeWarn.LastDeaths,
                $"시드 이름·목숨 (실제 {roster[0].Name} {roster[0].DeathCount})");
            Check(Equipment.WornAll(roster[0]).Count == 6, "시드 장착 6");
            Check(LastLifeWarn.QaPrompt, "시드가 경고를 연다");
            Check(LastLifeWarn.GearLine().Contains("가죽 흉갑"),
                $"시드 줄 (실제 {LastLifeWarn.GearLine()})");
            Environment.SetEnvironmentVariable(LastLifeWarn.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string field = File.ReadAllText(Path.Combine(runtime, "FieldScreen.cs"));
            string tower = File.ReadAllText(Path.Combine(runtime, "TowerScreen.cs"));
            Check(field.Contains("LastLifeWarn.GearLine")
                  && field.Contains("LastLifeWarn.GearRest")
                  && field.Contains("!string.IsNullOrEmpty(gearRest)")
                  && field.Contains("LastLifeWarn.HasAny")
                  && field.Contains("LastLifeWarn.SeedQaIfRequested"),
                "필드가 경고·시드를 읽고 빈 장비 뒷줄은 그리지 않는다");
            Check(tower.Contains("LastLifeWarn.GearLine")
                  && tower.Contains("LastLifeWarn.GearRest")
                  && tower.Contains("!string.IsNullOrEmpty(gearRest)")
                  && tower.Contains("LastLifeWarn.HasAny")
                  && tower.Contains("LastLifeWarn.SeedQaIfRequested"),
                "탑이 경고·시드를 읽고 빈 장비 뒷줄은 그리지 않는다");

            _ = nameof(LastLifeWarn.GearLine);
            _ = nameof(LastLifeWarn.SeedQaIfRequested);
            _ = nameof(LastLifeWarn.HasAny);

            Environment.SetEnvironmentVariable(LastLifeWarn.EnvShow, show);
            Environment.SetEnvironmentVariable(LastLifeWarn.EnvNo, no);
            LastLifeWarn.ResetForTest();
            Equipment.ResetAll();
            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();

            if (_fail == 0) Debug.Log("[LastLifeWarnSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[LastLifeWarnSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[LastLifeWarnSelfCheck] FAIL {_fail}건");
        }
    }
}
