using System.Collections.Generic;
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

    // ── 전투 스타일 수치의 단일 소스 (§3 전투 스타일 표) ──────────────
    // 예전에는 이 표가 **코드에만** 있었고 `CombatStyleDef`(ScriptableObject)는
    // 만들어만 두고 **읽는 곳이 0곳**이었다(grep 확인). 즉 기획자가 인스펙터에서 값을 바꿔도
    // 전투는 코드 상수를 계속 썼다 — 에셋으로 옮긴 의미가 통째로 없었다.
    //
    // ⚠️ 기존 `_Game/Data/Styles/*.asset`은 **m_Script가 fileID 0으로 끊겨** 있었다
    //    (CombatStyleDef.cs 머리 주석이 경고한 바로 그 사고). 게다가 `Resources/` 밖이라
    //    런타임이 애초에 못 읽는다 — 이 저장소가 이미 겪은 「Resources 밖 자산」 함정이다.
    //    그래서 `Assets/Resources/styles/`에 다시 만들어 넣었다.
    //
    // SO를 못 읽으면 아래 코드 표로 폴백한다. 검증(W1~W3)이 에셋 사고로 멈추면 안 되기 때문이다 —
    // 다만 **조용히** 폴백하지 않는다(한 번 경고).
    static Dictionary<Style, StyleSpec> _styleCache;
    static bool _styleWarned;

    static StyleSpec Spec(Style s)
    {
        if (_styleCache == null)
        {
            _styleCache = new Dictionary<Style, StyleSpec>();
            var defs = Resources.LoadAll<AshesToStars.CombatStyleDef>("styles");
            foreach (var d in defs)
            {
                var key = d.Id switch
                {
                    AshesToStars.StyleId.공격형 => Style.Aggressive,
                    AshesToStars.StyleId.방어형 => Style.Defensive,
                    AshesToStars.StyleId.생존형 => Style.Survival,
                    _ => Style.Balanced,
                };
                _styleCache[key] = new StyleSpec(d.딜배율, d.피해배율, d.후퇴체력, d.유지거리);
            }
            if (defs.Length == 0 && !_styleWarned)
            {
                _styleWarned = true;
                Debug.LogWarning("[W3] Resources/styles 에 전투 스타일 에셋이 없다 — 코드 기본값으로 폴백");
            }
        }
        if (_styleCache.TryGetValue(s, out var spec)) return spec;

        return s switch
        {
            Style.Aggressive => new StyleSpec(1.15f, 1.20f, 0.00f, 0.9f),
            Style.Balanced => new StyleSpec(1.00f, 1.00f, 0.15f, 1.4f),
            Style.Defensive => new StyleSpec(0.90f, 0.85f, 0.30f, 2.6f),
            Style.Survival => new StyleSpec(0.70f, 0.75f, 0.50f, 4.0f),
            _ => new StyleSpec(1, 1, 0, 1.5f),
        };
    }

    // ── 파티원 ───────────────────────────────────────────
    enum Role { Tank, Dps, Healer, Buffer }
    /// <summary>기획서 §3의 1차 전직 — 각자 고유 메커니즘 1개 + 대표 스킬</summary>
    enum Job { 수호기사, 광전사, 검사, 궁수, 마법사, 소환사, 사제, 드루이드, 음유시인, 주술사, 정령사 }
    enum Chant { 진군가, 수호가 }        // 음유시인 악장 (§3 악장 전환)

    class Member
    {
        public Role Role;
        public Style Style;
        public Transform Tr;
        public SpriteRenderer Sr;
        public Vector2 Pos;
        public Job Job;
        public AdvancementTier Advancement;
        public int SkillCount => Advancement == AdvancementTier.Basic ? 2 : 4;
        public float Hp, MaxHp, Atk, Range, Cd, SkillCd;
        public float Shield;              // 수호기사 성채 방패
        public float UltimateGauge;       // 2차 초필 전용 자원(직업 Gauge와 분리)
        public float UltimateCd;          // §18-6: 180초 재사용 대기
        public bool Flip;                 // 마지막으로 향한 좌우. 미세 흔들림에 안 뒤집히게 유지한다
        // ── 이동기(§5 ✅ 대시·구르기) ──
        public float DashT;               // 이동기 진행 잔여 시간(>0이면 미끄러지는 중)
        public float DashCd;              // 재사용 대기
        public float IFrame;              // 무적 잔여 — **사망 카운트에 직접 개입하는 능력**이다
        public Vector2 DashDir;
        public float DangerT;             // 위험에 놓인 시간 — AI는 이게 쌓여야 반응한다(서툴게)
        public float AiDashCheck;         // 다음 판단까지 남은 시간
        public float Gauge;               // 고유 자원: 수호게이지 / 연격스택 / 신앙
        public float FocusUntil;          // 기본 딜 「집중」 지속 종료 시각
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
        public bool ForceUltimate;
        /// <summary>명령 지점 표시(땅에 찍히는 점)</summary>
        public SpriteRenderer Marker;
    }

    // ── 수동 지휘 상태 (§5 "보스는 수동 지휘") ──
    int _sel = -1;                     // 선택된 파티 슬롯. -1이면 선택 없음
    GUIStyle _cmdBtn, _cmdLabel;

    // 글자 뒤에 까는 반투명 판.
    // 전투 화면은 배경을 안 깔기 때문에(카메라 렌더를 보여줘야 하므로) 밝은 바닥 위에
    // 흰 글씨가 그대로 놓여 읽히지 않았다(오너 지적 "글씨가 안보인다고").
    // ⛔ 절차 생성 링(`skill_ring`)은 **삭제됐다**(오너 지시 2026-08-15, 두 번 지적).
    //    스킬 범위를 보여주려고 코드로 그린 흰 원이었는데, 이펙트 로딩이 상시 실패해서
    //    (Texture2D로 임포트된 png를 `Load<Sprite>`로 읽고 있었다) 이 임시물이 화면의
    //    기본값이 돼 있었다. 지금은 `Resources/fx/`의 실제 이펙트만 쓰고, 그림이 없으면
    //    아무것도 안 그린다. **다시 만들지 마라** — 임시 대체물이 본물을 가리면
    //    결함이 결함으로 보이지 않는다.

    float _stormUntil; Vector2 _stormAt; float _stormR;

    // ── 스킬 이펙트 (Resources/FX) ────────────────────────
    // 절차 생성 링만으로는 어떤 스킬인지 구분이 안 된다. 실제 이펙트 아트를 얹는다.
    // 아틀라스에 넣지 않는 이유: 동시에 떠 있는 이펙트가 많아야 두세 개라 배칭 이득이 없고,
    // 큰 텍스처(256²)를 캐릭터 아틀라스에 밀어 넣으면 그쪽이 넘친다.
    // ⛔ 스킬 이펙트 **스프라이트 로딩 전체를 삭제했다**(2026-08-15).
    //    `Resources/fx/`의 프레임 28장을 읽어 `_fxTaunt`·`_fxStorm`·`_fxHeal`·`_fxMiracle`·
    //    `_fxChant*`에 담고 있었는데, 바닥 범위 링을 지운 뒤로 **그리는 곳이 0곳**이 됐다
    //    (선언과 대입만 2회씩, 실측). 내가 링을 지우면서 만든 죽은 코드다.
    //    지금 스킬 연출은 `FxPool`(한 장짜리 심볼 8종 + 코드 애니메이션)과
    //    `SkillCast`(이름·히트스톱·흔들림)가 담당한다. 되살리려면 그 둘과 역할이
    //    겹치지 않는지 먼저 볼 것 — 자산 자체는 `Resources/fx/`에 그대로 있다.


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

    // ⛔ `MakeRing`/`PlaceRing`은 **삭제됐다**(오너 지시 2026-08-15). 바닥에 원을 까는
    //    범위 표시 자체를 없앴다 — 상세는 아래 전투 갱신부 주석 참조. 되살리지 마라.

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
    // §10-9는 잡몹 상한을 300~500으로 정했는데 코드는 200에 머물러 있었다.
    // 200으로 둔 근거가 "성능이 걱정된다"였지 **측정이 아니었다** — `StressTest`는
    // 완성돼 있었지만 어디서도 생성되지 않는 죽은 코드라 60fps 기준을 한 번도 잰 적이 없다.
    // 진입점(`GAME_START=stress`)을 만들어 실측한 결과(`output/qa/ashes-to-stars/w1_result.csv`):
    //   300체 최저 177.9fps · **500체 최저 106.7fps** · 700체 94.4 · 1000체 56.7(여기서 60 붕괴)
    // 그래서 기획서 상한인 500을 그대로 쓴다. 1000은 실제로 못 버틴다.
    const int MAXM = 500;

    /// <summary>
    /// 죽은 뒤 사망 모션이 도는 시간. 0이면 살아 있거나 이미 치워졌다.
    ///
    /// 오너 지적 2026-08-15 「몬스터 스프라이트에 죽는 모션 있는데 그냥 사라지게 하지 마」.
    /// 몹마다 `death_00~03` 4장을 뽑아 놨는데 `KillMob`이 그 자리에서 오브젝트를 꺼서
    /// **한 장도 화면에 안 나왔다** — 이 저장소의 「만들어 놓고 안 쓰는」 계열이 아트에도 있었다.
    /// </summary>
    float[] _mDeadT;

    // ── 돌진형(§10-2 「예고 표식 후 직선 돌진 — 회피 가능한 위협, 수동 조작 보상」) ──
    // 3박자로 나눈다. **예고가 없으면 회피할 수 없고, 경직이 없으면 회피해도 보상이 없다.**
    //   0 접근 → 1 예고(0.8s, 제자리에서 노려본다) → 2 돌진(1.2s, 직선 고속) → 3 경직(1.0s)
    // 대시(§5)의 무적 0.3초가 이 돌진을 통과하는 「정확히 쓰면 한 번 산다」의 대상이다.
    float[] _mChargeT; int[] _mChargePhase; Vector2[] _mChargeDir;
    const float CHG_TELL = 0.8f, CHG_RUSH = 1.2f, CHG_STUN = 1.0f;
    const float CHG_RANGE = 7.5f;      // 이 거리 안에 들어오면 예고를 시작한다
    const float CHG_SPEED = 3.2f;      // 기본 이동 대비 배율 — 걸어서는 못 피한다
    /// <summary>예고·돌진 횟수. 0이면 배선했는데 안 도는 것이다 — QA가 캡처 시점에 읽는다.</summary>
    int _chargeTells, _chargeRushes;
    public static (int tell, int rush) ChargeStatsOnActive()
        => _game != null ? (_game._chargeTells, _game._chargeRushes) : (-1, -1);
    /// <summary>4프레임 × 0.1초(SpriteBank가 그렇게 끊는다) + 마지막 장을 잠깐 남긴다.</summary>
    const float DEATH_ANIM_SEC = 0.55f;
    Transform[] _mTr; SpriteRenderer[] _mSr;
    Vector2[] _mPos; float[] _mHp, _mCd, _mAtkCd, _mMaxHp; int[] _mKind; bool[] _mOn;
    SpriteRenderer[] _mBarBg, _mBarFg;      // 몹 체력바 (다친 개체만 표시)
    // kind: 0 추적 / 1 포위 / 2 원거리 / 3 정예-치유 / 4 정예-소환
    int _mAlive;

    // ── 보스 쫄 소환(§10-5) ───────────────────────────────
    // 보스전 소환 기믹이 만든 몹만 별도로 표시한다. 소환 몹이 파티에 준 피해를 따로
    // 셈해야 「소환 비활성 → 그 피해가 사라짐」 네거티브 컨트롤이 성립한다(웨이브 몹 피해와 분리).
    // BossBattle 인스턴스를 몰라도 되게 정적 통로를 둔다(회복·피해 보고와 같은 idiom).
    bool[] _mSummoned;
    int _summonedAlive;                     // 지금 살아 있는 소환 몹 수 — 보스가 슬롯 해제를 판정
    float _summonDmgToParty; int _summonHits;   // 소환 몹이 파티에 준 누적 피해·타격 수
    static W3Party _game;                    // GameMode(실플레이) 판. 보스 기믹이 여기로 소환한다

    const int MAXP = 200;
    Transform[] _pTr; Vector2[] _pPos, _pVel; float[] _pLife; bool[] _pOn; int _pCur;
    // 아군 공격 연출용 별도 풀 (몹 탄과 색으로 구분)
    Transform[] _aTr; Vector2[] _aPos, _aVel; float[] _aLife; bool[] _aOn; int _aCur;
    float[] _mFlash;
    Color[] _mTint;      // 개체별 계열색. 스폰 때 한 번 정하고 그대로 쓴다(깜빡임 방지)
    bool[] _mFlip;       // 마지막으로 향한 방향. 미세 흔들림에 안 뒤집히게 이력을 둔다

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

    /// <summary>
    /// true면 파티 전원에게 `_style`을 그대로 먹인다 — **검증 전용**이다.
    /// W1~W3 구성 비교는 스타일이 변수로 끼면 성립하지 않는다(같은 이유로 `SuppressTint`가 있다).
    /// 게임 플레이에서는 false라야 플레이어의 직업별 선택이 반영된다.
    /// </summary>
    public static bool UseFixedStyle;

    /// <summary>직업별 저장된 선택을 전투 스타일로 옮긴다(§3).</summary>
    static Style StyleOf(Job job) => AshesToStars.CombatStylePrefs.Get(job.ToString()) switch
    {
        AshesToStars.StyleId.공격형 => Style.Aggressive,
        AshesToStars.StyleId.방어형 => Style.Defensive,
        AshesToStars.StyleId.생존형 => Style.Survival,
        _ => Style.Balanced,
    };
    float _t;
    int _kills, _tauntUses, _backlineHits, _frontlineHits, _healsCast;
    float _shieldAbsorbed;
    int _supportHits;       // 힐·버퍼가 맞은 횟수 — 「후열 저격」이 실제로 일어나는지의 유일한 증거
    int _meleeHits, _shotHits, _framesThisRun;
    float _tauntUntil;                 // 도발이 원거리 몹까지 끄는 구간
    Member _tauntMember;               // 도발을 실제 시전한 수호기사 — 파티 0번 고정 금지
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
    /// <summary>검증 하네스가 `--race 엘프`로 종족을 강제할 때 그 이름. null이면 게임과
    /// 똑같이 `RacePrefs.Get()`(계정 선택)을 쓴다 — 구성 비교는 종족을 고정해야 성립한다.</summary>
    string _forcedRace;
    readonly StringBuilder _csv = new StringBuilder();
    /// <summary>대조 실험 구성 (§21-1f) — 스타일은 균형형 고정, 구성만 바꾼다</summary>
    struct Setup
    {
        public string Name;
        public Job[] Jobs;
        public AdvancementTier[] Advancements;
        public bool TauntEnabled;
        public Setup(string n, Job[] j, bool taunt = true)
        {
            Name = n; Jobs = j; TauntEnabled = taunt;
            Advancements = new AdvancementTier[j.Length];
            for (int i = 0; i < Advancements.Length; i++) Advancements[i] = AdvancementTier.First;
        }
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
    static Setup? PartySetup()
    {
        var combatants = PartyState.SortieCombatants();
        if (combatants == null || combatants.Count == 0) return null;
        var jobs = new System.Collections.Generic.List<Job>();
        var advancements = new System.Collections.Generic.List<AdvancementTier>();
        foreach (var combatant in combatants)
        {
            if (System.Enum.TryParse(combatant.Job, out Job j)) jobs.Add(j);
            else jobs.Add(Job.검사);   // 손상된/미래 직업 저장만 안전 폴백 — 확정 1차 11종은 모두 enum에 있어야 한다
            advancements.Add(combatant.Advancement);
        }
        if (jobs.Count == 0) return null;
        var setup = new Setup("편성 파티", jobs.ToArray());
        setup.Advancements = advancements.ToArray();
        return setup;
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

    /// <summary>QA에서 1차 전직 하나의 두 조작 슬롯을 실전투에 강제하는 직업명.</summary>
    public static string QaFirstAdvancementJob;
    public static int QaAdvancementDisabledSlot;
    float _qaAdvSlot1, _qaAdvSlot2;
    bool _qaAdvForced1, _qaAdvForced2;

    public static string[] FirstAdvancementProbeMetricNames(string job) => job switch
    {
        "광전사" => new[] { "광폭화 피해", "대지 가르기 명중" },
        "궁수" => new[] { "관통 사격 피해", "집중 지속초" },
        "소환사" => new[] { "소환수 돌격 피해", "위치 교체 횟수" },
        "드루이드" => new[] { "자연 표식 피해", "재생 회복" },
        "주술사" => new[] { "저주 중첩", "쇠약 지연초" },
        "정령사" => new[] { "화염 정령 가속 인원", "물 정령 보호막" },
        _ => System.Array.Empty<string>(),
    };

    public static (float slot1, float slot2) FirstAdvancementProbeOnActive() =>
        _game != null ? (_game._qaAdvSlot1, _game._qaAdvSlot2) : (-1f, -1f);

    // ── 2차 각성 초필 계측(§3·§18-6) ──
    public const float UltimateCooldownSeconds = 180f;
    public static string QaSecondAdvancementJob;
    /// <summary>1=단계 차단, 2=게이지 차단, 3=쿨다운 차단.</summary>
    public static int QaUltimateBlock;
    float _qaUltimateEffect;
    bool _qaUltimateForced;

    public static bool CanUseUltimate(AdvancementTier tier, float gauge, float cooldown) =>
        tier == AdvancementTier.Second && gauge >= 100f && cooldown <= 0f;

    public static bool SupportsAdvancementJob(string job) =>
        !string.IsNullOrEmpty(job) && System.Enum.TryParse(job, out Job _);

    public static bool UltimateProbePassed(int blockedCondition, float measuredEffect) =>
        blockedCondition == 0 ? measuredEffect > 0f : measuredEffect <= 0f;

    public static float UltimateProbeOnActive() =>
        _game != null ? _game._qaUltimateEffect : -1f;

    /// <summary>
    /// 계열 색조를 끈다. W1~W3 검증 하네스가 켜서 **구성 대조 실험에 색이 끼지 않게** 한다.
    /// `FamilyTint`가 static이라 인스턴스 필드(`GameMode`)를 못 봐서 static으로 둔다.
    /// </summary>
    public static bool SuppressTint;

    /// <summary>
    /// 필드 전투에 **실제로 막히는** 엄폐물을 켠다(§10-2 — 원거리 몹 대응이 판단이 되려면
    /// 돌아갈 지형이 있어야 한다). 던전은 `ArenaLayout` 템플릿이 이미 맡으므로 필드 전용이다.
    ///
    /// ⚠️ 왜 Awake가 아니라 별도 진입점인가: `GameMode`는 `BattleScreen`이 AddComponent
    ///    **뒤에** 대입하므로 Awake 시점엔 false다. 모드로 분기하려다 조용히 꺼지는 사고를
    ///    이미 겪었다(NextStyle 648 주석). 그래서 **켜는 쪽이 명시적으로 부른다.**
    ///    검증(W1~W3)은 이 메서드를 부르지 않으므로 빈 판이 그대로 유지된다.
    /// </summary>
    public void EnableFieldCover()
    {
        if (DungeonRun.Active) return;              // 던전은 템플릿이 맡는다 — 두 벌이 겹치면 길이 막힌다
        AshesToStars.FieldDecor.Build(SpriteBank.Load(), Arena, 20260813,
                                      AshesToStars.FieldDecor.Biome.Field, 엄폐물: true);
    }

    /// <summary>
    /// AddComponent 직후 Awake가 검증 기본 파티를 먼저 세우므로, GameMode 대입 뒤 실제 로스터로
    /// 한 번 다시 시작한다. 이 호출이 없으면 SortieCombatants는 정의만 있고 전투 소비처가 없다.
    /// </summary>
    public void ApplyGameParty()
    {
        if (!GameMode) return;
        _qi = -1;
        NextStyle();
    }

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
            // `--out`은 검증 하네스(W1~W3)만 준다 — CSV를 받아 구성을 대조하는 실행이다.
            // 그때만 계열 색조를 끈다. 색이 끼면 스크린샷 대조가 흔들린다.
            if (a[i] == "--out" && i + 1 < a.Length) { _outPath = a[i + 1]; SuppressTint = true; }
            if (a[i] == "--seconds" && i + 1 < a.Length)
                float.TryParse(a[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out 최대시간);
            if (a[i] == "--seed" && i + 1 < a.Length) int.TryParse(a[i + 1], out _seed);
            if (a[i] == "--reps" && i + 1 < a.Length && int.TryParse(a[i + 1], out int rp))
                _reps = Mathf.Clamp(rp, 1, 50);
            // `--race`는 종족 배선(§3·§18-9) 검증용 — 하네스가 인간(기준) vs 엘프/드워프를
            // 같은 시드로 돌려 배율이 결과에 반영되는지 CSV로 대조한다.
            if (a[i] == "--race" && i + 1 < a.Length) _forcedRace = a[i + 1];
        }
        _outPath ??= Path.Combine(Application.persistentDataPath, "w3_result.csv");
        // 새 열은 **끝에만** 붙인다 — 중간에 끼우면 이 CSV를 읽는 기존 도구가 조용히 어긋난다
        _csv.AppendLine("setup,survived_s,kills,taunts,shield,firestorm,ilseom,miracle,chant_sw,backline_hits,frontline_hits,shield_absorbed,healer_died_at,final_wave,kills_per_sec,verdict,faith_peak,seed,rep,support_hits");

        BuildWorld();
        NextStyle();
    }

    /// <summary>선택된 종족(§3·§18-9)의 기울기를 파티 배율 버스에 곱해 넣는다. `RaceDef`
    /// 에셋(Resources/races)을 읽는 유일한 런타임 소비처다. 전투에 걸리는 것은 체력·이속뿐 —
    /// 방어/경험치/드랍/영지는 전투 밖 계량이라 W3에 없다. 실패해도(에셋 없음 등) 인간=1로
    /// 조용히 넘어간다. `_forcedRace`(하네스 `--race`)가 있으면 그것, 없으면 계정 선택.</summary>
    void ApplyRaceModifiers()
    {
        RaceId id = AshesToStars.RacePrefs.Default;
        if (!string.IsNullOrEmpty(_forcedRace) && System.Enum.TryParse(_forcedRace, out RaceId forced))
            id = forced;
        else
            id = AshesToStars.RacePrefs.Get();

        var races = Resources.LoadAll<AshesToStars.RaceDef>("races");
        AshesToStars.RaceDef def = null;
        foreach (var r in races) if (r != null && r.Id == id) { def = r; break; }
        if (def == null)
        {
            Debug.LogWarning($"[W3] 종족 에셋을 못 찾음(id={id}, Resources/races {races.Length}종) — 배율 1로 진행");
            return;
        }
        _bHp *= def.체력배율;
        _bSpd *= def.이속배율;
        Debug.Log($"[W3] 종족={id} 체력×{def.체력배율:F2} 이속×{def.이속배율:F2} (누적 _bHp={_bHp:F2} _bSpd={_bSpd:F2})");
    }

    void BuildWorld()
    {
        var bank = SpriteBank.Load();
        // 던전 노드면 던전 바닥을 깐다(§3-4 — 아트 신규 0장, 기존 텍스처 재사용)
        GroundBuilder.Build(bank, Arena + 20f,
            DungeonRun.Active ? "ground/dungeon_rock_albedo" : "ground/field_plain_albedo");
        // 배경 프랍 — 전투 공간 **바깥 링**에만 깔린다(안쪽에 두면 유닛과 겹쳐 시야를 가린다).
        // 시드를 고정해 같은 판이면 같은 배치가 나오게 한다(측정 재현성).
        // ⚠️ 여기서 엄폐물을 켜지 마라 — Awake 시점엔 GameMode가 아직 false다(아래 645 주석과
        //    같은 함정). 게임 모드는 BattleScreen이 GameMode를 대입한 **뒤에**
        //    EnableFieldCover()로 명시적으로 켠다.
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
        _mDeadT = new float[MAXM];
        _mChargeT = new float[MAXM]; _mChargePhase = new int[MAXM]; _mChargeDir = new Vector2[MAXM];
        _mSummoned = new bool[MAXM];
        // 개체별 계열색을 **스폰 때 한 번 정해 기억한다.** 예전엔 피격 섬광이 끝날 때마다
        // `FamilyTint()`를 다시 불렀는데, 필드에서는 그 함수가 계열을 무작위로 굴리므로
        // **몹이 맞을 때마다 색이 바뀌어 깜빡였다**(오너 지적 2026-08-15, 내가 낸 버그).
        _mTint = new Color[MAXM]; _mFlip = new bool[MAXM];
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

    public static string FirstAdvancementRole(string job) => job switch
    {
        "수호기사" or "광전사" => "Tank",
        "사제" or "드루이드" => "Healer",
        "음유시인" or "주술사" or "정령사" => "Buffer",
        _ => "Dps",
    };

    static Role RoleOf(Job j) => System.Enum.Parse<Role>(FirstAdvancementRole(j.ToString()));

    public static float FirstAdvancementRange(string job) => job switch
    {
        "수호기사" => 1.5f, "광전사" => 1.8f, "검사" => 1.9f, "궁수" => 8.0f,
        "마법사" => 5.5f, "소환사" => 7.0f, "사제" => 6.5f, "드루이드" => 6.0f,
        "음유시인" => 6.0f, "주술사" => 6.0f, "정령사" => 6.5f, _ => 1.9f,
    };

    /// <summary>
    /// 근접 직업인가(§3). 근접은 투사체를 쏘지 않는다 —
    /// 예전엔 전 직업이 FireAlly로 탄을 날려 탱커·검사가 원거리처럼 보였다.
    /// </summary>
    static bool IsMelee(Job j) => j == Job.수호기사 || j == Job.광전사 || j == Job.검사;

    public static string FirstAdvancementMechanic(string job) => job switch
    {
        "수호기사" => "guard_gauge", "광전사" => "low_hp_rage", "검사" => "combo_stack",
        "궁수" => "stationary_focus", "마법사" => "density_aoe", "소환사" => "summon_slot",
        "사제" => "healing_faith", "드루이드" => "nature_mark", "음유시인" => "chant_swap",
        "주술사" => "curse_stack", "정령사" => "element_attach", _ => "swordsman_fallback",
    };

    public static string[] FirstAdvancementSkillLabels(string job) => job switch
    {
        "수호기사" => new[] { "도발의 함성", "성채 방패" },
        "광전사" => new[] { "광폭화", "대지 가르기" },
        "검사" => new[] { "일섬", "—" },
        "궁수" => new[] { "관통 사격", "집중 사격" },
        "마법사" => new[] { "화염폭풍", "—" },
        "소환사" => new[] { "소환수 돌격", "위치 교체" },
        "사제" => new[] { "치유의 파동", "기적" },
        "드루이드" => new[] { "자연 표식", "재생" },
        "음유시인" => new[] { "진군가", "수호가" },
        "주술사" => new[] { "저주 중첩", "쇠약 의식" },
        "정령사" => new[] { "화염 정령", "물 정령" },
        _ => new[] { "일섬", "연격" },
    };

    /// <summary>공격 연출. 근접은 타격만, 원거리는 탄을 날린다.</summary>
    void AttackFx(Member m, Vector2 targetPos)
    {
        m.AttackT = 0.26f;                       // 공격 애니메이션 재생 구간
        if (!IsMelee(m.Job)) FireAlly(m.Pos, targetPos);

        // 타격 이펙트 — 맞은 자리에 터진다. **초당 수십 번** 일어나므로 전부 뿌리면
        // 화면이 이펙트로 덮인다. 근접은 베는 궤적, 그 외는 스파크로 갈라 두 종류가
        // 섞이게 하고, 빈도를 솎아 "터지긴 하는데 시야는 살아 있는" 상태를 만든다.
        _fxTick++;
        if ((_fxTick & 1) == 0)
        {
            if (m.Job == Job.수호기사) FxPool.PlayJob(0, targetPos, 0.8f);
            else if (m.Job == Job.검사) FxPool.PlayJob(1, targetPos, 0.8f);
            else if (m.Job == Job.마법사) FxPool.PlayJob(2, targetPos, 0.9f);
            else if (m.Job == Job.사제) FxPool.PlayJob(3, targetPos, 0.85f);
            else if (m.Job == Job.음유시인) FxPool.PlayAtlas("bard_note", targetPos, 0.8f);
            else FxPool.Play(IsMelee(m.Job) ? FxPool.Kind.Slash : FxPool.Kind.Hit, targetPos, 0.8f);
        }
    }

    int _fxTick;    // 이펙트 솎아내기용 카운터

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
    /// <summary>
    /// 아트 선택. ⚠️ **역할이 아니라 직업으로** 고른다 — 역할로 고르면 딜러 둘
    /// (검사·마법사)이 같은 그림이 되어 화면에서 구분되지 않는다(오너 지적 2026-08-15).
    /// 파티 카드·전투 유닛이 같은 함수를 쓰므로 여기만 고치면 양쪽이 함께 갈린다.
    /// </summary>
    static SpriteBank.Job ArtOf(Job j) => j switch
    {
        Job.수호기사 or Job.광전사 => SpriteBank.Job.Tank,
        Job.마법사 => SpriteBank.Job.Mage,
        Job.사제 or Job.드루이드 => SpriteBank.Job.Healer,
        Job.음유시인 or Job.주술사 or Job.정령사 => SpriteBank.Job.Buffer,
        _ => SpriteBank.Job.Dps,
    };

    /// <summary>
    /// 이번 판의 시드. **구성은 시드에 섞지 않는다** — 반복 번호만 시드를 바꾼다.
    ///
    /// 예전에는 `구성index × 1000`이 시드에 들어가서, 다섯 구성이 **서로 다른 판**을 돌았다.
    /// 그러면 A와 D의 차이에 "구성 차이"와 "판 차이"가 섞여 무엇 때문인지 가를 수 없다.
    /// 같은 시드로 전 구성을 돌리면 스폰·AI 배정이 동일해져 **짝지은 비교**가 된다 —
    /// 같은 표본 수로 훨씬 작은 차이를 잡아낼 수 있다.
    /// (재실행하면 같은 값이 나와야 한다는 성질은 그대로 유지된다.)
    /// </summary>
    int RunSeed() => _seed + _rep;

    void NextStyle()
    {
        _qi++;
        // 게임 모드는 표준 5인(A) 한 판만 — 두 번째 구성으로 넘어가지 않는다
        if (GameMode && _qi > 0) return;
        if (_qi >= SETUPS.Length * _reps) { Finish(); return; }
        _rep = _qi % _reps;
        _shotThisRun = false;
        _setup = SETUPS[_qi / _reps];
        if (GameMode)
        {
            var partySetup = PartySetup();
            if (partySetup.HasValue) _setup = partySetup.Value;
        }
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
        // 필드 엄폐물은 그림이 라운드를 넘어 남는데 충돌 목록은 위 Clear/Build가 비운다.
        // 다시 등록하지 않으면 **보이는데 통과되는** 상태가 된다 — 엄폐가 조용히 거짓이 되고,
        // 이 저장소가 반복해서 겪은 「계산은 되는데 반영이 안 됨」의 거울상이다.
        AshesToStars.FieldDecor.RegisterCover();

        // 던전 강화를 배율로 환산한다. 던전 밖(필드 사냥·검증)에서는 전부 1이다.
        if (DungeonRun.Active)
            Boons.Multipliers(DungeonRun.State.Boons, out _bAtk, out _bHp, out _bSpd,
                              out _bCd, out _bHeal, out _bShield, out _bRange, out _bAtkSpd);

        // 종족 기울기(§3·§18-9)를 파티 전체 배율 버스에 접는다. `RaceDef`(Resources/races)를
        // 실제로 읽는 **유일한 런타임 소비처** — 이게 없으면 종족은 전투에 영향 0이다
        // (「정의는 있고 부르는 곳이 0곳」을 CombatStyleDef와 같은 방식으로 닫는다).
        // 전투에 걸리는 것은 체력·이속뿐이다(방어/경험치/드랍/영지는 전투 밖 계량이라 W3에 없다).
        // 인간=모든 배율 1 → 종족 도입 전과 완전히 동일하게 돌아 기존 측정을 깨지 않는다.
        ApplyRaceModifiers();

        // 구성에 맞춰 파티를 짠다
        for (int i = 0; i < 5; i++) _slots[i].Tr.gameObject.SetActive(false);
        _party = new Member[_setup.Jobs.Length];
        for (int i = 0; i < _setup.Jobs.Length; i++)
        {
            var m = _slots[i];
            var job = _setup.Jobs[i];
            m.Job = job;
            m.Advancement = _setup.Advancements != null && i < _setup.Advancements.Length
                ? _setup.Advancements[i]
                : AdvancementTier.First;
            m.Role = RoleOf(job);
            m.Sr.sprite = SpriteBank.Cached.Char(ArtOf(m.Job));
            m.MaxHp = (m.Role == Role.Tank ? 320f : m.Role == Role.Dps ? 130f : 150f) * _bHp;
            m.Atk = (m.Role == Role.Dps ? 26f : m.Role == Role.Tank ? 10f : m.Role == Role.Buffer ? 8f : 6f) * _bAtk;
            // 사거리는 **역할이 아니라 직업**으로 정한다(§3).
            // Role.Dps로 묶으면 검사(근접)와 마법사(원거리)가 같은 사거리를 갖게 되어
            // 검사가 멀찍이 서서 때리는 그림이 된다 — 오너 지적으로 발견.
            m.Range = FirstAdvancementRange(job.ToString());
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
            // 플레이어가 고른 스타일이 우선한다(§3, StyleScreen). `_style`은 W1~W3 **검증용**
            // 일괄 지정값이라, 그것만 쓰면 게임에서는 네 스타일 중 균형형 하나만 돌았다.
            // 검증 하네스(`SuppressTint`와 같은 계열의 `--out` 경로)는 지정값을 그대로 쓴다 —
            // 구성 비교는 전원이 같은 스타일이어야 성립하기 때문이다.
            m.Style = UseFixedStyle ? _style : StyleOf(m.Job);
            m.Hp = m.MaxHp;
            m.Threat = 0f; m.DeadT = 0f;
            m.Shield = 0f; m.Gauge = 0f; m.SkillCd = 0f; m.Chant = Chant.진군가;
            m.UltimateGauge = 0f; m.UltimateCd = 0f; m.ForceUltimate = false;
            // 진형 초기 배치: 탱 앞, 딜 중간, 힐 뒤 (§10-4가 이 진형을 유지시키는지 본다)
            // 진형: 탱 최전방 → 딜 중간 → 힐·버퍼 후열 (§10-4가 이 진형을 유지시키는지 관찰)
            m.Pos = new Vector2(i == 2 ? 1.2f : i == 4 ? -1.2f : 0f,
                                m.Role == Role.Tank ? 1.8f : m.Role == Role.Dps ? -0.4f : -2.6f);
            m.Tr.gameObject.SetActive(true);
        }
        for (int i = 0; i < MAXM; i++) { _mOn[i] = false; _mDeadT[i] = 0f; _mChargePhase[i] = 0; _mTr[i].gameObject.SetActive(false); }
        for (int i = 0; i < MAXP; i++) { _pOn[i] = false; _pTr[i].gameObject.SetActive(false); }
        if (_aOn != null) for (int i = 0; i < MAXP; i++) { _aOn[i] = false; _aTr[i].gameObject.SetActive(false); }
        _mAlive = 0;
        for (int i = 0; i < 시작웨이브; i++) SpawnMob();

        _t = 0f; _kills = 0; _tauntUses = 0; _backlineHits = 0; _frontlineHits = 0;
        _healsCast = 0; _healerDeadT = -1f; _shieldAbsorbed = 0f; _faithPeak = 0f; _supportHits = 0;
        _deadJobs.Clear();          // 판마다 새로 센다 — 안 비우면 구성 순회 때 누적된다
        _meleeHits = 0; _shotHits = 0; _framesThisRun = 0; _aiDashUses = 0; _chargeTells = 0; _chargeRushes = 0;
        _summonedAlive = 0; _summonDmgToParty = 0f; _summonHits = 0; _summonHbAt = 0f;
        // 실플레이 판만 보스 소환의 대상이 된다. 검증(W1~W3)은 GameMode가 아니라 여기서 빠진다 —
        // 측정 판에 소환이 끼면 구성 대조가 오염된다.
        _game = GameMode ? this : null;
        _tauntUntil = -1f; _tauntMember = null; _lastStandUntil = -1f; _lastStandCd = -1f; _partyChant = Chant.진군가;
        _qaAdvSlot1 = 0f; _qaAdvSlot2 = 0f; _qaAdvForced1 = false; _qaAdvForced2 = false;
        _qaUltimateEffect = 0f; _qaUltimateForced = false;
        for (int k = 0; k < _skillLog.Length; k++) _skillLog[k] = 0;
        Debug.Log($"[W3] 구성 {_setup.Name} 시작 ({_party.Length}인, 도발 {(_tauntEnabled ? "ON" : "OFF")}, "
                  + $"스킬 [{string.Join(",", System.Array.ConvertAll(_party, m => m.SkillCount))}])");
    }

    void SpawnMob(bool summoned = false)
    {
        for (int i = 0; i < MAXM; i++)
        {
            // 아직 사망 모션이 도는 슬롯을 재사용하면 **시체가 그 자리에서 되살아난다.**
            if (_mOn[i] || _mDeadT[i] > 0f) continue;
            float a = Random.value * Mathf.PI * 2f;
            _mPos[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(Arena * 0.6f, Arena);
            int r = Random.Range(0, 100);
            // §10-2 AI 4종 + 정예 2종.
            //   0 추적형 30% · 1 포위형 20% · 5 돌진형 25% · 2 원거리형 15% · 3/4 정예 10%
            // 원거리형은 §10-2가 정한 **15~25%** 안에 둔다(많으면 접근 자체가 불가능해진다).
            // 돌진형은 이번에 신설했다 — 「예고 표식 후 직선 돌진」이 §10-2의 4종 중
            // 유일하게 **없던** 것이고, 대시(§5)를 넣은 지금이라야 회피가 성립한다.
            _mKind[i] = r < 30 ? 0 : r < 50 ? 1 : r < 75 ? 5 : r < 90 ? 2 : r < 96 ? 3 : 4;
            // 보스 소환 몹은 **근접 돌격형(kind 1)으로 고정**한다. 원거리(kind 2)로 나오면
            // 탄으로 때려 피해 귀속 지점이 갈라지고, 무엇보다 「달려드는 쫄」이라는 소환 기믹의
            // 뜻과도 어긋난다. 근접으로 고정하면 파티 피해가 근접 판정 한 곳에서 온전히 잡힌다.
            if (summoned)
            {
                _mKind[i] = 1;
                // §10-5 「분리 소환」은 파티 **한복판에** 나타나는 것이 요점이다. 아레나 가장자리에
                // 스폰하면 달려오는 도중 전부 포커싱돼 죽어 위협이 안 된다(실측: 소환피해 0).
                // 파티원 곁(근접 사거리 안)에 꽂아 즉시 후열을 물게 한다.
                Vector2 anchor = _party != null && _party.Length > 0
                    ? _party[Random.Range(0, _party.Length)].Pos : Vector2.zero;
                // 근접 사거리(1.1) 안에 확실히 들도록 바싹 붙여 꽂는다 — 멀면 포위형 선회로
                // 사거리 밖을 맴돌다 첫 타 전에 죽어 위협이 안 잡힌다(실측: 0.8 오프셋은 3마리 중 1마리만 명중).
                _mPos[i] = anchor + new Vector2(Random.Range(-0.35f, 0.35f), Random.Range(-0.35f, 0.35f));
            }
            _mSummoned[i] = summoned;
            if (summoned) _summonedAlive++;
            // 소환 몹은 잡몹보다 훨씬 단단하게(220) — 파티 한복판에 떨어지면 순식간에 포커싱되므로,
            // 즉사하면 「달려들어 후열을 문다」는 위협이 성립하지 않는다(실측: 90도 첫 타 전에 죽었다).
            _mHp[i] = _mMaxHp[i] = summoned ? 220f : _mKind[i] >= 3 ? 90f : 26f;
            _mCd[i] = Random.value * 2f;
            // 소환 몹은 사거리 안에 나타나므로 **곧바로** 한 대 문다 — 예고 후 대응할 수 있는 위협이 된다.
            _mAtkCd[i] = summoned ? 0.15f : Random.value * 0.8f; _mFlash[i] = 0f;
            // 슬롯 재사용 — 앞 개체의 돌진 단계가 남아 있으면 **스폰하자마자 돌진**한다
            _mChargePhase[i] = 0; _mChargeT[i] = 0f;
            _mOn[i] = true; _mAlive++;
            // 스폰 그림과 애니메이션이 **같은 규칙**을 쓴다(MobSpriteKind가 단일 소스).
            // 예전엔 여기만 종류별이고 애니메이션은 0번 고정이라, 몹이 걷기 시작하는
            // 순간 그림이 바뀌었다.
            _mSr[i].sprite = SpriteBank.Cached.MobAnim(MobSpriteKind(_mKind[i]), SpriteBank.Motion.Idle, 0f);
            // 정예는 **역할 색이 먼저**다(치유·소환은 즉시 알아봐야 하는 대상이다).
            // 그 외 잡몹만 계열 색조를 입는다(§10-3).
            // 이 개체의 색을 **여기서 한 번** 정해 기억한다. 섬광 복원이 매번 다시 굴리면
            // 맞을 때마다 색이 바뀌어 깜빡인다.
            // 색은 종류에서 결정된다 — 정예 예외 분기를 여기 두면 두 곳이 어긋난다.
            // (실제로 어긋나 있었다: 정예 초록이 언데드 초록과 거의 같은 색이었다)
            _mTint[i] = FamilyTint(_mKind[i]);
            _mSr[i].color = _mTint[i];
            // 정예만 조금 크게. 기본 크기는 SpriteBank가 실측 PPU로 맞춰 둔다.
            _mTr[i].localScale = Vector3.one * (_mKind[i] >= 3 ? 1.4f : 1.0f);
            _mBarBg[i].gameObject.SetActive(false);      // 다시 스폰됐으니 만피 — 바 숨김
            _mBarFg[i].gameObject.SetActive(false);
            _mTr[i].gameObject.SetActive(true);
            return;
        }
    }

    // ── 보스 쫄 소환 정적 통로(§10-5) ─────────────────────
    // BossBattle이 W3Party 인스턴스를 참조하지 않아도 되게 정적 진입을 둔다(회복/피해 보고와
    // 같은 idiom). 실플레이 판(_game)이 없으면 조용히 무시 — 검증 하네스는 영향받지 않는다.

    /// <summary>지금 도는 실플레이 판에 쫄 n마리를 소환한다.</summary>
    public static void SummonMobsToActive(int n)
    {
        if (_game != null && _game.GameMode) _game.SummonMobs(n);
    }

    /// <summary>살아 있는 소환 몹 수. 보스가 「소환이 정리됐나」를 판정해 기믹 슬롯을 푼다.</summary>
    public static int SummonedAliveOnActive() => _game != null ? _game._summonedAlive : 0;

    /// <summary>소환 몹이 파티에 준 누적 피해. 네거티브 컨트롤의 측정값(QA 로그로 읽는다).</summary>
    public static float SummonDmgOnActive() => _game != null ? _game._summonDmgToParty : 0f;

    /// <summary>
    /// AI가 이동기를 쓴 횟수. **판이 끝나야 찍히는 로그로는 QA가 못 본다** —
    /// 시각 QA는 지정 초에 캡처하고 즉시 종료하므로 판 종료 로그를 영영 안 만난다
    /// (실측 2026-08-15: `[W3-DIAG]`에만 넣었더니 출력이 0줄이었다).
    /// 그래서 캡처 시점에 밖에서 읽어갈 수 있게 연다.
    /// </summary>
    public static int AiDashUsesOnActive() => _game != null ? _game._aiDashUses : -1;

    void SummonMobs(int n)
    {
        for (int k = 0; k < n; k++) SpawnMob(true);
        Debug.Log($"[W3] 보스 소환 — 쫄 {n}마리 (현재 소환 생존 {_summonedAlive})");
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

    // ── 스킬 발동 연출 (오너 지시 2026-08-15 「스킬 사용 시 사용했다는 느낌이 났으면」) ──
    //
    // 지금까지 스킬은 **수치로만** 존재했다. 파티가 잠깐 밝아지고 파티클이 조금 튀는 게
    // 전부라, 도발이 나갔는지 성채 방패가 나갔는지 화면에서 구분되지 않았다.
    // 바닥 범위 원은 오너가 지우라고 한 것이라 그 방향은 막혀 있다(§6-A).
    //
    // 그래서 셋을 겹친다. 하나만으로는 "느낌"이 안 난다:
    //   ① **이름을 띄운다** — 무엇이 터졌는지 읽히는 유일한 신호다
    //   ② **시간을 멈춘다**(히트스톱) — 큰 것일수록 길게. 타격감은 대부분 여기서 온다
    //   ③ **화면을 흔든다** — 공격 계열만. 힐·보호막까지 흔들면 다급함이 뭉개진다
    struct Callout { public string Text; public Vector2 At; public float T; public Color C; }
    readonly System.Collections.Generic.List<Callout> _callouts = new();
    const float CALLOUT_LIFE = 1.1f;

    /// <summary>스킬 이름을 시전자 머리 위에 띄운다. 색으로 계열을 구분한다.</summary>
    void SkillCast(Member m, string name, Color tint, int hitstop = 0, float shake = 0f)
    {
        m.SkillT = Mathf.Max(m.SkillT, 0.5f);          // 큰 동작을 확실히 재생
        m.FlashT = Mathf.Max(m.FlashT, 0.14f);         // 시전자 자신을 번쩍
        if (GameMode && _callouts.Count < 12)          // 난전에 12개 넘게 뜨면 글자밭이 된다
            _callouts.Add(new Callout { Text = name, At = m.Pos, T = 0f, C = tint });
        if (hitstop > 0) Hitstop(hitstop);
        if (shake > 0f) Shake(shake);
    }

    void UpdateCallouts(float dt)
    {
        for (int i = _callouts.Count - 1; i >= 0; i--)
        {
            var c = _callouts[i];
            c.T += dt;
            if (c.T >= CALLOUT_LIFE) _callouts.RemoveAt(i);
            else _callouts[i] = c;
        }
    }

    /// <summary>
    /// 떠오르는 스킬 이름. IMGUI로 그린다 — 이 프로젝트에는 uGUI가 없고(§GameFlow 주석),
    /// 월드 좌표를 카메라로 화면 좌표에 옮겨 그 자리에 찍는다.
    /// ⚠️ `GUI.matrix`가 걸린 구간에서 부르지 마라. 여기 좌표는 **실제 픽셀**이다.
    /// </summary>
    void DrawCallouts()
    {
        if (_callouts.Count == 0) return;
        var cam = Camera.main;
        if (cam == null) return;

        _calloutStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
        };

        foreach (var c in _callouts)
        {
            float u = c.T / CALLOUT_LIFE;
            var sp = cam.WorldToScreenPoint(new Vector3(c.At.x, c.At.y * ISO_Y, 0f));
            if (sp.z < 0f) continue;                                  // 카메라 뒤
            float y = Screen.height - sp.y - 46f - u * 34f;           // 위로 떠오른다
            var r = new Rect(sp.x - 90f, y, 180f, 26f);

            // 알파는 뒤쪽 절반에서만 뺀다 — 처음부터 흐려지면 읽기도 전에 사라진다
            float a = u < 0.55f ? 1f : 1f - (u - 0.55f) / 0.45f;

            _calloutStyle.normal.textColor = new Color(0f, 0f, 0f, a * 0.8f);
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), c.Text, _calloutStyle);  // 그림자
            _calloutStyle.normal.textColor = new Color(c.C.r, c.C.g, c.C.b, a);
            GUI.Label(r, c.Text, _calloutStyle);
        }
    }

    GUIStyle _calloutStyle;

    CameraFollow _cam;

    /// <summary>
    /// 화면 흔들림 세기 배율. **호출부 숫자를 하나씩 고치지 마라** — 여기 하나만 만진다.
    ///
    /// 오너 지적 2026-08-15 「흔들림 너무 커, 좀 줄여줘」. 호출부가 여섯 곳이라 각각을
    /// 고치면 다음에 또 조정할 때 어디를 봐야 하는지 알 수 없고, 한둘을 빠뜨려 세기가
    /// 제각각이 된다. 상대 비율(기적이 일섬보다 크다)은 호출부가 정하고, **전체 크기는
    /// 이 값 하나가 정한다.**
    /// </summary>
    const float SHAKE_SCALE = 0.35f;

    void Shake(float amp)
    {
        if (!GameMode) return;                     // 측정 실행의 스크린샷 검증을 흔들지 않는다
        _cam ??= Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
        if (_cam != null) _cam.Shake(amp * SHAKE_SCALE);
    }

    void Update()
    {
        // 실플레이 판을 보스 소환의 대상으로 등록한다. **여기서** 잡는다 —
        // NextStyle은 Awake에서 도는데 그때 GameMode는 아직 false다(BattleScreen이 AddComponent
        // 뒤에 대입하는 알려진 함정). Awake 시점에 잡으면 _game이 null로 굳어 소환이 조용히 샌다.
        if (GameMode) _game = this;

        if (_hitstop > 0) { _hitstop--; _stepAcc = 0f; return; }

        // 프레임이 얼마나 걸렸든 1/60 단위로만 진행한다(한 프레임 최대 10스텝 — 스파이럴 방지)
        _stepAcc += Mathf.Min(Time.deltaTime, 0.25f);
        int steps = 0;
        while (_stepAcc >= FixedStep && steps < 10) { _stepAcc -= FixedStep; steps++; Step(); }
    }

    float _summonHbAt;
    void Step()
    {
        float dt = FixedStep;
        _t += dt; _framesThisRun++;

        // 보스 소환 배선의 측정 증거 — 캡처 경로(프레임/초 지정)와 무관하게 Player.log에 남긴다.
        // 실플레이 판에서만, 10초마다 소환 생존·누적 피해를 찍는다(네거티브 컨트롤로 대조).
        if (GameMode && _t >= _summonHbAt)
        {
            _summonHbAt = _t + 10f;
            Debug.Log($"[QA] t={_t:F0}s 소환생존={_summonedAlive} 소환피해={_summonDmgToParty:F0}");
        }
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
        if (GameMode && !string.IsNullOrEmpty(QaFirstAdvancementJob))
        {
            foreach (var member in _party)
            {
                if (!member.Alive || member.Job.ToString() != QaFirstAdvancementJob) continue;
                if (!_qaAdvForced1 && _t >= 1f) { if (QaAdvancementDisabledSlot != 1) member.ForceSkill = 1; _qaAdvForced1 = true; }
                else if (!_qaAdvForced2 && _t >= 3f) { if (QaAdvancementDisabledSlot != 2) member.ForceSkill = 2; _qaAdvForced2 = true; }
                break;
            }
        }
        if (GameMode && !string.IsNullOrEmpty(QaSecondAdvancementJob) && !_qaUltimateForced && _t >= 1f)
        {
            foreach (var member in _party)
            {
                if (!member.Alive || member.Job.ToString() != QaSecondAdvancementJob) continue;
                member.UltimateGauge = QaUltimateBlock == 2 ? 99f : 100f;
                member.UltimateCd = QaUltimateBlock == 3 ? 10f : 0f;
                member.ForceUltimate = true;
                _sel = System.Array.IndexOf(_party, member); // 캡처에 초필 버튼·게이지를 남긴다
                _qaUltimateForced = true;
                break;
            }
        }
        TickDashProbe(dt);
        UpdateCallouts(dt);
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

        // ⛔ 바닥에 까는 **범위 링 표시를 전부 없앴다**(오너 지시 2026-08-15, 네 번 지적).
        //    도발·화염폭풍의 사거리를 바닥 원으로 보여주던 것인데, 원본이 절차 생성
        //    흰 원이든 실제 이펙트 아트든 **바닥에 큰 원이 깔린다는 것 자체가 문제**였다.
        //    스킬이 도는 것은 몹의 반응(끌려오는 것·죽는 것)과 시전자 동작으로 읽힌다.
        //    **다시 넣지 마라.** 범위를 보여주고 싶으면 원이 아닌 방식을 찾을 것.

        for (int mi = 0; mi < _party.Length; mi++)
        {
            var m = _party[mi];
            TickAiDash(m, mi, dt);
            if (m.AttackT > 0f) m.AttackT -= dt;
            if (m.HurtT > 0f) m.HurtT -= dt;
            if (m.SkillT > 0f) m.SkillT -= dt;

            // ── 이동기(§5) ──
            if (m.DashCd > 0f) m.DashCd -= dt;
            if (m.IFrame > 0f) m.IFrame -= dt;
            if (m.DashT > 0f)
            {
                m.DashT -= dt;
                // 거리 = 기본 이동 3초분(기획서). `DASH_TIME` 동안 그만큼을 나눠 간다.
                m.Pos += m.DashDir * (PlayerSpeed * 3f / DASH_TIME) * dt;
                if (ArenaLayout.Any) m.Pos = ArenaLayout.Resolve(m.Pos);
                m.Pos = Vector2.ClampMagnitude(m.Pos, Arena);   // 아레나 밖으로 못 빠져나간다
            }

            // 동작 우선순위: 사망 > 피격 > **스킬** > 공격 > 이동 > 대기.
            // 스킬을 공격보다 위에 둔다 — 스킬을 쓰는 순간에도 평타 판정이 같이 돌기 때문에
            // 아래에 두면 큰 동작이 평타 프레임에 묻혀 화면에서 구분되지 않는다.
            // ⚠️ 대시가 **피격·스킬보다 위**다. 이동기는 0.18초짜리 짧은 동작이라
            //    아래에 두면 다른 상태에 묻혀 한 프레임도 안 보인다.
            //    `dash_00~03`·`invuln_00` 아트와 `Motion.Dash`는 처음부터 있었는데
            //    **부르는 곳만 없었다**(2026-08-15 전수 조사에서 발견) — 대시를 구현하면서도
            //    그 아트를 안 쓰고 있었다.
            var want = !m.Alive ? SpriteBank.Motion.Death
                     : m.DashT > 0f ? SpriteBank.Motion.Dash
                     : m.HurtT > 0f ? SpriteBank.Motion.Hurt
                     : m.SkillT > 0f ? SpriteBank.Motion.Special
                     : m.AttackT > 0f ? SpriteBank.Motion.Attack
                     : (m.Pos - m.PrevPos).sqrMagnitude > 1e-5f ? SpriteBank.Motion.Walk
                     : SpriteBank.Motion.Idle;

            if (want != m.Mo) { m.Mo = want; m.AnimT = 0f; }
            else m.AnimT += dt;

            m.Sr.sprite = bank.CharAnim(ArtOf(m.Job), m.Mo, m.AnimT);
            // 무적 구간 표시 — 대시가 끝나고도 0.12초쯤 남는 무적을 화면에서 알 수 있게.
            // 「정확히 쓰면 한 번 산다」(§5)가 성립하려면 **무적이 켜져 있다는 것이 보여야** 한다.
            if (m.IFrame > 0f && m.DashT <= 0f)
                m.Sr.sprite = bank.Char(ArtOf(m.Job), SpriteBank.Frame.Invuln);
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
            // 몹과 같은 이유로 이력을 둔다 — `1e-4`면 정지 중 미세 흔들림에도 뒤집힌다.
            float mdx = m.Pos.x - m.PrevPos.x;
            if (Mathf.Abs(mdx) > 0.02f) m.Flip = mdx < 0f;
            m.Sr.flipX = m.Flip;
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
            // 죽어가는 몹 — 사망 모션을 끝까지 돌린 뒤에 치운다.
            // `_mOn`이 false라 아래 루프들(AI·타겟팅)은 이미 이 개체를 안 본다.
            if (_mDeadT[i] > 0f)
            {
                _mDeadT[i] += dt;
                if (_mDeadT[i] >= DEATH_ANIM_SEC)
                {
                    _mDeadT[i] = 0f;
                    _mTr[i].gameObject.SetActive(false);
                }
                else
                {
                    _mSr[i].sprite = bank.MobAnim(MobSpriteKind(_mKind[i]),
                                                  SpriteBank.Motion.Death, _mDeadT[i]);
                    _mSr[i].sortingOrder = Depth(_mPos[i].y) - 1;   // 시체는 산 것보다 뒤로
                }
                continue;
            }

            if (!_mOn[i]) continue;

            // 몹 애니메이션. 개체마다 시간을 어긋나게 줘야 100마리가 같은 프레임으로
            // 군무를 추지 않는다 — 물량이 많을수록 동기화가 눈에 띈다(§10-2).
            var mm = _mFlash[i] > 0f ? SpriteBank.Motion.Hurt : SpriteBank.Motion.Walk;
            _mSr[i].sprite = bank.MobAnim(MobSpriteKind(_mKind[i]), mm, _t + i * 0.37f);
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
            if (Input.GetKeyDown(KeyCode.E)) _party[_sel].ForceUltimate = true;
            // 이동기(§5) — Shift. 방향은 이동 입력, 없으면 가장 가까운 위협의 **반대쪽**으로.
            // "피하려고 쓰는 것"이 기본 용도라 아무 방향도 안 주면 도망 방향이 맞다.
            if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
            {
                var me = _party[_sel];
                var dir = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
                if (dir.sqrMagnitude < 1e-4f)
                {
                    int near = NearestMob(me.Pos, 6f);
                    if (near >= 0) dir = me.Pos - _mPos[near];
                }
                TryDash(me, dir);
            }
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
        Member tank = null;
        foreach (var candidate in _party)
            if (candidate.Alive && candidate.Role == Role.Tank && (tank == null || candidate.Job == Job.수호기사))
            { tank = candidate; if (candidate.Job == Job.수호기사) break; }
        if (tank == null) tank = _party[0];

        foreach (var m in _party)
        {
            if (!m.Alive) continue;
            m.Cd -= dt; m.SkillCd -= dt; m.UltimateCd -= dt;
            if (m.Advancement == AdvancementTier.Second)
                m.UltimateGauge = Mathf.Min(100f, m.UltimateGauge + dt);
            if (m.ForceUltimate)
            {
                m.ForceUltimate = false;
                if (CanUseUltimate(m.Advancement, m.UltimateGauge, m.UltimateCd))
                    CastUltimate(m);
            }
            // 위협 감쇠는 **비율**이어야 한다. 선형 −2/s인데 검사의 생성률이 40/s 이상이라
            // 위협이 발산했다 — 100초면 4,000, 거리 환산 480유닛인데 아레나 반경은 14다.
            // 즉 10초쯤부터 모든 근접 몹이 위치와 무관하게 딜러에게 갔고,
            // 도발(+80 = 9.6유닛)은 그걸 뒤집을 수 없었다.
            // τ=8초(도발 쿨 6초의 1.33배)로 정상상태를 만든다 — 도발이 항상 유효하게.
            m.Threat *= Mathf.Exp(-dt / 8f);

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

            if (m.Job == Job.수호기사)
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
                    _tauntUntil = _t + 3.0f; _tauntMember = m;    // 3초간 원거리도 시전자를 노린다
                    _skillLog[0]++;
                    SkillCast(m, "도발의 함성", new Color(1f, 0.78f, 0.32f), hitstop: 2);
                    FxParticles.Play(FxKind.쇼크웨이브, ToScreen(m.Pos), 1.8f,
                                     new Color(1f, 0.78f, 0.32f));
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
                    _skillLog[1]++;
                    SkillCast(m, "성채 방패", new Color(0.55f, 0.82f, 1f), hitstop: 2);
                    FxPool.PlayJob(4, m.Pos, 1.6f);
                    // 보호막은 **파티 전체**에 걸리는데 연출이 시전자에만 있으면
                    // 누가 보호받는지 화면에서 안 읽힌다.
                    foreach (var o in _party)
                        if (o.Alive) FxParticles.Play(FxKind.광륜, ToScreen(o.Pos), 0.7f,
                                                      new Color(0.55f, 0.82f, 1f));
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
                    case Job.광전사:
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
                    case Job.궁수:
                    case Job.마법사:
                    case Job.소환사:
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
                    case Job.드루이드:
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
            if (m.Advancement == AdvancementTier.Basic && m.Job == Job.음유시인)
            {
                // 기본 버퍼 2스킬: 고양은 현재 악장을 공격형으로, 쇠약은 가까운 적의 다음 행동을 늦춘다.
                if (m.ForceSkill == 1)
                {
                    m.ForceSkill = 0; _partyChant = Chant.진군가; m.Cd = 1.0f;
                    SkillCast(m, "고양", new Color(1f, 0.82f, 0.35f), hitstop: 1);
                    FlashParty();
                }
                else if (m.ForceSkill == 2)
                {
                    m.ForceSkill = 0;
                    int near = NearestMob(m.Pos, 7f);
                    if (near >= 0) _mAtkCd[near] = Mathf.Max(_mAtkCd[near], 2.0f);
                    m.Cd = 1.0f;
                    SkillCast(m, "쇠약", new Color(0.72f, 0.55f, 1f), hitstop: 1);
                }
                else m.Cd = 0.7f;
            }
            else if (m.Job == Job.음유시인)
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
                FxPool.PlayJob(5, m.Pos, 1.6f);             // 새 음유시인 오라 8프레임
                m.Cd = 0.8f; m.Threat += 3f;
            }
            else if (m.Advancement == AdvancementTier.Basic && m.Job == Job.사제)
            {
                Member worst = null;
                foreach (var o in _party)
                    if (o.Alive && (worst == null || o.Hp / o.MaxHp < worst.Hp / worst.MaxHp)) worst = o;
                if (m.ForceSkill == 2)
                {
                    m.ForceSkill = 0;
                    if (worst != null) worst.Shield = Mathf.Max(worst.Shield, 18f * _bShield);
                    SkillCast(m, "정화", new Color(0.7f, 1f, 0.9f), hitstop: 1);
                }
                else if (worst != null && (m.ForceSkill == 1 || worst.Hp < worst.MaxHp))
                {
                    m.ForceSkill = 0;
                    Heal(worst, 22f * _bHeal); _healsCast++;
                    SkillCast(m, "치유", new Color(0.55f, 1f, 0.65f), hitstop: 1);
                }
                m.Cd = 1.0f;
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
                    FxPool.PlayJob(3, m.Pos, 2.2f);
                    FxParticles.Play(FxKind.기적, ToScreen(m.Pos), 1.5f); FxParticles.Play(FxKind.광륜, ToScreen(m.Pos), 1.6f);
                    SkillCast(m, "기적", new Color(1f, 0.95f, 0.6f), hitstop: 5, shake: 0.45f);
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
                    SkillCast(m, "치유 파동", new Color(0.55f, 1f, 0.65f), hitstop: 1);
                    FxPool.PlayJob(3, m.Pos, 1.4f);
                }
                else if (worst != null && worst.Hp / worst.MaxHp < 0.85f)
                {
                    m.Gauge += Heal(worst, 24f * sp.DmgMul * _bHeal);
                    m.Cd = 1.0f; m.Threat += 8f; _healsCast++;
                }
                m.Gauge = Mathf.Min(100f, m.Gauge);
                if (m.Gauge > _faithPeak) _faithPeak = m.Gauge;
            }
            else if (m.Job == Job.광전사 && target >= 0)
            {
                // 분노 — HP가 낮을수록 공격력이 상승한다. 두 번째 슬롯은 근처 적까지 가른다.
                float rage = Mathf.Lerp(1.8f, 1f, m.Hp / m.MaxHp);
                float dmg = m.Atk * rage;
                if (m.ForceSkill == 1) { m.ForceSkill = 0; dmg *= 1.8f; _qaAdvSlot1 += dmg; SkillCast(m, "광폭화", Color.red, 2, 0.2f); }
                if (m.ForceSkill == 2)
                {
                    m.ForceSkill = 0;
                    for (int j = 0; j < MAXM; j++)
                        if (j != target && _mOn[j] && (_mPos[j] - m.Pos).sqrMagnitude < 6.25f)
                        { _mHp[j] -= dmg; _qaAdvSlot2 += 1f; if (_mHp[j] <= 0f) KillMob(j); }
                    SkillCast(m, "대지 가르기", new Color(1f, 0.5f, 0.25f), 3, 0.25f);
                }
                _mHp[target] -= dmg * sp.DmgMul * ChantAtk(); m.Cd = 0.5f / _bAtkSpd;
                AttackFx(m, _mPos[target]); FlashMob(target); if (_mHp[target] <= 0f) KillMob(target);
            }
            else if (m.Job == Job.궁수 && target >= 0)
            {
                // 집중 — 제자리에 서서 쏠수록 강해지고, 이동/백스텝 대신 슬롯2로 즉시 집중한다.
                if (m.ForceSkill == 2) { m.ForceSkill = 0; m.FocusUntil = _t + 4f; _qaAdvSlot2 += 4f; SkillCast(m, "집중 사격", Color.yellow, 1); }
                float dmg = m.Atk * (_t < m.FocusUntil ? 1.6f : 1f);
                if (m.ForceSkill == 1) { m.ForceSkill = 0; dmg *= 2.4f; _qaAdvSlot1 += dmg; SkillCast(m, "관통 사격", Color.white, 2); }
                _mHp[target] -= dmg * sp.DmgMul * ChantAtk(); m.Cd = 0.55f / _bAtkSpd;
                AttackFx(m, _mPos[target]); FlashMob(target); if (_mHp[target] <= 0f) KillMob(target);
            }
            else if (m.Job == Job.소환사 && target >= 0)
            {
                // 소환 슬롯 — 본체의 매 공격 사이에 소환수가 같은 표적을 추가 타격한다.
                m.Gauge = Mathf.Min(3f, m.Gauge + 1f);
                float summonDmg = m.Atk * (m.Gauge >= 3f ? 1.8f : 0.8f);
                if (m.ForceSkill == 1) { m.ForceSkill = 0; summonDmg *= 2f; _qaAdvSlot1 += summonDmg; m.Gauge = 0f; SkillCast(m, "소환수 돌격", Color.cyan, 2); }
                if (m.ForceSkill == 2)
                {
                    m.ForceSkill = 0;
                    Member swap = null;
                    foreach (var o in _party) if (o != m && o.Alive && (swap == null || (o.Pos - m.Pos).sqrMagnitude < (swap.Pos - m.Pos).sqrMagnitude)) swap = o;
                    if (swap != null) { Vector2 old = m.Pos; m.Pos = swap.Pos; swap.Pos = old; _qaAdvSlot2 += 1f; }
                    m.IFrame = Mathf.Max(m.IFrame, 0.8f); SkillCast(m, "위치 교체", Color.cyan, 1);
                }
                _mHp[target] -= summonDmg * sp.DmgMul * ChantAtk(); m.Cd = 0.7f;
                FireAlly(m.Pos, _mPos[target]); FlashMob(target); if (_mHp[target] <= 0f) KillMob(target);
            }
            else if (m.Job == Job.드루이드)
            {
                // 자연 표식 — 적 피해와 아군 재생을 한 행동에서 함께 만든다.
                Member worst = null;
                foreach (var o in _party) if (o.Alive && (worst == null || o.Hp / o.MaxHp < worst.Hp / worst.MaxHp)) worst = o;
                if (worst != null) { float healed = Heal(worst, (m.ForceSkill == 2 ? 28f : 10f) * _bHeal); if (m.ForceSkill == 2) _qaAdvSlot2 += healed; }
                if (target >= 0) { float dealt = m.Atk * (m.ForceSkill == 1 ? 2f : 1f); _mHp[target] -= dealt; if (m.ForceSkill == 1) _qaAdvSlot1 += dealt; FlashMob(target); if (_mHp[target] <= 0f) KillMob(target); }
                SkillCast(m, m.ForceSkill == 2 ? "재생" : "자연 표식", Color.green, 1); m.ForceSkill = 0; m.Cd = 1.0f;
            }
            else if (m.Job == Job.주술사 && target >= 0)
            {
                // 저주 스택 — 같은 대상에 반복할수록 증폭한다(프로토타입은 시전자 게이지로 계량).
                m.Gauge = Mathf.Min(5f, m.Gauge + 1f); float dmg = m.Atk * (1f + m.Gauge * 0.2f);
                if (m.ForceSkill == 1) { m.ForceSkill = 0; m.Gauge = 5f; _qaAdvSlot1 += m.Gauge; SkillCast(m, "저주 중첩", Color.magenta, 1); }
                if (m.ForceSkill == 2) { m.ForceSkill = 0; _mAtkCd[target] = Mathf.Max(_mAtkCd[target], 2.5f); _qaAdvSlot2 += 2.5f; SkillCast(m, "쇠약 의식", Color.magenta, 1); }
                _mHp[target] -= dmg; m.Cd = 0.8f; AttackFx(m, _mPos[target]); if (_mHp[target] <= 0f) KillMob(target);
            }
            else if (m.Job == Job.정령사)
            {
                // 정령 계약 — 화염은 아군 공격 템포, 물은 보호막으로 읽히는 부착 효과다.
                if (m.ForceSkill == 2)
                {
                    m.ForceSkill = 0; foreach (var o in _party) if (o.Alive) { float before = o.Shield; o.Shield = Mathf.Max(o.Shield, 20f * _bShield); _qaAdvSlot2 += o.Shield - before; }
                    SkillCast(m, "물 정령", Color.cyan, 1);
                }
                else
                {
                    bool forced = m.ForceSkill == 1;
                    m.ForceSkill = 0; foreach (var o in _party) if (o.Alive) { o.Cd = Mathf.Min(o.Cd, 0.15f); if (forced) _qaAdvSlot1 += 1f; }
                    SkillCast(m, "화염 정령", new Color(1f, 0.45f, 0.2f), 1);
                }
                m.Cd = 1.2f;
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
                    // 화염폭풍은 **범위 원 대신** 불길 자체를 터뜨려 보여준다(바닥 링은 삭제됨).
                    FxPool.Play(FxPool.Kind.Fire, c, _stormR * 0.9f);
                    FxParticles.Play(FxKind.마법진, ToScreen(c), _stormR); FxParticles.Play(FxKind.화염폭풍, ToScreen(c), _stormR);
                    SkillCast(m, "화염 폭풍", new Color(1f, 0.55f, 0.25f), hitstop: 3, shake: 0.28f);
                    FxParticles.Play(FxKind.쇼크웨이브, ToScreen(c), _stormR * 0.8f,
                                     new Color(1f, 0.6f, 0.25f));
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
            else if (m.Advancement == AdvancementTier.Basic && m.Job == Job.검사 && target >= 0)
            {
                if (m.ForceSkill == 2)
                {
                    m.ForceSkill = 0; m.FocusUntil = _t + 3f;
                    SkillCast(m, "집중", new Color(1f, 0.85f, 0.4f), hitstop: 1);
                }
                float dmg = m.Atk;
                if (m.ForceSkill == 1)
                {
                    m.ForceSkill = 0; dmg *= 2.2f;
                    SkillCast(m, "강타", new Color(1f, 0.65f, 0.35f), hitstop: 2, shake: 0.15f);
                }
                _mHp[target] -= dmg * sp.DmgMul * ChantAtk();
                m.Cd = (_t < m.FocusUntil ? 0.22f : 0.40f) / _bAtkSpd;
                m.Threat += dmg * 0.4f;
                AttackFx(m, _mPos[target]); FlashMob(target);
                if (_mHp[target] <= 0f) KillMob(target);
            }
            else if (m.Job == Job.검사 && target >= 0)
            {
                // 연격 스택 → 일섬 (§3 고유 메커니즘)
                m.Gauge += 1f;
                float dmg = m.Atk;
                // 일섬 — 버튼(슬롯1)이면 스택이 덜 찼어도 즉시 터뜨린다(§3 "스택 전량 소모")
                if (m.ForceSkill == 1) { m.ForceSkill = 0; m.Gauge = 0f; dmg = m.Atk * 3.2f; _skillLog[6]++; FxParticles.Play(FxKind.일섬, ToScreen(_mPos[target]));
                    SkillCast(m, "일섬", new Color(0.85f, 0.95f, 1f), hitstop: 3, shake: 0.2f);
                    FxPool.Play(FxPool.Kind.Slash, _mPos[target], 1.5f); }
                else if (m.Gauge >= 5f) { m.Gauge = 0f; dmg = m.Atk * 3.2f; _skillLog[6]++; FxParticles.Play(FxKind.일섬, ToScreen(_mPos[target]));
                    SkillCast(m, "일섬", new Color(0.85f, 0.95f, 1f), hitstop: 3, shake: 0.2f);
                    FxPool.Play(FxPool.Kind.Slash, _mPos[target], 1.5f); }
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

    // ── 이동기 수치 (§5 ✅ 오너 결정 2026-08-13) ─────────────────
    // 기획서가 못 박은 값이다: 무적 0.3초 · 쿨다운 6초 · 거리 = 기본 이동 3초분.
    // ⚠️ 무적 프레임은 **사망 카운트 시스템에 직접 개입**한다("정확히 쓰면 한 번 산다").
    //    이 수치를 만질 땐 §18-8 사망 관련 수치와 함께 볼 것 — 죽음의 무게가 흔들린다.
    const float DASH_IFRAME = 0.30f;
    const float DASH_CD = 6.0f;
    const float DASH_TIME = 0.18f;          // 미끄러지는 시간. 거리는 이 시간에 나눠 준다

    /// <summary>직업별 변주(§5) — 외형만이 아니라 성능도 갈린다. (거리배율, 쿨배율)</summary>
    static (float dist, float cd) DashSpec(Job j) => j switch
    {
        Job.수호기사 => (0.70f, 1.00f),   // 방패 돌진 — 짧다
        Job.검사 => (1.00f, 1.00f),       // 구르기 — 표준
        Job.마법사 => (1.35f, 1.30f),     // 점멸 — 멀리, 대신 쿨이 길다
        Job.사제 => (0.75f, 0.70f),       // 짧은 스텝 — 자주 피하되 멀리 못 간다
        _ => (0.80f, 0.75f),              // 음유시인 — 힐·버퍼 계열과 같은 성격
    };

    /// <summary>
    /// 이동기 자가검사(`GAME_START=dash`). 무인 실행에서는 Shift를 누를 사람이 없어
    /// **코드를 넣고도 한 번도 안 돌려본 채 "완료"라고 적기 쉽다** — 이 프로젝트가
    /// 반복해서 겪은 실패다. 그래서 기획서가 못 박은 세 값을 스스로 재고 판정한다:
    ///   ① 이동 거리 = 기본 이동 3초분   ② 무적 0.3초 동안 피해 0   ③ 쿨다운 6초
    /// ②는 **네거티브 컨트롤**을 함께 돌린다 — 무적이 끝난 뒤 같은 피해가 들어가는지.
    /// </summary>
    public static bool QaDashProbe;
    int _dashProbeStep;
    float _dashProbeT;
    Vector2 _dashProbeFrom;
    float _dashProbeHp;

    void TickDashProbe(float dt)
    {
        if (!QaDashProbe || _party == null || _party.Length == 0) return;
        var m = _party[0];
        if (!m.Alive) return;
        _dashProbeT += dt;

        if (_dashProbeStep == 0 && _dashProbeT > 2f)
        {
            _dashProbeFrom = m.Pos;
            bool fired = TryDash(m, new Vector2(1f, 0f));
            Debug.Log($"[QA-대시] 발동={fired} 직업={m.Job} 쿨={m.DashCd:F2}s 무적={m.IFrame:F2}s");
            // 아트가 실제로 바뀌는지 — 「구현했는데 그림은 그대로」를 잡는 유일한 방법이다
            // ⚠️ 이름으로 비교하지 마라 — 런타임에 만든 스프라이트는 **이름이 빈 문자열**이라
            //    "다름 OK"가 항상 나온다(첫 구현이 그랬다. 판별력 0인 검사였다).
            //    `rect`(아틀라스 안의 자리)로 봐야 실제로 다른 그림인지 알 수 있다.
            var dashSp = SpriteBank.Cached.CharAnim(ArtOf(m.Job), SpriteBank.Motion.Dash, 0f);
            var idleSp = SpriteBank.Cached.CharAnim(ArtOf(m.Job), SpriteBank.Motion.Idle, 0f);
            var invSp = SpriteBank.Cached.Char(ArtOf(m.Job), SpriteBank.Frame.Invuln);
            bool artOk = dashSp != null && idleSp != null && dashSp.rect != idleSp.rect;
            bool invOk = invSp != null && idleSp != null && invSp.rect != idleSp.rect;
            Debug.Log($"[QA-대시] 대시 아트 rect={dashSp?.rect} 대기 rect={idleSp?.rect} " +
                      $"→ {(artOk ? "다름 OK" : "❌ 같다(아트 미반영)")} · " +
                      $"무적 아트 {(invOk ? "다름 OK" : "❌ 같다")}");
            _dashProbeStep = 1; _dashProbeT = 0f;
            _dashProbeHp = m.Hp;
            Damage(m, 40f, true);                       // 무적 중 — 막혀야 한다
            Debug.Log($"[QA-대시] 무적 중 피해40 → HP {_dashProbeHp:F0}→{m.Hp:F0} " +
                      $"({(Mathf.Approximately(_dashProbeHp, m.Hp) ? "차단 OK" : "❌ 새어나감")})");
            return;
        }

        if (_dashProbeStep == 1 && _dashProbeT > DASH_TIME + 0.05f)
        {
            float moved = (m.Pos - _dashProbeFrom).magnitude;
            float want = PlayerSpeed * 3f * DashSpec(m.Job).dist;
            Debug.Log($"[QA-대시] 이동거리 {moved:F2} (기대 {want:F2}, 오차 {(moved - want):F2}) " +
                      $"— {(Mathf.Abs(moved - want) < want * 0.35f ? "PASS" : "❌ 어긋남")}");
            _dashProbeStep = 2; _dashProbeT = 0f;
            return;
        }

        // 네거티브 컨트롤 — 무적이 끝났으면 같은 피해가 **들어가야** 한다.
        // 이게 없으면 "언제나 안 맞는" 버그를 통과로 읽는다.
        if (_dashProbeStep == 2 && _dashProbeT > 0.6f)
        {
            float before = m.Hp;
            Damage(m, 40f, true);
            Debug.Log($"[QA-대시] 무적 종료 후 피해40 → HP {before:F0}→{m.Hp:F0} " +
                      $"({(m.Hp < before ? "관통 OK(네거티브 컨트롤)" : "❌ 무적이 안 풀린다")})");
            _dashProbeStep = 3;
        }
    }

    /// <summary>
    /// 자동 전투 AI의 이동기 사용 (§5 💡 「AI도 이동기를 쓰되 **타이밍이 서툴게**
    /// — 예고 표식을 절반만 인지」). 수동 우대 원칙을 지키는 장치다: AI가 사람만큼
    /// 잘 피하면 직접 조작할 이유가 사라진다.
    ///
    /// 서툴게 만드는 방법 셋(전부 기획서 의도를 그대로 옮긴 것):
    ///   ① **인지율 50%** — 판단 시점마다 절반은 그냥 못 본다
    ///   ② **반응 지연** — 위험에 `AI_REACT_DELAY`초 놓여 있어야 비로소 움직인다.
    ///      사람은 예고를 보고 미리 피하지만 AI는 맞기 시작해야 안다
    ///   ③ **판단 주기** — 매 프레임이 아니라 0.35초마다 본다
    ///
    /// ⚠️ 조작 중인 캐릭터(`_sel`)에는 걸지 않는다 — 플레이어가 아끼던 쿨다운을
    ///    AI가 대신 태워버리면 수동 조작이 오히려 손해가 된다.
    /// </summary>
    const float AI_REACT_DELAY = 0.55f;
    const float AI_SEE_CHANCE = 0.5f;
    const float AI_CHECK_SEC = 0.35f;
    /// <summary>AI가 실제로 이동기를 쓴 횟수. 0이면 배선했는데 안 도는 것이다 — 로그로 확인한다.</summary>
    int _aiDashUses;

    void TickAiDash(Member m, int index, float dt)
    {
        if (!GameMode || !m.Alive) return;
        if (index == _sel) return;                       // 조작 중인 캐릭터는 사람 몫이다
        if (m.DashCd > 0f || m.DashT > 0f) return;

        // 위험 판정.
        // ⭐ **돌진 예고를 최우선으로 본다** — §5 💡의 「예고 표식을 절반만 인지」는
        //    애초에 예고가 존재한다는 전제의 문장이고, §10-2의 돌진형을 넣은 지금이라야
        //    그 말이 그대로 성립한다. 예고가 없던 동안은 밀집·저체력으로 대신 읽었는데,
        //    돌진형이 들어오자 분포가 바뀌어 **AI 이동기 사용이 4회 → 0회로 떨어졌다**(실측).
        //    "피할 수 있는 위협"을 AI도 피할 수 있어야 잡몹 구간이 성립한다.
        bool incoming = false;
        for (int i = 0; i < MAXM && !incoming; i++)
        {
            if (!_mOn[i] || _mKind[i] != 5) continue;
            if (_mChargePhase[i] != 1 && _mChargePhase[i] != 2) continue;   // 예고·돌진 중만
            var d = m.Pos - _mPos[i];
            if (d.sqrMagnitude > 8f * 8f) continue;
            // 나를 향하고 있는가 — 옆으로 지나가는 돌진까지 피하면 과민하다
            var face = _mChargePhase[i] == 2 ? _mChargeDir[i] : (m.Pos - _mPos[i]).normalized;
            if (Vector2.Dot(face, -d.normalized) < -0.55f) incoming = true;
        }

        int near = CountMobsNear(m.Pos, 2.2f);
        bool danger = incoming || near >= 3 || (m.Hp < m.MaxHp * 0.35f && near >= 1);

        // 돌진은 **예고 0.8초 안에** 피해야 의미가 있다 — 일반 위험보다 빨리 반응한다.
        // 그래도 사람보다는 느리게(수동 우대): 지연을 없애지 않고 절반으로만 줄인다.
        float reactDelay = incoming ? AI_REACT_DELAY * 0.5f : AI_REACT_DELAY;

        if (!danger) { m.DangerT = 0f; return; }
        m.DangerT += dt;
        if (m.DangerT < reactDelay) return;              // ② 늦게 반응한다

        m.AiDashCheck -= dt;
        if (m.AiDashCheck > 0f) return;                  // ③ 매 프레임 보지 않는다
        m.AiDashCheck = AI_CHECK_SEC;
        if (Random.value > AI_SEE_CHANCE) return;        // ① 절반은 못 본다

        // 돌진을 피할 땐 **옆으로** 빠진다 — 뒤로 도망치면 직선 경로에 계속 남아
        // 결국 맞는다. 이게 돌진형을 "회피 가능한 위협"으로 만드는 실제 동작이다.
        if (incoming)
        {
            for (int i = 0; i < MAXM; i++)
            {
                if (!_mOn[i] || _mKind[i] != 5) continue;
                if (_mChargePhase[i] != 1 && _mChargePhase[i] != 2) continue;
                var d = m.Pos - _mPos[i];
                if (d.sqrMagnitude > 8f * 8f) continue;
                var axis = _mChargePhase[i] == 2 ? _mChargeDir[i] : (-d).normalized;
                var side = new Vector2(-axis.y, axis.x);
                if (Vector2.Dot(side, d) < 0f) side = -side;      // 이미 치우친 쪽으로
                if (TryDash(m, side)) { _aiDashUses++; m.DangerT = 0f; }
                return;
            }
        }

        // 몹 무리의 반대쪽으로 뺀다
        Vector2 away = Vector2.zero;
        int cnt = 0;
        for (int i = 0; i < MAXM; i++)
        {
            if (!_mOn[i]) continue;
            var d = m.Pos - _mPos[i];
            if (d.sqrMagnitude > 2.2f * 2.2f) continue;
            away += d.normalized; cnt++;
        }
        if (cnt == 0) return;
        if (TryDash(m, away / cnt)) _aiDashUses++;
        m.DangerT = 0f;
    }

    /// <summary>이동기 발동. 쿨이 남았거나 죽었으면 아무 일도 안 한다.</summary>
    bool TryDash(Member m, Vector2 dir)
    {
        if (m == null || !m.Alive || m.DashCd > 0f || m.DashT > 0f) return false;
        if (dir.sqrMagnitude < 1e-4f) dir = new Vector2(m.Flip ? -1f : 1f, 0f);

        var (distMul, cdMul) = DashSpec(m.Job);
        m.DashDir = dir.normalized * distMul;
        m.DashT = DASH_TIME;
        m.DashCd = DASH_CD * cdMul;
        m.IFrame = DASH_IFRAME;
        m.SkillT = Mathf.Max(m.SkillT, DASH_TIME);        // 큰 동작으로 재생

        string label = m.Job switch
        {
            Job.수호기사 => "방패 돌진",
            Job.마법사 => "점멸",
            Job.사제 => "스텝",
            Job.검사 => "구르기",
            _ => "스텝",
        };
        SkillCast(m, label, new Color(0.85f, 0.9f, 1f));
        // 파티클을 겹쳐 쏜다(오너 지시 2026-08-15 「이펙트 좀 더 화려하게」).
        // 하나만 쏘면 "뭔가 났다" 정도고, **먼지(바닥) + 무적(몸) + 광륜(빛)** 세 겹이라야
        // 이동기가 순간기술로 읽힌다. 빌트인 파이프라인엔 블룸이 없어서 광륜이 곧 빛이다.
        FxParticles.Play(FxKind.먼지, ToScreen(m.Pos), 1.0f);
        FxParticles.Play(FxKind.무적, ToScreen(m.Pos), 1.1f);
        FxParticles.Play(FxKind.광륜, ToScreen(m.Pos), 0.8f, new Color(0.8f, 0.9f, 1f));
        return true;
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

    /// <summary>
    /// 계열 색조(§10-3). 던전은 진입 전에 계열을 표시하는데, 화면에서는 전 계열이 **같은 그림**이라
    /// 그 표시가 의미가 없었다. 아트문서 §0-B가 "종족 4종은 **팔레트 스왑**(신규 0장)"이라고
    /// 정해둔 방식을 그대로 따른다 — 새 스프라이트를 그리지 않는다.
    /// ⚠️ 색만 바꾼다. 실루엣은 건드리지 않는다(수백 마리 화면에서 구분은 실루엣이 먼저다).
    /// </summary>
    /// <summary>
    /// 몹 종류(`_mKind`) → 스프라이트 계열 인덱스(`SpriteBank.MOB_DIRS`).
    ///
    /// **스폰 시 정적 스프라이트와 매 프레임 애니메이션이 같은 규칙을 써야 한다** —
    /// 예전엔 스폰은 종류별(`Cached.Mob(...)`)인데 애니메이션은 `MobAnim(0, ...)` 고정이라,
    /// 몹이 처음 나올 때와 걷기 시작할 때 그림이 바뀌었다. 그 규칙을 여기 하나로 모은다.
    ///
    /// §10-2 계열: 0·1·2=잡몹(추격·돌진), 3=치유 정예, 4=소환 정예.
    /// 정예 2종은 전용 아트가 아직 없어 원거리·군집 실루엣을 빌려 쓰되 색·크기로 구분한다
    /// (스폰 코드가 이미 초록/보라 틴트와 1.4배 크기를 준다).
    /// </summary>
    /// <summary>
    /// 행동 종류 → 실루엣. **둘이 어긋나 있었다** — 원거리형이 뿔 달린 돌진 실루엣
    /// (Charger)을 쓰고, 포위형이 날렵한 늑대(Chaser)를 쓰고 있었다. INBOX의
    /// 「실루엣이 그 몹의 행동을 예고해야 한다」가 정면으로 깨진 상태였다(2026-08-15 발견).
    /// 이제 짝을 맞춘다: 포위형=군집형 · 돌진형=뿔·덩치 · 원거리형=직립·긴 팔.
    /// ⚠️ 정예(4)는 추적형(0)과 실루엣을 공유한다 — §0-B가 「정예는 신규 아트 0장,
    ///    색조·크기로만 변주」로 확정했기 때문이다. 크기 1.4배와 전용 색으로 갈린다.
    /// </summary>
    static int MobSpriteKind(int kind) => kind switch
    {
        1 => SpriteBank.MobKindSwarmer,    // 포위형 — 낮고 넓게 벌어진 군집형
        2 => SpriteBank.MobKindRanged,     // 원거리형 — 직립·긴 팔
        3 => SpriteBank.MobKindChaser,     // 치유 정예
        5 => SpriteBank.MobKindCharger,    // 돌진형 — 뿔·덩치·앞으로 쏠린 무게중심
        _ => SpriteBank.MobKindBasic,      // 0 추적형 · 4 소환 정예(크기·색으로 갈린다)
    };

    /// <summary>
    /// 몹 색조. **같은 종류는 반드시 같은 색이다**(오너 지적 2026-08-15
    /// "같은 종류 몬스터끼리는 색깔 같게 해, 같은 모양끼리 알록달록하냐").
    ///
    /// 직전 구현은 필드에서 **개체마다** 계열을 무작위로 굴렸다. 그래서 똑같이 생긴
    /// 추격형 늑대가 하나는 주황, 옆의 하나는 자홍으로 서서 색이 아무 정보도 주지
    /// 않았다 — 색이 정보가 되려면 **같은 것은 같은 색**이라는 규칙이 먼저다.
    ///
    /// 이제 색은 **종류(실루엣)에서 결정**된다. 화면에서 색과 모양이 항상 짝지어
    /// 나오므로, 멀리서 색만 봐도 그게 원거리형인지 추격형인지 읽힌다.
    /// 던전에서는 계획된 계열색이 전체에 걸리는데(§10-3), 그때도 한 던전 안에서는
    /// 전부 같은 색이라 「같은 종류 같은 색」이 깨지지 않는다.
    /// ⚠️ 검증 실행(W1~W3)은 흰색 유지 — 구성 대조 실험에 색이 끼면 안 된다.
    /// </summary>
    static Color FamilyTint(int kind)
    {
        if (SuppressTint) return Color.white;
        if (DungeonRun.Active) return TintOf(DungeonRun.Plan.Family);

        // 잡몹 3종은 서로 최대한 먼 색상으로. 정예 2종은 잡몹이 쓰지 않는 색을 쓴다 —
        // 등급이 크기(1.4배)만으로 구분되면 뭉쳐 있을 때 안 읽힌다(오너 지적 "등급도
        // 같은 거 같은데"). 이제 색과 크기 **두 축**이 함께 등급을 말한다.
        // ⚠️ 여기 번호는 **행동 종류**(`_mKind`)다. 실루엣은 `MobSpriteKind`가 1:1로
        //    갈라주므로 종류마다 그림도 색도 하나씩 대응한다 — 그래서 같은 모양은 같은 색이 된다.
        //    (실루엣 이름과 행동 이름이 어긋나 있다는 별개 문제는 STATUS.md에 적어 뒀다)
        return kind switch
        {
            0 => TintOf(MobFamily.야수),      // 추적형 — 주황
            1 => TintOf(MobFamily.기계),      // 포위형 — 강청
            2 => TintOf(MobFamily.언데드),    // 원거리형 — 독초록
            5 => new Color(1f, 0.82f, 0.30f), // 돌진형 — 황색. 예고를 봐야 하는 몹이라 눈에 띄어야 한다
            3 => new Color(1f, 0.30f, 0.30f), // 치유 정예 — 적색(잡몹에 없는 색)
            _ => new Color(0.78f, 0.40f, 1f), // 소환 정예 — 보라(잡몹에 없는 색)
        };
    }

    /// <summary>
    /// 계열 색조. **곱셈 틴트**라 원본이 회색(0.7 근처)이면 결과 채도가 그대로 깎인다 —
    /// 예전 값은 전부 0.66~1.0 범위여서 곱한 뒤 채도가 15~30%밖에 안 됐고,
    /// 화면에서 "다 비슷한 회색"으로 보였다(오너 지적 2026-08-15, 수치로 확인).
    ///
    /// 그래서 **약한 채널을 과감히 눌러** 색상 차이를 벌린다. §10-3이 계열을 직업 상성으로
    /// 쓰는 이상 한눈에 갈려야 하고, 수백 마리 화면에서는 색이 그 유일한 단서다.
    /// ⚠️ 밝기까지 낮추면 어두운 배경에서 안 보인다 — 강한 채널은 1.0 근처로 유지한다.
    /// </summary>
    static Color TintOf(MobFamily f) => f switch
    {
        MobFamily.야수 => new Color(1f, 0.62f, 0.38f),      // 주황 — 살빛을 진하게
        MobFamily.언데드 => new Color(0.45f, 0.95f, 0.62f), // 독초록
        MobFamily.마족 => new Color(0.98f, 0.35f, 0.85f),   // 자홍
        MobFamily.기계 => new Color(0.45f, 0.75f, 1f),      // 강청
        _ => new Color(0.55f, 0.95f, 1f),                   // 정령 — 청록
    };

    void KillMob(int i)
    {
        _mOn[i] = false; _mAlive--; _kills++;
        if (_mSummoned[i]) { _mSummoned[i] = false; _summonedAlive = Mathf.Max(0, _summonedAlive - 1); }
        // 죽은 자리에 먼지 — 다만 **초당 처치가 수십 건**이므로 전부 뿌리면 화면이 먼지밭이 된다.
        // 5마리에 한 번만 낸다(파티클 풀 24개를 잡몹 사망이 독점하지 않게).
        // 5마리에 1번은 지나치게 성겼다 — 처치가 화면에서 거의 안 읽혔다.
        // 3마리에 1번으로 올리고, 정예는 매번 크게 터뜨린다(드물게 죽으므로 비용도 작다).
        if ((_kills % 3) == 0) MobDeathPuff(_mPos[i]);
        if (_mKind[i] >= 3)
        {
            FxParticles.Play(FxKind.쇼크웨이브, ToScreen(_mPos[i]), 1.2f);
            FxParticles.Play(FxKind.광륜, ToScreen(_mPos[i]), 0.9f, _mTint[i]);
        }
        // 정예는 매번 — 드물게 죽는 데다, 죽은 것이 화면에서 확실히 읽혀야 한다.
        if (_mKind[i] >= 3 || (_kills % 3) == 0)
            FxPool.Play(FxPool.Kind.Death, _mPos[i], _mKind[i] >= 3 ? 1.5f : 0.9f);

        // 오브젝트를 여기서 끄지 않는다 — 사망 모션이 돌 시간을 준다.
        // `_mOn[i]`는 이미 false라 AI·타겟팅·피격 판정에서는 즉시 빠진다(시체가 싸우지 않는다).
        // 실제로 치우는 것은 애니메이션 갱신부다.
        _mDeadT[i] = 0.0001f;                       // 0이 아니어야 "죽어가는 중"으로 읽힌다
        _mFlash[i] = 0f;                            // 피격 섬광이 남아 시체가 번쩍이지 않게
        _mSr[i].color = _mTint[i];
        _mSr[i].sprite = SpriteBank.Cached.MobAnim(MobSpriteKind(_mKind[i]), SpriteBank.Motion.Death, 0f);
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
            Member tgt = _mKind[i] == 2 ? PickBackline() : PickNearestOrTaunt(p);   // §10-4 후열 저격은 원거리형만
            if (tgt == null) continue;

            Vector2 to = tgt.Pos - p;
            float dist = to.magnitude + 1e-4f;
            Vector2 dir = to / dist;
            float spd;
            Vector2 want;

            if (_mKind[i] == 5)                                   // 돌진형 (§10-2)
            {
                _mChargeT[i] -= dt;
                switch (_mChargePhase[i])
                {
                    case 1:                                        // 예고 — 제자리에서 노려본다
                        want = Vector2.zero; spd = 0f;
                        if (_mChargeT[i] <= 0f)
                        {
                            // 방향은 **예고가 끝나는 순간** 고정한다. 계속 따라오면
                            // 회피가 불가능해져 "피할 수 있는 위협"이 거짓이 된다.
                            _mChargeDir[i] = dir;
                            _mChargePhase[i] = 2; _mChargeT[i] = CHG_RUSH;
                        }
                        break;
                    case 2:                                        // 돌진 — 직선 고속
                        want = _mChargeDir[i]; spd = PlayerSpeed * CHG_SPEED;
                        if (_mChargeT[i] <= 0f)
                        {
                            _mChargePhase[i] = 3; _mChargeT[i] = CHG_STUN; _chargeRushes++;
                            // 멈추는 순간이 가장 세게 보여야 한다 — 경직은 반격의 창이고,
                            // 그 창이 열렸다는 신호가 곧 「회피에 대한 보상」이다(§10-2).
                            FxParticles.Play(FxKind.쇼크웨이브, ToScreen(p), 1.4f);
                            FxParticles.Play(FxKind.먼지, ToScreen(p), 1.2f);
                            Shake(0.16f);
                        }
                        break;
                    case 3:                                        // 경직 — 회피에 대한 보상
                        want = Vector2.zero; spd = 0f;
                        if (_mChargeT[i] <= 0f) _mChargePhase[i] = 0;
                        break;
                    default:                                       // 접근
                        want = dir; spd = PlayerSpeed * ChaserRatio;
                        if (dist < CHG_RANGE)
                        {
                            _mChargePhase[i] = 1; _mChargeT[i] = CHG_TELL;
                            _chargeTells++;
                            // 예고 표식 — 이게 보이지 않으면 §10-2가 성립하지 않는다.
                            // 바닥 마법진(예고는 바닥에 깔려야 진행 방향이 읽힌다) + 심볼을 겹친다.
                            FxParticles.Play(FxKind.마법진, ToScreen(p), 1.6f,
                                             new Color(1f, 0.78f, 0.25f));
                            FxPool.Play(FxPool.Kind.Taunt, p, 1.5f, new Color(1f, 0.75f, 0.2f, 0.9f));
                        }
                        break;
                }
            }
            else if (_mKind[i] == 1)                              // 포위형
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
            // ⚠️ 임계값이 중요하다. 예전엔 `1e-4`라 **미세한 흔들림에도 매 프레임 뒤집혀**
            //    몹이 좌우로 파르르 떠는 것처럼 보였다(오너 지적 2026-08-15).
            //    유닛 분리(UnitSeparation)가 서로 밀어내므로 정지 상태에서도 x가 조금씩 흔들린다.
            //    한 프레임 이동량이 **실제로 방향이라 부를 만할 때**만 뒤집는다.
            float dx = p.x - prev.x;
            if (Mathf.Abs(dx) > 0.02f * Mathf.Max(0.016f, dt) / 0.016f) _mFlip[i] = dx < 0f;
            _mSr[i].flipX = _mFlip[i];
            _mSr[i].sortingOrder = Depth(p.y);   // 파티와 **같은 공식**이어야 앞뒤가 맞는다(D2)
            if (_mFlash[i] > 0f)
            {
                _mFlash[i] -= dt;
                _mSr[i].color = Color.white;
            }
            // 섬광이 끝나면 **스폰 때 정한 그 색**으로 돌아온다. 여기서 FamilyTint()를
            // 다시 부르면 필드에서는 계열이 매번 새로 굴려져 깜빡인다(2026-08-15 사고).
            else _mSr[i].color = _mTint[i];

            // 근접 공격은 **쿨다운을 두고 한 번씩** — 매 프레임 피해를 주면
            // 초당 수천 번이 되어 파티가 4초 만에 녹는다(2026-08-13 실측 버그)
            if (_mKind[i] != 2 && dist < 1.1f)
            {
                _mAtkCd[i] -= dt;
                if (_mAtkCd[i] <= 0f)
                {
                    _mAtkCd[i] = 1.0f;
                    _meleeHits++;
                    float md = 6f * sp.TakenMul;
                    Damage(tgt, md, tgt.Role == Role.Tank);
                    // 소환 몹이 준 피해만 따로 센다 — 네거티브 컨트롤의 측정선(§10-5).
                    if (_mSummoned[i]) { _summonDmgToParty += md; _summonHits++; }
                }
            }
        }
    }

    /// <summary>도발 반경 — 원거리 몹의 유지 거리(6.5)와 같아야 후열 저격을 실제로 끊는다.</summary>
    const float TauntRadius = 6.5f;

    Member PickNearestOrTaunt(Vector2 p)
    {
        // 도발은 **하드 락**이다(FFXIV Provoke 계열): 반경 안의 근접 몹은 위협·거리와 무관하게 탱을 본다.
        // 예전에는 근접 몹에게 도발이 위협 +80(≈ 거리 9.6유닛)으로만 작용했는데,
        // 위협은 감쇠하고 거리는 매 프레임 변하므로 "도발했는데 아무도 안 온다"가 자주 났다.
        // 실측: 도발을 꺼도(D 구성) 생존이 표준의 0.85배 — 도발이 판을 거의 못 바꿨다.
        if (_t < _tauntUntil)
        {
            if (_tauntMember != null && _tauntMember.Alive &&
                (_tauntMember.Pos - p).sqrMagnitude < TauntRadius * TauntRadius)
                return _tauntMember;
        }

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
            if (_tauntMember != null && _tauntMember.Alive) return _tauntMember;
        }
        // §10-4 "원거리 몹은 **후열(위협 낮은 쪽)** 저격".
        // ⚠️ 예전 식 `MaxHp − Threat×6`의 **최솟값**을 골랐다 — 위협이 높을수록 점수가 낮아지므로
        //    실제로는 **위협이 가장 높은 딜러**를 노렸다(주석과 정반대).
        //    게다가 위협이 0이어도 딜러 130 < 힐·버퍼 150이라 **힐러·버퍼는 한 번도 저격당하지 않았다.**
        //    그래서 "후열이 녹는다 → 도발로 끊는다"는 §10-4의 구조가 성립한 적이 없다.
        //    탱을 제외한 뒤 **위협이 낮은 쪽**을 고른다(같으면 HP 낮은 쪽).
        Member best = null; float score = float.MaxValue;
        foreach (var m in _party)
        {
            if (!m.Alive || m.Role == Role.Tank) continue;
            float s = m.Threat * 6f + m.MaxHp * 0.02f;
            if (s < score) { score = s; best = m; }
        }
        if (best == null)                                        // 탱만 남았다면 탱을 친다
            foreach (var m in _party) if (m.Alive) { best = m; break; }
        return best;
    }

    /// <summary>2차 각성의 4스킬+초필 1개. 초필 자원은 직업 고유 Gauge와 분리한다.</summary>
    void CastUltimate(Member m)
    {
        m.UltimateGauge = 0f;
        m.UltimateCd = UltimateCooldownSeconds;
        float effect = 0f;

        if (m.Role == Role.Tank)
        {
            foreach (var ally in _party)
            {
                if (!ally.Alive) continue;
                float before = ally.Shield;
                ally.Shield = Mathf.Max(ally.Shield, 80f * _bShield);
                ally.IFrame = Mathf.Max(ally.IFrame, 3f);
                effect += ally.Shield - before;
                FxParticles.Play(FxKind.무적, ToScreen(ally.Pos), 1.1f);
            }
        }
        else if (m.Role == Role.Dps)
        {
            for (int i = 0; i < MAXM; i++)
            {
                if (!_mOn[i]) continue;
                float dealt = Mathf.Min(_mHp[i], m.Atk * 4f * _bAtk);
                _mHp[i] -= dealt;
                effect += dealt;
                FlashMob(i);
                if (_mHp[i] <= 0f) KillMob(i);
            }
        }
        else if (m.Role == Role.Healer)
        {
            foreach (var ally in _party)
            {
                if (!ally.Alive) continue;
                effect += Heal(ally, 100f * _bHeal);
                float before = ally.Shield;
                ally.Shield = Mathf.Max(ally.Shield, 30f * _bShield);
                effect += ally.Shield - before;
            }
        }
        else
        {
            foreach (var ally in _party)
            {
                if (!ally.Alive) continue;
                ally.Cd = Mathf.Min(ally.Cd, 0f);
                ally.SkillCd = Mathf.Min(ally.SkillCd, 0f);
                float before = ally.Shield;
                ally.Shield = Mathf.Max(ally.Shield, 40f * _bShield);
                effect += (ally.Shield - before) + 1f;
            }
        }

        _qaUltimateEffect += effect;
        m.SkillT = 0.9f;
        FlashParty();
        SkillCast(m, $"{m.Job} 각성 초필", new Color(0.65f, 0.85f, 1f), hitstop: 6, shake: 0.55f);
        FxParticles.Play(FxKind.쇼크웨이브, ToScreen(m.Pos), 3.2f, new Color(0.65f, 0.85f, 1f));
        Debug.Log($"[W3-초필] {m.Job} 발동 효과={effect:F1} 쿨={m.UltimateCd:F0}초");
    }

    void Damage(Member m, float dmg, bool front)
    {
        if (!m.Alive) return;
        // 무적 프레임(§5) — 진입점이 하나라서 여기 한 줄이면 모든 피해가 막힌다.
        // 장판·투사체·근접이 각자 판정을 갖고 있었다면 어딘가는 반드시 새어나갔을 것이다.
        if (m.IFrame > 0f) return;
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
        Member guardian = null;
        foreach (var candidate in _party)
            if (candidate.Alive && candidate.Job == Job.수호기사) { guardian = candidate; break; }
        if (guardian != null)
        {
            // 자기가 맞으면 크게, 아군이 맞으면 작게 — 아군이 맞는 건 탱이 못 막은 것이다
            guardian.Gauge = Mathf.Min(100f, guardian.Gauge + (m == guardian ? incoming * 0.5f : incoming * 0.12f));
        }

        m.Hp -= dmg;

        // 최후의 보루(§3 수호기사 3번 스킬) — 3초간 HP가 1 미만으로 안 떨어진다.
        // 자동 전투라 조건 발동으로 둔다: 치명타 한 방에 즉사하는 것을 한 번 막아준다.
        if (m.Job == Job.수호기사 && m.Advancement != AdvancementTier.Basic
            && m.Hp <= 0f && _t >= _lastStandCd)
        {
            m.Hp = 1f;
            _lastStandUntil = _t + 3.0f;
            _lastStandCd = _t + 60f;
            _skillLog[7]++;
            FxParticles.Play(FxKind.무적, ToScreen(m.Pos), 1.6f);
            Debug.Log($"[W3] 최후의 보루 발동 @ {_t:F1}s");
        }
        if (m.Job == Job.수호기사 && _t < _lastStandUntil && m.Hp < 1f) m.Hp = 1f;
        FxParticles.Play(FxKind.피격, ToScreen(m.Pos));
        if (front) _frontlineHits++; else _backlineHits++;
        if (m.Role == Role.Healer || m.Role == Role.Buffer) _supportHits++;
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
    /// <summary>몹이 죽은 자리에 먼지 한 줌. 500체 화면이므로 **아주 작게만** 쓴다.</summary>
    void MobDeathPuff(Vector2 p) => FxParticles.Play(FxKind.먼지, ToScreen(p), 0.7f);

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
            Debug.Log($"[W3] 판 종료 — 소환 몹이 파티에 준 피해 {_summonDmgToParty:F0} ({_summonHits}회)");
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
            _faithPeak.ToString("F0", ci), RunSeed().ToString(ci), _rep.ToString(ci),
            _supportHits.ToString(ci)));

        // ⚠️ **판마다 즉시 쓴다.** 예전엔 전 구성이 끝난 뒤 Finish()에서 한 번에 썼는데,
        //    5회 반복(25판·약 50분) 측정이 24판째에 세션 종료로 죽자 **결과가 통째로 사라졌다**.
        //    긴 측정에서 "끝나야 저장"은 저장하지 않는 것과 같다.
        try { File.WriteAllText(_outPath, _csv.ToString()); }
        catch (System.Exception e) { Debug.LogWarning($"[W3] 중간 저장 실패: {e.Message}"); }
        Debug.Log($"[W3] {_setup.Name}: {_t:F1}s 생존 / 처치 {_kills} / 도발 {_tauntUses} / " +
                  $"후열피격 {_backlineHits} 전열피격 {_frontlineHits} / {verdict}");
        Debug.Log($"[W3-DIAG] AI 이동기 사용 {_aiDashUses}회");
        Debug.Log($"[W3-DIAG] 몹생존 {_mAlive} / 근접타격 {_meleeHits} / 투사체타격 {_shotHits} / " +
                  $"프레임 {_framesThisRun} / 초당근접 {_meleeHits / Mathf.Max(0.1f, _t):F0}");

        NextStyle();
    }

    void OnGUI()
    {
        _hud ??= new GUIStyle(GUI.skin.label) { fontSize = 17, normal = { textColor = Color.white } };

        // 스킬 이름 — `GUI.matrix`를 건드리기 **전에** 그린다(여기 좌표는 실제 픽셀이다).
        DrawCallouts();

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

            // 새 파티 초상화 시트를 우선한다. 리소스가 없을 때만 기존 유닛 스프라이트로 복귀한다.
            var pr = new Rect(card.x + 6, card.y + 4, 52, 66);
            var portraitTint = m.Alive ? Color.white : new Color(1f, .6f, .6f, .55f);
            if (!AshesToStars.PortraitAtlas.Draw(pr, AshesToStars.PortraitAtlas.KeyForJob(m.Job.ToString()), portraitTint))
            {
                var sp = bank?.Char(ArtOf(m.Job));
                if (sp != null)
                {
                    var uv = PortraitUV(sp);
                    GUI.color = portraitTint;
                    GUI.DrawTextureWithTexCoords(pr, sp.texture, uv);
                    GUI.color = Color.white;
                }
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
        string a, b;
        if (sel.Advancement == AdvancementTier.Basic)
            (a, b) = sel.Role switch
            {
                Role.Tank => ("도발", "방패벽"),
                Role.Dps => ("강타", "집중"),
                Role.Healer => ("치유", "정화"),
                _ => ("고양", "쇠약"),
            };
        else
        {
            string[] labels = FirstAdvancementSkillLabels(sel.Job.ToString());
            a = labels[0]; b = labels[1];
        }

        float w = 176f, gap = 8f;
        float x = Screen.width * 0.5f - (w * 2 + gap) * 0.5f;

        // ⚠️ 예전엔 본문이 `{ }`였다 — 버튼이 눌리는데 **아무 일도 일어나지 않았다.**
        //    키보드(스페이스/Q)로는 되는데 마우스로는 스킬을 아예 못 쓰는 상태였고,
        //    §5의 「보스는 수동 지휘」가 마우스 유저에게는 성립하지 않았다.
        //    미구현보다 나쁘다 — 눌리니까 사용자는 고장으로 읽는다(2026-08-15 감사에서 발견).
        if (SkillBtn(new Rect(x, y, w, 34), a, sel, 1)) sel.ForceSkill = 1;
        if (b != "—" && SkillBtn(new Rect(x + w + gap, y, w, 34), b, sel, 2)) sel.ForceSkill = 2;

        if (sel.Advancement == AdvancementTier.Second)
        {
            bool ready = CanUseUltimate(sel.Advancement, sel.UltimateGauge, sel.UltimateCd);
            float ux = x + w * 2 + gap * 2;
            GUI.enabled = ready;
            if (GUI.Button(new Rect(ux, y, w, 34),
                           ready ? "각성 초필 [E]" : $"초필 {Mathf.FloorToInt(sel.UltimateGauge)}% / {Mathf.CeilToInt(Mathf.Max(0f, sel.UltimateCd))}s",
                           _cmdBtn))
                sel.ForceUltimate = true;
            GUI.enabled = true;
        }

        GUI.Label(new Rect(x, y - 20f, 520, 20),
                  b != "—" ? $"스킬 — 스페이스/클릭: {a} · Q/클릭: {b}"
                           : $"스킬 — 스페이스 또는 클릭: {a}", _cmdLabel);
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
