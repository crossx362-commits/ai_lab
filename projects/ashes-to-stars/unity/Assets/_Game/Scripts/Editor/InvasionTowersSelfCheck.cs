using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>침략에서 화살탑·마법탑이 쏜다. QA_NO면 옛 잡몹 웨이브.</summary>
    public static class InvasionTowersSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Invasion Towers Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string no = Environment.GetEnvironmentVariable(InvasionTowers.EnvNo);
            var kind = GameFlow.Kind;
            Environment.SetEnvironmentVariable(InvasionTowers.EnvNo, null);
            GameState.ResetAll();
            EstateDefense.ResetForTest();
            InboundRaid.ResetForTest();
            InvasionTowers.ResetForTest();
            GameFlow.Kind = GameFlow.BattleKind.잡몹웨이브;

            Check(!InvasionTowers.Blocked, "기본은 켠다");
            Check(!InvasionTowers.InPlay, "잡몹 웨이브는 탑이 안 쏜다");
            Check(Mathf.Approximately(InvasionTowers.WallTakenMul, 1f), "비침략은 성벽 배율 1");
            Check(InvasionTowers.TrapBurst <= 0f, "비침략은 함정 0");

            GameFlow.Kind = GameFlow.BattleKind.침략;
            Check(InvasionTowers.InPlay, "침략이면 켠다");
            Check(!InvasionTowers.FriendlyNow(), "출정은 적 탑");
            GameState.SetTowerFloorForTest(30);
            Check(InvasionTowers.DummyLevel == 3, $"출정 더미 레벨 3 (실제 {InvasionTowers.DummyLevel})");
            Check(InvasionTowers.ArrowLv == 3 && InvasionTowers.MagicLv == 3, "출정은 더미 화살·마법");
            Check(InvasionTowers.WallLv == 0 && InvasionTowers.TrapLv == 0, "출정은 내 성벽·함정 없음");
            Check(Mathf.Approximately(InvasionTowers.WallTakenMul, 1f), "출정은 파티가 성벽 혜택 없음");
            Check(InvasionTowers.TrapBurst <= 0f, "출정은 내 함정 폭발 없음");
            Check(InvasionTowers.ArrowDmg > 0f && InvasionTowers.MagicDmg > 0f, "출정 적 탑 피해 > 0");

            InvasionTowers.FriendlyNow = () => true;
            EstateDefense.SetLevelForTest(EstateDefense.Kind.화살탑, 2);
            EstateDefense.SetLevelForTest(EstateDefense.Kind.마법탑, 1);
            EstateDefense.SetLevelForTest(EstateDefense.Kind.성벽, 2);
            EstateDefense.SetLevelForTest(EstateDefense.Kind.함정, 1);
            EstateDefense.GarrisonCount = () => 1;
            Check(InvasionTowers.ArrowLv == 2 && InvasionTowers.MagicLv == 1, "수비는 내 건물 레벨");
            Check(Mathf.Approximately(InvasionTowers.ArrowDmg, 12f),
                $"화살탑2 피해 12 (실제 {InvasionTowers.ArrowDmg})");
            Check(Mathf.Approximately(InvasionTowers.MagicDmg, 4f),
                $"마법탑1 피해 4 (실제 {InvasionTowers.MagicDmg})");
            Check(Mathf.Approximately(InvasionTowers.TrapBurst, 8f),
                $"함정1 폭발 8 (실제 {InvasionTowers.TrapBurst})");
            Check(Mathf.Approximately(InvasionTowers.WallTakenMul, 0.92f),
                $"성벽2 받는 피해 0.92 (실제 {InvasionTowers.WallTakenMul})");

            EstateDefense.GarrisonCount = () => 0;
            Check(Mathf.Approximately(InvasionTowers.ArrowDmg, 6f),
                $"수비 없으면 화살 효율 절반 (실제 {InvasionTowers.ArrowDmg})");
            Check(Mathf.Approximately(InvasionTowers.WallTakenMul, 0.96f),
                "수비 없으면 성벽도 절반");

            Environment.SetEnvironmentVariable(InvasionTowers.EnvNo, "1");
            Check(InvasionTowers.Blocked && !InvasionTowers.InPlay, "QA_NO면 옛 웨이브");
            Check(Mathf.Approximately(InvasionTowers.WallTakenMul, 1f), "차단하면 성벽 없음");
            Environment.SetEnvironmentVariable(InvasionTowers.EnvNo, null);

            string party = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/W3Party.cs"));
            Check(party.Contains("TickInvasionTowers") && party.Contains("InvasionTowers.InPlay"),
                "W3Party가 침략 탑을 틱한다");
            Check(party.Contains("InvasionTowers.WallTakenMul") && party.Contains("InvasionTowers.TrapBurst"),
                "성벽·함정이 피해 관문에 들어간다");

            EstateDefense.ResetForTest();
            InboundRaid.ResetForTest();
            InvasionTowers.ResetForTest();
            GameFlow.Kind = kind;
            Environment.SetEnvironmentVariable(InvasionTowers.EnvNo, no);
            if (_fail == 0) Debug.Log("[InvasionTowersSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[InvasionTowersSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[InvasionTowersSelfCheck] FAIL {_fail}건");
        }
    }
}
