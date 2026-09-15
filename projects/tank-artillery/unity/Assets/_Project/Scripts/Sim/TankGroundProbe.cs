// 명세: docs/GAME_SPEC_TANK_ARTILLERY.md §7-6-(2)
//
// === SDF 크레이터 벽은 수직이거나 오버행이다 ===
// Heightfield 때는 안식각 릴랙세이션으로 벽을 42° 아래로 눕힐 수 있었다. SDF는 볼륨이라 같은 수법을 못 쓴다.
//
// 그래서 지형을 고치는 대신 **이동 규칙**으로 푼다:
//   - 탱크는 차체 높이 절반(1.2m) 이하의 수직 턱을 자동으로 넘는다
//   - 그보다 깊은 구덩이는 전술적 위험으로 남긴다 — "적을 구덩이에 가두기"가 SDF가 준 새 전술이다
//
// ⚠️ 자동 구제(텔레포트·무료 이동·지형 복구)를 넣지 마라.
//    포격으로 만든 지형이 무의미해진다. 갇힘은 숨기지 말고 HUD로 알린다(§7-6-2).

using System;

namespace Tankfall.Sim
{
    public static class TankGroundProbe
    {
        /// <summary>자동으로 넘는 수직 턱(m). 차체 높이의 절반.</summary>
        public const float StepHeight = 1.2f;

        /// <summary>갇힘 판정에서 주위를 찔러보는 거리(m). 한 턴 최소 이동 단위쯤.</summary>
        public const float ProbeDistance = 1.5f;

        /// <summary>
        /// 이동을 걸음으로 쪼갤 때의 한 걸음(m). 탱크는 연속 이동하므로 작아야 한다.
        /// ⚠️ 이 값이 등반 가능 여부를 직접 바꾼다 — 구 크레이터 가장자리는 수직이라,
        ///    가장자리 ε 구간의 상승량이 √(2Rε) 다. R=3m 기준 걸음 0.5m면 1.73m(턱 초과, 막힘),
        ///    0.25m면 1.22m(경계), 0.1m면 0.77m(통과). 실측으로 확인했다.
        /// </summary>
        public const float WalkStep = 0.25f;

        /// <summary>지면을 못 찾았을 때</summary>
        public const float NoGround = float.NegativeInfinity;

        /// <summary>
        /// (x,z)에서 <paramref name="fromY"/> 아래로 내려가며 첫 지면을 찾는다.
        /// ⚠️ 위에서부터 찾으면 안 된다 — 오버행 아래에 있는 탱크가 처마 위로 순간이동한다.
        /// </summary>
        public static float GroundBelow(SdfVolume vol, float x, float z, float fromY, float minY = -19f)
        {
            float step = vol.Voxel * 0.5f;
            float prevY = fromY + StepHeight;          // 발밑보다 살짝 위에서 시작
            float prev = vol.SampleWorld(x, prevY, z);
            for (float y = prevY - step; y >= minY; y -= step)
            {
                float cur = vol.SampleWorld(x, y, z);
                if (prev > 0f && cur <= 0f)            // 공기 → 땅
                {
                    float t = prev / (prev - cur);
                    return prevY + (y - prevY) * t;
                }
                prev = cur; prevY = y;
            }
            return NoGround;
        }

        public enum MoveResult { Ok, TooSteep, NoGround }

        /// <summary>
        /// (fromX,fromZ)의 지면 <paramref name="fromGroundY"/> 에서 (toX,toZ)로 한 걸음 옮길 수 있는가.
        /// 올라가는 쪽만 막는다 — 내려가는 건 낙하로 처리한다(§6-3 낙하 피해).
        /// </summary>
        public static MoveResult CanStepTo(SdfVolume vol, float fromGroundY, float toX, float toZ,
                                           out float toGroundY, float stepHeight = StepHeight)
        {
            toGroundY = GroundBelow(vol, toX, toZ, fromGroundY + stepHeight);
            if (float.IsNegativeInfinity(toGroundY)) return MoveResult.NoGround;
            return (toGroundY - fromGroundY) <= stepHeight ? MoveResult.Ok : MoveResult.TooSteep;
        }

        /// <summary>
        /// 8방향 전부 막혔으면 갇힘. 반환 = 나갈 수 있는 방향 수(0이면 갇힘).
        /// 이 값을 HUD에 그대로 쓴다 — 플레이어가 "한 방향만 남았다"를 알아야 한다.
        /// </summary>
        public static int EscapeRoutes(SdfVolume vol, float x, float z, float groundY,
                                       float probe = ProbeDistance, float stepHeight = StepHeight)
        {
            int open = 0;
            for (int d = 0; d < 8; d++)
            {
                float a = d * (MathF.PI * 2f / 8f);
                float nx = x + MathF.Cos(a) * probe;
                float nz = z + MathF.Sin(a) * probe;
                if (CanStepTo(vol, groundY, nx, nz, out _, stepHeight) == MoveResult.Ok) open++;
            }
            return open;
        }
    }
}
