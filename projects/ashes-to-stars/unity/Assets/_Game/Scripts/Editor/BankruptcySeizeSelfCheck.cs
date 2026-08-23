using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>연체 3회 파산이면 건물 −1·비장착 30%가 빚으로 간다. QA_NO면 안 건드린다(§18-5).</summary>
    public static class BankruptcySeizeSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Bankruptcy Seize Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(BankruptcySeize.EnvShow);
            string no = Environment.GetEnvironmentVariable(BankruptcySeize.EnvNo);
            Environment.SetEnvironmentVariable(BankruptcySeize.EnvShow, null);
            Environment.SetEnvironmentVariable(BankruptcySeize.EnvNo, null);
            Environment.SetEnvironmentVariable("QA_LOAN_OVERDUE", null);

            const long Hour = 3600L;
            const long Term = Economy.LoanTermHours * Hour;
            long now = 2_000_000L;

            GameState.ResetAll();
            LifeSystem.ResetAll();
            RacePrefs.Set(RaceId.인간);
            EstateBuild.SetLevelForTest(3);
            for (int i = 0; i < EstateDefense.All.Length; i++)
                EstateDefense.SetLevelForTest(EstateDefense.All[i], 2);
            Check(EstateBuild.KeepLevel == 3, $"본성 3 (실제 {EstateBuild.KeepLevel})");
            Check(EstateDefense.TotalLevel() == 8, $"방어 합 8 (실제 {EstateDefense.TotalLevel()})");
            Check(BankruptcySeize.TakeCount(10) == 3 && BankruptcySeize.TakeCount(1) == 0,
                "10개→3 · 1개→0");

            GameState.SetTowerFloorForTest(30);
            GameState.Grant(200_000);
            Check(GameState.Borrow(30_000, now), "30층에서 대출");
            GameState.RefreshSanctions(now + Term * 2 + 1);
            Check(GameState.OverdueCount == 2, $"연체 2회는 강등 전 (실제 {GameState.OverdueCount})");
            Check(EstateBuild.KeepLevel == 3 && EstateDefense.TotalLevel() == 8,
                "연체 2회는 건물을 안 내린다");

            GameState.RefreshSanctions(now + Term * 3 + 1);
            Check(GameState.OverdueCount == 3 || GameState.Debt == 0,
                $"연체 3회 또는 압류로 빚 0 (연체 {GameState.OverdueCount}, 빚 {GameState.Debt})");
            Check(GameState.BankruptcyCount == 1, $"파산 1회 (실제 {GameState.BankruptcyCount})");
            Check(EstateBuild.KeepLevel == 2, $"본성 3→2 (실제 {EstateBuild.KeepLevel})");
            Check(EstateDefense.Level(EstateDefense.Kind.화살탑) == 1
                  && EstateDefense.Level(EstateDefense.Kind.함정) == 1,
                $"방어 2→1 (화살 {EstateDefense.Level(EstateDefense.Kind.화살탑)})");
            Check(BankruptcySeize.DidDowngrade && BankruptcySeize.KeepLine().Contains("§18-5"),
                $"강등 문구 (실제 {BankruptcySeize.KeepLine()})");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            EstateBuild.SetLevelForTest(1);
            EstateDefense.SetLevelForTest(EstateDefense.Kind.화살탑, 0);
            GameState.SetTowerFloorForTest(30);
            GameState.Grant(200_000);
            GameState.Borrow(30_000, now);
            GameState.RefreshSanctions(now + Term * 3 + 1);
            Check(EstateBuild.KeepLevel == 1, "본성 1은 그대로");
            Check(EstateDefense.Level(EstateDefense.Kind.화살탑) == 0, "방어 0은 그대로");
            Check(GameState.BankruptcyCount == 1, "빈 영지도 파산 1회");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            GameState.SetTowerFloorForTest(30);
            GameState.Gain(Economy.LifeItem.CraftHide, 10);
            for (int i = 0; i < 10; i++)
                Equipment.AddUnequippedForTest(Equipment.LeatherArmorRecipe);
            Check(Equipment.Unequipped().Count == 10, "비장착 흉갑 10");
            GameState.Grant(200_000);
            GameState.Borrow(30_000, now);
            long debt = GameState.Debt;
            GameState.RefreshSanctions(now + Term * 3 + 1);
            Check(GameState.Bag.GetCount(Economy.LifeItem.CraftHide) == 7,
                $"가죽 10→7 (실제 {GameState.Bag.GetCount(Economy.LifeItem.CraftHide)})");
            Check(Equipment.Unequipped().Count == 7,
                $"흉갑 10→7 (실제 {Equipment.Unequipped().Count})");
            Check(BankruptcySeize.GearTaken == 3 && BankruptcySeize.ItemTaken == 3,
                $"압류 장비3·가죽3 (장비 {BankruptcySeize.GearTaken}, 가죽 {BankruptcySeize.ItemTaken})");
            long expect = 3 * BankruptcySeize.HideCopper + 3 * BankruptcySeize.GearBaseCopper;
            Check(BankruptcySeize.Copper == expect,
                $"처분 {expect} (실제 {BankruptcySeize.Copper})");
            Check(GameState.Debt == 0, $"처분이 빚을 덮는다 (빚 {GameState.Debt}, 시작 {debt})");
            Check(BankruptcySeize.ItemLine().Contains("30%") && BankruptcySeize.ItemLine().Contains("§18-5"),
                $"압류 문구 (실제 {BankruptcySeize.ItemLine()})");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            var roster = LifeSystem.GetCharacters();
            Check(roster.Count >= 1, "장착 보호용 로스터");
            var tank = roster[0];
            tank.Advancement = AdvancementTier.First;
            tank.Job = "수호기사";
            LifeSystem.PersistRoster();
            var worn = Equipment.AddUnequippedForTest(Equipment.LeatherArmorRecipe);
            Check(worn != null && Equipment.TryEquip(tank, worn.Id), "흉갑 1개 장착");
            for (int i = 0; i < 10; i++)
                Equipment.AddUnequippedForTest(Equipment.LeatherArmorRecipe);
            Check(Equipment.Unequipped().Count == 10, "장착 제외 10");
            GameState.SetTowerFloorForTest(30);
            GameState.Grant(200_000);
            GameState.Borrow(30_000, now);
            GameState.RefreshSanctions(now + Term * 3 + 1);
            Check(Equipment.Worn(tank, EquipSlot.Armor) != null
                  && Equipment.Worn(tank, EquipSlot.Armor).Id == worn.Id,
                "장착 장비는 안 가져간다");
            Check(Equipment.Unequipped().Count == 7, $"비장착만 3장 (실제 {Equipment.Unequipped().Count})");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            EstateBuild.SetLevelForTest(3);
            GameState.SetTowerFloorForTest(30);
            GameState.Gain(Economy.LifeItem.CraftHide, 10);
            GameState.Grant(200_000);
            GameState.Borrow(30_000, now);
            GameState.RefreshSanctions(now + Term * 3 + 1);
            GameState.ForgetInMemoryForTest();
            Equipment.ForgetInMemoryForTest();
            EstateDefense.ForgetInMemoryForTest();
            Check(EstateBuild.KeepLevel == 2 && GameState.BankruptcyCount == 1,
                $"재기동 뒤에도 본성2·파산1 (본성 {EstateBuild.KeepLevel})");
            Check(GameState.Bag.GetCount(Economy.LifeItem.CraftHide) == 7
                  && BankruptcySeize.Applied,
                "재기동 뒤에도 가죽7·압류 기록");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            EstateBuild.SetLevelForTest(3);
            GameState.SetTowerFloorForTest(30);
            GameState.Grant(20_000);
            Check(GameState.Borrow(1_000, now), "작은 빚 시나리오 대출");
            GameState.Gain(Economy.LifeItem.CraftHide, 10);
            long walletBefore = GameState.Wallet.Copper;
            GameState.RefreshSanctions(now + Term * 3 + 1);
            Check(GameState.Debt == 0, "작은 빚은 처분으로 0");
            Check(GameState.Wallet.Copper == walletBefore + 3 * BankruptcySeize.HideCopper - 1_000,
                $"남은 대금은 지갑 (지갑 {GameState.Wallet.Copper})");

            Environment.SetEnvironmentVariable(BankruptcySeize.EnvNo, "1");
            GameState.ResetAll();
            LifeSystem.ResetAll();
            EstateBuild.SetLevelForTest(3);
            for (int i = 0; i < EstateDefense.All.Length; i++)
                EstateDefense.SetLevelForTest(EstateDefense.All[i], 2);
            GameState.Gain(Economy.LifeItem.CraftHide, 10);
            for (int i = 0; i < 10; i++)
                Equipment.AddUnequippedForTest(Equipment.LeatherArmorRecipe);
            GameState.SetTowerFloorForTest(30);
            GameState.Grant(200_000);
            GameState.Borrow(30_000, now);
            GameState.RefreshSanctions(now + Term * 3 + 1);
            Check(GameState.BankruptcyCount == 1, "차단해도 파산 횟수는 오른다");
            Check(EstateBuild.KeepLevel == 3 && EstateDefense.TotalLevel() == 8,
                "차단하면 건물 불변");
            Check(GameState.Bag.GetCount(Economy.LifeItem.CraftHide) == 10
                  && Equipment.Unequipped().Count == 10,
                "차단하면 가방 불변");
            Check(!BankruptcySeize.Applied && BankruptcySeize.Line().Contains("없음"),
                $"차단 문구 (실제 {BankruptcySeize.Line()})");
            Environment.SetEnvironmentVariable(BankruptcySeize.EnvNo, null);

            Environment.SetEnvironmentVariable(BankruptcySeize.EnvShow, "1");
            GameState.ResetAll();
            LifeSystem.ResetAll();
            BankruptcySeize.SeedQaIfRequested();
            Check(GameState.BankruptcyCount == 1, $"시드 파산 1 (실제 {GameState.BankruptcyCount})");
            Check(EstateBuild.KeepLevel == 2, $"시드 본성 2 (실제 {EstateBuild.KeepLevel})");
            Check(GameState.Bag.GetCount(Economy.LifeItem.CraftHide) == 7, "시드 가죽 7");
            Check(Equipment.Unequipped().Count == 7, "시드 흉갑 7");
            Check(BankruptcySeize.Line().Contains("건물") && BankruptcySeize.Line().Contains("§18-5"),
                $"시드 문구 (실제 {BankruptcySeize.Line()})");
            Environment.SetEnvironmentVariable(BankruptcySeize.EnvShow, null);

            string state = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/GameState.cs"));
            string estate = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/EstateScreen.cs"));
            Check(state.Contains("BankruptcySeize.Apply()"),
                "ApplyBankruptcy가 BankruptcySeize.Apply를 읽는다");
            Check(estate.Contains("KeepLine") && estate.Contains("EnvShow")
                  && estate.Contains("SeedQaIfRequested"),
                "영지 현황이 강등·압류 문구·시드를 읽는다");

            string seize = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/BankruptcySeize.cs"));
            Check(seize.Contains("EstateStatusHud.ShortCopper(_copper)")
                  && seize.IndexOf("FormatCurrency(_copper)") < 0,
                "압류·상환은 ShortCopper만");

            _ = nameof(BankruptcySeize.Apply);
            _ = nameof(BankruptcySeize.SeedQaIfRequested);
            _ = nameof(EstateBuild.DowngradeOne);
            _ = nameof(EstateDefense.DowngradeAll);

            Environment.SetEnvironmentVariable(BankruptcySeize.EnvShow, show);
            Environment.SetEnvironmentVariable(BankruptcySeize.EnvNo, no);
            GameState.ResetAll();
            LifeSystem.ResetAll();

            if (_fail > 0)
            {
                Debug.LogError("[BankruptcySeizeSelfCheck] FAIL\n" + _log);
                throw new InvalidOperationException("BankruptcySeizeSelfCheck FAIL " + _fail);
            }
            Debug.Log("[BankruptcySeizeSelfCheck] PASS\n" + _log);
        }
    }
}
