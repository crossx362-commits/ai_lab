using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §4 BalanceConfig.부활초소지상한 소비처.
    /// QA_NO_REVIVE_CAP면 옛 3·상한 줄 없음.
    /// </summary>
    public static class ReviveCapSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Revive Cap Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(ReviveCap.EnvShow);
            string no = Environment.GetEnvironmentVariable(ReviveCap.EnvNo);
            Environment.SetEnvironmentVariable(ReviveCap.EnvShow, null);
            Environment.SetEnvironmentVariable(ReviveCap.EnvNo, null);

            GameState.ResetAll();
            ReviveCap.ResetForTest();

            var cfg = ScriptableObject.CreateInstance<BalanceConfig>();
            Check(cfg != null && cfg.부활초소지상한 == 3,
                $"BalanceConfig.부활초소지상한 기본 3 (실제 {cfg?.부활초소지상한})");
            Check(!ReviveCap.Blocked, "기본은 켜짐");
            Check(ReviveCap.Limit() == 3, $"읽기 3 (실제 {ReviveCap.Limit()})");
            Check(ReviveCap.Line() == "부활초 소지 상한 3(§4)",
                $"기본 줄 (실제 {ReviveCap.Line()})");
            UnityEngine.Object.DestroyImmediate(cfg);

            Check(Economy.Capacity(Economy.LifeItem.RevivalTea) == 3,
                $"Capacity 부활초 3 (실제 {Economy.Capacity(Economy.LifeItem.RevivalTea)})");
            Check(GameState.Bag.TryAdd(Economy.LifeItem.RevivalTea, 3), "기본 3개 획득");
            Check(!GameState.Bag.TryAdd(Economy.LifeItem.RevivalTea, 1), "4번째는 거부");
            Check(GameState.Bag.GetCount(Economy.LifeItem.RevivalTea) == 3, "보유 3");

            var eight = ScriptableObject.CreateInstance<BalanceConfig>();
            eight.부활초소지상한 = 8;
            ReviveCap.ForceConfig = eight;
            Check(ReviveCap.Limit() == 8, "ForceConfig가 부활초소지상한을 읽는다");
            Check(ReviveCap.Line().Contains("8") && ReviveCap.Line().Contains("§4"),
                $"에셋 8 줄 (실제 {ReviveCap.Line()})");
            Check(Economy.Capacity(Economy.LifeItem.RevivalTea) == 8,
                $"Capacity가 Limit를 읽는다 (실제 {Economy.Capacity(Economy.LifeItem.RevivalTea)})");
            Check(GameState.Bag.TryAdd(Economy.LifeItem.RevivalTea, 1), "상한 8이면 4번째 허용");
            Check(GameState.Bag.GetCount(Economy.LifeItem.RevivalTea) == 4, "보유 4");
            ReviveCap.ForceConfig = null;
            UnityEngine.Object.DestroyImmediate(eight);
            Check(ReviveCap.Limit() == 3, "에셋을 치우면 다시 3");
            Check(!GameState.Bag.TryAdd(Economy.LifeItem.RevivalTea, 1),
                "상한을 되돌리면 5번째는 거부(이미 4)");

            GameState.ResetAll();
            Environment.SetEnvironmentVariable(ReviveCap.EnvNo, "1");
            Check(ReviveCap.Blocked, "QA_NO면 차단");
            var fake = ScriptableObject.CreateInstance<BalanceConfig>();
            fake.부활초소지상한 = 8;
            ReviveCap.ForceConfig = fake;
            Check(ReviveCap.Limit() == 3, "차단하면 ForceConfig 8도 옛 3");
            Check(ReviveCap.Line() == "", "차단하면 상한 줄 없음(옛 화면)");
            Check(Economy.Capacity(Economy.LifeItem.RevivalTea) == 3,
                "차단하면 Capacity도 3");
            ReviveCap.ForceConfig = null;
            UnityEngine.Object.DestroyImmediate(fake);
            Environment.SetEnvironmentVariable(ReviveCap.EnvNo, null);
            Check(!ReviveCap.Blocked && ReviveCap.Line() == "부활초 소지 상한 3(§4)",
                "차단을 풀면 다시 상한 줄");

            Environment.SetEnvironmentVariable(ReviveCap.EnvShow, "1");
            ReviveCap.ResetForTest();
            ReviveCap.SeedQaIfRequested();
            Check(ReviveCap.ShowQa, "시드 ShowQa");
            Check(ReviveCap.Line().Contains("3"),
                $"시드 줄 (실제 {ReviveCap.Line()})");
            Environment.SetEnvironmentVariable(ReviveCap.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string capSrc = File.ReadAllText(Path.Combine(runtime, "ReviveCap.cs"));
            Check(capSrc.Contains("부활초소지상한"),
                "ReviveCap가 BalanceConfig.부활초소지상한을 읽는다");
            string ecoSrc = File.ReadAllText(Path.Combine(runtime, "Economy.cs"));
            Check(ecoSrc.Contains("ReviveCap.Limit()"),
                "Economy.Capacity가 ReviveCap.Limit를 읽는다 — 지우면 소비처 0곳으로 되돌아간다");
            string bagSrc = File.ReadAllText(Path.Combine(runtime, "BagTextFmt.cs"));
            Check(bagSrc.Contains("Economy.Capacity"),
                "BagTextFmt가 Capacity를 읽는다");
            string partySrc = File.ReadAllText(Path.Combine(runtime, "PartyHudCap.cs"));
            Check(partySrc.Contains("ReviveCap.Limit()"),
                "PartyHudCap이 Limit를 읽는다");
            string charSrc = File.ReadAllText(Path.Combine(runtime, "CharacterScreen.cs"));
            Check(charSrc.Contains("ReviveCap.Line"),
                "CharacterScreen이 Line을 속성 탭에 그린다");
            Check(charSrc.Contains("ReviveCap.Limit()"),
                "부활초 사용 줄이 Limit를 읽는다");
            Check(charSrc.Contains("ReviveCap.SeedQaIfRequested"),
                "CharacterScreen이 SeedQa를 부른다");

            _ = nameof(ReviveCap.Limit);
            _ = nameof(ReviveCap.Line);
            _ = nameof(ReviveCap.SeedQaIfRequested);
            _ = nameof(BalanceConfig.부활초소지상한);
            _ = nameof(Economy.Capacity);

            Environment.SetEnvironmentVariable(ReviveCap.EnvShow, show);
            Environment.SetEnvironmentVariable(ReviveCap.EnvNo, no);
            ReviveCap.ResetForTest();
            GameState.ResetAll();

            string dir = Path.Combine(Application.dataPath, "../..", "results");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "revive_cap_selfcheck.log");
            var body = new StringBuilder();
            body.AppendLine(_fail == 0 ? "PASS ReviveCapSelfCheck" : "FAIL ReviveCapSelfCheck");
            body.Append(_log);
            File.WriteAllText(path, body.ToString());
            if (_fail == 0) Debug.Log("[ReviveCapSelfCheck] PASS → " + path);
            else Debug.LogError("[ReviveCapSelfCheck] FAIL " + _fail + " → " + path);
            if (_fail > 0) throw new InvalidOperationException(
                $"[ReviveCapSelfCheck] FAIL {_fail}건");
        }
    }
}
