// 명세: docs/GAME_SPEC_TANK_ARTILLERY.md §2-9-11 (헬리콥터 보급)
//
// === 출처 ===
// 원작 포트리스2 의 헬기 보급 규칙은 **기억이 아니라 조사**로 가져왔다(오너 규칙: 외부 게임 규칙은
// 조사하고 출처를 코드 머리말에 남긴다).
//   https://namu.wiki/w/포트리스2        (2026-09-16 조회)
//   https://namu.wiki/w/포트리스2/아이템  (2026-09-16 조회)
// 조사 결과 확인된 원작 규칙:
//   · 플레이 도중 **일정 확률로 헬기가 나타나 랜덤 아이템 상자를 떨구고 사라진다**.
//   · **이동으로 상자를 얻으면** 추가 아이템을 얻는다(= 주우려면 그 자리로 가야 한다).
//   · **상자를 파괴하면 그 안의 아이템도 사라진다**(= 적이 못 줍게 부수는 선택지가 있다).
// 확률·개수·주기 같은 구체 수치는 문서에 없다 — 아래 상수는 전부 [추정]이며 이 파일에 모아뒀다.

using System;
using System.Collections.Generic;

namespace Tankfall.Sim
{
    /// <summary>
    /// 헬기가 떨어뜨린 보급 상자들. **게임과 하네스가 같은 이 클래스를 쓴다**
    /// (한쪽만 보급을 받으면 승률이 게임의 승률이 아니게 된다 — §2-9-1 의 교훈).
    /// </summary>
    public class SupplyDrop
    {
        /// <summary>한 라운드에 헬기가 뜰 확률 [추정].</summary>
        public const float DropChancePerRound = 0.5f;
        /// <summary>한 번 뜰 때 떨구는 상자 수 [추정].</summary>
        public const int CratesPerDrop = 1;
        /// <summary>이 거리 안에 들어오면 줍는다 [추정] — 탱크 반경(2m)보다 넉넉해야 "지나가다 줍는" 느낌이 난다.</summary>
        public const float PickupRadius = 4.0f;
        /// <summary>폭발 중심이 이 거리 안이면 상자가 부서진다 [추정].</summary>
        public const float DestroyRadius = 5.0f;
        /// <summary>동시에 맵에 남을 수 있는 상자 수 [추정] — 안 주우면 쌓이기만 하는 걸 막는다.</summary>
        public const int MaxCrates = 3;

        public struct Crate
        {
            public float X, Y, Z;
            public ItemKind Item;
        }

        readonly List<Crate> _crates = new();
        public int Count => _crates.Count;
        public IReadOnlyList<Crate> Crates => _crates;

        public void Clear() => _crates.Clear();

        /// <summary>
        /// 라운드가 바뀔 때 부른다. 확률에 걸리면 상자를 떨군다. 반환값은 이번에 떨군 상자 수.
        /// 놓을 자리(지면 높이)는 호출부가 안다 — Sim 은 지형 샘플만 받으면 되므로 델리게이트로 받는다.
        ///
        /// 상자가 **닿을 수 있는 자리**에 떨어지게 하려면 탱크 위치를 알아야 한다.
        /// ⚠️ 처음엔 맵 전역(15~85%)에 무작위로 떨궜더니 **투하 1.99 / 획득 0.11** 이 나왔다 — 헬기가 장식이었다.
        ///    원작은 2D 좁은 전장이라 어디 떨어져도 닿지만, 이 게임은 200m 맵에 두 팀이 양 끝(x=42·158)에 있고
        ///    한 턴에 8m 밖에 못 움직인다. 그래서 **아무 탱크 하나를 골라 그 근처**에 떨군다 [추정] —
        ///    "무작위 위치"라는 원작 성격은 지키면서(어느 팀 근처일지는 무작위) 닿을 수는 있게 한다.
        /// </summary>
        public const float AnchorMin = 8f, AnchorMax = 26f;   // 앵커에서 이만큼 떨어진 자리 [추정]

        public int RollDrop(ref Rng rng, float mapSize, Func<float, float, float> groundAt,
                            IReadOnlyList<Vec3> anchors = null)
        {
            if (_crates.Count >= MaxCrates) return 0;
            if (rng.Float01() >= DropChancePerRound) return 0;
            int made = 0;
            for (int i = 0; i < CratesPerDrop && _crates.Count < MaxCrates; i++)
            {
                // 맵 가장자리는 피한다 — 못 가는 자리에 떨구면 보급이 아니라 장식이다.
                for (int tries = 0; tries < 12; tries++)
                {
                    float x, z;
                    if (anchors != null && anchors.Count > 0)
                    {
                        var a = anchors[Math.Min((int)(rng.Float01() * anchors.Count), anchors.Count - 1)];
                        float ang = rng.Range(0f, MathF.PI * 2f), rad = rng.Range(AnchorMin, AnchorMax);
                        x = a.X + MathF.Cos(ang) * rad;
                        z = a.Z + MathF.Sin(ang) * rad;
                        if (x < mapSize * 0.06f || x > mapSize * 0.94f || z < mapSize * 0.06f || z > mapSize * 0.94f) continue;
                    }
                    else
                    {
                        x = rng.Range(mapSize * 0.15f, mapSize * 0.85f);
                        z = rng.Range(mapSize * 0.15f, mapSize * 0.85f);
                    }
                    float g = groundAt(x, z);
                    if (float.IsNegativeInfinity(g) || float.IsNaN(g)) continue;
                    var all = Items.All();
                    _crates.Add(new Crate { X = x, Y = g, Z = z, Item = all[Math.Min((int)(rng.Float01() * all.Length), all.Length - 1)] });
                    made++;
                    break;
                }
            }
            return made;
        }

        /// <summary>
        /// 이 자리에 있는 유닛이 상자를 줍는다(원작: "이동으로 상자를 얻으면 추가 아이템").
        /// 주운 아이템을 반환하고 상자는 사라진다. 없으면 ItemKind.None.
        /// </summary>
        public ItemKind TryPickup(float x, float y, float z)
        {
            for (int i = 0; i < _crates.Count; i++)
            {
                var c = _crates[i];
                float dx = c.X - x, dy = c.Y - y, dz = c.Z - z;
                if (dx * dx + dy * dy + dz * dz > PickupRadius * PickupRadius) continue;
                _crates.RemoveAt(i);
                return c.Item;
            }
            return ItemKind.None;
        }

        /// <summary>
        /// 폭발로 상자가 부서진다(원작: "상자를 파괴하면 그 안에 있던 아이템도 사라진다").
        /// 적이 못 줍게 부수는 것도 전술이므로 **아이템은 아무에게도 안 간다**.
        /// </summary>
        /// <returns>부순 상자 수</returns>
        public int DestroyNear(float x, float y, float z, float radius)
        {
            float r = radius + DestroyRadius;
            int n = 0;
            for (int i = _crates.Count - 1; i >= 0; i--)
            {
                var c = _crates[i];
                float dx = c.X - x, dy = c.Y - y, dz = c.Z - z;
                if (dx * dx + dy * dy + dz * dz > r * r) continue;
                _crates.RemoveAt(i);
                n++;
            }
            return n;
        }

        /// <summary>지형이 깎이면 상자도 같이 내려앉는다 — 공중에 뜬 상자는 주울 수 없다.</summary>
        public void Settle(Func<float, float, float> groundAt)
        {
            for (int i = 0; i < _crates.Count; i++)
            {
                var c = _crates[i];
                float g = groundAt(c.X, c.Z);
                if (float.IsNegativeInfinity(g) || float.IsNaN(g)) continue;
                c.Y = g;
                _crates[i] = c;
            }
        }

        /// <summary>가장 가까운 상자. AI 가 주우러 갈지 판단할 때 쓴다. 없으면 false.</summary>
        public bool Nearest(float x, float z, out float cx, out float cz, out float dist)
        {
            cx = 0f; cz = 0f; dist = float.MaxValue;
            for (int i = 0; i < _crates.Count; i++)
            {
                var c = _crates[i];
                float dx = c.X - x, dz = c.Z - z;
                float d = MathF.Sqrt(dx * dx + dz * dz);
                if (d >= dist) continue;
                dist = d; cx = c.X; cz = c.Z;
            }
            return dist < float.MaxValue;
        }
    }
}
