using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>긴급 탈출은 목숨 그대로·보상 0. QA_NO면 줄 없음(§3·§4).</summary>
    public static class EscapeForfeitSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Escape Forfeit Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(EscapeForfeit.EnvShow);
            string no = Environment.GetEnvironmentVariable(EscapeForfeit.EnvNo);
            Environment.SetEnvironmentVariable(EscapeForfeit.EnvShow, null);
            Environment.SetEnvironmentVariable(EscapeForfeit.EnvNo, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            InvasionState.ResetForTest();
            DefenseState.ResetForTest();
            WorldStar.ResetForTest();
            DungeonRun.End();
            EscapeForfeit.ResetForTest();
            Honor.ResetForTest();
            RacePrefs.Set(RaceId.인간);

            Check(!EscapeForfeit.Active, "시작은 포기 아님");
            Check(string.IsNullOrEmpty(EscapeForfeit.Line()), "시작 줄 없음");

            var roster = LifeSystem.GetCharacters();
            var ch = roster[0];
            int deaths = ch.DeathCount;
            long exp = ch.Exp;
            GameState.Grant(50_000);
            long gold = GameState.Wallet.Copper;
            var reward = new BattleRewardInfo
            {
                Survived = true,
                GoldReward = 12_345,
                BattleDurationSeconds = 90f,
            };
            reward.ExpGains.Add("가짜 +100");
            reward.DroppedItems.Add(Economy.LifeItem.CraftHide);

            EscapeForfeit.Apply(reward);
            Check(EscapeForfeit.Active, "Apply 뒤 Active");
            Check(reward.GoldReward == 0 && !reward.Survived && reward.ExpGains.Count == 0
                  && reward.DroppedItems.Count == 0,
                $"보상 비움 (골드 {reward.GoldReward} 생존 {reward.Survived})");
            Check(GameState.Wallet.Copper == gold, $"지갑 불변 (실제 {GameState.Wallet.Copper})");
            Check(ch.DeathCount == deaths, $"목숨 불변 (실제 {ch.DeathCount})");
            Check(ch.Exp == exp, $"경험 불변 (실제 {ch.Exp})");
            Check(GameFlow.LastBattleSummary.IndexOf("보상 포기", StringComparison.Ordinal) >= 0
                  && GameFlow.LastBattleSummary.IndexOf("§4", StringComparison.Ordinal) >= 0,
                $"요약 (실제 {GameFlow.LastBattleSummary})");
            Check(EscapeForfeit.Line().IndexOf("긴급 탈출", StringComparison.Ordinal) >= 0,
                $"줄 (실제 {EscapeForfeit.Line()})");
            Check(EscapeForfeit.Body().IndexOf("목숨은 그대로", StringComparison.Ordinal) >= 0,
                $"본문 (실제 {EscapeForfeit.Body()})");

            DungeonRun.Begin(7, 0, DungeonKind.일반, GameFlow.Field);
            Check(DungeonRun.Active, "던전 시작");
            EscapeForfeit.Apply(reward);
            Check(!DungeonRun.Active, "던전 End — 나가면 초기화(§7)");

            GameState.SetTowerFloorForTest(30);
            GameState.Grant(100_000);
            long beforeSortie = GameState.Wallet.Copper;
            Check(InvasionState.TryBegin(), "침략 대기");
            Check(InvasionState.Pending, "대기 중");
            long afterPay = GameState.Wallet.Copper;
            Check(afterPay < beforeSortie, "출정비는 이미 냄");
            EscapeForfeit.Apply(reward);
            Check(!InvasionState.Pending, "대기는 패배 정산 없이 취소");
            Check(InvasionState.LastLoot == 0, "약탈 0");
            Check(GameState.Wallet.Copper == afterPay,
                $"패배 추가 소모 없음 (실제 {GameState.Wallet.Copper})");

            Environment.SetEnvironmentVariable(EscapeForfeit.EnvNo, "1");
            EscapeForfeit.ResetForTest();
            GameFlow.LastBattleSummary = "이전";
            reward.Survived = true;
            reward.GoldReward = 99;
            EscapeForfeit.Apply(reward);
            Check(EscapeForfeit.Blocked, "QA_NO");
            Check(!EscapeForfeit.Active, "QA_NO면 Active 아님");
            Check(string.IsNullOrEmpty(EscapeForfeit.Line()), "QA_NO면 줄 없음");
            Check(reward.GoldReward == 99 && reward.Survived, "QA_NO면 보상을 안 비움");
            Check(GameFlow.LastBattleSummary == "이전", "QA_NO면 요약 불변");
            Environment.SetEnvironmentVariable(EscapeForfeit.EnvNo, null);

            Environment.SetEnvironmentVariable(EscapeForfeit.EnvShow, "1");
            EscapeForfeit.ResetForTest();
            EscapeForfeit.SeedQaIfRequested();
            Check(EscapeForfeit.Active, "QA 시드 Active");
            Check(EscapeForfeit.Line().IndexOf("§4", StringComparison.Ordinal) >= 0,
                "시드 줄");
            Environment.SetEnvironmentVariable(EscapeForfeit.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string battleSrc = File.ReadAllText(Path.Combine(runtime, "BattleScreen.cs"));
            string resultSrc = File.ReadAllText(Path.Combine(runtime, "ResultScreen.cs"));
            string invSrc = File.ReadAllText(Path.Combine(runtime, "InvasionState.cs"));
            Check(battleSrc.IndexOf("EscapeForfeit.Apply", StringComparison.Ordinal) >= 0,
                "전투가 Apply를 읽는다");
            Check(battleSrc.IndexOf("LeaveByEscape", StringComparison.Ordinal) >= 0,
                "전투가 LeaveByEscape를 부른다");
            Check(resultSrc.IndexOf("EscapeForfeit.Body", StringComparison.Ordinal) >= 0
                  && resultSrc.IndexOf("EscapeForfeit.SeedQaIfRequested", StringComparison.Ordinal) >= 0
                  && resultSrc.IndexOf("EscapeForfeit.Active", StringComparison.Ordinal) >= 0,
                "결과가 Body·Seed·Active를 읽는다");
            Check(invSrc.IndexOf("AbortPending", StringComparison.Ordinal) >= 0,
                "침략이 AbortPending을 갖는다");

            _ = nameof(EscapeForfeit.Apply);
            _ = nameof(EscapeForfeit.Line);
            _ = nameof(EscapeForfeit.SeedQaIfRequested);
            _ = nameof(InvasionState.AbortPending);

            Environment.SetEnvironmentVariable(EscapeForfeit.EnvShow, show);
            Environment.SetEnvironmentVariable(EscapeForfeit.EnvNo, no);
            EscapeForfeit.ResetForTest();
            InvasionState.ResetForTest();
            DungeonRun.End();
            GameState.ResetAll();
            LifeSystem.ResetAll();

            if (_fail == 0) Debug.Log("[EscapeForfeitSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[EscapeForfeitSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[EscapeForfeitSelfCheck] FAIL {_fail}건");
        }
    }
}
