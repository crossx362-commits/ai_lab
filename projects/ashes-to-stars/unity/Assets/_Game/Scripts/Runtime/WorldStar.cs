using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 월드맵 내 별 크기(§14 ✅). 층이 오를수록 연속으로 커진다.
    /// 10층마다 모양을 바꾸는 연출은 💡라 여기 넣지 않는다.
    /// </summary>
    public static class WorldStar
    {
        public const int MaxFloor = 100;
        public const float MinPx = 40f;
        public const float MaxPx = 112f;
        public const float PlateH = 100f;

        public static int ClampFloor(int floor) => Mathf.Clamp(floor, 1, MaxFloor);

        public static float SizePx(int floor)
        {
            int f = ClampFloor(floor);
            return MinPx + (MaxPx - MinPx) * (f - 1) / (MaxFloor - 1);
        }

        public static string SizeLabel(int floor) =>
            $"{ClampFloor(floor)}층 · 별 {SizePx(floor):0}px";

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
