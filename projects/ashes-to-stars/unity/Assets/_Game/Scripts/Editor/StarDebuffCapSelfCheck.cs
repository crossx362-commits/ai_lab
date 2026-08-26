using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §18-13 BalanceConfig.디버프중첩별상한 소비처.
    /// QA_NO_STAR_DEBUFF_CAP면 옛 무제한·상한 줄 없음.
    /// </summary>
    public static class StarDebuffCapSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Star Debuff Cap Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(StarDebuffCap.EnvShow);
            string no = Environment.GetEnvironmentVariable(StarDebuffCap.EnvNo);
            string auraNo = Environment.GetEnvironmentVariable(WorldStar.EnvNoDebuff);
            Environment.SetEnvironmentVariable(StarDebuffCap.EnvShow, null);
            Environment.SetEnvironmentVariable(StarDebuffCap.EnvNo, null);
            Environment.SetEnvironmentVariable(WorldStar.EnvNoDebuff, null);

            GameState.ResetAll();
            WorldStar.ResetForTest();
            StarDebuffCap.ResetForTest();

            var cfg = ScriptableObject.CreateInstance<BalanceConfig>();
            Check(cfg != null && cfg.디버프중첩별상한 == 2,
                $"BalanceConfig.디버프중첩별상한 기본 2 (실제 {cfg?.디버프중첩별상한})");
            Check(!StarDebuffCap.Blocked, "기본은 켜짐");
            Check(StarDebuffCap.Cap() == 2, $"읽기 2 (실제 {StarDebuffCap.Cap()})");
            Check(StarDebuffCap.Line() == "디버프 중첩 최대 2개 별(§18-13)",
                $"기본 줄 (실제 {StarDebuffCap.Line()})");
            Check(StarDebuffCap.Apply(0) == 0, "0은 0");
            Check(StarDebuffCap.Apply(1) == 1, "상한 아래 1은 그대로");
            Check(StarDebuffCap.Apply(2) == 2, "상한 2는 그대로");
            Check(StarDebuffCap.Apply(3) == 2, $"3은 2로 잘린다 (실제 {StarDebuffCap.Apply(3)})");
            Check(StarDebuffCap.Apply(5) == 2, $"5도 2로 잘린다 (실제 {StarDebuffCap.Apply(5)})");
            UnityEngine.Object.DestroyImmediate(cfg);

            Check(WorldStar.StackedCount() == 0, "기본 중첩 0 (자기 별 꺼짐)");
            Check(WorldStar.AppliedStacks() == 0, "Apply(0)=0");
            Check(WorldStar.EnemyPercent() == 100, "꺼지면 100");
            Check(WorldStar.DebuffCapLine() == "디버프 중첩 최대 2개 별(§18-13)",
                $"WorldStar 줄 (실제 {WorldStar.DebuffCapLine()})");

            WorldStar.EnemyDebuff = true;
            Check(WorldStar.StackedCount() == 1, "켜면 자기 별 중첩 1");
            Check(WorldStar.AppliedStacks() == 1, "Apply(1)=1 — 상한 2 아래");
            Check(WorldStar.EnemyPercent() == WorldStar.EnemyDebuffPercent,
                $"EnemyPercent가 AppliedStacks를 읽는다 (실제 {WorldStar.EnemyPercent()})");
            Check(WorldStar.ApplyEnemy(1000) == 950, "1별이면 1000→950 (공식은 그대로)");

            var one = ScriptableObject.CreateInstance<BalanceConfig>();
            one.디버프중첩별상한 = 1;
            StarDebuffCap.ForceConfig = one;
            Check(StarDebuffCap.Cap() == 1, "ForceConfig가 디버프중첩별상한을 읽는다");
            Check(StarDebuffCap.Line().Contains("1") && StarDebuffCap.Line().Contains("§18-13"),
                $"에셋 1 줄 (실제 {StarDebuffCap.Line()})");
            Check(StarDebuffCap.Apply(3) == 1, $"요청 3이 상한 1로 잘린다 (실제 {StarDebuffCap.Apply(3)})");
            Check(WorldStar.AppliedStacks() == 1, "자기 별 1은 상한 1에서도 1");
            StarDebuffCap.ForceConfig = null;
            UnityEngine.Object.DestroyImmediate(one);
            StarDebuffCap.ResetForTest();
            Check(StarDebuffCap.Cap() == 2, "에셋을 치우면 다시 2");

            GameState.ResetAll();
            WorldStar.ResetForTest();
            WorldStar.EnemyDebuff = true;
            Environment.SetEnvironmentVariable(StarDebuffCap.EnvNo, "1");
            Check(StarDebuffCap.Blocked, "QA_NO면 차단");
            var fake = ScriptableObject.CreateInstance<BalanceConfig>();
            fake.디버프중첩별상한 = 1;
            StarDebuffCap.ForceConfig = fake;
            Check(StarDebuffCap.Apply(5) == 5, "차단하면 ForceConfig 1도 옛 무제한(요청 그대로)");
            Check(StarDebuffCap.Line() == "", "차단하면 상한 줄 없음(옛 화면)");
            Check(WorldStar.DebuffCapLine() == "", "WorldStar 줄도 빈 문자열");
            Check(WorldStar.AppliedStacks() == 1, "차단해도 자기 별 1은 1(미클램프)");
            Check(WorldStar.EnemyPercent() == WorldStar.EnemyDebuffPercent,
                "차단해도 켜진 디버프는 95 — 옛 on/off");
            StarDebuffCap.ForceConfig = null;
            UnityEngine.Object.DestroyImmediate(fake);
            Environment.SetEnvironmentVariable(StarDebuffCap.EnvNo, null);
            StarDebuffCap.ResetForTest();
            Check(!StarDebuffCap.Blocked && StarDebuffCap.Line() == "디버프 중첩 최대 2개 별(§18-13)",
                "차단을 풀면 다시 상한 줄");

            Environment.SetEnvironmentVariable(StarDebuffCap.EnvShow, "1");
            StarDebuffCap.ResetForTest();
            WorldStar.SeedDebuffCapQaIfRequested();
            Check(WorldStar.ShowDebuffCapQa, "시드 ShowQa");
            Check(WorldStar.DebuffCapLine().Contains("2"),
                $"시드 줄 (실제 {WorldStar.DebuffCapLine()})");
            Environment.SetEnvironmentVariable(StarDebuffCap.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string capSrc = File.ReadAllText(Path.Combine(runtime, "StarDebuffCap.cs"));
            Check(capSrc.Contains("디버프중첩별상한"),
                "StarDebuffCap가 BalanceConfig.디버프중첩별상한을 읽는다");
            string starSrc = File.ReadAllText(Path.Combine(runtime, "WorldStar.cs"));
            Check(starSrc.Contains("StarDebuffCap.Apply"),
                "WorldStar.EnemyPercent가 StarDebuffCap.Apply를 읽는다 — 지우면 소비처 0곳으로 되돌아간다");
            Check(starSrc.Contains("StarDebuffCap.Line"),
                "WorldStar.DebuffCapLine이 Line을 읽는다");
            string mapSrc = File.ReadAllText(Path.Combine(runtime, "WorldMapScreen.cs"));
            Check(mapSrc.Contains("WorldStar.DebuffCapLine"),
                "WorldMapScreen 부제가 DebuffCapLine을 그린다");
            Check(mapSrc.Contains("WorldStar.ShowDebuffCapQa"),
                "부제 QA가 상한 줄을 올린다");
            Check(mapSrc.Contains("PlayerCopy(WorldStar.DebuffCapLine())"),
                "부제 QA가 DebuffCapLine을 올린다");
            Check(mapSrc.Contains("WorldStar.SeedDebuffCapQaIfRequested"),
                "WorldMapScreen이 SeedQa를 부른다");

            _ = nameof(StarDebuffCap.Cap);
            _ = nameof(StarDebuffCap.Apply);
            _ = nameof(StarDebuffCap.Line);
            _ = nameof(StarDebuffCap.SeedQaIfRequested);
            _ = nameof(WorldStar.StackedCount);
            _ = nameof(WorldStar.AppliedStacks);
            _ = nameof(WorldStar.DebuffCapLine);
            _ = nameof(WorldStar.SeedDebuffCapQaIfRequested);
            _ = nameof(BalanceConfig.디버프중첩별상한);

            Environment.SetEnvironmentVariable(StarDebuffCap.EnvShow, show);
            Environment.SetEnvironmentVariable(StarDebuffCap.EnvNo, no);
            Environment.SetEnvironmentVariable(WorldStar.EnvNoDebuff, auraNo);
            StarDebuffCap.ResetForTest();
            WorldStar.ResetForTest();
            GameState.ResetAll();

            string dir = Path.Combine(Application.dataPath, "../..", "results");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "star_debuff_cap_selfcheck.log");
            var body = new StringBuilder();
            body.AppendLine(_fail == 0 ? "PASS StarDebuffCapSelfCheck" : "FAIL StarDebuffCapSelfCheck");
            body.Append(_log);
            File.WriteAllText(path, body.ToString());
            if (_fail == 0) Debug.Log("[StarDebuffCapSelfCheck] PASS → " + path);
            else Debug.LogError("[StarDebuffCapSelfCheck] FAIL " + _fail + " → " + path);
            if (_fail > 0) throw new InvalidOperationException(
                $"[StarDebuffCapSelfCheck] FAIL {_fail}건");
        }
    }
}
