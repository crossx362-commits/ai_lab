using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>삭제되면 영묘가 최고 층·마지막 출전·사망 원인·마지막 동료를 읽는다. QA_NO면 기록 없음(§4).</summary>
    public static class MemorialSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Memorial Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(Memorial.EnvShow);
            string no = Environment.GetEnvironmentVariable(Memorial.EnvNo);
            Environment.SetEnvironmentVariable(Memorial.EnvShow, null);
            Environment.SetEnvironmentVariable(Memorial.EnvNo, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            PartyState.ResetForTest();
            Memorial.ResetForTest();
            GameFlow.SetReturnForTest(GameFlow.Estate);

            var roster = LifeSystem.GetCharacters();
            var ch = roster[0];
            Check(!Memorial.HasRecord(ch), "기본 명부는 추모가 없다");
            Check(Memorial.Line(ch) == "기록 없음", $"빈 줄 (실제 {Memorial.Line(ch)})");

            ch.Name = Memorial.QaName;
            ch.Job = "수호기사";
            ch.Advancement = AdvancementTier.First;
            ch.DeathCount = 2;
            roster[1].Name = "힐러";
            LifeSystem.PersistRoster();
            Equipment.SeedCraftedLoadoutForQa(ch);
            PartyState.SetSlotsForTest(0, 1);
            GameState.SetTowerFloorForTest(Memorial.QaFloor);
            GameFlow.SetReturnForTest(GameFlow.Tower, GameFlow.BattleKind.보스);
            LifeSystem.RegisterDeath(ch);

            Check(ch.IsDeleted && ch.DeathCount == 3, "3회면 삭제");
            Check(Memorial.HasRecord(ch), "삭제가 추모를 찍는다");
            Check(ch.MemorialFloor == Memorial.QaFloor,
                $"최고 층 30 (실제 {ch.MemorialFloor})");
            Check(ch.MemorialPlace == "탑", $"장소 탑 (실제 {ch.MemorialPlace})");
            Check(ch.MemorialCause == "보스전 전멸", $"원인 (실제 {ch.MemorialCause})");
            string line = Memorial.Line(ch);
            Check(line.Contains("30층") && line.Contains("탑") && line.Contains("보스전 전멸")
                  && line.Contains("§4"),
                $"문구 (실제 {line})");
            string gear = Memorial.GearLine(ch);
            Check(gear.Contains("송곳니 검") && gear.Contains("유골 투구")
                  && gear.Contains("가죽 흉갑") && gear.Contains("부품 장갑")
                  && gear.Contains("원소 신발") && gear.Contains("마정 장신구"),
                $"장착 이름 (실제 {gear})");
            Check(Memorial.FormatParty(ch).Contains("힐러"),
                $"FormatParty 힐러 (실제 {Memorial.FormatParty(ch)})");
            Check(Memorial.PartyLine(ch).Contains("힐러") && Memorial.PartyLine(ch).Contains("§4")
                  && !Memorial.PartyLine(ch).Contains("추모시험"),
                $"동료 줄 (실제 {Memorial.PartyLine(ch)})");
            Check(Equipment.WornAll(ch).Count == 0, "장착은 지워진다");

            LifeSystem.ForgetInMemoryForTest();
            Equipment.ForgetInMemoryForTest();
            roster = LifeSystem.GetCharacters();
            ch = roster[0];
            Check(ch.IsDeleted && Memorial.HasRecord(ch), "재기동 뒤에도 삭제");
            Check(ch.MemorialFloor == 30 && ch.MemorialPlace == "탑"
                  && ch.MemorialCause == "보스전 전멸",
                $"재기동 기록 (실제 {Memorial.Line(ch)})");
            Check(Memorial.GearLine(ch).Contains("송곳니 검"),
                $"재기동 장착 (실제 {Memorial.GearLine(ch)})");
            Check(ch.MemorialParty.Contains("힐러") && Memorial.PartyLine(ch).Contains("힐러"),
                $"재기동 동료 (실제 {Memorial.PartyLine(ch)})");

            GameState.Gain(Economy.LifeItem.RebornStone, 1);
            Check(LifeSystem.UseRebornStone(ch), "환생");
            Check(!ch.IsDeleted && ch.MemorialRebirths == 1,
                $"환생 횟수 1 (실제 {ch.MemorialRebirths})");
            Check(Memorial.RebirthLine(ch).Contains("1회"),
                $"환생 문구 (실제 {Memorial.RebirthLine(ch)})");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            Memorial.ResetForTest();
            roster = LifeSystem.GetCharacters();
            ch = roster[0];
            ch.IsSpecialJob = true;
            ch.DeathCount = 0;
            GameState.SetTowerFloorForTest(50);
            GameFlow.SetReturnForTest(GameFlow.Tower, GameFlow.BattleKind.보스);
            LifeSystem.RegisterDeath(ch);
            Check(ch.IsDeleted && ch.MemorialCause == "특수 직업 1회 사망",
                $"특수 직업 원인 (실제 {ch.MemorialCause})");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            Memorial.ResetForTest();
            PartyState.ResetForTest();
            roster = LifeSystem.GetCharacters();
            ch = roster[0];
            ch.DeathCount = 2;
            PartyState.SetSlotsForTest(0);
            GameState.SetTowerFloorForTest(11);
            GameFlow.SetReturnForTest(GameFlow.Field, GameFlow.BattleKind.잡몹웨이브);
            LifeSystem.RegisterDeath(ch);
            Check(ch.MemorialPlace == "필드" && ch.MemorialCause == "필드 전멸",
                $"필드 (실제 {ch.MemorialPlace} {ch.MemorialCause})");
            Check(ch.MemorialParty == "혼자 출전" && Memorial.PartyLine(ch).Contains("혼자"),
                $"1인은 혼자 출전 (실제 {Memorial.PartyLine(ch)})");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            Memorial.ResetForTest();
            roster = LifeSystem.GetCharacters();
            ch = roster[0];
            ch.DeathCount = 2;
            GameFlow.SetReturnForTest(GameFlow.WorldMap, GameFlow.BattleKind.침략);
            LifeSystem.RegisterDeath(ch, isPvp: true);
            Check(!ch.IsDeleted && !Memorial.HasRecord(ch),
                "PvP는 추모를 안 찍는다");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            Memorial.ResetForTest();
            Environment.SetEnvironmentVariable(Memorial.EnvNo, "1");
            roster = LifeSystem.GetCharacters();
            ch = roster[0];
            ch.Name = Memorial.QaName;
            ch.DeathCount = 2;
            LifeSystem.PersistRoster();
            Equipment.SeedCraftedLoadoutForQa(ch);
            GameState.SetTowerFloorForTest(30);
            GameFlow.SetReturnForTest(GameFlow.Tower, GameFlow.BattleKind.보스);
            LifeSystem.RegisterDeath(ch);
            Check(ch.IsDeleted, "차단해도 삭제는 된다");
            Check(!Memorial.HasRecord(ch), "차단하면 기록 없음");
            Check(Memorial.Line(ch) == "기록 없음", $"차단 문구 (실제 {Memorial.Line(ch)})");
            Check(Memorial.HubLine() == "", "차단하면 허브 문구 없음");
            Environment.SetEnvironmentVariable(Memorial.EnvNo, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            Memorial.ResetForTest();
            Environment.SetEnvironmentVariable(Memorial.EnvShow, "1");
            Memorial.SeedQaIfRequested();
            roster = LifeSystem.GetCharacters();
            Check(roster[0].Name == Memorial.QaName && roster[0].IsDeleted,
                $"시드 이름·삭제 (실제 {roster[0].Name} {roster[0].IsDeleted})");
            Check(Memorial.Line(roster[0]).Contains("30층"),
                $"시드 문구 (실제 {Memorial.Line(roster[0])})");
            Check(Memorial.GearLine(roster[0]).Contains("가죽 흉갑"),
                $"시드 장착 (실제 {Memorial.GearLine(roster[0])})");
            Check(Memorial.PartyLine(roster[0]).Contains("힐러"),
                $"시드 동료 (실제 {Memorial.PartyLine(roster[0])})");
            Environment.SetEnvironmentVariable(Memorial.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string life = File.ReadAllText(Path.Combine(runtime, "LifeSystem.cs"));
            string estate = File.ReadAllText(Path.Combine(runtime, "EstateScreen.cs"));
            string result = File.ReadAllText(Path.Combine(runtime, "ResultScreen.cs"));
            string character = File.ReadAllText(Path.Combine(runtime, "CharacterScreen.cs"));
            Check(life.Contains("Memorial.Stamp(") && life.Contains("Memorial.NoteRebirth"),
                "RegisterDeath·환생이 추모를 읽는다");
            Check(estate.Contains("Memorial.Line") && estate.Contains("Memorial.HubLine")
                  && estate.Contains("Memorial.PartyLine")
                  && estate.Contains("Memorial.SeedQaIfRequested"),
                "영묘가 자막·문구·동료·시드를 읽는다");
            Check(result.Contains("Memorial.ResultLine"),
                "결과가 추모를 읽는다");
            Check(character.Contains("Memorial.Line")
                  && character.Contains("Memorial.PartyLine")
                  && character.Contains("Memorial.SeedQaIfRequested"),
                "캐릭터가 문구·동료·시드를 읽는다");
            Check(life.Contains("MemorialParty") && life.Contains("SanitizeMemorialParty"),
                "로스터가 동료 필드를 저장한다");

            _ = nameof(Memorial.Stamp);
            _ = nameof(Memorial.SeedQaIfRequested);
            _ = nameof(Memorial.Line);
            _ = nameof(Memorial.PartyLine);
            _ = nameof(Memorial.FormatParty);

            Environment.SetEnvironmentVariable(Memorial.EnvShow, show);
            Environment.SetEnvironmentVariable(Memorial.EnvNo, no);
            Memorial.ResetForTest();
            Equipment.ResetAll();
            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            GameFlow.SetReturnForTest(GameFlow.Estate);

            if (_fail == 0) Debug.Log("[MemorialSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[MemorialSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[MemorialSelfCheck] FAIL {_fail}건");
        }
    }
}
