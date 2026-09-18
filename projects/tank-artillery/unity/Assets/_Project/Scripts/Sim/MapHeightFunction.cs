// 명세: docs/GAME_SPEC_TANK_ARTILLERY.md §맵 6종
// 파괴 전 지형 h(x,z) + ∇h. 경사 계수는 유한차분 5샘플이 아니라 이 기울기로 굵는다.

using System;

namespace Tankfall.Sim
{
    public enum MapKind { TwinHills = 0, Crater = 1, Terrace = 2, Valley = 3, Ridge = 4, Badlands = 5 }

    public delegate void HeightGrad(float x, float z, out float h, out float dhx, out float dhz);

    public static class MapHeightFunction
    {
        /// <summary>
        /// 맵 한 변(m).
        ///
        /// ⚠️ 200 → 280 (2026-09-17, 오너 지시 "맵 확대"). 200m 에서는 스폰을 가장자리까지 당겨도
        ///    교전 거리가 150m 라 파워 100 사거리(220m, §5-2)의 68% 밖에 못 썼다.
        ///    280m + 스폰 여백 65m = **교전 150m**. 맵은 넓히되 교전 거리는 사거리 분포에 맞춘다.
        ///
        /// ⚠️ 교전 190m 를 시도했다가 **분포가 무너졌다**(2026-09-17 실측):
        ///    캐터펄트는 사거리 172m 라 **최대 파워로도 못 닿아** 승률 7%,
        ///    반대로 미사일(276m)·멀티미사일(266m)이 87%·79% 로 폭주했다.
        ///    교전 거리는 **가장 짧은 사거리(캐터펄트 172m)보다 뚜렷하게 아래**여야 한다 —
        ///    안 그러면 사거리가 "기종 차이"가 아니라 생사 판정이 된다.
        /// ⚠️ 이 값을 바꾸면 지형이 따라와야 한다 — 그래서 아래 좌표는 전부 **MapSize 비례**다.
        ///    절대 좌표(100f 같은)를 새로 적지 마라. 적는 순간 맵 크기를 못 바꾸는 코드가 된다.
        /// ⚠️ 면적이 1.96배라 초기 메시 생성·삼각형 수도 그만큼 는다. 바꿨으면 `-perf` 로 재라(§7-7).
        /// </summary>
        public const float MapSize = 280f;

        /// <summary>맵 중심. 지형 대칭의 기준선이다.</summary>
        public const float Center = MapSize * 0.5f;

        /// <summary>언덕 퍼짐(면적 단위). 맵 크기의 제곱에 비례해야 지형 밀도가 유지된다.</summary>
        public const float HillSpread = 1600f * (MapSize / 200f) * (MapSize / 200f);

        /// <summary>
        /// 스폰이 맵 가장자리에서 떨어진 거리. 교전 거리 = MapSize − 2×이 값.
        ///
        /// ⚠️ 42 → 25 (2026-09-17, 오너 지적 "포격거리 너무 가까움").
        ///    42 면 교전 거리가 116m 인데 파워 100 사거리가 220m 다(§5-2) — 최대의 절반만 쓰니
        ///    파워 조절이 승부를 가르지 않았다. 25 면 150m 로 사거리의 68% 를 쓴다.
        ///    더 늘리려면 맵(200m)을 키워야 하는데, 지형 함수가 x=100·z=75/125 같은 **절대 좌표**에
        ///    묶여 있어 같이 비례화해야 한다 — 그건 별도 작업이다.
        /// ⚠️ 이 값을 바꾸면 `verify.sh map`(스폰 대칭·40° 이륙)과 `battle`(승률)을 **둘 다** 다시 돌려라.
        ///    스폰이 가장자리로 갈수록 지형이 달라져 이륙 각도와 엄폐가 바뀐다.
        /// </summary>
        public const float SpawnInset = 65f;

        /// <summary>
        /// 한 팀의 탱크 수. **팀 인원을 뜻하는 숫자는 전부 여기서 유도한다** — 유닛 Id 부여, 턴 순서,
        /// 로스터 크기, 고르기 UI 제한, HUD 줄 수, 스폰 분배가 전부 이 하나를 본다.
        /// Sim 에 둔 이유는 하네스와 게임이 **같은 값**을 봐야 하기 때문이다 — 둘이 갈리면
        /// 하네스는 3v3 을, 게임은 4v4 를 재면서 승률 표가 조용히 거짓말을 한다.
        /// ⚠️ 바꾸면 밸런스가 움직인다. 3v3 으로 맞춘 승률 표는 무효이니 battle 게이트를 다시 떠라.
        /// </summary>
        public const int TeamSize = 4;   // 2026-09-18 오너 지시 "4:4 만들어" (이전 3)

        public const float TwinWestHillX = Center, TwinWestHillZ = MapSize * 0.375f;
        public const float TwinEastHillX = Center, TwinEastHillZ = MapSize * 0.625f;

        /// <summary>맵 종류 수 — 하드코딩된 3을 여기저기 두면 맵을 늘릴 때마다 빠뜨린다(2026-09-18 교훈).</summary>
        public static readonly int Count = Enum.GetValues(typeof(MapKind)).Length;

        public static string Name(MapKind k) => k switch
        {
            MapKind.TwinHills => "TwinHills",
            MapKind.Crater => "Crater",
            MapKind.Terrace => "Terrace",
            MapKind.Valley => "Valley",
            MapKind.Ridge => "Ridge",
            MapKind.Badlands => "Badlands",
            _ => k.ToString()
        };

        public static bool TryParse(string s, out MapKind k)
        {
            k = MapKind.TwinHills;
            return !string.IsNullOrEmpty(s) && Enum.TryParse(s.Trim(), true, out k);
        }

        public static void Spawn(MapKind map, int team, int slot, out float x, out float z)
        {
            int i = slot < 0 ? 0 : (slot > TeamSize - 1 ? TeamSize - 1 : slot);
            // 인원 수에서 유도한다 — 3명이면 0.25·0.50·0.75, 4명이면 0.2·0.4·0.6·0.8.
            // 예전엔 0.25 간격을 숫자로 박아 3명 전용이었다(4명이면 넷째가 맵 밖 z=MapSize 에 섰다).
            z = MapSize * (i + 1f) / (TeamSize + 1f);
            x = team == 0 ? SpawnInset : MapSize - SpawnInset;
            _ = map;
        }

        public static float Height(MapKind map, float x, float z)
        {
            Evaluate(map, x, z, out float h, out _, out _);
            return h;
        }

        public static float TwinHills(float x, float z)
        {
            Evaluate(MapKind.TwinHills, x, z, out float h, out _, out _);
            return h;
        }

        public static Func<float, float, float> Fn(MapKind map)
            => (x, z) => Height(map, x, z);

        public static HeightGrad Grad(MapKind map)
            => (float x, float z, out float h, out float hx, out float hz) => Evaluate(map, x, z, out h, out hx, out hz);

        public static void Evaluate(MapKind map, float x, float z, out float h, out float dhx, out float dhz)
        {
            switch (map)
            {
                case MapKind.Crater: EvalCrater(x, z, out h, out dhx, out dhz); break;
                case MapKind.Terrace: EvalTerrace(x, z, out h, out dhx, out dhz); break;
                case MapKind.Valley: EvalValley(x, z, out h, out dhx, out dhz); break;
                case MapKind.Ridge: EvalRidge(x, z, out h, out dhx, out dhz); break;
                case MapKind.Badlands: EvalBadlands(x, z, out h, out dhx, out dhz); break;
                default: EvalTwin(x, z, out h, out dhx, out dhz); break;
            }
        }

        static void EvalTwin(float x, float z, out float h, out float hx, out float hz)
        {
            // ⚠️ `s`(언덕 퍼짐)는 **면적 단위**라 맵이 커지면 같이 커져야 한다.
            //    280m 로 넓히고도 1600 을 그대로 뒀더니 200m 시절 언덕이 넓은 벌판에 덩그러니 놓여
            //    지형이 통째로 밋밋해졌다(실제로 그렇게 나왔다). 맵 비례로 스케일한다.
            h = 5f; hx = 0f; hz = 0f;
            Bump(x, z, TwinWestHillX, TwinWestHillZ, 17f, HillSpread, ref h, ref hx, ref hz);
            Bump(x, z, TwinEastHillX, TwinEastHillZ, 17f, HillSpread, ref h, ref hx, ref hz);
            AddDetailRipple(x, z, 1.2f, 0.05f, ref h, ref hx, ref hz);
        }

        static void Bump(float x, float z, float cx, float cz, float amp, float s,
                         ref float h, ref float hx, ref float hz)
        {
            float dx = x - cx, dz = z - cz;
            float u = 1f - (dx * dx + dz * dz) / s;
            if (u <= 0f) return;
            h += amp * u * u;
            float k = amp * 2f * u * (-2f / s);
            hx += k * dx;
            hz += k * dz;
        }

        static void EvalCrater(float x, float z, out float h, out float hx, out float hz)
        {
            float dx = x - Center, dz = z - Center;
            float r2 = dx * dx + dz * dz;
            float r = MathF.Sqrt(r2);
            float t = (r - 25f) / 70f;
            Smooth01(t, out float s, out float ds);
            h = 4f + 10f * s;
            if (r > 1e-4f && ds != 0f)
            {
                float dr = 10f * ds / 70f / r;
                hx = dr * dx;
                hz = dr * dz;
            }
            else { hx = 0f; hz = 0f; }
            AddDetailRipple(x, z, 0.8f, 0.04f, ref h, ref hx, ref hz);
        }

        /// <summary>
        /// 표면 디테일용 잔물결. **X=100(스폰 중심선) 기준 짝함수**로 둔다.
        /// ⚠️ 처음엔 `sin(x*k)*cos(z*k)` 를 썼는데 sin 은 x=100 기준 대칭이 아니다 —
        ///    슬롯에 따라 A/B 스폰 높이가 최대 0.74m 벌어져 `MapVerify` 대칭 게이트를 떨어뜨렸다
        ///    (TwinHills 는 진폭이 작아 우연히 허용치 0.5m 안에 들었을 뿐, 같은 결함을 갖고 있었다).
        ///    `cos((x-100)*k)` 는 x→200-x 미러에서 부호만 바뀌는 (x-100) 을 짝함수에 넣으므로 항상 대칭이다.
        /// </summary>
        static void AddDetailRipple(float x, float z, float amp, float k, ref float h, ref float hx, ref float hz)
        {
            float cxm = MathF.Cos((x - Center) * k), sxm = MathF.Sin((x - Center) * k);
            float sz = MathF.Sin(z * k), cz = MathF.Cos(z * k);
            h += amp * cxm * cz;
            hx += amp * (-k * sxm) * cz;
            hz += amp * cxm * (-k * sz);
        }

        static void EvalTerrace(float x, float z, out float h, out float hx, out float hz)
        {
            float u = MathF.Abs(x - Center);
            float sign = x >= Center ? 1f : -1f;
            float step, dstep;
            if (u < 28f) { step = 3.5f; dstep = 0f; }
            else if (u < 48f)
            {
                Smooth01((u - 28f) / 20f, out float s, out float ds);
                step = 3.5f + 6.5f * s;
                dstep = 6.5f * ds / 20f;
            }
            else { step = 10f; dstep = 0f; }
            float ripple = 0.6f * MathF.Sin(z * 0.07f);
            h = step + ripple;
            hx = sign * dstep;
            hz = 0.6f * 0.07f * MathF.Cos(z * 0.07f);
        }

        /// <summary>
        /// 계곡 — 맵 한가운데(x=Center)를 가로지르는 도랑. TwinHills(언덕 두 개, 국소 반경)의 반대꼴로,
        /// **x 하나에만** 의존해 z 전체를 가로지르는 굴곡을 만든다(사거리 축과 수직인 벽). 낮은 가운데를 넘어
        /// 반대쪽 스폰까지 아크를 쏴야 하므로 TwinHills 의 "옆으로 비켜 쏘기"와는 다른 조준을 요구한다.
        /// `Bump` 를 그대로 못 쓰는 이유: `Bump` 는 (x,z) 둘 다에서 거리를 재는 원형 범프라 국소 언덕이 된다 —
        /// 여기서는 z 에 무관해야 도랑이 폭 전체를 가로지른다.
        /// </summary>
        static void EvalValley(float x, float z, out float h, out float hx, out float hz)
        {
            float dx = x - Center;
            float half = MapSize * 0.30f;
            const float depth = 9f, baseH = 8f;
            float u = 1f - (dx * dx) / (half * half);
            if (u <= 0f) { h = baseH; hx = 0f; }
            else
            {
                h = baseH - depth * u * u;
                hx = 4f * depth * u * dx / (half * half);   // d/dx[-depth·(1-dx²/half²)²]
            }
            hz = 0f;
            AddDetailRipple(x, z, 0.7f, 0.045f, ref h, ref hx, ref hz);
        }

        /// <summary>능선 — <see cref="EvalValley"/> 와 정반대 부호. 도랑 대신 가운데에 벽을 세운다 —
        /// 저각으로는 아예 안 넘어가고 반드시 고각 아크가 필요하다.</summary>
        static void EvalRidge(float x, float z, out float h, out float hx, out float hz)
        {
            float dx = x - Center;
            float half = MapSize * 0.26f;
            const float height = 11f, baseH = 5f;
            float u = 1f - (dx * dx) / (half * half);
            if (u <= 0f) { h = baseH; hx = 0f; }
            else
            {
                h = baseH + height * u * u;
                hx = -4f * height * u * dx / (half * half);   // d/dx[+height·(1-dx²/half²)²]
            }
            hz = 0f;
            AddDetailRipple(x, z, 0.6f, 0.05f, ref h, ref hx, ref hz);
        }

        /// <summary>
        /// 황무지 — 낮은 능선 여러 개가 폭 전체에 반복된다(사인 곡선). 하나만 넘으면 되는 TwinHills·Ridge와
        /// 달리 **몇 번이고 걸린다** — 직사 궤적이 쭉 뚫리지 않는다. `AddDetailRipple` 과 같은
        /// `cos((x-Center)*k)` 짝함수 트릭을 진폭만 키워 쓴다 — x→MapSize-x 미러에서 항상 대칭이라
        /// 스폰 대칭 게이트(§맵 6종)가 자동으로 성립한다.
        /// </summary>
        static void EvalBadlands(float x, float z, out float h, out float hx, out float hz)
        {
            float k = MathF.PI * 3f / MapSize;   // 맵 폭에 능선 3개
            const float amp = 6f, baseH = 7f;
            float dx = x - Center;
            float cxm = MathF.Cos(dx * k);
            h = baseH + amp * cxm;
            hx = -amp * k * MathF.Sin(dx * k);
            hz = 0f;
            AddDetailRipple(x, z, 0.5f, 0.05f, ref h, ref hx, ref hz);
        }

        static void Smooth01(float t, out float s, out float ds)
        {
            if (t <= 0f) { s = 0f; ds = 0f; return; }
            if (t >= 1f) { s = 1f; ds = 0f; return; }
            s = t * t * (3f - 2f * t);
            ds = 6f * t * (1f - t);
        }
    }
}
