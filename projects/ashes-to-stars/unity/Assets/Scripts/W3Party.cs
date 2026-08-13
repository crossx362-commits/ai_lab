using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

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
        public Chant Chant;               // 음유시인 악장
        public float Threat;                 // 어그로 수치
        public bool Alive => Hp > 0f;
        public float DeadT;

        // ── 연출 ──
        public SpriteRenderer BarBg, BarFg;  // 머리 위 체력바
        public Vector2 PrevPos;              // 이동 여부 판정용
        public float AnimT;                  // 현재 동작이 시작된 뒤 흐른 시간
        public float AttackT, HurtT;         // 남은 동작 시간 (0보다 크면 그 동작 중)
        public SpriteBank.Motion Mo;
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
    Chant _partyChant = Chant.진군가;   // 음유시인 악장 (파티 오라)
    // 스킬 사용 횟수: 0도발 1성채방패 2치유파동 3기적 4악장전환 5화염폭풍 6일섬
    readonly int[] _skillLog = new int[7];
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
    /// <summary>판이 끝났을 때 호출. true=생존(상한 도달) / false=전멸</summary>
    public System.Action<bool> OnBattleEnd;

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
        }
        _outPath ??= Path.Combine(Application.persistentDataPath, "w3_result.csv");
        // faith_peak는 열 끝에 붙인다 — 중간에 끼우면 이 CSV를 읽는 기존 도구가 조용히 어긋난다
        _csv.AppendLine("setup,survived_s,kills,taunts,shield,firestorm,ilseom,miracle,chant_sw,backline_hits,frontline_hits,shield_absorbed,healer_died_at,final_wave,kills_per_sec,verdict,faith_peak");

        BuildWorld();
        NextStyle();
    }

    void BuildWorld()
    {
        var bank = SpriteBank.Load();
        GroundBuilder.Build(bank, Arena + 20f);

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
    /// 실제로 채워진 HP만큼을 돌려준다 — 신앙(§3 "회복량 누적")의 계량 단위.
    /// 이미 만피인 대상에 힐을 넣어도 신앙이 쌓이면 축적이 부풀려진다.
    /// </summary>
    static float Heal(Member o, float amount)
    {
        float before = o.Hp;
        o.Hp = Mathf.Min(o.MaxHp, o.Hp + amount);
        return o.Hp - before;
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

    void NextStyle()
    {
        _qi++;
        // 게임 모드는 표준 5인(A) 한 판만 — 두 번째 구성으로 넘어가지 않는다
        if (GameMode && _qi > 0) return;
        if (_qi >= SETUPS.Length) { Finish(); return; }
        _setup = SETUPS[_qi];
        _tauntEnabled = _setup.TauntEnabled;
        _style = Style.Balanced;                     // 스타일 고정 — 구성만 비교한다

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
            m.MaxHp = m.Role == Role.Tank ? 320f : m.Role == Role.Dps ? 130f : 150f;
            m.Atk = m.Role == Role.Dps ? 26f : m.Role == Role.Tank ? 10f : m.Role == Role.Buffer ? 8f : 6f;
            m.Range = m.Role == Role.Dps ? 5.5f : m.Role == Role.Tank ? 1.3f : 6.5f;
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
        _meleeHits = 0; _shotHits = 0; _framesThisRun = 0;
        _tauntUntil = -1f; _partyChant = Chant.진군가;
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

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        _t += dt; _framesThisRun++;
        // 전멸이 판정 기준이다. 시간 상한은 무한 루프를 막는 안전장치일 뿐 —
        // 45초 고정 상한을 쓰던 1차 실험은 다섯 구성 중 넷이 완주해버려
        // "얼마나 버티는가"를 물을 수 없었다(§21-1g).
        if (AllDead() || _t >= 최대시간)
        { RecordAndNext(); return; }

        TickParty(dt);
        TickMobs(dt);
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

        foreach (var m in _party)
        {
            if (m.AttackT > 0f) m.AttackT -= dt;
            if (m.HurtT > 0f) m.HurtT -= dt;

            // 동작 우선순위: 사망 > 피격 > 공격 > 이동 > 대기
            var want = !m.Alive ? SpriteBank.Motion.Death
                     : m.HurtT > 0f ? SpriteBank.Motion.Hurt
                     : m.AttackT > 0f ? SpriteBank.Motion.Attack
                     : (m.Pos - m.PrevPos).sqrMagnitude > 1e-5f ? SpriteBank.Motion.Walk
                     : SpriteBank.Motion.Idle;

            if (want != m.Mo) { m.Mo = want; m.AnimT = 0f; }
            else m.AnimT += dt;

            m.Sr.sprite = bank.CharAnim(ArtOf(m.Role), m.Mo, m.AnimT);
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

            if (m.Role == Role.Tank)
            {
                // 수호기사: 무리 쪽으로 전진 + 수호 게이지 축적
                int t2 = NearestMob(m.Pos, 99f);
                if (t2 >= 0) want = (_mPos[t2] - m.Pos).normalized;
                m.Gauge = Mathf.Min(100f, m.Gauge + dt * 14f);   // 피격·보호로 축적(단순화)

                if (_tauntEnabled && m.SkillCd <= 0f && CountMobsNear(m.Pos, 4.5f) >= 3)
                {
                    // ① 도발의 함성 — 광역 어그로. 원거리 몹까지 끌어야 후열이 산다(§10-4 대응)
                    m.SkillCd = 6f; m.Threat += 80f; _tauntUses++;
                    _tauntUntil = _t + 3.0f;                      // 3초간 원거리도 탱을 노린다
                    _skillLog[0]++;
                }
                if (m.Gauge >= 60f)
                {
                    // ② 성채 방패 — 게이지를 소모해 파티 전체에 보호막
                    m.Gauge = 0f;
                    foreach (var o in _party)
                        if (o.Alive) o.Shield = Mathf.Max(o.Shield, 40f);
                    FlashParty();
                    _skillLog[1]++;
                }
            }
            else
            {
                // 딜·힐: 탱 뒤에 붙되 스타일의 유지거리만큼 물러선다
                Vector2 behind = tank.Alive ? tank.Pos - Vector2.up * sp.KeepDist : Vector2.zero;
                want = (behind - m.Pos);
                if (want.magnitude < 0.3f) want = Vector2.zero; else want = want.normalized;

                // 저체력이면 더 물러선다 (스타일별 임계)
                if (m.Hp / m.MaxHp < sp.RetreatHp)
                {
                    int near = NearestMob(m.Pos, 6f);
                    if (near >= 0) want = (m.Pos - _mPos[near]).normalized;
                }
            }

            m.Pos += want * PlayerSpeed * 0.85f * dt;
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
                var wantChant = worstRatio < 0.45f ? Chant.수호가 : Chant.진군가;
                if (wantChant != m.Chant) { m.Chant = wantChant; _skillLog[4]++; }
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
                if (m.Gauge >= 100f && (wounded >= 3 || worstRatio2 < 0.35f))
                {
                    // ③ 기적 — 신앙 전량 소모, 파티 전체 완전 회복(§3)
                    m.Gauge = 0f;
                    foreach (var o in _party) if (o.Alive) o.Hp = o.MaxHp;
                    m.Cd = 2.0f; _healsCast++; _skillLog[3]++; FlashParty();
                }
                else if (wounded >= 2)
                {
                    // ② 치유의 파동 — 광역 힐. 회복시킨 만큼 신앙이 쌓인다(§3)
                    foreach (var o in _party)
                        if (o.Alive && (o.Pos - m.Pos).sqrMagnitude < 49f)
                            m.Gauge += Heal(o, 14f * sp.DmgMul);
                    m.Cd = 1.4f; m.Threat += 10f; _healsCast++; _skillLog[2]++;
                }
                else if (worst != null && worst.Hp / worst.MaxHp < 0.85f)
                {
                    m.Gauge += Heal(worst, 24f * sp.DmgMul);
                    m.Cd = 1.0f; m.Threat += 8f; _healsCast++;
                }
                m.Gauge = Mathf.Min(100f, m.Gauge);
                if (m.Gauge > _faithPeak) _faithPeak = m.Gauge;
            }
            else if (m.Job == Job.마법사 && target >= 0)
            {
                // ① 화염폭풍 — 광역 장판. 밀도가 높을수록 이득(§10-2와 정합)
                if (m.SkillCd <= 0f && CountMobsNear(_mPos[target], 3.2f) >= 4)
                {
                    m.SkillCd = 5f; m.Cd = 0.9f; _skillLog[5]++;
                    Vector2 c = _mPos[target];
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
                FireAlly(m.Pos, _mPos[target]); FlashMob(target);
                if (_mHp[target] <= 0f) KillMob(target);
            }
            else if (m.Job == Job.검사 && target >= 0)
            {
                // 연격 스택 → 일섬 (§3 고유 메커니즘)
                m.Gauge += 1f;
                float dmg = m.Atk;
                if (m.Gauge >= 5f) { m.Gauge = 0f; dmg = m.Atk * 3.2f; _skillLog[6]++; }
                _mHp[target] -= dmg * sp.DmgMul * ChantAtk();
                m.Cd = 0.35f;
                m.Threat += dmg * 0.4f;
                FireAlly(m.Pos, _mPos[target]); FlashMob(target);
                if (_mHp[target] <= 0f) KillMob(target);
            }
            else if (target >= 0)
            {
                _mHp[target] -= m.Atk * sp.DmgMul * ChantAtk();
                m.Cd = m.Role == Role.Dps ? 0.40f : 0.7f;
                m.Threat += m.Atk * 0.4f;
                FireAlly(m.Pos, _mPos[target]);      // 공격이 보여야 전투처럼 보인다
                FlashMob(target);
                if (_mHp[target] <= 0f) KillMob(target);
            }
        }
    }

    /// <summary>진군가면 공격 +15%, 수호가면 공격은 그대로 (방어는 Damage에서 처리)</summary>
    float ChantAtk() => _partyChant == Chant.진군가 ? 1.15f : 1.0f;

    /// <summary>파티 전체를 잠깐 번쩍 — 광역 스킬이 터진 걸 눈으로 알 수 있게</summary>
    void FlashParty()
    {
        foreach (var o in _party) if (o.Alive) o.Sr.color = Color.white;
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

            p += want * spd * dt;
            _mPos[i] = p;
            _mTr[i].position = ToScreen(p);
            _mSr[i].sortingOrder = (int)(-p.y * 16f);
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
        if (_partyChant == Chant.수호가) dmg *= 0.82f;      // 음유시인 수호가 오라
        if (m.Shield > 0f)                                   // 수호기사 성채 방패가 먼저 깎인다
        {
            float absorbed = Mathf.Min(m.Shield, dmg);
            m.Shield -= absorbed; dmg -= absorbed;
            _shieldAbsorbed += absorbed;
        }
        if (dmg <= 0f) { if (front) _frontlineHits++; else _backlineHits++; return; }
        m.Hp -= dmg;
        if (front) _frontlineHits++; else _backlineHits++;
        if (m.Hp <= 0f)
        {
            m.Hp = 0f; m.DeadT = _t;
            m.Tr.gameObject.SetActive(false);
            if (m.Role == Role.Healer && _healerDeadT < 0f) _healerDeadT = _t;
            Debug.Log($"[W3] {m.Role} 사망 @ {_t:F1}s");
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
            _faithPeak.ToString("F0", ci)));
        Debug.Log($"[W3] {_setup.Name}: {_t:F1}s 생존 / 처치 {_kills} / 도발 {_tauntUses} / " +
                  $"후열피격 {_backlineHits} 전열피격 {_frontlineHits} / {verdict}");
        Debug.Log($"[W3-DIAG] 몹생존 {_mAlive} / 근접타격 {_meleeHits} / 투사체타격 {_shotHits} / " +
                  $"프레임 {_framesThisRun} / 초당근접 {_meleeHits / Mathf.Max(0.1f, _t):F0}");

        var dir = Path.GetDirectoryName(_outPath);
        ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"w3_{_setup.Name}.png"));
        NextStyle();
    }

    void OnGUI()
    {
        _hud ??= new GUIStyle(GUI.skin.label) { fontSize = 17, normal = { textColor = Color.white } };
        var s = new StringBuilder();
        int wave = 시작웨이브 + (int)(_t / 점증간격) * 단계당증가;
        s.Append($"구성 {_setup.Name}   경과 {_t:F0}s   웨이브목표 {wave}   처치 {_kills}   도발 {_tauntUses}   ");
        s.Append($"전열피격 {_frontlineHits} / 후열피격 {_backlineHits}\n");
        foreach (var m in _party)
            s.Append($"{m.Role} {(m.Alive ? $"{m.Hp:F0}/{m.MaxHp:F0}" : "사망")}   ");
        GUI.Label(new Rect(14, 10, 900, 60), s.ToString(), _hud);
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
