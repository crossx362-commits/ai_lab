using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §4 BalanceConfig.PvP회복시간 소비처.
    /// QA_NO_PVP_RECOVER면 옛 12시간·줄 없음.
    /// </summary>
    public static class PvpRecoverSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/PvP Recover Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(PvpRecover.EnvShow);
            string no = Environment.GetEnvironmentVariable(PvpRecover.EnvNo);
            Environment.SetEnvironmentVariable(PvpRecover.EnvShow, null);
            Environment.SetEnvironmentVariable(PvpRecover.EnvNo, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PvpRecover.ResetForTest();

            var cfg = ScriptableObject.CreateInstance<BalanceConfig>();
            Check(cfg != null && Mathf.Approximately(cfg.PvP회복시간, 12f),
                $"BalanceConfig.PvP회복시간 기본 12 (실제 {cfg?.PvP회복시간})");
            Check(!PvpRecover.Blocked, "기본은 켜짐");
            Check(Mathf.Approximately(PvpRecover.Hours(), 12f),
                $"읽기 12h (실제 {PvpRecover.Hours()})");
            Check(PvpRecover.Seconds() == 12L * 3600L,
                $"읽기 43200초 (실제 {PvpRecover.Seconds()})");
            Check(PvpRecover.Seconds() == InvasionState.DefenseRecoverSeconds,
                "기본은 옛 보호막 12시간과 같다 — DefenseRecover 회귀");
            Check(PvpRecover.Line() == "PvP 회복 12h(§4)",
                $"기본 줄 (실제 {PvpRecover.Line()})");
            Check(LifeSystem.PvpRecoverSeconds() == PvpRecover.Seconds(),
                "LifeSystem이 PvpRecover.Seconds를 읽는다");
            UnityEngine.Object.DestroyImmediate(cfg);

            var six = ScriptableObject.CreateInstance<BalanceConfig>();
            six.PvP회복시간 = 6f;
            PvpRecover.ForceConfig = six;
            Check(Mathf.Approximately(PvpRecover.Hours(), 6f), "ForceConfig가 PvP회복시간을 읽는다");
            Check(PvpRecover.Seconds() == 6L * 3600L, $"에셋 6h 초 (실제 {PvpRecover.Seconds()})");
            Check(PvpRecover.Line().Contains("6") && PvpRecover.Line().Contains("§4"),
                $"에셋 6 줄 (실제 {PvpRecover.Line()})");
            Check(LifeSystem.PvpRecoverSeconds() == 6L * 3600L,
                $"LifeSystem이 ForceConfig 6h를 읽는다 (실제 {LifeSystem.PvpRecoverSeconds()})");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PvpRecover.ForceConfig = six;
            long t0 = 1_700_000_000L;
            LifeSystem.NowUnix = () => t0;
            var a = LifeSystem.GetCharacters()[0];
            LifeSystem.RegisterDeath(a, isPvp: true);
            Check(a.DeathCount == 0 && !a.IsDeleted, "PvP는 목숨을 안 깎는다");
            Check(LifeSystem.GetRecoveryTimeRemaining(a) == 6 * 3600,
                $"에셋 6h가 회복 시계에 걸린다 (실제 {LifeSystem.GetRecoveryTimeRemaining(a)})");
            PvpRecover.ForceConfig = null;
            UnityEngine.Object.DestroyImmediate(six);
            Check(Mathf.Approximately(PvpRecover.Hours(), 12f), "에셋을 치우면 다시 12");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Environment.SetEnvironmentVariable(PvpRecover.EnvNo, "1");
            Check(PvpRecover.Blocked, "QA_NO면 차단");
            var fake = ScriptableObject.CreateInstance<BalanceConfig>();
            fake.PvP회복시간 = 8f;
            PvpRecover.ForceConfig = fake;
            Check(Mathf.Approximately(PvpRecover.Hours(), 12f), "차단하면 ForceConfig 8도 옛 12");
            Check(PvpRecover.Seconds() == 12L * 3600L, "차단하면 초도 옛 12시간");
            Check(PvpRecover.Line() == "", "차단하면 회복 줄 없음(옛 화면)");
            Check(LifeSystem.PvpRecoverSeconds() == InvasionState.DefenseRecoverSeconds,
                "차단하면 LifeSystem도 옛 12시간");
            PvpRecover.ForceConfig = null;
            UnityEngine.Object.DestroyImmediate(fake);
            Environment.SetEnvironmentVariable(PvpRecover.EnvNo, null);
            Check(!PvpRecover.Blocked && PvpRecover.Line() == "PvP 회복 12h(§4)",
                "차단을 풀면 다시 회복 줄");

            Environment.SetEnvironmentVariable(PvpRecover.EnvShow, "1");
            PvpRecover.ResetForTest();
            PvpRecover.SeedQaIfRequested();
            Check(PvpRecover.ShowQa, "시드 ShowQa");
            Check(PvpRecover.Line().Contains("12"),
                $"시드 줄 (실제 {PvpRecover.Line()})");
            Environment.SetEnvironmentVariable(PvpRecover.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string recSrc = File.ReadAllText(Path.Combine(runtime, "PvpRecover.cs"));
            Check(recSrc.Contains("PvP회복시간"),
                "PvpRecover가 BalanceConfig.PvP회복시간을 읽는다");
            string lifeSrc = File.ReadAllText(Path.Combine(runtime, "LifeSystem.cs"));
            Check(lifeSrc.Contains("PvpRecover.Seconds()"),
                "LifeSystem이 PvpRecover.Seconds를 읽는다 — 지우면 소비처 0곳으로 되돌아간다");
            string charSrc = File.ReadAllText(Path.Combine(runtime, "CharacterScreen.cs"));
            Check(charSrc.Contains("PvpRecover.Line"),
                "CharacterScreen이 Line을 속성 탭에 그린다");
            Check(charSrc.Contains("PvpRecover.SeedQaIfRequested"),
                "CharacterScreen이 SeedQa를 부른다");

            _ = nameof(PvpRecover.Hours);
            _ = nameof(PvpRecover.Seconds);
            _ = nameof(PvpRecover.Line);
            _ = nameof(PvpRecover.SeedQaIfRequested);
            _ = nameof(BalanceConfig.PvP회복시간);

            Environment.SetEnvironmentVariable(PvpRecover.EnvShow, show);
            Environment.SetEnvironmentVariable(PvpRecover.EnvNo, no);
            PvpRecover.ResetForTest();
            GameState.ResetAll();
            LifeSystem.ResetAll();

            string dir = Path.Combine(Application.dataPath, "../..", "results");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "pvp_recover_selfcheck.log");
            var body = new StringBuilder();
            body.AppendLine(_fail == 0 ? "PASS PvpRecoverSelfCheck" : "FAIL PvpRecoverSelfCheck");
            body.Append(_log);
            File.WriteAllText(path, body.ToString());
            if (_fail == 0) Debug.Log("[PvpRecoverSelfCheck] PASS → " + path);
            else Debug.LogError("[PvpRecoverSelfCheck] FAIL " + _fail + " → " + path);
            if (_fail > 0) throw new InvalidOperationException(
                $"[PvpRecoverSelfCheck] FAIL {_fail}건");
        }
    }
}
