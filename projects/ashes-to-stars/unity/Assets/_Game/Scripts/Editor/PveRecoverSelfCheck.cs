using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §18-8 BalanceConfig.PvE회복시간 소비처.
    /// QA_NO_PVE_RECOVER면 옛 24시간·줄 없음.
    /// </summary>
    public static class PveRecoverSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/PvE Recover Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(PveRecover.EnvShow);
            string no = Environment.GetEnvironmentVariable(PveRecover.EnvNo);
            string raceNo = Environment.GetEnvironmentVariable(LifeSystem.EnvNoRaceRecover);
            float oldForce = LifeSystem.ForcePveRecoverHours;
            RaceId oldRace = RacePrefs.Get();
            Environment.SetEnvironmentVariable(PveRecover.EnvShow, null);
            Environment.SetEnvironmentVariable(PveRecover.EnvNo, null);
            Environment.SetEnvironmentVariable(LifeSystem.EnvNoRaceRecover, null);
            LifeSystem.ForcePveRecoverHours = 0f;

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PveRecover.ResetForTest();

            var cfg = ScriptableObject.CreateInstance<BalanceConfig>();
            Check(cfg != null && Mathf.Approximately(cfg.PvE회복시간, 24f),
                $"BalanceConfig.PvE회복시간 기본 24 (실제 {cfg?.PvE회복시간})");
            Check(!PveRecover.Blocked, "기본은 켜짐");
            Check(Mathf.Approximately(PveRecover.Hours(), 24f),
                $"읽기 24h (실제 {PveRecover.Hours()})");
            Check(PveRecover.Seconds() == 24L * 3600L,
                $"읽기 86400초 (실제 {PveRecover.Seconds()})");
            Check(PveRecover.Seconds() == LifeSystem.DefaultPveRecoverSeconds,
                "기본은 옛 하드코드 24시간과 같다 — DefaultPveRecover 회귀");
            Check(PveRecover.Line() == "PvE 회복 24h(§18-8)",
                $"기본 줄 (실제 {PveRecover.Line()})");
            UnityEngine.Object.DestroyImmediate(cfg);

            Environment.SetEnvironmentVariable(LifeSystem.EnvNoRaceRecover, "1");
            Check(LifeSystem.PveRecoverSeconds() == PveRecover.Seconds(),
                "종족표 차단이면 LifeSystem이 PveRecover.Seconds를 읽는다");
            var eight = ScriptableObject.CreateInstance<BalanceConfig>();
            eight.PvE회복시간 = 8f;
            PveRecover.ForceConfig = eight;
            Check(Mathf.Approximately(PveRecover.Hours(), 8f), "ForceConfig가 PvE회복시간을 읽는다");
            Check(PveRecover.Seconds() == 8L * 3600L, $"에셋 8h 초 (실제 {PveRecover.Seconds()})");
            Check(PveRecover.Line().Contains("8") && PveRecover.Line().Contains("§18-8"),
                $"에셋 8 줄 (실제 {PveRecover.Line()})");
            Check(LifeSystem.PveRecoverSeconds() == 8L * 3600L,
                $"LifeSystem이 ForceConfig 8h를 읽는다 (실제 {LifeSystem.PveRecoverSeconds()})");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PveRecover.ForceConfig = eight;
            Environment.SetEnvironmentVariable(LifeSystem.EnvNoRaceRecover, "1");
            long t0 = 1_700_000_000L;
            LifeSystem.NowUnix = () => t0;
            var a = LifeSystem.GetCharacters()[0];
            LifeSystem.RegisterDeath(a);
            Check(a.DeathCount == 1 && !a.IsDeleted, "PvE 1회는 목숨을 깎는다");
            Check(LifeSystem.GetRecoveryTimeRemaining(a) == 8 * 3600,
                $"에셋 8h가 회복 시계에 걸린다 (실제 {LifeSystem.GetRecoveryTimeRemaining(a)})");
            PveRecover.ForceConfig = null;
            UnityEngine.Object.DestroyImmediate(eight);
            Environment.SetEnvironmentVariable(LifeSystem.EnvNoRaceRecover, null);
            Check(Mathf.Approximately(PveRecover.Hours(), 24f), "에셋을 치우면 다시 24");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Environment.SetEnvironmentVariable(PveRecover.EnvNo, "1");
            Environment.SetEnvironmentVariable(LifeSystem.EnvNoRaceRecover, "1");
            Check(PveRecover.Blocked, "QA_NO면 차단");
            var fake = ScriptableObject.CreateInstance<BalanceConfig>();
            fake.PvE회복시간 = 10f;
            PveRecover.ForceConfig = fake;
            Check(Mathf.Approximately(PveRecover.Hours(), 24f), "차단하면 ForceConfig 10도 옛 24");
            Check(PveRecover.Seconds() == 24L * 3600L, "차단하면 초도 옛 24시간");
            Check(PveRecover.Line() == "", "차단하면 회복 줄 없음(옛 화면)");
            Check(LifeSystem.PveRecoverSeconds() == LifeSystem.DefaultPveRecoverSeconds,
                "차단하면 LifeSystem도 옛 24시간");
            PveRecover.ForceConfig = null;
            UnityEngine.Object.DestroyImmediate(fake);
            Environment.SetEnvironmentVariable(PveRecover.EnvNo, null);
            Environment.SetEnvironmentVariable(LifeSystem.EnvNoRaceRecover, null);
            Check(!PveRecover.Blocked && PveRecover.Line() == "PvE 회복 24h(§18-8)",
                "차단을 풀면 다시 회복 줄");

            Environment.SetEnvironmentVariable(PveRecover.EnvShow, "1");
            PveRecover.ResetForTest();
            PveRecover.SeedQaIfRequested();
            Check(PveRecover.ShowQa, "시드 ShowQa");
            Check(PveRecover.Line().Contains("24"),
                $"시드 줄 (실제 {PveRecover.Line()})");
            Environment.SetEnvironmentVariable(PveRecover.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string recSrc = File.ReadAllText(Path.Combine(runtime, "PveRecover.cs"));
            Check(recSrc.Contains("PvE회복시간"),
                "PveRecover가 BalanceConfig.PvE회복시간을 읽는다");
            string lifeSrc = File.ReadAllText(Path.Combine(runtime, "LifeSystem.cs"));
            Check(lifeSrc.Contains("PveRecover.Seconds()"),
                "LifeSystem이 PveRecover.Seconds를 읽는다 — 지우면 소비처 0곳으로 되돌아간다");
            string charSrc = File.ReadAllText(Path.Combine(runtime, "CharacterScreen.cs"));
            Check(charSrc.Contains("PveRecover.Line"),
                "CharacterScreen이 Line을 속성 탭에 그린다");
            Check(charSrc.Contains("PveRecover.SeedQaIfRequested"),
                "CharacterScreen이 SeedQa를 부른다");

            _ = nameof(PveRecover.Hours);
            _ = nameof(PveRecover.Seconds);
            _ = nameof(PveRecover.Line);
            _ = nameof(PveRecover.SeedQaIfRequested);
            _ = nameof(BalanceConfig.PvE회복시간);

            Environment.SetEnvironmentVariable(PveRecover.EnvShow, show);
            Environment.SetEnvironmentVariable(PveRecover.EnvNo, no);
            Environment.SetEnvironmentVariable(LifeSystem.EnvNoRaceRecover, raceNo);
            LifeSystem.ForcePveRecoverHours = oldForce;
            RacePrefs.Set(oldRace);
            PveRecover.ResetForTest();
            GameState.ResetAll();
            LifeSystem.ResetAll();

            string dir = Path.Combine(Application.dataPath, "../..", "results");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "pve_recover_selfcheck.log");
            var body = new StringBuilder();
            body.AppendLine(_fail == 0 ? "PASS PveRecoverSelfCheck" : "FAIL PveRecoverSelfCheck");
            body.Append(_log);
            File.WriteAllText(path, body.ToString());
            if (_fail == 0) Debug.Log("[PveRecoverSelfCheck] PASS → " + path);
            else Debug.LogError("[PveRecoverSelfCheck] FAIL " + _fail + " → " + path);
            if (_fail > 0) throw new InvalidOperationException(
                $"[PveRecoverSelfCheck] FAIL {_fail}건");
        }
    }
}
