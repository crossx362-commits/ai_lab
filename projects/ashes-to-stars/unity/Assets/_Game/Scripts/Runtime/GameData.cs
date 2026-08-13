using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 기획서의 수치를 **에디터에서 보고 고칠 수 있게** ScriptableObject로 옮긴 것.
    ///
    /// 왜 코드가 아니라 에셋인가:
    ///   기획서 §18 수치 밸런싱 기준표는 플레이테스트에서 계속 조정된다.
    ///   수치가 C# 상수로 박혀 있으면 값 하나 바꿀 때마다 컴파일·빌드가 필요하고,
    ///   기획자가 직접 만질 수 없다. 인스펙터에서 만지고 바로 플레이할 수 있어야 한다.
    ///
    /// 문서 대응: 각 필드의 툴팁에 기획서 절 번호를 적어 둔다.
    /// </summary>

    public enum RaceId { 인간, 엘프, 드워프, 수인 }
    public enum RoleId { 탱, 딜, 힐, 버퍼 }
    public enum MobAi { 추적, 포위, 돌진, 원거리 }
    public enum MobFamily { 야수, 언데드, 마족, 기계, 정령 }
    public enum EliteKind { 없음, 수호자, 처형자, 주술사, 군단장, 저주술사, 소환술사 }
    public enum StyleId { 공격형, 균형형, 방어형, 생존형 }

    // ────────────────────────────────────────────────────────
    [CreateAssetMenu(menuName = "재와별/종족", fileName = "Race_")]
    public class RaceDef : ScriptableObject
    {
        public RaceId Id;
        [TextArea] public string 정체성;

        [Header("기울기 (§18-9 — 10~20% 원칙)")]
        [Tooltip("경험치 획득 배율")] public float 경험치배율 = 1f;
        [Tooltip("전직 재료 획득 배율")] public float 전직재료배율 = 1f;
        [Tooltip("이동 속도 배율")] public float 이속배율 = 1f;
        [Tooltip("최대 HP 배율")] public float 체력배율 = 1f;
        [Tooltip("방어 배율")] public float 방어배율 = 1f;
        [Tooltip("PvE 사망 후 회복 시간(시간) — §18-8")] public float 회복시간 = 24f;
        [Tooltip("영지 생산 배율")] public float 영지생산배율 = 1f;
        [Tooltip("몬스터 드랍률 배율")] public float 드랍률배율 = 1f;
        [Tooltip("경매 수수료(%) — 기본 10")] public float 경매수수료 = 10f;

        [Header("고유 메커니즘")]
        [TextArea] public string 고유메커니즘;
        [Tooltip("드워프 불굴 / 수인 야성감각 발동 확률")] public float 고유발동확률 = 0f;
        [Tooltip("고유 메커니즘 발동 제한 — 전투당 횟수")] public int 전투당발동 = 1;
    }

    // ────────────────────────────────────────────────────────
    [Serializable]
    public class SkillDef
    {
        public string 이름;
        [TextArea] public string 설명;
        [Tooltip("쿨다운(초) — 마나 없음, 쿨다운 단일 체계(§11-5)")] public float 쿨다운 = 5f;
        [Tooltip("기본 공격력 대비 배율")] public float 위력배율 = 1f;
        [Tooltip("효과 반경(0이면 단일)")] public float 반경 = 0f;
        [Tooltip("고유 자원 소모량(0이면 미사용)")] public float 자원소모 = 0f;
        [Tooltip("초필살기 여부 — 2차 전직에서 해금(§3)")] public bool 초필살기 = false;
    }

    [CreateAssetMenu(menuName = "재와별/직업", fileName = "Job_")]
    public class JobDef : ScriptableObject
    {
        public string 직업명;
        public RoleId 역할;
        [TextArea] public string 컨셉;
        [TextArea] public string 고유메커니즘;

        [Header("기본 스탯")]
        public float 최대체력 = 130f;
        public float 공격력 = 20f;
        [Tooltip("공격 사거리(유닛)")] public float 사거리 = 5f;
        [Tooltip("공격 간격(초)")] public float 공격간격 = 0.45f;

        [Header("이동기 (§5 — 전 캐릭터 보유)")]
        [Tooltip("대시 형태: 구르기/방패돌진/점멸/스텝/위치교체")] public string 이동기형태 = "구르기";
        [Tooltip("무적 프레임(초) — 기본 0.3")] public float 무적시간 = 0.3f;
        [Tooltip("쿨다운(초) — 기본 6")] public float 이동기쿨 = 6f;
        [Tooltip("이동 거리(기본 이동 몇 초분)")] public float 이동기거리 = 3f;

        [Header("스킬 (기본2 → 1차전직4 → 2차전직 +초필)")]
        public SkillDef[] 스킬 = new SkillDef[0];

        [Header("리스크 프로필 (§3)")]
        [Tooltip("최저/낮음/중/중상/높음")] public string 사망리스크 = "중";
        [Tooltip("필드 자동사냥 적합도 0~1")] [Range(0f, 1f)] public float 자동사냥적합도 = 0.5f;
    }

    // ────────────────────────────────────────────────────────
    [CreateAssetMenu(menuName = "재와별/몬스터", fileName = "Mob_")]
    public class MobDef : ScriptableObject
    {
        public string 이름;
        public MobAi AI;
        public MobFamily 계열;
        public EliteKind 정예유형 = EliteKind.없음;

        [Header("수치 (§18-11)")]
        [Tooltip("플레이어 이동 속도 대비 배율 — 추적0.90/포위0.85/원거리0.65")]
        public float 속도배율 = 0.90f;
        [Tooltip("캐릭터 1타 데미지 대비 HP 배율 (0.8~1.5 = 1~2타에 사망)")]
        public float 체력배율 = 1.2f;
        [Tooltip("캐릭터 최대 HP 대비 피해 비율 (0.02~0.04)")]
        public float 피해비율 = 0.03f;
        [Tooltip("근접 공격 간격(초)")] public float 공격간격 = 1.0f;

        [Header("원거리형 전용")]
        public float 유지거리 = 6.5f;
        public float 발사간격 = 2.4f;
        [Tooltip("탄속 — 느려야 회피 가능(§10-2)")] public float 탄속 = 5.5f;

        [Header("표시")]
        public Sprite 스프라이트;
        public Color 색조 = Color.white;
        public float 크기 = 2.2f;
    }

    // ────────────────────────────────────────────────────────
    [CreateAssetMenu(menuName = "재와별/전투 스타일", fileName = "Style_")]
    public class CombatStyleDef : ScriptableObject
    {
        public StyleId Id;
        [Header("§3 전투 스타일 표")]
        [Tooltip("주는 피해 배율")] public float 딜배율 = 1f;
        [Tooltip("받는 피해 배율")] public float 피해배율 = 1f;
        [Tooltip("이 HP 비율 이하면 후퇴")] [Range(0f, 1f)] public float 후퇴체력 = 0.15f;
        [Tooltip("탱커로부터 유지할 거리")] public float 유지거리 = 1.4f;
        [TextArea] public string 행동설명;

        [Header("토글 기본값")]
        public bool 정예우선타겟 = false;
        [Tooltip("부활초·귀환의두루마리는 항상 false — 자동 사용 금지(§4)")]
        public bool 소모품자동사용 = false;
    }

    // ────────────────────────────────────────────────────────
    [CreateAssetMenu(menuName = "재와별/밸런스 설정", fileName = "GameBalance")]
    public class BalanceConfig : ScriptableObject
    {
        [Header("앵커 (§18 — 이것만 바꾸면 경제 전체가 스케일된다)")]
        [Tooltip("티어1 필드 자동사냥 1시간 수익 = 1골드")] public float 티어1시간당골드 = 1f;
        [Tooltip("티어당 수익 배율")] public float 티어배율 = 1.6f;
        [Tooltip("하위 레이드 스케일링 계수")] public float 스케일링계수 = 0.65f;
        [Tooltip("총 유입 대비 소각 목표(%)")] public Vector2 소각목표 = new Vector2(45f, 55f);

        [Header("플레이어")]
        public float 플레이어이동속도 = 4.2f;

        [Header("사망 (§4·§18-8)")]
        public int 사망상한 = 3;
        public float PvE회복시간 = 24f;
        public float PvP회복시간 = 12f;
        public int 부활초소지상한 = 3;

        [Header("성능 예산 (§10-9)")]
        public int 잡몹상한 = 500;
        public int 소환수상한 = 50;
        public int 투사체상한 = 200;

        [Header("쿼터뷰")]
        [Tooltip("월드 y를 화면에 그릴 때 곱하는 값 = sin(30°)")] public float ISO_Y = 0.5f;
    }
}
