using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>필드 도크 일정·저체력 부제는 한 줄. QA_NO면 옛 긴 줄(§16).</summary>
    public static class FieldDockCapSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Field Dock Cap Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(FieldDockCap.EnvShow);
            string boss = Environment.GetEnvironmentVariable(FieldDockCap.EnvBoss);
            string no = Environment.GetEnvironmentVariable(FieldDockCap.EnvNo);
            Environment.SetEnvironmentVariable(FieldDockCap.EnvShow, null);
            Environment.SetEnvironmentVariable(FieldDockCap.EnvBoss, null);
            Environment.SetEnvironmentVariable(FieldDockCap.EnvNo, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            HuntSchedule.ResetForTest();
            FieldBoss.ResetForTest();
            FieldDockCap.ResetForTest();

            Check(!FieldDockCap.Blocked, "기본은 켜짐");
            Check(FieldDockCap.LowHp() == "30%면 3초 이탈",
                $"저체력 (실제 {FieldDockCap.LowHp()})");
            Check(FieldDockCap.CaptionFits(FieldDockCap.LowHp()),
                $"저체력 길이 {FieldDockCap.RuneCount(FieldDockCap.LowHp())} ≤ {FieldDockCap.CaptionMaxRunes}");
            Check(FieldDockCap.Schedule() == "허브에서도 돈다 · 12h",
                $"일정 꺼짐 (실제 {FieldDockCap.Schedule()})");
            Check(FieldDockCap.CaptionFits(FieldDockCap.Schedule()),
                $"일정 길이 {FieldDockCap.RuneCount(FieldDockCap.Schedule())} ≤ {FieldDockCap.CaptionMaxRunes}");
            Check(FieldDockCap.Death() == "카운트 없음 · 12h",
                $"사망없음 (실제 {FieldDockCap.Death()})");
            string lockedDeath = "잠김 — " + FieldDockCap.Death();
            Check(FieldDockCap.CaptionFits(lockedDeath),
                $"잠김 사망없음 {FieldDockCap.RuneCount(lockedDeath)} ≤ {FieldDockCap.CaptionMaxRunes}");
            Check(FieldDockCap.Line().IndexOf("한 줄", StringComparison.Ordinal) >= 0,
                $"줄 (실제 {FieldDockCap.Line()})");

            FieldBoss.ForceSpawnForTest(0);
            Check(FieldDockCap.ShortBossName() == "재의 야수",
                $"짧은 이름 T1 (실제 {FieldDockCap.ShortBossName()})");
            Check(FieldDockCap.Boss() == "재의 야수 · 환생석 없음",
                $"배회 보스 (실제 {FieldDockCap.Boss()})");
            Check(FieldDockCap.CaptionFits(FieldDockCap.Boss()),
                $"배회 보스 길이 {FieldDockCap.RuneCount(FieldDockCap.Boss())} ≤ {FieldDockCap.CaptionMaxRunes}");
            Check(FieldDockCap.OldBoss().IndexOf("준비 없이", StringComparison.Ordinal) >= 0
                  && !FieldDockCap.CaptionFits(FieldDockCap.OldBoss()),
                $"옛 배회 보스는 긴 줄 (실제 {FieldDockCap.OldBoss()})");
            FieldBoss.ForceSpawnForTest(9);
            Check(FieldDockCap.ShortBossName() == "탑의 그림자",
                $"짧은 이름 T10 (실제 {FieldDockCap.ShortBossName()})");
            Check(FieldDockCap.CaptionFits(FieldDockCap.Boss()),
                $"T10 배회 보스 길이 {FieldDockCap.RuneCount(FieldDockCap.Boss())} ≤ {FieldDockCap.CaptionMaxRunes}");
            FieldBoss.ResetForTest();
            Check(FieldDockCap.BossLine().IndexOf("한 줄", StringComparison.Ordinal) >= 0,
                $"배회 줄 (실제 {FieldDockCap.BossLine()})");

            GameState.SetTowerFloorForTest(1);
            GameState.TrySelectTier(0);
            _ = LifeSystem.GetCharacters();
            _ = PartyState.Slots;
            if (PartyState.Slots.Count == 0)
            {
                Check(false, "편성이 없어 일정 시작을 못 잰다");
            }
            else
            {
                Check(HuntSchedule.TryStart(), "일정 시작");
                string run = FieldDockCap.Schedule();
                Check(run.IndexOf("정산", StringComparison.Ordinal) >= 0
                      && run.IndexOf("사망 없음", StringComparison.Ordinal) >= 0
                      && FieldDockCap.CaptionFits(run),
                    $"일정 중 (실제 {run})");
                HuntSchedule.Stop();
            }

            Environment.SetEnvironmentVariable(FieldDockCap.EnvNo, "1");
            Check(FieldDockCap.Blocked, "QA_NO");
            Check(FieldDockCap.LowHp() == FieldDockCap.OldLowHp
                  && !FieldDockCap.CaptionFits(FieldDockCap.LowHp()),
                $"QA_NO 저체력 옛 긴 줄 (실제 {FieldDockCap.LowHp()})");
            Check(FieldDockCap.Schedule() == FieldDockCap.OldSchedule
                  && !FieldDockCap.CaptionFits(FieldDockCap.Schedule()),
                $"QA_NO 일정 옛 긴 줄 (실제 {FieldDockCap.Schedule()})");
            Check(FieldDockCap.Death() == FieldDockCap.OldDeath
                  && !FieldDockCap.CaptionFits(FieldDockCap.Death()),
                $"QA_NO 사망없음 옛 긴 줄 (실제 {FieldDockCap.Death()})");
            FieldBoss.ForceSpawnForTest(0);
            Check(FieldDockCap.Boss() == FieldDockCap.OldBoss()
                  && !FieldDockCap.CaptionFits(FieldDockCap.Boss()),
                $"QA_NO 배회 보스 옛 긴 줄 (실제 {FieldDockCap.Boss()})");
            FieldBoss.ResetForTest();
            Check(FieldDockCap.Line().IndexOf("두 줄", StringComparison.Ordinal) >= 0,
                $"QA_NO 줄 (실제 {FieldDockCap.Line()})");
            Check(FieldDockCap.BossLine().IndexOf("두 줄", StringComparison.Ordinal) >= 0,
                $"QA_NO 배회 줄 (실제 {FieldDockCap.BossLine()})");
            Environment.SetEnvironmentVariable(FieldDockCap.EnvNo, null);

            FieldDockCap.ResetForTest();
            Environment.SetEnvironmentVariable(FieldDockCap.EnvShow, "1");
            RaidSpawn.ForceSpawnForTest(1);
            Check(RaidSpawn.Active, "시드 전 레이드");
            FieldDockCap.SeedQaIfRequested();
            Check(FieldDockCap.ShowQa, "시드 ShowQa");
            Check(!RaidSpawn.Active, "시드가 레이드를 걷어 도크를 연다");
            Check(!FieldBoss.Active, "시드가 배회 보스를 걷는다");
            Check(!HuntSchedule.Running, "시드는 일정 꺼짐 — 짧은 부제");
            Check(FieldDockCap.Line().IndexOf("한 줄", StringComparison.Ordinal) >= 0,
                $"시드 자막 (실제 {FieldDockCap.Line()})");
            Environment.SetEnvironmentVariable(FieldDockCap.EnvShow, null);

            FieldDockCap.ResetForTest();
            FieldBoss.ResetForTest();
            Environment.SetEnvironmentVariable(FieldDockCap.EnvBoss, "1");
            Check(FieldDockCap.ShowBossQa, "배회 시드 ShowBossQa");
            FieldDockCap.SeedBossQaIfRequested();
            Check(FieldBoss.Active, "배회 시드가 보스를 띄운다");
            Check(FieldDockCap.Boss() == "재의 야수 · 환생석 없음",
                $"배회 시드 부제 (실제 {FieldDockCap.Boss()})");
            Check(FieldDockCap.BossLine().IndexOf("한 줄", StringComparison.Ordinal) >= 0,
                $"배회 시드 자막 (실제 {FieldDockCap.BossLine()})");
            Environment.SetEnvironmentVariable(FieldDockCap.EnvBoss, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string fieldSrc = File.ReadAllText(Path.Combine(runtime, "FieldScreen.cs"));
            Check(fieldSrc.IndexOf("FieldDockCap.SeedQaIfRequested", StringComparison.Ordinal) >= 0
                  && fieldSrc.IndexOf("FieldDockCap.Line", StringComparison.Ordinal) >= 0
                  && fieldSrc.IndexOf("FieldDockCap.LowHp", StringComparison.Ordinal) >= 0
                  && fieldSrc.IndexOf("FieldDockCap.Schedule", StringComparison.Ordinal) >= 0
                  && fieldSrc.IndexOf("FieldDockCap.Death", StringComparison.Ordinal) >= 0,
                "필드가 시드·줄·LowHp·Schedule·Death를 읽는다");
            Check(fieldSrc.IndexOf("FieldDockCap.Boss", StringComparison.Ordinal) >= 0
                  && fieldSrc.IndexOf("FieldDockCap.BossLine", StringComparison.Ordinal) >= 0
                  && fieldSrc.IndexOf("FieldDockCap.SeedBossQaIfRequested", StringComparison.Ordinal) >= 0,
                "필드가 Boss·BossLine·시드를 읽는다");
            Check(fieldSrc.IndexOf(FieldDockCap.OldLowHp, StringComparison.Ordinal) < 0
                  && fieldSrc.IndexOf("편성을 보내 두면", StringComparison.Ordinal) < 0
                  && fieldSrc.IndexOf("카운트를 안 올린다", StringComparison.Ordinal) < 0,
                "도크가 옛 긴 줄을 안 붙인다");
            Check(fieldSrc.IndexOf("HuntSchedule.CardBody", StringComparison.Ordinal) < 0,
                "도크가 긴 CardBody를 안 읽는다");
            Check(fieldSrc.IndexOf("FieldBoss.CardBody", StringComparison.Ordinal) < 0,
                "도크가 긴 FieldBoss.CardBody를 안 읽는다");

            _ = nameof(FieldDockCap.LowHp);
            _ = nameof(FieldDockCap.Schedule);
            _ = nameof(FieldDockCap.Death);
            _ = nameof(FieldDockCap.Boss);
            _ = nameof(FieldDockCap.Line);
            _ = nameof(FieldDockCap.BossLine);
            _ = nameof(FieldDockCap.SeedQaIfRequested);
            _ = nameof(FieldDockCap.SeedBossQaIfRequested);

            Environment.SetEnvironmentVariable(FieldDockCap.EnvShow, show);
            Environment.SetEnvironmentVariable(FieldDockCap.EnvBoss, boss);
            Environment.SetEnvironmentVariable(FieldDockCap.EnvNo, no);
            FieldDockCap.ResetForTest();
            HuntSchedule.ResetForTest();
            FieldBoss.ResetForTest();
            GameState.ResetAll();

            if (_fail == 0) Debug.Log("[FieldDockCapSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[FieldDockCapSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[FieldDockCapSelfCheck] FAIL {_fail}건");
        }
    }
}
