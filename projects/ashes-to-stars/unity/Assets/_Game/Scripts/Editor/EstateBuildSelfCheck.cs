using System;
using System.Text;
using UnityEngine;
using UnityEditor;

namespace AshesToStars
{
    /// <summary>본성·핵심 건물 레벨·건설이 §18-12·SPEC §2-3과 같은지.</summary>
    public static class EstateBuildSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Estate Build Self Check")]
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
            Check(EstateBuild.UpgradeCost(EstateGrid.Cell.Mine, 1) == EstateBuild.UpgradeCost(1),
                "Cell 비용 오버로드는 같은 곡선");
            Check(EstateBuild.KeepLevel == 1, "시작 본성은 1");
            Check(EstateBuild.Level(EstateGrid.Cell.Mine) == 1, "시작 광산도 1");
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

            // 광산 업그레이드 — 본성 상한 안에서
            GameState.Grant(EstateBuild.UpgradeCost(EstateGrid.Cell.Mine, 1));
            Check(EstateBuild.TryStartUpgrade(EstateGrid.Cell.Mine), "광산 1→2 공사");
            Check(EstateBuild.Busy(EstateGrid.Cell.Mine) && EstateBuild.Level(EstateGrid.Cell.Mine) == 1,
                "광산 공사 중엔 레벨 그대로");
            now += 301;
            Check(EstateBuild.Level(EstateGrid.Cell.Mine) == 2, "광산도 시각이 되면 2");
            Check(!EstateBuild.Busy(EstateGrid.Cell.Mine), "광산 슬롯이 비한다");

            // 본성 상한 — 광산은 KeepLevel을 넘지 못한다
            Check(EstateBuild.WhyCannotUpgrade(EstateGrid.Cell.Mine) != null
                && EstateBuild.WhyCannotUpgrade(EstateGrid.Cell.Mine).Contains("본성"),
                "광산은 본성 Lv2가 상한");
            Check(!EstateBuild.TryStartUpgrade(EstateGrid.Cell.Mine), "상한이면 광산 공사 거부");

            // 병렬 busy — 본성·광산이 동시에 공사 가능
            EstateBuild.SetLevelForTest(3);
            // SetLevelForTest는 본성만 건드린다. 광산은 2로 남아 있음.
            GameState.Grant(EstateBuild.UpgradeCost(3) + EstateBuild.UpgradeCost(2));
            Check(EstateBuild.TryStartKeep(), "본성 3→4 병렬 시작");
            Check(EstateBuild.TryStartUpgrade(EstateGrid.Cell.Mine), "광산 2→3도 동시에");
            Check(EstateBuild.KeepBusy && EstateBuild.Busy(EstateGrid.Cell.Mine),
                "두 칸이 동시에 busy");
            long keepLeft = EstateBuild.RemainingSeconds(EstateGrid.Cell.Keep);
            long mineLeft = EstateBuild.RemainingSeconds(EstateGrid.Cell.Mine);
            Check(keepLeft == (long)Math.Ceiling(EstateBuild.UpgradeSeconds(3)),
                $"본성 남은 초는 3→4 곡선 (실제 {keepLeft})");
            Check(mineLeft == (long)Math.Ceiling(EstateBuild.UpgradeSeconds(2)),
                $"광산 남은 초는 2→3 곡선 (실제 {mineLeft})");
            Check(keepLeft != mineLeft, "칸마다 남은 시간이 따로다");
            now += mineLeft + 1;
            Check(EstateBuild.Level(EstateGrid.Cell.Mine) == 3
                && EstateBuild.KeepBusy && EstateBuild.KeepLevel == 3,
                "광산만 먼저 끝나고 본성은 공사 중");
            now += keepLeft + 1;
            Check(EstateBuild.KeepLevel == 4 && !EstateBuild.KeepBusy, "본성도 나중에 4");

            EstateBuild.SetLevelForTest(EstateBuild.MaxKeep);
            Check(EstateBuild.WhyCannotUpgrade() != null
                && EstateBuild.WhyCannotUpgrade().Contains("상한"),
                "본성 13이 상한");
            Check(!EstateBuild.TryStartKeep(), "본성 상한이면 공사 거부");

            // prefs 이주 — 옛 ats.estate.keep* → Keep 셀
            EstateBuild.ResetForTest();
            GameState.ResetAll();
            PlayerPrefs.SetInt("ats.estate.keep", 5);
            PlayerPrefs.SetInt("ats.estate.keep_to", 0);
            PlayerPrefs.SetString("ats.estate.keep_done", "0");
            PlayerPrefs.SetString("ats.estate.keep_orig", "0");
            PlayerPrefs.SetString("ats.estate.keep_job", "0");
            PlayerPrefs.Save();
            EstateBuild.ForgetInMemoryForTest();
            Check(EstateBuild.KeepLevel == 5, "옛 keep 키가 Keep으로 이주한다");
            Check(EstateBuild.Level(EstateGrid.Cell.Mine) == 1, "이주 뒤에도 광산은 기본 1");

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
