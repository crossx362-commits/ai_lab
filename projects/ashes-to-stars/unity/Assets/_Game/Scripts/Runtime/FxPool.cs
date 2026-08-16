using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 전투 이펙트(오너 지시 2026-08-15 「이펙트 효과도 병렬 개발」).
    ///
    /// 왜 한 장짜리 심볼 + 코드 애니메이션인가:
    ///   프레임 시트로 뽑으면 분할·격자선 청소·정렬 파이프라인이 또 필요하고, 이 저장소는
    ///   그 단계에서 반복해서 사고를 냈다(격자선이 캐릭터를 관통, 붙은 셀이 병합 등).
    ///   심볼 한 장을 **크기·투명도·회전**으로 굴리면 그 위험이 0이면서 화면에서는
    ///   충분히 이펙트로 읽힌다. 프레임 애니메이션이 꼭 필요해지면 그때 올려도 늦지 않다.
    ///
    /// 왜 풀인가:
    ///   잡몹 상한이 200(§10-9는 300~500을 요구한다)이라 피격 이펙트가 초당 수십 개씩
    ///   난다. 매번 GameObject를 만들고 버리면 GC가 프레임을 갉는다 — 이 게임의 성능
    ///   기준은 「몹 상한에서 60fps」이므로 할당은 처음부터 없애 놓는다.
    ///
    /// ⚠️ 스프라이트가 없으면 **조용히 아무것도 안 그린다.** 이펙트가 안 보이는데 오류도
    ///    없다면 `Resources/fx/` 반입부터 확인할 것(경고는 종류당 한 번만 낸다).
    /// </summary>
    public class FxPool : MonoBehaviour
    {
        public enum Kind { Hit, Slash, Heal, Fire, Shield, Taunt, Summon, Death }

        static readonly string[] FILES =
        {
            "fx_hit", "fx_slash", "fx_heal", "fx_fire", "fx_shield", "fx_taunt",
            "fx_summon", "fx_death",
        };

        /// <summary>종류별 (수명 초, 시작 크기, 끝 크기). 지속과 팽창이 이펙트의 성격을 만든다.</summary>
        static readonly (float life, float from, float to)[] SHAPE =
        {
            (0.22f, 0.55f, 1.10f),   // Hit    — 짧고 탁 터진다
            (0.26f, 0.75f, 1.25f),   // Slash  — 베고 사라진다
            (0.70f, 0.60f, 1.30f),   // Heal   — 느긋하게 퍼진다
            (0.55f, 0.45f, 1.60f),   // Fire   — 크게 번진다
            (0.60f, 1.10f, 0.90f),   // Shield — 감싸며 오므라든다
            (0.45f, 0.30f, 1.80f),   // Taunt  — 충격파처럼 멀리
            (0.90f, 0.70f, 1.05f),   // Summon — 오래 남는 마법진
            (0.50f, 0.80f, 1.30f),   // Death  — 흩어진다
        };

        const int CAP = 96;          // 동시 표시 상한. 넘치면 가장 오래된 것을 재사용한다
        const float ISO_Y = StressTest.ISO_Y;

        static FxPool _inst;
        static bool[] _warned;

        Sprite[] _sprites;
        SpriteRenderer[] _sr;
        float[] _t, _life, _from, _to, _spin;
        Color[] _tint;
        int[] _jobStyle;
        int _cursor;

        public static FxPool Instance
        {
            get
            {
                if (_inst != null) return _inst;
                var go = new GameObject("FxPool");
                _inst = go.AddComponent<FxPool>();
                return _inst;
            }
        }

        /// <summary>씬이 바뀌면 풀이 파괴된다 — 다음 접근 때 새로 만들게 참조를 끊는다.</summary>
        void OnDestroy() { if (_inst == this) _inst = null; }

        void Awake()
        {
            _sprites = new Sprite[FILES.Length];
            _warned ??= new bool[FILES.Length];
            for (int i = 0; i < FILES.Length; i++)
            {
                var tex = Resources.Load<Texture2D>("fx/" + FILES[i]);
                if (tex == null) continue;
                // 심볼은 화면에서 대략 캐릭터(2유닛) 정도로 보이면 된다 — PPU를 높이 대비로 잡는다.
                _sprites[i] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                            Vector2.one * 0.5f, tex.height / 2f, 0,
                                            SpriteMeshType.FullRect);
            }

            _sr = new SpriteRenderer[CAP];
            _t = new float[CAP]; _life = new float[CAP];
            _from = new float[CAP]; _to = new float[CAP];
            _spin = new float[CAP]; _tint = new Color[CAP];
            _jobStyle = new int[CAP];

            for (int i = 0; i < CAP; i++)
            {
                var go = new GameObject("fx");
                go.transform.SetParent(transform, false);
                _sr[i] = go.AddComponent<SpriteRenderer>();
                _sr[i].sortingOrder = 700;      // 캐릭터(500)보다 앞 — 이펙트는 가려지면 의미가 없다
                go.SetActive(false);
            }
        }

        /// <summary>이펙트 하나를 월드 좌표에 띄운다. 색조를 주면 곱해진다(계열색 연출용).</summary>
        public static void Play(Kind kind, Vector2 worldPos, float scale = 1f, Color? tint = null)
        {
            var p = Instance;
            if (p == null || p._sr == null) return;

            int k = (int)kind;
            if (p._sprites[k] == null)
            {
                if (!_warned[k])
                {
                    _warned[k] = true;
                    Debug.LogWarning($"[FxPool] 이펙트 스프라이트 없음: Resources/fx/{FILES[k]} — 이 종류는 안 그려진다");
                }
                return;
            }

            int i = p._cursor;
            p._cursor = (p._cursor + 1) % CAP;

            var (life, from, to) = SHAPE[k];
            p._t[i] = 0f;
            p._life[i] = life;
            p._from[i] = from * scale;
            p._to[i] = to * scale;
            // 회전은 종류마다 성격이 다르다 — 충격파는 안 돌고 마법진은 천천히 돈다.
            p._spin[i] = kind == Kind.Summon ? 40f : kind == Kind.Slash ? Random.Range(-25f, 25f) : 0f;
            p._tint[i] = tint ?? Color.white;
            p._jobStyle[i] = 0;

            var tr = p._sr[i].transform;
            tr.position = new Vector3(worldPos.x, worldPos.y * ISO_Y, -0.2f);
            tr.localRotation = Quaternion.Euler(0, 0, kind == Kind.Slash ? Random.Range(0f, 360f) : 0f);
            p._sr[i].sprite = p._sprites[k];
            p._sr[i].color = p._tint[i];
            p._sr[i].gameObject.SetActive(true);
        }

        /// <summary>4×2 직업 시트의 8프레임을 공격 수명 안에서 순서대로 재생한다.</summary>
        public static void PlayJob(int style, Vector2 worldPos, float scale = 1f)
        {
            var p = Instance;
            if (p == null || p._sr == null) return;
            var first = JobVfxSheets.Frame(style, 0);
            if (first == null) return;
            int i = p._cursor; p._cursor = (p._cursor + 1) % CAP;
            p._t[i] = 0f; p._life[i] = .34f; p._from[i] = .7f * scale; p._to[i] = 1.3f * scale;
            p._spin[i] = 0f; p._tint[i] = Color.white; p._jobStyle[i] = style + 1;
            var tr = p._sr[i].transform;
            tr.position = new Vector3(worldPos.x, worldPos.y * ISO_Y, -.2f); tr.localRotation = Quaternion.identity;
            p._sr[i].sprite = first; p._sr[i].color = Color.white; p._sr[i].gameObject.SetActive(true);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < CAP; i++)
            {
                if (!_sr[i].gameObject.activeSelf) continue;

                _t[i] += dt;
                float u = _t[i] / _life[i];
                if (u >= 1f) { _jobStyle[i] = 0; _sr[i].gameObject.SetActive(false); continue; }

                if (_jobStyle[i] > 0)
                    _sr[i].sprite = JobVfxSheets.Frame(_jobStyle[i] - 1, Mathf.Min(7, (int)(u * 8f)));

                // 크기는 처음에 빠르게 벌어지고 끝에서 느려진다 — 등속이면 밋밋하다.
                float e = 1f - (1f - u) * (1f - u);
                float s = Mathf.Lerp(_from[i], _to[i], e);
                _sr[i].transform.localScale = new Vector3(s, s * ISO_Y, 1f);

                if (_spin[i] != 0f)
                    _sr[i].transform.Rotate(0, 0, _spin[i] * dt);

                // 알파는 뒤쪽 절반에서만 뺀다. 처음부터 흐려지면 터지는 느낌이 죽는다.
                var c = _tint[i];
                c.a = u < 0.5f ? 1f : 1f - (u - 0.5f) * 2f;
                _sr[i].color = c;
            }
        }
    }
}
