using System;
using System.Text;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>수비대 전멸 뒤 12시간 출전 불가 = 침략 보호막과 같은 값(§13-5·§15).</summary>
    public static class DefenseRecoverSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(DefenseState.EnvShow);
            string no = Environment.GetEnvironmentVariable(LifeSystem.EnvNoRecover);
            Environment.SetEnvironmentVariable(DefenseState.EnvShow, null);
            Environment.SetEnvironmentVariable(LifeSystem.EnvNoRecover, null);
            Environment.SetEnvironmentVariable("QA_LOAN_OVERDUE", null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            InvasionState.ResetForTest();
            DefenseState.ResetForTest();
            PartyState.ResetForTest();

            Check(LifeSystem.PvpRecoverSeconds() == InvasionState.GuardSeconds,
                "수비대 회복 초 = 보호막 초 — 한쪽만 바꾸지 않는다");
            Check(LifeSystem.PvpRecoverSeconds() == InvasionState.DefenseRecoverSeconds,
                "LifeSystem이 DefenseRecoverSeconds를 소비한다");

            var roster = LifeSystem.GetCharacters();
            Check(roster.Count >= 2, $"로스터 2명 이상 (실제 {roster.Count})");
            Check(DefenseState.Toggle(0), "0번을 수비에 세운다");
            Check(DefenseState.Contains(0) && !PartyState.Contains(0),
                "수비는 출전에서 빠진다");

            long t0 = 1_700_000_000L;
            LifeSystem.NowUnix = () => t0;
            InvasionState.NowUnix = () => t0;
            Check(DefenseState.ApplyPvpRecover() == 1, "수비 1명이 회복에 들어간다");
            Check(roster[0].DeathCount == 0 && !roster[0].IsDeleted,
                "PvP 회복은 목숨을 안 깎는다");
            Check(!LifeSystem.IsAvailable(roster[0]), "회복 중이라 출전 불가");
            Check(LifeSystem.GetRecoveryTimeRemaining(roster[0]) == (int)InvasionState.GuardSeconds,
                $"남은 초 = 12시간 (실제 {LifeSystem.GetRecoveryTimeRemaining(roster[0])})");
            Check(DefenseState.Contains(0), "회복 중에도 수비 배치는 남는다(§15 무방비 창 방지)");
            Check(!PartyState.Toggle(0), "회복 중인 수비는 출전에 못 넣는다");
            Check(LifeSystem.FormatRecoveryPhrase(LifeSystem.GetRecoveryTimeRemaining(roster[0]))
                    .Contains("12시간"),
                $"문구 12시간 (실제 {LifeSystem.FormatRecoveryPhrase(LifeSystem.GetRecoveryTimeRemaining(roster[0]))})");

            LifeSystem.NowUnix = () => t0 + InvasionState.GuardSeconds - 1;
            Check(!LifeSystem.IsAvailable(roster[0]) && DefenseState.Contains(0),
                "만료 1초 전은 아직 출전 불가·수비 유지");

            LifeSystem.NowUnix = () => t0 + InvasionState.GuardSeconds + 1;
            Check(LifeSystem.IsAvailable(roster[0]), "12시간 1초 뒤 출전 가능");
            Check(DefenseState.Contains(0), "회복이 끝나도 수비 배치는 그대로다");
            Check(!PartyState.Toggle(0), "수비에서 안 내리면 출전은 여전히 거부");

            Check(DefenseState.Toggle(0), "해임");
            Check(PartyState.Toggle(0), "해임 뒤 출전");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            DefenseState.ResetForTest();
            PartyState.ResetForTest();
            roster = LifeSystem.GetCharacters();
            LifeSystem.NowUnix = () => t0;
            var sortieWipe = GameFlow.ApplyPveDefeat(isPvp: true);
            Check(sortieWipe.FallenNames.Count == 0, "침략 패배는 사망 목록이 비어 있다");
            Check(sortieWipe.RecoveredNames.Count > 0, "침략 패배는 출전 전원이 12시간 회복");
            Check(!LifeSystem.IsAvailable(roster[0]), "출전 0번도 회복 중");
            string summary = GameFlow.FormatDefeatSummary("침략 패배", sortieWipe);
            Check(summary.Contains("12시간") && summary.Contains("PvP 회복"),
                $"결과 요약이 12시간을 말한다 (실제 {summary.Replace("\n", " / ")})");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            DefenseState.ResetForTest();
            PartyState.ResetForTest();
            roster = LifeSystem.GetCharacters();
            Check(DefenseState.Toggle(0), "시드 전 수비 배치");
            Environment.SetEnvironmentVariable(LifeSystem.EnvNoRecover, "1");
            LifeSystem.NowUnix = () => t0;
            Check(DefenseState.ApplyPvpRecover() == 0, "QA_NO_DEFENSE_RECOVER면 회복 0");
            Check(LifeSystem.IsAvailable(roster[0]), "QA_NO면 출전 가능");
            Check(roster[0].DeathCount == 0, "QA_NO도 목숨은 안 깎는다");
            Environment.SetEnvironmentVariable(LifeSystem.EnvNoRecover, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            DefenseState.ResetForTest();
            PartyState.ResetForTest();
            LifeSystem.NowUnix = () => t0;
            Environment.SetEnvironmentVariable(DefenseState.EnvShow, "1");
            DefenseState.SeedQaIfRequested();
            roster = LifeSystem.GetCharacters();
            Check(DefenseState.Contains(0), "QA_DEFENSE_RECOVER=1이 수비에 세운다");
            Check(!LifeSystem.IsAvailable(roster[0]), "시드가 12시간 회복을 건다");
            Check(LifeSystem.FormatRecoveryPhrase(LifeSystem.GetRecoveryTimeRemaining(roster[0]))
                    .Contains("12시간"),
                "시드 화면 문구 12시간");
            Environment.SetEnvironmentVariable(DefenseState.EnvShow, null);

            LifeSystem.ForgetInMemoryForTest();
            DefenseState.ForgetInMemoryForTest();
            LifeSystem.NowUnix = () => t0 + 10;
            roster = LifeSystem.GetCharacters();
            Check(!LifeSystem.IsAvailable(roster[0]), "재기동 뒤에도 회복이 남는다");
            Check(DefenseState.Contains(0), "재기동 뒤에도 수비 배치가 남는다");

            var special = roster[1];
            special.IsSpecialJob = true;
            LifeSystem.NowUnix = () => t0;
            LifeSystem.RegisterDeath(special, isPvp: true);
            Check(!special.IsDeleted && special.DeathCount == 0 && !LifeSystem.IsAvailable(special),
                "특수 직업 PvP는 소멸하지 않고 12시간 회복만");

            _ = nameof(DefenseState.ApplyPvpRecover);
            _ = nameof(DefenseState.SeedQaIfRequested);
            _ = nameof(LifeSystem.StartPvpRecovery);
            _ = nameof(LifeSystem.PvpRecoverSeconds);
            _ = nameof(LifeSystem.FormatRecoveryPhrase);

            Environment.SetEnvironmentVariable(DefenseState.EnvShow, show);
            Environment.SetEnvironmentVariable(LifeSystem.EnvNoRecover, no);
            GameState.ResetAll();
            LifeSystem.ResetAll();
            InvasionState.ResetForTest();
            DefenseState.ResetForTest();
            PartyState.ResetForTest();

            if (_fail > 0)
            {
                Debug.LogError("[DefenseRecoverSelfCheck] FAIL\n" + _log);
                throw new InvalidOperationException("DefenseRecoverSelfCheck FAIL " + _fail);
            }
            Debug.Log("[DefenseRecoverSelfCheck] PASS\n" + _log);
        }
    }
}
