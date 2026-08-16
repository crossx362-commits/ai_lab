using System;
using System.Text;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>본성 레벨·건설 시간이 §18-12와 같은지.</summary>
    public static class EstateBuildSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            Environment.SetEnvironmentVariable("QA_ESTATE_KEEP_FAST", null);
            EstateBuild.ResetForTest();
            GameState.ResetAll();

            Check(Math.Abs(EstateBuild.UpgradeSeconds(1) - 300.0) < 0.01,
                "1→2는 5분(§18-12)");
            Check(Math.Abs(EstateBuild.UpgradeSeconds(3) - 300.0 * Math.Pow(1.6, 2)) < 0.1,
                "3→4는 5분×1.6²");
            Check(EstateBuild.UpgradeSeconds(20) == EstateBuild.CapSeconds,
                "고레벨은 24시간 상한");
            Check(EstateBuild.UpgradeCost(1) == (long)(8.0 * 1.5 * Economy.COPPER_PER_GOLD),
                "1→2 비용은 8×1.5 G/h");
            Check(EstateBuild.KeepLevel == 1, "시작 본성은 1");
            Check(EstateBuild.WarehouseCapCopper() == 12L * Economy.COPPER_PER_GOLD,
                "창고는 본성×12 G/h");

            Check(!EstateBuild.TryStartKeep(), "골드 없으면 안 오른다");
            GameState.Grant(EstateBuild.UpgradeCost(1));
            long gold = GameState.Wallet.Copper;
            long now = 1_700_000_000;
            EstateBuild.NowUnix = () => now;
            Check(EstateBuild.TryStartKeep(), "비용을 내면 공사가 시작된다");
            Check(GameState.Wallet.Copper == gold - EstateBuild.UpgradeCost(1), "공사비가 빠진다");
            Check(EstateBuild.KeepBusy && EstateBuild.KeepLevel == 1, "끝나기 전엔 레벨이 그대로다");
            Check(!EstateBuild.TryStartKeep(), "공사 중엔 다시 못 올린다");
            now += 299;
            Check(EstateBuild.KeepLevel == 1, "299초엔 아직 1");
            now += 2;
            Check(EstateBuild.KeepLevel == 2, "시간이 되면 수령 없이 2가 된다");
            Check(!EstateBuild.KeepBusy, "끝나면 슬롯이 비한다");
            Check(EstateBuild.WarehouseCapCopper() == 24L * Economy.COPPER_PER_GOLD,
                "본성 2면 창고도 2배");

            EstateBuild.ResetForTest();
            GameState.ResetAll();
            if (_fail > 0)
            {
                Debug.LogError("[EstateBuildSelfCheck] FAIL\n" + _log);
                throw new InvalidOperationException("EstateBuildSelfCheck FAIL " + _fail);
            }
            Debug.Log("[EstateBuildSelfCheck] PASS\n" + _log);
        }
    }
}
