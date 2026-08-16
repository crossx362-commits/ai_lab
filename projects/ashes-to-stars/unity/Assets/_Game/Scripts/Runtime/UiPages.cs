using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 허브 페이지 좌표. 화면마다 줄을 쌓지 않고 같은 격자·탭 높이를 쓴다.
    /// </summary>
    public static class UiPages
    {
        public const float TabH = 42f;
        public const float TabGap = 10f;

        public static Rect AfterTabs(Rect r, float extra = 12f) =>
            new Rect(r.x, r.y + TabH + extra, r.width, Mathf.Max(40f, r.height - TabH - extra));

        /// <summary>초상 둘레 장비 칸. 각도는 12시가 -90도.</summary>
        public static Rect SlotOnRing(Vector2 center, float radiusX, float radiusY,
                                      float degrees, float size)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float x = center.x + Mathf.Cos(rad) * radiusX - size * 0.5f;
            float y = center.y + Mathf.Sin(rad) * radiusY - size * 0.5f;
            return new Rect(x, y, size, size);
        }

        public static readonly float[] EquipRingDegrees = { -90f, -20f, 50f, 125f, 180f, -145f };

        /// <summary>명부 왼쪽 목록 · 오른쪽 대형 모습. 비율을 뒤집으면 오너 지시와 반대다.</summary>
        public const float RosterListRatio = 0.30f;
        public const float RosterRowH = 64f;
        public const float RosterRowGap = 6f;
        public const float LargeLookW = 200f;
        public const float LargeLookH = 240f;

        public static void RosterSplit(Rect r, out Rect list, out Rect stage)
        {
            const float gap = 10f;
            float listW = Mathf.Max(220f, r.width * RosterListRatio);
            if (listW > r.width - 120f) listW = Mathf.Max(160f, r.width * 0.3f);
            list = new Rect(r.x, r.y, listW, r.height);
            stage = new Rect(r.x + listW + gap, r.y, Mathf.Max(80f, r.width - listW - gap), r.height);
        }

        public static Rect RosterRow(Rect list, int index)
        {
            return new Rect(list.x, list.y + index * (RosterRowH + RosterRowGap),
                list.width, RosterRowH);
        }

        public static Rect LargeLook(Rect stage)
        {
            return new Rect(stage.center.x - LargeLookW * 0.5f, stage.y + 88f,
                LargeLookW, LargeLookH);
        }

        /// <summary>전투 idle 스프라이트 폴더. 초상이 아니라 전신 모습을 그릴 때 쓴다.</summary>
        public static string LookDir(string job) => job switch
        {
            "탱" or "수호기사" or "광전사" => "tank",
            "힐" or "사제" or "드루이드" => "healer",
            "버퍼" or "음유시인" or "주술사" or "정령사" => "buffer",
            "마법사" or "소환사" => "mage",
            _ => "dps",
        };

        public static Rect[] Grid(Rect r, int cols, int rows, float gap = 16f)
        {
            if (cols < 1) cols = 1;
            if (rows < 1) rows = 1;
            var cells = new Rect[cols * rows];
            float cw = (r.width - gap * (cols - 1)) / cols;
            float ch = (r.height - gap * (rows - 1)) / rows;
            for (int i = 0; i < cells.Length; i++)
            {
                int x = i % cols;
                int y = i / cols;
                cells[i] = new Rect(r.x + x * (cw + gap), r.y + y * (ch + gap), cw, ch);
            }
            return cells;
        }
    }
}
