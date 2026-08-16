using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 필드 배회 보스(§10-1 ✅). 오픈월드 조우 — 준비 없이 만나면 위험.
    /// 정의는 표에만 있고 필드 허브 소비처가 0곳이었다.
    /// 첫 슬라이스: 필드 카드로 출현 → 보스전. 드랍은 FieldDungeonBoss(환생석 없음).
    /// 탑 층을 올리지 않는다. 배회 스프라이트는 W3Party라 안 넣는다.
    /// 다중 3체·변종 패턴은 💡. QA_NO면 출현 없음.
    /// </summary>
    public static class FieldBoss
    {
        public const string EnvShow = "QA_FIELD_BOSS";
        public const string EnvNo = "QA_NO_FIELD_BOSS";
        public const int LifetimeSec = 20 * 60;
        public const int RollIntervalSec = 15 * 60;
        public const float SpawnChance = 0.5f;
        public const int BaseFloor = 5;
        public const int FloorStep = 10;

        static readonly string[] Roster =
        {
            "배회하는 재의 야수", "배회하는 강철 파수", "배회하는 서리 심장",
            "배회하는 심연의 눈", "배회하는 백골 군단", "배회하는 타락 성좌",
            "배회하는 태엽 심판", "배회하는 잿빛 용", "배회하는 별의 사도",
            "배회하는 탑의 그림자",
        };

        const string K_UNTIL = "ats.fieldboss.until";
        const string K_ROLL = "ats.fieldboss.nextroll";
        const string K_TIER = "ats.fieldboss.tier";

        static bool _fighting;
        static bool _qaSeeded;
        static Func<long> _now = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        public static Func<long> NowUnix
        {
            get => _now;
            set => _now = value ?? (() => DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        static long Now => NowUnix();

        public static bool Active => !Blocked && RemainingSec > 0;
        public static bool Fighting => _fighting && !Blocked;

        public static int RemainingSec
        {
            get
            {
                if (Blocked) return 0;
                long until = ReadLong(K_UNTIL);
                return (int)Mathf.Max(0, until - Now);
            }
        }

        public static int SpawnTier
        {
            get
            {
                int t = PlayerPrefs.GetInt(K_TIER, GameState.Tier);
                if (t < 0) t = 0;
                if (t > 9) t = 9;
                return t;
            }
        }

        /// <summary>T1=5 · T2=15 · T10=95. 탑 10층 대보스 테이블을 안 탄다.</summary>
        public static int FightFloor => BaseFloor + SpawnTier * FloorStep;

        public static string Name()
        {
            int i = SpawnTier;
            if (i < 0 || i >= Roster.Length) return Roster[0];
            return Roster[i];
        }

        public static Economy.DropSource DropSource => Economy.DropSource.FieldDungeonBoss;

        public static string Line()
        {
            if (!Active && !Fighting) return "";
            return $"배회 보스 {Name()}(§10-1)";
        }

        public static string CardTitle() => Active
            ? $"배회 보스 {RemainingText()}"
            : "배회 보스 없음";

        public static string CardBody() =>
            $"{Name()} · 준비 없이 만나면 위험 · 환생석 없음(§10-1·§10-8)";

        public static string BattleTitle() => $"필드 보스 · {Name()}";

        public static string RemainingText()
        {
            int s = RemainingSec;
            return $"{s / 60:D2}:{s % 60:D2}";
        }

        public static void Tick()
        {
            if (Blocked || Active) return;
            long next = ReadLong(K_ROLL);
            if (Now < next) return;
            PlayerPrefs.SetString(K_ROLL, (Now + RollIntervalSec).ToString());
            var rng = new Rng((uint)(Now & 0x7FFFFFFF));
            if (!rng.Chance(SpawnChance))
            {
                PlayerPrefs.Save();
                return;
            }
            SpawnNow(GameState.Tier);
        }

        public static void Consume()
        {
            PlayerPrefs.DeleteKey(K_UNTIL);
            PlayerPrefs.Save();
        }

        public static void BeginFight()
        {
            _fighting = !Blocked;
        }

        public static void EndFight()
        {
            _fighting = false;
        }

        public static bool ShowOnHub
        {
            get
            {
                if (Blocked) return false;
                string raw = Environment.GetEnvironmentVariable(EnvShow);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static void SeedQaIfRequested()
        {
            if (_qaSeeded || !ShowOnHub) return;
            _qaSeeded = true;
            GameState.TrySelectTier(0);
            SpawnNow(0);
        }

        public static void ResetForTest()
        {
            _fighting = false;
            _qaSeeded = false;
            PlayerPrefs.DeleteKey(K_UNTIL);
            PlayerPrefs.DeleteKey(K_ROLL);
            PlayerPrefs.DeleteKey(K_TIER);
            PlayerPrefs.Save();
            NowUnix = null;
        }

        static void SpawnNow(int tier)
        {
            if (tier < 0) tier = 0;
            if (tier > 9) tier = 9;
            PlayerPrefs.SetString(K_UNTIL, (Now + LifetimeSec).ToString());
            PlayerPrefs.SetInt(K_TIER, tier);
            PlayerPrefs.Save();
        }

        static long ReadLong(string key)
        {
            return long.TryParse(PlayerPrefs.GetString(key, "0"), out long v) ? v : 0;
        }
    }
}
