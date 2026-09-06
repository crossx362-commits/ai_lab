using UnityEngine;

namespace Ulon.Shared
{
    /// <summary>
    /// 월드 지형의 단일 원장(기획서 §6.1 광산/산지·§8.2 단조로운 화면 금지).
    /// 높이 함수를 여기 한 곳에 두고 빌더와 Assert가 같은 값을 쓴다 — 두 곳에 같은 식이 살면 갈라진다.
    /// 좌표는 월드 미터, 높이도 미터(Terrain 정규화는 빌더가 한다).
    /// </summary>
    public static class WorldTerrain
    {
        public const float Span = 300f;          // 지형 한 변
        public const float MaxHeight = 60f;      // Terrain size.y
        public const float SeaLevel = 3.0f;      // 물 표면 높이(바다·강·호수 공용)
        public const float LandBase = 6.0f;      // 평지 기준 높이 — 물보다 3m 위

        // 체비셰프 거리(정사각 링) 기준 띠. 마을·필드·던전(±68, 방+뚜껑 반경 ~14)은 전부 평지 띠 안이다.
        public const float FlatMax = 88f;        // 여기까지 평지
        public const float MountainStart = 92f;  // 산 시작
        public const float MountainPeak = 105f;  // 능선
        public const float MountainEnd = 118f;   // 산 끝
        public const float CoastEnd = 128f;      // 해안선 — 여기부터 바다
        public const float MountainHeight = 30f; // 능선 평균 높이(위에 능선 노이즈)
        public const float SeaFloor = 0.4f;

        // 호수 — 마을(반경 48)·던전(±68 모서리) 밖 서쪽 평지.
        public const float LakeX = -70f;
        public const float LakeZ = 10f;
        public const float LakeRadius = 15f;
        public const float LakeDepth = 1.8f;     // 물 아래 깊이

        // 강 — 호수에서 서쪽 바다까지. 산 띠를 협곡으로 통과한다.
        public const float RiverZ = 10f;
        public const float RiverHalfWidth = 4.5f;
        public const float RiverFromX = -80f;
        public const float RiverToX = -140f;

        /// <summary>월드 좌표의 지형 높이(미터).</summary>
        public static float HeightAt(float wx, float wz)
        {
            // 정사각 링 그대로 쓰면 화면에서 "네모난 담장"으로 읽힌다(조망 샷 실측).
            // 거리 자체를 노이즈로 흔들어 해안선·산자락을 들쭉날쭉하게 만든다.
            float wobble = (Mathf.PerlinNoise(wx * 0.012f + 5.5f, wz * 0.012f + 71f) - 0.5f) * 26f;
            float m = Mathf.Max(Mathf.Abs(wx), Mathf.Abs(wz)) + wobble;
            float h;

            if (m <= FlatMax)
            {
                h = LandBase + Rolling(wx, wz);
            }
            else if (m <= MountainStart)
            {
                h = LandBase + Rolling(wx, wz) * (1f - Mathf.InverseLerp(FlatMax, MountainStart, m));
            }
            else if (m <= MountainEnd)
            {
                // 산등성이 — 시작→능선→끝으로 오르내리고 능선 노이즈로 봉우리를 만든다.
                float t = m <= MountainPeak
                    ? Mathf.InverseLerp(MountainStart, MountainPeak, m)
                    : 1f - Mathf.InverseLerp(MountainPeak, MountainEnd, m);
                t = Mathf.SmoothStep(0f, 1f, t);
                float ridge = Mathf.PerlinNoise(wx * 0.045f + 91f, wz * 0.045f + 17f);
                ridge = 1f - Mathf.Abs(ridge * 2f - 1f);            // 능선형 노이즈
                float bumps = Mathf.PerlinNoise(wx * 0.13f, wz * 0.13f + 55f) * 0.35f;
                h = LandBase + t * MountainHeight * (0.55f + ridge * 0.65f + bumps);
            }
            else if (m <= CoastEnd)
            {
                float t = Mathf.InverseLerp(MountainEnd, CoastEnd, m);
                h = Mathf.Lerp(LandBase, SeaFloor, Mathf.SmoothStep(0f, 1f, t));
            }
            else
            {
                h = SeaFloor;
            }

            // 지형 가장자리는 반드시 바다로 — 노이즈가 육지를 경계 밖으로 밀면 조망에서
            // 수직 절벽(맵 끝 단면)이 보인다(조망 샷 실측).
            float edge = Mathf.Max(Mathf.Abs(wx), Mathf.Abs(wz));
            float rim = Span * 0.5f - 14f;
            if (edge > rim)
                h = Mathf.Lerp(h, SeaFloor, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(rim, Span * 0.5f, edge)));

            h = CarveLake(wx, wz, h);
            h = CarveRiver(wx, wz, h);
            return Mathf.Clamp(h, 0f, MaxHeight);
        }

        /// <summary>마을 주변은 평평하게, 바깥으로 갈수록 완만한 기복.</summary>
        static float Rolling(float wx, float wz)
        {
            float n = Mathf.PerlinNoise(wx * 0.028f + 12.3f, wz * 0.028f + 4.7f) * 0.55f;
            n += Mathf.PerlinNoise(wx * 0.07f + 30f, wz * 0.07f) * 0.28f;
            n += Mathf.PerlinNoise(wx * 0.18f, wz * 0.18f + 18f) * 0.17f;
            float dist = Mathf.Sqrt(wx * wx + wz * wz);
            float flatten = 1f;
            if (dist > 23f)
                flatten = dist >= 48f ? 0f : 1f - (dist - 23f) / 25f;
            return n * (1f - flatten) * 2.75f;
        }

        static float CarveLake(float wx, float wz, float h)
        {
            float d = Mathf.Sqrt((wx - LakeX) * (wx - LakeX) + (wz - LakeZ) * (wz - LakeZ));
            if (d > LakeRadius)
                return h;
            float t = Mathf.SmoothStep(0f, 1f, 1f - d / LakeRadius);   // 가장자리는 얕게
            float bed = SeaLevel - LakeDepth * t;
            return Mathf.Min(h, bed);
        }

        static float CarveRiver(float wx, float wz, float h)
        {
            if (wx > RiverFromX || wx < RiverToX)
                return h;
            // 살짝 굽은 물길 — 직선 수로는 화면에서 인공물로 읽힌다.
            float centerZ = RiverZ + Mathf.Sin((wx - RiverFromX) * 0.06f) * 6f;
            float d = Mathf.Abs(wz - centerZ);
            if (d > RiverHalfWidth * 2.2f)
                return h;
            float t = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Clamp01((d - RiverHalfWidth) / (RiverHalfWidth * 1.2f)));
            float bed = SeaLevel - 1.2f * t;
            return Mathf.Min(h, Mathf.Lerp(h, bed, t));
        }
    }
}
