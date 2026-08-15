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
                    int propIdx = Random.Range(0, ScatterCount(biome, propNames.Length));
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
                float px = rects[propIdx].height * atlas.height;         // 아틀라스 UV → 픽셀
                float ppu = px / Mathf.Max(0.05f, TargetUnits(propNames[propIdx]));
                sr.sprite = Sprite.Create(atlas, rects[propIdx], Vector2.one * 0.5f, ppu, 0,
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
        /// 노이즈로 흩뿌려도 되는 프랍의 개수. 평원은 앞 8종(자연물)만 산포 대상이고
        /// 뒤쪽 마을 구성물은 `BuildVillage`가 자리를 정한다.
        /// </summary>
        static int ScatterCount(Biome biome, int total) => biome == Biome.Field ? 8 : total;

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
        /// 배치 규칙 — 이 셋이 「마을처럼 보인다」를 만든다:
        ///   ① 집은 광장 둘레 **일정 간격**으로. 무작위면 난개발로 읽힌다
        ///   ② 집과 집 **사이를 울타리로 잇는다**. 빈틈이 있으면 흩어진 오두막이 된다
        ///   ③ 길가에 가로등, 집 옆에 건초·수레 — 생활의 흔적이 있어야 폐허가 아니다
        ///
        /// 광장 안(아레나)에는 **우물 하나만** 둔다. 그 이상 넣으면 전투 공간이 좁아진다.
        /// </summary>
        static int BuildVillage(float arenaRadius, string[] names,
                                System.Func<int, Vector2, bool, bool> place)
        {
            const float ISO_Y = StressTest.ISO_Y;
            int Idx(string n) => System.Array.IndexOf(names, n);
            int n = 0;

            // 집이 서는 고리 — 아레나 바로 바깥. 너무 멀면 화면 밖이라 안 보이고,
            // 너무 가까우면 전투에 끼어든다.
            float ringHouse = arenaRadius * 1.32f;
            float ringFence = arenaRadius * 1.12f;   // 광장과 집 사이 = 마당 경계
            float ringLamp = arenaRadius * 1.04f;    // 광장 가장자리 = 길가

            string[] houses = { "village_house_0", "village_house_1", "village_house_2",
                                "village_barn_0" };
            const int LOTS = 8;                       // 광장을 둘러싼 집터 수

            for (int i = 0; i < LOTS; i++)
            {
                float a = (i / (float)LOTS) * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));

                // ① 집 — 종류를 돌아가며 써서 같은 집이 이웃하지 않게 한다
                if (place(Idx(houses[i % houses.Length]), dir * ringHouse, false)) n++;

                // ③ 집 옆 생활 흔적 — 한 집 걸러 하나씩(전부 두면 지저분하다)
                if (i % 2 == 0)
                {
                    var side = new Vector2(-dir.y, dir.x) * (arenaRadius * 0.16f);
                    string trinket = (i % 4 == 0) ? "village_haystack_0" : "village_cart_0";
                    if (place(Idx(trinket), dir * ringHouse + side, false)) n++;
                }

                // ② 울타리 — 집터 앞을 가로지르고, 집터 경계에는 모서리 기둥
                if (place(Idx("village_fence_1"), dir * ringFence, false)) n++;
                float half = (Mathf.PI * 2f / LOTS) * 0.3f;
                for (int s = -1; s <= 1; s += 2)
                {
                    float fa = a + half * s;
                    var fd = new Vector2(Mathf.Cos(fa), Mathf.Sin(fa));
                    if (place(Idx("village_fence_0"), fd * ringFence, false)) n++;
                }

                // ③ 가로등 — 집터 사이 길목마다
                float la = a + (Mathf.PI / LOTS);
                var ld = new Vector2(Mathf.Cos(la), Mathf.Sin(la));
                if (place(Idx("village_lamp_0"), ld * ringLamp, false)) n++;
            }

            // 우물 — 광장의 랜드마크. 중앙은 스폰 지점이라 비우고 한쪽으로 치운다.
            // 엄폐 등록은 하지 않는다: 광장 안 장애물은 W1~W3 구성 비교를 흔든다(§3-4).
            if (place(Idx("village_well_0"), new Vector2(arenaRadius * 0.62f, 0f), false)) n++;

            _ = ISO_Y;   // 좌표는 place가 ISO 보정한다 — 여기서 두 번 곱하지 않는다
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
