using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>보스 HP는 층 기대 파티에서 온다. QA_NO면 옛 고정 100(§18-11).</summary>
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
            Check(BossHp.Hp(30, 300f) > BossHp.Hp(1, 90f) * 10f,
                $"30층 300초 HP {BossHp.Hp(30, 300f):0} > 1층 10배");

            int steps = 0;
            for (int f = 2; f <= 30; f++)
                if (!Mathf.Approximately(BossHp.PartyDps(f), BossHp.PartyDps(f - 1))) steps++;
            Check(steps >= 10, $"1~30층 변화 {steps}회 ≥ 10");

            Check(Mathf.Approximately(BossHp.CountMul(2), 0.65f)
                  && Mathf.Approximately(BossHp.CountMul(3), 0.45f),
                "2체 65% · 3체 45%");

            Environment.SetEnvironmentVariable(BossHp.EnvNo, "1");
            Check(BossHp.Blocked, "QA_NO면 차단");
            Check(Mathf.Approximately(BossHp.PartyDps(30), BossHp.BaselineDps),
                $"차단 30층 DPS {BossHp.PartyDps(30):0} = 100");
            Check(Mathf.Approximately(BossHp.Hp(5, 90f), 9000f),
                $"차단 5층 HP {BossHp.Hp(5, 90f):0} = 옛 9000");
            Check(BossHp.Line().Contains("고정"),
                $"차단 줄 (실제 {BossHp.Line()})");
            Environment.SetEnvironmentVariable(BossHp.EnvNo, null);

            Environment.SetEnvironmentVariable(BossHp.EnvShow, "1");
            BossHp.SeedQaIfRequested();
            Check(BossHp.ShowQa, "시드 켜짐");
            Check(GameState.TowerFloor == 30, $"시드 30층 (실제 {GameState.TowerFloor})");
            Check(BossHp.Line().Contains("§18-11"),
                $"시드 줄 (실제 {BossHp.Line()})");
            Environment.SetEnvironmentVariable(BossHp.EnvShow, null);
            BossHp.ResetForTest();

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string battle = File.ReadAllText(Path.Combine(runtime, "BossBattle.cs"));
            string tower = File.ReadAllText(Path.Combine(runtime, "TowerScreen.cs"));
            Check(battle.Contains("BossHp.Hp"), "보스가 Hp를 읽는다");
            Check(!battle.Contains("totalPartyDps = basePartyDps"),
                "옛 고정 basePartyDps 대입을 안 쓴다");
            Check(tower.Contains("BossHp.Line"), "탑이 Line을 읽는다");
            Check(tower.Contains("BossHp.SeedQaIfRequested"), "탑이 시드를 읽는다");

            Environment.SetEnvironmentVariable(BossHp.EnvShow, show);
            Environment.SetEnvironmentVariable(BossHp.EnvNo, no);
            if (_fail == 0) Debug.Log("[BossHpSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[BossHpSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[BossHpSelfCheck] FAIL {_fail}건");
        }
    }
}
