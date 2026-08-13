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

        /// <summary>
        /// 전투 공간 가장자리에 배경 프랍을 배치한다.
        /// </summary>
        /// <param name="bank">게임 스프라이트 뱅크 (머티리얼 템플릿용)</param>
        /// <param name="arenaRadius">전투 아레나 반경</param>
        /// <param name="seed">난수 시드 (재현성)</param>
        /// <param name="biome">배경 테마 (프랍 선택에 영향)</param>
        public static void Build(SpriteBank bank, float arenaRadius, int seed, Biome biome)
        {
            Random.InitState(seed);

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

            // ── 배치: arenaRadius ~ arenaRadius*1.8 링 ────────────────
            const int PROP_COUNT_MIN = 30;
            const int PROP_COUNT_MAX = 60;
            int count = Random.Range(PROP_COUNT_MIN, PROP_COUNT_MAX + 1);

            const float ISO_Y = StressTest.ISO_Y;

            // 임시 게임 오브젝트 폴더 생성 (씬에서 찾기 쉽도록)
            var decorRoot = new GameObject("FieldDecor_Props");

            for (int i = 0; i < count; i++)
            {
                // 링 위의 임의 위치 (아레나 밖에만)
                float angle = Random.value * Mathf.PI * 2f;
                float radius = Random.Range(arenaRadius, arenaRadius * 1.8f);
                Vector2 worldPos = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

                // 임의 프랍 선택
                int propIdx = Random.Range(0, propNames.Length);

                // 게임 오브젝트 생성
                var go = new GameObject("prop_" + propNames[propIdx], typeof(SpriteRenderer));
                go.transform.SetParent(decorRoot.transform, false);

                // 위치: 쿼터뷰 변환 (ISO_Y로 y 압축) + z는 바닥 앞, 캐릭터 뒤
                go.transform.position = new Vector3(worldPos.x, worldPos.y * ISO_Y, 0.1f);
                go.transform.localScale = Vector3.one;  // 프랍은 PPU로 크기 조정

                // 스프라이트 렌더러
                var sr = go.GetComponent<SpriteRenderer>();
                sr.sprite = Sprite.Create(atlas, rects[propIdx], Vector2.one * 0.5f, 32f, 0,
                                         SpriteMeshType.FullRect);
                sr.sharedMaterial = mat;

                // 정렬: y가 낮을수록 앞(화면에서 위쪽) — 캐릭터(500)보다 뒤
                // 쿼터뷰 Y 좌표로 깊이 정렬하되, 프랍 기본층(100) + 미세 정렬
                sr.sortingOrder = 100 - (int)(worldPos.y * 4f);
            }

            Debug.Log($"[FieldDecor] {biome} 프랍 {count}개 배치 (아틀라스 2048×2048, 배칭 유지)");
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
