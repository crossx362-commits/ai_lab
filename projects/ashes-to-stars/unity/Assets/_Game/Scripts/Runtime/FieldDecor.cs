using UnityEngine;

/// <summary>
/// 전투 아레나 주변에 배경 프랍을 배치한다 (블렌더 렌더링).
///
/// 성능 방침:
///   - 프랍 26개를 런타임에 하나의 아틀라스로 합친다 (배칭 유지).
///   - 모든 프랍이 같은 머티리얼을 공유하지만 개별 텍스처 대신 **아틀라스 내 UV 좌표**로 구분한다.
///   - 이렇게 하면 단일 머티리얼 배칭 요건은 만족하지만, 프랍 **텍스처가 같아져** 배칭이 깨지지 않는다.
///   - 비용: 프랍 26개 로드 + 아틀라스 생성(O(n), 게임 시작 시 1회).
///   - 500 캐릭터 렌더링에 비해 프랍은 마이너(30~60개, 일회성 배치).
///
/// 배칭이 깨지는 이유와 해법:
///   - **문제**: 개별 프랍 파일을 SpriteBank 아틀라스에 못 넣으니 각 프랍마다 다른 텍스처를 갖는다.
///     같은 Material이어도 텍스처가 다르면 GPU 배칭이 끊긴다 → 드로우콜 26배 증가.
///   - **해법**: 프랍 **전용** 런타임 아틀라스를 만들어 26개를 모두 한 텍스처에 넣는다.
///     SpriteBank.Load()와 같은 방식 — 로드 비용은 무시할 수준이고 배칭은 완벽하다.
/// </summary>
namespace AshesToStars
{
    public static class FieldDecor
    {
        /// <summary>바이옴별 배경 테마</summary>
        public enum Biome { Field, Ash, Dungeon }

        /// <summary>이 값을 넘는 노이즈 봉우리에만 심는다. 낮추면 들판이 숲이 된다.</summary>
        const float PROP_THRESHOLD = 0.62f;
        /// <summary>드로우콜이 아니라 **화면 가독성** 상한이다 — 너무 많으면 유닛이 묻힌다.</summary>
        const int PROP_CAP = 90;

        /// <summary>
        /// 아레나 **안쪽**에 선 프랍의 좌표. `ArenaLayout.Clear()`가 라운드마다 충돌 목록을
        /// 비우므로, 그림은 그대로 두고 **충돌만 다시 등록**하기 위해 따로 기억한다.
        /// 이게 없으면 "보이는데 통과되는" 상태가 되어 §10-2 엄폐가 조용히 거짓이 된다.
        /// </summary>
        static readonly System.Collections.Generic.List<Vector2> _cover =
            new System.Collections.Generic.List<Vector2>();

        /// <summary>안쪽 프랍의 충돌 반경 — 그림 크기가 아니라 "돌아가야 하는 정도"다.</summary>
        const float COVER_RADIUS = 0.9f;

        static GameObject _root;

        /// <summary>
        /// 기억해 둔 안쪽 프랍을 `ArenaLayout`에 다시 등록한다.
        /// **`ArenaLayout.Clear()`·`Build()` 뒤에 부를 것** — 그 둘이 목록을 비운다.
        /// </summary>
        public static void RegisterCover()
        {
            for (int i = 0; i < _cover.Count; i++)
                ArenaLayout.AddObstacle(_cover[i], COVER_RADIUS);
        }

        /// <summary>
        /// 전투 공간 가장자리에 배경 프랍을 배치한다.
        /// </summary>
        /// <param name="bank">게임 스프라이트 뱅크 (머티리얼 템플릿용)</param>
        /// <param name="arenaRadius">전투 아레나 반경</param>
        /// <param name="seed">난수 시드 (재현성)</param>
        /// <param name="biome">배경 테마 (프랍 선택에 영향)</param>
        /// <param name="엄폐물">
        /// true면 아레나 **안쪽**에도 심고 `ArenaLayout`에 장애물로 등록한다(실제로 막힌다).
        /// 기본 false — W1~W3 검증은 빈 판이어야 구성 비교가 성립하기 때문이다.
        /// 켜는 것은 게임플레이 결정이므로 호출부에서 명시적으로 넘긴다.
        /// </param>
        public static void Build(SpriteBank bank, float arenaRadius, int seed, Biome biome,
                                 bool 엄폐물 = false)
        {
            Random.InitState(seed);

            // 다시 부를 수 있다 — 게임 진입부가 엄폐물을 켜며 한 번 더 부른다.
            // 이전 프랍을 안 지우면 두 벌이 겹쳐 화면이 두 배로 지저분해진다.
            if (_root != null) Object.Destroy(_root);
            _cover.Clear();

            // ── 바이옴별 프랍 목록 ────────────────
            string[] propNames = GetPropNames(biome);
            if (propNames.Length == 0)
            {
                Debug.LogWarning("[FieldDecor] 바이옴 프랍 없음: " + biome);
                return;
            }

            // ── 텍스처 로드 및 검증 ────────────────
            Texture2D[] texes = new Texture2D[propNames.Length];
            int loadedCount = 0;
            for (int i = 0; i < propNames.Length; i++)
            {
                texes[i] = Resources.Load<Texture2D>("props/" + propNames[i]);
                if (texes[i] == null)
                {
                    Debug.LogWarning("[FieldDecor] 프랍 누락: props/" + propNames[i] +
                                   " — 경로 확인 또는 TextureImportRules 규칙 확인");
                    // 임시 단색으로 대체하지 않고 skip — 프랍이 없는 게 나음
                    continue;
                }
                if (!texes[i].isReadable)
                {
                    Debug.LogError("[FieldDecor] 읽기 불가 텍스처: " + propNames[i] +
                                 " — TextureImportRules.isReadable 확인");
                }
                loadedCount++;
            }

            if (loadedCount == 0)
            {
                Debug.LogError("[FieldDecor] 로드된 프랍이 없다. 배치 중단");
                return;
            }

            // ── 런타임 아틀라스 생성 (배칭 유지) ────────────────
            // 프랍 26개는 대부분 작은 이미지(보통 64×64 정도)라 2048×2048이면 충분하다.
            // 패킹 실패 시 투명이라 화면이 텅 빈다 — 주의.
            var atlas = new Texture2D(2048, 2048, TextureFormat.RGBA32, false);
            var rects = atlas.PackTextures(texes, 2, 2048, false);

            // 픽셀아트: Point 필터 (Bilinear면 도트가 뭉개짐)
            atlas.filterMode = FilterMode.Point;
            atlas.Apply(false, false);

            // 아틀라스 건강 확인 — 실패 시 전부 투명
            var probe = atlas.GetPixels32();
            int opaque = 0;
            for (int i = 0; i < probe.Length; i += 131) if (probe[i].a > 8) opaque++;
            if (opaque < 5)
                Debug.LogWarning("[FieldDecor] 아틀라스가 비었거나 거의 투명하다 — 프랍이 그려지지 않을 수 있음");
            else
                Debug.Log($"[FieldDecor] 아틀라스 OK — 불투명 표본 {opaque}");

            // ── 머티리얼 (SpriteBank 공유 머티리얼을 템플릿으로) ────────────────
            // 같은 Material이지만 mainTexture만 다르다. 배칭은 유지된다.
            var sh = Shader.Find("Sprites/Default");
            var mat = new Material(sh) { enableInstancing = true };
            mat.mainTexture = atlas;

            // ── 배치: 노이즈 봉우리 (2026-08-14 통합) ────────────────
            // 이전에는 링 위 **완전 랜덤**이었다. 그러면 프랍이 고르게 흩뿌려져
            // "저 바위 뒤로 돌아간다"가 성립하지 않는다 — 위치 선정(§10-2)이
            // 판단이 되려면 프랍이 **덩어리로 뭉치고 사이에 길이 나야** 한다.
            // 바닥 색조와 같은 노이즈(TerrainNoise)를 보므로 바위 구역에 바위가 난다.
            const float ISO_Y = StressTest.ISO_Y;
            var origin = TerrainNoise.Origin(seed);
            var decorRoot = new GameObject("FieldDecor_Props");
            _root = decorRoot;

            float outer = arenaRadius * 1.8f;
            float step = Mathf.Max(0.8f, arenaRadius * 0.09f);
            int placed = 0, blockers = 0;

            for (float y = -outer; y <= outer && placed < PROP_CAP; y += step)
                for (float x = -outer; x <= outer && placed < PROP_CAP; x += step)
                {
                    float d2 = x * x + y * y;
                    if (d2 > outer * outer) continue;

                    // 아레나 안쪽은 기본적으로 비운다 — W1~W3 검증은 **빈 판**이어야
                    // 구성 비교가 성립한다(W3Party 주석 §3-4). 엄폐물을 켠 게임 모드에서만
                    // 안쪽에 심고, 그때는 ArenaLayout에 장애물로 등록해 실제로 막는다.
                    bool inside = d2 < arenaRadius * arenaRadius;
                    if (inside && !엄폐물) continue;
                    // 스폰 지점만은 어떤 모드에서도 비운다 — 시작하자마자 끼면 판이 망가진다
                    if (d2 < (arenaRadius * 0.35f) * (arenaRadius * 0.35f)) continue;

                    if (TerrainNoise.Sample(origin, x, y) < PROP_THRESHOLD) continue;
                    if (Random.value > 0.45f) continue;      // 봉우리마다 다 심으면 벽이 된다

                    var worldPos = new Vector2(x + (Random.value - 0.5f) * step,
                                               y + (Random.value - 0.5f) * step);
                    int propIdx = Random.Range(0, propNames.Length);
                    if (texes[propIdx] == null) continue;     // 로드 실패분은 건너뛴다

                    var go = new GameObject("prop_" + propNames[propIdx], typeof(SpriteRenderer));
                    go.transform.SetParent(decorRoot.transform, false);
                    go.transform.position = new Vector3(worldPos.x, worldPos.y * ISO_Y, 0.1f);
                    go.transform.localScale = Vector3.one;   // 프랍은 PPU로 크기 조정

                    var sr = go.GetComponent<SpriteRenderer>();
                    sr.sprite = Sprite.Create(atlas, rects[propIdx], Vector2.one * 0.5f, 32f, 0,
                                             SpriteMeshType.FullRect);
                    sr.sharedMaterial = mat;
                    // 정렬: y가 낮을수록 앞. 캐릭터(500)보다 뒤인 프랍 기본층(100)
                    sr.sortingOrder = 100 - (int)(worldPos.y * 4f);

                    // 아레나 안에 선 프랍은 **보이기만 하면 안 된다** — 지나갈 수 있으면
                    // 엄폐가 성립하지 않고, 그림만 있는 장애물은 오히려 유저를 속인다.
                    if (inside) { _cover.Add(worldPos); blockers++; }

                    placed++;
                }

            if (엄폐물) RegisterCover();
            Debug.Log($"[FieldDecor] {biome} 프랍 {placed}개 노이즈 배치" +
                      (엄폐물 ? $" (아레나 내 엄폐물 {blockers}개)" : " (아레나 밖 장식만)"));
        }

        static string[] GetPropNames(Biome biome) => biome switch
        {
            // ── 평원 (field) ────────────────────
            // 덤불, 바위, 그루터기
            Biome.Field => new[]
            {
                "field_bush_0", "field_bush_1", "field_bush_2",
                "field_rock_0", "field_rock_1", "field_rock_2",
                "field_stump_0", "field_stump_1",
            },

            // ── 잿벌 (ash) ────────────────────
            // 뼈, 탄화된 목재
            Biome.Ash => new[]
            {
                "ash_bone_0", "ash_bone_1",
                "ash_charred_0", "ash_charred_1", "ash_charred_2",
            },

            // ── 던전 (dungeon) ────────────────────
            // 결정, 기둥, 잔해 (estate는 향후 확장용)
            Biome.Dungeon => new[]
            {
                "dungeon_crystal_0", "dungeon_crystal_1", "dungeon_crystal_2",
                "dungeon_pillar_0", "dungeon_pillar_1", "dungeon_pillar_2",
                "dungeon_rubble_0", "dungeon_rubble_1", "dungeon_rubble_2",
            },

            _ => new string[0]
        };
    }
}
