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
