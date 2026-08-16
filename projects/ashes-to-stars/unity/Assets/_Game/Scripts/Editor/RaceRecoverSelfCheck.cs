using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>PvE 사망 회복은 RaceDef.회복시간을 읽는다. 인간 18h · 나머지 24h(§3·§18-8).</summary>
    public static class RaceRecoverSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Race Recover Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(LifeSystem.EnvShowRaceRecover);
            string no = Environment.GetEnvironmentVariable(LifeSystem.EnvNoRaceRecover);
            RaceId oldRace = RacePrefs.Get();
            float oldForce = LifeSystem.ForcePveRecoverHours;
            Environment.SetEnvironmentVariable(LifeSystem.EnvShowRaceRecover, null);
            Environment.SetEnvironmentVariable(LifeSystem.EnvNoRaceRecover, null);
            Environment.SetEnvironmentVariable(LifeSystem.EnvNoRecover, null);
            LifeSystem.ForcePveRecoverHours = 0f;

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            DefenseState.ResetForTest();

            long t0 = 1_700_000_000L;
            LifeSystem.NowUnix = () => t0;

            RacePrefs.Set(RaceId.인간);
            Check(LifeSystem.PveRecoverSeconds() == LifeSystem.HumanPveRecoverSeconds,
                $"인간 PvE 회복 64800초 (실제 {LifeSystem.PveRecoverSeconds()})");
            Check(LifeSystem.PveRecoverSeconds() != LifeSystem.PvpRecoverSeconds(),
                "PvE 18시간과 PvP 12시간은 다른 시계다");

            var roster = LifeSystem.GetCharacters();
            Check(roster.Count >= 2, $"로스터 2명 이상 (실제 {roster.Count})");
            var human = roster[0];
            LifeSystem.RegisterDeath(human);
            Check(human.DeathCount == 1 && !human.IsDeleted, "PvE 1회는 목숨만 깎는다");
            Check(!LifeSystem.IsAvailable(human), "사망 직후 출전 불가");
            Check(LifeSystem.GetRecoveryTimeRemaining(human) == (int)LifeSystem.HumanPveRecoverSeconds,
                $"인간 남은 초 = 18시간 (실제 {LifeSystem.GetRecoveryTimeRemaining(human)})");
            Check(LifeSystem.FormatRecoveryPhrase(LifeSystem.GetRecoveryTimeRemaining(human))
                    .Contains("18시간"),
                $"문구 18시간 (실제 {LifeSystem.FormatRecoveryPhrase(LifeSystem.GetRecoveryTimeRemaining(human))})");

            LifeSystem.NowUnix = () => t0 + LifeSystem.HumanPveRecoverSeconds - 1;
            Check(!LifeSystem.IsAvailable(human), "만료 1초 전은 아직 출전 불가");
            LifeSystem.NowUnix = () => t0 + LifeSystem.HumanPveRecoverSeconds + 1;
            Check(LifeSystem.IsAvailable(human), "18시간 1초 뒤 출전 가능");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            RacePrefs.Set(RaceId.엘프);
            LifeSystem.NowUnix = () => t0;
            Check(LifeSystem.PveRecoverSeconds() == LifeSystem.DefaultPveRecoverSeconds,
                $"엘프 PvE 회복 86400초 (실제 {LifeSystem.PveRecoverSeconds()})");
            var elf = LifeSystem.GetCharacters()[0];
            LifeSystem.RegisterDeath(elf);
            Check(LifeSystem.GetRecoveryTimeRemaining(elf) == (int)LifeSystem.DefaultPveRecoverSeconds,
                $"엘프 남은 초 = 24시간 (실제 {LifeSystem.GetRecoveryTimeRemaining(elf)})");
            Check(LifeSystem.FormatRecoveryPhrase(LifeSystem.GetRecoveryTimeRemaining(elf))
                    .Contains("24시간"),
                $"엘프 문구 24시간 (실제 {LifeSystem.FormatRecoveryPhrase(LifeSystem.GetRecoveryTimeRemaining(elf))})");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            RacePrefs.Set(RaceId.인간);
            LifeSystem.NowUnix = () => t0;
            var pvp = LifeSystem.GetCharacters()[0];
            LifeSystem.RegisterDeath(pvp, isPvp: true);
            Check(pvp.DeathCount == 0 && !pvp.IsDeleted, "인간 PvP는 목숨을 안 깎는다");
            Check(LifeSystem.GetRecoveryTimeRemaining(pvp) == (int)LifeSystem.PvpRecoverSeconds(),
                $"인간 PvP도 12시간 — 9시간은 보호막과 어긋나 안 넣음 (실제 {LifeSystem.GetRecoveryTimeRemaining(pvp)})");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            RacePrefs.Set(RaceId.인간);
            Environment.SetEnvironmentVariable(LifeSystem.EnvNoRaceRecover, "1");
            LifeSystem.NowUnix = () => t0;
            Check(LifeSystem.PveRecoverSeconds() == LifeSystem.DefaultPveRecoverSeconds,
                "QA_NO_RACE_RECOVER면 인간도 24시간");
            var blocked = LifeSystem.GetCharacters()[0];
            LifeSystem.RegisterDeath(blocked);
            Check(LifeSystem.GetRecoveryTimeRemaining(blocked) == (int)LifeSystem.DefaultPveRecoverSeconds,
                "차단하면 86400초를 건다");
            Environment.SetEnvironmentVariable(LifeSystem.EnvNoRaceRecover, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            LifeSystem.NowUnix = () => t0;
            Environment.SetEnvironmentVariable(LifeSystem.EnvShowRaceRecover, "1");
            LifeSystem.SeedRaceRecoverQaIfRequested();
            var seeded = LifeSystem.GetCharacters()[0];
            Check(RacePrefs.Get() == RaceId.인간, "시드는 인간을 고른다");
            Check(seeded.DeathCount >= 1 && !LifeSystem.IsAvailable(seeded),
                "시드가 PvE 사망 1회·출전 불가를 건다");
            Check(LifeSystem.GetRecoveryTimeRemaining(seeded) == (int)LifeSystem.HumanPveRecoverSeconds,
                "시드 남은 초 = 18시간");
            Check(LifeSystem.FormatRecoveryPhrase(LifeSystem.GetRecoveryTimeRemaining(seeded))
                    .Contains("18시간"),
                "시드 화면 문구 18시간");
            Check(!DefenseState.Contains(0),
                "시드는 수비를 비운다 — PvE 18시간과 수비대 12시간을 가른다");
            Environment.SetEnvironmentVariable(LifeSystem.EnvShowRaceRecover, null);

            LifeSystem.ForgetInMemoryForTest();
            LifeSystem.NowUnix = () => t0 + 10;
            seeded = LifeSystem.GetCharacters()[0];
            Check(!LifeSystem.IsAvailable(seeded), "재기동 뒤에도 회복이 남는다");
            Check(LifeSystem.GetRecoveryTimeRemaining(seeded) == (int)LifeSystem.HumanPveRecoverSeconds - 10,
                "재기동 뒤에도 18시간에서 흐른다");

            _ = nameof(LifeSystem.PveRecoverSeconds);
            _ = nameof(LifeSystem.SeedRaceRecoverQaIfRequested);
            _ = nameof(LifeSystem.HumanPveRecoverSeconds);
            _ = nameof(RaceDef.회복시간);

            Environment.SetEnvironmentVariable(LifeSystem.EnvShowRaceRecover, show);
            Environment.SetEnvironmentVariable(LifeSystem.EnvNoRaceRecover, no);
            LifeSystem.ForcePveRecoverHours = oldForce;
            RacePrefs.Set(oldRace);
            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            DefenseState.ResetForTest();
            LifeSystem.NowUnix = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            if (_fail > 0)
            {
                Debug.LogError("[RaceRecoverSelfCheck] FAIL\n" + _log);
                throw new InvalidOperationException("RaceRecoverSelfCheck FAIL " + _fail);
            }
            Debug.Log("[RaceRecoverSelfCheck] PASS\n" + _log);
        }
    }
}
