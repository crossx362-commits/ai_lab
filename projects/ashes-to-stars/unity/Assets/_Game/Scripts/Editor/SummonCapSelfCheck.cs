using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §10-9 BalanceConfig.소환수상한 소비처.
    /// QA_NO_SUMMON_CAP면 옛 무제한·상한 줄 없음.
    /// </summary>
    public static class SummonCapSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Summon Cap Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(SummonCap.EnvShow);
            string no = Environment.GetEnvironmentVariable(SummonCap.EnvNo);
            Environment.SetEnvironmentVariable(SummonCap.EnvShow, null);
            Environment.SetEnvironmentVariable(SummonCap.EnvNo, null);

            GameState.ResetAll();
            SummonCap.ResetForTest();

            var cfg = ScriptableObject.CreateInstance<BalanceConfig>();
            Check(cfg != null && cfg.소환수상한 == 50,
                $"BalanceConfig.소환수상한 기본 50 (실제 {cfg?.소환수상한})");
            Check(!SummonCap.Blocked, "기본은 켜짐");
            Check(SummonCap.Limit() == 50, $"읽기 50 (실제 {SummonCap.Limit()})");
            Check(SummonCap.Line() == "소환수 상한 50(§10-9)",
                $"기본 줄 (실제 {SummonCap.Line()})");
            Check(SummonCap.Clamp(5) == 5, "상한 아래 요청은 그대로");
            Check(SummonCap.Clamp(80) == 50, $"80은 50으로 잘린다 (실제 {SummonCap.Clamp(80)})");
            Check(SummonCap.Clamp(0) == 0, "0 요청은 0");
            UnityEngine.Object.DestroyImmediate(cfg);

            var three = ScriptableObject.CreateInstance<BalanceConfig>();
            three.소환수상한 = 3;
            SummonCap.ForceConfig = three;
            Check(SummonCap.Limit() == 3, "ForceConfig가 소환수상한을 읽는다");
            Check(SummonCap.Line().Contains("3") && SummonCap.Line().Contains("§10-9"),
                $"에셋 3 줄 (실제 {SummonCap.Line()})");
            Check(SummonCap.Clamp(5) == 3, $"요청 5가 상한 3으로 잘린다 (실제 {SummonCap.Clamp(5)})");
            SummonCap.ForceConfig = null;
            UnityEngine.Object.DestroyImmediate(three);
            Check(SummonCap.Limit() == 50, "에셋을 치우면 다시 50");

            GameState.ResetAll();
            Environment.SetEnvironmentVariable(SummonCap.EnvNo, "1");
            Check(SummonCap.Blocked, "QA_NO면 차단");
            var fake = ScriptableObject.CreateInstance<BalanceConfig>();
            fake.소환수상한 = 3;
            SummonCap.ForceConfig = fake;
            Check(SummonCap.Clamp(80) == 80, "차단하면 ForceConfig 3도 옛 무제한(요청 그대로)");
            Check(SummonCap.Line() == "", "차단하면 상한 줄 없음(옛 화면)");
            SummonCap.ForceConfig = null;
            UnityEngine.Object.DestroyImmediate(fake);
            Environment.SetEnvironmentVariable(SummonCap.EnvNo, null);
            Check(!SummonCap.Blocked && SummonCap.Line() == "소환수 상한 50(§10-9)",
                "차단을 풀면 다시 상한 줄");

            Environment.SetEnvironmentVariable(SummonCap.EnvShow, "1");
            SummonCap.ResetForTest();
            SummonCap.SeedQaIfRequested();
            Check(SummonCap.ShowQa, "시드 ShowQa");
            Check(SummonCap.Line().Contains("50"),
                $"시드 줄 (실제 {SummonCap.Line()})");
            Environment.SetEnvironmentVariable(SummonCap.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string capSrc = File.ReadAllText(Path.Combine(runtime, "SummonCap.cs"));
            Check(capSrc.Contains("소환수상한"),
                "SummonCap가 BalanceConfig.소환수상한을 읽는다");
            string bossSrc = File.ReadAllText(Path.Combine(runtime, "BossBattle.cs"));
            Check(bossSrc.Contains("SummonCap.Clamp"),
                "BossBattle가 SummonCap.Clamp를 읽는다 — 지우면 소비처 0곳으로 되돌아간다");
            string charSrc = File.ReadAllText(Path.Combine(runtime, "CharacterScreen.cs"));
            Check(charSrc.Contains("SummonCap.Line"),
                "CharacterScreen이 Line을 속성 탭에 그린다");
            Check(charSrc.Contains("stats + \" · \" + summonCap"),
                "상한 줄을 StatLine 우선존에 붙인다 — 맨 뒤면 화면에서 잘린다");
            Check(charSrc.Contains("SummonCap.SeedQaIfRequested"),
                "CharacterScreen이 SeedQa를 부른다");
            Check(charSrc.Contains("!roster[i].IsDeleted"),
                "시드가 삭제된 캐릭터를 건너뛴다");

            _ = nameof(SummonCap.Limit);
            _ = nameof(SummonCap.Clamp);
            _ = nameof(SummonCap.Line);
            _ = nameof(SummonCap.SeedQaIfRequested);
            _ = nameof(BalanceConfig.소환수상한);

            Environment.SetEnvironmentVariable(SummonCap.EnvShow, show);
            Environment.SetEnvironmentVariable(SummonCap.EnvNo, no);
            SummonCap.ResetForTest();
            GameState.ResetAll();

            string dir = Path.Combine(Application.dataPath, "../..", "results");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "summon_cap_selfcheck.log");
            var body = new StringBuilder();
            body.AppendLine(_fail == 0 ? "PASS SummonCapSelfCheck" : "FAIL SummonCapSelfCheck");
            body.Append(_log);
            File.WriteAllText(path, body.ToString());
            if (_fail == 0) Debug.Log("[SummonCapSelfCheck] PASS → " + path);
            else Debug.LogError("[SummonCapSelfCheck] FAIL " + _fail + " → " + path);
            if (_fail > 0) throw new InvalidOperationException(
                $"[SummonCapSelfCheck] FAIL {_fail}건");
        }
    }
}
