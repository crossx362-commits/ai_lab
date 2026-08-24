using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §18-1 BalanceConfig.티어배율 소비처.
    /// QA_NO_TIER_MUL면 옛 ×1.6 표·배율 줄 없음.
    /// </summary>
    public static class TierMulSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Tier Mul Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(TierMul.EnvShow);
            string no = Environment.GetEnvironmentVariable(TierMul.EnvNo);
            string huntNo = Environment.GetEnvironmentVariable(Economy.EnvNoHuntGold);
            Environment.SetEnvironmentVariable(TierMul.EnvShow, null);
            Environment.SetEnvironmentVariable(TierMul.EnvNo, null);
            Environment.SetEnvironmentVariable(Economy.EnvNoHuntGold, null);

            GameState.ResetAll();
            TierMul.ResetForTest();
            GhAnchor.ResetForTest();
            SoftCap.ResetForTest();

            var cfg = ScriptableObject.CreateInstance<BalanceConfig>();
            Check(cfg != null && Mathf.Approximately(cfg.티어배율, 1.6f),
                $"BalanceConfig.티어배율 기본 1.6 (실제 {cfg?.티어배율})");
            Check(!TierMul.Blocked, "기본은 켜짐");
            Check(Mathf.Approximately(TierMul.Factor(), 1.6f), $"읽기 1.6 (실제 {TierMul.Factor()})");
            Check(TierMul.Table().Length == 10, $"표 길이 10 (실제 {TierMul.Table().Length})");
            Check(Mathf.Approximately(TierMul.Table()[0], 1f),
                $"T1 = 1 (실제 {TierMul.Table()[0]})");
            Check(Mathf.Approximately(TierMul.Table()[1], 1.6f),
                $"T2 = 1.6 (실제 {TierMul.Table()[1]})");
            Check(TierMul.Line() == "티어당 ×1.6(§18-1)",
                $"기본 줄 (실제 {TierMul.Line()})");
            Check(Economy.WaveHuntGold(0, 3600f) == 10_000,
                $"T1 3600초 = 10000 (실제 {Economy.WaveHuntGold(0, 3600f)})");
            Check(Economy.WaveHuntGold(1, 3600f) == 16_000,
                $"T2 3600초 = 16000 (실제 {Economy.WaveHuntGold(1, 3600f)})");
            Check(ReferenceEquals(Economy.TierRevenueMultiplier, TierMul.Table()),
                "Economy.TierRevenueMultiplier가 Table을 읽는다");
            UnityEngine.Object.DestroyImmediate(cfg);

            var two = ScriptableObject.CreateInstance<BalanceConfig>();
            two.티어배율 = 2f;
            TierMul.ForceConfig = two;
            TierMul.ResetForTest();
            TierMul.ForceConfig = two;
            Check(Mathf.Approximately(TierMul.Factor(), 2f), "ForceConfig가 티어배율을 읽는다");
            Check(Mathf.Approximately(TierMul.Table()[1], 2f),
                $"에셋 2면 T2=2 (실제 {TierMul.Table()[1]})");
            Check(TierMul.Line().Contains("×2") && TierMul.Line().Contains("§18-1"),
                $"에셋 2 줄 (실제 {TierMul.Line()})");
            Check(Economy.WaveHuntGold(1, 3600f) == 20_000,
                $"생성기가 Table을 읽는다 T2=20000 (실제 {Economy.WaveHuntGold(1, 3600f)})");
            float dungeonMul = Economy.ActionCostMultiplier["DungeonEntry"];
            long t2Cost = Economy.GetActionCostBase("DungeonEntry", 1);
            long expectT2 = (long)(dungeonMul * TierMul.Table()[1] * Economy.COPPER_PER_GOLD);
            Check(t2Cost == expectT2,
                $"T2 던전 입장이 Table을 읽는다 ({expectT2}, 실제 {t2Cost})");
            Check(t2Cost != (long)(dungeonMul * 1.6f * Economy.COPPER_PER_GOLD),
                "에셋 2면 T2 던전 입장이 옛 ×1.6 비용이 아니다");
            TierMul.ForceConfig = null;
            UnityEngine.Object.DestroyImmediate(two);
            TierMul.ResetForTest();
            Check(Mathf.Approximately(TierMul.Factor(), 1.6f), "에셋을 치우면 다시 1.6");
            Check(Economy.WaveHuntGold(1, 3600f) == 16_000, "에셋을 치우면 다시 16000");

            GameState.ResetAll();
            TierMul.ResetForTest();
            Environment.SetEnvironmentVariable(TierMul.EnvNo, "1");
            Check(TierMul.Blocked, "QA_NO면 차단");
            var fake = ScriptableObject.CreateInstance<BalanceConfig>();
            fake.티어배율 = 2f;
            TierMul.ForceConfig = fake;
            Check(Mathf.Approximately(TierMul.Factor(), 1.6f), "차단하면 ForceConfig 2도 옛 1.6");
            Check(Mathf.Approximately(TierMul.Table()[1], 1.6f), "차단하면 표도 옛 1.6");
            Check(TierMul.Line() == "", "차단하면 배율 줄 없음(옛 화면)");
            Check(Economy.WaveHuntGold(1, 3600f) == 16_000, "차단하면 골드도 옛 16000");
            TierMul.ForceConfig = null;
            UnityEngine.Object.DestroyImmediate(fake);
            Environment.SetEnvironmentVariable(TierMul.EnvNo, null);
            TierMul.ResetForTest();
            Check(!TierMul.Blocked && TierMul.Line() == "티어당 ×1.6(§18-1)",
                "차단을 풀면 다시 배율 줄");

            Environment.SetEnvironmentVariable(TierMul.EnvShow, "1");
            TierMul.ResetForTest();
            TierMul.SeedQaIfRequested();
            Check(TierMul.ShowQa, "시드 ShowQa");
            Check(TierMul.Line().Contains("×1.6"),
                $"시드 줄 (실제 {TierMul.Line()})");
            Environment.SetEnvironmentVariable(TierMul.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string capSrc = File.ReadAllText(Path.Combine(runtime, "TierMul.cs"));
            Check(capSrc.Contains("티어배율"),
                "TierMul가 BalanceConfig.티어배율을 읽는다");
            string ecoSrc = File.ReadAllText(Path.Combine(runtime, "Economy.cs"));
            Check(ecoSrc.Contains("TierMul.Table"),
                "Economy.TierRevenueMultiplier가 TierMul.Table을 읽는다 — 지우면 소비처 0곳으로 되돌아간다");
            string charSrc = File.ReadAllText(Path.Combine(runtime, "CharacterScreen.cs"));
            Check(charSrc.Contains("TierMul.Line"),
                "CharacterScreen이 Line을 속성 탭에 그린다");
            Check(charSrc.Contains("budget + \" · \" + tm"),
                "배율 줄을 잡몹 행에 붙인다");
            Check(charSrc.Contains("TierMul.ShowQa ? TierMul.Line()"),
                "부제에 배율 줄을 올린다 — 속성 패널 맨 뒤는 샷에 안 나온다");
            Check(charSrc.Contains("TierMul.SeedQaIfRequested"),
                "CharacterScreen이 SeedQa를 부른다");
            Check(charSrc.Contains("!roster[i].IsDeleted"),
                "시드가 삭제된 캐릭터를 건너뛴다");

            _ = nameof(TierMul.Factor);
            _ = nameof(TierMul.Table);
            _ = nameof(TierMul.Line);
            _ = nameof(TierMul.SeedQaIfRequested);
            _ = nameof(BalanceConfig.티어배율);
            _ = nameof(Economy.WaveHuntGold);
            _ = nameof(Economy.TierRevenueMultiplier);

            Environment.SetEnvironmentVariable(TierMul.EnvShow, show);
            Environment.SetEnvironmentVariable(TierMul.EnvNo, no);
            Environment.SetEnvironmentVariable(Economy.EnvNoHuntGold, huntNo);
            TierMul.ResetForTest();
            GhAnchor.ResetForTest();
            GameState.ResetAll();

            string dir = Path.Combine(Application.dataPath, "../..", "results");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "tier_mul_selfcheck.log");
            var body = new StringBuilder();
            body.AppendLine(_fail == 0 ? "PASS TierMulSelfCheck" : "FAIL TierMulSelfCheck");
            body.Append(_log);
            File.WriteAllText(path, body.ToString());
            if (_fail == 0) Debug.Log("[TierMulSelfCheck] PASS → " + path);
            else Debug.LogError("[TierMulSelfCheck] FAIL " + _fail + " → " + path);
            if (_fail > 0) throw new InvalidOperationException(
                $"[TierMulSelfCheck] FAIL {_fail}건");
        }
    }
}
