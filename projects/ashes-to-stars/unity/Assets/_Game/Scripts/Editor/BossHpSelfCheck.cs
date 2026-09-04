using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>보스 HP는 층 기대 파티 + 레이드 벽. QA_NO면 옛 고정 100·벽 없음(§18-10·§18-11).</summary>
    public static class BossHpSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Boss Hp Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(BossHp.EnvShow);
            string no = Environment.GetEnvironmentVariable(BossHp.EnvNo);
            Environment.SetEnvironmentVariable(BossHp.EnvShow, null);
            Environment.SetEnvironmentVariable(BossHp.EnvNo, null);
            BossHp.ResetForTest();
            DungeonRun.End();

            Check(!BossHp.Blocked, "기본은 켜짐");
            Check(Mathf.Approximately(BossHp.PartyDps(1), BossHp.BaselineDps),
                $"1층 DPS {BossHp.PartyDps(1):0} = {BossHp.BaselineDps:0}");
            Check(Mathf.Approximately(BossHp.Hp(1, 90f), 9000f),
                $"1층 90초 HP {BossHp.Hp(1, 90f):0} = 9000");
            Check(BossHp.ExpectedLevel(1) == 1 && BossHp.ExpectedLevel(30) == 30,
                $"기대 레벨 1층=1 · 30층=30 (실제 {BossHp.ExpectedLevel(1)}/{BossHp.ExpectedLevel(30)})");

            float dps1 = BossHp.PartyDps(1);
            float dps30 = BossHp.PartyDps(30);
            Check(dps30 > dps1 * 1.5f,
                $"30층 DPS {dps30:0} > 1층 {dps1:0} ×1.5");
            Check(dps30 > 400f && dps30 < 550f,
                $"30층 DPS {dps30:0} 는 §18-10 ≈471");
            Check(Mathf.Approximately(BossHp.WallMul(5), BossHp.WallMid),
                $"5층 벽 {BossHp.WallMul(5):0.0} = 1.5");
            Check(Mathf.Approximately(BossHp.WallMul(10), BossHp.WallMega),
                $"10층 벽 {BossHp.WallMul(10):0.0} = 2.2");
            Check(Mathf.Approximately(BossHp.WallMul(15), BossHp.WallMid)
                  && Mathf.Approximately(BossHp.WallMul(30), BossHp.WallMega),
                $"15층 1.5 · 30층 2.2 (실제 {BossHp.WallMul(15):0.0}/{BossHp.WallMul(30):0.0})");
            Check(Mathf.Approximately(BossHp.WallMul(1), 1f)
                  && Mathf.Approximately(BossHp.WallMul(6), 1f),
                $"1·6층은 벽 없음 (실제 {BossHp.WallMul(1):0.0}/{BossHp.WallMul(6):0.0})");
            DungeonRun.Begin(1u, 0, DungeonKind.일반, GameFlow.Field);
            Check(Mathf.Approximately(BossHp.WallMul(5), 1f)
                  && Mathf.Approximately(BossHp.WallMul(10), 1f),
                $"던전 중엔 탑 벽 없음 (실제 {BossHp.WallMul(5):0.0}/{BossHp.WallMul(10):0.0})");
            DungeonRun.End();
            Check(Mathf.Approximately(BossHp.WallMul(5), BossHp.WallMid),
                "던전을 나가면 5층 벽이 돌아온다");
            Check(Mathf.Approximately(BossHp.Hp(5, 90f), BossHp.PartyDps(5) * 90f * BossHp.WallMid),
                $"5층 HP는 기대 DPS ×90 ×1.5 (실제 {BossHp.Hp(5, 90f):0})");
            Check(Mathf.Approximately(BossHp.Hp(6, 90f), BossHp.PartyDps(6) * 90f),
                $"6층 HP는 벽 없음 (실제 {BossHp.Hp(6, 90f):0})");
            Check(BossHp.Hp(30, 300f) > BossHp.Hp(1, 90f) * 10f,
                $"30층 300초 HP {BossHp.Hp(30, 300f):0} > 1층 10배");
            Check(BossHp.Line(5).IndexOf("×1.5", StringComparison.Ordinal) >= 0
                  && BossHp.Line(5).IndexOf("§18-10", StringComparison.Ordinal) >= 0,
                $"5층 줄 (실제 {BossHp.Line(5)})");
            Check(BossHp.Line(10).IndexOf("×2.2", StringComparison.Ordinal) >= 0
                  && BossHp.Line(10).IndexOf("대보스", StringComparison.Ordinal) >= 0,
                $"10층 줄 (실제 {BossHp.Line(10)})");
            Check(BossHp.Line(6).IndexOf("×", StringComparison.Ordinal) < 0,
                $"6층 줄은 벽 없음 (실제 {BossHp.Line(6)})");

            int steps = 0;
            for (int f = 2; f <= 30; f++)
                if (!Mathf.Approximately(BossHp.PartyDps(f), BossHp.PartyDps(f - 1))) steps++;
            Check(steps >= 10, $"1~30층 변화 {steps}회 ≥ 10");

            Check(Mathf.Approximately(BossHp.CountMul(2), 0.65f)
                  && Mathf.Approximately(BossHp.CountMul(3), 0.45f),
                "2체 65% · 3체 45%");
            Check(Mathf.Approximately(BossHp.Hp(10, 180f, 2),
                    BossHp.PartyDps(10) * 180f * BossHp.WallMega * BossHp.TwoMul),
                $"10층 2체 HP는 ×2.2 ×0.65 (실제 {BossHp.Hp(10, 180f, 2):0})");
            Check(Mathf.Approximately(BossHp.Hp(10, 180f, 3),
                    BossHp.PartyDps(10) * 180f * BossHp.WallMega * BossHp.ThreeMul),
                $"10층 3체 HP는 ×2.2 ×0.45 (실제 {BossHp.Hp(10, 180f, 3):0})");
            Check(Mathf.Approximately(BossHp.Hp(10, 180f, 1), BossHp.Hp(10, 180f)),
                "1체는 기본 Hp와 같다");
            Check(BossHp.CountLine(2).IndexOf("65%", StringComparison.Ordinal) >= 0
                  && BossHp.CountLine(3).IndexOf("45%", StringComparison.Ordinal) >= 0
                  && string.IsNullOrEmpty(BossHp.CountLine(1)),
                $"마릿수 줄 (실제 {BossHp.CountLine(2)} / {BossHp.CountLine(3)})");

            Environment.SetEnvironmentVariable(BossHp.EnvNo, "1");
            Check(BossHp.Blocked, "QA_NO면 차단");
            Check(Mathf.Approximately(BossHp.PartyDps(30), BossHp.BaselineDps),
                $"차단 30층 DPS {BossHp.PartyDps(30):0} = 100");
            Check(Mathf.Approximately(BossHp.Hp(5, 90f), 9000f),
                $"차단 5층 HP {BossHp.Hp(5, 90f):0} = 옛 9000");
            Check(Mathf.Approximately(BossHp.WallMul(10), 1f)
                  && Mathf.Approximately(BossHp.Hp(10, 180f), 18000f),
                $"차단 10층도 벽 없음 (실제 {BossHp.WallMul(10):0.0} {BossHp.Hp(10, 180f):0})");
            Check(BossHp.Line().Contains("고정"),
                $"차단 줄 (실제 {BossHp.Line()})");
            Environment.SetEnvironmentVariable(BossHp.EnvNo, null);

            Environment.SetEnvironmentVariable(BossHp.EnvShow, "1");
            BossHp.SeedQaIfRequested();
            Check(BossHp.ShowQa, "시드 켜짐");
            Check(GameState.TowerFloor == 30, $"시드 30층 (실제 {GameState.TowerFloor})");
            Check(BossHp.Line().Contains("§18-11")
                  && BossHp.Line().IndexOf("×2.2", StringComparison.Ordinal) >= 0
                  && BossHp.Line().IndexOf("§18-10", StringComparison.Ordinal) >= 0,
                $"시드 줄 (실제 {BossHp.Line()})");
            Environment.SetEnvironmentVariable(BossHp.EnvShow, null);
            BossHp.ResetForTest();

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string battle = File.ReadAllText(Path.Combine(runtime, "BossBattle.cs"));
            string tower = File.ReadAllText(Path.Combine(runtime, "TowerScreen.cs"));
            Check(battle.Contains("BossHp.Hp"), "보스가 Hp를 읽는다");
            Check(battle.Contains("WallMul"), "보스가 벽 배율을 안다");
            Check(battle.IndexOf("BossHp.Hp(currentFloor, targetClearTime, bossCount)",
                    StringComparison.Ordinal) >= 0,
                "보스가 Hp에 마릿수를 넘긴다");
            Check(battle.Contains("BossHp.CountMul"), "차단 길도 CountMul을 읽는다");
            Check(battle.IndexOf("singleBossHp * 0.65f", StringComparison.Ordinal) < 0
                  && battle.IndexOf("singleBossHp * 0.45f", StringComparison.Ordinal) < 0,
                "옛 로컬 65/45를 안 쓴다");
            Check(!battle.Contains("totalPartyDps = basePartyDps"),
                "옛 고정 basePartyDps 대입을 안 쓴다");
            Check(tower.Contains("BossHp.Line"), "탑이 Line을 읽는다");
            Check(tower.Contains("BossHp.CountLine"), "탑이 CountLine을 읽는다");
            Check(tower.Contains("BossHp.SeedQaIfRequested"), "탑이 시드를 읽는다");
            string hpSrc = File.ReadAllText(Path.Combine(runtime, "BossHp.cs"));
            Check(hpSrc.IndexOf("WallMul(floor)", StringComparison.Ordinal) >= 0
                  && hpSrc.IndexOf("* WallMul", StringComparison.Ordinal) >= 0,
                "Hp가 WallMul을 곱한다");
            Check(hpSrc.IndexOf("* CountMul", StringComparison.Ordinal) >= 0,
                "Hp가 CountMul을 곱한다");
            _ = nameof(BossHp.WallMul);
            _ = nameof(BossHp.CountMul);
            _ = nameof(BossHp.CountLine);

            Environment.SetEnvironmentVariable(BossHp.EnvShow, show);
            Environment.SetEnvironmentVariable(BossHp.EnvNo, no);
            if (_fail == 0) Debug.Log("[BossHpSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[BossHpSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[BossHpSelfCheck] FAIL {_fail}건");
        }
    }
}
