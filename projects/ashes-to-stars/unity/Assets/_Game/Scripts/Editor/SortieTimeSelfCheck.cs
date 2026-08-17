using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>누적 출전 시간. 전투·일정이 더하고 영묘가 읽는다. QA_NO면 0(§4).</summary>
    public static class SortieTimeSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Sortie Time Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(SortieTime.EnvShow);
            string no = Environment.GetEnvironmentVariable(SortieTime.EnvNo);
            string memorialShow = Environment.GetEnvironmentVariable(Memorial.EnvShow);
            Environment.SetEnvironmentVariable(SortieTime.EnvShow, null);
            Environment.SetEnvironmentVariable(SortieTime.EnvNo, null);
            Environment.SetEnvironmentVariable(Memorial.EnvShow, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            PartyState.ResetForTest();
            Memorial.ResetForTest();
            SortieTime.ResetForTest();
            HuntSchedule.ResetForTest();

            var roster = LifeSystem.GetCharacters();
            var ch = roster[0];
            Check(SortieTime.Seconds(ch) == 0, "기본 출전 0");
            Check(SortieTime.Line(ch) == "", "빈 줄");
            Check(SortieTime.Format(3600) == "1시간 0분",
                $"1시간 (실제 {SortieTime.Format(3600)})");
            Check(SortieTime.Format(61) == "1분 1초",
                $"61초 (실제 {SortieTime.Format(61)})");

            PartyState.SetSlotsForTest(0, roster.Count > 1 ? 1 : 0);
            SortieTime.Apply(3600f);
            Check(SortieTime.Seconds(ch) == 3600, $"전투 1시간 (실제 {SortieTime.Seconds(ch)})");
            if (roster.Count > 1)
                Check(SortieTime.Seconds(roster[1]) == 3600, "편성 1번도 같은 초");
            Check(SortieTime.Line(ch).Contains("1시간 0분") && SortieTime.Line(ch).Contains("§4"),
                $"줄 (실제 {SortieTime.Line(ch)})");

            SortieTime.Apply(0.4f);
            Check(SortieTime.Seconds(ch) == 3600, "1초 미만은 안 더한다");

            ch.DeathCount = 2;
            GameState.SetTowerFloorForTest(Memorial.QaFloor);
            GameFlow.SetReturnForTest(GameFlow.Tower, GameFlow.BattleKind.보스);
            LifeSystem.RegisterDeath(ch);
            Check(Memorial.TimeLine(ch).Contains("1시간 0분"),
                $"영묘 출전 (실제 {Memorial.TimeLine(ch)})");
            Check(Memorial.HubLine().Contains("출전"),
                $"허브 (실제 {Memorial.HubLine()})");

            LifeSystem.ForgetInMemoryForTest();
            roster = LifeSystem.GetCharacters();
            ch = roster[0];
            Check(ch.IsDeleted && SortieTime.Seconds(ch) == 3600,
                $"재기동 3600 (실제 {SortieTime.Seconds(ch)})");
            Check(Memorial.TimeLine(ch).Contains("1시간 0분"),
                $"재기동 줄 (실제 {Memorial.TimeLine(ch)})");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            SortieTime.ResetForTest();
            HuntSchedule.ResetForTest();
            roster = LifeSystem.GetCharacters();
            PartyState.SetSlotsForTest(0);
            Check(HuntSchedule.TryStart(), "일정 시작");
            HuntSchedule.Tick(3600f);
            Check(SortieTime.Seconds(roster[0]) == 3600,
                $"일정 1시간 (실제 {SortieTime.Seconds(roster[0])})");
            HuntSchedule.Stop();

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            Memorial.ResetForTest();
            SortieTime.ResetForTest();
            Environment.SetEnvironmentVariable(SortieTime.EnvNo, "1");
            roster = LifeSystem.GetCharacters();
            PartyState.SetSlotsForTest(0);
            SortieTime.Apply(3600f);
            Check(SortieTime.Seconds(roster[0]) == 0, "차단하면 0");
            Check(SortieTime.Line(roster[0]) == "", "차단하면 줄 없음");
            roster[0].DeathCount = 2;
            LifeSystem.RegisterDeath(roster[0]);
            Check(Memorial.TimeLine(roster[0]) == "", "차단하면 영묘 줄 없음");
            Environment.SetEnvironmentVariable(SortieTime.EnvNo, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            Memorial.ResetForTest();
            SortieTime.ResetForTest();
            Environment.SetEnvironmentVariable(Memorial.EnvShow, "1");
            Memorial.SeedQaIfRequested();
            roster = LifeSystem.GetCharacters();
            Check(roster[0].IsDeleted && SortieTime.Seconds(roster[0]) == SortieTime.QaSeconds,
                $"시드 1시간 (실제 {SortieTime.Seconds(roster[0])})");
            Check(Memorial.TimeLine(roster[0]).Contains("1시간 0분"),
                $"시드 줄 (실제 {Memorial.TimeLine(roster[0])})");
            Environment.SetEnvironmentVariable(Memorial.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string battle = File.ReadAllText(Path.Combine(runtime, "BattleScreen.cs"));
            string hunt = File.ReadAllText(Path.Combine(runtime, "HuntSchedule.cs"));
            string estate = File.ReadAllText(Path.Combine(runtime, "EstateScreen.cs"));
            string character = File.ReadAllText(Path.Combine(runtime, "CharacterScreen.cs"));
            string memorialSrc = File.ReadAllText(Path.Combine(runtime, "Memorial.cs"));
            Check(battle.Contains("SortieTime.Apply"),
                "전투가 Apply를 읽는다");
            Check(hunt.Contains("SortieTime.AddToIndexes"),
                "일정이 초를 읽는다");
            Check(estate.Contains("Memorial.TimeLine") && character.Contains("Memorial.TimeLine"),
                "영묘·캐릭터가 줄을 읽는다");
            Check(memorialSrc.Contains("SortieTime.SeedQaIfRequested")
                  && memorialSrc.Contains("SortieTime.Line"),
                "Stamp 시드·줄이 출전을 읽는다");

            Environment.SetEnvironmentVariable(SortieTime.EnvShow, show);
            Environment.SetEnvironmentVariable(SortieTime.EnvNo, no);
            Environment.SetEnvironmentVariable(Memorial.EnvShow, memorialShow);

            if (_fail == 0) Debug.Log("[SortieTimeSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[SortieTimeSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[SortieTimeSelfCheck] FAIL {_fail}건");
        }
    }
}
