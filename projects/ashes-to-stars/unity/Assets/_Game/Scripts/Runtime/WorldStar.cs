using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 월드맵 내 별(§14 ✅). 크기는 층에 따라 커지고, 인식 범위도 같이 넓어진다.
    /// 영공 버프/디버프는 영지에서 켠다. 10층 모양 연출은 💡라 안 넣는다.
    /// </summary>
    public static class WorldStar
    {
        public const int MaxFloor = 100;
        public const float MinPx = 40f;
        public const float MaxPx = 112f;
        public const float PlateH = 100f;
        public const float MinSense = 4f;
        public const float MaxSense = 16f;
        public const float AllyBuffMul = 1.05f;
        public const float EnemyDebuffMul = 0.95f;

        const string K_ALLY = "ats.star.ally";
        const string K_ENEMY = "ats.star.enemy";

        static bool _loaded;
        static bool _ally;
        static bool _enemy;

        public static int ClampFloor(int floor) => Mathf.Clamp(floor, 1, MaxFloor);

        public static float SizePx(int floor)
        {
            int f = ClampFloor(floor);
            return MinPx + (MaxPx - MinPx) * (f - 1) / (MaxFloor - 1);
        }

        public static float Sense(int floor)
        {
            int f = ClampFloor(floor);
            return MinSense + (MaxSense - MinSense) * (f - 1) / (MaxFloor - 1);
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
        public static float EnemyMul => EnemyDebuff ? EnemyDebuffMul : 1f;

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

        public static void ResetForTest()
        {
            PlayerPrefs.DeleteKey(K_ALLY);
            PlayerPrefs.DeleteKey(K_ENEMY);
            _loaded = false;
            _ally = false;
            _enemy = false;
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
