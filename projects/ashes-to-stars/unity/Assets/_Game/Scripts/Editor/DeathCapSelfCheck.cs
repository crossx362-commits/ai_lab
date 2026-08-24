using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §4 BalanceConfig.사망상한 소비처.
    /// QA_NO_DEATH_CAP면 옛 3·상한 줄 없음.
    /// </summary>
    public static class DeathCapSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Death Cap Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(DeathCap.EnvShow);
            string no = Environment.GetEnvironmentVariable(DeathCap.EnvNo);
            Environment.SetEnvironmentVariable(DeathCap.EnvShow, null);
            Environment.SetEnvironmentVariable(DeathCap.EnvNo, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            DeathCap.ResetForTest();

            var cfg = ScriptableObject.CreateInstance<BalanceConfig>();
            Check(cfg != null && cfg.사망상한 == 3,
                $"BalanceConfig.사망상한 기본 3 (실제 {cfg?.사망상한})");
            Check(!DeathCap.Blocked, "기본은 켜짐");
            Check(DeathCap.Limit() == 3, $"읽기 3 (실제 {DeathCap.Limit()})");
            Check(DeathCap.Line() == "사망 상한 3(§4)",
                $"기본 줄 (실제 {DeathCap.Line()})");
            UnityEngine.Object.DestroyImmediate(cfg);

            var roster = LifeSystem.GetCharacters();
            Check(roster.Count > 0, "로스터가 있다");
            var a = roster[0];
            Check(!a.IsSpecialJob && a.MaxLives == 3,
                $"일반 MaxLives 3 (실제 {a.MaxLives})");

            var two = ScriptableObject.CreateInstance<BalanceConfig>();
            two.사망상한 = 2;
            DeathCap.ForceConfig = two;
            Check(DeathCap.Limit() == 2, "ForceConfig가 사망상한을 읽는다");
            Check(DeathCap.Line().Contains("2") && DeathCap.Line().Contains("§4"),
                $"에셋 2 줄 (실제 {DeathCap.Line()})");
            Check(a.MaxLives == 2, $"MaxLives가 Limit를 읽는다 (실제 {a.MaxLives})");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            DeathCap.ForceConfig = two;
            var b = LifeSystem.GetCharacters()[0];
            LifeSystem.RegisterDeath(b);
            Check(b.DeathCount == 1 && !b.IsDeleted,
                $"상한 2에서 1회는 삭제 아님 (카운트={b.DeathCount} 삭제={b.IsDeleted})");
            LifeSystem.RegisterDeath(b);
            Check(b.IsDeleted && b.DeathCount == 2,
                $"상한 2에서 2회 삭제 (삭제={b.IsDeleted}, 카운트={b.DeathCount})");
            DeathCap.ForceConfig = null;
            UnityEngine.Object.DestroyImmediate(two);
            Check(DeathCap.Limit() == 3, "에셋을 치우면 다시 3");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Environment.SetEnvironmentVariable(DeathCap.EnvNo, "1");
            Check(DeathCap.Blocked, "QA_NO면 차단");
            var fake = ScriptableObject.CreateInstance<BalanceConfig>();
            fake.사망상한 = 8;
            DeathCap.ForceConfig = fake;
            Check(DeathCap.Limit() == 3, "차단하면 ForceConfig 8도 옛 3");
            Check(DeathCap.Line() == "", "차단하면 상한 줄 없음(옛 화면)");
            var c = LifeSystem.GetCharacters()[0];
            Check(c.MaxLives == 3, "차단하면 MaxLives도 3");
            DeathCap.ForceConfig = null;
            UnityEngine.Object.DestroyImmediate(fake);
            Environment.SetEnvironmentVariable(DeathCap.EnvNo, null);
            Check(!DeathCap.Blocked && DeathCap.Line() == "사망 상한 3(§4)",
                "차단을 풀면 다시 상한 줄");

            Environment.SetEnvironmentVariable(DeathCap.EnvShow, "1");
            DeathCap.ResetForTest();
            DeathCap.SeedQaIfRequested();
            Check(DeathCap.ShowQa, "시드 ShowQa");
            Check(DeathCap.Line().Contains("3"),
                $"시드 줄 (실제 {DeathCap.Line()})");
            Environment.SetEnvironmentVariable(DeathCap.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string capSrc = File.ReadAllText(Path.Combine(runtime, "DeathCap.cs"));
            Check(capSrc.Contains("사망상한"),
                "DeathCap가 BalanceConfig.사망상한을 읽는다");
            string lifeSrc = File.ReadAllText(Path.Combine(runtime, "LifeSystem.cs"));
            Check(lifeSrc.Contains("DeathCap.Limit()"),
                "LifeSystem이 DeathCap.Limit를 읽는다 — 지우면 소비처 0곳으로 되돌아간다");
            string charSrc = File.ReadAllText(Path.Combine(runtime, "CharacterScreen.cs"));
            Check(charSrc.Contains("DeathCap.Line"),
                "CharacterScreen이 Line을 속성 탭에 그린다");
            Check(charSrc.Contains("DeathCap.SeedQaIfRequested"),
                "CharacterScreen이 SeedQa를 부른다");

            _ = nameof(DeathCap.Limit);
            _ = nameof(DeathCap.Line);
            _ = nameof(DeathCap.SeedQaIfRequested);
            _ = nameof(BalanceConfig.사망상한);

            Environment.SetEnvironmentVariable(DeathCap.EnvShow, show);
            Environment.SetEnvironmentVariable(DeathCap.EnvNo, no);
            DeathCap.ResetForTest();
            GameState.ResetAll();
            LifeSystem.ResetAll();

            string dir = Path.Combine(Application.dataPath, "../..", "results");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "death_cap_selfcheck.log");
            var body = new StringBuilder();
            body.AppendLine(_fail == 0 ? "PASS DeathCapSelfCheck" : "FAIL DeathCapSelfCheck");
            body.Append(_log);
            File.WriteAllText(path, body.ToString());
            if (_fail == 0) Debug.Log("[DeathCapSelfCheck] PASS → " + path);
            else Debug.LogError("[DeathCapSelfCheck] FAIL " + _fail + " → " + path);
            if (_fail > 0) throw new InvalidOperationException(
                $"[DeathCapSelfCheck] FAIL {_fail}건");
        }
    }
}
