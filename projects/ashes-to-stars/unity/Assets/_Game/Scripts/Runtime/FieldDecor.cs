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
        // 노이즈가 이 값을 넘는 칸에만 심는다. 0.62였을 때 **상한 90 중 실제 배치가 15개**뿐이라
        // 화면이 허허벌판이었다(오너 지적 2026-08-15 "지형 좀 어떻게 해봐"). 노이즈 봉우리가
        // 그만큼 드물다 — 임계를 내려 봉우리를 넓게 잡는다.
        const float PROP_THRESHOLD = 0.48f;
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
        ///
        /// ⚠️ 순서 의존: `_cover`는 static이라 **씬을 다시 로드해도 살아남는다.**
        ///    지금은 `W3Party.Awake`가 `BuildWorld()`(→`Build`가 `_cover`를 비움) →
        ///    `NextStyle()`(→여기) 순서라 옛 좌표가 등록될 창이 없다.
        ///    이 순서를 뒤집으면 **이전 판의 좌표가 보이지 않는 벽**이 된다 —
        ///    `ArenaLayout` 주석이 "이 게임에서 가장 화나는 버그"라고 적은 그것이다.
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

            // ⚠️ 상한을 루프 조건에 걸지 마라. `placed < PROP_CAP`을 for에 두면 맵을
            //    아래(-outer)부터 훑다가 상한에 닿는 순간 멈춰서 **프랍이 전부 아래쪽
            //    몇 줄에만 몰리고 그 위는 0개**가 된다. 임계를 낮춰 후보가 늘수록 더 심해진다
            //    (실측 2026-08-15: 90개가 전부 화면 밖 하단, 카메라에는 거의 안 보였다).
            //    그래서 **후보를 다 모은 뒤 균일하게 솎아낸다** — 밀도만 줄고 분포는 유지된다.
            var cand = new System.Collections.Generic.List<Vector2>();
            var candInside = new System.Collections.Generic.List<bool>();

            for (float y = -outer; y <= outer; y += step)
                for (float x = -outer; x <= outer; x += step)
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
                    // 봉우리마다 다 심으면 벽이 된다. 다만 0.45는 임계(0.62)와 곱해져
                    // 최종 밀도를 너무 깎았다 — 임계를 낮춘 지금은 이쪽을 조금 올려도
                    // "덩어리로 뭉치고 사이에 길이 난다"가 유지된다.
                    if (Random.value > 0.62f) continue;

                    cand.Add(new Vector2(x + (Random.value - 0.5f) * step,
                                         y + (Random.value - 0.5f) * step));
                    candInside.Add(inside);
                }

            // 균일 솎아내기 — 후보가 상한보다 많으면 일정 간격으로 건너뛰며 고른다.
            // 앞에서부터 자르면 다시 공간 편향이 생기므로 **간격(stride)**으로 고른다.
            float keep = cand.Count > PROP_CAP ? (float)PROP_CAP / cand.Count : 1f;
            float acc = 0f;
            for (int ci = 0; ci < cand.Count; ci++)
                {
                    acc += keep;
                    if (acc < 1f) continue;
                    acc -= 1f;

                    var worldPos = cand[ci];
                    bool inside = candInside[ci];
                    // ⚠️ 산포 대상은 **자연물만**이다. 마을 구성물(집·울타리·우물)이 여기
                    //    섞이면 집이 들판에 무작위로 흩어져 마을로 안 읽힌다 — 그것들은
                    //    `BuildVillage`가 정해진 자리에 세운다.
                    int propIdx = Random.Range(0, ScatterCount(propNames));
                    if (!Place(propIdx, worldPos, inside)) continue;
                    placed++;
                }

            // ── 마을 (오너 지시 2026-08-15 「지형을 마을처럼 구성해」) ────────────────
            // 아레나를 **마을 광장**으로 삼는다. 집을 광장 둘레에 세우고 사이를 울타리로
            // 잇고 길가에 가로등을 놓으면, 전투 공간은 그대로 비어 있는 채로 화면이
            // 마을로 읽힌다. 집을 노이즈로 흩뿌리지 않는 이유가 이것이다 — 마을은
            // **간격이 일정하고 정면이 광장을 향할 때** 마을로 보인다.
            int villageCount = 0;
            if (biome == Biome.Field)
                villageCount = BuildVillage(arenaRadius, propNames, Place);

            if (엄폐물) RegisterCover();
            Debug.Log($"[FieldDecor] {biome} 프랍 {placed}개 노이즈 배치" +
                      (villageCount > 0 ? $" + 마을 구성물 {villageCount}개" : "") +
                      (엄폐물 ? $" (아레나 내 엄폐물 {blockers}개)" : " (아레나 밖 장식만)"));

            // 로컬 함수 — 스캐터와 마을이 **같은 경로로** 프랍을 세운다.
            // 두 벌로 나누면 정렬 순서·아틀라스·엄폐 등록 중 하나가 조용히 어긋난다.
            bool Place(int propIdx, Vector2 worldPos, bool asCover)
            {
                if (propIdx < 0 || propIdx >= texes.Length) return false;
                if (texes[propIdx] == null) return false;    // 로드 실패분은 건너뛴다

                var go = new GameObject("prop_" + propNames[propIdx], typeof(SpriteRenderer));
                go.transform.SetParent(decorRoot.transform, false);
                go.transform.position = new Vector3(worldPos.x, worldPos.y * ISO_Y, 0.1f);
                go.transform.localScale = Vector3.one;   // 프랍은 PPU로 크기 조정

                var sr = go.GetComponent<SpriteRenderer>();
                // 크기는 **아트가 정한 목표 유닛**(prop_scale.json)에서 PPU를 역산한다.
                // PPU 32 고정이던 시절엔 128px 원본이 전부 4유닛 = 캐릭터(2유닛)의 두 배라
                // 바위가 사람만 했다. 그 표는 2026-08-14에 만들어 두고 **읽는 곳이 0곳**이었다 —
                // 마을을 세우면서 살렸다(집 4유닛 옆에 울타리 0.9유닛이라야 마을로 보인다).
                // ⚠️ `PackTextures`는 **정규화 UV**(0~1)를 돌려주는데 `Sprite.Create`는
                //    **픽셀 좌표** Rect를 받는다. UV를 그대로 넘기면 0.06×0.06픽셀짜리
                //    스프라이트가 되어 **아무것도 그려지지 않는다** — 그런데 배치 로그는
                //    정상적으로 "프랍 90개"라고 말한다. 이 저장소가 반복해서 겪은
                //    「수치는 전부 통과했는데 화면이 비어 있다」가 여기서도 일어나고 있었다
                //    (2026-08-15 발견: 프랍은 도입 이래 한 번도 화면에 나온 적이 없다).
                var uv = rects[propIdx];
                var pxRect = new Rect(uv.x * atlas.width, uv.y * atlas.height,
                                      uv.width * atlas.width, uv.height * atlas.height);
                float ppu = pxRect.height / Mathf.Max(0.05f, TargetUnits(propNames[propIdx]));
                sr.sprite = Sprite.Create(atlas, pxRect, Vector2.one * 0.5f, ppu, 0,
                                         SpriteMeshType.FullRect);
                sr.sharedMaterial = mat;
                // 정렬: y가 낮을수록 앞. 캐릭터(500)보다 뒤인 프랍 기본층(100)
                sr.sortingOrder = 100 - (int)(worldPos.y * 4f);

                // 아레나 안에 선 프랍은 **보이기만 하면 안 된다** — 지나갈 수 있으면
                // 엄폐가 성립하지 않고, 그림만 있는 장애물은 오히려 유저를 속인다.
                if (asCover) { _cover.Add(worldPos); blockers++; }
                return true;
            }
        }

        /// <summary>
        /// 마을길. 녹지를 도는 **고리길** + 마을을 관통하는 **큰길** 두 줄을 깐다.
        ///
        /// 왜 프랍이 아니라 색 조각인가:
        ///   길은 타일이지 물건이 아니다. 길 타일 아트를 새로 뽑아 이어 붙이면 이음매·
        ///   회전·모서리 처리가 따라오는데, 이 게임의 바닥은 이미 노이즈로 칠한 단색
        ///   평면이라 **같은 방식으로 흙색을 얹는 것**이 이음매 없이 자연스럽다.
        ///   밟은 자국이 이어져 보이면 그게 길이다 — 그림이 정교할 필요는 없다.
        ///
        /// 정렬은 프랍(100)보다 **뒤**(50)다. 길 위에 집·수레가 서야 하기 때문이다.
        /// </summary>
        /// <summary>
        /// 마을길 하나. 직선 두 점 사이를 **활처럼 휘게** 지난다.
        ///
        /// 왜 휘는가 (오너가 보내온 실제 마을 항공사진, 2026-08-15):
        ///   실제 마을 길은 자로 그은 십자가 아니다. 지형을 피해 굽고, 중간에서 갈라져
        ///   나가고, 갈라진 길이 또 굽는다. 직선 격자로 깔았더니 마을이 아니라
        ///   계획도시 블록처럼 보였다. `Bow`가 그 굽이를 만든다(가운데서 가장 크게 휜다).
        /// </summary>
        struct RoadPath
        {
            public Vector2 A, B;
            public float Bow;      // 중앙에서 수직으로 밀리는 양. 부호가 휘는 방향
            public float Width;
        }

        static Vector2 OnRoad(RoadPath r, float t)
        {
            var p = Vector2.Lerp(r.A, r.B, t);
            var d = (r.B - r.A).normalized;
            var nrm = new Vector2(-d.y, d.x);
            return p + nrm * r.Bow * Mathf.Sin(t * Mathf.PI);
        }

        /// <summary>길의 진행 방향(접선). 집을 길과 나란히 세울 때 쓴다.</summary>
        static Vector2 RoadDir(RoadPath r, float t)
        {
            const float H = 0.01f;
            return (OnRoad(r, Mathf.Min(1f, t + H)) - OnRoad(r, Mathf.Max(0f, t - H))).normalized;
        }

        /// <summary>
        /// 마을 길망. 굽은 큰길 하나에서 샛길 셋이 갈라져 나간다.
        ///
        /// ⛔ **원형(고리) 길을 만들지 마라.** 바닥에 그려진 큰 원은 화면에서 스킬 범위
        ///    표시로 읽힌다 — 오너가 지운 `skill_ring`과 구분되지 않아 재차 지적받았다.
        /// </summary>
        static RoadPath[] VillageRoads(float R)
        {
            var main = new RoadPath
            {
                A = new Vector2(-2.3f * R, -0.42f * R),
                B = new Vector2(2.3f * R, 0.30f * R),
                Bow = 0.30f * R,
                Width = 2.6f,
            };

            // 샛길 — 큰길의 서로 다른 지점에서 갈라져 반대쪽으로 굽어 나간다.
            // 갈라지는 자리를 t로 잡아 **반드시 큰길에 붙게** 한다(공중에 뜬 길 방지).
            var b1 = new RoadPath
            {
                A = OnRoad(main, 0.30f),
                B = new Vector2(-1.0f * R, 1.5f * R),
                Bow = -0.22f * R,
                Width = 1.8f,
            };
            var b2 = new RoadPath
            {
                A = OnRoad(main, 0.58f),
                B = new Vector2(0.9f * R, -1.6f * R),
                Bow = 0.20f * R,
                Width = 1.8f,
            };
            var b3 = new RoadPath
            {
                A = OnRoad(main, 0.78f),
                B = new Vector2(1.9f * R, 1.4f * R),
                Bow = 0.16f * R,
                Width = 1.6f,
            };
            return new[] { main, b1, b2, b3 };
        }

        /// <summary>
        /// 길과 마을 바닥을 깐다. 길 아트를 새로 뽑지 않고 흙색 조각을 얹는다 —
        /// 타일을 쓰면 이음매·모서리·회전 처리가 따라오는데, 바닥이 이미 단색 평면이라
        /// 색을 얹는 편이 이음매 없이 자연스럽다. 밟힌 자국이 이어지면 그게 길이다.
        /// </summary>
        static int BuildRoads(float arenaRadius, RoadPath[] roads)
        {
            const float ISO_Y = StressTest.ISO_Y;
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            var sp = Sprite.Create(tex, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1f);
            var mat = new Material(Shader.Find("Sprites/Default"));

            var dirt = new Color(0.42f, 0.34f, 0.24f, 0.55f);
            var packed = new Color(0.46f, 0.40f, 0.30f, 0.26f);
            int made = 0;

            void Piece(string name, Vector2 world, float w, float h, float rotDeg, Color c, int order)
            {
                var go = new GameObject(name, typeof(SpriteRenderer));
                go.transform.SetParent(_root.transform, false);
                go.transform.position = new Vector3(world.x, world.y * ISO_Y, 0.2f);
                go.transform.localRotation = Quaternion.Euler(0, 0, rotDeg);
                go.transform.localScale = new Vector3(w, h * ISO_Y, 1f);
                var sr = go.GetComponent<SpriteRenderer>();
                sr.sprite = sp;
                sr.sharedMaterial = mat;
                sr.color = c;
                sr.sortingOrder = order;   // 프랍(100)보다 뒤 — 길 위에 물건이 선다
                made++;
            }

            // ① 마을이 앉은 땅 — 길 주변만 흙이 드러난다. 사진의 마을도 집이 모인 자리만
            //    땅이 보이고 나머지는 초록이다. 큰 사각형 한 장을 깔면 잔디에 갈색 판을
            //    얹은 것처럼 보이므로, **길을 따라가며** 넓게 흩뿌린다.
            foreach (var r in roads)
                for (float t = 0f; t <= 1f; t += 0.02f)
                {
                    var p = OnRoad(r, t);
                    if (p.magnitude > arenaRadius * 2.2f) continue;
                    Piece("village_ground", p, r.Width * 5.5f, r.Width * 4.0f, 0f, packed, 40);
                }

            // ② 길 — 조각을 촘촘히 겹쳐 이음매를 없앤다. 조각마다 그 지점의 접선으로 눕힌다.
            foreach (var r in roads)
            {
                float len = Vector2.Distance(r.A, r.B) + Mathf.Abs(r.Bow) * 2f;
                int seg = Mathf.Max(12, Mathf.CeilToInt(len / 1.1f));
                for (int i = 0; i <= seg; i++)
                {
                    float t = i / (float)seg;
                    var p = OnRoad(r, t);
                    var d = RoadDir(r, t);
                    float ang = Mathf.Atan2(d.y * ISO_Y, d.x) * Mathf.Rad2Deg;
                    Piece("road", p, len / seg * 1.9f, r.Width, ang, dirt, 50);
                }
            }

            return made;
        }

        /// <summary>
        /// 노이즈로 흩뿌려도 되는 프랍의 개수. 평원은 앞 8종(자연물)만 산포 대상이고
        /// 뒤쪽 마을 구성물은 `BuildVillage`가 자리를 정한다.
        /// </summary>
        // 평원 자연물 = 덤불3 + 바위3 + 그루터기2 + 나무4 + 과수원나무1 + 관목열1 = 14.
        // 이 앞 14종만 노이즈 산포하고, 뒤 마을 구성물(집·울타리 등)은 BuildVillage가 세운다.
        /// <summary>
        /// 노이즈로 흩뿌려도 되는 프랍 수 = **`village_` 접두가 아닌 것의 개수**.
        ///
        /// ⚠️ 숫자를 손으로 적지 마라. 예전엔 `Field ? 8 : total` 같은 상수였는데,
        ///    목록에 자연물을 추가하고 이 숫자를 안 올리면 새 프랍이 **조용히 안 나오고**,
        ///    반대로 숫자만 크면 `village_house_*`가 뽑혀 **집이 들판에 흩뿌려진다.**
        ///    실제로 2026-08-15에 두 방향 모두 발생했다(자연물 13 vs 상수 14로 어긋남).
        ///    목록에서 직접 세면 두 곳을 따로 갱신할 일이 사라진다.
        ///
        /// 전제: 마을 구성물은 **이름이 `village_`로 시작하고 목록 뒤쪽에 모여 있다.**
        /// 새 마을 물건을 넣을 땐 이 규칙을 지킬 것.
        /// </summary>
        static int ScatterCount(string[] names)
        {
            int n = 0;
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i].StartsWith("village_")) break;   // 여기부터는 BuildVillage 몫
                n++;
            }
            return n > 0 ? n : names.Length;                  // 전부 마을이면 그냥 다 쓴다
        }

        // ── 프랍 목표 크기표 (art/prop_scale.json) ────────────────
        // 단일 소스는 `art/prop_scale.json`이다. 아트가 크기를 정하고 코드는 읽기만 한다 —
        // C# 상수로 옮겨 적으면 두 곳이 어긋나는 그 사고를 또 낸다.
        // ⚠️ 아트 쪽 파일을 고쳤으면 `Assets/Resources/prop_scale.json`으로 복사해야 반영된다.
        static System.Collections.Generic.Dictionary<string, float> _scale;
        const float DEFAULT_UNITS = 1.0f;

        static float TargetUnits(string name)
        {
            if (_scale == null)
            {
                _scale = new System.Collections.Generic.Dictionary<string, float>();
                var ta = Resources.Load<TextAsset>("prop_scale");
                if (ta == null)
                {
                    // 조용히 기본값으로 넘어가지 않는다 — 전부 1유닛이면 집이 사람만 해진다
                    Debug.LogWarning("[FieldDecor] Resources/prop_scale.json 없음 — 프랍 크기가 전부 기본값이 된다");
                }
                else
                {
                    // 평평한 "이름": 숫자 목록이라 정식 JSON 파서가 필요 없다.
                    // (JsonUtility는 사전을 못 읽고, 이 표에는 설명용 문자열 키도 섞여 있다)
                    var mm = System.Text.RegularExpressions.Regex.Matches(
                        ta.text, "\"([A-Za-z0-9_]+)\"\\s*:\\s*([0-9]*\\.?[0-9]+)");
                    foreach (System.Text.RegularExpressions.Match m in mm)
                        if (float.TryParse(m.Groups[2].Value,
                                System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out float v))
                            _scale[m.Groups[1].Value] = v;
                }
            }
            return _scale.TryGetValue(name, out float u) ? u : DEFAULT_UNITS;
        }

        /// <summary>
        /// 광장(아레나) 둘레에 마을을 세운다. 반환값은 실제로 선 구성물 수.
        ///
        /// ── 왜 이 형태인가 (오너 지시 2026-08-15 「마을의 의미를 조사해서 배치」) ──
        /// 중세 마을은 형태에 이름이 붙은 몇 가지 유형으로 나뉜다. 그중 **녹지형 마을
        /// (Angerdorf / green village)** 은 공동 소유의 중앙 풀밭(anger)을 집들이 빙 둘러싸는
        /// 형태다. 잉글랜드 북부에도 같은 계열이 있고, 특징이 셋이다:
        ///   ① 중앙에 공유 녹지 — 우물·연못이 여기 있고, 밤에 가축을 여기 모았다
        ///   ② 집은 **정면이 녹지를 향하고**, 헛간·축사는 **뒤쪽**에 둔다
        ///   ③ 집 뒤로 각자의 텃밭(croft)이 울타리로 구획되고, 그 바깥을 **농로가 고리로 두른다**
        ///
        /// 우리 전투 아레나가 정확히 ①의 녹지에 해당한다 — 그래서 이 유형을 골랐다.
        /// 직전 구현은 ②③이 빠져 있었다: 울타리가 집과 광장 **사이**에 있어 마당이 광장 쪽에
        /// 있었고(실제와 반대), 무엇보다 **길이 하나도 없었다.** 마을은 길로 이어져야
        /// 마을이지, 건물이 모여 있다고 마을이 아니다.
        ///
        /// 그래서 지금은: 녹지 둘레를 도는 **마을길(고리)** + 마을을 관통해 들어오고 나가는
        /// **큰길(직선 2줄)** + 집 뒤 **텃밭 울타리** + 녹지의 우물.
        /// </summary>
        static int BuildVillage(float arenaRadius, string[] names,
                                System.Func<int, Vector2, bool, bool> place)
        {
            int Idx(string n) => System.Array.IndexOf(names, n);
            int n = 0;

            var roads = VillageRoads(arenaRadius);
            n += BuildRoads(arenaRadius, roads);

            string[] houses = { "village_house_0", "village_house_1", "village_house_2",
                                "village_barn_0" };

            // 싸울 자리는 비운다. 여기에 집이 서면 시작하자마자 파티가 건물에 낀다.
            float clear = arenaRadius * 0.34f;
            // 화면 밖에 세워 봐야 안 보인다 — 카메라가 보여주는 범위는 가로 ±14·세로 ±8이다.
            float reach = arenaRadius * 2.1f;

            int lot = 0;
            foreach (var r in roads)
            {
                bool isMain = r.Width > 2.2f;
                // 큰길가는 집이 빽빽하고 샛길은 성기다 — 사진의 마을도 중심이 조밀하다.
                float step = isMain ? 0.13f : 0.20f;

                for (float t = 0.04f; t <= 0.97f; t += step)
                {
                    lot++;
                    var c = OnRoad(r, t);
                    var d = RoadDir(r, t);
                    var nrm = new Vector2(-d.y, d.x);

                    for (int s = -1; s <= 1; s += 2)     // 길 양쪽
                    {
                        // 줄이 자로 잰 듯하면 계획도시가 된다. 길에서 떨어진 거리와
                        // 길을 따라간 위치를 둘 다 흔들어 제각각으로 앉힌다.
                        float off = (isMain ? 3.0f : 2.4f) + Random.Range(-0.5f, 1.1f);
                        var at = c + nrm * off * s + d * Random.Range(-0.7f, 0.7f);

                        if (at.magnitude < clear) continue;
                        if (Mathf.Abs(at.x) > reach || Mathf.Abs(at.y) > reach * 0.8f) continue;
                        // 중심에서 멀수록 성기게 — 마을은 가장자리로 갈수록 흩어진다
                        float far = at.magnitude / (arenaRadius * 1.6f);
                        if (Random.value < far * 0.55f) continue;

                        string pick = houses[(lot * 3 + (s > 0 ? 1 : 0)) % houses.Length];
                        if (!place(Idx(pick), at, false)) continue;
                        n++;

                        // 집 뒤(길 반대쪽) 텃밭 울타리 — 마당이 길 쪽에 있으면 실제와 반대다
                        if (lot % 2 == 0 &&
                            place(Idx("village_fence_0"), at + nrm * 2.6f * s, false)) n++;

                        // 생활 흔적 — 집 옆에 건초·수레. 전부 두면 지저분하다
                        if (lot % 4 == 0)
                            if (place(Idx((lot % 8 == 0) ? "village_haystack_0" : "village_cart_0"),
                                      at + d * 2.2f, false)) n++;
                    }

                    // 가로등은 길가에, 큰길에만 드문드문
                    if (isMain && lot % 3 == 0)
                        if (place(Idx("village_lamp_0"), c + nrm * 1.9f, false)) n++;
                }
            }

            // 우물 — 큰길가의 랜드마크. 싸울 자리 밖에 둔다.
            var wellAt = OnRoad(roads[0], 0.46f) + new Vector2(0f, arenaRadius * 0.30f);
            if (place(Idx("village_well_0"), wellAt, false)) n++;

            return n;
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
                // 나무(오너 지시 2026-08-15 「나무도 배치하고」). 평원에 키 큰 것이 하나도
                // 없어서 마을만 덩그러니 서 있었다. 자연물이므로 노이즈 산포에 섞인다 —
                // ⚠️ **반드시 마을 구성물 앞**에 둘 것. 뒤에 두면 `ScatterCount`가 잘라내
                //    화면에 조용히 안 나온다.
                "field_tree_0", "field_tree_1", "field_tree_2", "field_tree_3",
                "field_shrub_row_0",
                // 마을 구성물(오너 지시 2026-08-15). 노이즈 산포에는 **섞이지 않는다** —
                // `NATURE_COUNT`로 앞쪽 자연물만 흩뿌리고, 아래 10종은 `BuildVillage`가
                // 정해진 자리에 세운다. 집이 풀처럼 흩뿌려지면 마을이 아니라 난개발이다.
                "village_house_0", "village_house_1", "village_house_2",
                "village_barn_0", "village_well_0",
                "village_fence_0", "village_fence_1",
                "village_haystack_0", "village_cart_0", "village_lamp_0",
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
