// 명세: docs/GAME_SPEC_TANK_ARTILLERY.md §10(AI), §7-6(이동 규칙), §28(발밑 굴착)
//
// ══════════════════════════════════════════════════════════════════════════
//  왜 이 파일이 생겼나 (2026-09-17)
// ══════════════════════════════════════════════════════════════════════════
// **게임의 AI 는 한 번도 움직이지 않았다.** `BattleDemo` 의 페이즈 머신이 AI 턴에서
// `Phase.Move` 를 건너뛰고 곧장 `AiThink` 로 갔고, `Sim/` 어디에도 이동 판단이 없었다.
// 조준(AiGunner)만 SIM 에 있고 이동은 **아무도 구현한 적이 없던** 축이다.
//
// 그래서 세 가지가 조용히 깨져 있었다:
//   1. AI 가 자기가 판 구덩이에서 못 나온다. 굴착탄이 판을 길게 만들면 탱크가 사발 바닥에
//      갇혀 남은 판을 통째로 허비한다(§28 "발밑을 파서 떨어뜨린다"가 영구 무력화가 된다).
//   2. **헬기 보급을 게임에서는 AI 가 절대 안 줍는다.** 원작에서 보급은 "이동으로 줍는" 것이라
//      (SupplyDrop 머리말) 안 움직이는 AI 에게 헬기는 장식이다.
//   3. 독·불 장판에서 안 나온다. `AiGunner.EffectValue` 는 "장판은 적이 나가면 끊기지만
//      나가려면 이동을 써야 한다"를 전제로 값을 매기는데, AI 는 나갈 수가 없었다.
//
// ⚠️ **그런데 하네스(`tools/BattleSimVerify.cs`)에는 이동 모델이 있었다** — "40% 턴에 임의 방향
//    8m + 상자 탐색". 즉 **하네스가 게임보다 잘 움직였다.** 승률을 게임이 아닌 것에서 재고 있었다는 뜻이다.
//    이 파일의 존재 이유 절반은 그 어긋남을 없애는 것이다: 게임과 하네스가 **같은 함수**를 부른다
//    (§2-9-1 "같은 로직이 여러 곳에 살면 반드시 어긋난다" — 걸음 크기가 1m vs 0.25m 로 갈렸던
//     2026-09-16 사고가 정확히 이 파일이 없어서 난 것이다).
//
// ⚠️ UnityEngine 을 참조하지 마라. asmdef `noEngineReferences`(§4-1) 가 막고,
//    `verify.sh compile` 의 첫 게이트가 이걸 검사한다.
// ⚠️ 난수는 전부 `ref Rng` 로 받는다. 리플레이·서버 재현·하네스 재현이 여기 걸려 있다.
// ⚠️ `TankGroundProbe` 의 `StepHeight`/`WalkStep` 을 여기서 바꾸지 마라 — 그 상수가 곧
//    "넘을 수 있는 턱"이라, 키우면 갇힘이라는 전술 자체가 사라진다(TankGroundProbe 머리말).

using System;
using System.Collections.Generic;

namespace Tankfall.Sim
{
    /// <summary>왜 움직였나. 로그·검증에 쓴다 — 이유가 안 남으면 "움직이긴 하는데 왜인지 모르는" AI 가 된다.</summary>
    public enum MoveReason { None, CannotMove, CraterEscape, HazardEscape, Supply, Range, Wander }

    public struct MovePlan
    {
        public bool Move;
        public float DirX, DirZ;     // 단위 벡터
        public float Distance;       // 걷고 싶은 거리(m). 호출부가 이동 게이지로 더 줄여도 된다.
        public MoveReason Why;
    }

    public static class AiMover
    {
        /// <summary>사발인지 보려고 둘러보는 반경(m) [추정]. 폭발 반경(≈7m)보다 커야 크레이터를 통째로 넘어다본다.</summary>
        public const float PitProbeRadius = 8f;

        /// <summary>상자를 주우러 갈 최대 거리(m). 한 턴 8m 라 여러 턴에 걸쳐 간다(하네스 실측으로 정해진 값).</summary>
        public const float SupplySeekRange = 45f;

        /// <summary>장판 밖으로 이만큼 더 나간다(m) — 경계에 서면 다음 턴에 다시 들어간다.</summary>
        public const float HazardMargin = 3f;

        /// <summary>아무 용무가 없을 때 그냥 움직일 확률 [추정]. 하네스의 옛 임의걸음과 같은 값이라 지뢰·지속불 측정이 안 바뀐다.</summary>
        public const float WanderChance = 0.40f;

        /// <summary>맵 가장자리에서 이만큼 안쪽까지만 간다(m).</summary>
        public const float EdgeInset = 5f;

        /// <summary>한 턴 이동 거리(m). 게임의 이동 게이지가 이걸 다시 자른다.</summary>
        public const float TurnDistance = 8f;

        // ──────────────────────────────────────────────────────────────
        //  사발 판정
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 지금 자리가 **움푹한 데 갇혀 있는가.** 반경 <see cref="PitProbeRadius"/> 로 8방향을 찔러,
        /// 그 높이차의 **중앙값**이 턱(StepHeight)보다 크면 사발 안이다. 방향은 **가장 가파르게 오르는 쪽** —
        /// 그쪽이 테두리다.
        ///
        /// ⚠️ 처음엔 "8방향이 **전부** 높으면 사발"로 했다가 실측에서 깨졌다(반경 14m 구덩이 탈출 7/20,
        ///    임의걸음 6/20 과 사실상 같음). 이유는 **벽에 올라타는 순간 판정이 꺼지는 것**이었다:
        ///    바닥에서 한 턴(8m) 걸어 벽 중턱에 서면 **뒤쪽(=바닥)이 나보다 낮다** → "전부 높다"가 깨져
        ///    Wander 로 넘어가고, 다음 턴에 아무 방향으로나 굴러 도로 미끄러진다. 나갔다 들어왔다를 반복했다.
        ///    중앙값은 벽 중턱에서도 여전히 양수라(뒤 한 쪽만 낮다) **오르는 도중에 판정이 안 꺼진다.**
        ///    상태를 들고 다니지 않고 히스테리시스를 얻는 방법이다 — 호출부가 기억할 게 없다.
        ///
        /// ⚠️ 비탈은 사발이 아니다. 균일한 비탈은 오르막 절반·내리막 절반이라 중앙값이 0 근처다.
        /// ⚠️ `TankGroundProbe.EscapeRoutes` 로는 이걸 못 판다. 그건 1.5m 한 걸음만 보므로
        ///    사발 바닥에서 옆 바닥으로 가는 것도 "탈출로"로 세어 버린다
        ///    (GameplayVerify.WalksOut 머리말이 실측으로 잡아 둔 함정이다).
        /// </summary>
        /// <param name="bestDirX">사발일 때 나갈 방향(가장 가파르게 오르는 쪽 = 테두리 쪽).</param>
        public static bool InPit(SdfVolume vol, float x, float z, float groundY,
                                 out float bestDirX, out float bestDirZ,
                                 float radius = PitProbeRadius)
        {
            bestDirX = 0f; bestDirZ = 0f;
            if (vol == null) return false;

            Span<float> rises = stackalloc float[8];
            int n = 0;
            // 방향은 두 갈래로 고른다.
            //   ① **첫 걸음이 실제로 걸리는** 오르막 중 가장 완만한 쪽 — 이게 정답이다.
            //   ② 하나도 안 걸리면 가장 가파른 쪽(어차피 갇혔고, 적어도 테두리를 향한다).
            //
            // ⚠️ ①의 "첫 걸음이 걸리는지"를 빼면 안 된다. 그것 없이 **가장 가파른 쪽**만 고르게 했더니
            //    반경 14m 구덩이에서 0/20 이 나왔다 — 임의걸음(6/20)보다도 나빴다. 가장 가파른 벽은
            //    곧 **못 오르는 벽**이라, 매 턴 같은 벽에 머리를 박고 결정론적으로 한 걸음도 못 갔다.
            //    임의걸음은 매번 다른 방향을 뽑아 우연히 넘었던 것이다. "막히면 다른 데를 본다"가 핵심.
            float gentlestUp = float.MaxValue, steepest = float.MinValue;
            float upX = 0f, upZ = 0f, steepX = 0f, steepZ = 0f;
            bool haveUp = false;

            for (int d = 0; d < 8; d++)
            {
                float a = d * (MathF.PI * 2f / 8f);
                float dx = MathF.Cos(a), dz = MathF.Sin(a);
                // 충분히 위에서 내려찍는다 — 사발 밖이 훨씬 높을 수 있다.
                float g = TankGroundProbe.GroundBelow(vol, x + dx * radius, z + dz * radius, groundY + radius + 30f);
                // 지면이 없는 쪽(맵 밖·허공)은 "나갈 수 있는 곳"이 아니라 **판정에서 뺀다** —
                // 없는 쪽을 낮은 쪽으로 세면 탱크가 맵 밖으로 걸어 나간다.
                if (float.IsNegativeInfinity(g)) continue;

                float rise = g - groundY;
                rises[n++] = rise;
                if (rise > steepest) { steepest = rise; steepX = dx; steepZ = dz; }

                if (rise <= 0f) continue;                    // 내리막 = 사발 안쪽. 그리로 가면 도로 미끄러진다.
                float sx = x + dx * TankGroundProbe.WalkStep, sz = z + dz * TankGroundProbe.WalkStep;
                if (TankGroundProbe.CanStepTo(vol, groundY, sx, sz, out _) != TankGroundProbe.MoveResult.Ok) continue;
                if (rise < gentlestUp) { gentlestUp = rise; upX = dx; upZ = dz; haveUp = true; }
            }
            if (n < 5) return false;          // 절반 넘게 지면을 못 찾았다 — 판단하지 않는다
            if (haveUp) { bestDirX = upX; bestDirZ = upZ; }
            else { bestDirX = steepX; bestDirZ = steepZ; }

            // 중앙값. 8개뿐이라 삽입 정렬이 가장 싸고, 할당이 없어야 매 턴 불러도 부담이 없다.
            for (int i = 1; i < n; i++)
            {
                float v = rises[i]; int j = i - 1;
                while (j >= 0 && rises[j] > v) { rises[j + 1] = rises[j]; j--; }
                rises[j + 1] = v;
            }
            float median = (n % 2 == 1) ? rises[n / 2] : (rises[n / 2 - 1] + rises[n / 2]) * 0.5f;
            return median > TankGroundProbe.StepHeight;
        }

        // ──────────────────────────────────────────────────────────────
        //  판단
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 이번 턴에 어디로 갈지. **게임과 하네스가 이 함수 하나를 같이 부른다.**
        ///
        /// 우선순위: ① 사발 탈출 ② 장판 탈출 ③ 보급 상자 ④ 사거리 확보 ⑤ 그냥 움직이기
        ///
        /// ⚠️ ①이 ②보다 먼저인 이유: 사발 안에서는 **어느 방향으로든 못 나간다.** 장판이 크레이터에
        ///    떨어져 있으면 장판 탈출 방향이 곧 벽이라, 사발부터 벗어나야 장판도 벗어난다.
        ///    실제로 크레이터에 불이 떨어지는 경우가 흔하다(같은 착탄점이 둘 다 만든다).
        /// </summary>
        /// <param name="canMove">속박(포세이돈 2번탄 등)에 걸리지 않았는가. <see cref="StatusEffects"/> 가 정한다.</param>
        /// <param name="maxRange">이 탱크의 최대 사거리(m). 적이 이보다 멀면 붙는다.</param>
        public static MovePlan Decide(SdfVolume vol, float x, float y, float z, bool canMove,
                                      float maxRange, float mapSize,
                                      IReadOnlyList<HazardField.HazardView> hazards,
                                      SupplyDrop supply,
                                      IReadOnlyList<AiGunner.Target> enemies,
                                      ref Rng rng)
        {
            var plan = new MovePlan { Why = MoveReason.None };
            if (!canMove) { plan.Why = MoveReason.CannotMove; return plan; }

            // ① 사발 탈출 — 가장 얕은 턱 쪽으로
            if (InPit(vol, x, z, y, out float px, out float pz))
                return Go(px, pz, MoveReason.CraterEscape);

            // ② 장판 탈출 — 밟고 서 있는 장판(불·독)의 중심 반대로.
            //    지뢰(Kind 0)는 **밟고 선 게 아니라 다가가면 터지는 것**이라 여기서 안 센다.
            if (hazards != null)
            {
                float worstOverlap = 0f, hx = 0f, hz = 0f;
                for (int i = 0; i < hazards.Count; i++)
                {
                    var h = hazards[i];
                    if (h.Kind == 0) continue;                       // 지뢰는 장판이 아니다
                    float dx = x - h.X, dz = z - h.Z;
                    float d = MathF.Sqrt(dx * dx + dz * dz);
                    float overlap = h.Radius - d;
                    if (overlap > worstOverlap) { worstOverlap = overlap; hx = dx; hz = dz; }
                }
                if (worstOverlap > 0f)
                {
                    // 중심에 정확히 서 있으면 방향이 0 이다 — 아무 쪽이나 고르되 결정론을 지킨다.
                    float len = MathF.Sqrt(hx * hx + hz * hz);
                    if (len < 1e-4f) { float a = rng.Range(0f, MathF.PI * 2f); hx = MathF.Cos(a); hz = MathF.Sin(a); len = 1f; }
                    return Go(hx / len, hz / len, MoveReason.HazardEscape, worstOverlap + HazardMargin);
                }
            }

            // ③ 보급 상자 — 원작에서 보급은 "이동으로 줍는" 것이다(SupplyDrop 머리말).
            if (supply != null && supply.Count > 0 && supply.Nearest(x, z, out float cx, out float cz, out float cd)
                && cd > 1e-3f && cd <= SupplySeekRange)
                return Go((cx - x) / cd, (cz - z) / cd, MoveReason.Supply, cd);

            // ④ 사거리 확보 — 못 닿는 적에게 붙는다.
            //    ⚠️ 여유를 둔다(0.95). 사거리 경계에 딱 서면 바람 한 번에 다시 못 닿는다.
            if (enemies != null && enemies.Count > 0 && maxRange > 0f)
            {
                float near = float.MaxValue, ex = 0f, ez = 0f;
                for (int i = 0; i < enemies.Count; i++)
                {
                    float dx = enemies[i].Center.X - x, dz = enemies[i].Center.Z - z;
                    float d = MathF.Sqrt(dx * dx + dz * dz);
                    if (d < near) { near = d; ex = dx; ez = dz; }
                }
                if (near > maxRange * 0.95f && near > 1e-3f)
                    return Go(ex / near, ez / near, MoveReason.Range, near - maxRange * 0.95f);
            }

            // ⑤ 그냥 움직이기 — 지뢰·지속불은 누가 밟아야 값이 매겨진다.
            //    하네스의 옛 임의걸음(40%)과 같은 확률이라 이 파일이 들어와도 그 측정이 안 흔들린다.
            if (rng.Float01() < WanderChance)
            {
                float a = rng.Range(0f, MathF.PI * 2f);
                return Go(MathF.Cos(a), MathF.Sin(a), MoveReason.Wander);
            }
            return plan;

            MovePlan Go(float dx2, float dz2, MoveReason why, float want = TurnDistance)
                => new MovePlan { Move = true, DirX = dx2, DirZ = dz2, Why = why, Distance = MathF.Min(want, TurnDistance) };
        }

        // ──────────────────────────────────────────────────────────────
        //  걷기
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 계획한 방향으로 실제로 걷는다. **게임과 하네스가 이 함수를 같이 쓴다** — 걸음 크기가
        /// 두 곳에 따로 적혀 1m vs 0.25m 로 갈렸던 2026-09-16 사고를 구조적으로 막는 게 목적이다.
        ///
        /// ⚠️ 걸음은 반드시 <see cref="TankGroundProbe.WalkStep"/>(0.25m)이다. 크레이터 가장자리의
        ///    상승량은 √(2Rε) 라 걸음이 커지면 게임이라면 넘었을 턱에 걸린다(TankGroundProbe 머리말).
        /// ⚠️ 막히면 포기하지 말고 옆으로 돌아간다. 직선으로만 걸으면 언덕 턱에 걸려 상자 앞에서 멈춘다 —
        ///    하네스 진단에서 "상자를 주우러 간 턴 4.62 중 3.61 이 막힘"이었던 그 실패다.
        /// </summary>
        /// <returns>실제로 걸은 거리(m). 0 이면 한 걸음도 못 갔다(갇힘).</returns>
        public static float Walk(SdfVolume vol, ref float x, ref float z, ref float groundY,
                                 float dirX, float dirZ, float distance, float mapSize,
                                 float edgeInset = EdgeInset)
        {
            if (vol == null || distance <= 0f) return 0f;
            float len = MathF.Sqrt(dirX * dirX + dirZ * dirZ);
            if (len < 1e-4f) return 0f;
            dirX /= len; dirZ /= len;

            const float Step = TankGroundProbe.WalkStep;
            int steps = (int)(distance / Step);
            float walked = 0f;

            for (int i = 0; i < steps; i++)
            {
                bool stepped = false;
                // 0 → +0.6rad → -0.6rad. 정면이 막히면 비스듬히 돌아간다 [추정].
                for (int t = 0; t < 3; t++)
                {
                    float ang = t == 0 ? 0f : (t == 1 ? 0.6f : -0.6f);
                    float ca = MathF.Cos(ang), sa = MathF.Sin(ang);
                    float sx = (dirX * ca - dirZ * sa) * Step, sz = (dirX * sa + dirZ * ca) * Step;
                    float tx = x + sx, tz = z + sz;
                    if (tx < edgeInset || tx > mapSize - edgeInset || tz < edgeInset || tz > mapSize - edgeInset) continue;
                    if (TankGroundProbe.CanStepTo(vol, groundY, tx, tz, out float ty) != TankGroundProbe.MoveResult.Ok) continue;
                    x = tx; z = tz; groundY = ty; stepped = true; break;
                }
                if (!stepped) break;
                walked += Step;
            }
            return walked;
        }
    }
}
