// 명세: docs/GAME_SPEC_TANK_ARTILLERY.md §7-3
//
// 오너 결정(2026-09-15): 크레이터 프로파일 = A + C 결합
//   A(구 불리언)  — 구멍 모양. 날카롭게 도려낸다. 예측 가능하고 언덕 옆면도 제대로 깎인다.
//   C(흙 보존)    — 파낸 흙을 가장자리 링에 쌓아 둔덕을 만든다. "적 앞을 파서 엄폐를 만든다"는 전술이 생긴다.
//   + 안식각 릴랙세이션 — A의 치명적 부작용을 막는 가드. 아래 주석 참조.
//
// ⚠️ 안식각 패스를 제거하지 마라.
//    순수 구 불리언은 크레이터 벽이 72° (실측, tools/crater_harness.py)다.
//    탱크 등반 한계는 42°(§2-7)이므로, 이 패스가 없으면 크레이터에 빠진 탱크가 영원히 못 나온다.
//    이건 연출 문제가 아니라 플레이어를 가두는 버그다.
//
// 실측(참조 구현 tools/crater_harness.py, Rc=7m 평지 1발):
//    안식각 없음 : 최대경사 72.1°  깊이 7.0m  둔덕 0.00m   → 갇힘
//    안식각 35°  : 최대경사 37.2°  깊이 5.8m  둔덕 1.64m   → 통과 (42°까지 4.8° 여유)
//    10발 연타   : 최대경사 39.2°  깊이 9.6m  둔덕 1.15m   → 톱니·접시 없음

using System;

namespace Tankfall.Sim
{
    public struct CraterRequest
    {
        public float X, Z;      // 착탄 월드 좌표 (수평)
        public float Radius;    // Rc = weapon.ExplosionRadius * weapon.TerrainDamage

        public CraterRequest(float x, float z, float radius) { X = x; Z = z; Radius = radius; }
    }

    public static class TerrainDeformer
    {
        // --- 전역 상수 (무기별로 바뀌지 않는다. 플레이어가 지형 반응을 학습할 수 있어야 하므로) ---

        /// <summary>안식각(축방향, 도). 대각 합성은 atan(√2·tan35°) = 37.2° 로 42° 한계 아래.</summary>
        public const float ReposeAngleDeg = 35f;

        /// <summary>릴랙세이션 반복. 40회가 수렴점(80·160회와 결과 동일 — 실측). 결정론을 위해 고정값.</summary>
        public const int ReposeIterations = 40;

        /// <summary>파낸 흙 중 둔덕으로 쌓이는 비율. 나머지는 소실(먼지로 날아간 흙).</summary>
        public const float DepositRatio = 0.8f;

        /// <summary>둔덕 링 바깥 반경 = Rc * 이 값. 2.0에서 둔덕 1.64m(탱크 높이의 80% = 엄폐 성립).</summary>
        public const float DepositRingMul = 2.0f;

        // 릴랙세이션 이웃: 축 2 + 대각 2. 대각은 거리 √2 만큼 임계를 키운다.
        // ⚠️ 축 4방향만 쓰면 대각 경사가 47.8°까지 남아 42° 한계를 넘는다(실측). 8방향(=이 4쌍)이어야 한다.
        static readonly int[] NX = { 1, 0, 1, 1 };
        static readonly int[] NZ = { 0, 1, 1, -1 };
        static readonly float[] NL = { 1f, 1f, 1.41421356f, 1.41421356f };

        /// <summary>폭발 하나를 지형에 적용한다. 호출 후 영향 청크만 dirty 처리하면 된다(§7-2).</summary>
        public static void ApplyCrater(HeightField f, in CraterRequest req)
        {
            float dug = Carve(f, req);
            Deposit(f, req, dug);
            Relax(f, req);
        }

        /// <summary>A: 구 불리언. 반환값은 파낸 부피(m³).</summary>
        static float Carve(HeightField f, in CraterRequest req)
        {
            float rc = req.Radius, cs = f.CellSize, area = cs * cs;
            // 착탄점의 지표 높이를 구 중심으로 쓴다 → 평지는 반구, 비탈은 옆면이 깎인다.
            int ci = Clamp((int)(req.X / cs), 0, f.Dim - 1);
            int cj = Clamp((int)(req.Z / cs), 0, f.Dim - 1);
            float cy = f.GetAt(ci, cj);

            float dug = 0f;
            int i0 = (int)((req.X - rc) / cs) - 1, i1 = (int)((req.X + rc) / cs) + 2;
            int j0 = (int)((req.Z - rc) / cs) - 1, j1 = (int)((req.Z + rc) / cs) + 2;

            for (int j = j0; j < j1; j++)
                for (int i = i0; i < i1; i++)
                {
                    if (!f.InRange(i, j)) continue;
                    float dx = i * cs - req.X, dz = j * cs - req.Z;
                    float dh = MathF.Sqrt(dx * dx + dz * dz);
                    if (dh >= rc) continue;

                    float half = MathF.Sqrt(rc * rc - dh * dh);
                    float bottom = cy - half, top = cy + half;
                    float h = f.GetAt(i, j);
                    // top 위쪽 지형은 건드리지 않는다 — 건드리면 오버행이 되는데 Heightfield는 표현 못 한다(§2-2).
                    if (h > bottom && h <= top)
                    {
                        dug += (h - bottom) * area;
                        f.SetAt(i, j, bottom);
                    }
                }
            return dug;
        }

        /// <summary>C: 파낸 흙을 Rc~Rc*Mul 링에 코사인 가중으로 쌓는다.</summary>
        static void Deposit(HeightField f, in CraterRequest req, float dug)
        {
            if (dug <= 0f) return;
            float rc = req.Radius, ro = rc * DepositRingMul, cs = f.CellSize, area = cs * cs;
            int i0 = (int)((req.X - ro) / cs) - 1, i1 = (int)((req.X + ro) / cs) + 2;
            int j0 = (int)((req.Z - ro) / cs) - 1, j1 = (int)((req.Z + ro) / cs) + 2;

            // 1패스: 가중치 합
            float wsum = 0f;
            for (int j = j0; j < j1; j++)
                for (int i = i0; i < i1; i++)
                {
                    if (!f.InRange(i, j)) continue;
                    float dx = i * cs - req.X, dz = j * cs - req.Z;
                    float dh = MathF.Sqrt(dx * dx + dz * dz);
                    if (dh < rc || dh >= ro) continue;
                    wsum += RingWeight(dh, rc, ro);
                }
            if (wsum <= 0f) return;

            // 2패스: 분배 (Σ 쌓인 부피 = dug * DepositRatio)
            float k = dug * DepositRatio / (wsum * area);
            for (int j = j0; j < j1; j++)
                for (int i = i0; i < i1; i++)
                {
                    if (!f.InRange(i, j)) continue;
                    float dx = i * cs - req.X, dz = j * cs - req.Z;
                    float dh = MathF.Sqrt(dx * dx + dz * dz);
                    if (dh < rc || dh >= ro) continue;
                    f.AddAt(i, j, k * RingWeight(dh, rc, ro));
                }
        }

        // Rc에서 1, Ro에서 0. cos² 이라 양 끝 기울기가 0이라 이음매가 매끄럽다.
        static float RingWeight(float dh, float rc, float ro)
        {
            float c = MathF.Cos(MathF.PI * 0.5f * (dh - rc) / (ro - rc));
            return c * c;
        }

        /// <summary>
        /// 안식각 릴랙세이션(Gauss-Seidel). 임계를 넘는 높이차만큼 흙을 아래 셀로 옮긴다 — 부피 보존.
        /// ⚠️ 순회 순서가 결과를 바꾼다(제자리 갱신). 서버·클라가 같은 값을 내려면 순서를 바꾸지 마라.
        /// </summary>
        static void Relax(HeightField f, in CraterRequest req)
        {
            float cs = f.CellSize;
            float lim = MathF.Tan(ReposeAngleDeg * (MathF.PI / 180f)) * cs;
            float r = req.Radius * DepositRingMul + 4f;   // 둔덕 바깥까지 여유 4m

            int i0 = Math.Max(1, (int)((req.X - r) / cs));
            int i1 = Math.Min(f.Dim - 1, (int)((req.X + r) / cs) + 1);
            int j0 = Math.Max(1, (int)((req.Z - r) / cs));
            int j1 = Math.Min(f.Dim - 1, (int)((req.Z + r) / cs) + 1);

            for (int pass = 0; pass < ReposeIterations; pass++)
            {
                float moved = 0f;
                for (int j = j0; j < j1; j++)
                    for (int i = i0; i < i1; i++)
                        for (int n = 0; n < 4; n++)
                        {
                            int ii = i + NX[n], jj = j + NZ[n];
                            if (ii < i0 || ii >= i1 || jj < j0 || jj >= j1) continue;

                            float d = f.GetAt(i, j) - f.GetAt(ii, jj);
                            float lm = lim * NL[n];
                            if (d > lm || d < -lm)
                            {
                                float t = (MathF.Abs(d) - lm) * 0.5f * (d > 0f ? 1f : -1f);
                                f.AddAt(i, j, -t);
                                f.AddAt(ii, jj, t);
                                moved += MathF.Abs(t);
                            }
                        }
                if (moved < 1e-4f) return;   // 조기 수렴
            }
        }

        static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
