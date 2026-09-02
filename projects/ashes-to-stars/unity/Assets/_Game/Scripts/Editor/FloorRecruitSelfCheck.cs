using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 층 클리어마다 기본직업 1종, 레이드는 2종 + 특수 직업 캐릭터 확률.
    /// 재도전·던전·잘못된 직업은 거부. 이 경로는 증표를 안 준다.
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

            Check(Math.Abs(FloorRecruit.RaidSpecialChance - 0.01f) < 0.0001f,
                "오너 21:07 특수 직업 확률은 1%");
            Check(!FloorRecruit.PendingJob && FloorRecruit.OfferedJobCount == 0, "시작은 층 보상 없음");
            Check(!FloorRecruit.OnCleared(0) && !FloorRecruit.OnCleared(101), "0·101층은 거부");
            Check(!FloorRecruit.PendingJob, "범위 밖은 대기를 안 연다");

            int roster0 = LifeSystem.GetCharacters().Count;
            long copper = GameState.Wallet.Copper;
            int lv = LifeSystem.GetCharacters()[0].Level;
            int tokens0 = GameState.Bag.GetCount(Economy.LifeItem.SpecialJobToken);

            Check(FloorRecruit.OnCleared(1), "1층 첫 클리어는 직업을 고르게 한다");
            Check(FloorRecruit.PendingJob && FloorRecruit.PendingFloor == 1
                  && FloorRecruit.PendingPicks == 1, "1층은 1종만 대기");
            Check(LifeSystem.GetCharacters().Count == roster0, "고르기 전에는 명부가 그대로다");
            Check(!FloorRecruit.TryClaim("수호기사"), "1차 직업명은 거부");
            Check(LifeSystem.GetCharacters().Count == roster0, "잘못된 직업은 명부를 안 늘린다");
            Check(FloorRecruit.PendingJob, "거부는 대기를 유지한다");

            Check(FloorRecruit.TryClaim("탱"), "탱을 고르면 영입");
            Check(!FloorRecruit.PendingJob && !FloorRecruit.AwaitingPick, "1종이면 고르면 닫힌다");
            Check(!FloorRecruit.TryClaim("딜"), "일반 층 두 번째는 거부");
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
            int beforeRaidRoster = LifeSystem.GetCharacters().Count;
            Check(FloorRecruit.OnCleared(5), "5층 레이드는 2종+특수 판정");
            Check(FloorRecruit.PendingJob && FloorRecruit.PendingFloor == 5
                  && FloorRecruit.PendingPicks == 2, "레이드는 기본 2종");
            Check(FloorRecruit.PendingSpecialPick, "강제 성공은 특수 역할 대기");
            Check(FloorRecruit.RolledSpecial(5), "5층 특수 판정은 한 번만");
            Check(GameState.Bag.GetCount(Economy.LifeItem.SpecialJobToken) == beforeRaid,
                "레이드 성공도 이 경로는 증표 0");
            Check(FloorRecruit.PickTitle().Contains("2종"),
                $"레이드 제목에 2종 (실제 {FloorRecruit.PickTitle()})");
            Check(FloorRecruit.SpecialHint().Contains("당첨"),
                $"2종 전에 당첨 안내 (실제 {FloorRecruit.SpecialHint()})");

            Check(FloorRecruit.TryClaim("버퍼"), "5층 첫 선택은 버퍼");
            Check(FloorRecruit.PendingPicks == 1 && FloorRecruit.PendingJob, "한 명 골라도 1종이 남는다");
            Check(LifeSystem.GetCharacters().Count == beforeRaidRoster + 1, "첫 선택은 1명");
            Check(!LifeSystem.GetCharacters()[beforeRaidRoster].IsSpecialJob, "첫 선택은 기본직업");

            Check(FloorRecruit.TryClaim("딜"), "5층 둘째는 딜");
            Check(!FloorRecruit.PendingJob && FloorRecruit.PendingSpecialPick,
                "2종을 고르면 특수 역할만 남는다");
            Check(FloorRecruit.PickTitle().Contains("특수"),
                $"특수 대기 제목 (실제 {FloorRecruit.PickTitle()})");
            Check(LifeSystem.GetCharacters().Count == beforeRaidRoster + 2, "둘째까지는 기본 2명");

            Check(!FloorRecruit.TryClaim("수호기사"), "특수 역할도 1차 직업명은 거부");
            Check(FloorRecruit.PendingSpecialPick, "특수 거부는 대기를 유지한다");
            Check(FloorRecruit.TryClaim("힐"), "특수 힐러를 고른다");
            Check(!FloorRecruit.AwaitingPick, "특수까지 고르면 닫힌다");
            Check(LifeSystem.GetCharacters().Count == beforeRaidRoster + 3, "특수까지 3명");
            var special = LifeSystem.GetCharacters()[beforeRaidRoster + 2];
            Check(special.IsSpecialJob && special.Job == "성기사" && special.MaxLives == 1
                  && special.Name.StartsWith("영입특수"),
                $"특수 영입은 성기사 1목숨 (실제 {special.Name} {special.Job} lives={special.MaxLives})");
            Check(!LifeSystem.UseRevivePotion(special), "살아있는 특수는 부활초 거부");
            special.IsDeleted = true;
            GameState.Gain(Economy.LifeItem.RebornStone);
            int stones = GameState.Bag.GetCount(Economy.LifeItem.RebornStone);
            Check(!LifeSystem.UseRebornStone(special), "삭제된 특수는 환생석 거부");
            Check(GameState.Bag.GetCount(Economy.LifeItem.RebornStone) == stones,
                "환생석 거부는 아이템을 안 쓴다");
            special.IsDeleted = false;
            GameState.Consume(Economy.LifeItem.RebornStone);
            Check(!Fusion.CanBeMaterial(special), "특수 영입은 합성 재료가 아니다");
            Check(FloorRecruit.PendingSpecialBanner && FloorRecruit.LastSpecialGot == 1
                  && FloorRecruit.LastSpecialName == special.Name,
                "특수 영입 배너가 열린다");
            Check(GameState.Bag.GetCount(Economy.LifeItem.SpecialJobToken) == beforeRaid,
                "특수 캐릭터는 증표를 안 쓴다");
            Check(!FloorRecruit.OnCleared(5), "같은 레이드 재도전은 다시 안 준다");
            Check(LifeSystem.GetCharacters().Count == beforeRaidRoster + 3,
                "재도전은 명부를 안 늘린다");
            Environment.SetEnvironmentVariable("QA_RAID_SPECIAL", null);

            Environment.SetEnvironmentVariable("QA_NO_RAID_SPECIAL", "1");
            int beforeFail = GameState.Bag.GetCount(Economy.LifeItem.SpecialJobToken);
            int beforeFailRoster = LifeSystem.GetCharacters().Count;
            Check(FloorRecruit.OnCleared(10), "10층은 직업 대기는 연다");
            Check(FloorRecruit.PendingPicks == 2 && !FloorRecruit.PendingSpecialPick,
                "강제 실패는 2종만, 특수 없음");
            Check(FloorRecruit.RolledSpecial(10), "10층도 특수 판정은 기록한다");
            Check(GameState.Bag.GetCount(Economy.LifeItem.SpecialJobToken) == beforeFail,
                "강제 실패는 증표 0");
            Check(FloorRecruit.TryClaim("탱") && FloorRecruit.TryClaim("버퍼"),
                "실패 레이드도 기본 2종은 고른다");
            Check(LifeSystem.GetCharacters().Count == beforeFailRoster + 2, "실패 레이드는 2명");
            Check(!FloorRecruit.PendingSpecialPick && FloorRecruit.LastSpecialGot == 1,
                "실패는 이전 5층 특수 기록을 안 지운다");
            Environment.SetEnvironmentVariable("QA_NO_RAID_SPECIAL", null);

            FloorRecruit.AckSpecialBanner();
            Check(!FloorRecruit.PendingSpecialBanner, "확인하면 특수 배너가 닫힌다");

            GameState.ForgetInMemoryForTest();
            FloorRecruit.ForgetInMemoryForTest();
            Check(FloorRecruit.OfferedJob(1) && FloorRecruit.OfferedJob(5)
                  && FloorRecruit.RolledSpecial(5) && FloorRecruit.LastGrantedJob == "버퍼"
                  && FloorRecruit.LastSpecialJob == "성기사",
                "층 보상·마지막 영입·특수 영입이 저장에서 되살아난다");

            Environment.SetEnvironmentVariable("QA_NO_FLOOR_REWARD", "1");
            FloorRecruit.ResetForTest();
            Check(!FloorRecruit.OnCleared(1), "QA_NO_FLOOR_REWARD=1이면 지급 거부");
            Check(!FloorRecruit.PendingJob && !FloorRecruit.PendingSpecialPick
                  && FloorRecruit.OfferedJobCount == 0,
                "강제 끔은 대기가 없다");
            Environment.SetEnvironmentVariable("QA_NO_FLOOR_REWARD", null);

            Environment.SetEnvironmentVariable("QA_FLOOR_REWARD", "1");
            GameState.ResetAll();
            FloorRecruit.ResetForTest();
            FloorRecruit.SeedQaIfRequested();
            Check(FloorRecruit.PendingJob && FloorRecruit.PendingFloor == 5
                  && FloorRecruit.PendingPicks == 2
                  && FloorRecruit.PendingSpecialPick
                  && FloorRecruit.PickTitle().Contains("2종")
                  && GameState.Bag.GetCount(Economy.LifeItem.SpecialJobToken) == 0,
                "QA_FLOOR_REWARD=1이면 5층 2종·특수 대기·증표 0");
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
            Check(FloorRecruit.PendingJob && FloorRecruit.PendingFloor == 3
                  && FloorRecruit.PendingPicks == 1,
                "ApplyTowerBossVictory가 직업 대기를 연다");
            Check(FloorRecruit.TryClaim("딜"), "생산 경계에서 고른 딜이 들어온다");
            Check(LifeSystem.GetCharacters().Count == beforeHook + 1, "생산 경계 영입이 명부에 남는다");
            Check(GameState.Bag.GetCount(Economy.LifeItem.SpecialJobToken) == 0,
                "3층은 레이드가 아니라 증표 0");

            int beforeRaidHook = LifeSystem.GetCharacters().Count;
            GameFlow.ApplyTowerBossVictory(5);
            Check(FloorRecruit.PendingPicks == 2 && !FloorRecruit.PendingSpecialPick,
                "생산 경계 5층도 2종·강제실패 특수 없음");
            Check(FloorRecruit.TryClaim("탱") && FloorRecruit.TryClaim("힐"),
                "생산 경계 레이드 2종");
            Check(LifeSystem.GetCharacters().Count == beforeRaidHook + 2,
                "생산 경계 레이드는 2명");
            Environment.SetEnvironmentVariable("QA_NO_RAID_SPECIAL", null);

            _ = nameof(FloorRecruit.OnCleared);
            _ = nameof(FloorRecruit.TryClaim);
            _ = nameof(FloorRecruit.SeedQaIfRequested);
            _ = nameof(FloorRecruit.AckSpecialBanner);
            _ = nameof(FloorRecruit.PickTitle);
            _ = nameof(LifeSystem.AddBasicRecruit);
            _ = nameof(LifeSystem.AddSpecialRecruit);
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
