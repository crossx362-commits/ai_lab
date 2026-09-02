using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 특수 직업 첫 슬라이스. 증표 전직·1회 사망 소멸·부활초/환생석 거부·50층 드랍 하한.
    /// </summary>
    public static class SpecialJobSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Special Job Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            DefenseState.ResetForTest();

            var roster = LifeSystem.GetCharacters();
            Check(roster.Count >= 2, $"로스터 자동 생성 (실제 {roster.Count})");
            var hero = roster[0];
            var bench = roster[1];

            Check(!hero.IsSpecialJob, "신규 로스터는 일반 직업");
            Check(!LifeSystem.CanBecomeSpecial(hero), "증표 0이면 전직 불가");
            Check(!LifeSystem.TryBecomeSpecial(hero), "증표 0이면 거부");
            Check(!hero.IsSpecialJob, "거부하면 플래그가 안 붙는다");

            GameState.Gain(Economy.LifeItem.SpecialJobToken);
            Check(GameState.Bag.GetCount(Economy.LifeItem.SpecialJobToken) == 1, "증표 1장 획득");
            hero.Advancement = AdvancementTier.First;
            hero.Job = "수호기사";
            Check(LifeSystem.CanBecomeSpecial(hero), "증표가 있으면 전직 가능");
            Check(LifeSystem.TryBecomeSpecial(hero), "증표로 특수 직업이 된다");
            Check(hero.IsSpecialJob && hero.Job == "성기사",
                $"수호기사 증표 전직은 성기사 (실제 {hero.Job})");
            Check(hero.MaxLives == 1, "특수 직업 목숨 상한 1");
            Check(GameState.Bag.GetCount(Economy.LifeItem.SpecialJobToken) == 0, "전직이 증표를 소비한다");

            int before = GameState.Bag.GetCount(Economy.LifeItem.SpecialJobToken);
            GameState.Gain(Economy.LifeItem.SpecialJobToken);
            Check(!LifeSystem.TryBecomeSpecial(hero), "이미 특수 직업이면 거부");
            Check(GameState.Bag.GetCount(Economy.LifeItem.SpecialJobToken) == before + 1,
                "거부하면 증표를 안 쓴다");
            GameState.Consume(Economy.LifeItem.SpecialJobToken);

            var rescue = new CharacterRecord("재건시드", "딜", 1) { IsRescue = true };
            roster.Add(rescue);
            GameState.Gain(Economy.LifeItem.SpecialJobToken);
            Check(!LifeSystem.TryBecomeSpecial(rescue), "긴급 재건은 특수 직업이 될 수 없다");
            Check(GameState.Bag.GetCount(Economy.LifeItem.SpecialJobToken) == 1,
                "재건 거부는 증표를 안 쓴다");
            GameState.Consume(Economy.LifeItem.SpecialJobToken);
            roster.Remove(rescue);

            LifeSystem.PersistRoster();
            LifeSystem.ForgetInMemoryForTest();
            roster = LifeSystem.GetCharacters();
            hero = roster[0];
            bench = roster[1];
            Check(hero.IsSpecialJob && hero.MaxLives == 1,
                "특수 직업 플래그가 저장에서 되살아난다");
            Check(!bench.IsSpecialJob && bench.MaxLives == 3,
                "다른 캐릭터는 일반 3목숨으로 남는다");
            Check(!Fusion.CanBeMaterial(hero), "살아있는 특수 직업은 합성 재료가 아니다(§3)");

            LifeSystem.RegisterDeath(hero, isPvp: true);
            Check(!hero.IsDeleted && hero.DeathCount == 0,
                "특수 직업 PvP 사망은 소멸하지 않는다(§3·§4)");

            LifeSystem.RegisterDeath(hero, isPvp: false);
            Check(hero.IsDeleted && hero.DeathCount == 1,
                "특수 직업 PvE 1회 사망은 즉시 소멸");
            Check(LifeSystem.GetDeletedCharacters().Contains(hero),
                "소멸한 특수 직업은 영묘에 기록만 남는다");

            LifeSystem.RegisterDeath(bench, isPvp: false);
            Check(!bench.IsDeleted && bench.DeathCount == 1,
                "일반 직업 1회 사망은 삭제되지 않는다");

            GameState.Gain(Economy.LifeItem.RevivalTea);
            int tea = LifeSystem.GetRevivePotions();
            Check(!LifeSystem.UseRevivePotion(hero), "삭제된 특수 직업에 부활초 거부");
            Check(LifeSystem.GetRevivePotions() == tea, "부활초 거부는 아이템을 안 쓴다");
            Check(LifeSystem.UseRevivePotion(bench), "일반 직업은 부활초가 통한다");
            Check(bench.DeathCount == 0, "일반 부활초가 사망 카운트를 깎는다");

            GameState.Gain(Economy.LifeItem.RebornStone);
            int stones = LifeSystem.GetRebornStones();
            Check(!LifeSystem.UseRebornStone(hero), "특수 직업은 환생석으로 안 돌아온다");
            Check(hero.IsDeleted, "환생 거부 뒤에도 삭제 상태");
            Check(LifeSystem.GetRebornStones() == stones, "환생 거부는 환생석을 안 쓴다");

            bench.IsDeleted = true;
            bench.DeathCount = 3;
            Check(LifeSystem.UseRebornStone(bench), "일반 직업은 환생석이 통한다");
            Check(!bench.IsDeleted && bench.DeathCount == 0, "일반 환생은 사망 0으로 돌아온다");

            Check(!Economy.CanDropSpecialJobToken(10)
                  && !Economy.CanDropSpecialJobToken(49)
                  && Economy.CanDropSpecialJobToken(50)
                  && Economy.CanDropSpecialJobToken(100),
                "증표는 50층 이상만");

            int tokenAt10 = 0, tokenAt50 = 0, stoneAt10 = 0;
            for (uint s = 1; s <= 8000u; s++)
            {
                var r10 = Rng.Stream(s, 10, SeedChannel.Drop);
                foreach (var d in Economy.RollBattleDrops(Economy.DropSource.Tower10Boss, 1, ref r10, 10))
                {
                    if (d == Economy.LifeItem.SpecialJobToken) tokenAt10++;
                    if (d == Economy.LifeItem.RebornStone) stoneAt10++;
                }
                var r50 = Rng.Stream(s, 50, SeedChannel.Drop);
                foreach (var d in Economy.RollBattleDrops(Economy.DropSource.Tower10Boss, 1, ref r50, 50))
                    if (d == Economy.LifeItem.SpecialJobToken) tokenAt50++;
            }
            Check(tokenAt10 == 0, $"10층 대보스는 증표 0 (실제 {tokenAt10})");
            Check(tokenAt50 > 0, $"50층 대보스는 증표가 나온다 (실제 {tokenAt50}/8000)");
            Check(stoneAt10 > 0, $"10층 환생석은 그대로 나온다 (실제 {stoneAt10}/8000)");

            string old = Environment.GetEnvironmentVariable("QA_SPECIAL_JOB");
            Environment.SetEnvironmentVariable("QA_SPECIAL_JOB", "1");
            GameState.ResetAll();
            LifeSystem.ResetAll();
            LifeSystem.SeedSpecialJobQaIfRequested();
            var seeded = LifeSystem.GetCharacters()[0];
            Check(seeded.IsSpecialJob, "QA_SPECIAL_JOB=1이면 첫 캐릭터가 특수 직업");
            Environment.SetEnvironmentVariable("QA_SPECIAL_JOB", old);

            _ = nameof(LifeSystem.TryBecomeSpecial);
            _ = nameof(LifeSystem.CanBecomeSpecial);
            _ = nameof(LifeSystem.SeedSpecialJobQaIfRequested);
            _ = nameof(Economy.CanDropSpecialJobToken);
            _ = nameof(Economy.SpecialJobTokenMinFloor);
            _ = nameof(ItemAtlas.KeyFor);

            if (_fail == 0) Debug.Log("[SpecialJobSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[SpecialJobSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[SpecialJobSelfCheck] FAIL {_fail}건");
        }
    }
}
