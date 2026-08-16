using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 층 클리어마다 기본직업 1종 선택, 레이드는 증표 2장 확률.
    /// 재도전·던전·잘못된 직업은 거부. 전투력·골드·레벨은 안 건드린다.
    /// </summary>
    public static class FloorRecruitSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Floor Recruit Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string noReward = Environment.GetEnvironmentVariable("QA_NO_FLOOR_REWARD");
            string qaReward = Environment.GetEnvironmentVariable("QA_FLOOR_REWARD");
            string raidOn = Environment.GetEnvironmentVariable("QA_RAID_SPECIAL");
            string raidOff = Environment.GetEnvironmentVariable("QA_NO_RAID_SPECIAL");
            Environment.SetEnvironmentVariable("QA_NO_FLOOR_REWARD", null);
            Environment.SetEnvironmentVariable("QA_FLOOR_REWARD", null);
            Environment.SetEnvironmentVariable("QA_RAID_SPECIAL", null);
            Environment.SetEnvironmentVariable("QA_NO_RAID_SPECIAL", null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            FloorRecruit.ResetForTest();
            LifeSystem.GetCharacters();

            Check(!FloorRecruit.PendingJob && FloorRecruit.OfferedJobCount == 0, "시작은 층 보상 없음");
            Check(!FloorRecruit.OnCleared(0) && !FloorRecruit.OnCleared(101), "0·101층은 거부");
            Check(!FloorRecruit.PendingJob, "범위 밖은 대기를 안 연다");

            int roster0 = LifeSystem.GetCharacters().Count;
            long copper = GameState.Wallet.Copper;
            int lv = LifeSystem.GetCharacters()[0].Level;
            int tokens0 = GameState.Bag.GetCount(Economy.LifeItem.SpecialJobToken);

            Check(FloorRecruit.OnCleared(1), "1층 첫 클리어는 직업을 고르게 한다");
            Check(FloorRecruit.PendingJob && FloorRecruit.PendingFloor == 1, "1층 선택이 대기 중");
            Check(LifeSystem.GetCharacters().Count == roster0, "고르기 전에는 명부가 그대로다");
            Check(!FloorRecruit.TryClaim("수호기사"), "1차 직업명은 거부");
            Check(LifeSystem.GetCharacters().Count == roster0, "잘못된 직업은 명부를 안 늘린다");
            Check(FloorRecruit.PendingJob, "거부는 대기를 유지한다");

            Check(FloorRecruit.TryClaim("탱"), "탱을 고르면 영입");
            Check(!FloorRecruit.PendingJob, "고르면 대기가 닫힌다");
            Check(LifeSystem.GetCharacters().Count == roster0 + 1, "명부가 1명 는다");
            var added = LifeSystem.GetCharacters()[roster0];
            Check(added.Job == "탱" && added.Level == 1
                  && added.Advancement == AdvancementTier.Basic && !added.IsSpecialJob,
                $"영입은 탱 Lv1 기본직업 (실제 {added.Job} Lv{added.Level} {added.Advancement})");
            Check(added.Name.StartsWith("영입"), $"이름이 영입으로 시작한다 (실제 {added.Name})");
            Check(FloorRecruit.LastGrantedName == added.Name && FloorRecruit.LastGrantedJob == "탱",
                "마지막 영입 기록이 남는다");
            Check(!Fusion.CanBeMaterial(added), "기본직업 영입은 합성 재료가 아니다");
            Check(GameState.Wallet.Copper == copper, "골드가 안 늘었다");
            Check(LifeSystem.GetCharacters()[0].Level == lv, "기존 레벨이 안 올랐다");
            Check(GameState.Bag.GetCount(Economy.LifeItem.SpecialJobToken) == tokens0,
                "일반 층은 증표를 안 준다");

            Check(!FloorRecruit.OnCleared(1), "같은 층 재도전은 다시 안 준다");
            Check(LifeSystem.GetCharacters().Count == roster0 + 1, "재도전은 명부를 안 늘린다");
            Check(!FloorRecruit.TryClaim("딜"), "대기 없으면 영입 거부");

            Check(FloorRecruit.OnCleared(2) && FloorRecruit.TryClaim("힐"), "2층은 힐을 따로 준다");
            Check(LifeSystem.GetCharacters().Count == roster0 + 2, "두 층이면 두 명");
            Check(LifeSystem.GetCharacters()[roster0 + 1].Job == "힐", "두 번째는 힐");

            Environment.SetEnvironmentVariable("QA_RAID_SPECIAL", "1");
            int beforeRaid = GameState.Bag.GetCount(Economy.LifeItem.SpecialJobToken);
            Check(FloorRecruit.OnCleared(5), "5층 레이드는 직업+증표 판정");
            Check(FloorRecruit.PendingJob && FloorRecruit.PendingFloor == 5, "레이드도 직업을 고른다");
            Check(FloorRecruit.RolledSpecial(5), "5층 특수 판정은 한 번만");
            Check(GameState.Bag.GetCount(Economy.LifeItem.SpecialJobToken)
                  == beforeRaid + FloorRecruit.RaidSpecialTokens,
                $"레이드 성공은 증표 {FloorRecruit.RaidSpecialTokens}장");
            Check(FloorRecruit.PendingSpecialBanner
                  && FloorRecruit.LastSpecialTokensGot == FloorRecruit.RaidSpecialTokens,
                "증표 배너가 열린다");
            Check(FloorRecruit.TryClaim("버퍼"), "5층에서 버퍼를 고른다");
            Check(!FloorRecruit.OnCleared(5), "같은 레이드 재도전은 증표도 안 준다");
            Check(GameState.Bag.GetCount(Economy.LifeItem.SpecialJobToken)
                  == beforeRaid + FloorRecruit.RaidSpecialTokens,
                "재도전은 증표를 유지하고 더하지 않는다");
            Environment.SetEnvironmentVariable("QA_RAID_SPECIAL", null);

            Environment.SetEnvironmentVariable("QA_NO_RAID_SPECIAL", "1");
            int beforeFail = GameState.Bag.GetCount(Economy.LifeItem.SpecialJobToken);
            Check(FloorRecruit.OnCleared(10), "10층은 직업 대기는 연다");
            Check(FloorRecruit.RolledSpecial(10), "10층도 특수 판정은 기록한다");
            Check(GameState.Bag.GetCount(Economy.LifeItem.SpecialJobToken) == beforeFail,
                "강제 실패는 증표 0");
            Check(FloorRecruit.PendingSpecialBanner
                  && FloorRecruit.LastSpecialTokensGot == FloorRecruit.RaidSpecialTokens,
                "실패는 이전 5층 배너를 안 지운다");
            Environment.SetEnvironmentVariable("QA_NO_RAID_SPECIAL", null);

            FloorRecruit.AckSpecialBanner();
            Check(!FloorRecruit.PendingSpecialBanner, "확인하면 증표 배너가 닫힌다");

            GameState.ForgetInMemoryForTest();
            FloorRecruit.ForgetInMemoryForTest();
            Check(FloorRecruit.OfferedJob(1) && FloorRecruit.OfferedJob(5)
                  && FloorRecruit.RolledSpecial(5) && FloorRecruit.LastGrantedJob == "버퍼",
                "층 보상·마지막 영입이 저장에서 되살아난다");

            Environment.SetEnvironmentVariable("QA_NO_FLOOR_REWARD", "1");
            FloorRecruit.ResetForTest();
            Check(!FloorRecruit.OnCleared(1), "QA_NO_FLOOR_REWARD=1이면 지급 거부");
            Check(!FloorRecruit.PendingJob && FloorRecruit.OfferedJobCount == 0,
                "강제 끔은 대기가 없다");
            Environment.SetEnvironmentVariable("QA_NO_FLOOR_REWARD", null);

            Environment.SetEnvironmentVariable("QA_FLOOR_REWARD", "1");
            GameState.ResetAll();
            FloorRecruit.ResetForTest();
            FloorRecruit.SeedQaIfRequested();
            Check(FloorRecruit.PendingJob && FloorRecruit.PendingFloor == 1
                  && FloorRecruit.PendingSpecialBanner
                  && FloorRecruit.LastSpecialTokensGot == 2
                  && GameState.Bag.GetCount(Economy.LifeItem.SpecialJobToken) >= 2,
                "QA_FLOOR_REWARD=1이면 1층 선택·증표 2장");
            Environment.SetEnvironmentVariable("QA_FLOOR_REWARD", qaReward);
            Environment.SetEnvironmentVariable("QA_NO_FLOOR_REWARD", noReward);
            Environment.SetEnvironmentVariable("QA_RAID_SPECIAL", raidOn);
            Environment.SetEnvironmentVariable("QA_NO_RAID_SPECIAL", raidOff);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            FloorRecruit.ResetForTest();
            LifeSystem.GetCharacters();
            Environment.SetEnvironmentVariable("QA_RAID_SPECIAL", null);
            Environment.SetEnvironmentVariable("QA_NO_RAID_SPECIAL", "1");
            int beforeHook = LifeSystem.GetCharacters().Count;
            GameFlow.ApplyTowerBossVictory(3);
            Check(GameState.TowerFloor == 4, "3층 격파는 4층으로 오른다");
            Check(FloorRecruit.PendingJob && FloorRecruit.PendingFloor == 3,
                "ApplyTowerBossVictory가 직업 대기를 연다");
            Check(FloorRecruit.TryClaim("딜"), "생산 경계에서 고른 딜이 들어온다");
            Check(LifeSystem.GetCharacters().Count == beforeHook + 1, "생산 경계 영입이 명부에 남는다");
            Check(GameState.Bag.GetCount(Economy.LifeItem.SpecialJobToken) == 0,
                "3층은 레이드가 아니라 증표 0");
            Environment.SetEnvironmentVariable("QA_NO_RAID_SPECIAL", null);

            _ = nameof(FloorRecruit.OnCleared);
            _ = nameof(FloorRecruit.TryClaim);
            _ = nameof(FloorRecruit.SeedQaIfRequested);
            _ = nameof(FloorRecruit.AckSpecialBanner);
            _ = nameof(LifeSystem.AddBasicRecruit);
            _ = nameof(GameFlow.ApplyTowerBossVictory);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            FloorRecruit.ResetForTest();

            if (_fail == 0) Debug.Log("[FloorRecruitSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[FloorRecruitSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[FloorRecruitSelfCheck] FAIL {_fail}건");
        }
    }
}
