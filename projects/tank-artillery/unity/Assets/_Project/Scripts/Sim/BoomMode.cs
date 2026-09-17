// 명세: docs/GAME_SPEC_TANK_ARTILLERY.md §2-9-16 (Boom 모드 — 지뢰밭·지진·유성)
//
// === 출처 ===
// 원작 포트리스2 의 Boom 모드 규칙은 **기억이 아니라 조사**로 가져왔다(오너 규칙).
//   https://namu.wiki/w/포트리스2   (2026-09-17 조회)
// 확인된 원문:
//   · 지뢰밭 "곳곳에 마인랜더의 지뢰가 드문드문 깔린 상태로 게임이 시작된다."
//   · 지진   "턴이 시작되기 전에 확률적으로 지진이 일어난다. 지진이 일어나면 각이 랜덤하게 변하고
//            탱의 위치가 미세하게 이동된다."
//   · 유성   "턴이 시작되기 전에 확률적으로 플레이어 중 한 명의 탱이 있는 곳과 그 근처에 메테오가 떨어진다."
// 확률·개수·세기는 원문에 없다 — 아래 상수는 전부 [추정]이고 이 파일에 모아뒀다.
//
// === 설계 ===
// 세 규칙 모두 **이미 있는 시스템의 재사용**이다(아이템 §2-9-10 과 같은 원칙):
//   지뢰밭 = HazardField.PlaceMine, 지진 = 유닛 좌표·각도 흔들기, 유성 = 착탄 처리(굴착+피해) 그대로.
// Sim 은 지형 굴착·피해 계산을 직접 하지 않으므로(호출부가 SdfDeformer 를 쥔다) **무엇이 어디서
// 일어나는지만 정해서 돌려주고**, 실제 적용은 게임·하네스가 각자 자기 방식으로 한다.
// 그래야 게임과 하네스가 같은 규칙·같은 확률을 쓰면서도 asmdef 규칙(UnityEngine 참조 금지)을 지킨다.

using System;
using System.Collections.Generic;

namespace Tankfall.Sim
{
    public static class BoomMode
    {
        /// <summary>판 시작에 깔리는 지뢰 수 [추정] — "드문드문" 이라 맵(200m)에 비해 성기게.</summary>
        public const int MineFieldCount = 14;
        /// <summary>깔린 지뢰의 반경·피해 [추정] — 마인랜더 2번탄과 같은 값을 쓴다(원작이 "마인랜더의 지뢰"라 한다).</summary>
        public const float MineRadius = 4f;
        public const int MineDamage = 120;

        /// <summary>턴 시작에 지진이 날 확률 [추정].</summary>
        public const float QuakeChance = 0.12f;
        /// <summary>지진이 탱크를 밀어내는 거리 [추정] — 원작 "미세하게 이동된다".</summary>
        public const float QuakeShift = 2.5f;
        /// <summary>지진이 포각을 흔드는 폭(도) [추정] — 원작 "각이 랜덤하게 변하고".</summary>
        public const float QuakePitchJitter = 18f;

        /// <summary>턴 시작에 유성이 떨어질 확률 [추정].</summary>
        public const float MeteorChance = 0.10f;
        /// <summary>한 번에 떨어지는 유성 수 [추정] — 원작 "탱이 있는 곳과 그 근처에".</summary>
        public const int MeteorCount = 3;
        /// <summary>표적 탱크 주변 흩어짐 반경 [추정].</summary>
        public const float MeteorSpread = 14f;
        public const float MeteorCrater = 6.5f;
        public const float MeteorBlast = 7.5f;
        public const int MeteorDamage = 180;

        /// <summary>지뢰밭에 깔 자리들. 높이는 호출부가 지형에서 찾는다.</summary>
        public static void RollMineField(ref Rng rng, float mapSize, List<(float X, float Z)> into)
        {
            into.Clear();
            for (int i = 0; i < MineFieldCount; i++)
                into.Add((rng.Range(mapSize * 0.12f, mapSize * 0.88f),
                          rng.Range(mapSize * 0.12f, mapSize * 0.88f)));
        }

        /// <summary>이번 턴에 지진이 나는가.</summary>
        public static bool RollQuake(ref Rng rng) => rng.Float01() < QuakeChance;

        /// <summary>지진이 한 탱크를 얼마나 밀지 [추정]. 각도 흔들림도 같이 준다.</summary>
        public static void QuakeShiftFor(ref Rng rng, out float dx, out float dz, out float dPitch)
        {
            float a = rng.Range(0f, MathF.PI * 2f);
            float d = rng.Range(0f, QuakeShift);
            dx = MathF.Cos(a) * d;
            dz = MathF.Sin(a) * d;
            dPitch = rng.Range(-QuakePitchJitter, QuakePitchJitter);
        }

        /// <summary>이번 턴에 유성이 떨어지는가.</summary>
        public static bool RollMeteor(ref Rng rng) => rng.Float01() < MeteorChance;

        /// <summary>
        /// 유성이 떨어질 자리들 — 원작 "플레이어 중 한 명의 탱이 있는 곳과 그 근처".
        /// 첫 발은 그 탱크 바로 위, 나머지는 주변에 흩뿌린다 [추정].
        /// </summary>
        public static void MeteorSpots(ref Rng rng, float tx, float tz, List<(float X, float Z)> into)
        {
            into.Clear();
            into.Add((tx, tz));
            for (int i = 1; i < MeteorCount; i++)
            {
                float a = rng.Range(0f, MathF.PI * 2f);
                float d = rng.Range(3f, MeteorSpread);
                into.Add((tx + MathF.Cos(a) * d, tz + MathF.Sin(a) * d));
            }
        }
    }
}
