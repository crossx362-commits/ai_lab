using UnityEngine;

namespace Ulon.Shared
{
    /// <summary>
    /// **던전 입구의 기하 원장** — 문이 어느 쪽을 보고, 오는 사람이 어디 서고, 문 뒤에 무엇이 있나.
    ///
    /// 왜 `Shared`인가: 지형(`WorldTerrain`)이 문 뒤에 언덕을 올리려면 **문의 방향**을 알아야 하는데,
    /// 그 규칙은 그동안 에디터(`VisualSliceBuilder.EntranceFront`)에만 있었다. 규칙이 두 벌이 되면
    /// 언덕과 문이 서로 다른 쪽을 보게 된다 — 그래서 **원장을 아래로 내리고 에디터는 부르기만 한다.**
    ///
    /// 「앞」은 이름이 아니라 **좌표가 정한다**(검수 원장 2026-09-09): `EntranceYaw`가 가리키는 쪽은
    /// 세 입구 모두 마을(원점) 쪽이었고, 이름만 「안쪽」이었다. 그래서 마을 방향과 대조해 고른다.
    /// </summary>
    public static class EntranceGeom
    {
        public struct Door
        {
            public float X, Z, Yaw;
            public string Root;
        }

        /// <summary>세 입구 — 지형·빌더·자가 **같은 목록**을 읽는다(하나가 빠지면 그 입구만 평지로 남는다).</summary>
        public static readonly Door[] All =
        {
            new Door { X = Dungeon1.EntranceX, Z = Dungeon1.EntranceZ, Yaw = Dungeon1.EntranceYaw, Root = Dungeon1.RootObject },
            new Door { X = Dungeon2.EntranceX, Z = Dungeon2.EntranceZ, Yaw = Dungeon2.EntranceYaw, Root = Dungeon2.RootObject },
            new Door { X = Dungeon3.EntranceX, Z = Dungeon3.EntranceZ, Yaw = Dungeon3.EntranceYaw, Root = Dungeon3.RootObject },
        };

        /// <summary>`EntranceYaw`가 가리키는 방향(평면).</summary>
        public static Vector2 Heading(float yaw)
        {
            float rad = yaw * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
        }

        /// <summary>오는 사람이 서는 쪽 — 마을(원점)이 있는 쪽이다.</summary>
        public static Vector2 Front(float x, float z, float yaw)
        {
            var h = Heading(yaw);
            return Vector2.Dot(h, new Vector2(-x, -z)) > 0f ? h : -h;
        }

        /// <summary>
        /// **문 뒤 언덕**(대장 승인 2026-09-09 ⓐ). 세 입구 모두 지면 10.00m 완전 평지·문 안팎 낙차
        /// 0.00m이라, 화면에서 「지하로 내려간다」가 아니라 「들판의 기념문」으로 읽혔다(셈 `dbc3fea5`).
        /// 소품(바위)으로 두르는 길은 두 판 다 근접 샷을 덮어 닫혔다 — 남은 것은 지형이다.
        ///
        /// 상수는 **한 벌**이고 세 입구가 같은 규칙을 쓴다(대장 조건 ①):
        /// - 언덕 중심은 문에서 뒤로 `MoundOffset`, 반경 `MoundRadius`, 높이 `MoundHeight`.
        ///   경사는 7m/18m ≈ 21°라 **벽 판정(50°)도 절벽 바위 규칙(32°)도 안 건드린다** —
        ///   지형 하나 올리면서 다른 자 셋을 흔들지 않기 위해 재서 고른 값이다.
        /// - 문 앞(진입로 쪽)과 문턱 `MouthClear`까지는 **0** — 문과 돌길이 언덕에 묻히면 안 된다.
        /// </summary>
        public const float MoundRadius = 18f;
        public const float MoundHeight = 7f;
        public const float MoundOffset = 10f;
        public const float MouthClear = 2.5f;      // 문 뒤 이만큼은 안 올린다(문틀·워프 착지 자리)
        public const float MouthFade = 4f;         // 그 뒤로 이만큼에 걸쳐 올라간다

        /// <summary>이 좌표에서 언덕이 지면을 얼마나 밀어 올리나(m). 0이면 평지 그대로.</summary>
        /// <summary>네거티브 컨트롤 전용 — 켜면 언덕 높이가 0이 된다(자가 언덕을 재고 있는지 확인용).</summary>
        public static bool MoundDisabled;

        public static float MoundRise(float wx, float wz)
        {
            if (MoundDisabled)
                return 0f;
            float rise = 0f;
            for (int i = 0; i < All.Length; i++)
            {
                var d = All[i];
                var front = Front(d.X, d.Z, d.Yaw);
                float behind = (wx - d.X) * -front.x + (wz - d.Z) * -front.y;   // 문 뒤로 얼마나 갔나
                if (behind <= MouthClear)
                    continue;                                                   // 문 앞·문턱은 평지
                float cx = d.X - front.x * MoundOffset, cz = d.Z - front.y * MoundOffset;
                float dist = Mathf.Sqrt((wx - cx) * (wx - cx) + (wz - cz) * (wz - cz));
                float radial = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(dist / MoundRadius));
                float gate = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((behind - MouthClear) / MouthFade));
                // 가장자리를 흔든다 — 매끈한 반구는 화면에서 「엎어 놓은 사발」로 읽힌다(지역 경계와 같은 기법).
                float wobble = Mathf.PerlinNoise(wx * 0.06f + 13.7f, wz * 0.06f + 4.9f) * 0.35f + 0.82f;
                rise = Mathf.Max(rise, MoundHeight * radial * gate * wobble);
            }
            return rise;
        }
    }
}
