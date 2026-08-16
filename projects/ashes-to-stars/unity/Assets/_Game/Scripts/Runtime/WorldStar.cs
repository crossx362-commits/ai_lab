using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 월드맵 내 별(§14 ✅). 크기는 층에 따라 커지고, 인식 범위도 같이 넓어진다.
    /// 영공 버프/디버프는 영지에서 켠다. 10층 모양 연출은 💡라 안 넣는다.
    /// 엘프는 같은 층에서 영공이 120%(§18-9). 탐험 범위 +30%는 안개 시스템이 없어 안 넣는다.
    /// 적 디버프는 침략 약탈이 95%로 읽는다(§14). 아군 버프는 광산이 이미 읽는다.
    /// </summary>
    public static class WorldStar
    {
        public const int MaxFloor = 100;
        public const float MinPx = 40f;
        public const float MaxPx = 112f;
        public const float PlateH = 72f;
        public const float MinSense = 4f;
        public const float MaxSense = 16f;
        public const float AllyBuffMul = 1.05f;
        public const float EnemyDebuffMul = 0.95f;
        public const int EnemyDebuffPercent = 95;
        public const string EnvShowSense = "QA_RACE_SENSE";
        public const string EnvNoSense = "QA_NO_RACE_SENSE";
        public const string EnvShowDebuff = "QA_AURA_DEBUFF";
        public const string EnvNoDebuff = "QA_NO_AURA_DEBUFF";
        public const int HumanSensePercent = 100;
        public const int ElfSensePercent = 120;

        const string K_ALLY = "ats.star.ally";
        const string K_ENEMY = "ats.star.enemy";

        static bool _loaded;
        static bool _ally;
        static bool _enemy;
        static bool _senseQaSeeded;
        static bool _debuffQaSeeded;

        public static int ClampFloor(int floor) => Mathf.Clamp(floor, 1, MaxFloor);

        public static float SizePx(int floor)
        {
            int f = ClampFloor(floor);
            return MinPx + (MaxPx - MinPx) * (f - 1) / (MaxFloor - 1);
        }

        /// <summary>층만 본 기준 영공. 종족 배율은 `Sense`가 곱한다.</summary>
        public static float SenseBase(int floor)
        {
            int f = ClampFloor(floor);
            return MinSense + (MaxSense - MinSense) * (f - 1) / (MaxFloor - 1);
        }

        /// <summary>SelfCheck가 종족 배율을 고정할 때만. 0이면 RaceDef·계정 종족을 본다.</summary>
        public static float ForceRaceSenseMul;

        public static bool SenseRaceBlocked =>
            Environment.GetEnvironmentVariable(EnvNoSense) == "1";

        /// <summary>§18-9 엘프 별 인식 +20%. 에셋이 없으면 표로 폴백한다.</summary>
        public static int RaceSensePercent()
        {
            if (SenseRaceBlocked) return HumanSensePercent;
            if (ForceRaceSenseMul > 0f) return Math.Max(1, (int)Math.Round(ForceRaceSenseMul * 100f));
            try
            {
                var races = Resources.LoadAll<RaceDef>("races");
                RaceId id = RacePrefs.Get();
                for (int i = 0; i < races.Length; i++)
                {
                    if (races[i] != null && races[i].Id == id && races[i].인식범위배율 > 0f)
                        return Math.Max(1, (int)Math.Round(races[i].인식범위배율 * 100f));
                }
            }
            catch
            {
                // 배치 검사 중 에셋 DB가 비면 표로 간다.
            }
            return RacePrefs.Get() == RaceId.엘프 ? ElfSensePercent : HumanSensePercent;
        }

        public static float ApplyRaceSense(float radius) =>
            radius * RaceSensePercent() / 100f;

        /// <summary>같은 층에서 엘프 영공이 인간/드워프/수인의 120%(§18-9).</summary>
        public static float Sense(int floor) => ApplyRaceSense(SenseBase(floor));

        public static string RaceSenseLine()
        {
            if (RaceSensePercent() == ElfSensePercent && RacePrefs.Get() == RaceId.엘프)
                return "엘프 인식 +20%(§18-9)";
            return "종족 인식 배율 없음";
        }

        public static string SizeLabel(int floor) =>
            $"{ClampFloor(floor)}층 · 별 {SizePx(floor):0}px · 영공 {Sense(floor):0.0}";

        public static bool AllyBuff
        {
            get { Load(); return _ally; }
            set { Load(); _ally = value; Save(); }
        }

        public static bool EnemyDebuff
        {
            get { Load(); return _enemy; }
            set { Load(); _enemy = value; Save(); }
        }

        public static float AllyMul => AllyBuff ? AllyBuffMul : 1f;

        public static bool AuraDebuffBlocked =>
            Environment.GetEnvironmentVariable(EnvNoDebuff) == "1";

        /// <summary>§14 적 디버프. 켜면 침략 약탈이 95%. QA_NO_AURA_DEBUFF면 100.</summary>
        public static int EnemyPercent()
        {
            if (AuraDebuffBlocked) return 100;
            return EnemyDebuff ? EnemyDebuffPercent : 100;
        }

        public static float EnemyMul => EnemyPercent() / 100f;

        public static long ApplyEnemy(long copper) => copper * EnemyPercent() / 100;

        public static string EnemyLine()
        {
            if (EnemyPercent() == EnemyDebuffPercent && EnemyDebuff)
                return "적 디버프 −5%(§14)";
            return "영공 디버프 없음";
        }

        public static string AuraLabel()
        {
            if (AllyBuff && EnemyDebuff) return "아군 버프 · 적 디버프";
            if (AllyBuff) return "아군 버프";
            if (EnemyDebuff) return "적 디버프";
            return "영공 꺼짐";
        }

        static void Load()
        {
            if (_loaded) return;
            _ally = PlayerPrefs.GetInt(K_ALLY, 0) != 0;
            _enemy = PlayerPrefs.GetInt(K_ENEMY, 0) != 0;
            _loaded = true;
        }

        static void Save()
        {
            PlayerPrefs.SetInt(K_ALLY, _ally ? 1 : 0);
            PlayerPrefs.SetInt(K_ENEMY, _enemy ? 1 : 0);
        }

        /// <summary>시각 QA. QA_RACE_SENSE=1이면 엘프·30층으로 영공을 연다.</summary>
        public static void SeedRaceSenseQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable(EnvShowSense) != "1") return;
            if (SenseRaceBlocked) return;
            if (_senseQaSeeded) return;
            _senseQaSeeded = true;
            RacePrefs.Set(RaceId.엘프);
            if (GameState.TowerFloor < 30)
                GameState.SetTowerFloorForTest(30);
        }

        /// <summary>시각 QA. QA_AURA_DEBUFF=1이면 적 디버프·30층·보호막 없음.</summary>
        public static void SeedAuraDebuffQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable(EnvShowDebuff) != "1") return;
            if (AuraDebuffBlocked) return;
            if (_debuffQaSeeded) return;
            _debuffQaSeeded = true;
            Load();
            RacePrefs.Set(RaceId.인간);
            _enemy = true;
            Save();
            if (GameState.TowerFloor < WorldMapScreen.InvasionUnlockFloor)
                GameState.SetTowerFloorForTest(WorldMapScreen.InvasionUnlockFloor);
        }

        public static void ResetForTest()
        {
            PlayerPrefs.DeleteKey(K_ALLY);
            PlayerPrefs.DeleteKey(K_ENEMY);
            _loaded = false;
            _ally = false;
            _enemy = false;
            _senseQaSeeded = false;
            _debuffQaSeeded = false;
            ForceRaceSenseMul = 0f;
        }

        public static Rect Plate(Rect body) =>
            new Rect(body.x, body.y, body.width, PlateH);

        public static Rect Icon(Rect plate, int floor)
        {
            float s = SizePx(floor);
            return new Rect(plate.x + 16f, plate.y + (plate.height - s) * 0.5f, s, s);
        }

        public static Rect Caption(Rect plate, Rect icon) =>
            new Rect(icon.xMax + 16f, plate.y + 28f,
                Mathf.Max(40f, plate.xMax - icon.xMax - 28f), 44f);

        public static Rect AfterPlate(Rect body) =>
            new Rect(body.x, body.y + PlateH + 12f, body.width,
                Mathf.Max(40f, body.height - PlateH - 12f));
    }
}
