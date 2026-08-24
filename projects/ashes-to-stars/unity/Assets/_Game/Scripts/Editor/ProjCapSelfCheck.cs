using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §10-9 BalanceConfig.투사체상한 소비처.
    /// QA_NO_PROJ_CAP면 옛 200·상한 줄 없음.
    /// </summary>
    public static class ProjCapSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Proj Cap Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(ProjCap.EnvShow);
            string no = Environment.GetEnvironmentVariable(ProjCap.EnvNo);
            Environment.SetEnvironmentVariable(ProjCap.EnvShow, null);
            Environment.SetEnvironmentVariable(ProjCap.EnvNo, null);

            GameState.ResetAll();
            ProjCap.ResetForTest();

            var cfg = ScriptableObject.CreateInstance<BalanceConfig>();
            Check(cfg != null && cfg.투사체상한 == 200,
                $"BalanceConfig.투사체상한 기본 200 (실제 {cfg?.투사체상한})");
            Check(!ProjCap.Blocked, "기본은 켜짐");
            Check(ProjCap.Limit() == 200, $"읽기 200 (실제 {ProjCap.Limit()})");
            Check(ProjCap.Line() == "투사체 상한 200(§10-9)",
                $"기본 줄 (실제 {ProjCap.Line()})");
            Check(ProjCap.Clamp(50) == 50, "상한 아래 요청은 그대로");
            Check(ProjCap.Clamp(300) == 200, $"300은 200으로 잘린다 (실제 {ProjCap.Clamp(300)})");
            Check(ProjCap.Clamp(0) == 0, "0 요청은 0");
            Check(StressTest.ProjectileHardCap == 200,
                $"W1 풀 상한 200 (실제 {StressTest.ProjectileHardCap})");
            UnityEngine.Object.DestroyImmediate(cfg);

            var eighty = ScriptableObject.CreateInstance<BalanceConfig>();
            eighty.투사체상한 = 80;
            ProjCap.ForceConfig = eighty;
            Check(ProjCap.Limit() == 80, "ForceConfig가 투사체상한을 읽는다");
            Check(ProjCap.Line().Contains("80") && ProjCap.Line().Contains("§10-9"),
                $"에셋 80 줄 (실제 {ProjCap.Line()})");
            Check(ProjCap.Clamp(120) == 80, $"요청 120이 상한 80으로 잘린다 (실제 {ProjCap.Clamp(120)})");
            Check(StressTest.ProjectileHardCap == 80,
                $"생성기가 Limit를 읽는다 (실제 {StressTest.ProjectileHardCap})");
            ProjCap.ForceConfig = null;
            UnityEngine.Object.DestroyImmediate(eighty);
            Check(ProjCap.Limit() == 200, "에셋을 치우면 다시 200");

            GameState.ResetAll();
            Environment.SetEnvironmentVariable(ProjCap.EnvNo, "1");
            Check(ProjCap.Blocked, "QA_NO면 차단");
            var fake = ScriptableObject.CreateInstance<BalanceConfig>();
            fake.투사체상한 = 80;
            ProjCap.ForceConfig = fake;
            Check(ProjCap.Limit() == 200, "차단하면 ForceConfig 80도 옛 200");
            Check(ProjCap.Line() == "", "차단하면 상한 줄 없음(옛 화면)");
            Check(StressTest.ProjectileHardCap == 200, "차단하면 W1 풀도 200");
            Check(ProjCap.Clamp(300) == 200, "차단하면 클램프도 옛 200");
            ProjCap.ForceConfig = null;
            UnityEngine.Object.DestroyImmediate(fake);
            Environment.SetEnvironmentVariable(ProjCap.EnvNo, null);
            Check(!ProjCap.Blocked && ProjCap.Line() == "투사체 상한 200(§10-9)",
                "차단을 풀면 다시 상한 줄");

            Environment.SetEnvironmentVariable(ProjCap.EnvShow, "1");
            ProjCap.ResetForTest();
            ProjCap.SeedQaIfRequested();
            Check(ProjCap.ShowQa, "시드 ShowQa");
            Check(ProjCap.Line().Contains("200"),
                $"시드 줄 (실제 {ProjCap.Line()})");
            Environment.SetEnvironmentVariable(ProjCap.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string capSrc = File.ReadAllText(Path.Combine(runtime, "ProjCap.cs"));
            Check(capSrc.Contains("투사체상한"),
                "ProjCap가 BalanceConfig.투사체상한을 읽는다");
            string w1Src = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/StressTest.cs"));
            Check(w1Src.Contains("ProjCap.Limit"),
                "StressTest가 ProjCap.Limit를 읽는다 — 지우면 소비처 0곳으로 되돌아간다");
            string charSrc = File.ReadAllText(Path.Combine(runtime, "CharacterScreen.cs"));
            Check(charSrc.Contains("ProjCap.Line"),
                "CharacterScreen이 Line을 속성 탭에 그린다");
            Check(charSrc.Contains("budget + \" · \" + projCap"),
                "상한 줄을 잡몹 행에 붙인다 — 맨 뒤면 화면에서 잘린다");
            Check(charSrc.Contains("ProjCap.SeedQaIfRequested"),
                "CharacterScreen이 SeedQa를 부른다");
            Check(charSrc.Contains("!roster[i].IsDeleted"),
                "시드가 삭제된 캐릭터를 건너뛴다");

            _ = nameof(ProjCap.Limit);
            _ = nameof(ProjCap.Clamp);
            _ = nameof(ProjCap.Line);
            _ = nameof(ProjCap.SeedQaIfRequested);
            _ = nameof(BalanceConfig.투사체상한);
            _ = nameof(StressTest.ProjectileHardCap);

            Environment.SetEnvironmentVariable(ProjCap.EnvShow, show);
            Environment.SetEnvironmentVariable(ProjCap.EnvNo, no);
            ProjCap.ResetForTest();
            GameState.ResetAll();

            string dir = Path.Combine(Application.dataPath, "../..", "results");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "proj_cap_selfcheck.log");
            var body = new StringBuilder();
            body.AppendLine(_fail == 0 ? "PASS ProjCapSelfCheck" : "FAIL ProjCapSelfCheck");
            body.Append(_log);
            File.WriteAllText(path, body.ToString());
            if (_fail == 0) Debug.Log("[ProjCapSelfCheck] PASS → " + path);
            else Debug.LogError("[ProjCapSelfCheck] FAIL " + _fail + " → " + path);
            if (_fail > 0) throw new InvalidOperationException(
                $"[ProjCapSelfCheck] FAIL {_fail}건");
        }
    }
}
