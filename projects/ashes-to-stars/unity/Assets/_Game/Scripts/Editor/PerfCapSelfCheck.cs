using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §10-9 BalanceConfig.잡몹상한 소비처.
    /// QA_NO_PERF_CAP면 옛 500·상한 줄 없음.
    /// </summary>
    public static class PerfCapSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Perf Cap Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(PerfCap.EnvShow);
            string no = Environment.GetEnvironmentVariable(PerfCap.EnvNo);
            Environment.SetEnvironmentVariable(PerfCap.EnvShow, null);
            Environment.SetEnvironmentVariable(PerfCap.EnvNo, null);

            GameState.ResetAll();
            PerfCap.ResetForTest();

            var cfg = ScriptableObject.CreateInstance<BalanceConfig>();
            Check(cfg != null && cfg.잡몹상한 == 500,
                $"BalanceConfig.잡몹상한 기본 500 (실제 {cfg?.잡몹상한})");
            Check(!PerfCap.Blocked, "기본은 켜짐");
            Check(PerfCap.MobLimit() == 500, $"읽기 500 (실제 {PerfCap.MobLimit()})");
            Check(PerfCap.Line() == "잡몹 상한 500(§10-9)",
                $"기본 줄 (실제 {PerfCap.Line()})");
            Check(DungeonGenerator.MobHardCap == 500,
                $"생성기 상한 500 (실제 {DungeonGenerator.MobHardCap})");
            UnityEngine.Object.DestroyImmediate(cfg);

            var eighty = ScriptableObject.CreateInstance<BalanceConfig>();
            eighty.잡몹상한 = 80;
            PerfCap.ForceConfig = eighty;
            Check(PerfCap.MobLimit() == 80, "ForceConfig가 잡몹상한을 읽는다");
            Check(PerfCap.Line().Contains("80") && PerfCap.Line().Contains("§10-9"),
                $"에셋 80 줄 (실제 {PerfCap.Line()})");
            Check(DungeonGenerator.MobHardCap == 80,
                $"생성기가 Limit를 읽는다 (실제 {DungeonGenerator.MobHardCap})");

            var plan = DungeonGenerator.Generate(1u, 9, DungeonKind.레이드급);
            int maxTarget = 0;
            if (plan != null && plan.Nodes != null)
            {
                for (int i = 0; i < plan.Nodes.Length; i++)
                {
                    var w = plan.Nodes[i].Wave;
                    if (w != null && w.TargetCount > maxTarget) maxTarget = w.TargetCount;
                }
            }
            Check(maxTarget > 0 && maxTarget <= 80,
                $"T10 레이드급이 상한 80으로 잘린다 (최대 {maxTarget})");
            PerfCap.ForceConfig = null;
            UnityEngine.Object.DestroyImmediate(eighty);
            Check(PerfCap.MobLimit() == 500, "에셋을 치우면 다시 500");

            GameState.ResetAll();
            Environment.SetEnvironmentVariable(PerfCap.EnvNo, "1");
            Check(PerfCap.Blocked, "QA_NO면 차단");
            var fake = ScriptableObject.CreateInstance<BalanceConfig>();
            fake.잡몹상한 = 80;
            PerfCap.ForceConfig = fake;
            Check(PerfCap.MobLimit() == 500, "차단하면 ForceConfig 80도 옛 500");
            Check(PerfCap.Line() == "", "차단하면 상한 줄 없음(옛 화면)");
            Check(DungeonGenerator.MobHardCap == 500, "차단하면 생성기도 500");
            PerfCap.ForceConfig = null;
            UnityEngine.Object.DestroyImmediate(fake);
            Environment.SetEnvironmentVariable(PerfCap.EnvNo, null);
            Check(!PerfCap.Blocked && PerfCap.Line() == "잡몹 상한 500(§10-9)",
                "차단을 풀면 다시 상한 줄");

            Environment.SetEnvironmentVariable(PerfCap.EnvShow, "1");
            PerfCap.ResetForTest();
            PerfCap.SeedQaIfRequested();
            Check(PerfCap.ShowQa, "시드 ShowQa");
            Check(PerfCap.Line().Contains("500"),
                $"시드 줄 (실제 {PerfCap.Line()})");
            Environment.SetEnvironmentVariable(PerfCap.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string capSrc = File.ReadAllText(Path.Combine(runtime, "PerfCap.cs"));
            Check(capSrc.Contains("잡몹상한"),
                "PerfCap가 BalanceConfig.잡몹상한을 읽는다");
            string genSrc = File.ReadAllText(Path.Combine(runtime, "DungeonGenerator.cs"));
            Check(genSrc.Contains("PerfCap.MobLimit()"),
                "DungeonGenerator가 PerfCap.MobLimit를 읽는다 — 지우면 소비처 0곳으로 되돌아간다");
            string charSrc = File.ReadAllText(Path.Combine(runtime, "CharacterScreen.cs"));
            Check(charSrc.Contains("PerfCap.Line"),
                "CharacterScreen이 Line을 속성 탭에 그린다");
            Check(charSrc.Contains("PerfCap.SeedQaIfRequested"),
                "CharacterScreen이 SeedQa를 부른다");
            Check(charSrc.Contains("!roster[i].IsDeleted"),
                "시드가 삭제된 캐릭터를 건너뛴다 — 0번 환생시험은 속성 줄을 안 그린다");

            _ = nameof(PerfCap.MobLimit);
            _ = nameof(PerfCap.Line);
            _ = nameof(PerfCap.SeedQaIfRequested);
            _ = nameof(BalanceConfig.잡몹상한);
            _ = nameof(DungeonGenerator.MobHardCap);

            Environment.SetEnvironmentVariable(PerfCap.EnvShow, show);
            Environment.SetEnvironmentVariable(PerfCap.EnvNo, no);
            PerfCap.ResetForTest();
            GameState.ResetAll();

            string dir = Path.Combine(Application.dataPath, "../..", "results");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "perf_cap_selfcheck.log");
            var body = new StringBuilder();
            body.AppendLine(_fail == 0 ? "PASS PerfCapSelfCheck" : "FAIL PerfCapSelfCheck");
            body.Append(_log);
            File.WriteAllText(path, body.ToString());
            if (_fail == 0) Debug.Log("[PerfCapSelfCheck] PASS → " + path);
            else Debug.LogError("[PerfCapSelfCheck] FAIL " + _fail + " → " + path);
            if (_fail > 0) throw new InvalidOperationException(
                $"[PerfCapSelfCheck] FAIL {_fail}건");
        }
    }
}
