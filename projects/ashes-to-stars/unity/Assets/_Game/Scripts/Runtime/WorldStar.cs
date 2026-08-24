using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 월드맵 내 별(§14 ✅). 크기는 층에 따라 커지고, 인식 범위도 같이 넓어진다.
    /// 크기 배율은 §18-13 `1 + 돌파층 × 0.02`(100층 = 3배). SizePx가 SizeMul을 읽는다.
    /// 영공 반경은 §18-13 `1 + 돌파층/10`(100층 = 11). SenseBase가 SenseMul을 읽는다.
    /// 옛 길은 40~112·4~16 선형이라 공식이 소비처 0곳이었다. QA_NO면 옛 선형.
    /// 영공 버프/디버프는 영지에서 켠다. 10층 모양 연출은 💡라 안 넣는다.
    /// 엘프는 같은 층에서 영공이 120%(§18-9). 탐험 범위 +30%는 WorldExplore가 읽는다.
    /// 적 디버프는 침략 약탈이 95%로 읽는다(§14). 아군 버프는 광산이 이미 읽는다.
    /// </summary>
    public static class WorldStar
    {
        public const int MaxFloor = 100;
        public const float MinPx = 40f;
        public const float MaxPx = 112f;
        public const float SizePerFloor = 0.02f;
        public const float SensePerFloor = 0.1f;
        public const float PlateH = 72f;
        public const float MinSense = 4f;
        public const float MaxSense = 16f;
        public const float AllyBuffMul = 1.05f;
        public const float EnemyDebuffMul = 0.95f;
        public const int EnemyDebuffPercent = 95;
        public const string EnvShow = "QA_STAR_SIZE";
        public const string EnvNo = "QA_NO_STAR_SIZE";
        public const string EnvShowRange = "QA_STAR_SENSE";
        public const string EnvNoRange = "QA_NO_STAR_SENSE";
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
        static bool _rangeQaSeeded;
        static bool _debuffQaSeeded;
        static bool _sizeQaSeeded;

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool ShowQa
        {
            get
            {
                if (Blocked) return false;
                string raw = Environment.GetEnvironmentVariable(EnvShow);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool RangeBlocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNoRange);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool ShowRangeQa
        {
            get
            {
                if (RangeBlocked) return false;
                string raw = Environment.GetEnvironmentVariable(EnvShowRange);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static int ClampFloor(int floor) => Mathf.Clamp(floor, 1, MaxFloor);

        /// <summary>옛 40~112 선형. QA_NO SizePx가 이 길을 쓴다.</summary>
        public static float OldSizePx(int floor)
        {
            int f = ClampFloor(floor);
            return MinPx + (MaxPx - MinPx) * (f - 1) / (MaxFloor - 1);
        }

        /// <summary>§18-13 `1 + 돌파층 × 0.02`. 100층 = 3. QA_NO면 옛 선형 비.</summary>
        public static float SizeMul(int floor)
        {
            int f = ClampFloor(floor);
            if (Blocked) return OldSizePx(f) / MinPx;
            return 1f + f * SizePerFloor;
        }

        /// <summary>표시 픽셀. SizeMul을 읽는다. 1층 ≈40.8 · 100층 = 120.</summary>
        public static float SizePx(int floor) => MinPx * SizeMul(floor);

        public static string SizeLine(int floor)
        {
            if (Blocked) return "별 크기는 옛 선형";
            return $"별 크기 ×{SizeMul(floor):0.00}(§18-13)";
        }

        public static string SizeLine() => SizeLine(Mathf.Max(1, GameState.TowerFloor));

        /// <summary>옛 4~16 선형. QA_NO SenseBase가 이 길을 쓴다.</summary>
        public static float OldSenseBase(int floor)
        {
            int f = ClampFloor(floor);
            return MinSense + (MaxSense - MinSense) * (f - 1) / (MaxFloor - 1);
        }

        /// <summary>§18-13 `1 + 돌파층/10`. 100층 = 11. QA_NO면 옛 선형.</summary>
        public static float SenseMul(int floor)
        {
            int f = ClampFloor(floor);
            if (RangeBlocked) return OldSenseBase(f);
            return 1f + f * SensePerFloor;
        }

        /// <summary>층만 본 기준 영공. SenseMul을 읽는다. 종족 배율은 `Sense`가 곱한다.</summary>
        public static float SenseBase(int floor) => SenseMul(floor);

        public static string SenseLine(int floor)
        {
            if (RangeBlocked) return "영공은 옛 선형";
            return $"영공 {SenseMul(floor):0.00}(§18-13)";
        }

        public static string SenseLine() => SenseLine(Mathf.Max(1, GameState.TowerFloor));

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

        public static string SizeLabel(int floor)
        {
            if (Blocked)
                return $"{ClampFloor(floor)}층 · 별 {SizePx(floor):0}px · 영공 {Sense(floor):0.0}";
            return $"{ClampFloor(floor)}층 · 별 ×{SizeMul(floor):0.00} · 영공 {Sense(floor):0.0}";
        }

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

        /// <summary>시각 QA. QA_STAR_SIZE=1이면 100층+자막.</summary>
        public static void SeedQaIfRequested()
        {
            if (!ShowQa) return;
            if (_sizeQaSeeded) return;
            _sizeQaSeeded = true;
            GameState.SetTowerFloorForTest(MaxFloor);
        }

        /// <summary>시각 QA. QA_STAR_SENSE=1이면 100층+영공 자막.</summary>
        public static void SeedRangeQaIfRequested()
        {
            if (!ShowRangeQa) return;
            if (_rangeQaSeeded) return;
            _rangeQaSeeded = true;
            GameState.SetTowerFloorForTest(MaxFloor);
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
            _rangeQaSeeded = false;
            _debuffQaSeeded = false;
            _sizeQaSeeded = false;
            ForceRaceSenseMul = 0f;
        }

        public static Rect Plate(Rect body) =>
            new Rect(body.x, body.y, body.width, PlateH);

        public static Rect Icon(Rect plate, int floor)
        {
            var inner = UiAtlas.ContentRect(plate, "panel", 2f);
            float s = SizePx(floor);
            return new Rect(inner.x, plate.y + (plate.height - s) * 0.5f, s, s);
        }

        public static Rect Caption(Rect plate, Rect icon)
        {
            var inner = UiAtlas.ContentRect(plate, "panel", 2f);
            float x = Mathf.Max(inner.x, icon.xMax + 12f);
            return new Rect(x, inner.y, Mathf.Max(40f, inner.xMax - x), inner.height);
        }

        public static Rect AfterPlate(Rect body) =>
            new Rect(body.x, body.y + PlateH + 12f, body.width,
                Mathf.Max(40f, body.height - PlateH - 12f));
    }
}
