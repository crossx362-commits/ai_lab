using UnityEngine;

namespace Ulon.Shared
{
    /// <summary>
    /// 지형 **도포 원장** — 어느 좌표가 무슨 지표인지 한 곳에서 정한다. 빌더가 칠하고 Assert가 같은 함수로 잰다.
    ///
    /// 왜 필요한가: §6.1 지역(농경지·숲·광산)이 소품만 얹힌 채 바닥은 전부 같은 밝은 초록이라
    /// 「지역」으로 안 읽혔다(검수 2026-09-06 관찰). 그리고 지역끼리 잇는 길이 없어 평지에 떠 있었다.
    /// </summary>
    public static class WorldSplat
    {
        // TerrainData.terrainLayers 순서와 같아야 한다(빌더가 이 순서로 배열을 만든다).
        public const int Grass = 0;
        public const int Rock = 1;
        public const int Sand = 2;
        public const int Tilled = 3;    // 농경지 — 갈아엎은 흙
        public const int Soil = 4;      // 숲 — 어두운 부엽토
        public const int Gravel = 5;    // 광산 — 자갈
        public const int Road = 6;      // 지역을 잇는 길
        public const int Cobble = 7;    // 마을 광장 — 돌포장
        /// <summary>
        /// **마른 풀**(검수 랩 ⑤ 「초록 한 톤」). 세어 보니 지역 밖 평지 표본 253곳 중 **184곳(73%)이
        /// 지표 한 겹**이었다 — 흙이 적은 게 아니라 초록이 한 가지인 것이 증상이다. 그래서 흙 비율은
        /// 그대로 두고 **잔디 안에서** 가른다: 같은 풀 자리를 짙은 풀과 마른 풀 둘로 나눠 칠한다.
        /// 두 겹의 합은 언제나 옛 풀 한 겹과 같다 — 「지역 밖 평지 풀 하한 0.80」은 그래서 안 흔들린다.
        /// </summary>
        public const int DryGrass = 8;
        /// <summary>
        /// **그늘진 절벽 바위**(검수 랩 ⑥ 「산 표면이 세로로 늘어난다」). 유니티 지형은 도포를
        /// **평면(XZ)으로 투영**하므로 경사가 설수록 무늬가 세로로 늘어난다 — 세어 보니 이 산은
        /// **최대 82°**다. 투영을 바꾸려면 지형 셰이더를 갈아야 하니, 대신 **늘어난 줄무늬를 끊는다**:
        /// 바위를 두 겹으로 갈라 다른 타일링·다른 색으로 얼룩지게 한다(경사면 39%가 한 겹이었다).
        /// </summary>
        public const int CliffDark = 9;
        public const int LayerCount = 10;

        /// <summary>
        /// **풀은 벽에 안 붙는다**(검수 랩 ⑨ — 상식 모순). 삼면 투영으로 암석 결은 살아났는데
        /// 앞쪽 절벽에 **초록·누런 띠가 커튼처럼 흘러내렸다**. 60m 수직 암벽에 풀은 자라지 않는다.
        ///
        /// 뿌리는 「산 중턱까지 풀이 올라간다」를 위해 걸어 둔 **바위 상한**이었다(고도 22m 아래에서
        /// rock ≤ 0.42~0.87). 그 상한이 **경사를 안 봤다** — 중턱 비탈에도, 80° 암벽에도 똑같이 걸려
        /// 풀을 남겼다. 그래서 상한 자체를 없애지 않고 **급경사에서만 풀어 준다**: 55°부터 풀리기
        /// 시작해 70°면 상한이 사라진다(= 바위 그대로).
        ///
        /// **45°부터 자르는 판은 실제로 돌려 보고 물렀다** — 중턱 풀이 0.66 → **0.20**으로 떨어져
        /// 「산 중턱 도포가 한쪽으로 쏠렸다」 자가 울었다(하한 0.35). 이 산은 6~18m 띠의 상당 부분이
        /// 45°를 넘는다. 벽에서 풀을 떼려다 **중턱을 회색 한 장으로 만들면 지난 반려로 되돌아간다** —
        /// 두 자를 동시에 만족시키는 창이 55~70°다.
        ///
        /// 굽는 쪽과 재는 쪽이 **같은 이 함수**를 읽는다. slope는 tan(경사각).
        /// </summary>
        public const float WallSlopeFrom = 1.43f;   // tan 55°
        public const float WallSlopeTo = 2.75f;     // tan 70°

        /// <summary>이 경사에서 「중턱 풀」 상한을 얼마나 풀어 줄지(0 = 그대로, 1 = 상한 없음).</summary>
        public static float WallRockAt(float slope)
        {
            return Mathf.Clamp01((slope - WallSlopeFrom) / (WallSlopeTo - WallSlopeFrom));
        }

        /// <summary>이 자리의 바위 중 **그늘진 절벽 바위가 차지하는 몫**(0~1).</summary>
        public static float DarkCliffAt(float wx, float wz)
        {
            // 잔디와 **다른 주기**를 쓴다 — 같은 주기면 산과 들이 같은 얼룩을 쓰는 것이 눈에 보인다.
            float broad = Mathf.PerlinNoise(wx * 0.032f + 41.7f, wz * 0.032f + 12.9f);
            float mid = Mathf.PerlinNoise(wx * 0.075f + 8.2f, wz * 0.075f + 77.5f);
            // **잔디와 반대로 전환을 넓힌다.** 잔디는 얼룩이 「이쪽 아니면 저쪽」이어야 색이 갈려 보였지만,
            // 절벽에서 필요한 것은 갈림이 아니라 **두 무늬가 겹쳐 늘어난 줄을 끊는 것**이다. 좁게 잡았더니
            // 자리마다 한 겹으로 떨어져 급경사 표본의 31%가 여전히 한 겹이었다(자가 잡았다).
            return Mathf.Clamp01((Mathf.Max(broad, mid * 0.94f) - 0.36f) / 0.45f) * 0.90f;
        }

        /// <summary>이 자리의 풀 중 **마른 풀이 차지하는 몫**(0~1). 굽는 쪽·재는 쪽이 같이 읽는다.</summary>
        public static float DryGrassAt(float wx, float wz)
        {
            // 저주파 얼룩이 톤을 가르고(멀리서 보이는 무늬), 고주파가 경계를 흐트러뜨린다(줄무늬 방지).
            // **완만하게 섞으면 화면에서는 그냥 탁해진다.** 첫 판이 그랬다: 마른 풀 몫이 평균 0.27인데
            // 값 대부분이 중간대(0.2~0.4)라 두 겹이 섞여 **한 톤이 조금 흐려졌을 뿐**이었다(마을 넓은
            // 샷에서 확인). 얼룩으로 읽히려면 자리마다 **거의 마른 풀이거나 거의 짙은 풀**이어야 한다 —
            // 그래서 전환 구간을 좁혀(0.34 → 0.13) 경계를 세우고, 얼룩 주기도 38m에서 22m로 줄인다.
            // 주기가 하나면 **얼룩과 얼룩 사이가 통째로 비는 자리**가 생긴다 — 22m 주기 하나로 깔았더니
            // 마을은 갈렸는데 사냥터 앞쪽 30m가 균일한 초록이었다(샷으로 확인). 큰 얼룩 위에 중간 얼룩을
            // 겹쳐 어느 화면에서도 한 톤 구간이 남지 않게 한다.
            float broad = Mathf.PerlinNoise(wx * 0.045f + 17.3f, wz * 0.045f + 53.1f);
            float mid = Mathf.PerlinNoise(wx * 0.090f + 63.9f, wz * 0.090f + 21.4f);
            float fine = Mathf.PerlinNoise(wx * 0.140f + 5.7f, wz * 0.140f + 88.4f) - 0.5f;
            float b = Mathf.Max(broad, mid * 0.92f);
            return Mathf.Clamp01((b + fine * 0.30f - 0.52f) / 0.13f) * 0.90f;
        }

        public const float RoadHalfWidth = 2.4f;   // 길 중심에서 이 폭까지는 온전히 길
        public const float RoadFade = 1.8f;        // 그 바깥으로 이 폭만큼 흙이 옅어진다

        /// <summary>마을 광장에서 각 지역으로 가는 길(직선 스포크). 지역이 평지에 떠 있지 않게 한다.</summary>
        public static Vector4[] Routes => new[]
        {
            new Vector4(0f, 0f, WorldRegions.Meadow.X, WorldRegions.Meadow.Z),
            new Vector4(0f, 0f, WorldRegions.Forest.X, WorldRegions.Forest.Z),
            new Vector4(0f, 0f, WorldRegions.Mine.X, WorldRegions.Mine.Z),
        };

        /// <summary>
        /// **마을 아마밭** — 지역(농경지)과 달리 마을 안에 있는 작은 뙈기라 원형 지역 규칙에 안 걸린다.
        /// 바닥이 초록 잔디인 채로 작물만 얹으면 「밭」이 아니라 「풀밭에 놓인 풀」이다(검수 반려:
        /// 20cm 덤불 하나가 아마밭이었다). 중심·반폭을 여기 한 곳에 적고 빌더·게이트가 같이 읽는다.
        /// </summary>
        public const float FlaxX = 3.4f * WorldScale.Kit;   // 마을 격자 × 킷 배율(랩 B)
        // 북쪽 가장자리를 마을 우리(z≈−17.4)에 물리지 않게 잡는다 — 첫 판은 마을 울타리가
        // 밭 안으로 들어와 「경작지」가 아니라 「우리」로 읽혔다(게이트가 안쪽 침범 6개로 잡았다).
        public const float FlaxZ = -20.6f * WorldScale.Kit;   // 마을 격자 × 킷 배율(랩 B)
        public const float FlaxHalfX = 5.0f * WorldScale.Kit;   // 마을 격자 × 킷 배율(랩 B)
        public const float FlaxHalfZ = 3.0f * WorldScale.Kit;   // 마을 격자 × 킷 배율(랩 B)

        /// <summary>아마밭 뙈기를 덮는 세기(0~1) — 사각 뙈기라 가장자리만 부드럽게 흘린다.</summary>
        public static float FlaxCoverAt(float wx, float wz)
        {
            const float fade = 1.4f;
            float tx = 1f - Mathf.Clamp01((Mathf.Abs(wx - FlaxX) - FlaxHalfX) / fade);
            float tz = 1f - Mathf.Clamp01((Mathf.Abs(wz - FlaxZ) - FlaxHalfZ) / fade);
            float wobble = (Mathf.PerlinNoise(wx * 0.35f + 31f, wz * 0.35f + 7f) - 0.5f) * 0.18f;
            return Mathf.Clamp01(Mathf.Min(tx, tz) + wobble);
        }

        /// <summary>채광 현장(갱구·광차 자리) — 배치 코드와 지표가 **같은 좌표**를 읽는다.</summary>
        public static readonly Vector2 MinePit = new Vector2(WorldRegions.Mine.X + 1.5f, WorldRegions.Mine.Z);
        public const float MinePitRadius = 9f;

        /// <summary>이 지역에 해당하는 도포 레이어.</summary>
        public static int LayerOf(string regionObject)
        {
            if (regionObject == WorldRegions.MeadowObject) return Tilled;
            if (regionObject == WorldRegions.ForestObject) return Soil;
            if (regionObject == WorldRegions.MineObject) return Gravel;
            return Grass;
        }

        /// <summary>
        /// 이 좌표를 덮는 지표(길 &gt; 지역)와 그 세기(0~1). 없으면 -1.
        /// 경계는 노이즈로 흔든다 — 원이 그대로 보이면 화면에서 「도장 찍은 자국」으로 읽힌다.
        /// </summary>
        public static int CoverAt(float wx, float wz, out float weight)
        {
            weight = 0f;
            int layer = -1;

            var regions = WorldRegions.All;
            for (int i = 0; i < regions.Length; i++)
            {
                var r = regions[i];
                float d = new Vector2(wx - r.X, wz - r.Z).magnitude;
                float wobble = (Mathf.PerlinNoise(wx * 0.05f + i * 13.7f, wz * 0.05f + i * 4.3f) - 0.5f) * (r.Radius * 0.22f);
                // **광산은 「바위가 있는 곳」이 아니라 파낸 자리다**(검수·오너 2026-09-09: 「돌이 깔린
                // 지대인데 바닥이 녹색이면 반려」). 자갈을 지역 밖까지 번지게 해 화면에서 잔디가 아니라
                // 파낸 땅으로 읽히게 한다. 다른 지역은 그대로다.
                // 1.35로는 **현장 주변만** 드러나고 서쪽 바위 대여섯은 여전히 잔디에 박혀 있었다
                // (검수 재반려). 바위가 서 있는 범위(반경 R−2m)를 자갈이 덮도록 1.75로 넓힌다 —
                // 바위를 옮기는 게 아니라 **지표를 바위에 맞춘다**.
                float spread = r.Object == WorldRegions.MineObject ? 1.75f : 1f;
                float t = 1f - Mathf.InverseLerp(r.Radius * 0.72f * spread, r.Radius * 1.04f * spread, d + wobble);
                t = Mathf.Clamp01(t) * 0.95f;
                if (t > weight)
                {
                    weight = t;
                    layer = LayerOf(r.Object);
                }
            }

            // 채광 현장 바로 앞은 **파낸 흙**이다 — 자갈만 깔면 「돌밭」이지 「캐낸 자리」로는 안 읽힌다.
            float pitWobble = (Mathf.PerlinNoise(wx * 0.18f + 41f, wz * 0.18f + 12f) - 0.5f) * 2.2f;
            float pit = 1f - Mathf.Clamp01((new Vector2(wx - MinePit.x, wz - MinePit.y).magnitude + pitWobble
                                            - MinePitRadius * 0.6f) / (MinePitRadius * 0.5f));
            pit = Mathf.Clamp01(pit) * 0.95f;
            if (pit > weight)
            {
                weight = pit;
                layer = Soil;
            }

            float flax = FlaxCoverAt(wx, wz);
            if (flax > weight)
            {
                weight = flax;
                layer = Tilled;
            }

            float hunt = HuntCoverAt(wx, wz);
            if (hunt > weight)
            {
                weight = hunt;
                layer = hunt > 0.62f ? Soil : Gravel;   // 짙은 데는 밟혀 드러난 흙, 옅은 데는 마른 자갈
            }

            float plaza = PlazaCoverAt(wx, wz);
            if (plaza > weight)
            {
                weight = plaza;
                layer = Cobble;
            }

            var routes = Routes;
            for (int i = 0; i < routes.Length; i++)
            {
                float d = DistToSegment(wx, wz, routes[i].x, routes[i].y, routes[i].z, routes[i].w);
                float t = 1f - Mathf.Clamp01((d - RoadHalfWidth) / RoadFade);
                if (t > weight)
                {
                    weight = t;
                    layer = Road;
                }
            }
            return layer;
        }

        /// <summary>
        /// **마을 광장 돌포장**(검수 랩 ② — 「01·02가 회색 십자로」).
        /// 왜 지형인가: 광장 바닥을 킷 `road.fbx` 판으로 깔았더니 **무늬가 안 보였다** — Kenney 모델은
        /// 색상 아틀라스라 UV가 한 점에 몰려 있어 어떤 텍스처를 씌워도 단색이 된다(2026-09-09 실측:
        /// 돌포장 무늬를 만들어 104칸을 칠했는데 화면은 회색 한 장). 지형은 UV가 제대로 있어 무늬가 산다.
        /// 모양은 굽는 쪽 `PlazaPath`와 같다: 가운데 네모 마당 + 길 넷.
        /// </summary>
        public static float PlazaCoverAt(float wx, float wz)
        {
            float mx = wx / WorldScale.Kit, mz = wz / WorldScale.Kit;
            float square = Inside(mx, -4f, 4f, mz, -4f, 4f);
            float ew = Inside(mx, -8f, 10f, mz, -1f, 1f);
            float ns = Inside(mx, -1f, 1f, mz, -7f, 11f);
            return Mathf.Max(square, Mathf.Max(ew, ns));
        }

        /// <summary>
        /// **사냥터 — 짐승이 다닌 자리**(검수 랩 ④ 「형광 초록 당구대」).
        /// 세어 보니 그 일대 지표는 **Grass 98%**였다(`OutdoorCensus` 사냥터 지표) — 도포가 한 겹이면
        /// 아무리 몹을 잘 흩어도 화면은 당구대다. 밭·광산처럼 **그 자리에서 일어나는 일**을 바닥에 적는다:
        /// 몹이 도는 안쪽은 밟혀 흙이 드러나고, 바깥으로 가면서 마른 자갈로 성기게 풀린다.
        /// 얼룩은 결정적 노이즈다 — 매 판 같은 그림이어야 검수가 같은 화면을 본다.
        /// </summary>
        public static readonly Vector2 HuntGround = new Vector2(5.88f, 70.35f);
        /// <summary>
        /// **무리 모양대로 닳는다** — 잡몹 여덟은 가로 31m·세로 10m로 서 있다(자리 원장 실측).
        /// 원으로 깔았더니 얼룩이 무리 앞쪽 잔디로 번져 「몹은 잔디에, 흙은 그 앞에」가 됐다.
        /// 다니는 모양이 곧 닳는 모양이다.
        /// </summary>
        public const float HuntRadius = 20f;
        public const float HuntRadiusZ = 11f;

        public static float HuntCoverAt(float wx, float wz)
        {
            float ex = (wx - HuntGround.x) / HuntRadius;
            float ez = (wz - HuntGround.y) / HuntRadiusZ;
            float d = Mathf.Sqrt(ex * ex + ez * ez) * HuntRadius;
            if (d > HuntRadius * 1.35f)
                return 0f;
            // 가장자리를 원으로 끊지 않는다 — 둘레를 흔들어 「자연히 닳은 자리」로 만든다.
            float wobble = (Mathf.PerlinNoise(wx * 0.07f + 91f, wz * 0.07f + 33f) - 0.5f) * 9f;
            float core = 1f - Mathf.Clamp01((d + wobble - HuntRadius * 0.45f) / (HuntRadius * 0.75f));
            if (core <= 0f)
                return 0f;
            // 안에서도 고르게 칠하지 않는다 — 성글게 벗겨진 얼룩이라야 「밟힌 자리」로 읽힌다.
            float patch = Mathf.PerlinNoise(wx * 0.16f + 7f, wz * 0.16f + 61f);
            float cover = Mathf.Clamp01(core * (0.35f + patch * 0.85f));

            // **몹이 선 자리는 확실히 드러난다**(검수 지시: 「몹이 밟고 선 것이 무엇인가」가 증상이다).
            // 얼룩만 깔았을 때 여덟 중 셋이 잔디를, 넷이 옅은 자갈을 밟고 있었다 — 발밑이 초록이면
            // 밟힌 자리를 아무리 넓혀도 「몹은 잔디에 섰다」로 읽힌다. 자리마다 다져진 원을 얹는다.
            for (int i = 0; i < HuntRoster.Spots.Length; i++)
            {
                var p = HuntRoster.World(i);
                float dd = new Vector2(wx - p.x, wz - p.y).magnitude;
                float trample = 1f - Mathf.Clamp01((dd - 1.6f) / 2.6f);
                if (trample > 0f)
                    cover = Mathf.Max(cover, 0.55f + trample * 0.45f);
            }
            return cover;
        }

        /// <summary>사각형 안이면 1, 가장자리 한 칸에서 0으로 — 돌포장이 칼로 자른 듯 끝나지 않게.</summary>
        static float Inside(float x, float x0, float x1, float z, float z0, float z1)
        {
            float fade = 0.6f;
            float ix = Mathf.Min(x - x0, x1 - x);
            float iz = Mathf.Min(z - z0, z1 - z);
            float i = Mathf.Min(ix, iz);
            return Mathf.Clamp01(i / fade);
        }

        public static float DistToSegment(float px, float pz, float ax, float az, float bx, float bz)
        {
            float dx = bx - ax, dz = bz - az;
            float len2 = dx * dx + dz * dz;
            float t = len2 < 0.0001f ? 0f : Mathf.Clamp01(((px - ax) * dx + (pz - az) * dz) / len2);
            float cx = ax + dx * t, cz = az + dz * t;
            return new Vector2(px - cx, pz - cz).magnitude;
        }
    }
}
