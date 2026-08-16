using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>하위 레이드 스케일 0.65. 5층+T5=11524. QA_NO면 원래 층(§18-10).</summary>
    public static class RaidScaleSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Raid Scale Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(RaidScale.EnvShow);
            string no = Environment.GetEnvironmentVariable(RaidScale.EnvNo);
            Environment.SetEnvironmentVariable(RaidScale.EnvShow, null);
            Environment.SetEnvironmentVariable(RaidScale.EnvNo, null);

            GameState.ResetAll();
            RaidScale.ResetForTest();

            var cfg = ScriptableObject.CreateInstance<BalanceConfig>();
            Check(cfg != null && Mathf.Approximately(cfg.스케일링계수, 0.65f),
                $"BalanceConfig.스케일링계수 기본 0.65 (실제 {cfg?.스케일링계수})");
            Check(RaidScale.ScalePercent() == 65, $"읽기 65 (실제 {RaidScale.ScalePercent()})");
            UnityEngine.Object.DestroyImmediate(cfg);

            GameState.SetTowerFloorForTest(5);
            Check(!RaidScale.Applies(5), "첫 5층은 스케일 없음");
            Check(RaidScale.Gold(5) == 2500, $"첫 5층 골드 2500 (실제 {RaidScale.Gold(5)})");
            Check(RaidScale.LowerFloor == 0, "10층 전엔 하위 카드 없음");
            Check(RaidScale.TargetSeconds(5) == 0f, "첫 5층은 Begin 덮어쓰기 없음");

            GameState.SetTowerFloorForTest(51);
            Check(GameState.TrySelectTier(4), "T5 선택");
            Check(RaidScale.Applies(5), "51층에서 5층은 하위");
            Check(RaidScale.LowerFloor == 5, "하위 카드는 5층");
            long gold = RaidScale.Gold(5);
            Check(gold == 11523, $"5층+T5 골드 11523 (실제 {gold})");
            Check(RaidScale.Exp(5) == 460, $"5층+T5 경험 460 (실제 {RaidScale.Exp(5)})");
            Check(Mathf.Approximately(RaidScale.TargetSeconds(5), 226.5f),
                $"5층+T5 목표 226.5초 (실제 {RaidScale.TargetSeconds(5)})");
            Check(RaidScale.Gold(50) == 16383, $"50층+T5는 원래 T5 (실제 {RaidScale.Gold(50)})");

            Check(GameState.TrySelectTier(0), "T1로 내림");
            Check(RaidScale.Gold(50) == 7360, $"50층+T1은 7360 — 최고 기록 강제 상승 없음 (실제 {RaidScale.Gold(50)})");
            Check(GameState.TrySelectTier(4), "T5 복귀");

            string line = RaidScale.Line();
            Check(line.Contains("0.65") && line.Contains("§18-10") && line.Contains("5층")
                  && line.Contains("T1→T5") && line.Contains("1골드 15실버 23쿠퍼"),
                $"문구 (실제 {line})");

            RaidScale.ForceScalePercent = 50;
            Check(RaidScale.Gold(5) == 9441, $"계수 50%면 9441 (실제 {RaidScale.Gold(5)})");
            RaidScale.ForceScalePercent = -1;

            var half = ScriptableObject.CreateInstance<BalanceConfig>();
            half.스케일링계수 = 0.50f;
            RaidScale.ForceConfig = half;
            Check(RaidScale.ScalePercent() == 50, "ForceConfig가 스케일링계수를 읽는다");
            Check(RaidScale.Gold(5) == 9441, $"에셋 0.50 → 9441 (실제 {RaidScale.Gold(5)})");
            RaidScale.ForceConfig = null;
            UnityEngine.Object.DestroyImmediate(half);
            Check(RaidScale.ScalePercent() == 65, "에셋을 치우면 다시 65");

            Environment.SetEnvironmentVariable(RaidScale.EnvNo, "1");
            Check(RaidScale.Blocked, "QA_NO면 차단");
            Check(!RaidScale.Applies(5), "차단하면 Applies 없음");
            Check(RaidScale.Gold(5) == 2500, $"차단하면 원래 층 2500 (실제 {RaidScale.Gold(5)})");
            Check(RaidScale.FormatLine(5).Contains("없음"),
                $"차단 문구 (실제 {RaidScale.FormatLine(5)})");
            Environment.SetEnvironmentVariable(RaidScale.EnvNo, null);
            Check(RaidScale.Gold(5) == 11523, "차단을 풀면 다시 11523");

            GameState.ForgetInMemoryForTest();
            Check(GameState.TowerFloor == 51 && GameState.Tier == 4, "재기동 뒤에도 51층·T5");
            Check(RaidScale.Gold(5) == 11523, "재기동 뒤에도 11523");

            Environment.SetEnvironmentVariable(RaidScale.EnvShow, "1");
            GameState.ResetAll();
            RaidScale.ResetForTest();
            RaidScale.SeedQaIfRequested();
            Check(GameState.TowerFloor == 51 && GameState.Tier == 4, "시드는 51층·T5");
            Check(RaidScale.Line().Contains("1골드 15실버 23쿠퍼"),
                $"시드 문구 (실제 {RaidScale.Line()})");
            Environment.SetEnvironmentVariable(RaidScale.EnvShow, null);

            string battle = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/BattleScreen.cs"));
            Check(battle.Contains("RaidScale.Gold") && battle.Contains("RaidScale.Exp")
                  && battle.Contains("RaidScale.TargetSeconds"),
                "BattleScreen이 Gold·Exp·TargetSeconds를 읽는다");
            string tower = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/TowerScreen.cs"));
            Check(tower.Contains("RaidScale.Line") && tower.Contains("RaidScale.LowerFloor"),
                "TowerScreen이 Line·LowerFloor를 읽는다");
            string scaleSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/RaidScale.cs"));
            Check(scaleSrc.Contains("스케일링계수"), "RaidScale가 BalanceConfig.스케일링계수를 읽는다");

            _ = nameof(RaidScale.Gold);
            _ = nameof(RaidScale.Exp);
            _ = nameof(RaidScale.TargetSeconds);
            _ = nameof(RaidScale.Line);
            _ = nameof(RaidScale.SeedQaIfRequested);
            _ = nameof(BalanceConfig.스케일링계수);

            Environment.SetEnvironmentVariable(RaidScale.EnvShow, show);
            Environment.SetEnvironmentVariable(RaidScale.EnvNo, no);
            RaidScale.ResetForTest();
            GameState.ResetAll();

            if (_fail > 0)
            {
                Debug.LogError("[RaidScaleSelfCheck] FAIL\n" + _log);
                throw new InvalidOperationException("RaidScaleSelfCheck FAIL " + _fail);
            }
            Debug.Log("[RaidScaleSelfCheck] PASS\n" + _log);
        }
    }
}
