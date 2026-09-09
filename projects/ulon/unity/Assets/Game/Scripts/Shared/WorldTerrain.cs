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
        // 평지 기준 높이. 던전 방이 지면 -DungeonDepth에 파이므로 수면(3.0)보다 그만큼 더 위에 있어야 한다 —
        // 6.0이었을 때 방 바닥이 수면 아래로 들어가 던전이 통째로 물에 잠겼다(QA 샷 실측).
        // 깊이를 4.5 → 5.8로 내리면서 침수 게이트(방 바닥 ≥ 수면+0.5)를 지키는 **최소 상향폭**만 올렸다.
        public const float LandBase = 10.0f;

        /// <summary>
        /// 던전 방을 지면 아래로 내리는 깊이(m). **실내 줌 상한이 여기서 유도된다** — 눈높이가 지표를 넘으면
        /// 화면이 통째로 잔디가 되기 때문이다(실측 2026-09-06). 그래서 깊이를 고치면 줌도 따라 움직인다:
        /// 다음 사람이 카메라 쪽 숫자를 손으로 맞추는 일이 없게 상수를 한 곳에 둔다(검수 요구).
        ///
        /// 값의 근거: 실내 줌 8.0m를 쓰려면 깊이 ≥ 눈높이(1.0) + 8.0×sin35° + 여유(0.15) = 5.74m.
        /// 지형·입구·콘텐츠 배치를 흔드는 값이라 **여유를 크게 잡지 않는다** — 5.8m가 8.0m를 만족하는 최소값이다.
        /// </summary>
        public const float DungeonDepth = 5.8f;

        /// <summary>플레이 카메라가 바라보는 지점의 높이(방 바닥 위) — 줌 상한 유도에 쓴다.</summary>
        public const float PlayerEyeHeight = 1.0f;

        /// <summary>눈이 지표를 뚫지 않도록 남기는 여유(m).</summary>
        public const float DungeonEyeMargin = 0.15f;

        /// <summary>이 피치에서 실내가 유지되는 최대 카메라 거리(m). 깊이에서 유도한다 — 하드코딩 금지.</summary>
        public static float IndoorDistanceFor(float pitchDeg)
        {
            float s = Mathf.Sin(pitchDeg * Mathf.Deg2Rad);
            if (s < 0.01f)
                return 999f;
            return (DungeonDepth - PlayerEyeHeight - DungeonEyeMargin) / s;
        }

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
        public const float LakeRadius = 21f;
        public const float LakeDepth = 1.8f;     // 물 아래 깊이

        // 강 — 호수에서 서쪽 바다까지. 산 띠를 협곡으로 통과한다.
        public const float RiverZ = 10f;
        // 폭 9m짜리 물길은 **조망에서 실 한 줄**이라 「강이 없다」로 읽혔다(검수 랩 ⑥, 14_world_vista).
        // 섬 한 변이 300m다 — 물길이 지형의 일부로 읽히려면 그 축척에 맞아야 한다.
        // 8m(폭 16m)로 넓혔더니 이번엔 **호수가 바다로 트인 후미**가 됐다 — 담수호가 아니게 된다.
        // 조망에서 보이되 호수와 바다 사이에 **목**이 남을 만큼만: 호수 지름 42m의 1/3 아래.
        public const float RiverHalfWidth = 6.0f;
        public const float RiverFromX = -80f;
        public const float RiverToX = -140f;

        /// <summary>월드 좌표의 지형 높이(미터).</summary>
        public static float HeightAt(float wx, float wz)
        {
            // 정사각 링 그대로 쓰면 화면에서 "네모난 담장"으로 읽힌다(조망 샷 실측).
            // 거리 자체를 노이즈로 흔들어 해안선·산자락을 들쭉날쭉하게 만든다.
            float wobble = (Mathf.PerlinNoise(wx * 0.009f + 5.5f, wz * 0.009f + 71f) - 0.5f) * 52f;
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
                // 균일한 톱니 링은 담장으로 읽힌다(§8.1) — 저주파 마스크로 산괴와 고개를 만든다.
                h = LandBase + t * MountainHeight * Massif(wx, wz) * (0.55f + ridge * 0.65f + bumps);
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
            h = RaisePier(wx, wz, h);
            return Mathf.Clamp(h, 0f, MaxHeight);
        }

        /// <summary>산괴 마스크 — 0에 가까우면 고개(넘어갈 수 있는 낮은 안부), 1이면 큰 산덩어리.</summary>
        public static float Massif(float wx, float wz)
        {
            // **산이 사방을 고르게 두르면 섬이 「대접」으로 읽힌다**(검수 랩 ⑥, 14_world_vista).
            // 마스크 주기를 늘리고(133m → 180m) 대비를 키워 **높은 산괴와 낮은 고개**를 크게 가른다 —
            // 산을 없애는 것이 아니라 **한쪽을 낮춰** 바다가 안쪽으로 들여다보이게 하는 것이다.
            float n = Mathf.PerlinNoise(wx * 0.0055f + 41.7f, wz * 0.0055f + 3.3f);
            return Mathf.Lerp(0.08f, 1.7f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.40f, 0.74f, n)));
        }

        /// <summary>마을 주변은 평평하게, 바깥으로 갈수록 완만한 기복.</summary>
        static float Rolling(float wx, float wz)
        {
            float n = Mathf.PerlinNoise(wx * 0.028f + 12.3f, wz * 0.028f + 4.7f) * 0.55f;
            n += Mathf.PerlinNoise(wx * 0.07f + 30f, wz * 0.07f) * 0.28f;
            n += Mathf.PerlinNoise(wx * 0.18f, wz * 0.18f + 18f) * 0.17f;
            // **마을이 앉는 평지는 마을과 같은 배로 넓어진다**(랩 B) — 킷을 2.1배로 키우자 마당·울타리가
            // 옛 평지(반경 23m) 밖 비탈로 걸어 나가 마구간 마당이 0.21m 기울어 「발이 지표에서 벗어남」이 났다.
            // 지형 자체는 실 미터지만 **평지의 크기는 마을이 정한다** — 그래서 이 두 반경만 배율을 탄다.
            // 경사 폭(25m)은 **그대로 둔다** — 배율을 곱해 100m까지 늘렸더니 던전 방이 앉은 자리
            // (반경 96m)의 지형이 0.4m 내려가 이미 파 둔 방의 실내 카메라가 지표를 뚫었다.
            // 넓힐 것은 평지이지 경사가 아니다.
            float flatR = 23f * WorldScale.Kit;
            float rampR = flatR + 25f;
            float dist = Mathf.Sqrt(wx * wx + wz * wz);
            float flatten = 1f;
            if (dist > flatR)
                flatten = dist >= rampR ? 0f : 1f - (dist - flatR) / (rampR - flatR);
            return n * (1f - flatten) * 2.75f;
        }

        static float CarveLake(float wx, float wz, float h)
        {
            float d = Mathf.Sqrt((wx - LakeX) * (wx - LakeX) + (wz - LakeZ) * (wz - LakeZ));
            if (d > LakeRadius)
                return h;
            // 바깥 40%만 완만한 둑, 안쪽은 넓은 수면 — 중심 한 점만 깊으면 물이 손톱만큼만 보인다.
            // 둑을 넓게(바깥 60%) — 급경사면 물가 모래 띠가 안 생기고 잔디가 물에 수직으로 잘린다(§8.2).
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - d / LakeRadius) / 0.6f));
            // 물가는 뭍 높이에서 서서히 내려가야 한다 — 바로 수면 높이로 떨어뜨리면 3m 수직 절벽이 된다.
            float bed = Mathf.Lerp(h, SeaLevel - LakeDepth, t);
            return Mathf.Min(h, bed);
        }

        /// <summary>
        /// **잔교 둑** — 부두가 물에 닿게 하는 것은 소품이 아니라 **지형**이다(검수 큐 ⑤, 2026-09-09:
        /// 「부두가 물 위 판때기 하나고 뭍과 이어지지도 않았다」). 낚시터는 게이트상 물가에 있었지만
        /// (수면 대비 +0.1m) 주변이 평평해 화면에서는 물 한가운데 떠 있는 판으로 읽혔다.
        ///
        /// 소품을 물 위로 띄우는 수리는 **발 높이 게이트와 싸우는 길**이라 택하지 않았다 — 대신
        /// 호수 중심에서 마을 쪽으로 **좁은 둑**을 수면 위로 남긴다. 부두 조각은 지표에 스냅되므로
        /// 저절로 그 둑 위에 서고, 화면은 「뭍에서 물로 뻗은 잔교」가 된다.
        /// </summary>
        // 물가에서 물 쪽으로 뻗는 길이. **9m로는 잔교가 거의 물에 안 나간다** — 이 호숫가는
        // 비탈이 급해 둑 시작점에서 4m만 가면 지표가 이미 5.4m(뭍)였다(실측 2026-09-09).
        public const float PierLength = 14f;
        // 둑 반폭. 사람 1.4m만 보면 1.5로 충분하지만, 널판 옆에 말뚝을 박으려면 그 자리도
        // 평평해야 한다(발 높이 자는 조각 밑면을 지표에 맞춘다) — 1.5에서는 말뚝이 전부 물에 섰다.
        public const float PierHalfWidth = 2.4f;
        public const float PierTop = SeaLevel + 0.35f;

        static float RaisePier(float wx, float wz, float h)
        {
            // 호수 중심 → 마을(원점) 방향이 물가다. 그 선을 따라 뻗는다.
            var cx = LakeX; var cz = LakeZ;
            float dx = -cx, dz = -cz;
            float len = Mathf.Sqrt(dx * dx + dz * dz);
            if (len < 0.001f)
                return h;
            dx /= len; dz /= len;
            // 물가 지점(호수 반경의 92% 자리)에서 안쪽으로 PierLength만큼.
            float shoreD = LakeRadius * 0.92f;
            float sx = cx + dx * shoreD, sz = cz + dz * shoreD;
            float ex = cx + dx * (shoreD - PierLength), ez = cz + dz * (shoreD - PierLength);
            // 선분까지의 거리.
            float vx = ex - sx, vz = ez - sz;
            float t = Mathf.Clamp01(((wx - sx) * vx + (wz - sz) * vz) / (vx * vx + vz * vz));
            float px = sx + vx * t, pz = sz + vz * t;
            float d = Mathf.Sqrt((wx - px) * (wx - px) + (wz - pz) * (wz - pz));
            if (d > PierHalfWidth * 1.8f)
                return h;
            // 가장자리는 물로 흘러내린다 — 수직 벽이면 화면에서 콘크리트 부두가 된다.
            float w = 1f - Mathf.Clamp01((d - PierHalfWidth) / (PierHalfWidth * 0.8f));
            return Mathf.Max(h, Mathf.Lerp(h, PierTop, w));
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
            // 바다에 가까울수록 넓고 깊게 — 하구가 강과 같은 폭이면 「수로」로 읽힌다.
            float mouth = Mathf.InverseLerp(RiverFromX, RiverToX, wx);
            float bed = SeaLevel - (1.2f + 1.4f * mouth) * t;
            return Mathf.Min(h, Mathf.Lerp(h, bed, t));
        }
    }
}
