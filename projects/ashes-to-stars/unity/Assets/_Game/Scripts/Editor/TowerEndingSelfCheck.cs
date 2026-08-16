using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 100층 결말 첫 슬라이스. 칭호·별 외형·층 상한·재도전·전투력 불변.
    /// </summary>
    public static class TowerEndingSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Tower Ending Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string noEnd = Environment.GetEnvironmentVariable("QA_NO_TOWER_END");
            string qaEnd = Environment.GetEnvironmentVariable("QA_TOWER_END");
            Environment.SetEnvironmentVariable("QA_NO_TOWER_END", null);
            Environment.SetEnvironmentVariable("QA_TOWER_END", null);

            GameState.ResetAll();
            TowerEnding.ResetForTest();
            LifeSystem.ResetAll();

            Check(GameState.TowerFloor == 1 && !TowerEnding.HasTitle && !TowerEnding.HasStarLook,
                "시작은 1층·칭호 없음");
            Check(!TowerEnding.TryGrant(99), "99층은 결말이 아니다");
            Check(!TowerEnding.HasTitle, "99층 거부는 칭호를 안 준다");

            GameState.SetTowerFloorForTest(99);
            GameFlow.ApplyTowerBossVictory(99);
            Check(GameState.TowerFloor == 100, "99층 격파는 100층으로 오른다");
            Check(!TowerEnding.HasTitle, "99층 격파는 칭호를 안 준다");

            long copper = GameState.Wallet.Copper;
            int lv = LifeSystem.GetCharacters()[0].Level;
            int tea = LifeSystem.GetRevivePotions();

            GameFlow.ApplyTowerBossVictory(100);
            Check(GameState.TowerFloor == 100, "100층 격파 뒤에도 층은 100이다(101 없음)");
            Check(TowerEnding.HasTitle && TowerEnding.TitleName == "별에 닿은 자",
                "칭호 별에 닿은 자");
            Check(TowerEnding.HasStarLook, "별 외형이 붙는다");
            Check(TowerEnding.PendingEpilogue, "첫 클리어는 에필로그를 연다");
            Check(GameState.Wallet.Copper == copper, "골드가 안 늘었다");
            Check(LifeSystem.GetCharacters()[0].Level == lv, "레벨이 안 올랐다");
            Check(LifeSystem.GetRevivePotions() == tea, "부활초가 안 늘었다");

            Check(!TowerEnding.TryGrant(100), "두 번째 100층은 재지급하지 않는다");
            Check(TowerEnding.HasTitle && !TowerEnding.PendingEpilogue,
                "재도전은 칭호를 유지하고 에필로그는 다시 안 연다");
            Check(GameState.TowerFloor == 100, "재도전도 101층을 열지 않는다");

            GameState.ClearFloor(100);
            Check(GameState.TowerFloor == 100, "ClearFloor(100)은 101로 안 간다");

            GameState.ForgetInMemoryForTest();
            TowerEnding.ForgetInMemoryForTest();
            Check(TowerEnding.HasTitle && TowerEnding.HasStarLook && GameState.TowerFloor == 100,
                "칭호·외형·100층이 저장에서 되살아난다");

            TowerEnding.SkipEpilogue();
            Check(!TowerEnding.PendingEpilogue, "건너뛰면 에필로그가 닫힌다");

            Environment.SetEnvironmentVariable("QA_NO_TOWER_END", "1");
            TowerEnding.ResetForTest();
            GameState.SetTowerFloorForTest(100);
            Check(!TowerEnding.TryGrant(100), "QA_NO_TOWER_END=1이면 지급 거부");
            Check(!TowerEnding.HasTitle, "강제 끔은 칭호가 없다");
            Environment.SetEnvironmentVariable("QA_NO_TOWER_END", null);

            Environment.SetEnvironmentVariable("QA_TOWER_END", "1");
            GameState.ResetAll();
            TowerEnding.ResetForTest();
            TowerEnding.SeedQaIfRequested();
            Check(GameState.TowerFloor == 100 && TowerEnding.HasTitle
                  && TowerEnding.HasStarLook && TowerEnding.PendingEpilogue,
                "QA_TOWER_END=1이면 100층·칭호·에필로그");
            Environment.SetEnvironmentVariable("QA_TOWER_END", qaEnd);
            Environment.SetEnvironmentVariable("QA_NO_TOWER_END", noEnd);

            _ = nameof(TowerEnding.TryGrant);
            _ = nameof(TowerEnding.SkipEpilogue);
            _ = nameof(TowerEnding.SeedQaIfRequested);
            _ = nameof(GameFlow.ApplyTowerBossVictory);

            GameState.ResetAll();
            TowerEnding.ResetForTest();

            if (_fail == 0) Debug.Log("[TowerEndingSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[TowerEndingSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[TowerEndingSelfCheck] FAIL {_fail}건");
        }
    }
}
