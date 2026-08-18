using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>별 인식은 RaceDef.인식범위배율을 읽는다. 엘프 +20% · 나머지 100%(§3·§18-9).</summary>
    public static class RaceSenseSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.01f;

        [MenuItem("Ashes to Stars/QA/Race Sense Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(WorldStar.EnvShowSense);
            string no = Environment.GetEnvironmentVariable(WorldStar.EnvNoSense);
            string noRange = Environment.GetEnvironmentVariable(WorldStar.EnvNoRange);
            RaceId oldRace = RacePrefs.Get();
            float oldForce = WorldStar.ForceRaceSenseMul;
            int oldFloor = GameState.TowerFloor;
            Environment.SetEnvironmentVariable(WorldStar.EnvShowSense, null);
            Environment.SetEnvironmentVariable(WorldStar.EnvNoSense, null);
            Environment.SetEnvironmentVariable(WorldStar.EnvNoRange, null);
            WorldStar.ForceRaceSenseMul = 0f;

            GameState.ResetAll();
            WorldStar.ResetForTest();
            GameState.SetTowerFloorForTest(30);

            float base30 = WorldStar.SenseBase(30);
            Check(Mathf.Approximately(base30, WorldStar.SenseMul(30)),
                $"30층 기준은 SenseMul ({base30:0.000})");
            Check(base30 > WorldStar.SenseMul(1) && base30 < WorldStar.SenseMul(100),
                $"30층 기준 영공은 중간 ({base30:0.000})");

            RacePrefs.Set(RaceId.인간);
            Check(WorldStar.RaceSensePercent() == WorldStar.HumanSensePercent,
                $"인간 인식 100 (실제 {WorldStar.RaceSensePercent()})");
            Check(Near(WorldStar.Sense(30), base30),
                $"인간은 기준값 (실제 {WorldStar.Sense(30):0.000})");
            Check(Near(WorldStar.ApplyRaceSense(10f), 10f), "인간 10 유지");
            Check(WorldStar.RaceSenseLine().Contains("없음"),
                $"인간 문구는 배율 없음 (실제 {WorldStar.RaceSenseLine()})");
            Check(WorldStar.SizeLabel(30).Contains("4.0"),
                $"인간 라벨 영공 4.0 (실제 {WorldStar.SizeLabel(30)})");

            RacePrefs.Set(RaceId.드워프);
            Check(WorldStar.RaceSensePercent() == WorldStar.HumanSensePercent
                    && Near(WorldStar.Sense(30), base30),
                $"드워프도 기준값 ({WorldStar.Sense(30):0.000})");

            RacePrefs.Set(RaceId.수인);
            Check(WorldStar.RaceSensePercent() == WorldStar.HumanSensePercent
                    && Near(WorldStar.Sense(30), base30),
                $"수인도 100 (실제 {WorldStar.Sense(30):0.000})");

            RacePrefs.Set(RaceId.엘프);
            float elf30 = WorldStar.Sense(30);
            Check(WorldStar.RaceSensePercent() == WorldStar.ElfSensePercent,
                $"엘프 인식 120 (실제 {WorldStar.RaceSensePercent()})");
            Check(Near(elf30, base30 * WorldStar.ElfSensePercent / 100f),
                $"엘프가 같은 층 인간의 120% (인간 {base30:0.000} / 엘프 {elf30:0.000})");
            Check(Near(WorldStar.ApplyRaceSense(10f), 12f), "엘프 10→12");
            Check(WorldStar.RaceSenseLine().Contains("+20%"),
                $"엘프 문구 +20% (실제 {WorldStar.RaceSenseLine()})");
            Check(WorldStar.SizeLabel(30).Contains("4.8"),
                $"엘프 라벨 영공 4.8 (실제 {WorldStar.SizeLabel(30)})");
            Check(Near(WorldStar.Sense(1), WorldStar.SenseMul(1) * 1.2f),
                $"1층 엘프 {WorldStar.Sense(1):0.00} = {WorldStar.SenseMul(1) * 1.2f:0.00}");
            Check(Near(WorldStar.Sense(100), WorldStar.SenseMul(100) * 1.2f),
                $"100층 엘프 {WorldStar.Sense(100):0.00} = {WorldStar.SenseMul(100) * 1.2f:0.00}");

            RacePrefs.Set(RaceId.엘프);
            Environment.SetEnvironmentVariable(WorldStar.EnvNoSense, "1");
            Check(WorldStar.RaceSensePercent() == WorldStar.HumanSensePercent,
                "QA_NO_RACE_SENSE이면 엘프도 100");
            Check(Near(WorldStar.Sense(30), base30), "차단하면 엘프=인간");
            Environment.SetEnvironmentVariable(WorldStar.EnvNoSense, null);

            RacePrefs.Set(RaceId.엘프);
            Check(Near(WorldStar.Sense(30), elf30), "재기동 뒤에도 엘프 120");

            Environment.SetEnvironmentVariable(WorldStar.EnvShowSense, "1");
            WorldStar.ResetForTest();
            WorldStar.SeedRaceSenseQaIfRequested();
            Check(RacePrefs.Get() == RaceId.엘프, "시드는 엘프를 고른다");
            Check(GameState.TowerFloor >= 30, $"시드는 30층 (실제 {GameState.TowerFloor})");
            Check(WorldStar.RaceSenseLine().Contains("+20%"), "시드 화면 문구 +20%");
            Check(WorldStar.SizeLabel(GameState.TowerFloor).Contains("4.8"),
                $"시드 라벨 영공 4.8 (실제 {WorldStar.SizeLabel(GameState.TowerFloor)})");
            Environment.SetEnvironmentVariable(WorldStar.EnvShowSense, null);

            _ = nameof(WorldStar.RaceSensePercent);
            _ = nameof(WorldStar.ApplyRaceSense);
            _ = nameof(WorldStar.RaceSenseLine);
            _ = nameof(WorldStar.SenseBase);
            _ = nameof(WorldStar.SeedRaceSenseQaIfRequested);
            _ = nameof(RaceDef.인식범위배율);

            Environment.SetEnvironmentVariable(WorldStar.EnvShowSense, show);
            Environment.SetEnvironmentVariable(WorldStar.EnvNoSense, no);
            Environment.SetEnvironmentVariable(WorldStar.EnvNoRange, noRange);
            WorldStar.ForceRaceSenseMul = oldForce;
            RacePrefs.Set(oldRace);
            WorldStar.ResetForTest();
            if (oldFloor > 0) GameState.SetTowerFloorForTest(oldFloor);
            else GameState.ResetAll();

            if (_fail > 0)
            {
                Debug.LogError("[RaceSenseSelfCheck] FAIL\n" + _log);
                throw new InvalidOperationException("RaceSenseSelfCheck FAIL " + _fail);
            }
            Debug.Log("[RaceSenseSelfCheck] PASS\n" + _log);
        }
    }
}
