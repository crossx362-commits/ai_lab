// 명세: docs/GAME_SPEC_TANK_ARTILLERY.md §2-9-15 (기후 — 증폭벽·회오리)
//
// === 출처 ===
// 원작 포트리스2 의 기후 규칙은 **기억이 아니라 조사**로 가져왔다(오너 규칙).
//   https://namu.wiki/w/포트리스2   (2026-09-17 조회)
// 확인된 원문:
//   · 증폭벽 "포탄이 이 증폭벽을 지나가면 포탄에 별똥이 생긴다. 이 별똥이 생긴 포탄은 대미지가 50% 증폭된다."
//   · 회오리 "포탄이 이 회오리에 걸리면 나선형으로 휘말려 올라가다가 포탄이 화면 위까지 넘어가면
//            높이 뜨다가 떨어진다."
// 위치·크기·발생 확률은 원문에 없다 — 이 파일의 상수는 전부 [추정]이고 여기 모아뒀다.
//
// === 왜 이 모양인가 ===
// 이 게임의 궤적은 **닫힌 해**다(§5-4). 회오리가 비행 중에 힘을 더하면 닫힌 해가 깨진다.
// 그래서 회오리는 "빨아올렸다가 떨어뜨린다"는 원작 서술 그대로 **두 토막의 닫힌 해**로 푼다 —
// 걸린 지점에서 한 번 끊고, 회오리 꼭대기에서 거의 수직으로 **다시 쏜 것처럼** 이어 붙인다.
// 원작의 "높이 뜨다가 떨어진다" 와 그림이 같고, 궤적 계산은 그대로 닫힌 해로 남는다.

using System;
using System.Collections.Generic;

namespace Tankfall.Sim
{
    /// <summary>증폭벽 — 세로로 선 판. 포탄이 통과하면 피해가 증폭된다.</summary>
    public struct AmpWall
    {
        public float X, Z;        // 중심
        public float HalfLen;     // z 축 방향 절반 길이
        public float MinY, MaxY;  // 높이 구간
    }

    /// <summary>회오리 — 세로 원기둥. 포탄이 들어오면 꼭대기로 빨려 올라간다.</summary>
    public struct Tornado
    {
        public float X, Z;
        public float Radius;
        public float TopY;        // 빨려 올라가는 높이
    }

    /// <summary>
    /// 맵에 떠 있는 기후 요소들. **게임과 하네스가 같은 이 클래스를 쓴다**(§2-9-1 의 교훈).
    /// null 이면 기후 없음 — 기존 호출부는 그대로 동작한다.
    /// </summary>
    public class AirField
    {
        /// <summary>증폭 배율 — 원작 "대미지가 50% 증폭된다".</summary>
        public const float AmpScale = 1.5f;

        // 아래는 전부 [추정] — 원문에 수치가 없다.
        public const float WallHalfLen = 26f;
        public const float WallMinY = 6f, WallMaxY = 46f;
        public const float WallThickness = 1.2f;
        public const float TornadoRadius = 7f;
        public const float TornadoTop = 62f;
        /// <summary>판 시작에 증폭벽이 생길 확률.</summary>
        public const float WallChance = 0.35f;
        /// <summary>판 시작에 회오리가 생길 확률.</summary>
        public const float TornadoChance = 0.35f;

        readonly List<AmpWall> _walls = new();
        readonly List<Tornado> _tornadoes = new();
        public IReadOnlyList<AmpWall> Walls => _walls;
        public IReadOnlyList<Tornado> Tornadoes => _tornadoes;
        public bool Any => _walls.Count > 0 || _tornadoes.Count > 0;

        public void Clear() { _walls.Clear(); _tornadoes.Clear(); }

        /// <summary>
        /// 판 시작에 기후를 굴린다 [추정 — 원작에 발생 규칙이 없다].
        /// 맵 한가운데 띠(양 팀 사이)에만 둔다 — 한쪽 진영에만 생기면 그게 곧 진영 유불리다.
        /// </summary>
        public void Roll(ref Rng rng, float mapSize)
        {
            Clear();
            float mid = mapSize * 0.5f;
            if (rng.Float01() < WallChance)
                _walls.Add(new AmpWall
                {
                    X = mid,
                    Z = rng.Range(mapSize * 0.3f, mapSize * 0.7f),
                    HalfLen = WallHalfLen,
                    MinY = WallMinY,
                    MaxY = WallMaxY,
                });
            if (rng.Float01() < TornadoChance)
                _tornadoes.Add(new Tornado
                {
                    X = mid + rng.Range(-18f, 18f),
                    Z = rng.Range(mapSize * 0.3f, mapSize * 0.7f),
                    Radius = TornadoRadius,
                    TopY = TornadoTop,
                });
        }

        /// <summary>테스트·연출용 직접 배치.</summary>
        public void AddWall(AmpWall w) => _walls.Add(w);
        public void AddTornado(Tornado t) => _tornadoes.Add(t);

        /// <summary>a→b 구간이 증폭벽을 지나갔는가.</summary>
        public bool CrossesWall(Vec3 a, Vec3 b)
        {
            for (int i = 0; i < _walls.Count; i++)
            {
                var w = _walls[i];
                // x 면을 넘었는가
                if ((a.X - w.X) * (b.X - w.X) > 0f) continue;
                float t = MathF.Abs(b.X - a.X) < 1e-6f ? 0f : (w.X - a.X) / (b.X - a.X);
                float y = a.Y + (b.Y - a.Y) * t, z = a.Z + (b.Z - a.Z) * t;
                if (y < w.MinY || y > w.MaxY) continue;
                if (MathF.Abs(z - w.Z) > w.HalfLen) continue;
                return true;
            }
            return false;
        }

        /// <summary>a→b 구간이 회오리 기둥에 들어갔는가. 들어갔으면 그 회오리를 준다.</summary>
        public bool EntersTornado(Vec3 a, Vec3 b, out Tornado hit)
        {
            for (int i = 0; i < _tornadoes.Count; i++)
            {
                var t = _tornadoes[i];
                if (b.Y > t.TopY) continue;                     // 꼭대기를 넘겨 쏘면 안 걸린다(원작: 고각샷으로 넘긴다)
                float dx = b.X - t.X, dz = b.Z - t.Z;
                if (dx * dx + dz * dz > t.Radius * t.Radius) continue;
                hit = t;
                return true;
            }
            hit = default;
            return false;
        }
    }
}
