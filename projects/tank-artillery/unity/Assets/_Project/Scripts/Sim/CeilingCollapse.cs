// 명세: docs/GAME_SPEC_TANK_ARTILLERY.md §7-6-(1)
//
// === SDF에는 중력이 없다 ===
// 천장을 옆으로 파면 지지 없는 흙덩이가 공중에 그대로 뜬다. Heightfield에는 없던 문제다
// (컬럼당 높이가 하나라 뜰 수가 없었다).
//
// 정석은 연결성 플러드필로 지면과 끊긴 성분을 찾아 떨어뜨리는 것이지만 비싸다.
// 여기서는 싼 쪽을 쓴다: **얇은 흙 층은 무너진다**.
//   - 대부분의 부유 덩어리는 폭발이 뚫다 만 얇은 천장에서 생긴다
//   - 얇으면 무너진다는 규칙은 플레이어가 예측할 수 있다("한 발 더 때리면 뚫린다")
//   - 비용이 기둥 스캔뿐이다 (실측 §7-6)
//
// ⚠️ 이 패스는 폭발 직후 항상 돌려야 한다. 빠뜨리면 공중에 흙이 뜬 채로 남는다.

using System;

namespace Tankfall.Sim
{
    public static class CeilingCollapse
    {
        /// <summary>이보다 얇은 흙 층은 무너진다(m). 명세 §7-6-(1).</summary>
        public const float MinThickness = 1.0f;

        /// <summary>층을 지울 때 써넣는 SDF 값(복셀 배수). 부호만 맞으면 되지만
        /// 이웃 복셀과 선형보간했을 때 표면이 되살아나지 않을 만큼은 커야 한다.</summary>
        const float AirFill = 2.0f;

        /// <summary>
        /// dirty 범위의 각 기둥을 훑어 얇은 흙 층을 제거한다.
        /// 반환 = 실제로 지운 범위(메시 재생성 범위를 여기까지 넓혀야 한다). 지운 게 없으면 Empty.
        /// </summary>
        public static DirtyBounds Apply(SdfVolume vol, in DirtyBounds b, float minThickness = MinThickness)
        {
            var outB = new DirtyBounds { I0 = int.MaxValue, J0 = int.MaxValue, K0 = int.MaxValue,
                                         I1 = int.MinValue, J1 = int.MinValue, K1 = int.MinValue, Empty = true };
            if (b.Empty) return outB;

            // 층이 dirty 범위를 걸쳐 있을 수 있으므로 위아래로 넉넉히 본다.
            int pad = (int)MathF.Ceiling(minThickness / vol.Voxel) + 2;
            int j0 = b.J0 - pad, j1 = b.J1 + pad;

            for (int iz = b.K0; iz < b.K1; iz++)
                for (int ix = b.I0; ix < b.I1; ix++)
                {
                    // 위에서 아래로 내려오며 흙 층 [top, bottom] 을 찾는다.
                    // 규약: 양수=공기. 공기→땅 전환이 층의 윗면, 땅→공기 전환이 아랫면.
                    int top = int.MinValue;
                    float topY = 0f;
                    float prevS = vol.Sample(ix, j1, iz);
                    bool prevAir = prevS > 0f;

                    for (int j = j1 - 1; j >= j0; j--)
                    {
                        float s = vol.Sample(ix, j, iz);
                        bool air = s > 0f;

                        // ⚠️ 두께를 복셀 개수로 재면 안 된다.
                        //    0.78m 천장이 격자 경계에 걸치면 땅 복셀 2개를 차지해 2칸(=1.0m)으로 잡히고,
                        //    임계 미만인데도 안 무너진다(실측으로 잡은 버그). 표면 위치를 보간해서 잰다.
                        if (prevAir && !air)                            // 공기 → 땅: 천장 윗면
                        {
                            top = j;
                            topY = Lerp(vol.WorldY(j + 1), vol.WorldY(j), prevS, s);
                        }
                        else if (!prevAir && air && top != int.MinValue)
                        {
                            int bottom = j + 1;                         // 마지막 땅 복셀
                            float bottomY = Lerp(vol.WorldY(j + 1), vol.WorldY(j), prevS, s);
                            float thickness = topY - bottomY;
                            if (thickness < minThickness)
                            {
                                for (int jj = bottom; jj <= top; jj++)
                                    vol.Set(ix, jj, iz, AirFill * vol.Voxel);
                                Grow(ref outB, ix, bottom, iz);
                                Grow(ref outB, ix, top, iz);
                            }
                            top = int.MinValue;
                        }
                        prevS = s; prevAir = air;
                    }
                    // j0 까지 땅이 이어지면 그건 지반이다 — 두께를 잴 수 없으므로 건드리지 않는다.
                }
            return outB;
        }

        /// <summary>부호가 갈리는 두 복셀 사이의 표면 y를 선형보간으로 구한다.</summary>
        static float Lerp(float ya, float yb, float sa, float sb)
            => ya + (yb - ya) * (sa / (sa - sb));

        static void Grow(ref DirtyBounds b, int i, int j, int k)
        {
            if (i < b.I0) b.I0 = i; if (i + 1 > b.I1) b.I1 = i + 1;
            if (j < b.J0) b.J0 = j; if (j + 1 > b.J1) b.J1 = j + 1;
            if (k < b.K0) b.K0 = k; if (k + 1 > b.K1) b.K1 = k + 1;
            b.Empty = false;
        }

        /// <summary>두 DirtyBounds 를 합친다(폭발 범위 + 붕괴 범위).</summary>
        public static DirtyBounds Union(in DirtyBounds a, in DirtyBounds b)
        {
            if (a.Empty) return b;
            if (b.Empty) return a;
            return new DirtyBounds
            {
                I0 = Math.Min(a.I0, b.I0), I1 = Math.Max(a.I1, b.I1),
                J0 = Math.Min(a.J0, b.J0), J1 = Math.Max(a.J1, b.J1),
                K0 = Math.Min(a.K0, b.K0), K1 = Math.Max(a.K1, b.K1),
                Empty = false
            };
        }
    }
}
