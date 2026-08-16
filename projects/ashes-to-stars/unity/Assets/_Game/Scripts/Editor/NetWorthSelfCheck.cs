using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>대출 한도가 장비·영지 평가를 읽는다. QA_NO면 지갑만(§18-5).</summary>
    public static class NetWorthSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Net Worth Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(NetWorth.EnvShow);
            string no = Environment.GetEnvironmentVariable(NetWorth.EnvNo);
            Environment.SetEnvironmentVariable(NetWorth.EnvShow, null);
            Environment.SetEnvironmentVariable(NetWorth.EnvNo, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            EstateBuild.ResetForTest();
            EstateDefense.ResetForTest();
            NetWorth.ResetForTest();
            RacePrefs.Set(RaceId.인간);

            Check(NetWorth.Assets() == 0 && GameState.LoanLimit == 0,
                "무자산이면 한도 0");
            Check(NetWorth.KeepCopper() == 0 && NetWorth.GearCopper() == 0,
                "본성 1·장비 없음은 평가 0");

            GameState.Grant(100_000);
            Check(NetWorth.Assets() == 100_000, $"지갑만 10골드 (실제 {NetWorth.Assets()})");
            Check(GameState.LoanLimit == 30_000,
                $"T1 지갑 10골드는 한도 3골드 (실제 {GameState.LoanLimit})");

            GameState.ResetAll();
            Equipment.ResetAll();
            EstateBuild.SetLevelForTest(3);
            long keep = EstateBuild.UpgradeCost(1) + EstateBuild.UpgradeCost(2);
            Check(keep == 300_000, $"본성 3 건설비 30골드 (실제 {keep})");
            Check(NetWorth.KeepCopper() == keep,
                $"본성 평가 {keep} (실제 {NetWorth.KeepCopper()})");
            Check(GameState.Wallet.Copper == 0, "지갑 0");
            Check(NetWorth.Assets() == keep, $"자산 = 본성 (실제 {NetWorth.Assets()})");
            Check(GameState.LoanLimit == 90_000,
                $"본성 3·지갑 0 한도 9골드 (실제 {GameState.LoanLimit})");
            Check(GameState.Borrow(90_000), "평가액으로 9골드 대출");
            Check(!GameState.Borrow(1), "한도를 넘으면 거절");
            Check(GameState.LoanLimit == 90_000,
                $"빌린 돈이 한도를 안 올린다 (실제 {GameState.LoanLimit})");

            GameState.ResetAll();
            Equipment.ResetAll();
            EstateBuild.ResetForTest();
            var gear = Equipment.AddUnequippedForTest(Equipment.LeatherArmorRecipe);
            Check(gear != null && BankruptcySeize.SaleCopper(gear) == 12_000,
                "가죽 흉갑 처분가 1골드 20실버");
            Check(NetWorth.GearCopper() == 12_000,
                $"장비 평가 12000 (실제 {NetWorth.GearCopper()})");
            Check(GameState.LoanLimit == 3_600,
                $"흉갑만 한도 36실버 (실제 {GameState.LoanLimit})");

            GameState.ResetAll();
            Equipment.ResetAll();
            EstateBuild.SetLevelForTest(3);
            Equipment.AddUnequippedForTest(Equipment.LeatherArmorRecipe);
            Check(NetWorth.Assets() == 312_000, $"본성+흉갑 31골드 20실버 (실제 {NetWorth.Assets()})");
            Check(GameState.LoanLimit == 93_600,
                $"합산 한도 9골드 36실버 (실제 {GameState.LoanLimit})");

            EstateDefense.SetLevelForTest(EstateDefense.Kind.화살탑, 1);
            long towerCost = EstateDefense.UpgradeCost(0);
            Check(towerCost == 48_000, $"화살탑 1 건설비 4골드 80실버 (실제 {towerCost})");
            Check(NetWorth.DefenseCopper() == towerCost,
                $"방어 평가 {towerCost} (실제 {NetWorth.DefenseCopper()})");
            Check(NetWorth.Assets() == 312_000 + towerCost,
                $"방어가 자산에 더해진다 (실제 {NetWorth.Assets()})");

            GameState.ForgetInMemoryForTest();
            EstateBuild.ForgetInMemoryForTest();
            EstateDefense.ForgetInMemoryForTest();
            Equipment.ForgetInMemoryForTest();
            Check(EstateBuild.KeepLevel == 3 && NetWorth.KeepCopper() == 300_000,
                "재기동 뒤에도 본성 평가가 남는다");

            Environment.SetEnvironmentVariable(NetWorth.EnvNo, "1");
            GameState.ResetAll();
            Equipment.ResetAll();
            EstateBuild.SetLevelForTest(3);
            Equipment.AddUnequippedForTest(Equipment.LeatherArmorRecipe);
            Check(NetWorth.Blocked, "QA_NO면 차단");
            Check(NetWorth.Assets() == 0 && GameState.LoanLimit == 0,
                $"차단하면 지갑 0·한도 0 (자산 {NetWorth.Assets()} 한도 {GameState.LoanLimit})");
            Check(NetWorth.Line().IndexOf("지갑만", StringComparison.Ordinal) >= 0,
                $"차단 문구 (실제 {NetWorth.Line()})");
            Environment.SetEnvironmentVariable(NetWorth.EnvNo, null);

            Environment.SetEnvironmentVariable(NetWorth.EnvShow, "1");
            GameState.ResetAll();
            Equipment.ResetAll();
            EstateBuild.ResetForTest();
            NetWorth.ResetForTest();
            NetWorth.SeedQaIfRequested();
            Check(EstateBuild.KeepLevel == 3, $"시드 본성 3 (실제 {EstateBuild.KeepLevel})");
            Check(NetWorth.GearCopper() == 12_000, "시드 흉갑");
            Check(GameState.Wallet.Copper == 0, "시드 지갑 0");
            Check(GameState.LoanLimit == 93_600, $"시드 한도 93600 (실제 {GameState.LoanLimit})");
            Check(NetWorth.Line().IndexOf("§18-5", StringComparison.Ordinal) >= 0
                  && NetWorth.Line().IndexOf("한도", StringComparison.Ordinal) >= 0,
                $"시드 문구 (실제 {NetWorth.Line()})");
            Environment.SetEnvironmentVariable(NetWorth.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string stateSrc = File.ReadAllText(Path.Combine(runtime, "GameState.cs"));
            string estateSrc = File.ReadAllText(Path.Combine(runtime, "EstateScreen.cs"));
            string towerSrc = File.ReadAllText(Path.Combine(runtime, "TowerScreen.cs"));
            Check(stateSrc.IndexOf("NetWorth.Assets", StringComparison.Ordinal) >= 0,
                "GameState.LoanLimit가 NetWorth.Assets를 읽는다");
            Check(estateSrc.IndexOf("NetWorth.Line", StringComparison.Ordinal) >= 0
                  && estateSrc.IndexOf("NetWorth.SeedQaIfRequested", StringComparison.Ordinal) >= 0,
                "영지 현황이 Line·Seed를 읽는다");
            Check(towerSrc.IndexOf("NetWorth.Line", StringComparison.Ordinal) >= 0,
                "탑 대출 화면이 Line을 읽는다");

            _ = nameof(NetWorth.Assets);
            _ = nameof(NetWorth.Line);
            _ = nameof(NetWorth.SeedQaIfRequested);

            Environment.SetEnvironmentVariable(NetWorth.EnvShow, show);
            Environment.SetEnvironmentVariable(NetWorth.EnvNo, no);
            NetWorth.ResetForTest();
            GameState.ResetAll();

            if (_fail == 0) Debug.Log("[NetWorthSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[NetWorthSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[NetWorthSelfCheck] FAIL {_fail}건");
        }
    }
}
