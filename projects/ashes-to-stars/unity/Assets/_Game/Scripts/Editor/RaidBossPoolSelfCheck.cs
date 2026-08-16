using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>하위 레이드 보스는 깬 풀에서 뽑힌다. QA_NO면 입장 층(§9).</summary>
    public static class RaidBossPoolSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Raid Boss Pool Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(RaidBossPool.EnvShow);
            string no = Environment.GetEnvironmentVariable(RaidBossPool.EnvNo);
            Environment.SetEnvironmentVariable(RaidBossPool.EnvShow, null);
            Environment.SetEnvironmentVariable(RaidBossPool.EnvNo, null);

            GameState.ResetAll();
            RaidBossPool.ResetForTest();
            RaidScale.ResetForTest();

            Check(RaidBossPool.Name(5) == "문지기 골렘",
                $"5층 이름 (실제 {RaidBossPool.Name(5)})");
            Check(RaidBossPool.Name(10) == "재의 군주",
                $"10층 이름 (실제 {RaidBossPool.Name(10)})");
            Check(RaidBossPool.Name(30) == "심연의 눈",
                $"30층 이름 (실제 {RaidBossPool.Name(30)})");
            Check(RaidBossPool.Name(50) == "죽음의 문지기",
                $"50층 이름 (실제 {RaidBossPool.Name(50)})");
            Check(RaidBossPool.Name(100) == "탑의 주인",
                $"100층 이름 (실제 {RaidBossPool.Name(100)})");

            GameState.SetTowerFloorForTest(5);
            Check(RaidBossPool.PoolCount == 0, $"첫 5층 전 풀 0 (실제 {RaidBossPool.PoolCount})");
            Check(!RaidBossPool.Applies(5), "첫 5층은 추첨 없음");
            Check(RaidBossPool.Pick(5) == 5, "첫 5층 Pick=5");
            Check(RaidBossPool.Name() == "문지기 골렘", "첫 5층은 문지기 골렘");

            GameState.SetTowerFloorForTest(10);
            Check(!RaidBossPool.Applies(10), "첫 10층은 추첨 없음");
            Check(RaidBossPool.Pick(10) == 10, "첫 10층 Pick=10");
            Check(RaidBossPool.PoolCount == 1 && RaidBossPool.ClearedFloors()[0] == 5,
                "10층 도전 중 풀은 5만");

            GameState.SetTowerFloorForTest(11);
            Check(RaidBossPool.Applies(5), "11층에서 5층은 하위");
            Check(RaidBossPool.PoolCount == 2, $"11층 풀 2 (실제 {RaidBossPool.PoolCount})");
            Check(RaidBossPool.ClearedFloors()[0] == 5 && RaidBossPool.ClearedFloors()[1] == 10,
                "11층 풀은 5·10");
            RaidBossPool.ForceSeed = 0;
            Check(RaidBossPool.Pick(5) == 5, "시드 0 → 5층");
            RaidBossPool.ForceSeed = 1;
            Check(RaidBossPool.Pick(5) == 10, "시드 1 → 10층");
            RaidBossPool.ForceSeed = 0;

            GameState.SetTowerFloorForTest(51);
            Check(GameState.TrySelectTier(4), "T5 선택");
            Check(RaidBossPool.PoolCount == 10, $"51층 풀 10 (실제 {RaidBossPool.PoolCount})");
            Check(RaidBossPool.ClearedFloors()[0] == 5 && RaidBossPool.ClearedFloors()[9] == 50,
                "51층 풀은 5…50");
            RaidBossPool.ForceSeed = 5;
            Check(RaidBossPool.Pick(5) == 30, $"시드 5 → 30층 (실제 {RaidBossPool.PickedFloor})");
            Check(RaidBossPool.Name() == "심연의 눈", $"출현 이름 (실제 {RaidBossPool.Name()})");
            Check(RaidBossPool.FightFloor == 30, "FightFloor=30");
            Check(RaidBossPool.DropSourceFor(30) == Economy.DropSource.Tower10Boss,
                "30층은 대보스 테이블(10층 단위)");
            Check(RaidBossPool.DropSourceFor(5) == Economy.DropSource.Tower5Boss,
                "5층은 중간 레이드 테이블");
            Check(RaidBossPool.DropSourceFor(15) == Economy.DropSource.Tower5Boss,
                "15층은 중간 레이드 테이블");
            Check(!Economy.CanDropSpecialJobToken(30), "30층은 증표 없음");
            Check(Economy.CanDropSpecialJobToken(50), "50층은 증표");
            Check(RaidScale.Gold(5) == 11523, $"골드는 입장 층 스케일 11523 (실제 {RaidScale.Gold(5)})");
            Check(RaidBossPool.Line().Contains("10종") && RaidBossPool.Line().Contains("§9"),
                $"풀 문구 (실제 {RaidBossPool.Line()})");
            Check(RaidBossPool.PickedLine().Contains("심연의 눈")
                  && RaidBossPool.PickedLine().Contains("30층"),
                $"출현 문구 (실제 {RaidBossPool.PickedLine()})");
            Check(RaidBossPool.BattleTitle().Contains("하위 레이드")
                  && RaidBossPool.BattleTitle().Contains("심연의 눈"),
                $"전투 제목 (실제 {RaidBossPool.BattleTitle()})");

            RaidBossPool.ForcePickedFloor = 50;
            Check(RaidBossPool.Pick(5) == 50, "Force면 50");
            Check(Economy.CanDropSpecialJobToken(RaidBossPool.FightFloor), "50층 출현은 증표");
            RaidBossPool.ForcePickedFloor = 0;

            Environment.SetEnvironmentVariable(RaidBossPool.EnvNo, "1");
            Check(RaidBossPool.Blocked, "QA_NO면 차단");
            Check(RaidBossPool.Pick(5) == 5, "차단하면 입장 층");
            Check(RaidBossPool.Line().Contains("고정"),
                $"차단 문구 (실제 {RaidBossPool.Line()})");
            Environment.SetEnvironmentVariable(RaidBossPool.EnvNo, null);
            RaidBossPool.ForceSeed = 5;
            Check(RaidBossPool.Pick(5) == 30, "차단을 풀면 다시 30");

            GameState.ForgetInMemoryForTest();
            Check(GameState.TowerFloor == 51, "재기동 뒤에도 51층");
            Check(RaidBossPool.PoolCount == 10, "재기동 뒤에도 풀 10");

            Environment.SetEnvironmentVariable(RaidBossPool.EnvShow, "1");
            GameState.ResetAll();
            RaidBossPool.ResetForTest();
            RaidBossPool.SeedQaIfRequested();
            Check(GameState.TowerFloor == 51 && GameState.Tier == 4, "시드는 51층·T5");
            Check(RaidBossPool.PickedFloor == 30 && RaidBossPool.Name() == "심연의 눈",
                $"시드 출현 (실제 {RaidBossPool.PickedFloor} {RaidBossPool.Name()})");
            Check(RaidBossPool.Line().Contains("10종"),
                $"시드 문구 (실제 {RaidBossPool.Line()})");
            Environment.SetEnvironmentVariable(RaidBossPool.EnvShow, null);

            string flow = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/GameFlow.cs"));
            Check(flow.Contains("RaidBossPool.Pick"),
                "GameFlow.GoBattle이 Pick을 읽는다");
            string tower = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/TowerScreen.cs"));
            Check(tower.Contains("RaidBossPool.Line") && tower.Contains("RaidBossPool.SeedQaIfRequested"),
                "TowerScreen이 Line·Seed를 읽는다");
            string battle = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/BattleScreen.cs"));
            Check(battle.Contains("RaidBossPool.BattleTitle")
                  && battle.Contains("RaidBossPool.FightFloor")
                  && battle.Contains("RaidBossPool.DropSourceFor"),
                "BattleScreen이 Title·FightFloor·DropSource를 읽는다");
            string result = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/ResultScreen.cs"));
            Check(result.Contains("RaidBossPool.PickedLine"),
                "ResultScreen이 PickedLine을 읽는다");

            _ = nameof(RaidBossPool.Pick);
            _ = nameof(RaidBossPool.Line);
            _ = nameof(RaidBossPool.PickedLine);
            _ = nameof(RaidBossPool.SeedQaIfRequested);
            _ = nameof(RaidBossPool.FightFloor);

            Environment.SetEnvironmentVariable(RaidBossPool.EnvShow, show);
            Environment.SetEnvironmentVariable(RaidBossPool.EnvNo, no);
            RaidBossPool.ResetForTest();
            GameState.ResetAll();

            if (_fail > 0)
            {
                Debug.LogError("[RaidBossPoolSelfCheck] FAIL\n" + _log);
                throw new InvalidOperationException("RaidBossPoolSelfCheck FAIL " + _fail);
            }
            Debug.Log("[RaidBossPoolSelfCheck] PASS\n" + _log);
        }
    }
}
