using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §18-1 BalanceConfig.티어1시간당골드 소비처.
    /// QA_NO_GH_ANCHOR면 옛 1골드·앵커 줄 없음.
    /// </summary>
    public static class GhAnchorSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Gh Anchor Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(GhAnchor.EnvShow);
            string no = Environment.GetEnvironmentVariable(GhAnchor.EnvNo);
            string huntNo = Environment.GetEnvironmentVariable(Economy.EnvNoHuntGold);
            Environment.SetEnvironmentVariable(GhAnchor.EnvShow, null);
            Environment.SetEnvironmentVariable(GhAnchor.EnvNo, null);
            Environment.SetEnvironmentVariable(Economy.EnvNoHuntGold, null);

            GameState.ResetAll();
            GhAnchor.ResetForTest();
            SoftCap.ResetForTest();

            var cfg = ScriptableObject.CreateInstance<BalanceConfig>();
            Check(cfg != null && Mathf.Approximately(cfg.티어1시간당골드, 1f),
                $"BalanceConfig.티어1시간당골드 기본 1 (실제 {cfg?.티어1시간당골드})");
            Check(!GhAnchor.Blocked, "기본은 켜짐");
            Check(Mathf.Approximately(GhAnchor.Hours(), 1f), $"읽기 1 (실제 {GhAnchor.Hours()})");
            Check(GhAnchor.CopperPerHour() == 10_000,
                $"T1 쿠퍼 10000 (실제 {GhAnchor.CopperPerHour()})");
            Check(GhAnchor.Line() == "G/h 앵커 1골드(§18-1)",
                $"기본 줄 (실제 {GhAnchor.Line()})");
            Check(Economy.WaveHuntGold(0, 3600f) == 10_000,
                $"T1 3600초 = 10000 (실제 {Economy.WaveHuntGold(0, 3600f)})");
            Check(Economy.HuntGoldHourLine().Contains("1골드"),
                $"필드 시간당이 Hours를 읽는다 (실제 {Economy.HuntGoldHourLine()})");
            UnityEngine.Object.DestroyImmediate(cfg);

            var two = ScriptableObject.CreateInstance<BalanceConfig>();
            two.티어1시간당골드 = 2f;
            GhAnchor.ForceConfig = two;
            Check(Mathf.Approximately(GhAnchor.Hours(), 2f), "ForceConfig가 티어1시간당골드를 읽는다");
            Check(GhAnchor.Line().Contains("2골드") && GhAnchor.Line().Contains("§18-1"),
                $"에셋 2골드 줄 (실제 {GhAnchor.Line()})");
            Check(Economy.WaveHuntGold(0, 3600f) == 20_000,
                $"생성기가 Hours를 읽는다 (실제 {Economy.WaveHuntGold(0, 3600f)})");
            Check(Economy.HuntGoldHourLine().Contains("2골드"),
                $"시간당 줄이 2골드 (실제 {Economy.HuntGoldHourLine()})");
            GhAnchor.ForceConfig = null;
            UnityEngine.Object.DestroyImmediate(two);
            GhAnchor.ResetForTest();
            Check(Mathf.Approximately(GhAnchor.Hours(), 1f), "에셋을 치우면 다시 1");
            Check(Economy.WaveHuntGold(0, 3600f) == 10_000, "에셋을 치우면 다시 10000");

            GameState.ResetAll();
            GhAnchor.ResetForTest();
            Environment.SetEnvironmentVariable(GhAnchor.EnvNo, "1");
            Check(GhAnchor.Blocked, "QA_NO면 차단");
            var fake = ScriptableObject.CreateInstance<BalanceConfig>();
            fake.티어1시간당골드 = 2f;
            GhAnchor.ForceConfig = fake;
            Check(Mathf.Approximately(GhAnchor.Hours(), 1f), "차단하면 ForceConfig 2도 옛 1");
            Check(GhAnchor.Line() == "", "차단하면 앵커 줄 없음(옛 화면)");
            Check(Economy.WaveHuntGold(0, 3600f) == 10_000, "차단하면 골드도 옛 1");
            GhAnchor.ForceConfig = null;
            UnityEngine.Object.DestroyImmediate(fake);
            Environment.SetEnvironmentVariable(GhAnchor.EnvNo, null);
            GhAnchor.ResetForTest();
            Check(!GhAnchor.Blocked && GhAnchor.Line() == "G/h 앵커 1골드(§18-1)",
                "차단을 풀면 다시 앵커 줄");

            Environment.SetEnvironmentVariable(GhAnchor.EnvShow, "1");
            GhAnchor.ResetForTest();
            GhAnchor.SeedQaIfRequested();
            Check(GhAnchor.ShowQa, "시드 ShowQa");
            Check(GhAnchor.Line().Contains("1골드"),
                $"시드 줄 (실제 {GhAnchor.Line()})");
            Environment.SetEnvironmentVariable(GhAnchor.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string capSrc = File.ReadAllText(Path.Combine(runtime, "GhAnchor.cs"));
            Check(capSrc.Contains("티어1시간당골드"),
                "GhAnchor가 BalanceConfig.티어1시간당골드를 읽는다");
            string ecoSrc = File.ReadAllText(Path.Combine(runtime, "Economy.cs"));
            Check(ecoSrc.Contains("GhAnchor.Hours"),
                "Economy.WaveHuntGold가 GhAnchor.Hours를 읽는다 — 지우면 소비처 0곳으로 되돌아간다");
            string charSrc = File.ReadAllText(Path.Combine(runtime, "CharacterScreen.cs"));
            Check(charSrc.Contains("GhAnchor.Line"),
                "CharacterScreen이 Line을 속성 탭에 그린다");
            Check(charSrc.Contains("budget + \" · \" + gh"),
                "앵커 줄을 잡몹 행에 붙인다");
            Check(charSrc.Contains("GhAnchor.ShowQa ? GhAnchor.Line()"),
                "부제에 앵커 줄을 올린다 — 속성 패널 맨 뒤는 샷에 안 나온다");
            Check(charSrc.Contains("GhAnchor.SeedQaIfRequested"),
                "CharacterScreen이 SeedQa를 부른다");
            Check(charSrc.Contains("!roster[i].IsDeleted"),
                "시드가 삭제된 캐릭터를 건너뛴다");

            _ = nameof(GhAnchor.Hours);
            _ = nameof(GhAnchor.CopperPerHour);
            _ = nameof(GhAnchor.Line);
            _ = nameof(GhAnchor.SeedQaIfRequested);
            _ = nameof(BalanceConfig.티어1시간당골드);
            _ = nameof(Economy.WaveHuntGold);

            Environment.SetEnvironmentVariable(GhAnchor.EnvShow, show);
            Environment.SetEnvironmentVariable(GhAnchor.EnvNo, no);
            Environment.SetEnvironmentVariable(Economy.EnvNoHuntGold, huntNo);
            GhAnchor.ResetForTest();
            GameState.ResetAll();

            string dir = Path.Combine(Application.dataPath, "../..", "results");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "gh_anchor_selfcheck.log");
            var body = new StringBuilder();
            body.AppendLine(_fail == 0 ? "PASS GhAnchorSelfCheck" : "FAIL GhAnchorSelfCheck");
            body.Append(_log);
            File.WriteAllText(path, body.ToString());
            if (_fail == 0) Debug.Log("[GhAnchorSelfCheck] PASS → " + path);
            else Debug.LogError("[GhAnchorSelfCheck] FAIL " + _fail + " → " + path);
            if (_fail > 0) throw new InvalidOperationException(
                $"[GhAnchorSelfCheck] FAIL {_fail}건");
        }
    }
}
