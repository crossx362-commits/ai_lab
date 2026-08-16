using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>필드 배회 보스 — 허브 출현·보스전·FieldDungeonBoss 드랍. QA_NO면 없음(§10-1).</summary>
    public static class FieldBossSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Field Boss Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(FieldBoss.EnvShow);
            string no = Environment.GetEnvironmentVariable(FieldBoss.EnvNo);
            Environment.SetEnvironmentVariable(FieldBoss.EnvShow, null);
            Environment.SetEnvironmentVariable(FieldBoss.EnvNo, null);

            GameState.ResetAll();
            FieldBoss.ResetForTest();
            RaidBossPool.ResetForTest();
            RaidScale.ForceScalePercent = -1;
            GameState.SetTowerFloorForTest(51);
            GameState.TrySelectTier(0);

            Check(!FieldBoss.Active && !FieldBoss.Fighting, "시작은 출현 없음");
            Check(FieldBoss.DropSource == Economy.DropSource.FieldDungeonBoss,
                "드랍은 FieldDungeonBoss(환생석 없음)");

            SpawnForced(0);
            Check(FieldBoss.Active, "T1 강제 출현");
            Check(FieldBoss.FightFloor == 5, $"T1 출현 층 5 (실제 {FieldBoss.FightFloor})");
            Check(FieldBoss.Name().IndexOf("배회", StringComparison.Ordinal) >= 0,
                $"이름 배회 (실제 {FieldBoss.Name()})");
            Check(FieldBoss.Line().IndexOf("§10-1", StringComparison.Ordinal) >= 0,
                $"문구 §10-1 (실제 {FieldBoss.Line()})");
            Check(FieldBoss.CardBody().IndexOf("환생석 없음", StringComparison.Ordinal) >= 0,
                "카드 환생석 없음");

            FieldBoss.BeginFight();
            Check(FieldBoss.Fighting, "입장하면 Fighting");
            Check(!RaidScale.Applies(5), "필드 보스는 하위 레이드 스케일 아님");
            Check(!RaidBossPool.Applies(5), "필드 보스는 탑 풀 추첨 아님");
            Check(RaidScale.Gold(5) == RaidScale.LegacyGold(5),
                $"T1 골드는 원래 층 (실제 {RaidScale.Gold(5)})");
            FieldBoss.EndFight();
            Check(!FieldBoss.Fighting, "끝나면 Fighting 해제");

            FieldBoss.Consume();
            Check(!FieldBoss.Active, "들어가면 필드에서 사라진다");

            FieldBoss.ResetForTest();
            Environment.SetEnvironmentVariable(FieldBoss.EnvShow, "1");
            FieldBoss.SeedQaIfRequested();
            Check(FieldBoss.Active, "QA 시드 출현");
            Check(FieldBoss.FightFloor == 5, "시드는 T1·5층");
            Environment.SetEnvironmentVariable(FieldBoss.EnvShow, null);

            SpawnForced(1);
            Check(FieldBoss.FightFloor == 15, $"T2 출현 층 15 (실제 {FieldBoss.FightFloor})");
            Check(FieldBoss.Name() == "배회하는 강철 파수",
                $"T2 이름 (실제 {FieldBoss.Name()})");

            SpawnForced(4);
            Check(FieldBoss.FightFloor == 45, $"T5 출현 층 45 (실제 {FieldBoss.FightFloor})");
            FieldBoss.BeginFight();
            Check(!RaidScale.Applies(45), "고티어도 탑 스케일 아님");
            Check(RaidBossPool.DropSourceFor(45) == Economy.DropSource.Tower5Boss,
                "탑 테이블은 그대로 5층 키 — 필드가 그걸 안 읽는다");
            FieldBoss.EndFight();

            Environment.SetEnvironmentVariable(FieldBoss.EnvNo, "1");
            FieldBoss.ResetForTest();
            Environment.SetEnvironmentVariable(FieldBoss.EnvShow, "1");
            FieldBoss.SeedQaIfRequested();
            Check(FieldBoss.Blocked && !FieldBoss.Active && !FieldBoss.Fighting,
                "QA_NO면 출현·전투 없음");
            FieldBoss.BeginFight();
            Check(!FieldBoss.Fighting, "QA_NO면 BeginFight도 꺼짐");
            Environment.SetEnvironmentVariable(FieldBoss.EnvNo, null);
            Environment.SetEnvironmentVariable(FieldBoss.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string fieldSrc = File.ReadAllText(Path.Combine(runtime, "FieldScreen.cs"));
            string battleSrc = File.ReadAllText(Path.Combine(runtime, "BattleScreen.cs"));
            string flowSrc = File.ReadAllText(Path.Combine(runtime, "GameFlow.cs"));
            string scaleSrc = File.ReadAllText(Path.Combine(runtime, "RaidScale.cs"));
            string poolSrc = File.ReadAllText(Path.Combine(runtime, "RaidBossPool.cs"));
            Check(fieldSrc.IndexOf("FieldBoss.Active", StringComparison.Ordinal) >= 0
                  && fieldSrc.IndexOf("FieldBoss.SeedQaIfRequested", StringComparison.Ordinal) >= 0,
                "필드 허브가 Active·Seed를 읽는다");
            Check(fieldSrc.IndexOf("EnterFieldBoss", StringComparison.Ordinal) >= 0,
                "필드가 EnterFieldBoss를 부른다");
            Check(battleSrc.IndexOf("FieldBoss.DropSource", StringComparison.Ordinal) >= 0,
                "보스 보상이 FieldBoss.DropSource를 읽는다");
            Check(battleSrc.IndexOf("FieldBoss.Fighting", StringComparison.Ordinal) >= 0
                  && battleSrc.IndexOf("ApplyTowerBossVictory", StringComparison.Ordinal) >= 0,
                "필드 보스는 탑 층을 안 올린다");
            Check(flowSrc.IndexOf("FieldBoss.Fighting", StringComparison.Ordinal) >= 0,
                "GoBattle이 Fighting이면 탑 풀을 안 뽑는다");
            Check(flowSrc.IndexOf("FieldBoss.EndFight", StringComparison.Ordinal) >= 0,
                "전투를 떠나면 EndFight");
            Check(scaleSrc.IndexOf("FieldBoss.Fighting", StringComparison.Ordinal) >= 0,
                "RaidScale.Applies가 Fighting을 읽는다");
            Check(poolSrc.IndexOf("FieldBoss.Fighting", StringComparison.Ordinal) >= 0,
                "RaidBossPool.Applies가 Fighting을 읽는다");

            _ = nameof(FieldBoss.Active);
            _ = nameof(FieldBoss.DropSource);
            _ = nameof(FieldBoss.BeginFight);
            _ = nameof(FieldBoss.SeedQaIfRequested);

            Environment.SetEnvironmentVariable(FieldBoss.EnvShow, show);
            Environment.SetEnvironmentVariable(FieldBoss.EnvNo, no);
            FieldBoss.ResetForTest();
            RaidBossPool.ResetForTest();
            GameState.ResetAll();

            if (_fail == 0) Debug.Log("[FieldBossSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[FieldBossSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[FieldBossSelfCheck] FAIL {_fail}건");
        }

        static void SpawnForced(int tier)
        {
            FieldBoss.ResetForTest();
            GameState.SetTowerFloorForTest(51);
            GameState.TrySelectTier(tier);
            long now = 2_000_000L;
            FieldBoss.NowUnix = () => now;
            PlayerPrefs.SetString("ats.fieldboss.until", (now + FieldBoss.LifetimeSec).ToString());
            PlayerPrefs.SetInt("ats.fieldboss.tier", tier);
            PlayerPrefs.Save();
        }
    }
}
