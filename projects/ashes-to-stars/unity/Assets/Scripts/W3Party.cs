using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using AshesToStars;      // FxParticles — 검증 스크립트는 전역 네임스페이스, 게임 코드는 AshesToStars

/// <summary>
/// 재와 별 — W3 파티·자동 전투 검증 (§21 프로토타입 W3)
///
/// 검증 질문: **자동/수동 이중구조가 실제로 굴러가는가.**
///   ① 파티 3인(탱·딜·힐)이 각자 역할대로 AI로 싸우는가
///   ② 어그로 규칙(§10-4)이 진형을 만들어내는가 — 근접몹은 최근접, 원거리몹은 후열 저격,
///      탱의 도발이 그걸 끊는다. 이게 성립해야 §9 "레이드 1인 불가"의 기반이 선다
///   ③ 전투 스타일(공격/균형/방어/생존, §3)이 실제로 다른 결과를 내는가
///   ④ 정예몹(주술사=치유, 소환술사)이 "무엇을 먼저 죽일까"를 만드는가
///
/// 측정: 스타일별로 판을 돌려 생존 시간·처치 수·피격 분포를 비교한다.
/// 스타일을 바꿔도 결과가 같으면 그 시스템은 존재 의미가 없다는 뜻이다.
/// </summary>
public class W3Party : MonoBehaviour
{
    // ── 전투 스타일 (§3 수치) ────────────────────────────
    public enum Style { Aggressive, Balanced, Defensive, Survival }

    struct StyleSpec
    {
        public float DmgMul, TakenMul, RetreatHp, KeepDist;
        public StyleSpec(float d, float t, float r, float k) { DmgMul = d; TakenMul = t; RetreatHp = r; KeepDist = k; }
    }

    static StyleSpec Spec(Style s) => s switch
    {
        Style.Aggressive => new StyleSpec(1.15f, 1.20f, 0.00f, 0.9f),
        Style.Balanced => new StyleSpec(1.00f, 1.00f, 0.15f, 1.4f),
        Style.Defensive => new StyleSpec(0.90f, 0.85f, 0.30f, 2.6f),
        Style.Survival => new StyleSpec(0.70f, 0.75f, 0.50f, 4.0f),
        _ => new StyleSpec(1, 1, 0, 1.5f),
    };

    // ── 파티원 ───────────────────────────────────────────
    enum Role { Tank, Dps, Healer, Buffer }
    /// <summary>기획서 §3의 1차 전직 — 각자 고유 메커니즘 1개 + 대표 스킬</summary>
    enum Job { 수호기사, 검사, 마법사, 사제, 음유시인 }
    enum Chant { 진군가, 수호가 }        // 음유시인 악장 (§3 악장 전환)

    class Member
    {
        public Role Role;
        public Style Style;
        public Transform Tr;
        public SpriteRenderer Sr;
        public Vector2 Pos;
        public Job Job;
        public float Hp, MaxHp, Atk, Range, Cd, SkillCd;
        public float Shield;              // 수호기사 성채 방패
        public float Gauge;               // 고유 자원: 수호게이지 / 연격스택 / 신앙
        public float FlashT;              // 스킬 발동 섬광 잔여 시간
        public Chant Chant;               // 음유시인 악장
        public float Threat;                 // 어그로 수치
        public bool Alive => Hp > 0f;
        public float DeadT;

        // ── 연출 ──
        public SpriteRenderer BarBg, BarFg;  // 머리 위 체력바
        public Vector2 PrevPos;              // 이동 여부 판정용
        public float AnimT;                  // 현재 동작이 시작된 뒤 흐른 시간
        public float AttackT, HurtT;         // 남은 동작 시간 (0보다 크면 그 동작 중)
        /// <summary>
        /// 스킬 동작(special 프레임) 재생 시간.
        /// 시트의 "도발/회피/캐스팅/오라/제스처" 열이 곧 이 동작이다(§3) —
        /// 프레임은 있는데 아무도 안 써서 스킬이 화면에서 평타와 구분되지 않았다.
        /// </summary>
        public float SkillT;
        public SpriteBank.Motion Mo;

        /// <summary>수동 이동 명령 목적지(§5). 있으면 AI 판단을 덮는다.</summary>
        public Vector2? Order;
        /// <summary>
        /// 강제 발동할 스킬 슬롯(0=없음, 1·2). 버튼을 누르면 세워지고 발동하면 0으로 돌아간다.
        /// 쿨다운만 0으로 만드는 방식은 **조건이 안 맞으면 아무 일도 안 일어나** 눌러도 반응이 없다 —
        /// 지휘가 성립하려면 "눌렀는데 왜 안 나가지"가 없어야 한다(§5).
        /// </summary>
        public int ForceSkill;
        /// <summary>명령 지점 표시(땅에 찍히는 점)</summary>
        public SpriteRenderer Marker;
    }

    // ── 수동 지휘 상태 (§5 "보스는 수동 지휘") ──
    int _sel = -1;                     // 선택된 파티 슬롯. -1이면 선택 없음
    GUIStyle _cmdBtn, _cmdLabel;

    // 글자 뒤에 까는 반투명 판.
    // 전투 화면은 배경을 안 깔기 때문에(카메라 렌더를 보여줘야 하므로) 밝은 바닥 위에
    // 흰 글씨가 그대로 놓여 읽히지 않았다(오너 지적 "글씨가 안보인다고").
    // 스킬 범위 표시용 원형 텍스처. 스킬이 수치로만 돌면 화면에서는 아무 일도 안 일어난 것처럼 보인다
    // — 도발이 몹을 모으는 것도, 화염폭풍이 밀집을 노리는 것도 보여야 판단 근거가 된다(§5 수동 지휘).
    static Sprite _ringSprite;
    static Sprite Ring()
    {
        if (_ringSprite != null) return _ringSprite;
        const int N = 128;
        var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
        var px = new Color32[N * N];
        float c = (N - 1) * 0.5f;
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                // 가장자리는 진하게, 안쪽은 옅게 — 범위가 어디까지인지 읽히게
                byte a = d > 1f ? (byte)0 : (byte)(d > 0.86f ? 200 : 34 * (1f - d));
                px[y * N + x] = new Color32(255, 255, 255, a);
            }
        t.SetPixels32(px); t.filterMode = FilterMode.Bilinear; t.Apply();
        _ringSprite = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
        return _ringSprite;
    }

    SpriteRenderer _tauntRing, _stormRing;
    float _stormUntil; Vector2 _stormAt; float _stormR;

    // ── 스킬 이펙트 (Resources/FX) ────────────────────────
    // 절차 생성 링만으로는 어떤 스킬인지 구분이 안 된다. 실제 이펙트 아트를 얹는다.
    // 아틀라스에 넣지 않는 이유: 동시에 떠 있는 이펙트가 많아야 두세 개라 배칭 이득이 없고,
    // 큰 텍스처(256²)를 캐릭터 아틀라스에 밀어 넣으면 그쪽이 넘친다.
    static Sprite[] _fxTaunt, _fxStorm, _fxHeal;
    static Sprite _fxMiracle, _fxChantAtk, _fxChantDef;
    static bool _fxLoaded;

    static Sprite[] LoadFx(string prefix, int n)
    {
        var a = new Sprite[n];
        for (int i = 0; i < n; i++) a[i] = Resources.Load<Sprite>($"FX/{prefix}{i:00}");
        return a;
    }

    static void LoadFxOnce()
    {
        if (_fxLoaded) return;
        _fxLoaded = true;
        _fxTaunt = LoadFx("fx_taunt_", 4);
        _fxStorm = LoadFx("fx_firestorm_", 4);
        _fxHeal = LoadFx("fx_heal_wave_", 3);
        _fxMiracle = Resources.Load<Sprite>("FX/fx_miracle_light");
        _fxChantAtk = Resources.Load<Sprite>("FX/fx_bardic_attack");
        _fxChantDef = Resources.Load<Sprite>("FX/fx_bardic_defense");

        // 없으면 절차 생성 링으로 폴백한다 — 조용히 아무것도 안 뜨는 것보다 낫다
        int missing = 0;
        if (_fxTaunt[0] == null) missing++;
        if (_fxStorm[0] == null) missing++;
        if (missing > 0)
            Debug.LogWarning($"[W3] 스킬 이펙트 {missing}종 누락 — Resources/FX 확인. 링으로 대체한다");
    }

    /// <summary>프레임 배열에서 경과 시간에 맞는 장을 고른다. 끝나면 마지막 장.</summary>
    static Sprite FxFrame(Sprite[] a, float t, float dur)
    {
        if (a == null || a.Length == 0 || a[0] == null) return null;
        int i = Mathf.Clamp(Mathf.FloorToInt(t / Mathf.Max(0.01f, dur) * a.Length), 0, a.Length - 1);
        return a[i] ?? a[0];
    }

    // 단색 사각형용 1×1 텍스처 캐시. IMGUI로 카드·바를 그리려면 색마다 텍스처가 필요하다.
    static readonly System.Collections.Generic.Dictionary<Color, Texture2D> _tints = new();
    static Texture2D Tint(Color c)
    {
        if (_tints.TryGetValue(c, out var t) && t != null) return t;
        t = new Texture2D(1, 1);
        t.SetPixel(0, 0, c);
        t.Apply();
        _tints[c] = t;
        return t;
    }

    static Texture2D _scrimTex;
    static Texture2D Scrim()
    {
        if (_scrimTex != null) return _scrimTex;
        _scrimTex = new Texture2D(1, 1);
        _scrimTex.SetPixel(0, 0, new Color(0.02f, 0.02f, 0.04f, 0.78f));
        _scrimTex.Apply();
        return _scrimTex;
    }

    /// <summary>
    /// 머리 위 체력바를 만든다. 배경 + 전경 두 장이며 둘 다 아틀라스의 흰 칸을 쓰므로
    /// 배칭이 깨지지 않는다(W1 성능의 전제).
    /// </summary>
    static void MakeHpBar(Transform parent, SpriteBank bank,
                          out SpriteRenderer bg, out SpriteRenderer fg, float w, float y)
    {
        var bgo = new GameObject("hp_bg", typeof(SpriteRenderer));
        bgo.transform.SetParent(parent, false);
        bgo.transform.localPosition = new Vector3(0, y, -0.1f);
        bgo.transform.localScale = new Vector3(w, 0.13f, 1f);
        bg = bgo.GetComponent<SpriteRenderer>();
        bg.sprite = bank.White; bg.sharedMaterial = bank.Mat;
        bg.color = new Color(0.05f, 0.05f, 0.06f, 0.85f);
        bg.sortingOrder = 900;

        var fgo = new GameObject("hp_fg", typeof(SpriteRenderer));
        fgo.transform.SetParent(parent, false);
        fgo.transform.localPosition = new Vector3(0, y, -0.2f);
        fgo.transform.localScale = new Vector3(w * 0.94f, 0.09f, 1f);
        fg = fgo.GetComponent<SpriteRenderer>();
        fg.sprite = bank.White; fg.sharedMaterial = bank.Mat;
        fg.color = new Color(0.35f, 0.85f, 0.4f);
        fg.sortingOrder = 901;
    }

    /// <summary>
    /// 아틀라스 스프라이트에서 **캐릭터 몸통만** 잘라내는 UV.
    ///
    /// 스프라이트 캔버스에는 이펙트 여백이 많아(416×297에 캐릭터는 150px 남짓)
    /// 통째로 그리면 초상화가 깨알만 해진다. Sprite는 pivot(= 발밑)과 pixelsPerUnit을
    /// 들고 있으므로 몸통 영역을 역산할 수 있다: 높이 = 화면 목표 유닛 × PPU.
    /// </summary>
    static Rect PortraitUV(Sprite sp)
    {
        const float CHAR_UNITS = 2.0f;           // SpriteBank의 U_CHAR와 같은 값
        float h = CHAR_UNITS * sp.pixelsPerUnit;
        float w = h * 0.78f;
        float cx = sp.rect.x + sp.pivot.x;
        float y0 = sp.rect.y + sp.pivot.y - h * 0.04f;   // 발밑을 살짝 포함
        var t = sp.texture;
        return new Rect((cx - w * 0.5f) / t.width, y0 / t.height, w / t.width, h / t.height);
    }

    /// <summary>스킬 범위 링 하나. 바닥에 눕혀 그리므로 세로를 ISO_Y로 누른다.</summary>
    static SpriteRenderer MakeRing(SpriteBank bank, Color c)
    {
        var go = new GameObject("skill_ring", typeof(SpriteRenderer));
        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = Ring();
        sr.sharedMaterial = bank.Mat;
        sr.color = c;
        sr.sortingOrder = 210;          // 그림자(200)보다 앞, 유닛보다 뒤
        go.SetActive(false);
        return sr;
    }

    static void PlaceRing(SpriteRenderer sr, Vector2 world, float radius)
    {
        sr.transform.position = new Vector3(world.x, world.y * ISO_Y, -0.5f);
        sr.transform.localScale = new Vector3(radius * 2f, radius * 2f * ISO_Y, 1f);
    }

    /// <summary>
    /// 발밑 그림자 — 유닛이 **바닥에 서 있다**는 인상의 핵심이다.
    /// 그림자가 없으면 스프라이트가 공중에 떠 보여 쿼터뷰가 아니라 탑뷰처럼 읽힌다
    /// (2026-08-13 오너 지적). 자식으로 달아 두면 유닛을 따라 저절로 움직인다.
    /// </summary>
    static void MakeShadow(Transform parent, SpriteBank bank, float w)
    {
        var go = new GameObject("shadow", typeof(SpriteRenderer));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0.06f, 0.05f);
        // 쿼터뷰라 세로를 눌러 타원으로 — 정원이면 위에서 본 그림이 된다
        go.transform.localScale = new Vector3(w, w * ISO_Y * 0.55f, 1f);
        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = bank.White;
        sr.sharedMaterial = bank.Mat;
        sr.color = new Color(0f, 0f, 0f, 0.32f);
        sr.sortingOrder = 200;              // 바닥보다 앞, 유닛보다 뒤
    }

    /// <summary>
    /// 체력바를 비율에 맞춰 줄인다. 가운데가 아니라 **왼쪽 끝을 고정**해야
    /// 줄어드는 방향이 한쪽으로 보인다 — 스케일만 줄이면 양쪽에서 줄어들어 어색하다.
    /// </summary>
    static void SetBar(SpriteRenderer fg, float ratio, float fullW)
    {
        ratio = Mathf.Clamp01(ratio);
        var s = fg.transform.localScale;
        fg.transform.localScale = new Vector3(fullW * ratio, s.y, 1f);
        fg.transform.localPosition = new Vector3(-fullW * (1f - ratio) * 0.5f,
                                                 fg.transform.localPosition.y,
                                                 fg.transform.localPosition.z);
        fg.color = ratio > 0.5f ? new Color(0.35f, 0.85f, 0.4f)
                 : ratio > 0.25f ? new Color(0.95f, 0.78f, 0.3f)
                 : new Color(0.9f, 0.3f, 0.3f);
    }

    Member[] _party;
    Member[] _slots;

    // ── 몹 ───────────────────────────────────────────────
    const int MAXM = 200;
    Transform[] _mTr; SpriteRenderer[] _mSr;
    Vector2[] _mPos; float[] _mHp, _mCd, _mAtkCd, _mMaxHp; int[] _mKind; bool[] _mOn;
    SpriteRenderer[] _mBarBg, _mBarFg;      // 몹 체력바 (다친 개체만 표시)
    // kind: 0 추적 / 1 포위 / 2 원거리 / 3 정예-치유 / 4 정예-소환
    int _mAlive;

    const int MAXP = 200;
    Transform[] _pTr; Vector2[] _pPos, _pVel; float[] _pLife; bool[] _pOn; int _pCur;
    // 아군 공격 연출용 별도 풀 (몹 탄과 색으로 구분)
    Transform[] _aTr; Vector2[] _aPos, _aVel; float[] _aLife; bool[] _aOn; int _aCur;
    float[] _mFlash;

    // ── 설정 ─────────────────────────────────────────────
    [Header("판 설정 — 전멸까지 돌린다 (§21-1g)")]
    [Tooltip("안전 상한. 이 시간을 넘으면 강제 종료 — 무한 루프 방지용이지 판정 기준이 아니다")]
    public float 최대시간 = 240f;
    [Tooltip("시작 웨이브 크기")] public int 시작웨이브 = 12;
    [Tooltip("이 간격(초)마다 웨이브 압력이 한 단계 오른다")] public float 점증간격 = 12f;
    [Tooltip("한 단계마다 늘어나는 동시 몹 수")] public int 단계당증가 = 6;
    public float Arena = 14f;

    [Header("잡몹 속도 (플레이어 기준 배율 — §18-11)")]
    public float PlayerSpeed = 4.2f;
    // 기획서 §18-11 권장값 — 카이팅 시뮬레이션으로 도출됐다.
    // 0.64였을 때 직선 도주가 지배 전략이 되어 잡몹이 위협이 안 됐다(§21-1c).
    // ⚠️ 이 값은 MobDef 에셋과 **반드시 같아야** 한다 — 한때 여기만 0.80/0.70이라
    //    같은 기획을 두 곳이 다르게 구현하고 있었다(정합성 감사로 발견).
    public float ChaserRatio = 0.90f;
    public float SwarmerRatio = 0.85f;
    public float RangedRatio = 0.65f;

    const float ISO_Y = StressTest.ISO_Y;
    static Vector3 ToScreen(Vector2 w, float z = 0f) => new Vector3(w.x, w.y * ISO_Y, z);

    // ── 계측 ─────────────────────────────────────────────
    Style _style = Style.Balanced;
    float _t;
    int _kills, _tauntUses, _backlineHits, _frontlineHits, _healsCast;
    float _shieldAbsorbed;
    int _meleeHits, _shotHits, _framesThisRun;
    float _tauntUntil;                 // 도발이 원거리 몹까지 끄는 구간
    float _lastStandUntil = -1f;       // 최후의 보루 지속(§3 수호기사)
    float _lastStandCd = -1f;          // 최후의 보루 재사용 대기
    Chant _partyChant = Chant.진군가;   // 음유시인 악장 (파티 오라)
    // 스킬 사용 횟수: 0도발 1성채방패 2치유파동 3기적 4악장전환 5화염폭풍 6일섬
    readonly int[] _skillLog = new int[8];   // 7 = 최후의 보루(§3 수호기사)
    float _healerDeadT;
    // 사제 신앙의 판 최고치. 기적이 0회일 때 "신앙이 안 찼다"와 "조건이 안 걸렸다"를 가른다 —
    // 추측으로 원인을 고르지 않기 위한 계측이다(2026-08-13).
    float _faithPeak;
    string _outPath;
    readonly StringBuilder _csv = new StringBuilder();
    /// <summary>대조 실험 구성 (§21-1f) — 스타일은 균형형 고정, 구성만 바꾼다</summary>
    struct Setup
    {
        public string Name;
        public Job[] Jobs;
        public bool TauntEnabled;
        public Setup(string n, Job[] j, bool taunt = true) { Name = n; Jobs = j; TauntEnabled = taunt; }
    }

    static readonly Setup[] SETUPS =
    {
        new Setup("A_표준5인",   new[]{ Job.수호기사, Job.검사, Job.마법사, Job.사제, Job.음유시인 }),
        new Setup("B_딜특화",    new[]{ Job.수호기사, Job.검사, Job.마법사, Job.검사, Job.마법사 }),
        new Setup("C_1인",       new[]{ Job.검사 }),
        new Setup("D_도발OFF",   new[]{ Job.수호기사, Job.검사, Job.마법사, Job.사제, Job.음유시인 }, false),
        new Setup("E_힐러없음",  new[]{ Job.수호기사, Job.검사, Job.마법사, Job.검사, Job.음유시인 }),
    };
    int _qi = -1;

    /// <summary>편성 화면이 정한 파티를 W3의 Job 배열로 바꾼다. 편성이 없으면 null(기본 구성 유지).</summary>
    static Job[] PartySetup()
    {
        var names = PartyState.SortieJobs();
        if (names == null || names.Count == 0) return null;
        var jobs = new System.Collections.Generic.List<Job>();
        foreach (var n in names)
            if (System.Enum.TryParse(n, out Job j)) jobs.Add(j);
            else jobs.Add(Job.검사);   // 아직 W3에 없는 전직은 근접 딜로 대체 — 조용히 빼면 인원이 줄어든다
        return jobs.Count == 0 ? null : jobs.ToArray();
    }

    // ── 재현 가능한 측정 (2026-08-14) ─────────────────────────────
    // 여태 몹 스폰이 시드 없는 난수였고 구성당 1회만 돌았다. 그래서 §21-1h의 "B −5.0%"처럼
    // 작은 차이를 결론의 근거로 삼은 것이 **애초에 성립하지 않았다**(신뢰구간이 없다).
    // 실행마다 시드를 고정하고 구성당 여러 번 돌려 중앙값으로 판정한다.
    int _seed = 20260814;
    int _reps = 1;
    int _rep;                    // 현재 구성의 몇 번째 반복인가
    bool _shotThisRun;           // 이 판의 중반 스크린샷을 찍었는가
    Setup _setup;
    bool _tauntEnabled = true;
    GUIStyle _hud;

    // ── 게임 모드 ─────────────────────────────────────────
    // 이 스크립트는 원래 **대조 실험용**이라 구성 5종을 자동 순회하고 CSV를 쓴다.
    // 실제 게임 화면(Battle 씬)에서는 표준 5인 한 판만 돌고 끝나야 하므로 갈래를 나눈다.
    // 검증 경로를 건드리지 않으려고 플래그로만 분기한다 — 측정 코드를 게임용으로
    // 고쳐 쓰면 나중에 측정값이 왜 달라졌는지 아무도 모르게 된다.
    [Header("게임 모드 (Battle 씬에서 켠다)")]
    public bool GameMode;

    // 던전 임시 강화 배율(§7). **게임 모드에서만** 적용한다 —
    // 검증(W1~W3)은 강화 없는 기준선이어야 구성 비교가 성립한다.
    float _bAtk = 1f, _bHp = 1f, _bSpd = 1f, _bCd = 1f, _bHeal = 1f, _bShield = 1f, _bRange = 1f, _bAtkSpd = 1f;
    /// <summary>판이 끝났을 때 호출. true=생존(상한 도달) / false=전멸</summary>
    public System.Action<bool> OnBattleEnd;

    /// <summary>파티원이 죽는 **순간** 호출 — (슬롯 번호, 직업 이름). §4의 목숨 카운트를 밖에서 올린다.</summary>
    public System.Action<int, string> OnMemberDied;

    readonly System.Collections.Generic.List<string> _deadJobs = new System.Collections.Generic.List<string>();
    /// <summary>이번 판에서 죽은 직업 목록. 판이 끝난 뒤에도 조회할 수 있다.</summary>
    public System.Collections.Generic.IReadOnlyList<string> DeadJobs => _deadJobs;

    void Awake()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;

        var a = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] == "--out" && i + 1 < a.Length) _outPath = a[i + 1];
            if (a[i] == "--seconds" && i + 1 < a.Length)
                float.TryParse(a[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out 최대시간);
            if (a[i] == "--seed" && i + 1 < a.Length) int.TryParse(a[i + 1], out _seed);
            if (a[i] == "--reps" && i + 1 < a.Length && int.TryParse(a[i + 1], out int rp))
                _reps = Mathf.Clamp(rp, 1, 50);
        }
        _outPath ??= Path.Combine(Application.persistentDataPath, "w3_result.csv");
        // 새 열은 **끝에만** 붙인다 — 중간에 끼우면 이 CSV를 읽는 기존 도구가 조용히 어긋난다
        _csv.AppendLine("setup,survived_s,kills,taunts,shield,firestorm,ilseom,miracle,chant_sw,backline_hits,frontline_hits,shield_absorbed,healer_died_at,final_wave,kills_per_sec,verdict,faith_peak,seed,rep");

        BuildWorld();
        NextStyle();
    }

    void BuildWorld()
    {
        var bank = SpriteBank.Load();
        // 던전 노드면 던전 바닥을 깐다(§3-4 — 아트 신규 0장, 기존 텍스처 재사용)
        GroundBuilder.Build(bank, Arena + 20f,
            DungeonRun.Active ? "ground/dungeon_rock_albedo" : "ground/field_plain_albedo");

        // 스킬 범위 표시 두 장 — 매번 만들지 않고 켜고 끈다
        LoadFxOnce();
        _tauntRing = MakeRing(bank, new Color(1f, 0.72f, 0.25f, 0.9f));
        _stormRing = MakeRing(bank, new Color(1f, 0.42f, 0.2f, 0.95f));
        // 배경 프랍 — 전투 공간 **바깥 링**에만 깔린다(안쪽에 두면 유닛과 겹쳐 시야를 가린다).
        // 시드를 고정해 같은 판이면 같은 배치가 나오게 한다(측정 재현성).
        AshesToStars.FieldDecor.Build(bank, Arena, 20260813, AshesToStars.FieldDecor.Biome.Field);

        // 파티 슬롯 5칸을 미리 만들고, 구성(§21-1f)에 따라 켜고 끈다
        _slots = new Member[5];
        // ⚠️ 여기서 예전엔 슬롯 5칸에 { Player, Mob0, Mob1, Summon, Mob2 }를 꽂았다 —
        //    즉 파티원 다섯 중 넷이 **몹 스프라이트**로 그려지고 있었다(2026-08-13 발견).
        //    이제 역할에 맞는 오너 픽셀아트를 쓴다. 배정은 BuildParty에서 Role 기준으로 한다.
        for (int i = 0; i < 5; i++)
        {
            var go = new GameObject("slot" + i, typeof(SpriteRenderer));
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sharedMaterial = bank.Mat;
            sr.sortingOrder = 500;
            // 스케일 1.0 — 크기는 SpriteBank가 스프라이트마다 실측해 PPU로 맞춘다.
            // 손으로 곱하던 2.8배가 "캐릭터가 몹보다 6배 큰" 원인이었다.
            go.transform.localScale = Vector3.one;
            var m = new Member { Tr = go.transform, Sr = sr };
            MakeHpBar(go.transform, bank, out m.BarBg, out m.BarFg, 1.05f, 2.2f);

            MakeShadow(go.transform, bank, 0.95f);

            // 명령 지점 표시 — 캐릭터가 아니라 **월드에** 놓인다(따라다니면 안 된다)
            var mk = new GameObject("order_mark", typeof(SpriteRenderer));
            m.Marker = mk.GetComponent<SpriteRenderer>();
            m.Marker.sprite = bank.White; m.Marker.sharedMaterial = bank.Mat;
            m.Marker.color = new Color(1f, 0.85f, 0.35f, 0.55f);
            mk.SetActive(false);
            _slots[i] = m;
            go.SetActive(false);
        }

        _mTr = new Transform[MAXM]; _mSr = new SpriteRenderer[MAXM];
        _mPos = new Vector2[MAXM]; _mHp = new float[MAXM]; _mCd = new float[MAXM];
        _mAtkCd = new float[MAXM]; _mMaxHp = new float[MAXM];
        _mBarBg = new SpriteRenderer[MAXM]; _mBarFg = new SpriteRenderer[MAXM];
        _mKind = new int[MAXM]; _mOn = new bool[MAXM]; _mFlash = new float[MAXM];
        var mr = new GameObject("Mobs").transform;
        for (int i = 0; i < MAXM; i++)
        {
            var go = new GameObject("m", typeof(SpriteRenderer));
            go.transform.SetParent(mr, false);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sharedMaterial = bank.Mat;
            MakeShadow(go.transform, bank, 0.62f);
            MakeHpBar(go.transform, bank, out _mBarBg[i], out _mBarFg[i], 0.7f, 1.7f);
            // 몹 체력바는 **다친 놈만** 보여준다 — 200마리가 전부 달고 있으면
            // 화면이 바 천지가 되고 정작 위험한 대상이 눈에 안 들어온다.
            _mBarBg[i].gameObject.SetActive(false);
            _mBarFg[i].gameObject.SetActive(false);
            go.SetActive(false);
            _mTr[i] = go.transform; _mSr[i] = sr;
        }

        _pTr = new Transform[MAXP]; _pPos = new Vector2[MAXP]; _pVel = new Vector2[MAXP];
        _pLife = new float[MAXP]; _pOn = new bool[MAXP];
        var pr = new GameObject("Shots").transform;
        for (int i = 0; i < MAXP; i++)
        {
            var go = new GameObject("s", typeof(SpriteRenderer));
            go.transform.SetParent(pr, false);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = bank.Projectile;
            sr.sharedMaterial = bank.Mat;
            // 적 탄 = 붉은 자주. 아군 탄(노랑)과 **색으로** 갈라야 수백 발이 날아다닐 때 읽힌다.
            // 모양으로 구분하려 하면 픽셀 크기에서 이미 실패한다.
            sr.color = new Color(1f, 0.42f, 0.55f);
            sr.sortingOrder = 400;
            go.transform.localScale = Vector3.one * 1.0f;
            go.SetActive(false);
            _pTr[i] = go.transform;
        }

        _aTr = new Transform[MAXP]; _aPos = new Vector2[MAXP]; _aVel = new Vector2[MAXP];
        _aLife = new float[MAXP]; _aOn = new bool[MAXP];
        var ar = new GameObject("AllyShots").transform;
        for (int i = 0; i < MAXP; i++)
        {
            var go = new GameObject("a", typeof(SpriteRenderer));
            go.transform.SetParent(ar, false);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = bank.Projectile;
            sr.sharedMaterial = bank.Mat;
            sr.color = new Color(1f, 0.95f, 0.5f);      // 아군 탄 = 노랑
            sr.sortingOrder = 450;
            go.transform.localScale = Vector3.one * 0.8f;
            go.SetActive(false);
            _aTr[i] = go.transform;
        }
    }

    static Role RoleOf(Job j) => j switch
    {
        Job.수호기사 => Role.Tank,
        Job.사제 => Role.Healer,
        Job.음유시인 => Role.Buffer,
        _ => Role.Dps,
    };

    /// <summary>
    /// 근접 직업인가(§3). 근접은 투사체를 쏘지 않는다 —
    /// 예전엔 전 직업이 FireAlly로 탄을 날려 탱커·검사가 원거리처럼 보였다.
    /// </summary>
    static bool IsMelee(Job j) => j == Job.수호기사 || j == Job.검사;

    /// <summary>공격 연출. 근접은 타격만, 원거리는 탄을 날린다.</summary>
    void AttackFx(Member m, Vector2 targetPos)
    {
        m.AttackT = 0.26f;                       // 공격 애니메이션 재생 구간
        if (!IsMelee(m.Job)) FireAlly(m.Pos, targetPos);
    }

    /// <summary>
    /// 쿼터뷰 깊이 정렬 — **앞(y가 작은) 유닛이 뒤 유닛을 가려야** 입체로 보인다.
    /// y를 ISO_Y로 눌러도 정렬이 없으면 전부 한 평면에 붙어 탑뷰처럼 읽힌다
    /// (오너 지적 "탑뷰 아니고 쿼터뷰라고"). 파티와 몹이 **같은 공식**을 써야
    /// 서로 올바른 순서로 겹친다.
    /// </summary>
    static int Depth(float worldY) => 1000 - Mathf.RoundToInt(worldY * 10f);

    /// <summary>지정한 지점 주변 반경 안에서 가장 가까운 몹. 파티가 탱 기준으로 목표를 모으는 데 쓴다.</summary>
    int NearestMobTo(Vector2 center, float radius)
    {
        int best = -1; float bd = radius * radius;
        for (int i = 0; i < MAXM; i++)
        {
            if (!_mOn[i]) continue;
            float d = (_mPos[i] - center).sqrMagnitude;
            if (d < bd) { bd = d; best = i; }
        }
        return best;
    }

    /// <summary>가장 몹이 밀집한 지점의 몹 인덱스. 마법사가 화염폭풍 자리를 고르는 근거(§10-2).</summary>
    int DensestMob(Vector2 from)
    {
        int best = -1; float bestScore = -1f;
        for (int i = 0; i < MAXM; i++)
        {
            if (!_mOn[i]) continue;
            float d = (_mPos[i] - from).magnitude;
            if (d > 18f) continue;
            float score = CountMobsNear(_mPos[i], 3.2f) - d * 0.15f;
            if (score > bestScore) { bestScore = score; best = i; }
        }
        return best;
    }

    /// <summary>
    /// 실제로 채워진 HP만큼을 돌려준다 — 신앙(§3 "회복량 누적")의 계량 단위.
    /// 이미 만피인 대상에 힐을 넣어도 신앙이 쌓이면 축적이 부풀려진다.
    /// </summary>
    /// <summary>
    /// 회복. 실제로 채워진 양만 돌려준다(과회복은 세지 않는다).
    ///
    /// ⚠️ 보스전 **힐 체크 기믹의 유일한 입력**이 이 함수다. 지금까지 아무도 보고하지 않아
    /// `actualPartyHealing`이 언제나 0이었고, 그래서 힐 체크는 **무조건 실패 → 즉시 전멸**이었다.
    /// 즉 던전·탑 보스전은 15초에 자동 패배하는 판이었다(실측: "Heal Check FAILED! 0 / 5000").
    /// </summary>
    static float Heal(Member o, float amount)
    {
        float before = o.Hp;
        o.Hp = Mathf.Min(o.MaxHp, o.Hp + amount);
        float healed = o.Hp - before;
        AshesToStars.BossBattle.ReportHealingToActive(healed);
        return healed;
    }

    /// <summary>
    /// 파티 역할 → 오너 픽셀아트 직업.
    /// Role과 SpriteBank.Job은 지금 우연히 순서가 같지만 캐스팅으로 엮지 않는다 —
    /// 한쪽에 값이 끼면 조용히 어긋나는 종류의 결합이다.
    /// </summary>
    static SpriteBank.Job ArtOf(Role r) => r switch
    {
        Role.Tank => SpriteBank.Job.Tank,
        Role.Healer => SpriteBank.Job.Healer,
        Role.Buffer => SpriteBank.Job.Buffer,
        _ => SpriteBank.Job.Dps,
    };

    /// <summary>
    /// 이번 판의 시드. 구성·반복마다 달라야 하지만 **재실행하면 같은 값**이어야 한다 —
    /// 그래야 "같은 조건에서 구성만 다르다"는 대조 실험이 성립한다.
    /// </summary>
    int RunSeed() => _seed + _qi / Mathf.Max(1, _reps) * 1000 + _rep;

    void NextStyle()
    {
        _qi++;
        // 게임 모드는 표준 5인(A) 한 판만 — 두 번째 구성으로 넘어가지 않는다
        if (GameMode && _qi > 0) return;
        if (_qi >= SETUPS.Length * _reps) { Finish(); return; }
        _rep = _qi % _reps;
        _shotThisRun = false;
        _setup = SETUPS[_qi / _reps];
        // **판을 세우기 전에** 시드를 박는다. 스폰 위치·AI 배정이 여기서부터 뽑힌다.
        Random.InitState(RunSeed());
        _tauntEnabled = _setup.TauntEnabled;
        _style = Style.Balanced;                     // 스타일 고정 — 구성만 비교한다

        // 아레나 템플릿을 실제 장애물로 세운다(§3-4). 검증(W1~W3)은 항상 빈 판이어야
        // 구성 비교가 성립하므로 **게임 모드 + 던전 노드**일 때만 깐다.
        ArenaLayout.Clear();
        // ⚠️ GameMode를 조건에 넣지 마라 — BattleScreen이 AddComponent **뒤에** 대입하므로
        //    Awake(→NextStyle)가 도는 시점에는 아직 false다. 실제로 이것 때문에
        //    장애물과 강화가 통째로 적용되지 않았다(스크린샷으로 발견).
        //    DungeonRun.Active는 검증 빌드에서 절대 참이 될 수 없으므로 이것만으로 충분하다.
        if (DungeonRun.Active && DungeonRun.PendingNode >= 0)
        {
            var node = DungeonRun.Plan.Nodes[DungeonRun.PendingNode];
            ArenaLayout.Build(node.Template, node.TerrainSeed, Arena, SpriteBank.Cached.Mat);
        }

        // 던전 강화를 배율로 환산한다. 던전 밖(필드 사냥·검증)에서는 전부 1이다.
        if (DungeonRun.Active)
            Boons.Multipliers(DungeonRun.State.Boons, out _bAtk, out _bHp, out _bSpd,
                              out _bCd, out _bHeal, out _bShield, out _bRange, out _bAtkSpd);

        // 구성에 맞춰 파티를 짠다
        for (int i = 0; i < 5; i++) _slots[i].Tr.gameObject.SetActive(false);
        _party = new Member[_setup.Jobs.Length];
        for (int i = 0; i < _setup.Jobs.Length; i++)
        {
            var m = _slots[i];
            var job = _setup.Jobs[i];
            m.Job = job;
            m.Role = RoleOf(job);
            m.Sr.sprite = SpriteBank.Cached.Char(ArtOf(m.Role));
            m.MaxHp = (m.Role == Role.Tank ? 320f : m.Role == Role.Dps ? 130f : 150f) * _bHp;
            m.Atk = (m.Role == Role.Dps ? 26f : m.Role == Role.Tank ? 10f : m.Role == Role.Buffer ? 8f : 6f) * _bAtk;
            // 사거리는 **역할이 아니라 직업**으로 정한다(§3).
            // Role.Dps로 묶으면 검사(근접)와 마법사(원거리)가 같은 사거리를 갖게 되어
            // 검사가 멀찍이 서서 때리는 그림이 된다 — 오너 지적으로 발견.
            m.Range = job switch
            {
                Job.수호기사 => 1.5f,   // 근접 탱 — 몹에 붙어야 도발·방패가 의미를 갖는다
                Job.검사 => 1.9f,       // 근접 딜
                Job.마법사 => 5.5f,
                Job.사제 => 6.5f,
                _ => 6.0f,              // 음유시인
            };
            m.Range *= _bRange;
            _party[i] = m;
        }

        var cam = Camera.main ?? Object.FindFirstObjectByType<Camera>();
        if (cam != null)
        {
            var f = cam.GetComponent<CameraFollow>() ?? cam.gameObject.AddComponent<CameraFollow>();
            f.Target = _party[0].Tr;
            f.LookAhead = 0.8f;
        }

        for (int i = 0; i < _party.Length; i++)
        {
            var m = _party[i];
            m.Style = _style;
            m.Hp = m.MaxHp;
            m.Threat = 0f; m.DeadT = 0f;
            m.Shield = 0f; m.Gauge = 0f; m.SkillCd = 0f; m.Chant = Chant.진군가;
            // 진형 초기 배치: 탱 앞, 딜 중간, 힐 뒤 (§10-4가 이 진형을 유지시키는지 본다)
            // 진형: 탱 최전방 → 딜 중간 → 힐·버퍼 후열 (§10-4가 이 진형을 유지시키는지 관찰)
            m.Pos = new Vector2(i == 2 ? 1.2f : i == 4 ? -1.2f : 0f,
                                m.Role == Role.Tank ? 1.8f : m.Role == Role.Dps ? -0.4f : -2.6f);
            m.Tr.gameObject.SetActive(true);
        }
        for (int i = 0; i < MAXM; i++) { _mOn[i] = false; _mTr[i].gameObject.SetActive(false); }
        for (int i = 0; i < MAXP; i++) { _pOn[i] = false; _pTr[i].gameObject.SetActive(false); }
        if (_aOn != null) for (int i = 0; i < MAXP; i++) { _aOn[i] = false; _aTr[i].gameObject.SetActive(false); }
        _mAlive = 0;
        for (int i = 0; i < 시작웨이브; i++) SpawnMob();

        _t = 0f; _kills = 0; _tauntUses = 0; _backlineHits = 0; _frontlineHits = 0;
        _healsCast = 0; _healerDeadT = -1f; _shieldAbsorbed = 0f; _faithPeak = 0f;
        _deadJobs.Clear();          // 판마다 새로 센다 — 안 비우면 구성 순회 때 누적된다
        _meleeHits = 0; _shotHits = 0; _framesThisRun = 0;
        _tauntUntil = -1f; _lastStandUntil = -1f; _lastStandCd = -1f; _partyChant = Chant.진군가;
        for (int k = 0; k < _skillLog.Length; k++) _skillLog[k] = 0;
        Debug.Log($"[W3] 구성 {_setup.Name} 시작 ({_party.Length}인, 도발 {(_tauntEnabled ? "ON" : "OFF")})");
    }

    void SpawnMob()
    {
        for (int i = 0; i < MAXM; i++)
        {
            if (_mOn[i]) continue;
            float a = Random.value * Mathf.PI * 2f;
            _mPos[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(Arena * 0.6f, Arena);
            int r = Random.Range(0, 100);
            _mKind[i] = r < 35 ? 0 : r < 60 ? 1 : r < 85 ? 2 : r < 94 ? 3 : 4;   // 정예 15%
            _mHp[i] = _mMaxHp[i] = _mKind[i] >= 3 ? 90f : 26f;
            _mCd[i] = Random.value * 2f;
            _mAtkCd[i] = Random.value * 0.8f; _mFlash[i] = 0f;
            _mOn[i] = true; _mAlive++;
            _mSr[i].sprite = SpriteBank.Cached.Mob(_mKind[i] == 2 ? 2 : _mKind[i] == 1 ? 1 : 0);
            _mSr[i].color = _mKind[i] == 3 ? new Color(0.4f, 1f, 0.5f)      // 치유 정예 = 초록
                          : _mKind[i] == 4 ? new Color(0.8f, 0.5f, 1f)      // 소환 정예 = 보라
                          : Color.white;
            // 정예만 조금 크게. 기본 크기는 SpriteBank가 실측 PPU로 맞춰 둔다.
            _mTr[i].localScale = Vector3.one * (_mKind[i] >= 3 ? 1.4f : 1.0f);
            _mBarBg[i].gameObject.SetActive(false);      // 다시 스폰됐으니 만피 — 바 숨김
            _mBarFg[i].gameObject.SetActive(false);
            _mTr[i].gameObject.SetActive(true);
            return;
        }
    }

    /// <summary>
    /// 측정용 고정 스텝(초). 시뮬레이션을 **프레임 시간과 분리**한다.
    ///
    /// 시드를 고정하고도 같은 시드 두 판이 어긋났다(실측: E 구성 처치 86 vs 64).
    /// 원인은 난수가 아니라 `Time.deltaTime`이었다 — 프레임마다 dt가 달라
    /// 이동·쿨다운·피격 판정이 매번 다른 지점에서 끊긴다.
    /// **시드만으로는 재현되지 않는다**; 스텝을 고정해야 비로소 대조 실험이 성립한다.
    /// </summary>
    const float FixedStep = 1f / 60f;
    float _stepAcc;

    /// <summary>
    /// 히트스톱 — 큰 타격 순간 시뮬레이션을 몇 프레임 멈춘다.
    ///
    /// `Time.timeScale`을 쓰지 않는다: 이 스크립트는 측정 하네스이기도 해서
    /// 전역 시간을 건드리면 CSV의 생존 시간이 오염된다. 고정 스텝을 건너뛰는 방식이라
    /// **게임 모드에서만** 적용되고 검증 실행에는 영향이 없다.
    /// </summary>
    int _hitstop;
    float _screenFlash;
    Color _screenFlashColor = Color.white;
    public void Hitstop(int frames = 3) { if (GameMode) _hitstop = Mathf.Max(_hitstop, frames); }

    CameraFollow _cam;
    void Shake(float amp)
    {
        if (!GameMode) return;                     // 측정 실행의 스크린샷 검증을 흔들지 않는다
        _cam ??= Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
        if (_cam != null) _cam.Shake(amp);
    }

    void Update()
    {
        if (_hitstop > 0) { _hitstop--; _stepAcc = 0f; return; }

        // 프레임이 얼마나 걸렸든 1/60 단위로만 진행한다(한 프레임 최대 10스텝 — 스파이럴 방지)
        _stepAcc += Mathf.Min(Time.deltaTime, 0.25f);
        int steps = 0;
        while (_stepAcc >= FixedStep && steps < 10) { _stepAcc -= FixedStep; steps++; Step(); }
    }

    void Step()
    {
        float dt = FixedStep;
        _t += dt; _framesThisRun++;
        // 전멸이 판정 기준이다. 시간 상한은 무한 루프를 막는 안전장치일 뿐 —
        // 45초 고정 상한을 쓰던 1차 실험은 다섯 구성 중 넷이 완주해버려
        // "얼마나 버티는가"를 물을 수 없었다(§21-1g).
        if (AllDead() || _t >= 최대시간)
        { RecordAndNext(); return; }

        // 판 중반 스크린샷 — **판이 끝난 뒤에 찍으면 안 된다.**
        // CaptureScreenshot은 그 프레임 끝에 찍히는데, 바로 다음 줄에서 다음 구성으로 넘어가므로
        // 파일 이름은 A인데 화면은 "구성 B 경과 0s"인 그림이 저장됐다(실측). 전투 장면이 한 장도 없었다.
        if (!_shotThisRun && _t >= 20f)
        {
            _shotThisRun = true;
            var dir = Path.GetDirectoryName(_outPath);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"w3_{_setup.Name}.png"));
        }

        TickCommand();
        TickParty(dt);
        TickMobs(dt);

        // 겹침 해소 — 유닛이 서로 파고들면 실루엣이 먹혀 무엇이 몇 마리인지 안 읽힌다.
        // 이동을 계산한 **뒤에** 한 번만 민다(이동 중에 밀면 추적이 흔들린다).
        ResolveOverlap();
        TickShots(dt);
        TickAllyShots(dt);
        TickVisuals(dt);

        // 웨이브 압력 점증 — 시간이 갈수록 동시 몹 수 목표가 올라간다.
        // 이래야 모든 구성이 결국 무너지고 "언제 무너지는가"로 비교할 수 있다.
        int 목표 = 시작웨이브 + (int)(_t / 점증간격) * 단계당증가;
        목표 = Mathf.Min(목표, MAXM - 20);
        if (_mAlive < 목표) for (int i = 0; i < 2 && _mAlive < 목표; i++) SpawnMob();
    }

    /// <summary>
    /// 연출 갱신 — 애니메이션 프레임, 좌우 반전, 체력바.
    /// 전투 로직과 분리해 둔다: 여기서 하는 일은 화면 표현뿐이라
    /// 이 함수를 통째로 빼도 측정 수치는 한 자리도 달라지지 않아야 한다.
    /// </summary>
    void TickVisuals(float dt)
    {
        var bank = SpriteBank.Cached;

        // 도발 지속 구간 — 몹이 탱에게 끌리는 동안 탱 주변에 범위를 띄운다.
        // 이게 없으면 "도발했다"가 수치로만 존재하고 화면에서는 아무 일도 안 일어난 것처럼 보인다.
        var tk = _party.Length > 0 ? _party[0] : null;
        bool taunting = _t < _tauntUntil && tk != null && tk.Alive;
        if (_tauntRing.gameObject.activeSelf != taunting) _tauntRing.gameObject.SetActive(taunting);
        if (taunting)
        {
            float elapsed = 3f - (_tauntUntil - _t);
            float k = Mathf.InverseLerp(_tauntUntil, _tauntUntil - 3f, _t);   // 남을수록 크게
            var fx = FxFrame(_fxTaunt, elapsed, 3f);
            if (fx != null) { _tauntRing.sprite = fx; _tauntRing.color = Color.white; }
            else { var c = _tauntRing.color; c.a = 0.25f + k * 0.5f; _tauntRing.color = c; }
            PlaceRing(_tauntRing, tk.Pos, 4.5f * (0.55f + k * 0.45f));
        }

        bool storming = _t < _stormUntil;
        if (_stormRing.gameObject.activeSelf != storming) _stormRing.gameObject.SetActive(storming);
        if (storming)
        {
            var fx = FxFrame(_fxStorm, 0.45f - (_stormUntil - _t), 0.45f);
            if (fx != null) { _stormRing.sprite = fx; _stormRing.color = Color.white; }
            PlaceRing(_stormRing, _stormAt, _stormR);
        }

        foreach (var m in _party)
        {
            if (m.AttackT > 0f) m.AttackT -= dt;
            if (m.HurtT > 0f) m.HurtT -= dt;
            if (m.SkillT > 0f) m.SkillT -= dt;

            // 동작 우선순위: 사망 > 피격 > **스킬** > 공격 > 이동 > 대기.
            // 스킬을 공격보다 위에 둔다 — 스킬을 쓰는 순간에도 평타 판정이 같이 돌기 때문에
            // 아래에 두면 큰 동작이 평타 프레임에 묻혀 화면에서 구분되지 않는다.
            var want = !m.Alive ? SpriteBank.Motion.Death
                     : m.HurtT > 0f ? SpriteBank.Motion.Hurt
                     : m.SkillT > 0f ? SpriteBank.Motion.Special
                     : m.AttackT > 0f ? SpriteBank.Motion.Attack
                     : (m.Pos - m.PrevPos).sqrMagnitude > 1e-5f ? SpriteBank.Motion.Walk
                     : SpriteBank.Motion.Idle;

            if (want != m.Mo) { m.Mo = want; m.AnimT = 0f; }
            else m.AnimT += dt;

            m.Sr.sprite = bank.CharAnim(ArtOf(m.Role), m.Mo, m.AnimT);
            m.Sr.sortingOrder = Depth(m.Pos.y);

            // 선택된 캐릭터를 눈에 띄게 — 누구에게 명령하는지 보이지 않으면 지휘가 성립하지 않는다(§5)
            bool picked = _sel >= 0 && _sel < _party.Length && _party[_sel] == m;
            if (picked) m.Sr.color = new Color(1f, 0.96f, 0.72f);
            if (m.FlashT > 0f)
            {
                m.FlashT -= dt;
                m.Sr.color = Color.Lerp(m.Sr.color, Color.white, 0.85f);
            }

            // 이동 명령 지점 표시 — 명령이 들어갔는지 보이지 않으면 눌렀는지조차 알 수 없다
            if (m.Marker != null)
            {
                bool show = m.Order.HasValue;
                if (m.Marker.gameObject.activeSelf != show) m.Marker.gameObject.SetActive(show);
                if (show)
                {
                    m.Marker.transform.position = ToScreen(m.Order.Value, -2f);
                    m.Marker.sortingOrder = Depth(m.Order.Value.y) - 1;
                    float pulse = 0.7f + Mathf.PingPong(_t * 1.6f, 0.35f);
                    m.Marker.transform.localScale = new Vector3(pulse, pulse * ISO_Y, 1f);
                }
            }
            if (Mathf.Abs(m.Pos.x - m.PrevPos.x) > 1e-4f) m.Sr.flipX = m.Pos.x < m.PrevPos.x;
            m.PrevPos = m.Pos;

            bool showBar = m.Alive;
            if (m.BarBg.gameObject.activeSelf != showBar)
            {
                m.BarBg.gameObject.SetActive(showBar);
                m.BarFg.gameObject.SetActive(showBar);
            }
            if (showBar) SetBar(m.BarFg, m.Hp / m.MaxHp, 0.99f);
        }

        for (int i = 0; i < MAXM; i++)
        {
            if (!_mOn[i]) continue;

            // 몹 애니메이션. 개체마다 시간을 어긋나게 줘야 100마리가 같은 프레임으로
            // 군무를 추지 않는다 — 물량이 많을수록 동기화가 눈에 띈다(§10-2).
            var mm = _mFlash[i] > 0f ? SpriteBank.Motion.Hurt : SpriteBank.Motion.Walk;
            _mSr[i].sprite = bank.MobAnim(0, mm, _t + i * 0.37f);
            _mSr[i].sortingOrder = Depth(_mPos[i].y);

            float ratio = _mMaxHp[i] > 0f ? _mHp[i] / _mMaxHp[i] : 1f;
            bool hurt = ratio < 0.999f;
            if (_mBarBg[i].gameObject.activeSelf != hurt)
            {
                _mBarBg[i].gameObject.SetActive(hurt);
                _mBarFg[i].gameObject.SetActive(hurt);
            }
            if (hurt) SetBar(_mBarFg[i], ratio, 0.66f);
        }
    }

    /// <summary>
    /// 수동 지휘 입력 (§5) — 캐릭터를 골라 위치를 지시한다.
    ///   숫자키 1~5 / 캐릭터 클릭 → 선택
    ///   우클릭(또는 선택 상태에서 좌클릭 빈 땅) → 그 자리로 이동 명령
    ///   ESC·0 → 선택 해제(다시 자동)
    /// 기획서가 "2개 명령(위치·스킬)으로 5인을 다루는 방식은 전례가 드물다"고 한
    /// V3 검증 대상이다. 여기서 굴러가야 보스전 지휘가 성립한다.
    /// </summary>
    void TickCommand()
    {
        if (_party == null) return;

        for (int i = 0; i < _party.Length && i < 5; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i) && _party[i].Alive) _sel = i;
        if (Input.GetKeyDown(KeyCode.Alpha0)) _sel = -1;

        // 스페이스 = 1번 스킬, Q = 2번 스킬. 마우스를 안 떼고 쓸 수 있어야 지휘가 빠르다(§5)
        if (_sel >= 0 && _party[_sel].Alive)
        {
            if (Input.GetKeyDown(KeyCode.Space)) _party[_sel].ForceSkill = 1;
            if (Input.GetKeyDown(KeyCode.Q)) _party[_sel].ForceSkill = 2;
        }

        var cam = Camera.main;
        if (cam == null) return;

        // 화면 → 월드. ToScreen이 y를 ISO_Y로 눌렀으므로 되돌린다.
        Vector3 w3 = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 world = new Vector2(w3.x, w3.y / ISO_Y);

        if (Input.GetMouseButtonDown(0))
        {
            int hit = -1; float best = 1.6f;
            for (int i = 0; i < _party.Length; i++)
            {
                if (!_party[i].Alive) continue;
                float d = (_party[i].Pos - world).magnitude;
                if (d < best) { best = d; hit = i; }
            }
            if (hit >= 0) _sel = hit;
            else if (_sel >= 0) _party[_sel].Order = world;   // 선택 중이면 빈 땅 클릭 = 이동
        }
        if (Input.GetMouseButtonDown(1) && _sel >= 0 && _party[_sel].Alive)
            _party[_sel].Order = world;
    }

    // ── 파티 자동 전투 ────────────────────────────────────
    void TickParty(float dt)
    {
        var sp = Spec(_style);
        var tank = _party[0];

        foreach (var m in _party)
        {
            if (!m.Alive) continue;
            m.Cd -= dt; m.SkillCd -= dt;
            m.Threat = Mathf.Max(0f, m.Threat - dt * 2f);

            int target = NearestMob(m.Pos, m.Range * 1.6f);
            Vector2 want = Vector2.zero;

            // ── 수동 지휘가 최우선 (§5 "잡몹은 자동, 보스는 수동 지휘") ──
            // 명령이 있으면 AI 판단을 덮는다. 목적지에 닿으면 스스로 해제해 AI로 돌아간다.
            bool commanded = false;
            if (m.Order.HasValue)
            {
                Vector2 d = m.Order.Value - m.Pos;
                if (d.magnitude < 0.5f) m.Order = null;
                else { want = d.normalized; commanded = true; }
            }

            if (m.Role == Role.Tank)
            {
                // ⚠️ 이동과 스킬을 **분리**한다. 예전엔 이 둘이 한 분기에 묶여 있어서
                //    이동 명령을 내리는 순간 탱커가 도발·방패를 못 쓰게 됐다.
                //    수동 지휘는 "어디로 갈지"를 지시하는 것이지 전투를 멈추는 게 아니다(§5).
                if (!commanded)
                {
                    int t2 = NearestMob(m.Pos, 99f);
                    if (t2 >= 0) want = (_mPos[t2] - m.Pos).normalized;
                }
                // 수호 게이지는 §3대로 **피격·아군 보호**로 찬다(Damage에서 더한다).
                // 예전에는 시간으로 찼다(dt*14) — 그러면 탱이 가만히 서 있어도 방패가 나가고,
                // "맞아주는 것이 곧 자원"이라는 탱의 정체성이 성립하지 않는다.
                // 아주 적은 자연 회복만 남긴다 — 도발이 실패해 한 대도 안 맞는 판에서 굳지 않게.
                m.Gauge = Mathf.Min(100f, m.Gauge + dt * 2f);

                // 강제 발동(버튼)이면 조건을 건너뛴다 — 지휘는 "지금" 쓰는 것이다
                bool force1 = m.ForceSkill == 1, force2 = m.ForceSkill == 2;
                if (_tauntEnabled && (force1 || (m.SkillCd <= 0f && CountMobsNear(m.Pos, 4.5f) >= 3)))
                {
                    if (force1) m.ForceSkill = 0;
                    // ① 도발의 함성 — 광역 어그로. 원거리 몹까지 끌어야 후열이 산다(§10-4 대응)
                    m.SkillCd = 6f * _bCd; m.Threat += 80f; _tauntUses++;
                    _tauntUntil = _t + 3.0f;                      // 3초간 원거리도 탱을 노린다
                    _skillLog[0]++; m.SkillT = 0.55f;
                    FlashParty();                                 // 발동 순간을 눈에 띄게
                    FxParticles.Play(FxKind.도발, ToScreen(m.Pos), 1.2f);
                }
                if (force2 || m.Gauge >= 60f)
                {
                    // ② 성채 방패 — 게이지를 소모해 파티 전체에 보호막
                    if (force2) m.ForceSkill = 0;
                    m.Gauge = 0f;
                    foreach (var o in _party)
                        if (o.Alive) o.Shield = Mathf.Max(o.Shield, 40f * _bShield);
                    FlashParty();
                    _skillLog[1]++; m.SkillT = 0.55f;
                    foreach (var o in _party) if (o.Alive) FxParticles.Play(FxKind.무적, ToScreen(o.Pos));
                }
            }
            else if (commanded)
            {
                // 이동만 명령이 덮는다. 아래 공격·스킬 블록은 그대로 돈다.
            }
            else
            {
                // ── 직업별 이동 (§3) ──
                // ⚠️ 예전엔 탱을 뺀 전원이 **똑같은 한 점**(탱 뒤)을 목표로 삼았다.
                //    그래서 넷이 한 덩어리로 겹쳐 다녔고, §10-4가 말하는 전열/후열 구분이
                //    화면에서 사라졌다(오너 지적 "다 똑같이 움직이자너").
                //    직업마다 **무엇을 보고 어디로 가는가**가 달라야 역할이 눈에 읽힌다.
                int slot = System.Array.IndexOf(_party, m);
                float lane = (slot - (_party.Length - 1) * 0.5f) * 1.7f;
                Vector2 anchor = tank.Alive ? tank.Pos : Vector2.zero;
                Vector2 goal;

                switch (m.Job)
                {
                    case Job.검사:
                    {
                        // 근접 딜 — **탱이 모아둔 무리**를 친다. 혼자 먼 몹을 쫓아가면
                        // 탱의 보호를 벗어나 §10-4의 어그로 구조가 성립하지 않는다
                        // (오너 지적 "탱커가 도발 써서 몹 모으고 … 이런식으로 가야지 다 따로노냐").
                        int t = tank.Alive ? NearestMobTo(tank.Pos, 7f) : NearestMob(m.Pos, 99f);
                        if (t < 0) t = NearestMob(m.Pos, 99f);
                        goal = t >= 0 ? _mPos[t] : anchor;
                        if (t >= 0 && (_mPos[t] - m.Pos).magnitude < m.Range * 0.9f) goal = m.Pos;
                        break;
                    }
                    case Job.마법사:
                    {
                        // 원거리 딜 — 밀집한 무리를 사거리 끝에서 노린다. 너무 붙으면 물러선다.
                        // 기준점을 탱으로 잡아 **탱이 모아둔 무리**가 우선 후보가 되게 한다.
                        int t = DensestMob(tank.Alive ? tank.Pos : m.Pos);
                        if (t < 0) { goal = anchor + Vector2.right * lane; break; }
                        Vector2 away = (m.Pos - _mPos[t]);
                        float dist = away.magnitude;
                        away = dist > 0.01f ? away / dist : Vector2.up;
                        goal = _mPos[t] + away * (m.Range * 0.85f) + Vector2.right * (lane * 0.5f);
                        break;
                    }
                    case Job.사제:
                    {
                        // 힐 — 돌봐야 할 대상 곁으로. **탱을 우선**한다:
                        // 설계상 피해를 받는 것이 탱이고(§10-4), 탱이 무너지면 후열이 그대로 노출된다.
                        // 다만 다른 아군이 탱보다 확연히 위험하면 그쪽으로 간다.
                        Member care = tank.Alive ? tank : null;
                        foreach (var o in _party)
                        {
                            if (!o.Alive) continue;
                            float r = o.Hp / o.MaxHp;
                            if (care == null || r < care.Hp / care.MaxHp - 0.2f) care = o;
                        }
                        Vector2 cp = care != null ? care.Pos : anchor;
                        // 대상보다 몹 반대편에 선다 — 힐 사거리는 닿되 최전선에는 안 서게
                        goal = cp - Vector2.up * (sp.KeepDist * 0.9f) + Vector2.right * (lane * 0.6f);
                        break;
                    }
                    default:
                    {
                        // 음유시인 — 파티 무게중심 뒤. 오라가 전원에게 닿는 자리를 지킨다(§3)
                        Vector2 c = Vector2.zero; int n = 0;
                        foreach (var o in _party) if (o.Alive) { c += o.Pos; n++; }
                        if (n > 0) c /= n;
                        goal = c - Vector2.up * (sp.KeepDist * 1.15f) + Vector2.right * lane;
                        break;
                    }
                }

                want = goal - m.Pos;
                want = want.magnitude < 0.35f ? Vector2.zero : want.normalized;

                // 저체력이면 무엇을 하던 중이든 물러선다 (스타일별 임계)
                if (m.Hp / m.MaxHp < sp.RetreatHp)
                {
                    int near = NearestMob(m.Pos, 6f);
                    if (near >= 0) want = (m.Pos - _mPos[near]).normalized;
                }
            }

            // 서로 밀어내기 — 목표가 같아도 몸이 겹치지 않게.
            // 진형 오프셋만으로는 이동 중에 여전히 뭉친다(오너 지적 "너무 뭉쳐서 움직임").
            Vector2 sep = Vector2.zero;
            foreach (var o in _party)
            {
                if (o == m || !o.Alive) continue;
                Vector2 d = m.Pos - o.Pos;
                float sq = d.sqrMagnitude;
                if (sq > 1e-4f && sq < 2.25f)          // 1.5유닛 안쪽이면 민다
                    sep += d / Mathf.Sqrt(sq) * (1.5f - Mathf.Sqrt(sq));
            }
            if (sep.sqrMagnitude > 1e-4f)
                want = (want + sep * 1.4f).normalized;

            m.Pos += want * PlayerSpeed * 0.85f * _bSpd * dt;   // 강화는 아군에게만 — 몹 속도에 걸지 마라
            if (ArenaLayout.Any) m.Pos = ArenaLayout.Resolve(m.Pos);
            m.Pos = Vector2.ClampMagnitude(m.Pos, Arena + 3f);
            m.Tr.position = ToScreen(m.Pos, -1f);
            m.Sr.color = m.Hp / m.MaxHp < 0.3f ? new Color(1f, 0.55f, 0.55f) : Color.white;

            // 공격 / 치유
            if (m.Cd > 0f) continue;
            if (m.Job == Job.음유시인)
            {
                // 악장 전환 — 위급하면 수호가, 아니면 진군가 (§3 고유 메커니즘)
                float worstRatio = 1f;
                foreach (var o in _party) if (o.Alive) worstRatio = Mathf.Min(worstRatio, o.Hp / o.MaxHp);
                // 버튼으로 악장을 직접 고를 수 있다 — §3이 "전환이 곧 운영"이라 한 부분이라
                // 자동 판단만 두면 버퍼의 조작 여지가 사라진다.
                var wantChant = m.ForceSkill == 1 ? Chant.진군가
                              : m.ForceSkill == 2 ? Chant.수호가
                              : (worstRatio < 0.45f ? Chant.수호가 : Chant.진군가);
                if (m.ForceSkill != 0) m.ForceSkill = 0;
                if (wantChant != m.Chant) { m.Chant = wantChant; _skillLog[4]++; m.SkillT = 0.5f; }
                _partyChant = m.Chant;                      // 파티 전체에 적용되는 오라
                m.Cd = 0.8f; m.Threat += 3f;
            }
            else if (m.Job == Job.사제)
            {
                int wounded = 0;
                Member worst = null;
                float worstRatio2 = 1f;
                foreach (var o in _party)
                {
                    if (!o.Alive) continue;
                    float r = o.Hp / o.MaxHp;
                    if (r < 0.7f) wounded++;
                    if (r < worstRatio2) worstRatio2 = r;
                    if (worst == null || r < worst.Hp / worst.MaxHp) worst = o;
                }

                // 📌 기적이 45초판·114초 전멸판 통틀어 **0회**였던 원인 (2026-08-13 규명)
                //   ① 기적은 wounded>=3, 치유의 파동은 wounded>=2에서 발동한다.
                //      파동이 부상자를 2명 이하로 계속 눌러 **3명이 되는 창이 열리지 않았다.**
                //      파동으로도 못 막을 만큼 무너지면 그땐 이미 전멸 직전이라 사제도 같이 죽는다.
                //      → 위급도(최저 HP 비율)를 **별도 축**으로 넣어 파동이 못 막는 상황을 잡는다.
                //   ② 신앙을 행동 횟수(+6/회)로 쌓고 있었다. 기획서 §3은 "**회복량 누적**"이다.
                //      → 실제 회복시킨 양을 그대로 신앙으로 환산한다(코드가 기획서를 따르게).
                // 기적: 버튼(슬롯2)이면 신앙만 있으면 바로 쓴다
                if ((m.ForceSkill == 2 && m.Gauge >= 40f) || (m.Gauge >= 100f && (wounded >= 3 || worstRatio2 < 0.35f)))
                {
                    if (m.ForceSkill == 2) m.ForceSkill = 0;
                    // ③ 기적 — 신앙 전량 소모, 파티 전체 완전 회복(§3)
                    m.Gauge = 0f;
                    foreach (var o in _party) if (o.Alive) o.Hp = o.MaxHp;
                    m.Cd = 2.0f; _healsCast++; _skillLog[3]++; m.SkillT = 0.7f; FlashParty();
                    FxParticles.Play(FxKind.기적, ToScreen(m.Pos), 1.5f); Hitstop(5); Shake(0.45f);
                }
                else if (m.ForceSkill == 1 || wounded >= 2)
                {
                    // ② 치유의 파동 — 광역 힐. 회복시킨 만큼 신앙이 쌓인다(§3)
                    if (m.ForceSkill == 1) m.ForceSkill = 0;
                    foreach (var o in _party)
                        if (o.Alive && (o.Pos - m.Pos).sqrMagnitude < 49f)
                            m.Gauge += Heal(o, 14f * sp.DmgMul * _bHeal);
                    m.Cd = 1.4f; m.Threat += 10f; _healsCast++; _skillLog[2]++; m.SkillT = 0.45f;
                    FxParticles.Play(FxKind.치유파동, ToScreen(m.Pos), 1.1f);
                }
                else if (worst != null && worst.Hp / worst.MaxHp < 0.85f)
                {
                    m.Gauge += Heal(worst, 24f * sp.DmgMul * _bHeal);
                    m.Cd = 1.0f; m.Threat += 8f; _healsCast++;
                }
                m.Gauge = Mathf.Min(100f, m.Gauge);
                if (m.Gauge > _faithPeak) _faithPeak = m.Gauge;
            }
            else if (m.Job == Job.마법사 && target >= 0)
            {
                // ① 화염폭풍 — 광역 장판. 밀도가 높을수록 이득(§10-2와 정합)
                if (m.ForceSkill == 1 || (m.SkillCd <= 0f && CountMobsNear(_mPos[target], 3.2f) >= 4))
                {
                    if (m.ForceSkill == 1) m.ForceSkill = 0;
                    m.SkillCd = 5f * _bCd; m.Cd = 0.9f / _bAtkSpd; _skillLog[5]++; m.SkillT = 0.6f;
                    Vector2 c = _mPos[target];
                    // 장판 범위를 잠깐 띄운다 — 어디를 태웠는지 보여야 밀집 노림이 읽힌다
                    _stormAt = c; _stormR = Mathf.Sqrt(10.2f); _stormUntil = _t + 0.45f;
                    FxParticles.Play(FxKind.화염폭풍, ToScreen(c), _stormR); Shake(0.28f);
                    for (int j = 0; j < MAXM; j++)
                        if (_mOn[j] && (_mPos[j] - c).sqrMagnitude < 10.2f)
                        {
                            _mHp[j] -= 30f * sp.DmgMul * ChantAtk();
                            FlashMob(j);
                            if (_mHp[j] <= 0f) KillMob(j);
                        }
                    FireAlly(m.Pos, c);
                    continue;
                }
                _mHp[target] -= m.Atk * sp.DmgMul * ChantAtk();
                m.Cd = 0.40f;
                m.Threat += m.Atk * 0.4f;
                AttackFx(m, _mPos[target]); FlashMob(target);
                if (_mHp[target] <= 0f) KillMob(target);
            }
            else if (m.Job == Job.검사 && target >= 0)
            {
                // 연격 스택 → 일섬 (§3 고유 메커니즘)
                m.Gauge += 1f;
                float dmg = m.Atk;
                // 일섬 — 버튼(슬롯1)이면 스택이 덜 찼어도 즉시 터뜨린다(§3 "스택 전량 소모")
                if (m.ForceSkill == 1) { m.ForceSkill = 0; m.Gauge = 0f; dmg = m.Atk * 3.2f; _skillLog[6]++; m.SkillT = 0.5f; FxParticles.Play(FxKind.일섬, ToScreen(_mPos[target])); Hitstop(3); Shake(0.2f); }
                else if (m.Gauge >= 5f) { m.Gauge = 0f; dmg = m.Atk * 3.2f; _skillLog[6]++; m.SkillT = 0.5f; FxParticles.Play(FxKind.일섬, ToScreen(_mPos[target])); Hitstop(3); Shake(0.2f); }
                _mHp[target] -= dmg * sp.DmgMul * ChantAtk();
                m.Cd = 0.35f / _bAtkSpd;
                m.Threat += dmg * 0.4f;
                AttackFx(m, _mPos[target]); FlashMob(target);
                if (_mHp[target] <= 0f) KillMob(target);
            }
            else if (target >= 0)
            {
                _mHp[target] -= m.Atk * sp.DmgMul * ChantAtk();
                m.Cd = m.Role == Role.Dps ? 0.40f : 0.7f;
                m.Threat += m.Atk * 0.4f;
                AttackFx(m, _mPos[target]);          // 근접은 타격만, 원거리는 탄(§3)
                FlashMob(target);
                if (_mHp[target] <= 0f) KillMob(target);
            }
        }
    }

    /// <summary>진군가면 공격 +15%, 수호가면 공격은 그대로 (방어는 Damage에서 처리)</summary>
    float ChantAtk() => _partyChant == Chant.진군가 ? 1.15f : 1.0f;

    /// <summary>파티 전체를 잠깐 번쩍 — 광역 스킬이 터진 걸 눈으로 알 수 있게</summary>
    /// <summary>
    /// 파티 전체를 잠깐 밝게 — 스킬이 나갔다는 신호.
    ///
    /// 예전에는 `Sr.color = Color.white`만 넣었는데, 매 프레임 색을 다시 칠하는 코드가 뒤에 있어
    /// **한 프레임도 보이지 않는 죽은 코드**였다(조사에서 발견). 지속 시간을 갖는 값으로 바꾼다.
    /// </summary>
    void FlashParty()
    {
        foreach (var o in _party) if (o.Alive) o.FlashT = 0.12f;
    }

    bool AllDead()
    {
        foreach (var m in _party) if (m.Alive) return false;
        return true;
    }

    int NearestMob(Vector2 p, float maxR)
    {
        int best = -1; float bd = maxR * maxR;
        for (int i = 0; i < MAXM; i++)
        {
            if (!_mOn[i]) continue;
            float d = (_mPos[i] - p).sqrMagnitude;
            if (d < bd) { bd = d; best = i; }
        }
        return best;
    }

    int CountMobsNear(Vector2 p, float r)
    {
        int c = 0; float r2 = r * r;
        for (int i = 0; i < MAXM; i++) if (_mOn[i] && (_mPos[i] - p).sqrMagnitude < r2) c++;
        return c;
    }

    void KillMob(int i)
    {
        _mOn[i] = false; _mAlive--; _kills++;
        _mTr[i].gameObject.SetActive(false);
    }

    // ── 몹 AI + 어그로 규칙 (§10-4) ───────────────────────
    // 겹침 해소용 임시 버퍼 — 매 프레임 할당하면 GC가 튄다
    Vector2[] _sepPos; bool[] _sepAlive;

    /// <summary>
    /// 몹과 파티를 한 배열에 모아 겹침을 푼다. 파티끼리·몹끼리·서로 전부 대상이다 —
    /// 아군만 밀면 몹이 아군을 통과해 겹치고, 몹만 밀면 파티 5인이 한 점에 뭉친다.
    /// 아군은 지시받은 위치가 있으므로 **덜 밀리게**(강도 0.35) 한다.
    /// </summary>
    void ResolveOverlap()
    {
        int n = MAXM + _party.Length;
        if (_sepPos == null || _sepPos.Length < n) { _sepPos = new Vector2[n]; _sepAlive = new bool[n]; }

        for (int i = 0; i < MAXM; i++) { _sepPos[i] = _mPos[i]; _sepAlive[i] = _mOn[i]; }
        for (int k = 0; k < _party.Length; k++)
        {
            _sepPos[MAXM + k] = _party[k].Pos;
            _sepAlive[MAXM + k] = _party[k].Alive;
        }

        UnitSeparation.Resolve(_sepPos, _sepAlive, n, 0.5f);

        for (int i = 0; i < MAXM; i++)
            if (_mOn[i])
            {
                var p = _sepPos[i];
                if (ArenaLayout.Any) p = ArenaLayout.Resolve(p, 0.3f);
                _mPos[i] = p;
                _mTr[i].position = ToScreen(p);
                _mSr[i].sortingOrder = Depth(p.y);
            }

        for (int k = 0; k < _party.Length; k++)
        {
            var m = _party[k];
            if (!m.Alive) continue;
            // 아군은 명령 위치를 지켜야 하므로 밀린 양의 일부만 반영한다
            m.Pos = Vector2.Lerp(m.Pos, _sepPos[MAXM + k], 0.7f);
            if (ArenaLayout.Any) m.Pos = ArenaLayout.Resolve(m.Pos);
            m.Tr.position = ToScreen(m.Pos);
        }
    }

    void TickMobs(float dt)
    {
        var sp = Spec(_style);
        for (int i = 0; i < MAXM; i++)
        {
            if (!_mOn[i]) continue;
            Vector2 p = _mPos[i];

            // 어그로: 근접은 최근접, 원거리는 **후열(위협 낮은 쪽) 저격**.
            // 단 탱의 도발(Threat 급등)이 그걸 끊는다 — 이 두 줄이 진형을 만든다.
            Member tgt = _mKind[i] == 2 ? PickBackline() : PickNearestOrTaunt(p);
            if (tgt == null) continue;

            Vector2 to = tgt.Pos - p;
            float dist = to.magnitude + 1e-4f;
            Vector2 dir = to / dist;
            float spd;
            Vector2 want;

            if (_mKind[i] == 1)                                   // 포위형
            {
                Vector2 tan = new Vector2(-dir.y, dir.x);
                float w = Mathf.Clamp01((dist - 2.5f) / 4f);
                want = (dir * w + tan * (1f - w)).normalized;
                spd = PlayerSpeed * SwarmerRatio;
            }
            else if (_mKind[i] == 2)                              // 원거리형
            {
                float keep = 6.5f;
                want = dist > keep + 1f ? dir : (dist < keep - 1f ? -dir : new Vector2(-dir.y, dir.x));
                spd = PlayerSpeed * RangedRatio;
                _mCd[i] -= dt;
                if (_mCd[i] <= 0f && dist < 12f) { _mCd[i] = 2.4f; Fire(p, dir); }
            }
            else if (_mKind[i] == 3)                              // 정예: 주변 몹 치유
            {
                want = dir; spd = PlayerSpeed * 0.6f;
                _mCd[i] -= dt;
                if (_mCd[i] <= 0f)
                {
                    _mCd[i] = 1.5f;
                    for (int j = 0; j < MAXM; j++)
                        if (_mOn[j] && (_mPos[j] - p).sqrMagnitude < 16f)
                            _mHp[j] = Mathf.Min(_mKind[j] >= 3 ? 90f : 26f, _mHp[j] + 8f);
                }
            }
            else if (_mKind[i] == 4)                              // 정예: 잡몹 소환
            {
                want = dir; spd = PlayerSpeed * 0.55f;
                _mCd[i] -= dt;
                if (_mCd[i] <= 0f && _mAlive < MAXM - 20) { _mCd[i] = 5.0f; SpawnMob(); }
            }
            else { want = dir; spd = PlayerSpeed * ChaserRatio; } // 추적형

            Vector2 prev = _mPos[i];
            p += want * spd * dt;
            if (ArenaLayout.Any) p = ArenaLayout.Resolve(p, 0.3f);
            _mPos[i] = p;
            _mTr[i].position = ToScreen(p);
            // 좌우 이동을 스프라이트 반전으로 표현한다(방향별 그림이 아직 없다 — 아트문서 §0-A).
            // 아군은 이미 이렇게 하고 있었는데 몹만 항상 같은 쪽을 보고 있어서,
            // 왼쪽으로 몰려갈 때 전부 뒷걸음질치는 것처럼 보였다.
            // ⚠️ 임계값 없이 매 프레임 갱신하면 정지한 몹이 부동소수 잡음으로 떨린다.
            if (Mathf.Abs(p.x - prev.x) > 1e-4f) _mSr[i].flipX = p.x < prev.x;
            _mSr[i].sortingOrder = Depth(p.y);   // 파티와 **같은 공식**이어야 앞뒤가 맞는다(D2)
            if (_mFlash[i] > 0f)
            {
                _mFlash[i] -= dt;
                _mSr[i].color = Color.white;
            }
            else if (_mKind[i] == 3) _mSr[i].color = new Color(0.4f, 1f, 0.5f);
            else if (_mKind[i] == 4) _mSr[i].color = new Color(0.8f, 0.5f, 1f);

            // 근접 공격은 **쿨다운을 두고 한 번씩** — 매 프레임 피해를 주면
            // 초당 수천 번이 되어 파티가 4초 만에 녹는다(2026-08-13 실측 버그)
            if (_mKind[i] != 2 && dist < 1.1f)
            {
                _mAtkCd[i] -= dt;
                if (_mAtkCd[i] <= 0f)
                {
                    _mAtkCd[i] = 1.0f;
                    _meleeHits++;
                    Damage(tgt, 6f * sp.TakenMul, tgt.Role == Role.Tank);
                }
            }
        }
    }

    Member PickNearestOrTaunt(Vector2 p)
    {
        Member best = null; float score = float.MaxValue;
        foreach (var m in _party)
        {
            if (!m.Alive) continue;
            float d = (m.Pos - p).magnitude - m.Threat * 0.12f;   // 위협이 높을수록 끌린다
            if (d < score) { score = d; best = m; }
        }
        return best;
    }

    /// <summary>원거리 몹은 후열(가장 약한 지원 직업)을 노린다 — 도발이 없으면 힐러가 먼저 죽는다</summary>
    Member PickBackline()
    {
        // 도발 지속 중에는 원거리 몹도 탱을 노린다 — 안 그러면 후열이 일방적으로 녹는다
        if (_t < _tauntUntil)
        {
            foreach (var m in _party) if (m.Alive && m.Role == Role.Tank) return m;
        }
        Member best = null; float score = float.MaxValue;
        foreach (var m in _party)
        {
            if (!m.Alive) continue;
            float s = m.MaxHp - m.Threat * 6f;                    // HP 낮고 위협 낮은 쪽
            if (s < score) { score = s; best = m; }
        }
        return best;
    }

    void Damage(Member m, float dmg, bool front)
    {
        if (!m.Alive) return;
        float incoming = dmg;
        if (_partyChant == Chant.수호가) dmg *= 0.82f;      // 음유시인 수호가 오라
        if (m.Shield > 0f)                                   // 수호기사 성채 방패가 먼저 깎인다
        {
            float absorbed = Mathf.Min(m.Shield, dmg);
            m.Shield -= absorbed; dmg -= absorbed;
            _shieldAbsorbed += absorbed;
        }
        if (dmg <= 0f) { if (front) _frontlineHits++; else _backlineHits++; return; }

        // 수호 게이지 — §3 "피격·아군 보호 시 축적". 맞아주는 것이 탱의 자원이다.
        var tank = _party.Length > 0 ? _party[0] : null;
        if (tank != null && tank.Alive && tank.Role == Role.Tank)
        {
            // 자기가 맞으면 크게, 아군이 맞으면 작게 — 아군이 맞는 건 탱이 못 막은 것이다
            tank.Gauge = Mathf.Min(100f, tank.Gauge + (m == tank ? incoming * 0.5f : incoming * 0.12f));
        }

        m.Hp -= dmg;

        // 최후의 보루(§3 수호기사 3번 스킬) — 3초간 HP가 1 미만으로 안 떨어진다.
        // 자동 전투라 조건 발동으로 둔다: 치명타 한 방에 즉사하는 것을 한 번 막아준다.
        if (m.Role == Role.Tank && m.Hp <= 0f && _t >= _lastStandCd)
        {
            m.Hp = 1f;
            _lastStandUntil = _t + 3.0f;
            _lastStandCd = _t + 60f;
            _skillLog[7]++;
            FxParticles.Play(FxKind.무적, ToScreen(m.Pos), 1.6f);
            Debug.Log($"[W3] 최후의 보루 발동 @ {_t:F1}s");
        }
        if (m.Role == Role.Tank && _t < _lastStandUntil && m.Hp < 1f) m.Hp = 1f;
        FxParticles.Play(FxKind.피격, ToScreen(m.Pos));
        if (front) _frontlineHits++; else _backlineHits++;
        if (m.Hp <= 0f)
        {
            m.Hp = 0f; m.DeadT = _t;
            FxParticles.Play(FxKind.사망, ToScreen(m.Pos));
            // 파티원 사망은 §4에서 목숨이 깎이는 사건이다. 수백 마리가 얽힌 화면에서
            // 유닛 하나가 사라지는 것으로는 **아무도 알아채지 못한다** — 화면을 한 번 붉게 친다.
            _screenFlash = 0.35f; _screenFlashColor = new Color(0.9f, 0.15f, 0.2f);
            Shake(0.6f); Hitstop(4);
            m.Tr.gameObject.SetActive(false);
            if (m.Role == Role.Healer && _healerDeadT < 0f) _healerDeadT = _t;
            Debug.Log($"[W3] {m.Role} 사망 @ {_t:F1}s");

            // 누가 죽었는지 밖으로 알린다(§4). 예전엔 판이 끝날 때 생존 여부 하나만 넘겨서
            // 목숨 카운트가 **파티 전멸 단위**로 뭉뚱그려졌다 — 실제로는 한 명만 죽는 판이 더 흔하다.
            // 사망 처리는 이 한 곳뿐이고 Hp<=0이면 Alive가 false라 다시 들어오지 않으므로 중복 기록은 없다.
            _deadJobs.Add(m.Job.ToString());
            OnMemberDied?.Invoke(System.Array.IndexOf(_party, m), m.Job.ToString());
        }
    }

    /// <summary>아군 공격 표시 — 짧게 날아가 사라지는 탄. 판정은 이미 즉시 처리됐고 이건 연출이다</summary>
    void FireAlly(Vector2 from, Vector2 to)
    {
        for (int n = 0; n < MAXP; n++)
        {
            int i = _aCur; _aCur = (_aCur + 1) % MAXP;
            if (_aOn[i]) continue;
            _aOn[i] = true; _aPos[i] = from;
            Vector2 d = (to - from);
            float dist = Mathf.Max(0.5f, d.magnitude);
            _aVel[i] = d / dist * 14f;               // 빠르게 — 타격감
            _aLife[i] = dist / 14f;                  // 대상에 닿으면 소멸
            _aTr[i].gameObject.SetActive(true);
            return;
        }
    }

    void TickAllyShots(float dt)
    {
        for (int i = 0; i < MAXP; i++)
        {
            if (!_aOn[i]) continue;
            _aLife[i] -= dt;
            _aPos[i] += _aVel[i] * dt;
            _aTr[i].position = ToScreen(_aPos[i], -0.6f);
            if (_aLife[i] <= 0f) { _aOn[i] = false; _aTr[i].gameObject.SetActive(false); }
        }
    }

    /// <summary>피격 몹을 흰색으로 잠깐 번쩍 — 뭘 때리고 있는지 보이게</summary>
    void FlashMob(int i) { _mFlash[i] = 0.12f; }

    void Fire(Vector2 from, Vector2 dir)
    {
        for (int n = 0; n < MAXP; n++)
        {
            int i = _pCur; _pCur = (_pCur + 1) % MAXP;
            if (_pOn[i]) continue;
            _pOn[i] = true; _pPos[i] = from; _pVel[i] = dir * 5.5f; _pLife[i] = 4f;
            _pTr[i].gameObject.SetActive(true);
            return;
        }
    }

    void TickShots(float dt)
    {
        var sp = Spec(_style);
        for (int i = 0; i < MAXP; i++)
        {
            if (!_pOn[i]) continue;
            _pLife[i] -= dt;
            _pPos[i] += _pVel[i] * dt;
            _pTr[i].position = ToScreen(_pPos[i], -0.5f);
            foreach (var m in _party)
            {
                if (!m.Alive) continue;
                if ((m.Pos - _pPos[i]).sqrMagnitude < 0.36f)
                {
                    _shotHits++;
                    Damage(m, 9f * sp.TakenMul, m.Role == Role.Tank);
                    _pOn[i] = false; _pTr[i].gameObject.SetActive(false);
                    break;
                }
            }
            if (_pLife[i] <= 0f) { _pOn[i] = false; _pTr[i].gameObject.SetActive(false); }
        }
    }

    void RecordAndNext()
    {
        int aliveCount = 0;
        foreach (var m in _party) if (m.Alive) aliveCount++;
        // 상한까지 살아남았다면 압력이 부족했다는 뜻 — 그건 통과가 아니라 측정 실패다
        // 게임 모드는 측정이 목적이 아니다 — CSV를 쓰지 않고 결과만 알리고 멈춘다.
        // 여기서 CSV까지 쓰면 실제 플레이가 검증 데이터에 섞여 측정값을 오염시킨다.
        if (GameMode)
        {
            OnBattleEnd?.Invoke(aliveCount > 0);
            enabled = false;
            return;
        }

        string verdict = aliveCount > 0 ? "측정실패_상한도달" : "전멸";
        int 최종웨이브 = 시작웨이브 + (int)(_t / 점증간격) * 단계당증가;
        var ci = CultureInfo.InvariantCulture;
        _csv.AppendLine(string.Join(",",
            _setup.Name, _t.ToString("F1", ci), _kills.ToString(ci),
            _tauntUses.ToString(ci), _skillLog[1].ToString(ci), _skillLog[5].ToString(ci),
            _skillLog[6].ToString(ci), _skillLog[3].ToString(ci), _skillLog[4].ToString(ci),
            _backlineHits.ToString(ci), _frontlineHits.ToString(ci),
            _shieldAbsorbed.ToString("F0", ci),
            _healerDeadT < 0 ? "-" : _healerDeadT.ToString("F1", ci),
            최종웨이브.ToString(ci), (_kills / Mathf.Max(1f, _t)).ToString("F2", ci), verdict,
            _faithPeak.ToString("F0", ci), RunSeed().ToString(ci), _rep.ToString(ci)));
        Debug.Log($"[W3] {_setup.Name}: {_t:F1}s 생존 / 처치 {_kills} / 도발 {_tauntUses} / " +
                  $"후열피격 {_backlineHits} 전열피격 {_frontlineHits} / {verdict}");
        Debug.Log($"[W3-DIAG] 몹생존 {_mAlive} / 근접타격 {_meleeHits} / 투사체타격 {_shotHits} / " +
                  $"프레임 {_framesThisRun} / 초당근접 {_meleeHits / Mathf.Max(0.1f, _t):F0}");

        NextStyle();
    }

    void OnGUI()
    {
        _hud ??= new GUIStyle(GUI.skin.label) { fontSize = 17, normal = { textColor = Color.white } };

        // 화면 플래시 — 가장 싼 강조 수단이고, 파티클과 달리 **절대 묻히지 않는다**
        if (_screenFlash > 0f)
        {
            _screenFlash -= Time.deltaTime;
            var c = _screenFlashColor; c.a = Mathf.Clamp01(_screenFlash) * 0.35f;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Tint(c));
        }
        var s = new StringBuilder();
        int wave = 시작웨이브 + (int)(_t / 점증간격) * 단계당증가;
        s.Append($"구성 {_setup.Name}   경과 {_t:F0}s   웨이브목표 {wave}   처치 {_kills}   도발 {_tauntUses}   ");
        s.Append($"전열피격 {_frontlineHits} / 후열피격 {_backlineHits}\n");
        foreach (var m in _party)
            s.Append($"{m.Role} {(m.Alive ? $"{m.Hp:F0}/{m.MaxHp:F0}" : "사망")}   ");
        GUI.DrawTexture(new Rect(8, 126, 1000, 64), Scrim());
        GUI.Label(new Rect(14, 130, 990, 60), s.ToString(), _hud);

        // 지휘 바는 항상 띄운다. 자동 전투 중에도 캐릭터를 골라 옮길 수 있어야 한다(오너 지시).
        // "자동"은 **명령을 안 내렸을 때의 기본값**이지, 개입을 막는 모드가 아니다(§5).
        CommandBar();
    }

    /// <summary>
    /// 지휘 바 (§5) — 파티원 선택 버튼 + 선택한 캐릭터의 스킬 버튼.
    /// 기획서가 말하는 "캐릭터 선택 → 위치 + 스킬" 두 명령 중 **스킬 쪽**이 여기다.
    /// 위치 명령은 TickCommand(마우스)가 받는다.
    /// </summary>
    void CommandBar()
    {
        _cmdBtn ??= new GUIStyle(GUI.skin.button) { fontSize = 15 };
        _cmdLabel ??= new GUIStyle(GUI.skin.label)
        { fontSize = 16, normal = { textColor = new Color(.95f, .96f, 1f) } };

        // ── 파티 목록 (인게임 캐릭터 선택) ──
        // 이름만 있는 버튼으로는 누가 누군지, 누가 위험한지 한눈에 안 들어온다.
        // 초상화 + HP + 목숨을 함께 보여야 §5의 "판을 읽고 지휘한다"가 가능해진다.
        const float CW = 132f, CH = 96f, GAP = 6f;
        float total = _party.Length * CW + (_party.Length - 1) * GAP;
        float x = Mathf.Max(16f, (Screen.width - total) * 0.5f);
        float y = Screen.height - CH - 34f;

        GUI.DrawTexture(new Rect(0, y - 30f, Screen.width, CH + 64f), Scrim());

        var bank = SpriteBank.Cached;
        for (int i = 0; i < _party.Length; i++)
        {
            var m = _party[i];
            var card = new Rect(x + i * (CW + GAP), y, CW, CH);
            bool picked = _sel == i;

            // 선택 표시 — 테두리를 밝게
            if (picked) GUI.DrawTexture(new Rect(card.x - 3, card.y - 3, card.width + 6, card.height + 6),
                                        Tint(new Color(1f, 0.82f, 0.35f, 0.95f)));
            GUI.DrawTexture(card, Tint(m.Alive ? new Color(.10f, .11f, .15f, .95f)
                                               : new Color(.22f, .07f, .07f, .95f)));

            // 초상화 — 아틀라스에서 몸통만 잘라 그린다
            var sp = bank?.Char(ArtOf(m.Role));
            if (sp != null)
            {
                var uv = PortraitUV(sp);
                var pr = new Rect(card.x + 6, card.y + 4, 52, 66);
                GUI.color = m.Alive ? Color.white : new Color(1f, .6f, .6f, .55f);
                GUI.DrawTextureWithTexCoords(pr, sp.texture, uv);
                GUI.color = Color.white;
            }

            GUI.Label(new Rect(card.x + 62, card.y + 6, CW - 66, 20), $"{i + 1}.{m.Job}", _cmdLabel);

            // HP 바 — 숫자보다 길이가 빨리 읽힌다
            float ratio = m.Alive ? Mathf.Clamp01(m.Hp / m.MaxHp) : 0f;
            var bar = new Rect(card.x + 62, card.y + 30, CW - 70, 10);
            GUI.DrawTexture(bar, Tint(new Color(0, 0, 0, .8f)));
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * ratio, bar.height),
                            Tint(ratio > .5f ? new Color(.35f, .85f, .4f)
                               : ratio > .25f ? new Color(.95f, .78f, .3f)
                                              : new Color(.9f, .3f, .3f)));
            GUI.Label(new Rect(card.x + 62, card.y + 42, CW - 66, 18),
                      m.Alive ? $"{m.Hp:F0}/{m.MaxHp:F0}" : "사망", _cmdLabel);

            // 명령 상태 — 이동 지시가 걸려 있으면 표시
            if (m.Order.HasValue)
                GUI.Label(new Rect(card.x + 6, card.y + CH - 24, CW - 12, 20), "▶ 이동 중", _cmdLabel);

            GUI.enabled = m.Alive;
            if (GUI.Button(new Rect(card.x, card.y, card.width, card.height), GUIContent.none, GUIStyle.none))
                _sel = picked ? -1 : i;
            GUI.enabled = true;
        }

        GUI.Label(new Rect(16, y - 26, 1200, 22),
                  _sel < 0 ? "카드를 클릭하거나 1~5 키로 캐릭터 선택 — 자동 전투 중"
                           : $"[{_party[_sel].Job}] 선택됨 — 우클릭으로 이동 지시 · 0으로 해제", _cmdLabel);

        if (_sel < 0 || !_party[_sel].Alive) return;
        SkillButtons(_party[_sel], y - 62f);
    }

    /// <summary>
    /// 선택한 캐릭터의 고유 스킬 버튼 2개(§3 대표 스킬).
    /// 버튼은 쿨다운을 0으로 만드는 게 아니라 **강제 발동 슬롯**을 세운다 —
    /// 쿨만 풀어주면 발동 조건(밀집·부상자 수 등)이 안 맞을 때 눌러도 아무 일이 안 일어나
    /// "눌렀는데 왜 안 나가지"가 된다. 지휘는 지금 쓰는 것이다(§5).
    /// </summary>
    void SkillButtons(Member sel, float y)
    {
        (string a, string b) = sel.Job switch
        {
            Job.수호기사 => ("도발의 함성", "성채 방패"),
            Job.검사 => ("일섬", "—"),
            Job.마법사 => ("화염폭풍", "—"),
            Job.사제 => ("치유의 파동", "기적"),
            _ => ("진군가", "수호가"),
        };

        float w = 176f, gap = 8f;
        float x = Screen.width * 0.5f - (w * 2 + gap) * 0.5f;

        if (SkillBtn(new Rect(x, y, w, 34), a, sel, 1)) { }
        if (b != "—" && SkillBtn(new Rect(x + w + gap, y, w, 34), b, sel, 2)) { }

        GUI.Label(new Rect(x, y - 20f, 520, 20),
                  $"스킬 — 스페이스: {a}", _cmdLabel);
    }

    bool SkillBtn(Rect r, string label, Member m, int slot)
    {
        bool queued = m.ForceSkill == slot;
        if (queued) GUI.DrawTexture(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4),
                                    Tint(new Color(1f, 0.82f, 0.35f, 0.9f)));
        if (GUI.Button(r, queued ? $"{label} ▶" : label, _cmdBtn))
        {
            m.ForceSkill = slot;
            return true;
        }
        return false;
    }

    void Finish()
    {
        File.WriteAllText(_outPath, _csv.ToString());
        Debug.Log("[W3] ===RESULT===\n" + _csv);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
