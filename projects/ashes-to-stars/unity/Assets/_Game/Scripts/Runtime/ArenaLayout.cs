using System.Collections.Generic;
using UnityEngine;

namespace AshesToStars
{
    // ─────────────────────────────────────────────────────────────
    // 아레나 템플릿을 **실제 장애물로** 만든다 (던전 명세 §3-4).
    //
    // 왜 필요한가: 생성기는 노드마다 템플릿(open_ring·pillars·choke·pockets·arena_wide)을
    // 뽑는데, 그게 화면에 아무 영향도 주지 않으면 **템플릿은 이름뿐인 값**이다.
    // 이 저장소가 반복해서 겪은 「계산은 되는데 반영이 안 됨」이 정확히 그 모양이다.
    //
    // 무엇을 하지 않는가: 물리 엔진을 쓰지 않는다. 유닛 이동은 전부 좌표 갱신이라
    // 콜라이더를 붙여도 아무도 안 본다. 대신 **원형 밀어내기** 하나로 막는다 —
    // 싸고, 결정적이고(고정 스텝 측정과 호환), 쿼터뷰에서 읽힌다.
    //
    // 배치는 노드의 TerrainSeed에서 나온다 → 같은 시드면 같은 배치(§3-2).
    // ─────────────────────────────────────────────────────────────
    public static class ArenaLayout
    {
        public struct Obstacle
        {
            public Vector2 Pos;
            public float Radius;
        }

        static readonly List<Obstacle> _obstacles = new List<Obstacle>();
        static Transform _root;

        public static IReadOnlyList<Obstacle> Obstacles => _obstacles;

        /// <summary>지금 판에 장애물이 있는가. 없으면 밀어내기 계산 자체를 건너뛴다.</summary>
        public static bool Any => _obstacles.Count > 0;

        public static void Clear()
        {
            _obstacles.Clear();
            if (_root != null) Object.Destroy(_root.gameObject);
            _root = null;
        }

        /// <summary>
        /// 스프라이트 없이 **충돌만** 등록한다. 그림은 호출자가 이미 세운 경우에 쓴다
        /// (필드 프랍을 노이즈로 심는 `FieldDecor`가 유일한 호출자).
        ///
        /// ⚠️ 순서 제약: `Clear()`가 이 목록을 비운다. 라운드마다 Clear가 돌면
        ///    **Clear 뒤에** 다시 등록해야 한다. 그림은 남고 충돌만 사라지면
        ///    "보이는데 통과되는" 상태가 되어 §10-2 엄폐가 조용히 거짓이 된다.
        /// </summary>
        public static void AddObstacle(Vector2 pos, float radius)
            => _obstacles.Add(new Obstacle { Pos = pos, Radius = radius });

        /// <summary>
        /// 템플릿대로 장애물을 깔고 스프라이트를 세운다.
        /// arenaRadius 안쪽에만 놓는다 — 밖에 놓으면 아무도 지나가지 않아 존재 이유가 없다.
        /// </summary>
        public static void Build(ArenaTemplate template, uint seed, float arenaRadius, Material mat)
        {
            Clear();
            var rng = new Rng(seed);
            _root = new GameObject("ArenaObstacles").transform;

            switch (template)
            {
                case ArenaTemplate.pillars:
                    // 원형 카이팅용 기둥 — 숙련자가 기둥을 돌며 무리를 늘어뜨리는 판(§18-11 시뮬레이션)
                    Ring(ref rng, arenaRadius * 0.45f, rng.Range(4, 7), 1.1f, "dungeon_pillar_", 3, mat);
                    break;

                case ArenaTemplate.choke:
                    // 중앙을 가로지르는 벽 두 줄 + 가운데 통로. 포위 회피와 탱의 몸빵이 값을 갖는다
                    Wall(ref rng, new Vector2(-arenaRadius * 0.55f, 0f), new Vector2(-2.2f, 0f), 1.4f, mat);
                    Wall(ref rng, new Vector2(arenaRadius * 0.55f, 0f), new Vector2(2.2f, 0f), 1.4f, mat);
                    break;

                case ArenaTemplate.pockets:
                    // ✅ §10-2 원거리 몹 대응 — 엄폐가 의미를 갖는 유일한 판
                    Ring(ref rng, arenaRadius * 0.72f, rng.Range(3, 5), 1.3f, "dungeon_cover_", 3, mat);
                    break;

                case ArenaTemplate.arena_wide:
                    // 보스 전용. 동시 다발 장판이 들어갈 자리를 비워둔다 — 일부러 빈 판이다
                    break;

                default:   // open_ring — 기준선. 장애물 없음
                    break;
            }
        }

        static void Ring(ref Rng rng, float radius, int count, float r, string prefix, int variants, Material mat)
        {
            float start = rng.Value01() * Mathf.PI * 2f;
            for (int i = 0; i < count; i++)
            {
                float a = start + Mathf.PI * 2f * i / count;
                var p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                p += new Vector2(rng.Value01() - 0.5f, rng.Value01() - 0.5f) * 1.5f;
                Place(p, r, prefix + rng.Next(variants), mat);
            }
        }

        static void Wall(ref Rng rng, Vector2 from, Vector2 to, float r, Material mat)
        {
            int seg = Mathf.Max(2, Mathf.RoundToInt(Vector2.Distance(from, to) / (r * 1.4f)));
            for (int i = 0; i <= seg; i++)
                Place(Vector2.Lerp(from, to, i / (float)seg), r, "dungeon_wall_" + rng.Next(3), mat);
        }

        static void Place(Vector2 pos, float radius, string resource, Material mat)
        {
            _obstacles.Add(new Obstacle { Pos = pos, Radius = radius });

            var tex = Resources.Load<Texture2D>("props/" + resource);
            if (tex == null)
            {
                // 그림이 없어도 막기는 해야 한다. 다만 **조용히** 넘어가지 않는다 —
                // 보이지 않는 벽에 막히는 것이 이 게임에서 가장 화나는 버그다.
                Debug.LogWarning($"[아레나] 프랍 누락: props/{resource} — 보이지 않는 장애물이 된다");
                return;
            }

            var go = new GameObject(resource);
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(pos.x, pos.y * StressTest.ISO_Y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                      new Vector2(0.5f, 0.12f), tex.height / (radius * 2.2f));
            sr.sharedMaterial = mat;
            sr.sortingOrder = (int)(-pos.y * 16f);   // 파티·몹과 같은 y 정렬 규칙(쿼터뷰)
        }

        /// <summary>
        /// 장애물 밖으로 밀어낸다. 이동을 계산한 **뒤에** 부르면 된다.
        /// 막는 것이지 되튕기는 게 아니다 — 벽에 붙어 미끄러지는 느낌이 나야 한다.
        /// </summary>
        public static Vector2 Resolve(Vector2 pos, float unitRadius = 0.35f)
        {
            if (_obstacles.Count == 0) return pos;
            for (int i = 0; i < _obstacles.Count; i++)
            {
                var o = _obstacles[i];
                Vector2 d = pos - o.Pos;
                float need = o.Radius + unitRadius;
                float m = d.magnitude;
                if (m >= need || m < 1e-4f) continue;
                pos = o.Pos + d / m * need;
            }
            return pos;
        }

        /// <summary>
        /// 한 걸음이 집을 관통하면 접선으로 비켜 간다. 막고 나서 밀어내기만 하면
        /// 목표 쪽으로 계속 들이받아 지붕 위를 가로지른다.
        /// </summary>
        public static Vector2 Around(Vector2 from, Vector2 delta, float unitRadius = 0.35f)
        {
            if (_obstacles.Count == 0 || delta.sqrMagnitude < 1e-8f) return delta;
            Vector2 raw = from + delta;
            int hit = Blocking(from, raw, unitRadius);
            if (hit < 0) return Resolve(raw, unitRadius) - from;

            var o = _obstacles[hit];
            Vector2 away = from - o.Pos;
            if (away.sqrMagnitude < 1e-6f) away = Vector2.up;
            Vector2 tan = new Vector2(-away.y, away.x);
            if (Vector2.Dot(tan, delta) < 0f) tan = -tan;
            Vector2 slide = tan.normalized * delta.magnitude;
            return Resolve(from + slide, unitRadius) - from;
        }

        static int Blocking(Vector2 from, Vector2 to, float unitRadius)
        {
            int hit = -1;
            float best = float.MaxValue;
            for (int i = 0; i < _obstacles.Count; i++)
            {
                var o = _obstacles[i];
                float need = o.Radius + unitRadius;
                if (!SegmentHits(from, to, o.Pos, need)
                    && (to - o.Pos).sqrMagnitude >= need * need)
                    continue;
                float d = (from - o.Pos).sqrMagnitude;
                if (d < best) { best = d; hit = i; }
            }
            return hit;
        }

        static bool SegmentHits(Vector2 a, Vector2 b, Vector2 c, float radius)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 < 1e-8f ? 0f : Mathf.Clamp01(Vector2.Dot(c - a, ab) / len2);
            return (a + ab * t - c).sqrMagnitude < radius * radius;
        }
    }
}
