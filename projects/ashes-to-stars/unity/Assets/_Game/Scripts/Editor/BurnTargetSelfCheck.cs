using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §18-2 BalanceConfig.소각목표 소비처.
    /// QA_NO_BURN_TARGET면 옛 45~55·소각 줄 없음.
    /// </summary>
    public static class BurnTargetSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Burn Target Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(BurnTarget.EnvShow);
            string no = Environment.GetEnvironmentVariable(BurnTarget.EnvNo);
            Environment.SetEnvironmentVariable(BurnTarget.EnvShow, null);
            Environment.SetEnvironmentVariable(BurnTarget.EnvNo, null);

            GameState.ResetAll();
            BurnTarget.ResetForTest();

            var cfg = ScriptableObject.CreateInstance<BalanceConfig>();
            Check(cfg != null
                  && Mathf.Approximately(cfg.소각목표.x, 45f)
                  && Mathf.Approximately(cfg.소각목표.y, 55f),
                $"BalanceConfig.소각목표 기본 45~55 (실제 {cfg?.소각목표})");
            Check(!BurnTarget.Blocked, "기본은 켜짐");
            Check(Mathf.Approximately(BurnTarget.Low(), 45f)
                  && Mathf.Approximately(BurnTarget.High(), 55f),
                $"읽기 45~55 (실제 {BurnTarget.Low()}~{BurnTarget.High()})");
            Check(BurnTarget.Line() == "소각 목표 45~55%(§18-2)",
                $"기본 줄 (실제 {BurnTarget.Line()})");
            UnityEngine.Object.DestroyImmediate(cfg);

            var wide = ScriptableObject.CreateInstance<BalanceConfig>();
            wide.소각목표 = new Vector2(40f, 60f);
            BurnTarget.ForceConfig = wide;
            Check(Mathf.Approximately(BurnTarget.Low(), 40f)
                  && Mathf.Approximately(BurnTarget.High(), 60f),
                "ForceConfig가 소각목표를 읽는다");
            Check(BurnTarget.Line().Contains("40") && BurnTarget.Line().Contains("60")
                  && BurnTarget.Line().Contains("§18-2"),
                $"에셋 40~60 줄 (실제 {BurnTarget.Line()})");
            BurnTarget.ForceConfig = null;
            UnityEngine.Object.DestroyImmediate(wide);
            BurnTarget.ResetForTest();
            Check(Mathf.Approximately(BurnTarget.Low(), 45f)
                  && Mathf.Approximately(BurnTarget.High(), 55f),
                "에셋을 치우면 다시 45~55");

            var swap = ScriptableObject.CreateInstance<BalanceConfig>();
            swap.소각목표 = new Vector2(70f, 50f);
            BurnTarget.ForceConfig = swap;
            Check(Mathf.Approximately(BurnTarget.Low(), 50f)
                  && Mathf.Approximately(BurnTarget.High(), 70f),
                $"뒤집힌 벡터는 정렬 (실제 {BurnTarget.Low()}~{BurnTarget.High()})");
            BurnTarget.ForceConfig = null;
            UnityEngine.Object.DestroyImmediate(swap);
            BurnTarget.ResetForTest();

            GameState.ResetAll();
            BurnTarget.ResetForTest();
            Environment.SetEnvironmentVariable(BurnTarget.EnvNo, "1");
            Check(BurnTarget.Blocked, "QA_NO면 차단");
            var fake = ScriptableObject.CreateInstance<BalanceConfig>();
            fake.소각목표 = new Vector2(40f, 60f);
            BurnTarget.ForceConfig = fake;
            Check(Mathf.Approximately(BurnTarget.Low(), 45f)
                  && Mathf.Approximately(BurnTarget.High(), 55f),
                "차단하면 ForceConfig 40~60도 옛 45~55");
            Check(BurnTarget.Line() == "", "차단하면 소각 줄 없음(옛 화면)");
            BurnTarget.ForceConfig = null;
            UnityEngine.Object.DestroyImmediate(fake);
            Environment.SetEnvironmentVariable(BurnTarget.EnvNo, null);
            BurnTarget.ResetForTest();
            Check(!BurnTarget.Blocked && BurnTarget.Line() == "소각 목표 45~55%(§18-2)",
                "차단을 풀면 다시 소각 줄");

            Environment.SetEnvironmentVariable(BurnTarget.EnvShow, "1");
            BurnTarget.ResetForTest();
            BurnTarget.SeedQaIfRequested();
            Check(BurnTarget.ShowQa, "시드 ShowQa");
            Check(BurnTarget.Line().Contains("45") && BurnTarget.Line().Contains("55"),
                $"시드 줄 (실제 {BurnTarget.Line()})");
            Environment.SetEnvironmentVariable(BurnTarget.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string capSrc = File.ReadAllText(Path.Combine(runtime, "BurnTarget.cs"));
            Check(capSrc.Contains("소각목표"),
                "BurnTarget가 BalanceConfig.소각목표를 읽는다");
            string charSrc = File.ReadAllText(Path.Combine(runtime, "CharacterScreen.cs"));
            Check(charSrc.Contains("BurnTarget.Line"),
                "CharacterScreen이 Line을 속성 탭에 그린다");
            Check(charSrc.Contains("budget + \" · \" + burn"),
                "소각 줄을 잡몹 행에 붙인다");
            Check(charSrc.Contains("BurnTarget.ShowQa ? BurnTarget.Line()"),
                "부제에 소각 줄을 올린다 — 속성 패널 맨 뒤는 샷에 안 나온다");
            Check(charSrc.Contains("BurnTarget.SeedQaIfRequested"),
                "CharacterScreen이 SeedQa를 부른다");
            Check(charSrc.Contains("!roster[i].IsDeleted"),
                "시드가 삭제된 캐릭터를 건너뛴다");

            _ = nameof(BurnTarget.Range);
            _ = nameof(BurnTarget.Low);
            _ = nameof(BurnTarget.High);
            _ = nameof(BurnTarget.Line);
            _ = nameof(BurnTarget.SeedQaIfRequested);
            _ = nameof(BalanceConfig.소각목표);

            Environment.SetEnvironmentVariable(BurnTarget.EnvShow, show);
            Environment.SetEnvironmentVariable(BurnTarget.EnvNo, no);
            BurnTarget.ResetForTest();
            GameState.ResetAll();

            string dir = Path.Combine(Application.dataPath, "../..", "results");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "burn_target_selfcheck.log");
            var body = new StringBuilder();
            body.AppendLine(_fail == 0 ? "PASS BurnTargetSelfCheck" : "FAIL BurnTargetSelfCheck");
            body.Append(_log);
            File.WriteAllText(path, body.ToString());
            if (_fail == 0) Debug.Log("[BurnTargetSelfCheck] PASS → " + path);
            else Debug.LogError("[BurnTargetSelfCheck] FAIL " + _fail + " → " + path);
            if (_fail > 0) throw new InvalidOperationException(
                $"[BurnTargetSelfCheck] FAIL {_fail}건");
        }
    }
}
