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
    }

    public static class FxParticles
    {
        const int Pool = 24;                 // 동시 재생 상한. 넘치면 가장 오래된 것을 재사용한다
        static ParticleSystem[] _pool;
        static int _cursor;
        static Material _mat;
        static Transform _root;

        static void EnsureBuilt()
        {
            if (_pool != null) return;

            // 부드러운 원형 점 — 32×32 하나로 모든 이펙트를 만든다.
            // 픽셀아트에 딱딱한 사각 입자를 섞으면 도트가 아니라 노이즈로 보인다.
            var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f)) / 15.5f;
                    float a = Mathf.Clamp01(1f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));   // 제곱 = 가장자리가 더 빨리 사라진다
                }
            tex.Apply();

            // Sprites/Default는 어느 프로젝트에나 있고 알파 블렌드가 정직하다.
            // Additive를 쓰면 잿빛 연기(사망)가 밝게 타올라 버려서 상황과 어긋난다.
            var sh = Shader.Find("Sprites/Default");
            _mat = new Material(sh) { mainTexture = tex };

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
            r.material = _mat;
            r.sortMode = ParticleSystemSortMode.None;
            r.sortingOrder = 900;               // 캐릭터(±y*16)보다 확실히 위
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
