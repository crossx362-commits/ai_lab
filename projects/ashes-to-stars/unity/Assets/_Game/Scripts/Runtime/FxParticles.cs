using UnityEngine;

namespace AshesToStars
{
    // ─────────────────────────────────────────────────────────────
    // 유니티 파티클 이펙트 (오너 지시 2026-08-14)
    //
    // 왜 프리팹·에셋이 아니라 코드로 만드나:
    //   ① **아트 신규 0장.** 파티클 텍스처는 런타임에 생성한 부드러운 점 하나뿐이고,
    //      나머지는 색·수명·속도·크기 곡선으로 만든다. 아트 물량 산정(아트문서 §0-B)을 건드리지 않는다.
    //   ② 프리팹은 씬·GUID에 묶여 있어 **머지 충돌과 고아 meta**가 나기 쉽다.
    //      이 프로젝트는 rename 한 번에 GUID 32개가 고아가 될 뻔한 적이 있다(인수인계 §5).
    //   ③ 코드면 값이 왜 그런지 주석으로 남는다 — 인스펙터 값은 이유를 못 적는다.
    //
    // 기존 스프라이트 FX(fx_taunt_ 등)를 **대체하지 않고 겹쳐 쓴다.** 스프라이트는 "장판의 범위"를
    // 알려주고(게임 규칙), 파티클은 "지금 터졌다"를 알려준다(순간 피드백). 역할이 다르다.
    //
    // ⚠️ 쿼터뷰 픽셀아트다. 파티클이 크고 뿌옇게 깔리면 도트가 묻힌다 —
    //    작게(0.1~0.35), 짧게(0.3~0.9초), 그리고 반드시 캐릭터보다 **뒤나 앞 한쪽**에만 둔다.
    // ─────────────────────────────────────────────────────────────

    public enum FxKind
    {
        도발,       // 탱 주변으로 퍼지는 금색 파문
        화염폭풍,   // 장판 위로 솟는 불티
        치유파동,   // 아래에서 위로 오르는 초록 입자
        기적,       // 금색 기둥 — 판을 뒤집는 순간이라 가장 크게
        일섬,       // 순간적인 흰 섬광 조각
        피격,       // 붉은 스파크 — 맞았다는 즉각 피드백
        사망,       // 잿빛 연기
        무적,       // §5 대시 무적 표시. 이게 안 보이면 무적은 학습 불가능한 기술이 된다
        광륜,       // 빛 자체 — Additive 큰 소프트 원. 빌트인엔 블룸이 없으므로 이게 "빛"이다
        쇼크웨이브, // 밖으로 퍼지는 고리 — 보스 착지·폭발
        먼지,       // 착지·이동 — 유일하게 어두워야 하는 것(알파)
        마법진,     // 장판 예고(§10-5) — 바닥에 깔린다
    }

    public static class FxParticles
    {
        const int Pool = 24;                 // 동시 재생 상한. 넘치면 가장 오래된 것을 재사용한다
        static ParticleSystem[] _pool;
        static int _cursor;
        static Material _matAlpha;           // 연기·먼지 — 어두워질 수 있어야 한다
        static Material _matAdd;             // 불·성광·무적 — 빛은 더해져야 빛으로 읽힌다
        static Transform _root;
        static Texture2D _haloTex, _sparkTex;

        // ── 정렬 계층 (조사 결론)
        //   205 = 바닥 장판·마법진(유닛보다 뒤) / 210 = 캐릭터 뒤 광륜 / 900 = 앞 섬광
        const int SortGround = 205, SortBehind = 210, SortFront = 900;

        static void EnsureBuilt()
        {
            if (_pool != null) return;

            // ── 입자 텍스처 두 장 (조사 결론: 용도가 다르면 텍스처도 달라야 한다)
            //   ① 하드에지 8×8 — 스파크·불티. 픽셀아트 옆에서 부드러운 원은 도트가 아니라 얼룩이다
            //   ② 소프트 광륜 64×64 — "빛"은 이것 하나로만 만든다(부드러움을 여기로 격리)
            var spark = new Texture2D(8, 8, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(3.5f, 3.5f)) / 3.5f;
                    spark.SetPixel(x, y, new Color(1f, 1f, 1f, d <= 1f ? 1f : 0f));   // 계단 없는 단단한 점
                }
            spark.Apply();

            var halo = new Texture2D(64, 64, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float d = Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 31.5f);
                    float a = Mathf.Pow(1f - d, 2.2f);
                    halo.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            halo.Apply();

            // 알파 블렌드 — 연기·먼지처럼 **어두워질 수 있어야 하는** 것.
            _matAlpha = new Material(Shader.Find("Sprites/Default")) { mainTexture = spark };

            // Additive — 불·성광·무적. 유니티 문서 자신이 "glow effects, like fire or magic spells"에
            // Additive를 쓰라고 적어둔 용도다. 빌트인 파이프라인엔 블룸이 없고(포스트프로세싱 미포함),
            // `Sprites/Default`는 `Lighting Off`라 **파티클 Lights 모듈은 화면에 아무 변화도 안 준다** —
            // 그래서 "빛"은 Additive + 큰 소프트 광륜으로 만든다.
            // ⚠️ 이 셰이더는 `m_AlwaysIncludedShaders`에 등록해 두었다.
            //    등록 안 하면 **에디터는 정상, 빌드는 분홍**이 된다.
            var add = Shader.Find("Legacy Shaders/Particles/Additive");
            _matAdd = add != null ? new Material(add) { mainTexture = halo }
                                  : new Material(Shader.Find("Sprites/Default")) { mainTexture = halo };
            if (add == null) Debug.LogWarning("[FX] Additive 셰이더를 못 찾았다 — 알파로 대체(빛이 덜 빛난다)");
            _haloTex = halo; _sparkTex = spark;

            _root = new GameObject("FxParticles").transform;
            _pool = new ParticleSystem[Pool];
            for (int i = 0; i < Pool; i++) _pool[i] = Build(i);
        }

        static ParticleSystem Build(int i)
        {
            var go = new GameObject("fx" + i);
            go.transform.SetParent(_root, false);
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;  // 캐릭터가 움직여도 불티는 제자리
            main.maxParticles = 120;

            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = 0f;               // 버스트로만 낸다 — 지속 방출은 픽셀 화면을 덮는다

            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.material = _matAlpha;
            r.sortMode = ParticleSystemSortMode.None;   // 정렬 비용 0 — 500체 화면에서 이게 예산이다
            r.sortingOrder = SortFront;
            return ps;
        }

        /// <summary>
        /// 이펙트를 한 번 재생한다. 위치는 **월드 좌표**(쿼터뷰 변환은 호출부가 이미 했다).
        /// scale로 장판 반지름 등을 넘긴다(1 = 기본 크기).
        /// </summary>
        public static void Play(FxKind kind, Vector3 pos, float scale = 1f, Color? tint = null)
        {
            EnsureBuilt();
            var ps = _pool[_cursor];
            _cursor = (_cursor + 1) % Pool;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.transform.position = pos;

            // 재질·정렬은 **종류가 결정한다**. 연기를 Additive로 그리면 잿빛이 밝게 타오르고,
            // 빛을 알파로 그리면 그냥 흰 얼룩이 된다.
            var rend = ps.GetComponent<ParticleSystemRenderer>();
            bool glow = kind == FxKind.광륜 || kind == FxKind.무적 || kind == FxKind.기적 ||
                        kind == FxKind.치유파동 || kind == FxKind.일섬 || kind == FxKind.화염폭풍 ||
                        kind == FxKind.쇼크웨이브 || kind == FxKind.마법진;
            rend.material = glow ? _matAdd : _matAlpha;
            rend.sortingOrder = kind == FxKind.마법진 ? SortGround
                              : (kind == FxKind.광륜 || kind == FxKind.무적) ? SortBehind
                              : SortFront;

            var main = ps.main;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radiusThickness = 1f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, Curve(kind));

            int count;
            switch (kind)
            {
                case FxKind.도발:
                    // 밖으로 퍼지는 파문 — "이쪽을 봐라"라는 신호이므로 방향이 바깥이어야 한다
                    count = 28; shape.radius = 0.6f * scale;
                    main.startLifetime = 0.55f; main.startSpeed = 3.2f * scale;
                    main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.24f);
                    main.startColor = Grad(new Color(1f, 0.85f, 0.35f), new Color(1f, 0.6f, 0.15f));
                    break;
                case FxKind.화염폭풍:
                    // 불티는 위로 솟았다 꺼진다. 중력을 음수로 줘 떠오르게 한다
                    count = 40; shape.radius = 1.6f * scale;
                    main.startLifetime = 0.7f; main.startSpeed = 0.6f;
                    main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
                    main.gravityModifier = -0.9f;
                    main.startColor = Grad(new Color(1f, 0.72f, 0.2f), new Color(1f, 0.28f, 0.1f));
                    break;
                case FxKind.치유파동:
                    count = 22; shape.radius = 0.9f * scale;
                    main.startLifetime = 0.8f; main.startSpeed = 0.4f;
                    main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
                    main.gravityModifier = -1.4f;      // 회복은 위로 — 관례를 따르는 편이 읽기 쉽다
                    main.startColor = Grad(new Color(0.6f, 1f, 0.7f), new Color(1f, 1f, 0.85f));
                    break;
                case FxKind.기적:
                    // 판을 뒤집는 순간(§3 사제). 가장 크고 가장 오래 남는다 — 놓치면 안 되는 사건이다
                    count = 60; shape.radius = 0.35f * scale;
                    main.startLifetime = 0.95f; main.startSpeed = 4.5f;
                    main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.34f);
                    main.gravityModifier = -1.8f;
                    main.startColor = Grad(new Color(1f, 0.95f, 0.6f), new Color(1f, 1f, 1f));
                    break;
                case FxKind.일섬:
                    count = 14; shape.radius = 0.2f;
                    main.startLifetime = 0.22f; main.startSpeed = 7f;
                    main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
                    main.startColor = Grad(new Color(1f, 1f, 1f), new Color(0.8f, 0.9f, 1f));
                    break;
                case FxKind.피격:
                    count = 8; shape.radius = 0.15f;
                    main.startLifetime = 0.3f; main.startSpeed = 2.4f;
                    main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.13f);
                    main.gravityModifier = 1.2f;
                    main.startColor = Grad(new Color(1f, 0.35f, 0.3f), new Color(0.7f, 0.1f, 0.1f));
                    break;
                case FxKind.사망:
                    count = 16; shape.radius = 0.35f;
                    main.startLifetime = 0.9f; main.startSpeed = 0.5f;
                    main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.36f);
                    main.gravityModifier = -0.25f;
                    main.startColor = Grad(new Color(0.55f, 0.55f, 0.6f), new Color(0.25f, 0.25f, 0.3f));
                    break;
                case FxKind.광륜:
                    // 빛 그 자체. 큰 소프트 원 하나를 낮은 알파로 **캐릭터 뒤에** 둔다 —
                    // 앞에 두면 도트를 덮어 픽셀아트가 뭉갠 것처럼 보인다.
                    count = 1; shape.radius = 0.01f;
                    main.startLifetime = 0.6f; main.startSpeed = 0f;
                    main.startSize = new ParticleSystem.MinMaxCurve(2.6f * scale);
                    main.startColor = Grad(new Color(1f, 0.92f, 0.6f, 0.30f), new Color(1f, 0.8f, 0.4f, 0.22f));
                    break;
                case FxKind.쇼크웨이브:
                    // 고리 — 가장자리에서만 나오게 해 링으로 읽히게 한다(radiusThickness 0)
                    count = 34; shape.radius = 0.5f * scale; shape.radiusThickness = 0f;
                    main.startLifetime = 0.45f; main.startSpeed = 6.5f * scale;
                    main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
                    main.startColor = Grad(new Color(1f, 1f, 1f, 0.9f), new Color(0.7f, 0.85f, 1f, 0.5f));
                    break;
                case FxKind.먼지:
                    count = 10; shape.radius = 0.3f * scale;
                    main.startLifetime = 0.5f; main.startSpeed = 1.1f;
                    main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
                    main.gravityModifier = 0.35f;
                    main.startColor = Grad(new Color(0.72f, 0.68f, 0.6f, 0.7f), new Color(0.5f, 0.47f, 0.42f, 0.3f));
                    break;
                case FxKind.마법진:
                    // 장판 예고(§10-5) — **바닥에** 깔려야 "저기 온다"로 읽힌다.
                    // 쿼터뷰라 세로를 눌러 원이 아니라 타원으로 보이게 한다
                    count = 46; shape.radius = 1f * scale; shape.radiusThickness = 0f;
                    main.startLifetime = 1.1f; main.startSpeed = 0f;
                    main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.26f);
                    main.startColor = Grad(new Color(1f, 0.5f, 0.35f, 0.75f), new Color(1f, 0.25f, 0.2f, 0.45f));
                    break;
                default:   // 무적 — §5의 핵심 기술이 눈에 보이게 하는 표시
                    count = 18; shape.radius = 0.5f * scale;
                    main.startLifetime = 0.35f; main.startSpeed = 1.2f;
                    main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.18f);
                    main.startColor = Grad(new Color(1f, 0.9f, 0.4f), new Color(0.6f, 1f, 1f));
                    break;
            }

            if (tint.HasValue)
            {
                // 아트문서 §0-A: 무적 표시는 직업군별 색(탱=금 / 근접딜=보라 / 원거리딜=청록 / 지원=금+초록).
                // 무적이 눈에 안 보이면 §5의 핵심 기술이 학습 불가능해진다.
                var c = tint.Value;
                main.startColor = Grad(c, Color.Lerp(c, Color.white, 0.5f));
            }
            ps.Emit(count);
        }

        /// <summary>수명 동안의 크기 곡선. 터질 때 크고 사라질 때 작아진다.</summary>
        static AnimationCurve Curve(FxKind kind)
        {
            if (kind == FxKind.기적 || kind == FxKind.도발)
                return AnimationCurve.EaseInOut(0f, 0.4f, 1f, 1f);   // 퍼지는 것은 커지며 사라진다
            return AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        }

        static ParticleSystem.MinMaxGradient Grad(Color a, Color b) =>
            new ParticleSystem.MinMaxGradient(a, b) { mode = ParticleSystemGradientMode.TwoColors };
    }
}
