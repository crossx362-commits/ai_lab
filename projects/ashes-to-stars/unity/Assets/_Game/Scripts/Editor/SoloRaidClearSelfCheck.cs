using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §8 1인 최초 클리어. 레이드 층·출전 1명·보스마다 1회·전투력 불변.
    /// </summary>
    public static class SoloRaidClearSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Solo Raid Clear Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string noSolo = Environment.GetEnvironmentVariable("QA_NO_SOLO_CLEAR");
            string qaSolo = Environment.GetEnvironmentVariable("QA_SOLO_CLEAR");
            Environment.SetEnvironmentVariable("QA_NO_SOLO_CLEAR", null);
            Environment.SetEnvironmentVariable("QA_SOLO_CLEAR", null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            SoloRaidClear.ResetForTest();
            LifeSystem.GetCharacters();

            Check(!SoloRaidClear.HasAny && SoloRaidClear.Count == 0, "시작은 1인 칭호 없음");
            Check(SoloRaidClear.IsRaidFloor(5) && SoloRaidClear.IsRaidFloor(100)
                  && !SoloRaidClear.IsRaidFloor(4) && !SoloRaidClear.IsRaidFloor(6),
                "레이드는 5·10·…·100층만");
            Check(!SoloRaidClear.TryGrant(6, 1), "6층은 레이드가 아니다");
            Check(!SoloRaidClear.HasAny, "비레이드 거부는 칭호를 안 준다");
            Check(!SoloRaidClear.TryGrant(5, 5), "5인 클리어는 특별 보상이 아니다");
            Check(!SoloRaidClear.HasAny, "5인 거부는 칭호를 안 준다");
            Check(!SoloRaidClear.TryGrant(5, 0), "출전 0명은 거부");

            long copper = GameState.Wallet.Copper;
            int lv = LifeSystem.GetCharacters()[0].Level;
            int tea = LifeSystem.GetRevivePotions();

            PartyState.SetSlotsForTest(0);
            Check(PartyState.SortieRecords().Count == 1, "출전을 1명으로 고정");
            GameFlow.ApplyTowerBossVictory(5);
            Check(GameState.TowerFloor == 6, "5층 격파는 6층으로 오른다");
            Check(SoloRaidClear.HasClear(5) && SoloRaidClear.LastTitle == "5층을 홀로 깬 자",
                "칭호 5층을 홀로 깬 자");
            Check(SoloRaidClear.HasLook && SoloRaidClear.LookName == "홀로 선 별",
                "홀로 선 별 외형");
            Check(SoloRaidClear.PendingBanner, "첫 클리어는 결과 배너를 연다");
            Check(GameState.Wallet.Copper == copper, "골드가 안 늘었다");
            Check(LifeSystem.GetCharacters()[0].Level == lv, "레벨이 안 올랐다");
            Check(LifeSystem.GetRevivePotions() == tea, "부활초가 안 늘었다");

            Check(!SoloRaidClear.TryGrant(5, 1), "같은 보스 두 번째는 재지급하지 않는다");
            Check(SoloRaidClear.Count == 1 && !SoloRaidClear.PendingBanner,
                "재도전은 칭호를 유지하고 배너는 다시 안 연다");

            Check(SoloRaidClear.TryGrant(10, 1), "다른 보스(10층)는 따로 준다");
            Check(SoloRaidClear.HasClear(5) && SoloRaidClear.HasClear(10) && SoloRaidClear.Count == 2,
                "보스마다 1회 · 두 보스가 남는다");
            Check(SoloRaidClear.LastTitle == "10층을 홀로 깬 자", "마지막 칭호는 10층");

            PartyState.SetSlotsForTest(0, 1);
            Check(!SoloRaidClear.TryGrant(15, PartyState.SortieRecords().Count),
                "2인 15층은 거부");
            Check(!SoloRaidClear.HasClear(15), "2인은 15층 칭호가 없다");

            GameState.ForgetInMemoryForTest();
            Check(SoloRaidClear.HasClear(5) && SoloRaidClear.HasClear(10)
                  && SoloRaidClear.HasLook && SoloRaidClear.Count == 2,
                "칭호·외형이 저장에서 되살아난다");

            SoloRaidClear.AckBanner();
            Check(!SoloRaidClear.PendingBanner, "확인하면 배너가 닫힌다");

            Environment.SetEnvironmentVariable("QA_NO_SOLO_CLEAR", "1");
            SoloRaidClear.ResetForTest();
            Check(!SoloRaidClear.TryGrant(5, 1), "QA_NO_SOLO_CLEAR=1이면 지급 거부");
            Check(!SoloRaidClear.HasAny, "강제 끔은 칭호가 없다");
            Environment.SetEnvironmentVariable("QA_NO_SOLO_CLEAR", null);

            Environment.SetEnvironmentVariable("QA_SOLO_CLEAR", "1");
            GameState.ResetAll();
            SoloRaidClear.ResetForTest();
            SoloRaidClear.SeedQaIfRequested();
            Check(SoloRaidClear.HasClear(5) && SoloRaidClear.HasLook
                  && SoloRaidClear.PendingBanner && SoloRaidClear.LastTitle == "5층을 홀로 깬 자",
                "QA_SOLO_CLEAR=1이면 5층 칭호·배너");
            Environment.SetEnvironmentVariable("QA_SOLO_CLEAR", qaSolo);
            Environment.SetEnvironmentVariable("QA_NO_SOLO_CLEAR", noSolo);

            _ = nameof(SoloRaidClear.TryGrant);
            _ = nameof(SoloRaidClear.AckBanner);
            _ = nameof(SoloRaidClear.SeedQaIfRequested);
            _ = nameof(GameFlow.ApplyTowerBossVictory);
            _ = nameof(PartyState.SetSlotsForTest);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            SoloRaidClear.ResetForTest();

            if (_fail == 0) Debug.Log("[SoloRaidClearSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[SoloRaidClearSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[SoloRaidClearSelfCheck] FAIL {_fail}건");
        }
    }
}
