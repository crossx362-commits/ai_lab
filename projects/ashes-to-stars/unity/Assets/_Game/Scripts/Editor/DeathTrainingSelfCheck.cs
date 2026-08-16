using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>첫 5층 레이드 전 PvE는 비살상. 동의 뒤·6층부터는 목숨이 깎인다(§온보딩).</summary>
    public static class DeathTrainingSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Death Training Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(DeathTraining.EnvShow);
            string no = Environment.GetEnvironmentVariable(DeathTraining.EnvNo);
            Environment.SetEnvironmentVariable(DeathTraining.EnvShow, null);
            Environment.SetEnvironmentVariable(DeathTraining.EnvNo, null);
            Environment.SetEnvironmentVariable("QA_V4_WIPE", null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            DeathTraining.ResetForTest();

            Check(DeathTraining.RaidFloor == 5, "첫 레이드는 5층");
            Check(GameState.TowerFloor == 1, $"새 게임 1층 (실제 {GameState.TowerFloor})");
            Check(DeathTraining.IsTraining, "1층은 비살상 훈련");
            Check(!DeathTraining.Consented, "시작 때 미동의");
            Check(DeathTraining.NeedsConsent(5), "5층 입장은 동의가 필요하다");
            Check(!DeathTraining.NeedsConsent(4), "4층은 동의 없이 들어간다");
            Check(DeathTraining.CanEnterFloor(4), "4층 CanEnter");
            Check(!DeathTraining.CanEnterFloor(5), "5층 CanEnter 거부");
            Check(DeathTraining.Line().Contains("비살상 훈련")
                  && DeathTraining.Line().Contains("§4"),
                $"훈련 문구 (실제 {DeathTraining.Line()})");
            Check(DeathTraining.ConsentTitle().Contains("영구 사망"),
                $"동의 제목 (실제 {DeathTraining.ConsentTitle()})");
            Check(DeathTraining.ConsentBody().Contains("3번")
                  && DeathTraining.ConsentBody().Contains("10층"),
                $"동의 본문 3번·10층 (실제 {DeathTraining.ConsentBody()})");

            var roster = LifeSystem.GetCharacters();
            Check(roster.Count > 0, "로스터가 있다");
            int deathsBefore = roster[0].DeathCount;
            var report = GameFlow.ApplyPveDefeat();
            Check(report.TrainingReturn, "훈련 패배는 TrainingReturn");
            Check(report.FallenNames.Count == 0, "훈련은 [사망]이 없다");
            Check(report.DeletedNames.Count == 0, "훈련은 삭제 없다");
            Check(roster[0].DeathCount == deathsBefore,
                $"훈련 패배 목숨 불변 (실제 {roster[0].DeathCount})");
            Check(!roster[0].IsDeleted, "훈련 패배는 삭제 아님");
            Check(report.ReturnedNames.Count > 0, "귀환 이름이 있다");
            string summary = GameFlow.FormatDefeatSummary("전멸", report);
            Check(summary.Contains("HP 1 귀환") && summary.Contains("목숨은 그대로"),
                $"요약 귀환 (실제 {summary})");
            Check(!summary.Contains("[사망]"), "요약에 [사망] 없음");

            var wipe = LifeSystem.ApplyWipe(PartyState.SortieRecords());
            Check(!wipe.TrainingReturn, "ApplyWipe는 살상(V4 경로)");
            Check(wipe.FallenNames.Count > 0, "ApplyWipe는 사망을 남긴다");
            Check(roster[0].DeathCount == deathsBefore + 1,
                $"ApplyWipe 목숨 +1 (실제 {roster[0].DeathCount})");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            DeathTraining.ResetForTest();
            Check(DeathTraining.Consent(), "동의");
            Check(DeathTraining.Consented, "동의 뒤 Consented");
            Check(!DeathTraining.IsTraining, "동의 뒤 훈련 종료");
            Check(!DeathTraining.NeedsConsent(5), "동의 뒤 5층 문 열림");
            Check(string.IsNullOrEmpty(DeathTraining.Line()), "동의 뒤 훈련 문구 없음");
            Check(!DeathTraining.Consent(), "두 번째 동의는 false");

            DeathTraining.ForgetInMemoryForTest();
            Check(DeathTraining.Consented, "재기동 뒤에도 동의");
            Check(!DeathTraining.IsTraining, "재기동 뒤에도 살상");

            int afterConsent = LifeSystem.GetCharacters()[0].DeathCount;
            var lethal = GameFlow.ApplyPveDefeat();
            Check(!lethal.TrainingReturn, "동의 뒤 ApplyPveDefeat는 살상");
            Check(lethal.FallenNames.Count > 0, "동의 뒤 [사망]");
            Check(LifeSystem.GetCharacters()[0].DeathCount == afterConsent + 1,
                $"동의 뒤 목숨 +1 (실제 {LifeSystem.GetCharacters()[0].DeathCount})");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            DeathTraining.ResetForTest();
            GameState.SetTowerFloorForTest(6);
            Check(!DeathTraining.IsTraining, "6층은 동의 없어도 살상 — 10층 면제 없음");
            Check(!DeathTraining.NeedsConsent(5), "6층에선 5층 재입장 동의 없음");
            int atSix = LifeSystem.GetCharacters()[0].DeathCount;
            var six = GameFlow.ApplyPveDefeat();
            Check(!six.TrainingReturn, "6층 패배는 살상");
            Check(LifeSystem.GetCharacters()[0].DeathCount == atSix + 1,
                $"6층 목숨 +1 (실제 {LifeSystem.GetCharacters()[0].DeathCount})");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            DeathTraining.ResetForTest();
            GameState.SetTowerFloorForTest(10);
            Check(!DeathTraining.IsTraining, "10층 장기 면제 없음");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            DeathTraining.ResetForTest();
            Environment.SetEnvironmentVariable(DeathTraining.EnvNo, "1");
            Check(DeathTraining.Blocked, "QA_NO면 차단");
            Check(!DeathTraining.IsTraining, "차단하면 훈련 아님");
            Check(!DeathTraining.NeedsConsent(5), "차단하면 동의 생략");
            int blockedDeaths = LifeSystem.GetCharacters()[0].DeathCount;
            var blocked = GameFlow.ApplyPveDefeat();
            Check(!blocked.TrainingReturn, "차단하면 살상");
            Check(LifeSystem.GetCharacters()[0].DeathCount == blockedDeaths + 1,
                $"차단하면 목숨 +1 (실제 {LifeSystem.GetCharacters()[0].DeathCount})");
            Environment.SetEnvironmentVariable(DeathTraining.EnvNo, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            DeathTraining.ResetForTest();
            var pvp = GameFlow.ApplyPveDefeat(isPvp: true);
            Check(!pvp.TrainingReturn, "PvP는 훈련이 아니다");
            Check(LifeSystem.GetCharacters()[0].DeathCount == 0, "PvP는 목숨 0");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            DeathTraining.ResetForTest();
            Environment.SetEnvironmentVariable(DeathTraining.EnvShow, "1");
            DeathTraining.SeedQaIfRequested();
            Check(GameState.TowerFloor == 5, $"시드 5층 (실제 {GameState.TowerFloor})");
            Check(DeathTraining.IsTraining, "시드는 훈련");
            Check(DeathTraining.QaPromptConsent, "시드는 동의 화면");
            Check(GameFlow.LastDefeatReport != null && GameFlow.LastDefeatReport.TrainingReturn,
                "시드 결과도 귀환");
            Check(GameFlow.LastBattleSummary.Contains("HP 1 귀환"),
                $"시드 요약 (실제 {GameFlow.LastBattleSummary})");
            Environment.SetEnvironmentVariable(DeathTraining.EnvShow, null);

            _ = nameof(DeathTraining.IsTraining);
            _ = nameof(DeathTraining.NeedsConsent);
            _ = nameof(DeathTraining.ApplyReturn);
            _ = nameof(DeathTraining.Consent);
            _ = nameof(DeathTraining.SeedQaIfRequested);
            _ = nameof(GameFlow.ApplyPveDefeat);

            Environment.SetEnvironmentVariable(DeathTraining.EnvShow, show);
            Environment.SetEnvironmentVariable(DeathTraining.EnvNo, no);
            DeathTraining.ResetForTest();
            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();

            if (_fail > 0)
            {
                Debug.LogError("[DeathTrainingSelfCheck] FAIL\n" + _log);
                throw new InvalidOperationException("DeathTrainingSelfCheck FAIL " + _fail);
            }
            Debug.Log("[DeathTrainingSelfCheck] PASS\n" + _log);
        }
    }
}
