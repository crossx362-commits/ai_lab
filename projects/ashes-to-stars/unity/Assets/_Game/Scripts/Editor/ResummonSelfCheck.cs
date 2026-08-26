using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §18-14 소환수 재소환(0.5G/h T1=50실버 · 쿨다운 30초) 소비처.
    /// QA_NO_RESUMMON이면 옛 동작(줄 없음).
    /// </summary>
    public static class ResummonSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Resummon Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(Resummon.EnvShow);
            string no = Environment.GetEnvironmentVariable(Resummon.EnvNo);
            Environment.SetEnvironmentVariable(Resummon.EnvShow, null);
            Environment.SetEnvironmentVariable(Resummon.EnvNo, null);

            GameState.ResetAll();
            Resummon.ResetForTest();

            var cfg = ScriptableObject.CreateInstance<BalanceConfig>();
            Check(cfg != null && cfg.재소환비용실버 == 50f,
                $"BalanceConfig.재소환비용실버 기본 50 (실제 {cfg?.재소환비용실버})");
            Check(cfg != null && cfg.재소환쿨다운초 == 30f,
                $"BalanceConfig.재소환쿨다운초 기본 30 (실제 {cfg?.재소환쿨다운초})");
            Check(!Resummon.Blocked, "기본은 켜짐");
            Check(Resummon.CostSilver() == 50, $"비용 읽기 50실버 (실제 {Resummon.CostSilver()})");
            Check(Resummon.CooldownSeconds() == 30f, $"쿨다운 읽기 30초 (실제 {Resummon.CooldownSeconds()})");
            Check(Resummon.Line() == "소환수 재소환 50실버 · 쿨다운 30초(§18-14)",
                $"기본 줄 (실제 {Resummon.Line()})");
            UnityEngine.Object.DestroyImmediate(cfg);

            var custom = ScriptableObject.CreateInstance<BalanceConfig>();
            custom.재소환비용실버 = 80f;
            custom.재소환쿨다운초 = 45f;
            Resummon.ForceConfig = custom;
            Check(Resummon.CostSilver() == 80, "ForceConfig가 재소환비용실버를 읽는다");
            Check(Mathf.Approximately(Resummon.CooldownSeconds(), 45f),
                "ForceConfig가 재소환쿨다운초를 읽는다");
            Check(Resummon.Line().Contains("80") && Resummon.Line().Contains("45") && Resummon.Line().Contains("§18-14"),
                $"에셋 80·45 줄 (실제 {Resummon.Line()})");
            Resummon.ForceConfig = null;
            UnityEngine.Object.DestroyImmediate(custom);
            Check(Resummon.CostSilver() == 50 && Resummon.CooldownSeconds() == 30f, "에셋을 치우면 다시 50·30");

            // 0 이하 값은 원장 기본값으로 폴백 — 수치 튜닝 금지(§21-3) 방어.
            var zero = ScriptableObject.CreateInstance<BalanceConfig>();
            zero.재소환비용실버 = 0f;
            zero.재소환쿨다운초 = -5f;
            Resummon.ForceConfig = zero;
            Check(Resummon.CostSilver() == 50, "비용 0은 기본 50 폴백");
            Check(Resummon.CooldownSeconds() == 30f, "쿨다운 음수는 기본 30 폴백");
            Resummon.ForceConfig = null;
            UnityEngine.Object.DestroyImmediate(zero);

            GameState.ResetAll();
            Environment.SetEnvironmentVariable(Resummon.EnvNo, "1");
            Check(Resummon.Blocked, "QA_NO면 차단");
            var fake = ScriptableObject.CreateInstance<BalanceConfig>();
            fake.재소환비용실버 = 80f;
            fake.재소환쿨다운초 = 45f;
            Resummon.ForceConfig = fake;
            Check(Resummon.CostSilver() == 50, "차단하면 비용도 옛 기본값 경로");
            Check(Resummon.Line() == "", "차단하면 재소환 줄 없음(옛 화면)");
            Resummon.ForceConfig = null;
            UnityEngine.Object.DestroyImmediate(fake);
            Environment.SetEnvironmentVariable(Resummon.EnvNo, null);
            Check(!Resummon.Blocked && Resummon.Line().Contains("50"),
                "차단을 풀면 다시 재소환 줄");

            Environment.SetEnvironmentVariable(Resummon.EnvShow, "1");
            Resummon.ResetForTest();
            Resummon.SeedQaIfRequested();
            Check(Resummon.ShowQa, "시드 ShowQa");
            Check(Resummon.Line().Contains("50"), $"시드 줄 (실제 {Resummon.Line()})");
            Environment.SetEnvironmentVariable(Resummon.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string resummonSrc = File.ReadAllText(Path.Combine(runtime, "Resummon.cs"));
            Check(resummonSrc.Contains("재소환비용실버") && resummonSrc.Contains("재소환쿨다운초"),
                "Resummon이 BalanceConfig.재소환*을 읽는다");
            string balanceSrc = File.ReadAllText(Path.Combine(runtime, "BalanceConfig.cs"));
            Check(balanceSrc.Contains("public float 재소환비용실버 = 50f;"),
                "BalanceConfig에 §18-14 비용 필드가 authored돼 있다");
            string charSrc = File.ReadAllText(Path.Combine(runtime, "CharacterScreen.cs"));
            Check(charSrc.Contains("Resummon.Line"),
                "CharacterScreen이 Line을 속성 탭에 그린다");
            Check(charSrc.Contains("SeedResummonQaIfRequested();"),
                "CharacterScreen이 SeedResummon을 부른다");

            _ = nameof(Resummon.CostSilver);
            _ = nameof(Resummon.CooldownSeconds);
            _ = nameof(Resummon.Line);
            _ = nameof(Resummon.SeedQaIfRequested);
            _ = nameof(BalanceConfig.재소환비용실버);
            _ = nameof(BalanceConfig.재소환쿨다운초);

            Environment.SetEnvironmentVariable(Resummon.EnvShow, show);
            Environment.SetEnvironmentVariable(Resummon.EnvNo, no);
            Resummon.ResetForTest();
            GameState.ResetAll();

            string dir = Path.Combine(Application.dataPath, "../..", "results");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "resummon_selfcheck.log");
            var body = new StringBuilder();
            body.AppendLine(_fail == 0 ? "PASS ResummonSelfCheck" : "FAIL ResummonSelfCheck");
            body.Append(_log);
            File.WriteAllText(path, body.ToString());
            if (_fail == 0) Debug.Log("[ResummonSelfCheck] PASS → " + path);
            else Debug.LogError("[ResummonSelfCheck] FAIL " + _fail + " → " + path);
            if (_fail > 0) throw new InvalidOperationException(
                $"[ResummonSelfCheck] FAIL {_fail}건");
        }
    }
}
