// 명세: docs/GAME_SPEC_TANK_ARTILLERY.md §7-5
//
// 지형에 콜라이더가 없으므로(§7-5) 레이캐스트는 전부 여기를 지난다:
//   조준 거리계 · AI 시야 판정 · 마우스 클릭 지점 · 포탄 충돌 정밀화
//
// ⚠️ 스피어 트레이싱(SDF 값만큼 건너뛰기)을 쓰지 않는다.
//    우리 SDF는 진짜 거리장이 아니라 평면 근사(y − h)라, 가파른 비탈에서 실제 거리보다 큰 값이 나와
//    표면을 뚫고 지나간다. 고정 스텝으로 부호 변화를 찾고 이분 탐색으로 좁힌다.

using System;

namespace Tankfall.Sim
{
    public struct RayHit
    {
        public bool Hit;
        public float Distance;
        public float X, Y, Z;
    }

    public static class SdfRaymarch
    {
        /// <summary>표면 탐색 고정 스텝(복셀 배수). 0.5면 복셀 0.5m에서 0.25m 간격.</summary>
        public const float StepRatio = 0.5f;

        /// <summary>이분 탐색 반복. 8회면 0.25m 구간이 1mm 아래로 좁혀진다.</summary>
        public const int Refine = 8;

        public static RayHit March(SdfVolume vol, float ox, float oy, float oz,
                                   float dx, float dy, float dz, float maxDist)
        {
            // 방향 정규화 — 호출자가 안 했을 수 있다
            float len = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
            if (len < 1e-6f) return default;
            dx /= len; dy /= len; dz /= len;

            float step = vol.Voxel * StepRatio;
            float tPrev = 0f;
            float sPrev = vol.SampleWorld(ox, oy, oz);
            if (sPrev <= 0f)                       // 시작점이 이미 땅속
                return new RayHit { Hit = true, Distance = 0f, X = ox, Y = oy, Z = oz };

            for (float t = step; t <= maxDist; t += step)
            {
                float s = vol.SampleWorld(ox + dx * t, oy + dy * t, oz + dz * t);
                if (s <= 0f)
                {
                    // [tPrev, t] 안에 표면이 있다. 이분 탐색으로 좁힌다.
                    float lo = tPrev, hi = t;
                    for (int i = 0; i < Refine; i++)
                    {
                        float mid = (lo + hi) * 0.5f;
                        if (vol.SampleWorld(ox + dx * mid, oy + dy * mid, oz + dz * mid) > 0f) lo = mid;
                        else hi = mid;
                    }
                    return new RayHit { Hit = true, Distance = hi, X = ox + dx * hi, Y = oy + dy * hi, Z = oz + dz * hi };
                }
                tPrev = t; sPrev = s;
            }
            return default;
        }
    }
}
