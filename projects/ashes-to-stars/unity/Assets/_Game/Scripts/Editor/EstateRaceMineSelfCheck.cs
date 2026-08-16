using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>광산은 RaceDef.영지생산배율을 읽는다. 드워프 +20% · 수인 −20%(§3·§18-9).</summary>
    public static class EstateRaceMineSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Estate Race Mine Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(EstateMine.EnvShowRace);
            string no = Environment.GetEnvironmentVariable(EstateMine.EnvNoRace);
            RaceId oldRace = RacePrefs.Get();
            float oldForce = EstateMine.ForceRaceMul;
            Environment.SetEnvironmentVariable(EstateMine.EnvShowRace, null);
            Environment.SetEnvironmentVariable(EstateMine.EnvNoRace, null);
            Environment.SetEnvironmentVariable("QA_NO_MINE", null);
            Environment.SetEnvironmentVariable("QA_ESTATE_MINE", null);
            EstateMine.ForceRaceMul = 0f;

            GameState.ResetAll();
            WorldStar.ResetForTest();
            EstateMine.ResetForTest();

            long t1 = EstateMine.CopperPerHour();
            Check(t1 == 25L * Economy.COPPER_PER_SILVER,
                $"T1 기준은 25실버/h ({t1})");

            RacePrefs.Set(RaceId.인간);
            Check(EstateMine.RacePercent() == EstateMine.HumanPercent,
                $"인간 생산 100 (실제 {EstateMine.RacePercent()})");
            Check(EstateMine.CopperPerHourEffective() == t1,
                $"인간은 기준값 ({EstateMine.CopperPerHourEffective()})");
            Check(EstateMine.RaceLine().Contains("없음"),
                $"인간 문구는 배율 없음 (실제 {EstateMine.RaceLine()})");

            RacePrefs.Set(RaceId.엘프);
            Check(EstateMine.RacePercent() == EstateMine.HumanPercent
                    && EstateMine.CopperPerHourEffective() == t1,
                $"엘프도 기준값 ({EstateMine.CopperPerHourEffective()})");

            RacePrefs.Set(RaceId.드워프);
            long dwarf = t1 * EstateMine.DwarfPercent / 100;
            Check(EstateMine.RacePercent() == EstateMine.DwarfPercent,
                $"드워프 생산 120 (실제 {EstateMine.RacePercent()})");
            Check(EstateMine.CopperPerHourEffective() == dwarf,
                $"드워프 T1 = 30실버/h ({dwarf}, 실제 {EstateMine.CopperPerHourEffective()})");
            Check(dwarf == 30L * Economy.COPPER_PER_SILVER, "드워프 30실버");
            Check(EstateMine.RaceLine().Contains("+20%"),
                $"드워프 문구 +20% (실제 {EstateMine.RaceLine()})");

            RacePrefs.Set(RaceId.수인);
            long beast = t1 * EstateMine.BeastPercent / 100;
            Check(EstateMine.RacePercent() == EstateMine.BeastPercent,
                $"수인 생산 80 (실제 {EstateMine.RacePercent()})");
            Check(EstateMine.CopperPerHourEffective() == beast,
                $"수인 T1 = 20실버/h ({beast}, 실제 {EstateMine.CopperPerHourEffective()})");
            Check(beast == 20L * Economy.COPPER_PER_SILVER, "수인 20실버");
            Check(EstateMine.RaceLine().Contains("20%"),
                $"수인 문구 20% (실제 {EstateMine.RaceLine()})");

            long now = 1_700_000_000;
            RacePrefs.Set(RaceId.드워프);
            GameState.ResetAll();
            WorldStar.ResetForTest();
            EstateMine.ResetForTest();
            EstateMine.NowUnix = () => now;
            Check(EstateMine.Tick() == 0, "첫 Tick은 기준점만");
            now += 3600;
            long added = EstateMine.Tick();
            Check(added == dwarf && GameState.Wallet.Copper == dwarf,
                $"드워프 1시간이면 30실버 ({added})");

            RacePrefs.Set(RaceId.수인);
            GameState.ResetAll();
            WorldStar.ResetForTest();
            EstateMine.ResetForTest();
            EstateMine.NowUnix = () => now;
            EstateMine.Tick();
            now += 3600;
            Check(EstateMine.Tick() == beast && GameState.Wallet.Copper == beast,
                $"수인 1시간이면 20실버 ({EstateMine.CopperPerHourEffective()})");

            RacePrefs.Set(RaceId.드워프);
            Environment.SetEnvironmentVariable(EstateMine.EnvNoRace, "1");
            Check(EstateMine.RacePercent() == EstateMine.HumanPercent,
                "QA_NO_RACE_MINE이면 드워프도 100");
            Check(EstateMine.CopperPerHourEffective() == t1,
                "차단하면 기준값");
            Environment.SetEnvironmentVariable(EstateMine.EnvNoRace, null);

            RacePrefs.Set(RaceId.드워프);
            WorldStar.ResetForTest();
            WorldStar.AllyBuff = true;
            Check(EstateMine.CopperPerHourEffective() > dwarf,
                $"영공 버프가 드워프 위에 곱한다 ({EstateMine.CopperPerHourEffective()} > {dwarf})");
            WorldStar.ResetForTest();
            Check(EstateMine.CopperPerHourEffective() == dwarf,
                "버프를 끄면 드워프 30실버로 돌아온다");

            RacePrefs.Set(RaceId.드워프);
            GameState.ResetAll();
            WorldStar.ResetForTest();
            EstateMine.ResetForTest();
            EstateMine.ForgetInMemoryForTest();
            Check(EstateMine.CopperPerHourEffective() == dwarf,
                "재기동 뒤에도 드워프 30실버");

            GameState.ResetAll();
            WorldStar.ResetForTest();
            EstateMine.ResetForTest();
            GameState.Earn(100_000);
            Environment.SetEnvironmentVariable(EstateMine.EnvShowRace, "1");
            EstateMine.SeedRaceQaIfRequested();
            Check(RacePrefs.Get() == RaceId.드워프, "시드는 드워프를 고른다");
            Check(GameState.Wallet.Copper == dwarf,
                $"시드는 지갑을 비우고 드워프 1시간분 ({GameState.Wallet.Copper})");
            Check(EstateMine.RaceLine().Contains("+20%"), "시드 화면 문구 +20%");
            Environment.SetEnvironmentVariable(EstateMine.EnvShowRace, null);

            _ = nameof(EstateMine.RacePercent);
            _ = nameof(EstateMine.ApplyRace);
            _ = nameof(EstateMine.RaceLine);
            _ = nameof(EstateMine.SeedRaceQaIfRequested);
            _ = nameof(RaceDef.영지생산배율);

            Environment.SetEnvironmentVariable(EstateMine.EnvShowRace, show);
            Environment.SetEnvironmentVariable(EstateMine.EnvNoRace, no);
            EstateMine.ForceRaceMul = oldForce;
            RacePrefs.Set(oldRace);
            WorldStar.ResetForTest();
            GameState.ResetAll();
            EstateMine.ResetForTest();

            if (_fail > 0)
            {
                Debug.LogError("[EstateRaceMineSelfCheck] FAIL\n" + _log);
                throw new InvalidOperationException("EstateRaceMineSelfCheck FAIL " + _fail);
            }
            Debug.Log("[EstateRaceMineSelfCheck] PASS\n" + _log);
        }
    }
}
