// 명세: docs/GAME_SPEC_TANK_ARTILLERY.md §9-3(실루엣), §8 TankData
//
// === 왜 VIEW 가 아니라 SIM 인가 ===
// 탱크가 "생김새만 다르고 성능은 같은" 상태였다. 성능을 주면 밸런스 문제가 생기는데,
// 밸런스는 눈으로 못 본다 — 돌려서 승률을 재야 한다. 수치를 SIM 에 둬야
// 유니티 없이 매치업 승률을 잴 수 있다(tools/BattleSimVerify.cs).
//
// === 로스터는 포트리스2 원작을 그대로 따른다(오너 지시) ===
// 4계열 13종. 계열별 성격은 원작 문서에 적힌 서술을 그대로 옮겼다:
//   고전 — "화력이 직격타이면 매우 강력하나 방어력·체력이 낮고 이동거리가 짧다"
//   근대 — "방어력이 매우 높고 평준화된 능력치"
//   현대 — "방어력과 공격력이 모두 높고 사정거리도 길지만 화력이 분산된다"
//   미래 — 계열 공통 성격 대신 **탱크마다 고유 능력**
// 고유 능력도 원작 서술대로다: 이온어태커 "정타를 맞아도 지형이 전부 파인다",
// 포세이돈 "눈이 오면 데미지와 지형파괴가 25% 증가", 세크윈드 "체력 50% 이하면 데미지 50% 증가",
// 마인랜더 체력 1150(전 탱크 최고), 슈퍼탱크 "모든 수치가 월등, 랜덤에서만 낮은 확률로 등장",
// 레이저탱크 "지뢰를 무시하고 가장 멀리 이동".
//
// ⚠️ 원작에 있는데 이 게임에 **없는 것은 옮기지 않았다.** 지뢰 시스템이 없으므로
//    마인랜더의 지뢰와 레이저의 지뢰 무시는 플래그로 선언하지 않는다 — 읽는 코드가 0곳인
//    죽은 데이터가 되기 때문이다. 대신 이미 있는 축으로 옮겼다:
//    마인랜더 → 굴착 반경(지형에 흔적을 남기는 쪽), 레이저 → 이동 게이지 최장.
//
// ⚠️ 여기 값을 바꾸면 반드시 `./tools/verify.sh battle` 로 매치업 승률을 다시 재라.
//    §2-5-1 에서 봤듯 이 게임은 수치 하나에 한 판 길이가 배로 뛴다.

using System;

namespace Tankfall.Sim
{
    /// <summary>포트리스2 등장탱크 13종. 순서는 계열순(고전→근대→현대→미래).</summary>
    public enum TankKind
    {
        Catapult = 0, CrossBow = 1, Cannon = 2,                  // 고전
        Carrot = 3, Duke = 4, MineLander = 5,                    // 근대
        Missile = 6, MultiMissile = 7, SuperTank = 8,            // 현대
        Laser = 9, IonAttacker = 10, Poseidon = 11, SecWind = 12 // 미래
    }

    public enum TankEra { Classic = 0, Early = 1, Modern = 2, Future = 3 }

    /// <summary>날씨. 눈이 오면 포세이돈만 강해진다(원작 고유 능력).</summary>
    public enum Weather { Clear = 0, Snow = 1 }

    /// <summary>
    /// 수치로 표현되지 않는 고유 능력. **여기 있는 건 전부 실제로 읽는 코드가 있다** —
    /// 선언만 하고 안 쓰는 플래그는 추가하지 마라.
    /// </summary>
    [Flags]
    public enum TankTrait
    {
        None = 0,
        SnowBonus = 1,   // 포세이돈: 눈 → 피해·굴착 +25%   (WithCondition)
        LowHpRage = 2,   // 세크윈드: 체력 50% 이하 → 피해 +50% (WithCondition)
        Hidden = 4,      // 슈퍼탱크: 목록에서 고를 수 없다   (Selectable)
    }

    /// <summary>무기 슬롯(§8 WeaponController). 일반탄은 무한, 특수탄은 탄수 제한.</summary>
    public enum ShellKind { Normal = 0, Special = 1 }

    /// <summary>탱크 1종의 전투 수치. 기획서 §9-3 실루엣이 그대로 성능이 되게 한다.</summary>
    // ⚠️ 사거리 평준화(2026-09-17, 오너 지시 "기종마다 차이점이 있어야지").
    //    사거리는 `PowerScale² / GravityScale` 로 정해지는데, 172m(캐터펄트)~307m(레이저)로 **1.78배** 벌어져
    //    교전 거리(150m) 대비 여유가 곧 명중률이 되어 **사거리 하나가 승률을 지배**했다.
    //    실측: 교전 190m 로 늘렸더니 캐터펄트가 아예 못 닿아 7%, 미사일(276m)이 87% 로 갈렸다.
    //    사거리를 205~262m(1.28배)로 좁혀, 특수탄·설치물·속박·굴착 같은 **기종 고유 축이 승부에 드러나게** 한다.
    //    차이를 없애는 게 아니라 차이가 **여러 축으로 갈리게** 하는 조정이다.
    public struct TankStats
    {
        public TankKind Kind;
        public TankEra Era;
        public TankTrait Trait;
        public string Name;

        public int Hp;
        /// <summary>
        /// 방어력(원작 스탯). 받는 피해 = 원피해 × (100 / Defense).
        /// 검증: 원작 서술 "슈퍼탱크는 전 속성 피해 0.8배" = 100/125. 모델이 역산으로 맞는다.
        /// </summary>
        public float Defense;
        /// <summary>
        /// 기본 딜레이(원작 스탯, 530~580). 원작 턴 순서는 교대가 아니라 **누적 딜레이가 작은 쪽이 먼저**다.
        /// 탄종별 추가 딜레이가 여기에 더해진다(TurnOrder 참조).
        /// </summary>
        public int Delay;
        public float PowerScale;      // 초기속도 배율 — 사거리
        public float GravityScale;    // 중력 배율 — 최대 사거리와 비행시간
        public float WindScale;       // 바람 감도 배율 — 1 보다 작으면 옆바람에 덜 밀린다
        public float BlastRadius;     // 피해 반경
        public float CraterRadius;    // 지형을 파는 반경. ⚠️ 피해 반경과 **분리**한다(§2-5-1)
        public float BaseDamage;
        public float DirectDamage;
        public float MinPitch, MaxPitch;
        public float MoveSpeed;       // 이동 페이즈 속도 배율(원작의 "이동거리")

        // 특수탄(2번탄) 변형 배율. 탱크마다 **원래 잘하는 것을 극단으로** 민다.
        public string SpecialName;
        public float SpBlast, SpCrater, SpBase, SpDirect;

        /// <summary>비행 프로파일(추진·유도). For() 가 탄종에 맞춰 채운다 — Table 행에는 없다(기본 = 순수 포물선).</summary>
        public FlightProfile Flight;

        // (구) SpecialAmmo·SpecialUnlockRound 는 원작에 없는 내 발명이라 제거(§2-9). 2번탄 무한, 제한은 SS(NiceShot).

        // ── 로스터 표 ────────────────────────────────────────────────────────────
        // 기준점은 **캐롯탱크**(원작에서 초심자용 평준화 탱크)다. 나머지는 이 값 대비로 읽어라.
        //   Hp1000 / Power1.0 / Grav1.0 / Wind1.0 / 폭발7 / 굴착7 / 기본300 / 직격100 / -5~80° / 이동1.0
        //
        // ⚠️ MaxRange 가 교전 거리(스폰 간 149m)보다 작으면 그 탱크는 "밸런스가 나쁜" 게 아니라
        //    **게임에 참가하지 못한다.** 예전 박격포가 40판 전패로 가르쳐 줬다. verify.sh 의 [6-0] 이 강제로 막는다.
        static readonly TankStats[] Table = BuildTable();

        static TankStats Row(TankKind k, TankEra era, string name, int hp, float def, int delay,
                             float pow, float grav, float wind,
                             float blast, float crater, float bas, float dir,
                             float minP, float maxP, float move,
                             string spName, float spB, float spC, float spBase, float spDir,
                             TankTrait trait = TankTrait.None)
            => new TankStats
            {
                Kind = k, Era = era, Trait = trait, Name = name, Hp = hp, Defense = def, Delay = delay,
                PowerScale = pow, GravityScale = grav, WindScale = wind,
                BlastRadius = blast, CraterRadius = crater,
                BaseDamage = bas, DirectDamage = dir,
                MinPitch = minP, MaxPitch = maxP, MoveSpeed = move,
                SpecialName = spName, SpBlast = spB, SpCrater = spC, SpBase = spBase, SpDirect = spDir,
            };

        // ── 원작 수치 반영 규칙 ──────────────────────────────────────────────────
        // 출처: 나무위키 포트리스2 개별 탱크 문서(2026-09-15 교차 검증). [추정] 표기는 문서에 없어 내가 채운 값.
        //   체력·방어력·각도·딜레이 — 원작 그대로
        //   공격력(BaseDamage)     — 원작 1번탄 공격력 그대로
        //   폭발 반경              — 원작 폭발범위 × 0.146 (원작 48 ≈ 기준 7m)
        //   이동                   — 원작 이동거리 / 55
        //   PowerScale·Gravity·Wind·굴착·직격 — 원작에 없는 3D 축. 내 설계. 매치업으로 조정한다.
        //
        // ⚠️ 각도 상한이 45° 미만인 탱크(캐롯 40, 레이저 36 등)는 이론 최대사거리를 못 낸다.
        //    MaxRange 가 이를 반영한다. [6-0] 참가 자격 검사가 이 값으로 막는다.
        static TankStats[] BuildTable()
        {
            var t = new TankStats[13];

            // ── 고전 ── 화력 높고 각도 넓고, 방어·이동이 나쁘다 (원작 서술 그대로)
            t[0] = Row(TankKind.Catapult, TankEra.Classic, "캐터펄트", 950, 100f, 560,
                       1.03f, 1.15f, 1.10f, 6.6f, 8.0f, 300f, 90f, 0f, 90f, 0.91f,
                       "화염바위", 0.88f, 1.30f, 0.80f, 0.60f);
            //   원작: 1번 바위(300/폭발45), 2번 불덩어리(폭발40, 착지점 지속불 — 지속피해 미구현)
            t[1] = Row(TankKind.CrossBow, TankEra.Classic, "크로스보우", 940, 99f, 570,
                       1.05f, 1.00f, 0.85f, 4.5f, 6.0f, 330f, 270f, 15f, 80f, 1.04f,
                       "독화살", 0.90f, 0.80f, 0.30f, 0.50f);
            //   ⚠️ 굴착 4.5→6.0 · 직격 200→270 (2026-09-17, AI 이동 도입 후 40판/칸 실측).
            //     승률 17~38% 로 줄곧 최하위였는데 **명중률은 75% 로 1위**였다 — 잘 쏘는데 지는 판이었다.
            //
            //     ⚠️ 원인을 두 번 잘못 짚었다. 스윕이 둘 다 뒤집었다 — 가설로 고치지 말고 재라:
            //       ① "2번탄(독화살)을 판당 1.7 회밖에 안 써서" → SpBase 0.30/0.45/0.60 → 38 · 38 · 37%.
            //          **승률이 안 움직인다.** 2번탄을 더 쓰긴 하는데(2.0→2.6) 그게 이기는 길이 아니었다.
            //          AI 는 옳게 고르고 있었다(1번탄 정타 670 vs 2번탄 269+독 60 — 2배 차는 못 메운다).
            //       ② "정타 보상이 작아서" → 굴착 6.0 에서 직격 200/270/340 → 39 · 46 · 48%.
            //          듣기는 하지만 직격 혼자서는 39% 를 못 벗어난다.
            //
            //     진짜 결손은 **낙하 피해**였다(§28). 굴착 4.5m 가 13종 최하위라 한 판 낙하 피해가 45 뿐이었다
            //     (다른 기종은 300~1000). 굴착 4.5/6.0/7.0 → 37 · 48 · 56%, 낙하 피해 46→234→360 으로
            //     단조롭게 따라갔다. 7.0 은 과해서 6.0 이 중앙이다. 직격은 340 이 48%, 270 이 46% 라
            //     **원작(200)에서 덜 벗어나는 270** 을 택했다(2%p 값으로 [추정] 폭을 줄인다).
            //     폭발 4.5m(원작 31, 최하위)는 정체성이라 그대로 둔다 — 굴착은 원작에 없는 [추정] 값이고
            //     캐터펄트 6.6/8.0 처럼 굴착>폭발은 흔하다. 작살이 지면을 깊게 뚫는 그림도 어색하지 않다.
            //   원작: 1번 창(330/폭발31), 2번 독화살(100/폭발28 + 지속피해 — 미구현이라 일단 약한 탄)
            t[2] = Row(TankKind.Cannon, TankEra.Classic, "캐논", 920, 104f, 580,
                       1.00f, 1.00f, 1.00f, 11.4f, 9.0f, 140f, 60f, 25f, 55f, 0.87f,
                       "빨콩", 0.42f, 0.55f, 3.00f, 3.00f);
            //   ★ 원작: 1번 검콩은 **가장 넓고(78) 약하다(140)**. 2번 빨콩은 420 으로 전 무기 최강.
            //     방어 89 로 전 탱크 최약 — "선택과 집중형".
            //   ⚠️ 3D 적응(2026-09-17, 오너 지시 "캐논 고쳐"): 방어 89→104 · 각도 상한 45→55.
            //     실측으로 병목을 가렸다(§2-9-5 방식, 단일 변수 각 40판):
            //       방어  89→22% · 100→31% · 112→39%   ← 가장 강한 축
            //       각도  45→22% · 55→24% · 65→30%
            //       딜레이 580→22% · 550→27% · 530→29%
            //     즉 캐논은 **못 맞춰서가 아니라 먼저 죽어서** 진다(명중 75%·피해/명중 462 는 상위권인데 평균 14턴).
            //     원작의 "최약 방어" 정체성은 남기되(104 는 여전히 캐논이 하위권) 함정(⬇ 38% 미만)에서는 빼낸다.
            //     각도는 듀크와 같은 3D 적응이다(§2-9-8: 2D 평지 기준 각도가 3D 언덕에서 탄도를 막는다).

            // ── 근대 ── 평준화, 방어 두껍다
            t[3] = Row(TankKind.Carrot, TankEra.Early, "캐롯탱크", 1000, 110f, 560,   // 방어·공격 [추정]
                       1.00f, 1.00f, 1.00f, 7.0f, 7.0f, 250f, 100f, 0f, 40f, 1.00f,
                       "삼연포탄", 1.15f, 1.40f, 0.45f, 0.80f);
            //   원작: 2번 녹색 소형 포탄 3발 — 지형파괴 우수. 다탄두 Fan(3) 이 실제로 3발을 쏘므로 SpBase 는 **발당** 배율이다.
            //   ⚠️ 0.95 는 다탄두 없던 시절 "3발 합"을 한 발에 넣은 값 — Fan(3) 이 들어온 뒤엔 3배 중복이라 승률 80%(1위).
            //      실측(§2-9-5): 0.95→80% · 0.60→60% · 0.40→46%. 0.45 채택(발당 112, 3발 합 337 ≈ 1번 250 × 1.35).
            t[4] = Row(TankKind.Duke, TankEra.Early, "듀크탱크", 1000, 123f, 560,
                       1.00f, 0.95f, 1.00f, 8.0f, 6.5f, 240f, 110f, 0f, 55f, 1.09f,
                       "독구름", 0.78f, 0.60f, 0.62f, 0.50f);
            //   원작: 1번 240/폭발55, 2번 독구름 150/폭발43 바람에 휨 + 지속피해.
            //   3D 지형 적응: 원작 0~40°는 2D 평지 기준이라 3D 언덕 맵에서 탄도가 차단됨(승률 29%) → 55°로 완화(§2-9-5 실험6).
            t[5] = Row(TankKind.MineLander, TankEra.Early, "마인랜더", 1150, 120f, 560,   // 방어·공격 [추정]
                       0.98f, 1.00f, 1.05f, 6.0f, 8.5f, 200f, 80f, 5f, 40f, 1.00f,
                       "지뢰탄", 0.85f, 1.70f, 0.50f, 0.50f);
            //   원작: 체력 1150 전 탱크 최고. 2번 지뢰 설치(밟으면 최고 피해 — 설치물 미구현)

            // ── 현대 ── 화력·방어 높고 사거리 길고 화력이 분산
            t[6] = Row(TankKind.Missile, TankEra.Modern, "미사일", 1100, 117f, 560,
                       1.07f, 1.00f, 1.15f, 6.6f, 7.5f, 300f, 70f, 20f, 45f, 1.04f,
                       "유도탄", 0.70f, 0.80f, 0.30f, 0.60f);
            //   원작: 1번 4단 폭발(120-90-60-30), 2번 유도탄 90/폭발32 — 약한 특수탄. "강한 발사력"
            t[7] = Row(TankKind.MultiMissile, TankEra.Modern, "멀티미사일", 1050, 115f, 570,
                       1.06f, 1.00f, 1.20f, 5.8f, 8.0f, 175f, 40f, 25f, 50f, 1.00f,
                       "구연장", 0.70f, 0.90f, 0.34f, 0.40f);
            //   ⚠️ 원작은 1번 175×3 부채꼴, 2번 60×9. 멀티샷 미구현이라 단발 175 로 두면 **실제보다 약하다.**
            t[8] = Row(TankKind.SuperTank, TankEra.Modern, "슈퍼탱크", 1000, 125f, 550,
                       1.15f, 1.00f, 0.90f, 6.6f, 8.0f, 325f, 150f, 20f, 45f, 1.09f,
                       "구연유도", 0.70f, 0.90f, 0.25f, 0.40f, TankTrait.Hidden);
            //   ★ 원작: 체력 1000 으로 평범. 진짜 강점은 **방어 125(전 속성 0.8배)** + 3연 미사일.
            //     "랜덤에서만 낮은 확률로 등장"이라 선택 불가.

            // ── 미래 ── 화력·이동 높고 방어 낮다. 탱크마다 고유 능력
            t[9] = Row(TankKind.Laser, TankEra.Future, "레이저탱크", 990, 90f, 540,   // 방어·이동 [추정]
                       1.04f, 0.90f, 0.55f, 4.0f, 4.0f, 200f, 135f, 5f, 36f, 1.04f,
                       "회전레이저", 1.00f, 1.00f, 0.75f, 1.60f);
            //   ★ 원작: 저각형(5~36°), 딜레이 540 으로 빠름. 2번 3연 회전 레이저(풀히트 시 고피해)
            //   ⚠️ 직격 220 은 원작 수치가 아니라 [추정]이었고, 3D 에서 **압도적 1위**를 만들었다
            //      (2026-09-17 실측 12종 매치업: 평균 72~75% ⬆, 피해/명중 777 로 전체 최고, 평균 10턴으로 최단).
            //      단일 변수 실측: 220→75% · 160→67% · 135→약58% · 120→58%.
            //      135 로 내린다 — 저각·장사거리·빠른 딜레이라는 원작 정체성은 그대로 두고 **한 방 크기만** 깎는다.
            t[10] = Row(TankKind.IonAttacker, TankEra.Future, "이온어태커", 970, 91f, 550,
                        1.00f, 1.00f, 1.00f, 7.0f, 6.5f, 200f, 110f, 20f, 55f, 1.27f,
                        "위성탄", 1.38f, 1.30f, 1.35f, 0.80f);
            //   ⚠️ 굴착 7.5→6.5 (2026-09-17). 낙하 피해를 낮추자(§28) 굴착이 보상은 줄고 자해는 그대로라
            //     32% 로 무너졌다. 명중률 46% 로 꼴찌인 게 증거다 — 자기가 판 구덩이에 적이 숨는다.
            //     10→7.5 로 한 번 줄여 39%까지 올렸던 것과 같은 방향의 한 걸음이다.
            //   ⚠️ 굴착은 **자기 명중률을 깎는 양날**이다(§2-5-1 나선). 크게 팔수록 적이 자기가 판 구덩이에 숨어 안 보인다.
            //      옛 실측(§2-9-5, 아이템·보급·궁극기 도입 전): 8→66% · 10→50% · 11→29%.
            //      재실측(2026-09-17, 현행 시스템 전부 켠 상태): **10→21%(명중 40%) · 7→41%(명중 62%) · 5→44%**.
            //      시스템이 늘면서 같은 굴착이 더 해로워졌다 — 10 은 이제 꼴찌를 만드는 값이라 7.5 로 내린다.
            //      7.5 여도 마인랜더(8.5) 다음가는 상위 굴착이라 "지형 완전 파괴" 정체성은 남는다.
            //   ⚠️ 굴착 상수를 바꿨으면 **명중률 열**을 같이 봐라. 승률만 보면 원인을 못 짚는다.
            //   ★ 원작: 고각형(20~55°), "정타만 해도 지형 완전 파괴". 2번 위성탄 270/폭발66 —
            //     X축 착지점 **위에서 수직 낙하**(오버행 무시). 수직 낙하 미구현이라 일단 넓고 센 탄.
            t[11] = Row(TankKind.Poseidon, TankEra.Future, "포세이돈", 1000, 105f, 550,
                        1.02f, 1.00f, 0.95f, 7.9f, 7.0f, 230f, 100f, 15f, 50f, 0.98f,
                        "속박수탄", 0.80f, 0.90f, 0.83f, 0.80f, TankTrait.SnowBonus);
            //   원작: 1번 230/폭발54, 2번 190×2 + **대상 이동금지 2턴**(미구현). 눈 오면 위력·폭발 +25%
            t[12] = Row(TankKind.SecWind, TankEra.Future, "세크윈드", 985, 92f, 530,
                        1.02f, 1.05f, 1.00f, 9.5f, 6.5f, 220f, 110f, 0f, 40f, 1.45f,
                        "회전에너지", 0.65f, 0.80f, 0.77f, 1.00f, TankTrait.LowHpRage);
            //   ★ 원작: **이동 80 전 탱크 최장**, 딜레이 530 최저. 세크파워 = 체력 50% 이하 공격 +50%.
            //     2번 회전 에너지탄 170×2(풀히트 쉬움)
            return t;
        }

        /// <summary>순수 기본 수치. 조건부 능력·특수탄은 <see cref="For"/> 가 얹는다.</summary>
        public static TankStats Get(TankKind k) => Table[(int)k];

        public static int Count => 13;

        /// <summary>선택 가능한 탱크(슈퍼탱크 제외). 매치업 행렬도 이걸 쓴다.</summary>
        public static TankKind[] Selectable()
        {
            var list = new System.Collections.Generic.List<TankKind>();
            for (int i = 0; i < Table.Length; i++)
                if ((Table[i].Trait & TankTrait.Hidden) == 0) list.Add(Table[i].Kind);
            return list.ToArray();
        }

        public static TankEra EraOf(TankKind k) => Table[(int)k].Era;
        public static string EraName(TankEra e)
            => e == TankEra.Classic ? "고전" : e == TankEra.Early ? "근대" : e == TankEra.Modern ? "현대" : "미래";

        /// <summary>
        /// **최종 수치를 만드는 유일한 입구.** 기본값 → 특수탄 → 조건부 능력 순으로 얹는다.
        ///
        /// ⚠️ 호출부가 넷(하네스·데모 두 곳·AI)인데 각자 조합하면 조금씩 어긋난다.
        ///    포세이돈의 눈 보정을 한 곳에서 빠뜨리면 "AI 는 세게 쏘는데 화면은 약하게 터지는" 버그가 된다.
        ///    그래서 조합은 여기서만 한다(CLAUDE.md: 같은 로직이 여러 곳에 살면 재발한다).
        /// </summary>
        public static TankStats For(TankKind k, ShellKind shell = ShellKind.Normal,
                                    float hpFrac = 1f, Weather weather = Weather.Clear)
            => Get(k).WithShell(shell).WithCondition(hpFrac, weather).WithFlight(shell);

        /// <summary>탄종별 비행 프로파일을 끼운다. AI·시뮬·뷰·하네스가 전부 st.Flight 를 보므로 경로가 하나다.</summary>
        public TankStats WithFlight(ShellKind shell)
        {
            var s = this;
            s.Flight = FlightProfile.Of(Kind, shell, PowerScale);
            return s;
        }

        /// <summary>
        /// 특수탄을 끼운 상태의 수치. **TankStats 를 변형해 돌려주므로 하류(AI·시뮬·뷰)가 그대로 동작한다** —
        /// 탄종마다 발사 경로를 따로 만들면 세 벌이 조금씩 어긋난다(§4-1 이중 구현 금지).
        /// </summary>
        public TankStats WithShell(ShellKind shell)
        {
            if (shell == ShellKind.Normal) return this;
            var s = this;
            s.BlastRadius = BlastRadius * SpBlast;
            s.CraterRadius = CraterRadius * SpCrater;
            s.BaseDamage = BaseDamage * SpBase;
            s.DirectDamage = DirectDamage * SpDirect;
            s.Name = SpecialName;
            return s;
        }

        /// <summary>
        /// 조건부 고유 능력. 원작 서술을 그대로 옮긴 것만 있다.
        ///   포세이돈: "눈이 오면 데미지와 지형파괴가 25% 증가"
        ///   세크윈드: "체력이 50% 이하가 되면 데미지가 50% 증가"
        /// </summary>
        public TankStats WithCondition(float hpFrac, Weather weather)
        {
            var s = this;
            if ((Trait & TankTrait.SnowBonus) != 0 && weather == Weather.Snow)
            {
                s.BaseDamage *= 1.25f; s.DirectDamage *= 1.25f; s.CraterRadius *= 1.25f;
            }
            if ((Trait & TankTrait.LowHpRage) != 0 && hpFrac <= 0.5f)
            {
                s.BaseDamage *= 1.5f; s.DirectDamage *= 1.5f;
            }
            return s;
        }

        /// <summary>
        /// 45°에서의 이론 최대 사거리(m). **매치업을 짜기 전에 이 값이 교전 거리보다 큰지 반드시 확인하라.**
        /// 작으면 그 탱크는 밸런스가 나쁜 게 아니라 게임에 참가하지 못한다.
        /// </summary>
        public float MaxRange
        {
            get
            {
                float v = Ballistics.VelocityMax * PowerScale;
                // 45° 를 못 올리는 탱크(캐롯 40°, 레이저 36°)는 sin(2θ) 만큼 덜 나간다
                float bestDeg = MathF.Min(45f, MaxPitch);
                float s2 = MathF.Sin(2f * bestDeg * MathF.PI / 180f);
                return v * v * s2 / (Ballistics.Gravity * GravityScale);
            }
        }

        /// <summary>이 탱크의 파워→초기속도.</summary>
        public float SpeedAt(float power01) => Ballistics.PowerToSpeed(power01) * PowerScale * Flight.Mul;   // 추진 탄은 초기속도를 깎는다(FlightProfile.SpeedMul)

        /// <summary>이 탱크의 가속도(중력 배율 반영 + 바람).</summary>
        public Vec3 AccelWith(float windX, float windZ)
            => new Vec3(windX * Ballistics.WindCoeff * WindScale,
                        -Ballistics.Gravity * GravityScale,
                        windZ * Ballistics.WindCoeff * WindScale);

        /// <summary>
        /// 사거리 R 을 45°로 쏠 때의 비행시간.
        ///
        /// ⚠️ **중력이 클수록 비행이 짧아진다.** v² = R·g 이므로 t = √(2R/g) 다.
        ///    "가파른 포물선이라 오래 난다"는 직관은 틀렸다 — 45°에서 궤적 정점은 R/4 로
        ///    **중력과 무관**하다. GravityScale 이 실제로 바꾸는 것은 최대 사거리와 비행시간뿐이다.
        ///    바람 편차는 ½·a·t² 이므로 비행시간이 곧 바람 취약성이다.
        /// </summary>
        public float FlightTimeAt(float range)
            => MathF.Sqrt(2f * range / (Ballistics.Gravity * GravityScale));
    }
}
