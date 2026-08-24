using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>탑 허브 제목판 부제는 한 줄. QA_NO면 옛 스펙 나열(§16).</summary>
    public static class TowerHubCapSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Tower Hub Cap Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(TowerHubCap.EnvShow);
            string no = Environment.GetEnvironmentVariable(TowerHubCap.EnvNo);
            Environment.SetEnvironmentVariable(TowerHubCap.EnvShow, null);
            Environment.SetEnvironmentVariable(TowerHubCap.EnvNo, null);

            GameState.ResetAll();
            DeathTraining.ResetForTest();
            RaidScale.ResetForTest();
            RaidBossPool.ResetForTest();
            RaidReroll.ResetForTest();
            RaidCost.ResetForTest();
            BossHp.ResetForTest();
            BossCount.ResetForTest();
            BossSkills.ResetForTest();

            Check(!TowerHubCap.Blocked, "기본은 켜짐");
            Check(TowerHubCap.Line().IndexOf("한 줄", StringComparison.Ordinal) >= 0,
                $"줄 (실제 {TowerHubCap.Line()})");

            DeathTraining.Consent();
            GameState.SetTowerFloorForTest(30);
            string rest =
                $"최대 100층. 해금 T{GameState.UnlockedTier + 1} · 세계 T{GameState.Tier + 1} · 보유 {GameState.WalletText}";
            string slim = CallCompose(rest);
            Check(string.IsNullOrEmpty(DeathTraining.Line()), "30층은 훈련 아님");
            Check(slim == rest, $"기본은 rest만 (실제 {slim})");
            Check(TowerHubCap.CaptionFits(slim),
                $"기본 길이 {TowerHubCap.RuneCount(slim)} ≤ {TowerHubCap.CaptionMaxRunes}");
            Check(slim.IndexOf("2→3→4", StringComparison.Ordinal) < 0
                  && slim.IndexOf("§10-5", StringComparison.Ordinal) < 0
                  && slim.IndexOf("§18-11", StringComparison.Ordinal) < 0,
                "기본에 스펙 덤프 없음");
            string shotDump = TowerHubCap.OldJoin(
                "",
                "하위 레이드 스케일 0.65(§18-10) · 5층 T1→T3 · 25실버",
                "하위 레이드 보스 풀 10종",
                "재입장 ×1(§18-2)",
                "대보스 38실버(§18-2)",
                "보스 HP는 기대 파티 472 DPS(§18-11)",
                "2체 각 65%(§18-11)",
                "대보스 2체(§10-7)",
                "대보스 2→3→4(§10-5)",
                rest);
            Check(!TowerHubCap.CaptionFits(shotDump),
                $"샷의 옛 줄 {TowerHubCap.RuneCount(shotDump)} > {TowerHubCap.CaptionMaxRunes}");
            Check(TowerHubCap.Compose(
                    "",
                    "하위 레이드 스케일 0.65(§18-10) · 5층 T1→T3 · 25실버",
                    "하위 레이드 보스 풀 10종",
                    "재입장 ×1(§18-2)",
                    "대보스 38실버(§18-2)",
                    "보스 HP는 기대 파티 472 DPS(§18-11)",
                    "2체 각 65%(§18-11)",
                    "대보스 2체(§10-7)",
                    "대보스 2→3→4(§10-5)",
                    rest) == rest,
                "Compose는 스펙 줄을 버린다");

            Environment.SetEnvironmentVariable(TowerHubCap.EnvNo, "1");
            Check(TowerHubCap.Blocked, "QA_NO");
            Check(TowerHubCap.Line().IndexOf("잘린다", StringComparison.Ordinal) >= 0,
                $"차단 줄 (실제 {TowerHubCap.Line()})");
            string old = CallCompose(rest);
            Check(old.IndexOf(rest, StringComparison.Ordinal) >= 0,
                $"옛 줄이 rest를 잇는다 (실제 {old})");
            if (old != slim)
            {
                Check(old.Length > slim.Length, $"옛 줄이 더 김 ({old.Length} > {slim.Length})");
                Check(!TowerHubCap.CaptionFits(old),
                    $"옛 줄 길이 {TowerHubCap.RuneCount(old)} > {TowerHubCap.CaptionMaxRunes}");
            }
            Environment.SetEnvironmentVariable(TowerHubCap.EnvNo, null);
            Check(!TowerHubCap.Blocked, "차단을 풀면 다시 켜짐");

            Environment.SetEnvironmentVariable(TowerHubCap.EnvShow, "1");
            Check(TowerHubCap.ShowQa, "시드 ShowQa");
            Check(TowerHubCap.Line().IndexOf("한 줄", StringComparison.Ordinal) >= 0, "시드 줄");
            Environment.SetEnvironmentVariable(TowerHubCap.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string tower = File.ReadAllText(Path.Combine(runtime, "TowerScreen.cs"));
            Check(tower.IndexOf("TowerHubCap.Compose", StringComparison.Ordinal) >= 0,
                "탑이 Compose를 읽는다");
            Check(tower.IndexOf("scale + \" · \" + rest", StringComparison.Ordinal) < 0
                  && tower.IndexOf("skills + \" · \" + rest", StringComparison.Ordinal) < 0,
                "탑 부제가 옛 스펙 잇기를 안 한다");

            Environment.SetEnvironmentVariable(TowerHubCap.EnvShow, show);
            Environment.SetEnvironmentVariable(TowerHubCap.EnvNo, no);
            DeathTraining.ResetForTest();
            RaidScale.ResetForTest();
            RaidBossPool.ResetForTest();
            RaidReroll.ResetForTest();
            RaidCost.ResetForTest();
            BossHp.ResetForTest();
            BossCount.ResetForTest();
            BossSkills.ResetForTest();
            GameState.ResetAll();

            if (_fail == 0) Debug.Log("[TowerHubCapSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[TowerHubCapSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[TowerHubCapSelfCheck] FAIL {_fail}건");
        }

        static string CallCompose(string rest) =>
            TowerHubCap.Compose(
                DeathTraining.Line(),
                RaidScale.Line(),
                RaidBossPool.Line(),
                RaidReroll.Line(),
                RaidCost.Line(),
                BossHp.Line(),
                BossHp.CountLine(),
                BossCount.Line(),
                BossSkills.Line(),
                rest);
    }
}
