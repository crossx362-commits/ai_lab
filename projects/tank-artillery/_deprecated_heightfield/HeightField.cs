// 명세: docs/GAME_SPEC_TANK_ARTILLERY.md §7-1
//
// ⚠️ SIM 레이어 규칙(§4-1): 이 파일은 UnityEngine을 참조하지 않는다.
//    유니티 프로젝트에 편입할 때 Sim.asmdef 로 묶어 UnityEngine 참조를 컴파일 단계에서 막는다.
//    이유 — 서버(헤드리스)·AI·리플레이·궤적 프리뷰가 전부 이 코드를 그대로 쓴다.

using System;

namespace Tankfall.Sim
{
    /// <summary>지형 높이 격자. 셀 Cells개, 정점 (Cells+1)^2개.</summary>
    public sealed class HeightField
    {
        public readonly int Cells;      // §7-1: 256
        public readonly float MapSize;  // §7-1: 200m
        public readonly float CellSize; // 0.78125m
        public readonly float[] H;      // row-major, 길이 Dim*Dim

        public int Dim => Cells + 1;

        public HeightField(int cells, float mapSize)
        {
            Cells = cells;
            MapSize = mapSize;
            CellSize = mapSize / cells;
            H = new float[(cells + 1) * (cells + 1)];
        }

        public float GetAt(int i, int j) => H[j * Dim + i];
        public void SetAt(int i, int j, float v) => H[j * Dim + i] = v;
        public void AddAt(int i, int j, float v) => H[j * Dim + i] += v;
        public bool InRange(int i, int j) => i >= 0 && i < Dim && j >= 0 && j < Dim;

        public float WorldOf(int index) => index * CellSize;

        /// <summary>이중선형 샘플. 지형 콜라이더를 두지 않으므로(§7-5) 접지·충돌·조준이 전부 이 함수를 쓴다.</summary>
        public float SampleHeight(float x, float z)
        {
            float fx = x / CellSize, fz = z / CellSize;
            int i = (int)MathF.Floor(fx), j = (int)MathF.Floor(fz);
            if (i < 0) { i = 0; fx = 0; }
            if (j < 0) { j = 0; fz = 0; }
            if (i >= Cells) { i = Cells - 1; fx = i + 1; }
            if (j >= Cells) { j = Cells - 1; fz = j + 1; }
            float tx = fx - i, tz = fz - j;
            float h00 = GetAt(i, j), h10 = GetAt(i + 1, j);
            float h01 = GetAt(i, j + 1), h11 = GetAt(i + 1, j + 1);
            float a = h00 + (h10 - h00) * tx;
            float b = h01 + (h11 - h01) * tx;
            return a + (b - a) * tz;
        }

        /// <summary>중앙차분 경사(도). 탱크 등반 한계 판정(§2-7, 42°)과 같은 기준으로 잰다.</summary>
        public float SlopeDegAt(int i, int j)
        {
            if (i <= 0 || j <= 0 || i >= Dim - 1 || j >= Dim - 1) return 0f;
            float gx = (GetAt(i + 1, j) - GetAt(i - 1, j)) / (2f * CellSize);
            float gz = (GetAt(i, j + 1) - GetAt(i, j - 1)) / (2f * CellSize);
            return MathF.Atan(MathF.Sqrt(gx * gx + gz * gz)) * (180f / MathF.PI);
        }
    }
}
