// 명세: docs/GAME_SPEC_TANK_ARTILLERY.md — M1+M3 통합 플레이
//
// 3 vs 3 턴제 포격전. Play 만 누르면 씬 없이 전부 코드가 만든다.
//
// 들어간 것: 탄도 해석해(§5) · 파워 제곱근 매핑(§5-2) · 바람(§5-3) · 조준 역산 AI(§5-7)
//            피해 감쇠(§6-1) · 직격 보너스(§6-2) · 지형 파괴(§7) · 천장 붕괴(§7-6-1) · 턱 넘기(§7-6-2)
// 아직 없는 것: 궁극기 · 특수탄 · 탱크별 능력치 · 이동 게이지 정밀 규칙 · 네트워크

using System.Collections.Generic;
using UnityEngine;
using Tankfall.Sim;

namespace Tankfall.View
{
    public sealed partial class BattleDemo : MonoBehaviour
    {
        // --- 지형 (§7-1) ---
        // ⚠️ 맵 크기는 `MapHeightFunction.MapSize` 가 **단일 소스**다. 여기에 숫자를 적으면
        //    지형과 게임이 다른 크기를 믿게 되어 탱크가 허공에 뜨거나 맵 밖에서 싸운다.
        const float MapSize = MapHeightFunction.MapSize;
        const float Voxel = 0.5f, OriginY = -20f;
        const int ChunkN = 16;

        // --- 전투 ---
        const int MaxHp = 1000;
        const float BlastRadius = 7f;        // 일반탄(§2-4)
        const float BaseDamage = 300f, DirectDamage = 100f;
        const float TankRadius = 2.0f;
        const float MoveGaugeMax = 100f;

        // §2-1 페이즈 시간. 합 27초가 턴 상한이고, 대부분 이동을 스킵해 평균 15~18초가 된다
        // — 자동 대전에서 쓴 18초/턴(§2-5) 이 이 값에서 나온다.
        const float MovePhaseSec = 12f;
        const float FirePhaseSec = 15f;

        /// <summary>접지 시각 보정(m). Surface Nets 정점은 셀 안 교차점 평균이라
        /// 렌더 표면이 SDF 0면보다 살짝 안쪽이다 — 그대로 앉히면 궤도가 지형에 잠겨 보인다.
        /// 물리(이동·충돌)에는 쓰지 않는다. 순수 시각 보정이다.</summary>
        const float GroundVisualLift = 0.18f;

        // --- 조작 ---
        const float DriveSpeed = 8f, TurnSpeed = 70f;
        const float TurretSpeed = 70f, BarrelSpeed = 35f;
        const float MinElev = -5f, MaxElev = 80f;
        const float ChargeRate = 0.75f;      // 파워 게이지 왕복 속도

        /// <summary>
        /// §2-1 확정안 — **2페이즈 턴**. 3D 에서는 한 턴에 카메라 회전·이동·포탑 정렬·피치·차징이
        /// 전부 들어가 원작 포트리스의 20초가 성립하지 않는다. 그래서 이동과 조준을 갈랐다:
        ///
        ///   MOVE(12초) 이동 + 자유 카메라 → FIRE(15초) 카메라 자동 스냅 + 조준 + 발사
        ///
        /// FIRE 진입 시 카메라가 포신 뒤로 스냅되므로 **조준 시간에서 카메라 조작 비용이 사라진다**.
        /// 그게 이 분할의 목적이다. 되돌릴 수 없는 전환인 이유도 같다 — 되돌아갈 수 있으면
        /// 이동/조준을 오가며 시간을 다 쓴다.
        /// </summary>
        enum Phase { Move, Fire, Flying, Resolve, AiThink, GameOver }

        sealed class Unit
        {
            public int Id, Team, Hp = MaxHp, HpMax = MaxHp;
            public TankKind Kind;
            public Weather W = Weather.Clear;
            public ShellKind Shell = ShellKind.Normal;
            public SkillGauge Skill;   // 나이스샷 포인트. 원작: 1·2번탄 무한, 제한은 SS 뿐
            public float HpFrac => HpMax > 0 ? Hp / (float)HpMax : 1f;
            /// <summary>
            /// 탄종 + **현재 상황**까지 반영한 수치. 조준·발사·피해가 전부 이걸 본다.
            /// ⚠️ 세크윈드는 체력이 절반 밑으로 내려가면 피해가 1.5배가 되고 포세이돈은 눈이 오면 세진다 —
            ///    그래서 이건 고정값이 아니라 **매번 다시 만드는 값**이다. 캐싱하면 그 능력이 죽는다.
            /// </summary>
            public TankStats St => TankStats.For(Kind, Shell, HpFrac, W);
            public Transform Root, Turret, Barrel, Fire;
            public float Heading, TurretYaw, BarrelPitch = 45f, Gauge = MoveGaugeMax;
            public bool Alive => Hp > 0;
            public float Jolt;         // 피격 흔들림 잔량(연출) — GroundUnit 이 줄인다
            public Vector3 Pos => Root.position;
            public Vec3 Center => new Vec3(Root.position.x, Root.position.y + 1.2f, Root.position.z);
        }

        /// <summary>격파 — 탱크를 숨기고 그 자리에 화구·연기 기둥(ParticleFx.Death). 죽음 처리는 전부 여기로 — 흩어져 있으면 연출이 빠진다.</summary>
        void KillUnit(Unit u)
        {
            if (u.Root.gameObject.activeSelf)
            {
                EnsureFx().Death(u.Root.position, TankShape.BodyColor(u.Kind));
                // 포탑이 통째로 튀어 오른다(재미요소, 연출 전용). 원본은 숨기고 복제본만 날린다 — 판정용 계층은 건드리지 않는다.
                if (u.Turret != null)
                {
                    var pop = Instantiate(u.Turret.gameObject, u.Turret.position, u.Turret.rotation);
                    pop.name = "TurretWreck";
                    pop.AddComponent<WreckPiece>().Launch(new Vector3(Random.Range(-3f, 3f), Random.Range(11f, 16f), Random.Range(-3f, 3f)),
                                                          new Vector3(Random.Range(-240f, 240f), Random.Range(-120f, 120f), Random.Range(-240f, 240f)), 4.5f);
                }
            }
            u.Root.gameObject.SetActive(false);
        }

        ParticleFx EnsureFx()
        {
            if (_fx == null) _fx = new GameObject("ParticleFx").AddComponent<ParticleFx>();
            return _fx;
        }

        /// <summary>독·속박 상태 파티클을 Sim 상태와 맞춘다. 턴이 바뀌거나 상태가 걸릴 때 부른다.</summary>
        void RefreshStatusFx()
        {
            var fx = EnsureFx();
            foreach (var u in _units)
                fx.SetUnitStatus(u.Id, u.Root, u.Alive && _status.HasDot(u.Id), u.Alive && !_status.CanMove(u.Id));
        }

        bool _headless;              // 자동 검증 모드 — 슬로모 같은 시간 연출은 끈다(프레임 수로 도는 하네스를 흔들지 않기 위해)
        float _slowUntil = -1f;      // 직격·격파 슬로모 종료 시각(unscaled)

        /// <summary>
        /// 재미요소(오너 지시 2026-09-17): 직격·격파 순간 0.45초 슬로모. 규칙이 아니라 **연출**이다 —
        /// Time.timeScale 만 낮추므로 탄도·피해·턴은 그대로다. 자동 검증 모드에서는 안 켠다.
        /// </summary>
        void HitStop(float scale, float sec)
        {
            if (_headless) return;
            Time.timeScale = scale;
            _slowUntil = Time.unscaledTime + sec;
        }

        SdfVolume _vol;
        TerrainView _terrain;
        Scatter _scatter;              // 나무·바위·덤불(Scatter.cs) — 거리 감각과 폭발 가독성 때문에 둔다
        Environment _env;              // 구름·원경·해(Environment.cs) — 빈 하늘은 원작에도 없었다
        ParticleFx _fx;                // 폭발·자취 연출(§11 M2). 없으면 착탄이 아무 무게 없이 지나간다
        Camera _cam;
        readonly List<Unit> _units = new List<Unit>();
        int _turn;                       // _units 인덱스
        Phase _phase = Phase.Move;

        /// <summary>
        /// 원작 딜레이 턴제(§2-9). 교대가 아니라 누적 딜레이가 작은 유닛이 먼저다 —
        /// 세크윈드(530)가 캐논(580)보다 판 전체에서 약 9% 더 많이 쏜다.
        /// 동률은 등록 순서이므로 **팀 교대 순서로 등록**해야 한다(195행 정렬 뒤에 등록).
        /// </summary>
        TurnOrder _order;
        float _power, _chargeDir = 1f;
        bool _charging;
        /// <summary>나이스샷 표시점(원작: 게이지 위에 미리 찍어 두는 녹색 선). Q/E 로 옮긴다.</summary>
        float _mark = 0.5f;
        bool _useSs;              // 3번 키 — 이번 사격을 SS(강화 2번탄)로
        bool _useUlt;             // 4번 키 — 이번 사격을 궁극기(§49)로
        string _niceFlash;        // "나이스샷!" 표시
        readonly StatusEffects _status = new StatusEffects();   // 독·화상·속박
        readonly HazardField _hazards = new HazardField();      // 지뢰·지속불
        readonly List<(Vec3 impact, int direct, float scale)> _pendingShots = new List<(Vec3, int, float)>();
        TankKind _shooterKind; ShellKind _shooterShell;
        bool _shooterUlt;         // 이번 사격이 궁극기(§49, 핵)인가 — 폭발 연출이 버섯구름으로 바뀐다
        TankKind _shellVisualKind = (TankKind)(-1);   // 지금 만들어 둔 탄 메시가 어느 기종 것인가
        ShellKind _shellVisualShell;
        ShellTrail.Style _trailStyle;                 // 지금 날아가는 탄의 자취 스타일
        GuidanceProfile _guide;                       // 지금 날아가는 탄의 초기 유도·자세 제어 단계(없으면 default)

        /// <summary>
        /// 탄 비주얼 배율. 1.5 (2026-09-17, 오너 지시 "미사일 크기 1.5배 크게").
        /// 150m 교전에서 탄이 작아 **무엇이 날아오는지** 보이지 않았다 — 모양·색·자취로 기종을 가르기로 했는데
        /// 정작 알갱이가 작으면 그 세 채널이 전부 무의미해진다.
        /// ⚠️ 연출 전용이다. 명중 판정은 `TankRadius`·SDF 레이마칭이 하므로 여기를 키워도 탄도·피해는 안 변한다.
        /// </summary>
        const float ShellScale = 1.5f;
        Vector2 _wind;                   // 수평 방향 × 세기

        /// <summary>
        /// 난이도 사다리(§2-9-8 로 "강한 쪽이 실제로 이긴다" 검증됨). `-difficulty` 로 고르고,
        /// 게임 중에는 F1 으로 순환한다. 기본은 중급(명중 83% · 6.2분, TwinHills 실측).
        /// ⚠️ 상수 자체를 만지지 마라 — 판 길이를 늘리는 손잡이는 지형을 갈아 실력 변별력을 지운다(AiGunner 머리말).
        /// </summary>
        static readonly (string Name, float Err)[] Difficulties =
        {
            ("초급", AiGunner.ErrorNovice),
            ("중급", AiGunner.ErrorNormal),
            ("상급", AiGunner.ErrorExpert),
            ("에이스", AiGunner.ErrorAce),
        };
        int _difficulty = 1;   // 중급
        float AiDifficulty => Difficulties[_difficulty].Err;
        Rng _aiRng = new Rng(0xA17C0DEu);   // 결정론 — UnityEngine.Random 은 리플레이를 깬다
        int _round = 1;

        /// <summary>
        /// 기본 편성 3종. 12종 중 **서로 가장 다른 셋**을 골랐다 —
        /// 캐논(고전·직격), 캐롯(근대·기준), 레이저(미래·평사).
        /// 셋 다 비슷한 걸 고르면 종을 나눈 게 화면에서 안 보인다.
        /// </summary>
        static readonly TankKind[] DefaultRoster = { TankKind.Cannon, TankKind.Carrot, TankKind.Laser };

        /// <summary>판 전체 날씨. 눈이면 포세이돈만 강해진다(원작 고유 능력).</summary>
        /// ⚠️ 이건 한 번 고정하고 마는 값이 아니다 — 반드시 `SetWeather()` 로만 바꿔라.
        ///    유닛이 자기 사본(`Unit.W`)을 들고 있어서 직접 대입하면 이미 생성된 탱크가 옛 날씨로 남는다.
        Weather _weather = Weather.Clear;
        /// <summary>`-weather clear|snow` 로 고정했는가. 안 주면 판 시작에 25% 확률로 눈[추정].</summary>
        Weather? _weatherForced;
        /// <summary>눈이 올 확률. 원작 기획서에 빈도 규칙이 없어 내가 정한 값이다 — [추정].</summary>
        const float SnowChance = 0.25f;
        string _log = "";
        float _camYaw = 180f, _camPitch = 20f, _camDist = 26f;
        float _phaseTimer;
        List<Vec3> _shotPath;
        float _shotT;
        Transform _shell;
        // 다탄두 연출: 중앙 탄 외의 부탄은 각자 궤적을 따라 같은 시각에 날아간다(카메라는 중앙 탄만 따른다)
        readonly List<List<Vec3>> _subPaths = new List<List<Vec3>>();
        readonly List<Transform> _subShells = new List<Transform>();
        Transform _beam;          // 위성탄: 하늘에서 착탄점으로 꽂히는 수직 빔(원작 "위성 레이저")
        // 설치물 연출: 지뢰 = 검은 공, 지속불 = 주황 원반, 독구름 = 녹색 원반. 판정(HazardField)은 Sim 이 하고 여기선 스냅샷만 그린다.
        readonly List<HazardField.HazardView> _hazardBuf = new List<HazardField.HazardView>();
        readonly List<Transform> _hazardGos = new List<Transform>();
        Material _mineMat;
        float _beamTimer;
        TankKind[] _roster = DefaultRoster;
        bool _rosterFixed;
        /// <summary>사람이 없는 모드(자동사격·갤러리·성능·자체검사)인가. 고르기 화면 같은 대화형 UI 는 이걸 본다.</summary>
        bool _autoMode;               // -roster 로 지정됐으면 고르기 화면을 띄우지 않는다
        // ── 탱크 고르기(§2-9-17) ──
        bool _picking;
        readonly List<TankKind> _pick = new List<TankKind>();
        /// <summary>랜덤 뽑기에서 슈퍼탱크가 나올 확률 [추정 — 아래 주석 참조].</summary>
        const float SuperTankChance = 0.06f;   // -roster Cannon,Carrot,Laser 로 바꿀 수 있다(연출 확인용)
        MapKind _map = MapKind.TwinHills;
        bool _forceSpecial;                    // -forcespecial: AI 가 항상 2번탄 — 위성탄·독구름 같은 연출을 확인할 때만
        bool _forceUlt;                        // -forceult: 매 발 궁극기(핵) — 버섯구름 확인용. 나이스샷 문턱을 건너뛴다
        Vec3 _pendingImpact;
        int _pendingDirect = -1;
        string _winner;

        // 자동 스크린샷(배치 검증)
        bool _autoShot; int _frame; string _shotDir;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindFirstObjectByType<BattleDemo>() != null) return;
            new GameObject("[Tankfall Battle]").AddComponent<BattleDemo>();
        }

        float Height(float x, float z) => MapHeightFunction.Height(_map, x, z);

        Unit Current => _units.Count == 0 ? null : _units[Mathf.Clamp(_turn, 0, _units.Count - 1)];
        bool IsPlayerTurn => Current != null && Current.Team == 0;

        void Start()
        {
            Application.targetFrameRate = 60;
            // ⚠️ 빌드된 플레이어는 포커스를 잃으면 Update 가 멈춘다. 자동 검증(-phasecheck)은
            //    분 단위로 도는데 창이 뒤로 가면 그대로 정지해 "멈춤"으로 오진하게 된다(실제로 겪었다).
            Application.runInBackground = true;
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-autoshot") { _autoShot = true; _autoMode = true; }
                else if (args[i] == "-perf") { _perf = true; _autoMode = true; }
                else if (args[i] == "-timeevents") _timeEvents = true;
                else if (args[i] == "-phasecheck") _phaseCheck = true;
                else if (args[i] == "-gallery") { _gallery = true; _autoMode = true; }
                else if (args[i] == "-forcespecial") _forceSpecial = true;
                else if (args[i] == "-forceult") { _forceSpecial = true; _forceUlt = true; }
                else if (args[i] == "-map" && i + 1 < args.Length)
                {
                    if (!MapHeightFunction.TryParse(args[i + 1], out _map))
                        Debug.LogWarning($"[Tankfall] -map 모르는 이름 '{args[i + 1]}' — TwinHills");
                }
                else if (args[i] == "-shotdir" && i + 1 < args.Length) _shotDir = args[i + 1];
                else if (args[i] == "-items" && i + 1 < args.Length)
                {
                    if (!int.TryParse(args[i + 1].Trim(), out _itemSlots) || _itemSlots < 0)
                    { _itemSlots = 2; Debug.LogWarning($"[Tankfall] -items 는 0 이상 정수 — 기본 2"); }
                }
                else if (args[i] == "-uiselftest") _uiSelfTest = true;
                else if (args[i] == "-shellcheck") _shellCheck = true;
                else if (args[i] == "-shellgallery") _shellGallery = true;
                else if (args[i] == "-practice") _practice = true;
                else if (args[i] == "-practiceselftest") { _practice = true; _practiceSelfTest = true; }
                else if (args[i] == "-supplyselftest") { _supplySelfTest = true; _autoMode = true; }
                else if (args[i] == "-ultselftest") { _ultSelfTest = true; _autoMode = true; }
                else if (args[i] == "-impairselftest") { _impairSelfTest = true; _autoMode = true; }
                else if (args[i] == "-climateselftest") { _climateSelfTest = true; _autoMode = true; }
                else if (args[i] == "-boom") _boom = true;                     // Boom 모드(§2-9-16)
                else if (args[i] == "-boomselftest") { _boom = true; _boomSelfTest = true; _autoMode = true; }
                else if (args[i] == "-rosterselftest") { _rosterSelfTest = true; _autoMode = true; }
                else if (args[i] == "-gameselftest") { _gameSelfTest = true; _autoMode = true; }   // 음악·설정 저장·전적·무승부
                else if (args[i] == "-difficulty" && i + 1 < args.Length)
                {
                    int found = -1;
                    for (int d = 0; d < Difficulties.Length; d++)
                        if (string.Equals(Difficulties[d].Name, args[i + 1].Trim(), System.StringComparison.OrdinalIgnoreCase)) found = d;
                    // 한글 이름이 인자로 넘기기 번거로우니 영문 별칭도 받는다.
                    switch (args[i + 1].Trim().ToLowerInvariant())
                    {
                        case "novice": case "easy": found = 0; break;
                        case "normal": found = 1; break;
                        case "expert": case "hard": found = 2; break;
                        case "ace": found = 3; break;
                    }
                    if (found >= 0) _difficulty = found;
                    else Debug.LogWarning($"[Tankfall] -difficulty 모르는 이름 '{args[i + 1]}' — 중급");
                }
                else if (args[i] == "-weather" && i + 1 < args.Length)
                {
                    if (System.Enum.TryParse<Weather>(args[i + 1].Trim(), true, out var w)) _weatherForced = w;
                    else Debug.LogWarning($"[Tankfall] -weather 모르는 이름 '{args[i + 1]}' — 무작위");
                }
                else if (args[i] == "-roster" && i + 1 < args.Length)
                {
                    // 세 종류를 쉼표로. 모르는 이름이면 기본 로스터를 유지하고 로그만 남긴다 — 자동사격이 죽으면 안 된다.
                    var names = args[i + 1].Split(',');
                    var list = new List<TankKind>();
                    foreach (var n in names)
                        if (System.Enum.TryParse<TankKind>(n.Trim(), true, out var k)) list.Add(k);
                        else Debug.LogWarning($"[Tankfall] -roster 모르는 기종 '{n}' — 무시");
                    if (list.Count == 3) { _roster = list.ToArray(); _rosterFixed = true; }
                    else Debug.LogWarning($"[Tankfall] -roster 는 정확히 3종이어야 한다(받은 것 {list.Count}) — 기본 로스터 사용");
                }
            }
            // ⚠️ 예전엔 이 기본값이 `_autoShot` 일 때만 걸렸다. -gallery 를 추가하니 폴더가 null 이라
            //    Shot() 이 매 프레임 ArgumentNullException 을 던지고 사진은 한 장도 안 남았다.
            //    **찍는 모드가 늘 때마다 재발할 조건**이라 조건 자체를 없앤다.
            if (string.IsNullOrEmpty(_shotDir)) _shotDir = "Screenshots";
            // ⚠️ 자동 검증 모드는 타이틀을 거치지 않는다 — 거치면 하네스가 메뉴에서 조용히 멈춘다(BattleScreens.cs 머리말).
            //    사람이 켠 경우에만 타이틀로 시작한다. 이 판정은 인자만 보므로 파싱 직후가 자리다 —
            //    아래 저장 설정 읽기(Prefs)와 화면 선택이 같은 값을 써야 한다(사람 판 판정을 두 곳에 두지 않는다).
            bool headless = _autoShot || _perf || _gallery || _phaseCheck || _supplySelfTest || _practice || _uiSelfTest || _shellCheck || _shellGallery;
            // 사람 판이면 지난번 설정(맵·난이도·아이템·날씨·Boom·로스터·소리)을 이어받는다. 인자로 준 값은 안 덮는다(Prefs 머리말).
            // ⚠️ 지형을 만들기 **전**이어야 한다 — 뒤에서 읽으면 타이틀 뒤 전장이 다른 맵으로 서 있다.
            if (!headless && !_autoMode) LoadPrefs();

            SetupWorld();
            // ⚠️ 날씨를 **지형보다 먼저** 정한다. 지형 정점 색이 눈 여부를 보고 칠해지므로(TerrainPalette),
            //    순서가 뒤집히면 눈 오는 판에서 지형만 여름색으로 남는다.
            SetWeather(_weatherForced ?? (Random.value < SnowChance ? Weather.Snow : Weather.Clear));
            RebuildTerrain();

            // 날씨는 위에서 이미 정했다(지형 색이 그걸 본다). 유닛은 생성 시 `W` 사본을 뜨므로 여기 순서면 충분하다.
            SpawnTeams();
            _items.Clear();
            // 아이템 뽑기는 판마다 달라야 한다 — 바람·날씨와 같은 취급(고정 시드면 매 판 같은 가방이 나온다).
            _itemRng = new Rng((uint)Random.Range(1, int.MaxValue));
            if (_itemSlots > 0)
            {
                var roll = new List<ItemKind>();
                foreach (var u in _units) { Items.Roll(ref _itemRng, _itemSlots, roll); _items.Bag(u.Id).AddRange(roll); }
            }
            if (_practice)
            {
                // 연습장은 "나 하나 vs 표적 셋". 아군 2·3번은 쓰지 않으니 치운다(턴이 안 오므로 서 있기만 한다).
                foreach (var u in _units)
                    if (u.Team == 0 && u.Id != 0) u.Root.gameObject.SetActive(false);
                _turn = 0;
            }
            RollWind();
            // 탱크 고르기(§2-9-17) — 사람이 하는 판에서만. 자동 모드·연습장·자체검사는 건너뛴다.
            // ⚠️ 예전엔 여기 모드를 하나하나 나열했다. `-rosterselftest` 를 새로 만들자마자 그 목록에
            //    빼먹어서 **자체검사가 고르기 화면에 갇혀 영영 안 끝났다**(빌드는 성공, 검사는 무응답).
            //    모드가 늘 때마다 재발할 조건이라 목록이 아니라 **플래그 하나**로 판정한다 —
            //    사람이 없는 모드는 파싱할 때 `_autoMode` 를 켜므로 여기 손댈 일이 없다.
            _log = $"전투 개시 — {MapHeightFunction.Name(_map)} · {WeatherName(_weather)} · AI {Difficulties[_difficulty].Name} · {TankStats.Get(_roster[0]).Name}·{TankStats.Get(_roster[1]).Name}·{TankStats.Get(_roster[2]).Name}  (파랑 vs 빨강)";

            _headless = headless;   // 판정은 위(인자 파싱 직후)에서 한 번만 한다
            // ⚠️ 2026-09-17 사고: `-uiselftest` 가 **고르기 화면에 갇혀 영영 안 끝났다.**
            //    탱크 고르기(§2-9-17)가 들어오면서 `_picking` 이 Update 맨 앞에서 return 하는데,
            //    무인 모드는 각자 파싱에서 `_autoMode` 를 켜야 했고 `-uiselftest`·`-shellcheck`·
            //    `-shellgallery`·`-phasecheck` 넷이 빠져 있었다. 로그에 **아무것도 안 찍혀서**
            //    "멈춘 건지 느린 건지" 조차 안 보였다(빌드가 깨진 줄 알고 한참 헤맸다).
            //
            //    플래그를 하나씩 켜는 방식이 원인이다 — 무인 모드를 새로 만들 때마다 또 빠진다.
            //    그래서 **headless 목록에서 유도한다.** 위 `headless` 와 같은 조건이면 사람이 없다는 뜻이고,
            //    사람이 없으면 고르기 화면은 성립하지 않는다. 새 무인 모드는 headless 에만 추가하면 된다.
            if (headless) _autoMode = true;                 // 머리말 참조 — 목록을 단일 소스로 삼는다
            _picking = !_rosterFixed && !_autoMode && !_practice;
            _screen = headless ? GameScreen.Battle : GameScreen.Title;
            Sfx.Muted = _autoShot || _perf || _gallery || _phaseCheck || _supplySelfTest || _uiSelfTest || Application.isBatchMode;
        }

        void SpawnTeams()
        {
            // === 색 채널 두 개 (포트리스 참고) ===
            // 포트리스는 탱크마다 고유색이라 색만 보고 상대 기체를 안다. 그걸 따르되,
            // 3v3 이라 팀도 읽혀야 하므로 **종 = 차체색, 팀 = 강조색**으로 채널을 나눈다.
            // 채도를 높게 잡은 이유: 파스텔에 가까운 밝은 색이 토이 느낌을 만든다(귀여움은 비례 + 색이다).
            // 종별 색은 ProceduralTank.BodyColor 가 단일 소스다 — 여기서 또 적으면 13종에서 어긋난다.
            var teamMat = new[]
            {
                MakeMat(new Color(0.15f, 0.48f, 0.98f), 0.45f),   // 아군 — 선명한 파랑
                MakeMat(new Color(0.98f, 0.20f, 0.24f), 0.45f),   // 적군 — 선명한 빨강
            };
            var track = MakeMat(new Color(0.22f, 0.20f, 0.24f), 0f);
            var wood = MakeMat(new Color(0.56f, 0.37f, 0.20f), 0.05f);   // 통나무 범퍼

            // ⚠️ 예전엔 shapes[] 와 kinds[] 두 배열을 손으로 맞췄는데, 13종에서는 반드시 어긋난다.
            //    이제 실루엣도 색도 **종류로 조회**한다 — 짝맞춤이라는 실패 지점 자체를 없앴다.
            var kinds = _roster;
            for (int t = 0; t < 2; t++)
                for (int i = 0; i < 3; i++)
                {
                    MapHeightFunction.Spawn(_map, t, i, out float x, out float z);
                    var root = ProceduralTank.Build(TankShape.Of(kinds[i]),
                                                    MakeMat(TankShape.BodyColor(kinds[i]), 0.22f),
                                                    track, teamMat[t], out var tur, out var bar, out var fp, wood);
                    var u = new Unit { Id = t * 3 + i, Team = t, Kind = kinds[i], W = _weather,
                                       Root = root, Turret = tur, Barrel = bar, Fire = fp };
                    u.HpMax = TankStats.Get(kinds[i]).Hp;
                    u.Hp = u.HpMax;
                    u.BarrelPitch = Mathf.Clamp(u.BarrelPitch, u.St.MinPitch, u.St.MaxPitch);
                    float g = TankGroundProbe.GroundBelow(_vol, x, z, 60f);
                    root.position = new Vector3(x, (float.IsNegativeInfinity(g) ? 10f : g) + GroundVisualLift, z);
                    // 서로 마주보게
                    MapHeightFunction.Spawn(_map, 1 - t, i, out float ox, out float oz);
                    u.Heading = Mathf.Atan2(ox - x, oz - z) * Mathf.Rad2Deg;
                    root.rotation = Quaternion.Euler(0, u.Heading, 0);
                    _units.Add(u);
                }
            // 턴 순서: 50% 확률로 선공 팀 결정 (공정 대전 보장, 팀 교차 A1 B1 A2 B2 A3 B3 / B1 A1 B2 A2 B3 A3)
            int firstTeam = Random.value < 0.5f ? 0 : 1;
            _units.Sort((a, b) =>
            {
                int orderA = (a.Team == firstTeam) ? 0 : 1;
                int orderB = (b.Team == firstTeam) ? 0 : 1;
                return ((a.Id % 3) * 2 + orderA) - ((b.Id % 3) * 2 + orderB);
            });
            _status.Clear(); _hazards.Clear(); _pendingShots.Clear(); RefreshHazards();
            _supply.Clear(); RefreshCrates();
            _impair.Clear();
            _air.Roll(ref _airRng, MapSize);
            RefreshAir();
            // Boom 모드 지뢰밭 — 원작 "곳곳에 마인랜더의 지뢰가 드문드문 깔린 상태로 게임이 시작된다".
            if (_boom)
            {
                BoomMode.RollMineField(ref _boomRng, MapSize, _boomSpots);
                int placed = 0;
                foreach (var (bx, bz) in _boomSpots)
                {
                    float g = TankGroundProbe.GroundBelow(_vol, bx, bz, 80f);
                    if (float.IsNegativeInfinity(g)) continue;
                    _hazards.PlaceMine(bx, g, bz, BoomMode.MineRadius, BoomMode.MineDamage, -1);
                    placed++;
                }
                RefreshHazards();
                Debug.Log($"[Tankfall] Boom 모드 — 지뢰 {placed}발 매설");
            }
            _order = new TurnOrder();
            foreach (var u in _units) _order.Add(u.Id, TankStats.Get(u.Kind).Delay);
            _turn = 0;   // 전원 누적 0 → 첫 등록 유닛이 먼저 턴을 가져감
        }

        /// <summary>
        /// 지형을 통째로 다시 만든다. 판 시작(§3 20_Lobby → 30_Battle)과 재시작이 같이 쓴다.
        ///
        /// ⚠️ 맵을 바꿨으면 **반드시** 이걸 거쳐야 한다 — `_vol` 은 생성 시점의 높이 함수를 굽는다.
        ///    `_map` 만 바꾸고 재스폰하면 탱크는 새 맵 좌표에, 지형은 옛 맵 모양으로 남아 공중에 뜬다.
        /// </summary>
        void RebuildTerrain()
        {
            if (_terrain != null) Destroy(_terrain.gameObject);
            _vol = new SdfVolume(Voxel, ChunkN, OriginY, Height, Mathf.RoundToInt(MapSize / Voxel),
                                MapHeightFunction.Grad(_map));

            var go = new GameObject("Terrain");
            _terrain = go.AddComponent<TerrainView>();
            _terrain.Theme = MapTheme.Of(_map, _weather == Weather.Snow);
            ApplyTheme();                      // 하늘·태양·안개도 이 맵의 것으로
            _terrain.Init(_vol, ChunkN, MakeTerrainMat());
            int cells = Mathf.RoundToInt(MapSize / Voxel);
            _terrain.BuildRegion(0, cells / ChunkN, _vol.GridY(-2f) / ChunkN, _vol.GridY(40f) / ChunkN + 1,
                                 0, cells / ChunkN);

            ScatterObjects();
        }

        /// <summary>
        /// 지형 위에 나무·바위를 뿌린다. 지형을 다시 만들 때마다 같이 다시 뿌려야 한다 —
        /// 안 그러면 옛 지형 높이에 맞춰진 나무가 공중에 뜨거나 땅에 묻힌다.
        /// </summary>
        void ScatterObjects()
        {
            if (_scatter == null)
            {
                var sgo = new GameObject("Scatter");
                _scatter = sgo.AddComponent<Scatter>();
            }
            // 탱크 스폰 자리는 비워 둔다(Scatter 머리말 규칙 2).
            var avoid = new List<Vector3>();
            for (int t = 0; t < 2; t++)
                for (int i = 0; i < 3; i++)
                {
                    MapHeightFunction.Spawn(_map, t, i, out float sx, out float sz);
                    avoid.Add(new Vector3(sx, 0f, sz));
                }
            var th = MapTheme.Of(_map, _weather == Weather.Snow);
            _scatter.Build(_vol, MapSize, (int)_map * 7919 + 13, avoid, th);

            // 배경(구름·원경 산맥·해). 맵 밖이라 지형 파괴와 무관하지만 **테마가 바뀌면 같이 바뀌어야** 한다.
            if (_env == null) _env = new GameObject("Environment").AddComponent<Environment>();
            // 맵 밖 바닥 높이는 **맵 가장자리의 실제 지면**에 맞춘다 — 눈대중으로 0 을 쓰면 경계에 단이 진다.
            float edgeY = TankGroundProbe.GroundBelow(_vol, 2f, MapSize * 0.5f, 90f);
            if (float.IsNegativeInfinity(edgeY)) edgeY = 0f;
            _env.Build(MapSize, th, (int)_map * 104729 + 7, _sun != null ? _sun.transform.forward : Vector3.down, edgeY);
        }

        Light _sun;
        Transform _skyDome;
        Material _skyMat;

        void SetupWorld()
        {
            var camGo = new GameObject("MainCamera");
            _cam = camGo.AddComponent<Camera>();
            _cam.tag = "MainCamera";
            _cam.farClipPlane = 900f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.allowMSAA = true;
            _cam.allowHDR = true;          // 가산 파티클(섬광·빔)이 흰색으로 뭉개지지 않게

            ApplyQuality();

            _sun = new GameObject("Sun").AddComponent<Light>();
            _sun.type = LightType.Directional;
            _sun.transform.rotation = Quaternion.Euler(46f, 35f, 0f);
            _sun.intensity = 1.0f;
            _sun.shadows = LightShadows.Soft;
            // 그림자 품질 — 로우폴리는 **면과 그림자로** 형태를 읽히게 한다(조사 근거: directional light 가 면을 가른다).
            //   기본값(near 가 촘촘하고 far 가 뭉개짐)이면 200m 맵에서 먼 탱크 그림자가 통째로 사라진다.
            _sun.shadowBias = 0.02f;
            _sun.shadowNormalBias = 0.35f;
            _sun.shadowNearPlane = 0.2f;

            // 채워 넣는 반대편 광원. 하나뿐이면 그늘이 새까매져 형태가 안 보인다(앰비언트를 올리면 색이 뜬다 — 형광 연두 사고).
            var fill = new GameObject("FillLight").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.transform.rotation = Quaternion.Euler(28f, 35f + 180f, 0f);
            fill.intensity = 0.28f;
            fill.shadows = LightShadows.None;
            fill.color = new Color(0.72f, 0.80f, 0.95f);        // 하늘빛 — 그늘을 차갑게

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;

            BuildSkyDome();
            ApplyTheme();
        }

        /// <summary>
        /// 화질 설정. 코드로 만든 프로젝트라 QualitySettings 기본값이 그대로 남아 있었다 —
        /// MSAA 가 꺼져 있어 지형·탱크 모서리가 전부 계단으로 나왔고(로우폴리는 **모서리가 곧 형태**라 치명적),
        /// 그림자 거리가 짧아 200m 맵의 먼 쪽에는 그림자가 아예 없었다.
        ///
        /// ⚠️ 여기 값을 올리면 프레임이 떨어진다. 바꿨으면 `-perf` 로 재라(§7-7 처럼 분해해서).
        /// </summary>
        static void ApplyQuality()
        {
            QualitySettings.antiAliasing = 4;                    // MSAA 4x — 모서리 계단 제거
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
            QualitySettings.shadowProjection = ShadowProjection.StableFit;
            QualitySettings.shadowDistance = 320f;               // 맵이 200m — 반대편 탱크 그림자까지 살린다
            QualitySettings.shadowCascades = 4;
            QualitySettings.shadowCascade4Split = new Vector3(0.05f, 0.15f, 0.35f);
            QualitySettings.softParticles = true;
            QualitySettings.pixelLightCount = 4;
            QualitySettings.vSyncCount = 1;
        }

        /// <summary>
        /// 카메라를 감싸는 안쪽 구. 하늘 그라디언트를 여기에 칠한다(원작은 맵마다 하늘이 달랐다 — SkyGradient.shader 조사 근거).
        /// ⚠️ 카메라를 따라다녀야 한다 — 고정하면 맵 끝에서 하늘 밖으로 나간다.
        /// </summary>
        void BuildSkyDome()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "SkyDome";
            Destroy(go.GetComponent<Collider>());
            go.transform.localScale = Vector3.one * 860f;
            var mr = go.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var sh = Shader.Find("Tankfall/SkyGradient");
            if (sh == null) { Debug.LogWarning("[Tankfall] SkyGradient 셰이더 없음 — 하늘이 단색이 된다."); Destroy(go); return; }
            _skyMat = new Material(sh);
            mr.sharedMaterial = _skyMat;
            _skyDome = go.transform;
        }

        /// <summary>맵 테마(MapTheme)를 하늘·태양·안개·지형에 한 번에 바른다. 색이 갈리는 지점은 여기 하나다.</summary>
        void ApplyTheme()
        {
            var th = MapTheme.Of(_map, _weather == Weather.Snow);
            if (_skyMat != null)
            {
                _skyMat.SetColor("_Top", th.SkyTop);
                _skyMat.SetColor("_Bottom", th.SkyBottom);
            }
            if (_cam != null) _cam.backgroundColor = th.SkyBottom;
            if (_sun != null) _sun.color = th.Sun;

            // 먼 지형이 하늘로 녹아들게 — 200m 맵에서 끝이 칼로 자른 듯 끊기면 배경이 판때기로 보인다.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = th.Fog;
            RenderSettings.fogStartDistance = 220f;
            RenderSettings.fogEndDistance = 620f;

            // ⚠️ 앰비언트를 올리면 그림자가 옅어지고 색이 뜬다(형광 연두 사고). 직사광이 형태를 만들게 두고 여기는 낮게.
            // 앰비언트: 너무 높으면 색이 뜨고(형광 연두) 너무 낮으면 그늘이 칙칙해진다. 그 사이 값이다.
            RenderSettings.ambientSkyColor = Color.Lerp(th.SkyTop, Color.white, 0.2f) * 0.70f;
            RenderSettings.ambientEquatorColor = Color.Lerp(th.Mid, th.SkyBottom, 0.5f) * 0.58f;
            RenderSettings.ambientGroundColor = th.RockDark * 0.45f;

            if (_terrain != null) _terrain.Theme = th;
        }

        /// <summary>
        /// 지형 머티리얼. 정점 색 셰이더가 있어야 풀·흙·바위가 나온다(TerrainPalette).
        /// ⚠️ 셰이더가 빠지면 **조용히 단색으로 돌아가는 게 아니라** 분홍색이 된다 —
        ///    그래서 못 찾으면 경고를 남기고 Standard 로 떨어진다(단색이지만 최소한 보인다).
        /// </summary>
        static Material MakeTerrainMat()
        {
            var sh = Shader.Find("Tankfall/TerrainVertexColor");
            if (sh == null)
            {
                Debug.LogWarning("[Tankfall] Tankfall/TerrainVertexColor 셰이더가 없다 — 지형이 단색이 된다. " +
                                 "BuildScript.EnsureShadersIncluded 확인.");
                return MakeMat(new Color(0.46f, 0.44f, 0.36f), 0.05f);
            }
            var m = new Material(sh);
            // 계단 그림자(셀 셰이딩) 단계. 조사 근거: 로우폴리는 면을 **갈라 보여줘야** 형태가 읽힌다.
            // 4 단계면 캐주얼 톤이 나오면서 지형 굴곡도 안 뭉갠다 [실측으로 조정 가능].
            if (m.HasProperty("_Ramp")) m.SetFloat("_Ramp", 4f);
            return m;
        }

        static Shader _shader;

        static Material MakeMat(Color c, float smooth)
        {
            if (_shader == null)
            {
                // 빌드에서 Standard 가 빠질 수 있다(코드로만 머티리얼을 만들면 참조가 없다).
                // BuildScript.EnsureShadersIncluded 가 막지만, 안전망으로 폴백을 둔다.
                _shader = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse")
                          ?? Shader.Find("Sprites/Default");
                if (_shader == null)
                    Debug.LogError("[Tankfall] 셰이더를 찾지 못했다 — 아무것도 그려지지 않는다. " +
                                   "GraphicsSettings 의 AlwaysIncludedShaders 를 확인하라.");
            }
            var m = new Material(_shader) { color = c };
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            return m;
        }

        /// <summary>바람은 라운드 동안 고정(§15) — 매 턴 바뀌면 운 요소가 너무 커진다.</summary>
        /// <summary>
        /// 날씨를 바꾸는 **유일한 경로**. 유닛이 각자 `W` 사본을 들고 있어서(`Unit.St` 가 그걸 본다)
        /// `_weather` 에 직접 대입하면 이미 생성된 탱크는 옛 날씨로 남는다 — 포세이돈 능력이 조용히 죽는 길이다.
        /// </summary>
        void SetWeather(Weather w)
        {
            _weather = w;
            if (_units != null) foreach (var u in _units) u.W = w;
            if (_cam != null) EnsureFx().SetSnow(w == Weather.Snow && !Application.isBatchMode, _cam.transform, _wind);
            // 원작: "눈이 내리면 … 카터펄트의 불과 듀크탱크의 독가스는 사라진다"
            //       (https://namu.wiki/w/포트리스2 · 2026-09-17 조회)
            if (w != Weather.Snow) return;
            int gone = _hazards.ClearFires();
            if (gone > 0) { RefreshHazards(); _log = $"❄ 눈 — 불·독가스 {gone}곳이 꺼졌다"; }
        }

        static string WeatherName(Weather w) => w == Weather.Snow ? "눈" : "맑음";

        void RollWind()
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            float s = Random.Range(0f, 10f);
            _wind = new Vector2(Mathf.Cos(a) * s, Mathf.Sin(a) * s);
            if (_weather == Weather.Snow && _cam != null) EnsureFx().SetSnow(!Application.isBatchMode, _cam.transform, _wind);   // 눈은 바람을 따라 흩날린다
        }

        void Update()
        {
            if (_units.Count == 0) return;                 // Start 가 실패한 경우
            if (_gallery) { GalleryStep(); return; }
            if (_perf) { PerfStep(); return; }
            if (_picking) { UpdateCamera(Time.deltaTime); return; }   // 고르는 동안 판은 멈춘다
            if (_supplySelfTest) { SupplySelfTestStep(); return; }
            if (_ultSelfTest) { UltSelfTestStep(); return; }
            if (_impairSelfTest) { ImpairSelfTestStep(); return; }
            if (_climateSelfTest) { ClimateSelfTestStep(); return; }
            if (_rosterSelfTest) { RosterSelfTestStep(); return; }
            if (_gameSelfTest) { GameSelfTestStep(); return; }
            if (_boomSelfTest) { BoomSelfTestStep(); return; }
            if (_autoShot) { AutoShotStep(); return; }
            if (_uiSelfTest) { UiSelfTestStep(); return; }
            if (_shellCheck) { ShellCheckStep(); return; }
            if (_shellGallery) { ShellGalleryStep(); return; }

            float dt = Time.deltaTime;
            TickPopups(dt);
            if (_slowUntil >= 0f && Time.unscaledTime >= _slowUntil) { Time.timeScale = 1f; _slowUntil = -1f; }
            // 배경음악은 화면 상태에서 유도한다 — 전환 지점마다 켜고 끄면 빠뜨리는 경로가 생긴다(결과→타이틀→연습장…).
            Sfx.Music(_screen == GameScreen.Battle || _screen == GameScreen.Pause ? Sfx.Track.Battle
                    : _screen == GameScreen.Result ? Sfx.Track.None : Sfx.Track.Title);

            // 타이틀·선택·일시정지·결과 화면에서는 전투 로직이 아예 안 돈다.
            if (_screen != GameScreen.Battle) { ScreenUpdate(); return; }

            // Esc = 일시정지. 연습장·자동 모드에서도 빠져나갈 길은 있어야 한다.
            if (Input.GetKeyDown(KeyCode.Escape)) { _screen = GameScreen.Pause; return; }

            _battleClock += dt;                      // 한 판 길이 실측(§11 M3 게이트)
            TickHelicopter(dt);
            TickAir(dt);

            // F2 = 연습장 "정답 보기"(§5-7). ⚠️ **PvP HUD 에는 절대 노출 금지**(기획서 §58) —
            //    그래서 연습장에서만 열린다. 이 조건을 풀지 마라.
            if (_practice && Input.GetKeyDown(KeyCode.F2)) ShowPracticeAnswer();
            if (_practiceSelfTest && _phase == Phase.Move) { PracticeSelfTestFire(); return; }

            // 아이템: [ ] 로 고르고 Enter 로 쓴다. ⚠️ 숫자키 1·2·3 은 이미 탄종(§8)이 쓰고 있다.
            if (_itemSlots > 0 && IsPlayerTurn && (_phase == Phase.Move || _phase == Phase.Fire))
            {
                var bag = _items.Bag(Current.Id);
                if (bag.Count > 0)
                {
                    if (Input.GetKeyDown(KeyCode.LeftBracket)) _itemSel = (_itemSel - 1 + bag.Count) % bag.Count;
                    if (Input.GetKeyDown(KeyCode.RightBracket)) _itemSel = (_itemSel + 1) % bag.Count;
                    if (_itemSel >= bag.Count) _itemSel = 0;
                    if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) UseItem(Current, bag[_itemSel]);
                }
            }

            // F1 = AI 난이도 순환. 다음 AI 조준부터 바로 반영된다(AiDifficulty 는 프로퍼티라 캐시가 없다).
            if (Input.GetKeyDown(KeyCode.F1))
            {
                _difficulty = (_difficulty + 1) % Difficulties.Length;
                _log = $"AI 난이도 → {Difficulties[_difficulty].Name}";
            }

            switch (_phase)
            {
                case Phase.Move:
                    if (!IsPlayerTurn) { AiMoveStep(dt); break; }
                    if (_phaseCheck)
                    {
                        if (_pcTurns == 0) BeginPhaseCheckTurn();   // 첫 턴은 NextTurn 을 안 거친다
                        _phaseTimer -= dt; _pcMoveHold -= dt;
                        // 턴마다 다르게: 일부는 일찍 스킵, 일부는 타이머를 끝까지 태워 자동 진입을 검증
                        if (_pcMoveHold <= 0f) { _pcMoveSkips++; EnterFire(); }
                        else if (_phaseTimer <= 0f) EnterFire();
                        break;
                    }
                    _phaseTimer -= dt;
                    PlayerMove(dt);
                    // 스페이스=즉시 스킵, 우클릭=조준 진입(§2-1), 시간 초과=자동 진입
                    if (_phaseTimer <= 0f || Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(1))
                        EnterFire();
                    break;
                case Phase.Fire:
                    if (!IsPlayerTurn) { _phase = Phase.AiThink; _phaseTimer = 0.9f; break; }
                    if (_phaseCheck)
                    {
                        _phaseTimer -= dt;
                        // 홀수 턴은 일부러 시간을 다 태워 **강제 발사** 경로를 탄다
                        if (_pcTurns % 2 == 1) { if (_phaseTimer <= 0f) { _pcForced++; ForceFire(); } }
                        else if (_phaseTimer < FirePhaseSec - 0.5f) AiShoot();
                        break;
                    }
                    _phaseTimer -= dt;
                    PlayerAim(dt);
                    if (_phaseTimer <= 0f) ForceFire();
                    break;
                case Phase.AiThink:
                    _phaseTimer -= dt;
                    if (_phaseTimer <= 0f) AiShoot();
                    break;
                case Phase.Flying: FlyStep(dt); break;
                case Phase.Resolve:
                    _phaseTimer -= dt;
                    if (_phaseTimer <= 0f) NextTurn();
                    break;
            }
            UpdateCamera(dt);
            TickFx(dt);
            if (_phaseCheck) PhaseCheckLog();
        }

        /// <summary>턴 진입 시 이번 턴의 합성 입력 시나리오를 정한다.</summary>
        void BeginPhaseCheckTurn()
        {
            _pcTurns++;
            // 3턴마다 MOVE 를 스킵하지 않고 12초 타이머를 끝까지 태운다(자동 진입 경로 검증)
            _pcMoveHold = (_pcTurns % 3 == 0) ? 999f : 0.4f;
            if (_pcTurns > 12)
            {
                Debug.Log($"[Tankfall] PHASECHECK 완료 — 턴 {_pcTurns - 1} · MOVE조기스킵 {_pcMoveSkips} · 강제발사 {_pcForced}");
                Application.Quit(0);
            }
        }

        void PhaseCheckLog()
        {
            if (_phase == _lastLoggedPhase) return;
            Debug.Log($"[Tankfall] PHASE {_lastLoggedPhase,-8} -> {_phase,-8} 턴 {_pcTurns} 라운드 {_round} 남은 {Mathf.Max(0f, _phaseTimer):F1}s");
            _lastLoggedPhase = _phase;
        }

        // ---------------- 플레이어 ----------------

        /// <summary>MOVE 페이즈 — 이동과 자유 카메라만. 여기서 조준은 못 한다.</summary>
        // ── AI 이동(§2-1 MOVE 페이즈) ──────────────────────────────────────
        //
        // ⚠️ **이게 없던 동안 게임의 AI 는 한 번도 움직이지 않았다**(2026-09-17, 다른 세션이 발견).
        //    `case Phase.Move: if (!IsPlayerTurn) { _phase = Phase.AiThink; }` 로 통째로 건너뛰었다.
        //    결과가 조용해서 오래 안 들켰다 — 화면에는 "적군 조준 중"이 뜨니 정상으로 보인다.
        //    실제로는:
        //      · AI 가 자기가 판 구덩이에서 못 나온다(이온어태커가 "적이 자기 구덩이에 숨어" 안 보이던 증상)
        //      · 헬기 보급 상자를 **게임에서는 영원히 안 줍는다**(헬기가 장식이 된다)
        //      · 독·불 장판에서 안 나온다 — AiGunner.EffectValue 는 "적이 나가면 끊긴다"를 전제로 값을 매기는데 못 나간다
        //    그리고 **하네스는 움직이고 있었다.** 즉 승률 측정이 게임의 승률이 아니었다(§2-9-1 이 경고한 상태).
        //
        // 판단은 Sim/AiMover 가 한다 — 게임과 하네스가 **같은 함수**를 부른다. 여기서 정책을 또 쓰지 마라.
        // 걷는 것은 플레이어와 같은 DriveUnit 을 쓴다(WalkStep 분할·보급 줍기·지뢰 밟기가 전부 거기 있다).
        void AiMoveStep(float dt)
        {
            var u = Current;
            if (u == null || !u.Alive) { _phase = Phase.AiThink; _phaseTimer = 0.9f; return; }

            if (!_aiMoveDecided)
            {
                _aiMoveDecided = true;
                _hazards.Snapshot(_hazardBuf);
                _aiFoes.Clear();
                foreach (var o in _units)
                    if (o.Alive && o.Team != u.Team) _aiFoes.Add(new AiGunner.Target { Id = o.Id, Kind = o.Kind, Center = o.Center, Defense = o.St.Defense });
                var plan = AiMover.Decide(_vol, u.Pos.x, u.Pos.y, u.Pos.z, _status.CanMove(u.Id),
                                          u.St.MaxRange, MapSize, _hazardBuf,
                                          _itemSlots > 0 ? _supply : null, _aiFoes, ref _aiRng);
                _aiMoveLeft = plan.Move ? plan.Distance : 0f;
                if (plan.Move)
                {
                    u.Heading = Mathf.Atan2(plan.DirX, plan.DirZ) * Mathf.Rad2Deg;
                    // 이유를 남긴다 — 안 남기면 "움직이긴 하는데 왜인지 모르는" AI 가 된다.
                    _log = $"{(u.Team == 0 ? "아군" : "적군")}{u.Id % 3 + 1} 이동 — {MoveWhy(plan.Why)}";
                }
                _phaseTimer = 2.2f;      // 안전 상한. 턱에 막혀 제자리걸음이어도 턴이 안 멈춘다.
            }

            _phaseTimer -= dt;
            if (_aiMoveLeft > 0f && u.Gauge > 0f && _phaseTimer > 0f)
            {
                var before = u.Pos;
                DriveUnit(u, 1f, dt);
                _aiMoveLeft -= Vector3.Distance(before, u.Pos);
                // 한 발짝도 못 갔으면 막힌 것이다 — 타이머를 태우지 말고 바로 조준으로 넘긴다.
                if ((u.Pos - before).sqrMagnitude < 1e-8f) _aiMoveLeft = 0f;
                return;
            }
            _aiMoveDecided = false;
            _phase = Phase.AiThink; _phaseTimer = 0.9f;
        }

        static string MoveWhy(MoveReason r) => r switch
        {
            MoveReason.CraterEscape => "구덩이 탈출",
            MoveReason.HazardEscape => "장판 탈출",
            MoveReason.Supply => "보급 상자",
            MoveReason.Range => "사거리 확보",
            MoveReason.Wander => "자리 옮김",
            _ => "이동",
        };

        bool _aiMoveDecided;
        float _aiMoveLeft;
        readonly List<AiGunner.Target> _aiFoes = new List<AiGunner.Target>();

        void PlayerMove(float dt)
        {
            var u = Current;
            float turn = (Input.GetKey(KeyCode.A) ? -1f : 0f) + (Input.GetKey(KeyCode.D) ? 1f : 0f);
            float fwd = (Input.GetKey(KeyCode.W) ? 1f : 0f) + (Input.GetKey(KeyCode.S) ? -1f : 0f);
            u.Heading += turn * TurnSpeed * dt;
            bool driving = Mathf.Abs(fwd) > 0.01f && u.Gauge > 0f;
            if (driving) DriveUnit(u, fwd, dt);
            Sfx.Engine(driving);                 // 게이지가 바닥나면 소리도 멈춘다 — 왜 안 움직이는지가 귀로도 온다
            GroundUnit(u, dt);
            ApplyAim(u);
        }

        /// <summary>
        /// MOVE → FIRE. 카메라를 포신 뒤로 스냅한다(§2-1). 되돌아갈 수 없다.
        /// </summary>
        void EnterFire()
        {
            var u = Current;
            _camYaw = u.Heading + u.TurretYaw;        // 포신이 향한 곳을 그대로 본다
            _camPitch = 18f;
            _camDist = 22f;
            Sfx.Engine(false);
            _phase = Phase.Fire;
            _phaseTimer = FirePhaseSec;
            _power = 0f; _charging = false;
            _log = "FIRE — 좌우/상하 조준, 스페이스 길게 눌러 파워";
        }

        /// <summary>
        /// 시간 초과 강제 발사(§2-1, 포트리스 동일). 차징 중이면 그 파워로, 아니면 중간값으로 쏜다.
        /// ⚠️ 파워 0 으로 쏘면 발밑에 떨어져 자폭이라 처벌이 과하다 — 머뭇거린 대가는
        ///    "원하는 파워를 못 고른다"로 충분하다.
        /// </summary>
        void ForceFire()
        {
            var u = Current;
            float pw = _charging ? _power : 0.5f;
            _charging = false;
            _log = $"시간 초과 — 강제 발사 (파워 {pw * 100:F0})";
            FireFrom(u, u.TurretYaw, u.BarrelPitch, pw);
        }

        /// <summary>FIRE 페이즈 — 조준과 발사만. 이동은 끝났다.</summary>
        void PlayerAim(float dt)
        {
            var u = Current;

            // 탄종 전환 — FIRE 페이즈에서만. 쏘기 직전까지 바꿀 수 있어야 관측 후 판단이 가능하다.
            if (Input.GetKeyDown(KeyCode.Alpha1)) { u.Shell = ShellKind.Normal; _useSs = false; _useUlt = false; }
            // 원작: 2번탄은 무한. 예전 "3발 제한 + 2라운드 해금"은 내 발명이었고 걷어냈다(§2-9).
            if (Input.GetKeyDown(KeyCode.Alpha2)) { u.Shell = ShellKind.Special; _useSs = false; _useUlt = false; }
            // SS = 나이스샷 2점. 없는데 누르면 아무 일도 없다(HUD 가 이유를 보여준다)
            if (Input.GetKeyDown(KeyCode.Alpha3) && u.Skill.CanSs()) { u.Shell = ShellKind.Special; _useSs = true; _useUlt = false; }
            // 4 = 궁극기(§49). 나이스샷을 SS 보다 더 모아야 열린다(NiceShot.UltimateCost).
            // ⚠️ 오너 확정 2026-09-17 "궁극기가 핵 쏘는 거라니까" — 궁극기는 **이것 하나뿐이다.**
            //    병합 때 다른 기계가 만든 게이지형(R 키, 기종 고유 효과)이 같이 들어왔으나 전부 걷어냈다.
            //    다시 만들지 마라. 궁극기 = 2번탄을 핵급으로 키워 한 방에 판을 뒤집는 것(NiceShot.ApplyUltimate).
            if (Input.GetKeyDown(KeyCode.Alpha4) && u.Skill.CanUltimate()) { u.Shell = ShellKind.Special; _useSs = false; _useUlt = true; }
            // 나이스샷 표시점 — 게이지를 여기서 정확히 멈추면 +1
            if (Input.GetKey(KeyCode.Q)) _mark = Mathf.Clamp01(_mark - 0.45f * dt);
            if (Input.GetKey(KeyCode.E)) _mark = Mathf.Clamp01(_mark + 0.45f * dt);
            // 사각이 탄종에 따라 달라지지는 않지만, 종에 따라 다르므로 전환 후 다시 물린다
            u.BarrelPitch = Mathf.Clamp(u.BarrelPitch, u.St.MinPitch, u.St.MaxPitch);

            u.TurretYaw += ((Input.GetKey(KeyCode.LeftArrow) ? -1f : 0f) + (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)) * TurretSpeed * dt;
            // 각도고정탄(§2-9-14) — 원작 "각도를 조절할 수 없다". 좌우 회전은 막지 않는다(원문이 '각도'만 말한다).
            if (!_impair.AngleLocked(u.Id))
                u.BarrelPitch = Mathf.Clamp(u.BarrelPitch + ((Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) + (Input.GetKey(KeyCode.DownArrow) ? -1f : 0f)) * BarrelSpeed * dt,
                                            u.St.MinPitch, u.St.MaxPitch);
            ApplyAim(u);

            // 파워 차징 — 누르는 동안 0↔100 왕복, 놓으면 발사(§11)
            if (Input.GetKeyDown(KeyCode.Space)) { _charging = true; _power = 0f; _chargeDir = 1f; }
            if (_charging)
            {
                _power += _chargeDir * ChargeRate * dt;
                if (_power >= 1f) { _power = 1f; _chargeDir = -1f; }
                if (_power <= 0f) { _power = 0f; _chargeDir = 1f; }
                if (Input.GetKeyUp(KeyCode.Space))
                {
                    _charging = false;
                    // 원작 나이스샷: 표시점에 정확히 멈추면 스킬포인트 +1 (허용 ±1.5% [추정])
                    if (NiceShot.Judge(_mark, _power)) { u.Skill.OnNiceShot(); _niceFlash = $"나이스샷! 포인트 {u.Skill.Points}"; }
                    // 파워고정탄(§2-9-14) — 원작 "50% 이하의 힘으로 발사할 수 없게 된다".
                    float pw2 = _impair.ClampPower(u.Id, _power);
                    if (pw2 != _power) _log = $"[방해] 파워고정탄 — 50% 미만으로는 못 쏜다 ({_power * 100:F0} → {pw2 * 100:F0})";
                    FireFrom(u, u.TurretYaw, u.BarrelPitch, pw2);
                }
            }
        }

        void DriveUnit(Unit u, float fwd, float dt)
        {
            if (!_status.CanMove(u.Id)) { _log = "⛓ 속박 — 이번 턴 이동 불가(포세이돈 2번탄)"; return; }
            Vector3 p = u.Pos;
            float ground = TankGroundProbe.GroundBelow(_vol, p.x, p.z, p.y + 0.5f);
            if (float.IsNegativeInfinity(ground)) ground = p.y;
            float want = fwd * DriveSpeed * dt;
            // ⚠️ WalkStep(0.25m)으로 쪼갠다. 큰 단위로 옮기면 멀쩡한 크레이터에서 갇힌다(§7-6-2).
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(want) / TankGroundProbe.WalkStep));
            float each = want / steps;
            Vector3 dir = Quaternion.Euler(0, u.Heading, 0) * Vector3.forward;
            float moved = 0f;
            for (int i = 0; i < steps && u.Gauge > 0f; i++)
            {
                float nx = p.x + dir.x * each, nz = p.z + dir.z * each;
                if (TankGroundProbe.CanStepTo(_vol, ground, nx, nz, out float ny) != TankGroundProbe.MoveResult.Ok) break;
                p.x = nx; p.z = nz; ground = ny;
                moved += Mathf.Abs(each);
                TryPickupSupplyAt(u, p.x, ground, p.z);   // 원작: 보급은 "이동으로 줍는" 것이다
                // 지뢰·지속불 — 밟는 순간
                int hz = Damage.AfterDefense(_hazards.OnUnitAt(u.Id, u.Kind, new Vec3(p.x, ground + 1.2f, p.z)), u.St.Defense);
                if (hz > 0)
                {
                    u.Hp = Mathf.Max(0, u.Hp - hz);
                    _log = $"◉ 설치물! −{hz}";
                    if (!u.Alive) KillUnit(u);
                    break;
                }
                // 원작의 "이동거리"를 게이지 소모율로 옮겼다. 레이저탱크(1.35)가 가장 멀리 가고
                // 고전 계열(0.75)이 가장 못 간다 — 선언만 해두면 죽은 값이 되므로 여기서 실제로 쓴다.
                u.Gauge -= Mathf.Abs(each) * 2.2f / Mathf.Max(0.1f, u.St.MoveSpeed);   // 기준 45m
            }
            u.Root.position = new Vector3(p.x, ground + GroundVisualLift, p.z);
        }

        void GroundUnit(Unit u, float dt)
        {
            Vector3 p = u.Pos;
            float g = TankGroundProbe.GroundBelow(_vol, p.x, p.z, p.y + 0.5f);
            if (!float.IsNegativeInfinity(g)) u.Root.position = new Vector3(p.x, g + GroundVisualLift, p.z);
            Vector3 n = GroundNormal(p.x, p.z, u.Pos.y);
            var want = Quaternion.LookRotation(
                Vector3.ProjectOnPlane(Quaternion.Euler(0, u.Heading, 0) * Vector3.forward, n), n);
            u.Root.rotation = dt > 0f ? Quaternion.Slerp(u.Root.rotation, want, 1f - Mathf.Exp(-12f * dt)) : want;
            // 피격 흔들림(연출): 차체가 튀어 오르며 좌우로 흔들리다 잦아든다. 위치·판정(Center)에는 안 들어간다 — 곧 GroundUnit 이 되돌린다.
            if (u.Jolt > 0f)
            {
                float k = u.Jolt; u.Jolt = Mathf.Max(0f, u.Jolt - dt * 1.8f);
                u.Root.rotation *= Quaternion.Euler(Mathf.Sin(Time.time * 38f) * 10f * k, 0f, Mathf.Cos(Time.time * 29f) * 8f * k);
                u.Root.position += Vector3.up * (Mathf.Abs(Mathf.Sin(Time.time * 22f)) * 0.45f * k);
            }
        }

        Vector3 GroundNormal(float x, float z, float y)
        {
            const float d = 1.3f;
            float hL = TankGroundProbe.GroundBelow(_vol, x - d, z, y + 2f);
            float hR = TankGroundProbe.GroundBelow(_vol, x + d, z, y + 2f);
            float hB = TankGroundProbe.GroundBelow(_vol, x, z - d, y + 2f);
            float hF = TankGroundProbe.GroundBelow(_vol, x, z + d, y + 2f);
            if (float.IsInfinity(hL) || float.IsInfinity(hR) || float.IsInfinity(hB) || float.IsInfinity(hF)) return Vector3.up;
            var n = new Vector3((hL - hR) / (2f * d), 1f, (hB - hF) / (2f * d)).normalized;
            return Vector3.Angle(n, Vector3.up) > 42f ? Vector3.up : n;
        }

        void ApplyAim(Unit u)
        {
            u.Turret.localRotation = Quaternion.Euler(0, u.TurretYaw, 0);
            u.Barrel.localRotation = Quaternion.Euler(-u.BarrelPitch, 0, 0);
        }

        // ---------------- AI ----------------

        /// <summary>
        /// 조준은 전부 <see cref="AiGunner"/>(SIM)가 한다. 여기서 다시 구현하지 마라 —
        /// 예전엔 이 자리에 파워·각도에 각각 ±8% 를 곱하는 별도 AI 가 있었고,
        /// 두 오차가 겹쳐 45°에서 20m 넘게 빗나갔다(폭발 반경의 3배). SIM 쪽만 고치면
        /// 이쪽이 안 고쳐지는 이중 구현이 원인이었다.
        /// SIM 에 두면 유니티 없이 자동 대전으로 명중률·한 판 길이를 **측정**할 수 있다.
        /// </summary>
        void AiShoot()
        {
            var u = Current;
            var enemies = new List<AiGunner.Target>();
            foreach (var o in _units)
                if (o.Alive && o.Team != u.Team)
                    enemies.Add(new AiGunner.Target { Id = o.Id, Kind = o.Kind, Center = o.Center, Defense = o.St.Defense });
            if (enemies.Count == 0) { NextTurn(); return; }

            var from = new Vec3(u.Fire.position.x, u.Fire.position.y, u.Fire.position.z);
            var swA = _timeEvents ? System.Diagnostics.Stopwatch.StartNew() : null;
            var plan = AiGunner.Decide(_vol, from, enemies, new Vec3(_wind.x, 0f, _wind.y),
                                       AiDifficulty, ref _aiRng, MapSize, TankStats.Get(u.Kind));
            if (swA != null)
                Debug.Log($"[Tankfall] EVENT AI조준 {swA.Elapsed.TotalMilliseconds:F2} ms " +
                          $"(적 {enemies.Count}, 파워 {plan.Power * 100:F0}, 각 {plan.PitchDeg:F1}°)");
            if (!plan.Valid) { NextTurn(); return; }

            AiUseItems(u);   // 조준 전에 쓴다 — 파워업은 피해만 올리므로 조준을 흔들지 않는다
            u.TurretYaw = Mathf.DeltaAngle(u.Heading, plan.YawDeg);
            // 방해탄(§2-9-14): 각도고정·파워고정은 AI 에게도 실제로 걸린다(하네스와 같은 규칙).
            if (!_impair.AngleLocked(u.Id)) u.BarrelPitch = Mathf.Clamp(plan.PitchDeg, MinElev, MaxElev);
            ApplyAim(u);
            FireFrom(u, u.TurretYaw, u.BarrelPitch, _impair.ClampPower(u.Id, Mathf.Clamp01(plan.Power)));
        }

        // ---------------- 발사 · 비행 ----------------

        void FireFrom(Unit u, float turretYaw, float pitch, float power)
        {
            // ⚠️ 착탄은 몇 초 뒤다 — 그때 `Current` 를 보면 이미 턴이 넘어가 있어 피해가 엉뚱한 팀에 쌓인다.
            //    쏘는 순간의 팀을 여기서 박아 둔다.
            _shooterTeam = u.Team;
            _stat[u.Team].Shots++;
            Sfx.Fire(power);

            float worldYaw = u.Heading + turretYaw;
            var baseSt = TankStats.For(u.Kind, ShellKind.Normal, u.HpFrac, u.W);
            float speed = baseSt.SpeedAt(power);
            var p0 = new Vec3(u.Fire.position.x, u.Fire.position.y, u.Fire.position.z);
            var accel = baseSt.AccelWith(_wind.x, _wind.y);
            var boxes = new List<TankHitbox>();
            foreach (var o in _units) if (o.Alive) boxes.Add(new TankHitbox { Id = o.Id, Center = o.Center, Radius = TankRadius });

            // 1) 기준 탄도(패턴 중앙)
            System.Func<int, bool> homingOk = id => { var x = _units.Find(o => o.Id == id); return x != null && x.Team != u.Team; };
            // 탄종 선택용 기준 궤적 — 유도는 빼고(§8 "탄도가 같으니 조준을 다시 풀 필요가 없다"), 기후는 넣는다.
            var res = ProjectileSimulator.Simulate(_vol, p0, Ballistics.VelocityFrom(worldYaw, pitch, speed), accel, boxes, u.Id, MapSize, baseSt.Flight, null, _air);

            // 2) 탄종. 플레이어는 FIRE 페이즈에서 골랐고, AI 는 착탄점을 본 뒤 고른다(§8) — 탄도가 같으니 조준을 다시 풀 필요가 없다.
            var shell = u.Shell;
            bool ss = IsPlayerTurn && _useSs;
            bool ult = IsPlayerTurn && _useUlt;
            if (!IsPlayerTurn && res.Hit)
            {
                var foes = new List<AiGunner.Target>();
                foreach (var o in _units)
                    if (o.Alive && o.Team != u.Team) foes.Add(new AiGunner.Target { Id = o.Id, Kind = o.Kind, Center = o.Center, Defense = o.St.Defense });
                Vec3? satImpact = null; int satDirect = -1;
                if (ShellEffects.Of(u.Kind, ShellKind.Special).Type == ShellEffects.EffectType.SatelliteStrike)
                    satImpact = SatelliteStrike.Resolve(_vol, res.Impact.X, res.Impact.Z, res.Impact.Y + 60f, boxes, out satDirect);
                var normalHits = AiGunner.SimulatePattern(_vol, p0, worldYaw, pitch, speed, accel, boxes, u.Id, MapSize,
                                                          Spread.Pattern(u.Kind, ShellKind.Normal), res, baseSt.Flight, null, _air);
                var specialHits = satImpact.HasValue ? null
                    : AiGunner.SimulatePattern(_vol, p0, worldYaw, pitch, speed, accel, boxes, u.Id, MapSize,
                                               Spread.Pattern(u.Kind, ShellKind.Special), res, baseSt.Flight, null, _air);
                shell = AiGunner.PickShell(baseSt, normalHits, specialHits, foes, _status, satImpact, satDirect);
                // ⚠️ 궁극기를 먼저 보고, 못 쓰면 **아낄지 말지**를 정한다.
                //    아끼는 단계가 없으면 2점에서 SS 로 다 써버려 궁극기가 영원히 안 나온다
                //    (하네스에서 궁극기 ON/OFF 승률이 똑같이 나와 들통난 사고 — NiceShot.AiSaveForUltimate 머리말).
                ult = shell == ShellKind.Special && u.Skill.CanUltimate();
                ss = !ult && shell == ShellKind.Special && u.Skill.CanSs()
                     && !NiceShot.AiSaveForUltimate(u.Skill.Points, ref _aiRng);
                // AI 나이스샷 — 게이지 정지 정밀도를 난이도별 확률로 대신한다 [추정]
                if (NiceShot.AiJudge(AiDifficulty, ref _aiRng)) u.Skill.OnNiceShot();
            }
            // 연출 확인용 강제(-forcespecial) — AI 뿐 아니라 자동사격의 플레이어 탱크(ForceFire 경로)에도 걸어야 아군 사격에서도 보인다
            if (_forceSpecial) shell = ShellKind.Special;
            // -forceult: 핵(버섯구름) 확인용. 게이지를 **실제로 채워서** 연다 —
            // 플래그로 우회하면 "연출은 되는데 게임에서는 영영 안 열리는" 상태를 못 잡는다(궁극기가 한 번도 발동 안 되던 사고).
            if (_forceUlt) { while (!u.Skill.CanUltimate()) u.Skill.OnNiceShot(); ult = true; }
            var st = TankStats.For(u.Kind, shell, u.HpFrac, u.W);
            if (shell == ShellKind.Special && ult && u.Skill.CanUltimate()) { st = NiceShot.ApplyUltimate(st, u.Kind); u.Skill.SpendUltimate(); }
            else if (shell == ShellKind.Special && ss && u.Skill.CanSs()) { st = NiceShot.ApplySs(st); u.Skill.SpendSs(); }
            // 파워업: 원작 분류가 "능력 아이템(공격력 강화)". ⚠️ 속도를 올리면 이미 끝낸 조준이 통째로 빗나간다.
            if (_items.HasPowerUp(u.Id)) { st.BaseDamage *= Items.PowerUpScale; st.DirectDamage *= Items.PowerUpScale; }
            _shooterStats = st; _shooterKind = u.Kind; _shooterShell = shell; _shooterUlt = ult;
            _shooterId = u.Id;
            // ⚠️ 쏜 뒤 일반탄으로 되돌린다. 안 그러면 다음 턴에 "고른 적 없는 2번탄"이 나가는 것처럼 보인다.
            u.Shell = ShellKind.Normal; _useSs = false; _useUlt = false;

            // 3) 다탄두: 고른 탄종의 패턴대로 전부 계산하고, 부탄은 각자 궤적으로 같이 날린다(카메라는 중앙 탄).
            var pattern = Spread.Pattern(u.Kind, shell, ult);
            _pendingShots.Clear();
            _subPaths.Clear();
            // 유도탄(2번탄)은 기준 궤적과 달리 정점 뒤에 휘므로 중앙 탄을 **유도 포함**으로 다시 푼다(부탄도 같은 프로파일).
            var centerRes = st.Flight.HasHoming
                ? ProjectileSimulator.Simulate(_vol, p0, Ballistics.VelocityFrom(worldYaw, pitch, speed), accel, boxes, u.Id, MapSize, st.Flight, homingOk)
                : res;
            foreach (var pt in pattern)
            {
                bool center = pt.YawOffsetDeg == 0f && pt.PitchOffsetDeg == 0f;
                var sub = pattern.Count == 1 || center ? centerRes
                    : ProjectileSimulator.Simulate(_vol, p0, Ballistics.VelocityFrom(worldYaw + pt.YawOffsetDeg, pitch + pt.PitchOffsetDeg, speed), accel, boxes, u.Id, MapSize, st.Flight, homingOk, _air);
                // 기후(§2-9-15): 증폭벽을 지난 탄은 피해 1.5배(원작). 부탄마다 따로 판정한다.
                if (sub.Hit) _pendingShots.Add((sub.Impact, sub.DirectHitTankId, pt.DamageScale * sub.DamageScale));
                if (sub.DamageScale > 1f) _log += "  [증폭벽]";
                if (sub.Tornadoed) _log += "  [회오리]";
                if (!center && sub.Path != null && sub.Path.Count > 1) _subPaths.Add(sub.Path);
            }
            while (_subShells.Count < _subPaths.Count)
            {
                var sg2 = new GameObject($"SubShell{_subShells.Count}");
                sg2.AddComponent<MeshFilter>().sharedMesh = ProceduralTank.Shell(u.Kind, shell);
                sg2.AddComponent<MeshRenderer>().sharedMaterial = MakeMat(ProceduralTank.ShellColor(u.Kind, shell), 0.4f);
                sg2.transform.localScale = Vector3.one * (ShellScale * 0.78f);   // 부탄은 조금 작게 — 중앙 탄이 주인공이다
                _subShells.Add(sg2.transform);
            }
            for (int si = 0; si < _subShells.Count; si++) _subShells[si].gameObject.SetActive(si < _subPaths.Count);

            _trailStyle = ShellTrail.Of(u.Kind, shell, ult);
            // 초기 유도·자세 제어(미사일 계열만). 발사 방향은 궤적 첫 구간에서 읽으므로 따로 들고 다닐 게 없다.
            _guide = GuidanceProfile.Of(u.Kind, st.Flight);
            _shotPath = centerRes.Path; _shotT = 0f;
            _pendingImpact = centerRes.Impact; _pendingDirect = centerRes.DirectHitTankId;

            if (_shell == null)
            {
                var sg = new GameObject("Shell");
                sg.AddComponent<MeshFilter>();
                sg.AddComponent<MeshRenderer>();
                _shell = sg.transform;
                _shellVisualKind = (TankKind)(-1);
            }
            // 탄 모양·색은 **기종과 탄종**을 따른다(ProceduralTank.Shell 머리말의 원작 근거).
            // 매 발 새로 만들면 GC 가 튀므로 바뀔 때만 다시 만든다.
            if (_shellVisualKind != u.Kind || _shellVisualShell != shell)
            {
                _shellVisualKind = u.Kind; _shellVisualShell = shell;
                _shell.GetComponent<MeshFilter>().sharedMesh = ProceduralTank.Shell(u.Kind, shell);
                _shell.GetComponent<MeshRenderer>().sharedMaterial =
                    MakeMat(ProceduralTank.ShellColor(u.Kind, shell), 0.4f);
                _shell.localScale = Vector3.one * ShellScale;
                foreach (var tr in _subShells)
                {
                    tr.GetComponent<MeshFilter>().sharedMesh = ProceduralTank.Shell(u.Kind, shell);
                    tr.GetComponent<MeshRenderer>().sharedMaterial = _shell.GetComponent<MeshRenderer>().sharedMaterial;
                }
            }
            // 더블파이어: 같은 각도·파워를 기억해 착탄 뒤 한 발 더 쏜다(원작 "같은 힘과 각도").
            if (_items.HasDoubleFire(u.Id) && !_pendingDoubleFire)
            { _pendingDoubleFire = true; _dfYaw = turretYaw; _dfPitch = pitch; _dfPower = power; }
            _shell.gameObject.SetActive(res.Hit || res.Path.Count > 1);

            // ⚠️ 자취는 **탄이 만들어진 뒤에** 붙인다 — 위에서 붙였더니 첫 발은 `_shell` 이 아직 null 이라 자취가 없었다.
            if (_fx == null) _fx = new GameObject("ParticleFx").AddComponent<ParticleFx>();
            _fx.AttachTrail(_shell, _trailStyle);
            // 부탄(다탄두)도 같이 — 중앙 탄만 남기면 9연이 한 발처럼 보인다.
            for (int si = 0; si < _subShells.Count && si < _subPaths.Count; si++)
                _fx.AttachTrail(_subShells[si], _trailStyle);
            _fx.MuzzleFlash(u.Fire.position, u.Fire.forward, u.Kind, shell);
            _phase = Phase.Flying;
            _log = $"{(u.Team == 0 ? "아군" : "적군")} 발사 — 각 {pitch:F0}° 파워 {power * 100:F0} 바람 {_wind.magnitude:F1}"
                 + (shell == ShellKind.Special ? $"  [{st.Name}]" : "") + (pattern.Count > 1 ? $" ×{pattern.Count}" : "");
        }

        /// <summary>
        /// 비행 중 탄 회전(연출, 2026-09-17). 포탄·돌은 구르고, 지뢰는 원반처럼 돌고, 바람칼은 날 축으로 돈다.
        /// 미사일·화살·빔은 진행 방향을 지켜야 하므로 거의 안 돈다. 위치·탄도에는 아무 영향이 없다.
        /// </summary>
        static Quaternion ShellSpin(TankKind k, int phase = 0)
        {
            float t = Time.time + phase * 0.37f;
            switch (k)
            {
                case TankKind.Cannon:      return Quaternion.AngleAxis(t * 420f, Vector3.right);
                case TankKind.Catapult:    return Quaternion.AngleAxis(t * 260f, Vector3.right) * Quaternion.AngleAxis(t * 90f, Vector3.up);
                case TankKind.Duke:        return Quaternion.AngleAxis(t * 240f, Vector3.right);
                case TankKind.Carrot:      return Quaternion.AngleAxis(t * 200f, Vector3.forward);
                case TankKind.MineLander:  return Quaternion.AngleAxis(t * 540f, Vector3.up);
                case TankKind.SecWind:     return Quaternion.AngleAxis(t * 900f, Vector3.forward);
                case TankKind.IonAttacker: return Quaternion.AngleAxis(t * 160f, Vector3.forward);
                case TankKind.Poseidon:    return Quaternion.AngleAxis(Mathf.Sin(t * 6f) * 12f, Vector3.right);
                case TankKind.Missile:
                case TankKind.MultiMissile:
                case TankKind.SuperTank:   return Quaternion.AngleAxis(t * 70f, Vector3.forward);
                default:                   return Quaternion.identity;
            }
        }

        void FlyStep(float dt)
        {
            if (_shotPath == null || _shotPath.Count < 2) { Impact(); return; }
            _shotT += dt / Ballistics.SimStep;                 // path 는 SimStep 간격
            int i = Mathf.FloorToInt(_shotT);
            if (i >= _shotPath.Count - 1) { Impact(); return; }
            var a = _shotPath[i]; var b = _shotPath[i + 1];
            float f = _shotT - i;
            _shell.position = new Vector3(Mathf.Lerp(a.X, b.X, f), Mathf.Lerp(a.Y, b.Y, f), Mathf.Lerp(a.Z, b.Z, f));
            float flight = _shotT * Ballistics.SimStep;                     // path 인덱스 → 비행 경과(초)
            Attitude(_shell, _shotPath, i, flight, dt, 0);

            // 부탄: 같은 시각(_shotT)의 자기 궤적 위치. 먼저 떨어진 부탄은 숨긴다(착탄 처리는 중앙 탄 착탄 때 한꺼번에).
            for (int si = 0; si < _subPaths.Count; si++)
            {
                var sp = _subPaths[si]; var tr = _subShells[si];
                if (i >= sp.Count - 1) { tr.gameObject.SetActive(false); continue; }
                var sa = sp[i]; var sb = sp[i + 1];
                tr.position = new Vector3(Mathf.Lerp(sa.X, sb.X, f), Mathf.Lerp(sa.Y, sb.Y, f), Mathf.Lerp(sa.Z, sb.Z, f));
                Attitude(tr, sp, i, flight, dt, si + 1);
            }
        }

        /// <summary>
        /// 탄의 **자세**. 탄이 길쭉해졌으므로(미사일·레이저) 그냥 두면 옆으로 누워 날아간다.
        ///
        /// 기본은 "속도 방향을 본다". 미사일 계열만 Sim/Guidance 의 초기 유도·자세 제어를 얹어
        /// 발사 직후 **노즈를 하늘로 세웠다가**(수직 상승) 속도 방향으로 꺾어 눕는다(Pitch-over).
        /// 그 동안 TVC·측추력기 연출이 붙는다. ⚠️ 위치는 여기서 안 건드린다 — 궤적은 이미 확정돼 있다.
        /// </summary>
        void Attitude(Transform tr, List<Vec3> path, int i, float t, float dt, int spinPhase)
        {
            var a = path[i]; var b = path[i + 1];
            var dir = new Vector3(b.X - a.X, b.Y - a.Y, b.Z - a.Z);
            if (dir.sqrMagnitude <= 1e-10f) return;
            var spin = ShellSpin(_shooterKind, spinPhase);
            if (!_guide.Has || path.Count < 2) { tr.rotation = Quaternion.LookRotation(dir) * spin; return; }

            // 선회율(초당 도) — 직전 구간과의 각 변화. 유도탄이 정점 뒤 꺾을 때 날개·측추력기가 물리는 근거다.
            float turnDeg = 0f;
            if (i >= 1)
            {
                var pv = new Vector3(a.X - path[i - 1].X, a.Y - path[i - 1].Y, a.Z - path[i - 1].Z);
                if (pv.sqrMagnitude > 1e-10f)
                    turnDeg = Vector3.Angle(pv, dir) / Ballistics.SimStep;
            }
            var launch = path[1] - path[0];
            var att = Guidance.At(t, dt, new Vec3(dir.x, dir.y, dir.z), launch, _guide, turnDeg);
            tr.rotation = Quaternion.LookRotation(new Vector3(att.Nose.X, att.Nose.Y, att.Nose.Z)) * spin;
            if (_fx != null) _fx.Guidance(tr, att, _trailStyle);
        }

        /// <summary>
        /// 설치물 스냅샷을 화면에 맞춘다. 지뢰는 공, 장판(지속불·독구름)은 **파티클 루프**(ParticleFx.SetFields).
        /// ⚠️ 처음엔 장판을 얇은 원반 하나로 그렸는데 크레이터가 사발 모양이라 중앙 높이의 원반은 사발 벽 아래로 들어가
        ///    화면에 아무것도 안 보였다(스크린샷으로 확인). 구슬 고리를 거쳐 지금은 위로 솟는 불꽃/안개라 사발 안에서도 보인다.
        /// </summary>
        void RefreshHazards()
        {
            _hazards.Snapshot(_hazardBuf);
            if (_mineMat == null)
            {
                _mineMat = MakeMat(new Color(0.12f, 0.12f, 0.12f), 0.5f);
            }
            int used = 0;
            Transform Take()
            {
                if (used >= _hazardGos.Count)
                {
                    var go = new GameObject($"Hazard{_hazardGos.Count}");
                    go.AddComponent<MeshFilter>().sharedMesh = ProceduralTank.Ball(1f, 10);
                    go.AddComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    _hazardGos.Add(go.transform);
                }
                var t = _hazardGos[used++];
                t.gameObject.SetActive(true);
                return t;
            }
            float GroundAt(float x, float z, float nearY)
            {
                float g = TankGroundProbe.GroundBelow(_vol, x, z, nearY + 6f);
                return float.IsNegativeInfinity(g) ? nearY : g;
            }
            // 장판(지속불·독구름)은 파티클 루프(ParticleFx.SetFields) — 예전 구슬 고리는 걷어냈다(머리말 이유는 그대로:
            // 사발 크레이터 안에서도 불꽃은 위로 오르니 어디서나 보인다). 지면 높이는 스냅샷 Y 대신 실제 지면을 잰다.
            for (int i = 0; i < _hazardBuf.Count; i++)
            {
                var h = _hazardBuf[i];
                if (h.Kind == 0)
                {
                    var tr = Take();
                    tr.GetComponent<MeshRenderer>().sharedMaterial = _mineMat;
                    tr.position = new Vector3(h.X, GroundAt(h.X, h.Z, h.Y) + 0.5f, h.Z);
                    tr.localScale = Vector3.one * 0.6f;
                    continue;
                }
                h.Y = GroundAt(h.X, h.Z, h.Y);
                _hazardBuf[i] = h;
            }
            for (int i = used; i < _hazardGos.Count; i++) _hazardGos[i].gameObject.SetActive(false);
            EnsureFx().SetFields(_hazardBuf);
        }

        /// <summary>착탄 뒤 잠깐 남는 연출(위성탄 빔)의 수명. Update 와 AutoShotStep 양쪽에서 부른다.</summary>
        void TickFx(float dt)
        {
            if (_beam != null && _beam.gameObject.activeSelf)
            {
                _beamTimer -= dt;
                if (_beamTimer <= 0f) _beam.gameObject.SetActive(false);
            }
        }

        /// <summary>위성탄: 착탄점 위 60m 하늘에서 수직으로 꽂히는 빔. 원작은 화면 위에서 레이저가 내려온다.</summary>
        void ShowSatelliteBeam(Vec3 at)
        {
            // 예전엔 실린더 메시(그림자가 맵을 가로질러 껐던 그것)였다 — 지금은 파티클 기둥 + 낙하 스파크 + 바닥 링.
            EnsureFx().SatelliteBeam(new Vector3(at.X, at.Y, at.Z), 60f);
        }

        void Impact()
        {
            if (_fx != null)
            {
                _fx.StopTrail(_shell); _fx.StopGuidance(_shell);
                foreach (var tr in _subShells) { _fx.StopTrail(tr); _fx.StopGuidance(tr); }
            }
            if (_shell != null) _shell.gameObject.SetActive(false);
            foreach (var tr in _subShells) tr.gameObject.SetActive(false);
            _subPaths.Clear();
            _shotPath = null;

            var c = _pendingImpact;
            if (_pendingShots.Count == 0 && c.LengthSq > 0.001f) _pendingShots.Add((c, _pendingDirect, 1f));
            // 연습장 채점용 — 다탄두면 중앙 탄(첫 발)을 기준으로 잰다.
            // ⚠️ 거리를 **여기서** 확정한다. 아래에서 지형이 깎이면 표적이 크레이터로 떨어지는데,
            //    턴이 넘어간 뒤에 재면 그 낙하 거리까지 오차로 세어 5m 씩 부풀었다(실측으로 잡음).
            if (_practice)
            {
                _practiceImpact = _pendingShots.Count > 0 ? _pendingShots[0].impact : c;
                _practiceMissAtImpact = float.MaxValue;
                foreach (var o in _units)
                {
                    if (o.Team == 0) continue;
                    var d = o.Center - _practiceImpact;
                    float dist = Mathf.Sqrt(d.X * d.X + d.Y * d.Y + d.Z * d.Z);
                    if (dist < _practiceMissAtImpact) _practiceMissAtImpact = dist;
                }
            }
            if (_pendingShots.Count > 0)
            {
                Sfx.Boom(_shooterStats.CraterRadius / 16f);      // 굴착 반경이 클수록 낮고 크게
                var fx = ShellEffects.Of(_shooterKind, _shooterShell);
                var boxes = new List<TankHitbox>();
                foreach (var o in _units) if (o.Alive) boxes.Add(new TankHitbox { Id = o.Id, Center = o.Center, Radius = TankRadius });
                float craterEach = CraterShape.PerShotCrater(_shooterStats.CraterRadius, _pendingShots.Count);
                var swB = _timeEvents ? System.Diagnostics.Stopwatch.StartNew() : null;
                string dmgLog = "";
                var impacts = new List<Vec3>();          // 보급 상자 파괴 판정용(아래에서 쓴다)
                foreach (var ps in _pendingShots)
                {
                    Vec3 impact = ps.impact; int directId = ps.direct;
                    // 유도탄: 근처 적에게 끌린다(원작 "녹색 유도탄")
                    if (fx.Type == ShellEffects.EffectType.Homing)
                    {
                        int myTeam = Current.Team;
                        impact = ShellEffects.HomingCorrect(impact, boxes, Current.Id, 0, id => { var x = _units.Find(o => o.Id == id); return x != null && x.Team != myTeam; }, out int hd);
                        if (hd >= 0) directId = hd;
                    }
                    // 위성탄: 착탄 X·Z 의 하늘에서 수직 낙하 — 오버행을 무시한다(원작)
                    if (fx.Type == ShellEffects.EffectType.SatelliteStrike)
                    {
                        impact = SatelliteStrike.Resolve(_vol, impact.X, impact.Z, impact.Y + 60f, boxes, out directId);
                        ShowSatelliteBeam(impact);
                        dmgLog += "  [위성탄]";   // HUD 폰트에 🛰 글리프가 없어 □ 로 찍혔다
                    }

                    impacts.Add(impact);
                    // 지형 파괴 → 천장 붕괴(§7-6-1) → 영향 청크만 재생성
                    if (_fx == null) _fx = new GameObject("ParticleFx").AddComponent<ParticleFx>();
                    _fx.Blast(new Vector3(impact.X, impact.Y, impact.Z), craterEach,
                              MapTheme.Of(_map, _weather == Weather.Snow).RockDark, _shooterKind, _shooterShell, _shooterUlt);

                    // 탄마다 파이는 모양이 다르다(원작 근거는 CraterShape). 게임과 하네스가 같은 표를 쓴다.
                    float vscale = CraterShape.VScaleOf(_shooterKind, _shooterShell);
                    var blast = SdfDeformer.SubtractSphere(_vol, new BlastRequest(impact.X, impact.Y, impact.Z, craterEach, vscale));
                    var collapse = CeilingCollapse.Apply(_vol, blast);
                    _terrain.ApplyDirty(CeilingCollapse.Union(blast, collapse));

                    // 피해(§6) + 맞은 유닛에 붙는 효과
                    foreach (var o in _units)
                    {
                        if (!o.Alive) continue;
                        float dist = (o.Center - impact).Length;
                        bool direct = o.Id == directId;
                        if (dist > _shooterStats.BlastRadius && !direct) continue;
                        int dmg = Damage.AfterDefense(
                            Damage.Compute(dist, _shooterStats.BlastRadius, _shooterStats.BaseDamage * ps.scale, _shooterStats.DirectDamage * ps.scale, direct),
                            o.St.Defense);
                        if (dmg <= 0) continue;
                        // 실드(§2-9-10): 들어오는 공격 1회를 통째로 막는다(원작).
                        if (_items.ConsumeShield(o.Id)) { _fx.ShieldBlock(new Vector3(o.Center.X, o.Center.Y, o.Center.Z)); dmgLog += $"  [{(o.Team == 0 ? "아군" : "적군")}{o.Id % 3 + 1} 실드]"; continue; }
                        o.Hp = Mathf.Max(0, o.Hp - dmg);
                        CountDamage(o, dmg, direct ? $"-{dmg} 직격" : $"-{dmg}", true);
                        dmgLog += $"  {(o.Team == 0 ? "아군" : "적군")}{o.Id % 3 + 1} −{dmg}{(direct ? "(직격)" : "")}";
                        if (fx.Type == ShellEffects.EffectType.Poison) { _status.Poison(o.Id, fx.Param1, fx.Param2, o.Kind); dmgLog += "[독]"; }
                        if (fx.Type == ShellEffects.EffectType.Root) { _status.Root(o.Id, fx.Param1); dmgLog += "[속박]"; }
                        // 방해탄(§2-9-14): 맞은 적에게 건다.
                        var imk = _items.ImpairShot(Current.Id);
                        if (imk != ImpairKind.None && o.Team != Current.Team)
                        { _impair.Apply(o.Id, imk); dmgLog += $"[{Impair.Name(imk)}]"; }
                        _fx.Hit(new Vector3(o.Center.X, o.Center.Y, o.Center.Z), direct);
                        o.Jolt = direct ? 1f : 0.6f;                              // 차체가 들썩인다(GroundUnit)
                        // ⚠️ 격파는 KillUnit 을 쓴다(잔해·연기 연출). 원격 쪽 `Root.SetActive(false)` 로 되돌리지 마라 — 탱크가 그냥 사라진다.
                        if (!o.Alive) { KillUnit(o); dmgLog += "☠"; HitStop(0.25f, 0.6f); }
                        else if (direct) HitStop(0.35f, 0.35f);
                    }
                    // 자리에 남는 효과
                    // 눈이면 불·독가스는 아예 안 남는다(원작 규칙 — SetWeather 머리말의 출처 참조).
                    bool snowKillsField = _weather == Weather.Snow;
                    if (fx.Type == ShellEffects.EffectType.Burn)
                    { if (snowKillsField) dmgLog += "  [지속불 — 눈에 꺼짐]"; else { _hazards.PlaceFire(impact.X, impact.Y, impact.Z, _shooterStats.BlastRadius, fx.Param1, fx.Param2); dmgLog += "  [지속불]"; } }
                    if (fx.Type == ShellEffects.EffectType.PoisonCloud)
                    { if (snowKillsField) dmgLog += "  [독구름 — 눈에 꺼짐]"; else { _hazards.PlaceFire(impact.X, impact.Y, impact.Z, _shooterStats.BlastRadius, fx.Param1, fx.Param2, 1); dmgLog += "  [독구름]"; } }
                    if (fx.Type == ShellEffects.EffectType.Mine) { _hazards.PlaceMine(impact.X, impact.Y, impact.Z, 4f, fx.Param1, -1); _fx.MinePlaced(new Vector3(impact.X, impact.Y, impact.Z)); dmgLog += "  [지뢰 설치]"; }
                }
                if (swB != null) Debug.Log($"[Tankfall] EVENT 착탄처리 ×{_pendingShots.Count} {swB.Elapsed.TotalMilliseconds:F2} ms");
                // 폭발이 나무·바위를 쓸어낸다. 안 지우면 파인 자리 위에 공중 나무가 남는다(§7-6-1 과 같은 문제).
                if (_scatter != null)
                    foreach (var ps in impacts)
                        _scatter.DestroyNear(new Vector3(ps.X, ps.Y, ps.Z), craterEach + 1.5f);

                if (_hitThisShot && _shooterTeam >= 0) { _stat[_shooterTeam].Hits++; _hitThisShot = false; }
                _pendingShots.Clear();
                RefreshHazards();
                RefreshStatusFx();
                // 헬기 보급(§2-9-11): 폭발이 상자를 부순다(원작 — 안의 아이템도 사라진다). 지형이 깎였으니 남은 건 내려앉힌다.
                if (_itemSlots > 0 && _supply.Count > 0)
                {
                    int broke = 0;
                    foreach (var ip in impacts) broke += _supply.DestroyNear(ip.X, ip.Y, ip.Z, _shooterStats.BlastRadius);
                    _supply.Settle((x, z) => TankGroundProbe.GroundBelow(_vol, x, z, 80f));
                    if (broke > 0) dmgLog += $"  [보급 상자 {broke}개 파괴]";
                    RefreshCrates();
                }

                // 발밑이 사라진 탱크는 떨어지면서 피해를 입는다(§28). 폭발 피해와 별개다.
                // ⚠️ GroundUnit 에 넣으면 안 된다 — 그건 매 프레임 돌아서 낙하 피해가 무한히 누적된다.
                foreach (var o in _units)
                {
                    if (!o.Alive) continue;
                    Vector3 op = o.Pos;
                    float g = TankGroundProbe.GroundBelow(_vol, op.x, op.z, op.y + 0.5f);
                    if (float.IsNegativeInfinity(g)) continue;
                    int fall = Damage.AfterDefense(Damage.FromFall(op.y - GroundVisualLift - g), o.St.Defense);
                    if (fall <= 0) continue;
                    o.Hp = Mathf.Max(0, o.Hp - fall);
                    CountDamage(o, fall, $"-{fall} 낙하", false);
                    dmgLog += $"  {(o.Team == 0 ? "아군" : "적군")}{o.Id % 3 + 1} −{fall}(낙하 {op.y - GroundVisualLift - g:F0}m)";
                    if (!o.Alive) { KillUnit(o); dmgLog += "☠"; }
                }
                _log = dmgLog.Length > 0 ? "착탄!" + dmgLog : "착탄 — 빗나감";

                // 지형이 꺼졌을 수 있으니 전원 재접지
                foreach (var o in _units) if (o.Alive) GroundUnit(o, 0f);
            }
            else _log = "불발 (맵 밖)";

            _impactFocus = new Vector3(c.X, c.Y, c.Z);
            _hasImpactFocus = c.LengthSq > 0.001f;
            _phase = Phase.Resolve;
            _phaseTimer = 1.4f;
        }

        /// <summary>
        /// 서든데스(§2-5-1). **지형 파괴 되먹임 나선을 끊는 유일한 수단**이다.
        ///
        /// 실측: 크레이터가 쌓이면 같은 AI·같은 오차인데도 명중률이 40턴 만에 75.8% → 4.2% 로 떨어진다.
        /// 그러면 판이 안 끝난다(오차 6% 에서 무승부 28/40, 평균 95.8분). 6라운드 상한을 걸면 0/40 · 15.4분.
        /// 정상적으로 끝나는 판(오차 3% 이하)은 이 상한에 닿지도 않으므로 전혀 건드리지 않는다.
        ///
        /// ⚠️ 지형을 되돌리는 방식으로 고치지 마라 — `TankGroundProbe` 머리말의 금지 사항이다.
        ///    "포격으로 만든 지형이 무의미해진다."
        /// </summary>
        const int SuddenDeathRound = 6;
        const float SuddenDeathPct = 0.05f;

        /// <summary>
        /// 피해를 전적에 쌓고 맞은 자리에 숫자를 띄운다. 폭발·낙하 **두 경로가 전부 여기만** 쓴다 —
        /// 한쪽이 직접 더하면 명중률이 조용히 어긋난다(§2-9-1 의 교훈: 같은 로직이 여러 곳에 살면 재발한다).
        /// </summary>
        /// <param name="credited">쏜 쪽의 "명중"으로 셀지. 낙하 피해는 지형이 준 것이라 명중으로 세지 않는다.</param>
        void CountDamage(Unit victim, int dmg, string popup, bool credited)
        {
            if (dmg <= 0) return;
            if (credited) Sfx.Hit();
            _stat[victim.Team].Taken += dmg;
            if (credited && _shooterTeam >= 0)
            {
                _stat[_shooterTeam].Damage += dmg;
                if (_shooterTeam != victim.Team) _hitThisShot = true;
            }
            AddPopup(victim.Pos, popup, victim.Team == 0 ? Ui.Enemy : Ui.Good);
        }

        /// <summary>이번 사격이 적을 맞혔는가. 한 발이 여러 명을 맞혀도 명중은 1 이다(명중률의 분모가 발수라서).</summary>
        bool _hitThisShot;

        void ApplySuddenDeath()
        {
            if (_round <= SuddenDeathRound) return;
            int downed = 0;
            foreach (var u in _units)
                if (u.Alive)
                {
                    u.Hp = Mathf.Max(0, u.Hp - Mathf.Max(1, Mathf.RoundToInt(u.St.Hp * SuddenDeathPct)));
                    if (!u.Alive) { downed++; KillUnit(u); }
                }
            _log = $"☠ 서든데스 {_round}라운드 — 전원 최대HP {SuddenDeathPct * 100:F0}%" + (downed > 0 ? $" ({downed}대 격파)" : "");
        }

        /// <summary>
        /// 방금 행동한 유닛의 딜레이를 쌓고(탄종·이동 추가 [추정]) 누적 최소 유닛을 _turn 으로.
        /// NextTurn 과 자동사격(AutoShotStep) 두 경로가 **반드시 이것만** 쓴다 — 한쪽이 교대로 남으면 재발한다.
        /// </summary>
        void AdvanceTurn()
        {
            var prev = Current;
            _order.Consume(prev.Id, prev.Shell, prev.Gauge < MoveGaugeMax - 0.01f);
            int nextId = _order.Next(id => { var x = _units.Find(o => o.Id == id); return x != null && x.Alive; });
            _turn = _units.FindIndex(o => o.Id == nextId);
        }

        void NextTurn()
        {
            if (_practice) { PracticeNextTurn(); return; }

            // 텔레포트탄: 내 탱크를 착탄 자리로 옮긴다(원작). 지면 위로 올려 세운다.
            if (_shooterId >= 0 && _items.HasTeleport(_shooterId) && _pendingImpact.LengthSq > 0.001f)
            {
                var me = _units.Find(x => x.Id == _shooterId);
                if (me != null && me.Alive)
                {
                    float g = TankGroundProbe.GroundBelow(_vol, _pendingImpact.X, _pendingImpact.Z, _pendingImpact.Y + 40f);
                    if (!float.IsNegativeInfinity(g))
                    {
                        me.Root.position = new Vector3(_pendingImpact.X, g + GroundVisualLift, _pendingImpact.Z);
                        _log = "[아이템] 텔레포트탄 — 착탄 자리로 이동";
                    }
                }
            }
            // 더블파이어: 같은 각도·파워로 한 발 더. 턴은 넘기지 않는다.
            if (_pendingDoubleFire)
            {
                _pendingDoubleFire = false;
                var me = _units.Find(x => x.Id == _shooterId);
                if (me != null && me.Alive)
                {
                    _items.ClearShotFlags(me.Id);      // 두 번째 발이 또 더블파이어가 되면 무한이다
                    _log = "[아이템] 더블파이어 — 한 발 더";
                    FireFrom(me, _dfYaw, _dfPitch, _dfPower);
                    return;
                }
            }
            if (_shooterId >= 0) _items.ClearShotFlags(_shooterId);

            ApplySuddenDeath();

            int aliveA = 0, aliveB = 0;
            foreach (var u in _units) { if (!u.Alive) continue; if (u.Team == 0) aliveA++; else aliveB++; }
            if (aliveA == 0 || aliveB == 0)
            {
                // 동시 전멸은 무승부다. 서든데스(전원 최대HP 5%)가 양 팀을 같은 턴에 0 으로 만들 수 있고,
                // 다탄두·지진도 그렇다 — 예전엔 이 경우가 "적군 승리"로 찍혔다(aliveA==0 을 먼저 봤다).
                bool draw = aliveA == 0 && aliveB == 0;
                _winner = WinnerText(aliveA, aliveB);
                _phase = Phase.GameOver;
                // 사람이 켠 판만 결과 화면으로. 하네스는 GameOver 상태 그대로 두고 자기 종료 조건을 쓴다.
                if (!_autoShot && !_phaseCheck && !_gallery && !_perf && !_supplySelfTest && !_uiSelfTest)
                {
                    _screen = GameScreen.Result;
                    if (draw) Sfx.Confirm(); else if (aliveA > 0) Sfx.Win(); else Sfx.Lose();
                    // 전적은 사람이 AI 와 붙은 판만. 연습장(적이 안 쏜다)·자동 모드는 안 센다.
                    if (!_practice && !_headless && !_autoMode)
                        Prefs.Record(_difficulty, draw ? Prefs.Outcome.Draw : aliveA > 0 ? Prefs.Outcome.Win : Prefs.Outcome.Lose);
                }
                Debug.Log($"[Tankfall] {_winner} · 라운드 {_round} · {_battleClock / 60f:F1}분 · " +
                          $"아군 {_stat[0].Hits}/{_stat[0].Shots}발 {_stat[0].Damage}딜 · 적군 {_stat[1].Hits}/{_stat[1].Shots}발 {_stat[1].Damage}딜");
                return;
            }

            // 방금 행동한 유닛의 딜레이를 쌓고(탄종·이동 추가 [추정]) 누적 최소를 다음으로
            // 턴 시작 효과: 독·화상 tick, 지속불 수명, 서 있는 자리의 설치물. 죽으면 다음 유닛으로.
            for (int guard = 0; guard < 12; guard++)
            {
                AdvanceTurn();
                var cu = Current;
                _hazards.TickStartOfTurn();
                int dot = Damage.AfterDefense(_status.TickStartOfTurn(cu.Id), cu.St.Defense);
                int hz = Damage.AfterDefense(_hazards.OnUnitAt(cu.Id, cu.Kind, cu.Center), cu.St.Defense);
                RefreshHazards();
                RefreshStatusFx();
                if (dot + hz <= 0) break;
                cu.Hp = Mathf.Max(0, cu.Hp - dot - hz);
                _log = $"{(cu.Team == 0 ? "아군" : "적군")}{cu.Id % 3 + 1} 턴 시작 피해{(dot > 0 ? $" 지속 −{dot}" : "")}{(hz > 0 ? $" 설치물 −{hz}" : "")}";
                if (cu.Alive) break;
                KillUnit(cu);
            }
            _niceFlash = null;
            // 라운드 = 행동 수 / 유닛 수. 서든데스가 이 값을 본다
            int round = _order.ActionsTaken / _units.Count + 1;
            if (round != _round) { _round = round; RollWind(); RollSupply(); }

            TryPickupSupply(Current);        // 상자 위에 서 있으면 이동 없이도 줍는다
            _items.TickStartOfTurn(Current.Id);
            _impair.TickStartOfTurn(Current.Id);
            if (_boom) BoomTurnStart();
            // 바보탄: 카메라가 내 자리로 안 오게 **엉뚱한 탱크 자리**에 붙여둔다(원작 "화면이 자신의 위치로 오지 않는다").
            if (_impair.Has(Current.Id, ImpairKind.Confuse))
            {
                var others = _units.FindAll(x => x.Alive && x.Id != Current.Id);
                _confuseFocus = others.Count > 0
                    ? others[Random.Range(0, others.Count)].Pos + Vector3.up * 2.2f
                    : Current.Pos + Vector3.up * 2.2f;
            }
            RefreshFogVisibility();
            Current.Gauge = MoveGaugeMax * _items.MoveScale(Current.Id);   // 이동증가(원작 5턴 2배)
            _aiMoveDecided = false; _aiMoveLeft = 0f;   // ⚠️ 안 버리면 다음 AI 턴이 지난 턴 계획을 이어 쓴다
            _itemSel = 0;
            _power = 0f; _charging = false;
            _phase = Phase.Move;
            _phaseTimer = MovePhaseSec;
            if (_phaseCheck) BeginPhaseCheckTurn();
            if (IsPlayerTurn) _log = "MOVE — WASD 이동, 스페이스/우클릭으로 조준 진입";
        }

        // ---------------- 아이템(§2-9-10) ----------------

        /// <summary>
        /// 아이템 사용. 회복·날씨·바람은 Sim 이 유닛/씬을 모르므로 여기서 델리게이트로 넘긴다.
        /// 턴을 먹는 아이템(에너지2)은 쓰면 그 턴이 끝난다 — 원작 표기 그대로다.
        /// </summary>
        void UseItem(Unit u, ItemKind k)
        {
            var info = Items.Get(k);
            var r = _items.Use(u.Id, u.Team, k,
                (id, frac) => { var t = _units.Find(x => x.Id == id); if (t != null) t.Hp = Mathf.Min(t.HpMax, t.Hp + Mathf.RoundToInt(t.HpMax * frac)); },
                (team, frac) => { foreach (var t in _units) if (t.Team == team && t.Alive) t.Hp = Mathf.Min(t.HpMax, t.Hp + Mathf.RoundToInt(t.HpMax * frac)); },
                () => SetWeather(Weather.Snow),
                () => { _wind = -_wind; });
            if (r == ItemState.UseResult.NotHeld) return;
            {
                // 종류별 모양·색(ParticleFx.ItemBurst): 회복=초록 상승 · 실드=파란 껍질 · 파워/이동=주황/하늘 링 · 눈/텔레포트=소용돌이 · 더블파이어=노란 섬광
                int style = k == ItemKind.Shield ? 1 : (k == ItemKind.PowerUp || k == ItemKind.MoveUp) ? 2 : (k == ItemKind.SnowFall || k == ItemKind.TeleportBullet) ? 3 : k == ItemKind.DoubleFire ? 4 : 0;
                var col = k == ItemKind.Shield ? new Color(0.45f, 0.7f, 1f) : k == ItemKind.PowerUp ? new Color(1f, 0.55f, 0.2f) : k == ItemKind.MoveUp ? new Color(0.5f, 0.85f, 1f)
                        : k == ItemKind.SnowFall ? Color.white : k == ItemKind.TeleportBullet ? new Color(0.8f, 0.5f, 1f) : k == ItemKind.DoubleFire ? new Color(1f, 0.9f, 0.4f) : new Color(0.5f, 1f, 0.5f);
                if (k == ItemKind.TeamEnergy) { foreach (var t in _units) if (t.Team == u.Team && t.Alive) EnsureFx().ItemBurst(t.Pos, col, 0); }
                else EnsureFx().ItemBurst(u.Pos, col, style);
            }
            _log = $"[아이템] {info.Name} — {info.Desc}";
            // 로그로도 남긴다 — HUD 문자열만 쓰면 자동 검증에서 아이템이 도는지 확인할 방법이 없다.
            Debug.Log($"[Tankfall] 아이템 {(u.Team == 0 ? "아군" : "적군")}{u.Id % 3 + 1} {info.Name}");
            if (_itemSel > 0) _itemSel--;
            // 턴을 먹는 아이템은 사격 없이 턴을 넘긴다.
            if (r == ItemState.UseResult.AppliedEndsTurn && !info.AppliesToShot) NextTurn();
        }


        /// <summary>AI 아이템 정책 [추정] — 턴을 안 먹는 건 쓸 수 있으면 쓴다. 하네스(BattleSimVerify)와 같은 규칙.</summary>
        void AiUseItems(Unit u)
        {
            if (_itemSlots == 0) return;
            bool lowHp = u.Hp < u.HpMax / 2, critical = u.Hp < u.HpMax / 4;
            bool wantSnow = _weather != Weather.Snow;
            if (wantSnow)
            {
                bool hasPoseidon = false;
                foreach (var t in _units) if (t.Team == u.Team && t.Alive && t.Kind == TankKind.Poseidon) hasPoseidon = true;
                wantSnow = hasPoseidon;
            }
            // 방해탄(§2-9-14) AI 정책 [추정]: 각도·파워 고정은 늘 쓴다(상대가 AI 든 사람이든 조준을 묶는다).
            //   화면 방해 4종은 **사람이 상대일 때만** 쓴다 — AI 에게는 화면이 없어 효과가 0 인데 턴은 먹으므로
            //   AI 대 AI 에서 쓰면 순손해다. 이 게임은 플레이어가 0팀이므로 AI(1팀)만 쓸 수 있다.
            bool foeIsHuman = u.Team == 1 && !_autoShot;
            foreach (var k in new[] { ItemKind.Shield, ItemKind.MoveUp, ItemKind.PowerUp, ItemKind.SnowFall, ItemKind.TeamEnergy, ItemKind.AddEnergy1, ItemKind.DoubleFire,
                                      ItemKind.LockAngleShell, ItemKind.LockPowerShell,
                                      ItemKind.FlipShell, ItemKind.WobbleShell, ItemKind.FogShell, ItemKind.ConfuseShell })
            {
                if (!_items.Has(u.Id, k)) continue;
                if (k == ItemKind.AddEnergy1 && !lowHp) continue;
                if (k == ItemKind.TeamEnergy && !lowHp) continue;
                if (k == ItemKind.Shield && _items.HasShield(u.Id)) continue;
                if (k == ItemKind.SnowFall && !wantSnow) continue;
                if (Impair.IsScreenOnly(Items.ImpairOf(k)) && !foeIsHuman) continue;
                if (Items.ImpairOf(k) != ImpairKind.None && _items.ImpairShot(u.Id) != ImpairKind.None) continue;   // 한 사격에 하나만
                UseItem(u, k);
            }
            _ = critical;   // 에너지2 는 턴을 먹어서 AI 가 쓰면 사격을 못 한다 — 하네스와 같이 급할 때만 [추정], 지금은 보류
        }

        // ---------------- 연습장(§68) ----------------

        /// <summary>
        /// 연습장의 턴. 전투와 다른 점은 셋뿐이다: 표적이 **되살아나고**, 적이 **반격하지 않으며**,
        /// 턴이 항상 **플레이어에게 돌아온다**. 서든데스·승패 판정도 돌리지 않는다 —
        /// 연습장은 이기는 곳이 아니라 같은 조건을 반복하는 곳이다.
        /// </summary>
        void PracticeNextTurn()
        {
            var me = _units[0];   // 연습장은 아군 1번만 쏜다

            // 이번 발의 성적 — Impact() 가 착탄 순간에 확정해 둔 거리를 쓴다(지형이 깎이기 전 값).
            float miss = _practiceMissAtImpact;
            if (_practiceImpact.LengthSq > 0.001f)
            {
                _practiceShots++;
                _practiceLastMiss = miss;
                if (miss < _practiceBestMiss) _practiceBestMiss = miss;
                bool hit = miss <= _shooterStats.BlastRadius;
                if (hit) _practiceHits++;
                if (_practiceSolveVerified) { _practiceVerifiedShots++; if (hit) _practiceVerifiedHits++; }
                _log = hit ? $"명중! 표적까지 {miss:F1}m (폭발 반경 {_shooterStats.BlastRadius:F1}m)"
                           : $"빗나감 — 표적까지 {miss:F1}m";
                if (_practiceSelfTest)
                {
                    var t0 = _units.Find(o => o.Team == 1);
                    Debug.Log($"[Tankfall] 연습장 채점 실제오차 {miss:F2}m 착탄({_practiceImpact.X:F1},{_practiceImpact.Y:F1},{_practiceImpact.Z:F1})" +
                              (t0 != null ? $" 표적0중심({t0.Center.X:F1},{t0.Center.Y:F1},{t0.Center.Z:F1})" : ""));
                }
            }
            _practiceImpact = default; _practiceMissAtImpact = float.MaxValue;

            // 표적 부활 — **매번 자리를 옮긴다**.
            // ⚠️ 처음엔 제자리에서 되살렸는데, 같은 곳을 계속 쏘니 크레이터가 깊어져 표적이 구덩이에 잠겼고
            //    캐롯(0~40°)의 각도로는 닿지 않는 자리가 됐다 — 자체검사 명중률이 발을 거듭할수록 떨어져서 들켰다.
            //    연습장은 같은 구덩이를 파는 곳이 아니라 **매번 다른 거리·방향을 읽는 곳**이다(§1 포격 감각).
            //    지형 파괴 자체는 남겨둔다 — 전투와 같은 전장에서 연습해야 감각이 옮겨간다.
            foreach (var o in _units)
            {
                if (o.Team == 0) continue;
                o.Hp = o.HpMax;
                o.Root.gameObject.SetActive(true);
                PlaceTargetRandomly(o);
            }

            _niceFlash = null;
            RollWind();          // 매 발 바람이 바뀐다 — 바람 읽기가 연습의 핵심이다
            _practiceAnswer = "";
            _turn = 0;           // 턴은 항상 나에게 돌아온다(Current = _units[_turn])
            me.Gauge = MoveGaugeMax;
            _power = 0f; _charging = false;
            _phase = Phase.Move;
            _phaseTimer = MovePhaseSec;
            _log = $"연습 {_practiceShots}발 · 명중 {_practiceHits} — MOVE(스페이스로 조준)";
        }

        /// <summary>연습 표적을 적 진영 쪽 임의 위치에 지면 위로 세운다.</summary>
        void PlaceTargetRandomly(Unit o)
        {
            for (int tries = 0; tries < 12; tries++)
            {
                float x = Random.Range(MapSize * 0.55f, MapSize * 0.92f);
                float z = Random.Range(MapSize * 0.12f, MapSize * 0.88f);
                float g = TankGroundProbe.GroundBelow(_vol, x, z, 60f);
                if (float.IsNegativeInfinity(g)) continue;
                o.Root.position = new Vector3(x, g + GroundVisualLift, z);
                return;
            }
        }

        /// <summary>정답대로 자동 발사해 채점까지 돌려본다(`-practiceselftest`). 20발 쏘고 결과를 찍고 끝낸다.</summary>
        void PracticeSelfTestFire()
        {
            if (_practiceSelfTestShots == 3) Shot("연습장");   // 오너에게 보여줄 화면 한 장
            if (_practiceSelfTestShots >= 20)
            {
                float acc = _practiceShots > 0 ? _practiceHits * 100f / _practiceShots : 0f;
                float vacc = _practiceVerifiedShots > 0 ? _practiceVerifiedHits * 100f / _practiceVerifiedShots : -1f;
                Debug.Log($"[Tankfall] 연습장 자체검사 — 전체 {_practiceHits}/{_practiceShots}발 ({acc:F0}%), " +
                          $"검증통과 해만 {_practiceVerifiedHits}/{_practiceVerifiedShots}발 ({vacc:F0}%), 최고 오차 {_practiceBestMiss:F2}m");
                // ⚠️ 게이트는 **전체 명중률이 아니라 "검증통과라고 말한 해"의 명중률**이다.
                //    지형·각도 범위 때문에 애초에 해가 없는 배치도 나오는데(폴백), 그걸 못 맞혔다고 역산이
                //    틀린 건 아니다. 거짓말을 안 하려면 주장한 것만 재야 한다 — "된다고 한 건 된다".
                if (_practiceVerifiedShots >= 5 && vacc >= 90f)
                    Debug.Log("[Tankfall] ✅ 정답 보기가 '된다'고 한 해는 실제로 맞는다(§5-7 역산 + 채점 정상)");
                else if (_practiceVerifiedShots < 5)
                    Debug.Log("[Tankfall] ❌ 검증통과 해 표본이 너무 적다 — 판단 불가");
                else
                    Debug.Log("[Tankfall] ❌ 검증통과라고 한 해가 실제로는 안 맞는다 — 역산이나 채점이 고장났다");
                Application.Quit();
                return;
            }
            _practiceSelfTestShots++;
            var me = _units[0];
            if (!TrySolvePractice(out float pitch, out float yaw, out float power))
            {
                Debug.Log("[Tankfall] 연습장 자체검사 — 해 없음, 이 발은 건너뛴다");
                PracticeNextTurn();
                return;
            }
            // ⚠️ `FireFrom` 의 turretYaw 는 **차체 방향 기준 상대각**이다(worldYaw = Heading + turretYaw).
            //    역산이 주는 건 월드 야우라 그대로 넘기면 차체 방향이 한 번 더 더해져 엉뚱한 데로 날아간다
            //    (처음에 이걸 몰라 자체검사가 20발 전부 지형 밖으로 나갔다).
            float rel = Mathf.DeltaAngle(me.Heading, yaw);
            me.TurretYaw = rel; me.BarrelPitch = pitch;
            Debug.Log($"[Tankfall] 연습장 시도{_practiceSelfTestShots}: {_practiceSolveNote} 바람{_wind.magnitude:F1}");
            FireFrom(me, rel, pitch, power);
        }

        /// <summary>
        /// 연습장 "정답 보기"(§5-7). 지금 바람·지형에서 가장 가까운 표적을 맞히는 각도·파워를 계산해 보여준다.
        /// ⚠️ 기획서 §58 에 따라 **PvP 에서는 절대 노출하면 안 된다** — 호출부가 `_practice` 로 잠겨 있다.
        /// </summary>
        void ShowPracticeAnswer()
        {
            if (TrySolvePractice(out float pitch, out float yaw, out float power))
                _practiceAnswer = $"정답: 각 {pitch:F1}° · 파워 {power * 100f:F0} · 야우 {yaw:F1}°";
            else
                _practiceAnswer = "해 없음 — 이 각도 범위·사거리로는 못 닿는다";
            _log = _practiceAnswer;
        }

        /// <summary>
        /// 지금 바람·지형에서 가장 가까운 표적을 맞히는 각도·파워를 §5-7 역산으로 구한다.
        /// **정답 보기와 자체검사가 같은 이 함수를 쓴다** — 둘이 다른 계산을 하면 검사가 검사가 아니다.
        /// 파워를 훑어 가장 낮은 파워로 풀리는 해를 고른다. 해가 없으면 사거리·각도 범위 밖이라는 뜻이다.
        /// </summary>
        bool TrySolvePractice(out float pitchDeg, out float yawDeg, out float power01)
        {
            pitchDeg = 0f; yawDeg = 0f; power01 = 0f;
            _practiceSolveVerified = false;
            var me = _units[0];
            Unit target = null; float best = float.MaxValue;
            foreach (var o in _units)
            {
                if (o.Team == 0 || !o.Alive) continue;
                var d = o.Center - me.Center;
                if (d.LengthSq < best) { best = d.LengthSq; target = o; }
            }
            if (target == null) return false;

            var st = TankStats.For(me.Kind, ShellKind.Normal, me.HpFrac, me.W);
            var accel = st.AccelWith(_wind.x, _wind.y);
            var from = new Vec3(me.Fire.position.x, me.Fire.position.y, me.Fire.position.z);
            for (int i = 1; i <= 20; i++)
            {
                float power = i / 20f;
                float speed = st.SpeedAt(power);   // FireFrom 과 **같은 함수** — 따로 계산하면 정답이 정답이 아니게 된다
                if (!Ballistics.SolveLaunchAngles(from, target.Center, speed, accel, st.Flight, out var low, out var high)) continue;
                // §5-7 5단계: 두 해를 **실제로 날려보고 지형에 막히지 않는 쪽**을 택한다.
                //   ⚠️ 이 단계를 빼면 저각 해가 언덕에 박히는데도 "정답"이라고 내놓는다
                //      (실측: 자체검사 명중률 75% — 빠진 5발이 전부 막힌 저각 해였다).
                //   둘 다 막히면 고각 해를 준다(산 넘기기).
                var boxes = new List<TankHitbox>();
                foreach (var o in _units) if (o.Alive) boxes.Add(new TankHitbox { Id = o.Id, Center = o.Center, Radius = TankRadius });
                bool haveFallback = false; float fbPitch = 0f, fbYaw = 0f, fbPower = 0f;
                foreach (var cand in new[] { low, high })
                {
                    if (cand.PitchDeg < st.MinPitch || cand.PitchDeg > st.MaxPitch) continue;
                    if (!haveFallback) { haveFallback = true; fbPitch = cand.PitchDeg; fbYaw = cand.YawDeg; fbPower = power; }
                    var sim = ProjectileSimulator.Simulate(_vol, from,
                        Ballistics.VelocityFrom(cand.YawDeg, cand.PitchDeg, speed), accel, boxes, me.Id, MapSize, st.Flight, null);
                    var miss = sim.Impact - target.Center;
                    // 허용 오차는 **폭발 반경이 아니라 탱크 반경**이다. 폭발 반경(캐롯 7m)까지 열어두면
                    // 5m 씩 빗나간 해도 "정답"이라고 내놓는다(실측으로 걸렸다). 연습장의 정답은 표적을
                    // 직접 맞히는 해여야 한다 — 시뮬 시간 간격 때문에 착탄점이 탱크 표면에 찍히므로 반경만큼은 연다.
                    if (!sim.Hit || miss.LengthSq > TankRadius * TankRadius) continue;   // 막혔거나 빗나갔다
                    pitchDeg = cand.PitchDeg; yawDeg = cand.YawDeg; power01 = power;
                    _practiceSolveNote = $"해 검증통과 각{pitchDeg:F1} 파워{power * 100:F0} 예측오차{Mathf.Sqrt(miss.LengthSq):F2}m 표적({target.Center.X:F0},{target.Center.Z:F0}) 포구({from.X:F1},{from.Y:F1},{from.Z:F1})";
                    _practiceSolveVerified = true;
                    return true;
                }
                if (haveFallback && i == 20)
                {
                    pitchDeg = fbPitch; yawDeg = fbYaw; power01 = fbPower;
                    _practiceSolveNote = $"해 폴백(전부 막힘) 각{pitchDeg:F1} 파워{fbPower * 100:F0}";
                    _practiceSolveVerified = false;
                    return true;
                }
            }
            return false;
        }

        // ---------------- 카메라 · HUD ----------------

        /// <summary>지금 조작 중인 내 탱크에 이 방해가 걸려 있는가. AI 탱크 것은 내 화면을 건드리지 않는다.</summary>
        bool ImpairedMine(ImpairKind k)
            => !_practice && Current != null && Current.Team == 0 && _impair.Has(Current.Id, k);

        Vector3 _confuseFocus;   // 바보탄일 때 카메라가 머무는 엉뚱한 자리

        /// <summary>
        /// 안개탄(§2-9-14) — 원작: "자신과 팀원을 제외한 탱들의 모습은 볼 수 없고 단지 포탄이 날아오는 것만 볼 수 있게 된다".
        /// 그래서 적 탱크의 렌더러만 끈다 — 포탄(_shell)은 그대로 보인다.
        /// ⚠️ 게임오브젝트를 끄면 안 된다. 죽은 탱크를 숨기는 코드가 같은 스위치를 쓰고 있어서 서로 덮어쓴다.
        /// </summary>
        void RefreshFogVisibility()
        {
            bool fog = ImpairedMine(ImpairKind.Fog);
            foreach (var u in _units)
            {
                if (u.Root == null) continue;
                bool hide = fog && u.Team != 0;
                foreach (var mr in u.Root.GetComponentsInChildren<MeshRenderer>(true)) mr.enabled = !hide;
            }
        }

        /// <summary>
        /// 기후 연출(§2-9-15) — 증폭벽은 반투명 판, 회오리는 도는 원기둥.
        /// **보이지 않으면 플레이어가 피할 수도 노릴 수도 없다** — 규칙만 있고 화면에 없으면 죽은 값이다.
        /// </summary>
        void RefreshAir()
        {
            int used = 0;
            Transform Take(PrimitiveType t, Color c)
            {
                if (used >= _airGos.Count)
                {
                    var go = GameObject.CreatePrimitive(t);
                    go.name = $"Air{_airGos.Count}";
                    Destroy(go.GetComponent<Collider>());
                    var mr = go.GetComponent<MeshRenderer>();
                    mr.sharedMaterial = MakeMat(c, 0.6f);
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    _airGos.Add(go.transform);
                }
                var tr = _airGos[used++];
                tr.gameObject.SetActive(true);
                return tr;
            }
            foreach (var w in _air.Walls)
            {
                var tr = Take(PrimitiveType.Cube, new Color(1.0f, 0.85f, 0.35f));
                tr.position = new Vector3(w.X, (w.MinY + w.MaxY) * 0.5f, w.Z);
                tr.localScale = new Vector3(AirField.WallThickness, w.MaxY - w.MinY, w.HalfLen * 2f);
                tr.rotation = Quaternion.identity;
            }
            foreach (var t in _air.Tornadoes)
            {
                var tr = Take(PrimitiveType.Cylinder, new Color(0.65f, 0.78f, 0.95f));
                tr.position = new Vector3(t.X, t.TopY * 0.5f, t.Z);
                tr.localScale = new Vector3(t.Radius * 2f, t.TopY * 0.5f, t.Radius * 2f);   // 기본 실린더 높이 2
            }
            for (int i = used; i < _airGos.Count; i++) _airGos[i].gameObject.SetActive(false);
        }

        /// <summary>회오리를 돌린다 — 서 있기만 하면 기둥인지 회오리인지 안 읽힌다.</summary>
        void TickAir(float dt)
        {
            if (_air.Tornadoes.Count == 0) return;
            _tornadoSpin += dt * 220f;
            int wallN = _air.Walls.Count;
            for (int i = 0; i < _air.Tornadoes.Count && wallN + i < _airGos.Count; i++)
                _airGos[wallN + i].rotation = Quaternion.Euler(0f, _tornadoSpin, 0f);
        }

        /// <summary>
        /// Boom 모드(§2-9-16) 턴 시작 — 지진·유성. 원작 서술은 Sim/BoomMode.cs 머리말에 있다.
        /// ⚠️ 유성은 **착탄 처리와 같은 경로**(굴착 → 천장 붕괴 → 메시 갱신 → 피해)를 쓴다.
        ///    따로 만들면 지형만 파이고 피해가 안 들어가는 반쪽이 되기 쉽다.
        /// </summary>
        void BoomTurnStart()
        {
            // ── 지진 ── "각이 랜덤하게 변하고 탱의 위치가 미세하게 이동된다"
            if (BoomMode.RollQuake(ref _boomRng))
            {
                int moved = 0;
                foreach (var o in _units)
                {
                    if (!o.Alive) continue;
                    BoomMode.QuakeShiftFor(ref _boomRng, out float dx, out float dz, out float dp);
                    var pq = o.Pos;
                    float nx = Mathf.Clamp(pq.x + dx, 5f, MapSize - 5f), nz = Mathf.Clamp(pq.z + dz, 5f, MapSize - 5f);
                    float g = TankGroundProbe.GroundBelow(_vol, nx, nz, pq.y + 3f);
                    if (float.IsNegativeInfinity(g)) continue;
                    o.Root.position = new Vector3(nx, g + GroundVisualLift, nz);
                    o.BarrelPitch = Mathf.Clamp(o.BarrelPitch + dp, o.St.MinPitch, o.St.MaxPitch);
                    ApplyAim(o);
                    moved++;
                }
                if (moved > 0) { _log = $"⛰ 지진! 탱크 {moved}대의 위치와 각도가 흔들렸다"; _boomQuakes++; }
            }

            // ── 유성 ── "플레이어 중 한 명의 탱이 있는 곳과 그 근처에 메테오가 떨어진다"
            if (!BoomMode.RollMeteor(ref _boomRng)) return;
            var alive = _units.FindAll(x => x.Alive);
            if (alive.Count == 0) return;
            var target = alive[Mathf.Min((int)(_boomRng.Float01() * alive.Count), alive.Count - 1)];
            var tp = target.Pos;
            BoomMode.MeteorSpots(ref _boomRng, tp.x, tp.z, _boomSpots);
            int hitN = 0;
            foreach (var (mx, mz) in _boomSpots)
            {
                float g = TankGroundProbe.GroundBelow(_vol, mx, mz, 90f);
                if (float.IsNegativeInfinity(g)) continue;
                var blast = SdfDeformer.SubtractSphere(_vol, new BlastRequest(mx, g, mz, BoomMode.MeteorCrater));
                var collapse = CeilingCollapse.Apply(_vol, blast);
                _terrain.ApplyDirty(CeilingCollapse.Union(blast, collapse));
                foreach (var o in _units)
                {
                    if (!o.Alive) continue;
                    float dist = (o.Center - new Vec3(mx, g, mz)).Length;
                    if (dist > BoomMode.MeteorBlast) continue;
                    int dmg = Damage.AfterDefense(
                        Damage.Compute(dist, BoomMode.MeteorBlast, BoomMode.MeteorDamage, BoomMode.MeteorDamage, false),
                        o.St.Defense);
                    if (dmg <= 0) continue;
                    o.Hp = Mathf.Max(0, o.Hp - dmg);
                    hitN++;
                    if (!o.Alive) o.Root.gameObject.SetActive(false);
                }
            }
            foreach (var o in _units) if (o.Alive) GroundUnit(o, 0f);
            _boomMeteors++;
            _log = $"☄ 유성! {_boomSpots.Count}발이 떨어졌다" + (hitN > 0 ? $" — {hitN}대 피격" : "");
        }

        int _boomQuakes, _boomMeteors;   // Boom 모드 네거티브 컨트롤 — 0 이면 안 도는 것

        /// <summary>
        /// `-boomselftest` — Boom 모드(§2-9-16) 세 가지가 실제로 도는지 확인한다.
        /// 확률(지진 12%·유성 10%)이라 한 번 불러선 안 나온다 — **나올 때까지 돌려서 효과를 잰다**.
        /// 끝내 안 나오면 그것도 실패다(확률이 0 이면 죽은 시스템이다).
        /// </summary>
        void BoomSelfTestStep()
        {
            int fail = 0;

            // ① 지뢰밭 — Start 에서 이미 깔렸어야 한다.
            _hazards.Snapshot(_hazardBuf);
            int mines = 0;
            foreach (var h in _hazardBuf) if (h.Kind == 0) mines++;
            if (mines < BoomMode.MineFieldCount / 2)
            { Debug.Log($"[Tankfall] ❌ 지뢰밭이 {mines}발뿐이다(기대 {BoomMode.MineFieldCount})"); fail++; }
            else Debug.Log($"[Tankfall] Boom 자체검사 지뢰밭 — {mines}발 매설 확인");

            // ② 지진 — 위치·각도가 실제로 바뀌는가
            bool quakeSeen = false;
            for (int t = 0; t < 400 && !quakeSeen; t++)
            {
                var before = new List<Vector3>();
                var pitchBefore = new List<float>();
                foreach (var o in _units) { before.Add(o.Pos); pitchBefore.Add(o.BarrelPitch); }
                int q0 = _boomQuakes, m0 = _boomMeteors;
                BoomTurnStart();
                if (_boomQuakes == q0) continue;
                quakeSeen = true;
                float maxMove = 0f, maxPitch = 0f;
                for (int i = 0; i < _units.Count; i++)
                {
                    maxMove = Mathf.Max(maxMove, (before[i] - _units[i].Pos).magnitude);
                    maxPitch = Mathf.Max(maxPitch, Mathf.Abs(pitchBefore[i] - _units[i].BarrelPitch));
                }
                if (maxMove < 0.05f) { Debug.Log("[Tankfall] ❌ 지진인데 탱크가 하나도 안 움직였다"); fail++; }
                else if (maxPitch < 0.05f) { Debug.Log("[Tankfall] ❌ 지진인데 포각이 하나도 안 바뀌었다"); fail++; }
                else Debug.Log($"[Tankfall] Boom 자체검사 지진 — 최대 이동 {maxMove:F2}m, 각 변화 {maxPitch:F1}°");
                _ = m0;
            }
            if (!quakeSeen) { Debug.Log("[Tankfall] ❌ 400턴 돌려도 지진이 안 났다"); fail++; }

            // ③ 유성 — 지형이 파이고 피해가 들어가는가
            bool meteorSeen = false;
            for (int t = 0; t < 600 && !meteorSeen; t++)
            {
                int hpBefore = 0, tri = _terrain.TriangleCount;
                foreach (var o in _units) hpBefore += o.Hp;
                int m0 = _boomMeteors;
                BoomTurnStart();
                if (_boomMeteors == m0) continue;
                meteorSeen = true;
                int hpAfter = 0;
                foreach (var o in _units) hpAfter += o.Hp;
                if (_terrain.TriangleCount == tri && hpAfter == hpBefore)
                { Debug.Log("[Tankfall] ❌ 유성이 떨어졌는데 지형도 체력도 그대로다"); fail++; }
                else Debug.Log($"[Tankfall] Boom 자체검사 유성 — 팀 전체 체력 {hpBefore} → {hpAfter}, 삼각형 {tri} → {_terrain.TriangleCount}");
            }
            if (!meteorSeen) { Debug.Log("[Tankfall] ❌ 600턴 돌려도 유성이 안 떨어졌다"); fail++; }

            if (fail == 0) Debug.Log("[Tankfall] ✅ Boom 모드(§2-9-16) — 지뢰밭·지진·유성 전부 동작한다");
            else Debug.Log($"[Tankfall] ❌ Boom 자체검사 실패 {fail}건");
            Application.Quit(fail == 0 ? 0 : 1);
        }

        /// <summary>
        /// 탱크 고르기(§2-9-17). 원작 13종을 다 만들어 놓고도 **게임에서는 캐논·캐롯·레이저 셋만**
        /// 나오고 있었다 — 나머지 열은 매치업 하네스에서만 돌던 셈이다(선언만 있고 화면에 없으면 죽은 값이다).
        ///
        /// ⚠️ 슈퍼탱크: 코드 주석에 "랜덤에서만 낮은 확률로 등장"이라 적혀 있었지만 **조사로 확인되지 않았다**
        ///    (namu.wiki 포트리스2 / 포트리스2·등장탱크 둘 다 슈퍼탱크 서술이 없다, 2026-09-17 조회).
        ///    그래서 "고를 수는 없고 랜덤에서만 낮은 확률로 나온다"는 취급과 확률 6% 는 전부 [추정]이다.
        ///    확실한 건 하나 — **그냥 두면 한 판도 안 나오는 죽은 탱크**라 랜덤 경로라도 열어둔다.
        /// </summary>
        void DrawPicker()
        {
            var kinds = TankStats.Selectable();
            float w = 720f, h = 430f;
            var r = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            GUI.Box(r, "");
            GUILayout.BeginArea(new Rect(r.x + 16, r.y + 12, r.width - 32, r.height - 24));
            var title = new GUIStyle(GUI.skin.label) { fontSize = 18, richText = true };
            var st = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };
            GUILayout.Label($"<b>탱크 고르기</b>   —   {MapHeightFunction.Name(_map)} · {WeatherName(_weather)} · AI {Difficulties[_difficulty].Name}", title);
            GUILayout.Label($"<size=12>세 대를 고르세요 ({_pick.Count}/3). 같은 기종을 겹쳐 골라도 됩니다.</size>", st);
            GUILayout.Space(6);

            int col = 0;
            GUILayout.BeginHorizontal();
            foreach (var k in kinds)
            {
                var t = TankStats.Get(k);
                var sp2 = TankStats.For(k, ShellKind.Special, 1f, Weather.Clear);
                if (GUILayout.Button($"{t.Name}  |  {TankStats.EraName(t.Era)} · 체 {t.Hp} · 2번탄 {sp2.Name}", GUILayout.Width(160), GUILayout.Height(46)))
                    if (_pick.Count < 3) _pick.Add(k);
                if (++col % 4 == 0) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(8);

            string chosen = _pick.Count == 0 ? "<color=#999>아직 없음</color>" : "";
            foreach (var k in _pick) chosen += TankStats.Get(k).Name + "  ";
            GUILayout.Label($"고른 탱크: <b>{chosen}</b>", st);
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("되돌리기", GUILayout.Width(110), GUILayout.Height(34)) && _pick.Count > 0)
                _pick.RemoveAt(_pick.Count - 1);
            if (GUILayout.Button("랜덤 3종", GUILayout.Width(110), GUILayout.Height(34)))
            {
                _pick.Clear();
                for (int i = 0; i < 3; i++)
                    _pick.Add(Random.value < SuperTankChance
                        ? TankKind.SuperTank                       // 고를 수는 없고 랜덤에서만 [추정]
                        : kinds[Random.Range(0, kinds.Length)]);
            }
            GUI.enabled = _pick.Count == 3;
            if (GUILayout.Button("<b>시작</b>", GUILayout.Width(140), GUILayout.Height(34))) ConfirmPick();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        /// <summary>고른 대로 판을 다시 세운다. 지형·날씨는 그대로 두고 탱크만 새로 만든다.</summary>
        void ConfirmPick()
        {
            _roster = _pick.ToArray();
            foreach (var u in _units) if (u.Root != null) Destroy(u.Root.gameObject);
            _units.Clear();
            SpawnTeams();
            _items.Clear();
            if (_itemSlots > 0)
            {
                var roll = new List<ItemKind>();
                foreach (var u in _units) { Items.Roll(ref _itemRng, _itemSlots, roll); _items.Bag(u.Id).AddRange(roll); }
            }
            _turn = 0;
            _picking = false;
            _phase = Phase.Move;
            _phaseTimer = MovePhaseSec;
            _log = $"전투 개시 — {TankStats.Get(_roster[0]).Name}·{TankStats.Get(_roster[1]).Name}·{TankStats.Get(_roster[2]).Name}  (파랑 vs 빨강)";
            Debug.Log($"[Tankfall] 로스터 확정 — {_roster[0]}·{_roster[1]}·{_roster[2]}");
        }

        bool _rosterSelfTest;

        /// <summary>
        /// `-rosterselftest` — **13종이 전부 실제로 판에 설 수 있는지** 확인한다(§2-9-17).
        /// 수치표에 줄이 있는 것과 게임에 세울 수 있는 것은 다르다 — 실제로 스폰해서
        /// 기종·체력·외형(메시)·2번탄·궁극기가 다 붙는지 본다. 하나라도 어긋나면 rc=1.
        /// </summary>
        void RosterSelfTestStep()
        {
            int fail = 0, ok = 0;
            foreach (TankKind k in System.Enum.GetValues(typeof(TankKind)))
            {
                _pick.Clear();
                _pick.Add(k); _pick.Add(k); _pick.Add(k);
                ConfirmPick();
                if (_units.Count != 6) { Debug.Log($"[Tankfall] ❌ {k}: 유닛이 {_units.Count}대(6대여야 한다)"); fail++; continue; }
                var u = _units[0];
                if (u.Kind != k) { Debug.Log($"[Tankfall] ❌ {k}: 스폰된 기종이 {u.Kind}"); fail++; continue; }
                if (u.HpMax != TankStats.Get(k).Hp) { Debug.Log($"[Tankfall] ❌ {k}: 체력 {u.HpMax} (표 {TankStats.Get(k).Hp})"); fail++; continue; }
                int meshes = u.Root == null ? 0 : u.Root.GetComponentsInChildren<MeshFilter>(true).Length;
                if (meshes < 3) { Debug.Log($"[Tankfall] ❌ {k}: 외형 메시가 {meshes}개뿐이다"); fail++; continue; }
                var sp = TankStats.For(k, ShellKind.Special, 1f, Weather.Clear);
                if (string.IsNullOrEmpty(sp.Name)) { Debug.Log($"[Tankfall] ❌ {k}: 2번탄 이름이 비었다"); fail++; continue; }
                int shots = Spread.Pattern(k, ShellKind.Special).Count;
                // 궁극기(§49) = 2번탄을 핵급으로 키우는 것. "있다" 는 **수치가 실제로 커진다**로만 확인된다.
                var ultSt = NiceShot.ApplyUltimate(sp, k);
                float ultMul = sp.BaseDamage > 0f ? ultSt.BaseDamage / sp.BaseDamage : 0f;
                if (ultMul <= 1.01f && ultSt.BlastRadius <= sp.BlastRadius && ultSt.CraterRadius <= sp.CraterRadius)
                { Debug.Log($"[Tankfall] ❌ {k}: 궁극기가 2번탄을 안 키운다"); fail++; continue; }
                Debug.Log($"[Tankfall] 로스터 자체검사 {TankStats.Get(k).Name,-10} 체 {u.HpMax,5} · 메시 {meshes,2} · 2번탄 {sp.Name}×{shots} · 궁 피해 ×{ultMul:F1} 폭발 {ultSt.BlastRadius:F1}m");
                ok++;
            }
            if (fail == 0 && ok == 13) Debug.Log("[Tankfall] ✅ 원작 13종 전부 게임에 세울 수 있다(수치·외형·2번탄·궁극기)");
            else Debug.Log($"[Tankfall] ❌ 로스터 자체검사 실패 {fail}건 (확인 {ok}/13)");
            Application.Quit(fail == 0 && ok == 13 ? 0 : 1);
        }

        void UpdateCamera(float dt)
        {
            if (Input.GetMouseButton(1))
            {
                _camYaw += Input.GetAxisRaw("Mouse X") * 3.2f;
                _camPitch = Mathf.Clamp(_camPitch - Input.GetAxisRaw("Mouse Y") * 2.4f, -5f, 78f);
            }
            _camDist = Mathf.Clamp(_camDist - Input.mouseScrollDelta.y * 2.5f, 8f, 120f);

            // §18 3단 연출: 발사 → 포탄 추적 → **착탄점 체류**.
            // 착탄 직후 곧바로 다음 탱크로 돌아가면 자기가 무슨 짓을 했는지 못 본다.
            // 방해탄(§2-9-14) — **내 탱크에 걸린 것만** 내 화면을 방해한다(원작: 맞은 사람의 화면).
            bool flip = ImpairedMine(ImpairKind.FlipScreen);
            bool wobble = ImpairedMine(ImpairKind.Wobble);
            bool confuse = ImpairedMine(ImpairKind.Confuse);
            _impairClock += dt;

            Vector3 focus =
                _phase == Phase.Flying && _shell != null && _shell.gameObject.activeSelf
                    ? _shell.position
                    : _phase == Phase.Resolve && _hasImpactFocus
                        ? _impactFocus
                        : Current.Pos + Vector3.up * 2.2f;
            // 바보탄: 원작 "자신의 턴이 와도 화면이 자신의 위치로 오지 않는다" — 카메라를 안 데려온다.
            if (confuse && _phase != Phase.Flying && _phase != Phase.Resolve) focus = _confuseFocus;
            var rot = Quaternion.Euler(_camPitch, _camYaw, 0);
            var want = focus + rot * Vector3.back * _camDist;
            _cam.transform.position = dt > 0f ? Vector3.Lerp(_cam.transform.position, want, 1f - Mathf.Exp(-9f * dt)) : want;
            _cam.transform.LookAt(focus);
            // 멀미탄: 원작 "화면이 물결치듯이 울렁거린다" — 카메라를 흔든다.
            if (wobble)
                _cam.transform.rotation *= Quaternion.Euler(Mathf.Sin(_impairClock * 2.3f) * 3.5f,
                                                            Mathf.Sin(_impairClock * 1.7f) * 3.5f, 0f);
            // 반전탄: 원작 "화면이 상하로 뒤집힌다".
            if (flip) _cam.transform.rotation *= Quaternion.Euler(0f, 0f, 180f);

            // 폭발 흔들림. ⚠️ LookAt **뒤에** 더해야 한다 — 앞에 두면 LookAt 이 흔들림을 지운다.
            if (_fx != null && _fx.Shake > 0.001f)
            {
                float a = _fx.Shake;
                _cam.transform.position += new Vector3(
                    (Mathf.PerlinNoise(Time.time * 34f, 0.3f) - 0.5f),
                    (Mathf.PerlinNoise(0.7f, Time.time * 34f) - 0.5f),
                    (Mathf.PerlinNoise(Time.time * 29f, 5.1f) - 0.5f)) * a * 1.6f;
            }

            if (_skyDome != null) _skyDome.position = _cam.transform.position;
        }

        // ================= HUD (§57) =================
        //
        // 전부 도형이다 — 숫자와 ASCII 막대(`[███···┃]`)로 그리던 걸 §8 의 PowerBarUI·TeamStatusUI·
        // MoveGaugeUI·TurnTimerUI·WindIndicatorUI 로 나눠 실제 바·패널로 그린다.
        //
        // 왜 바꿨나: 포격은 **파워 게이지를 눈으로 멈추는 게임**이다(§11, 나이스샷 §2-9-2).
        // 20칸 문자 막대는 한 칸이 파워 5 라 표시점과의 차이를 눈으로 못 가른다 — 조작이 아니라 운이 된다.
        // 픽셀 바는 같은 폭에서 20배 넘는 해상도를 준다.
        //
        // ⚠️ 색은 전부 `Ui` 팔레트에서 온다. 여기서 새 색을 적지 마라(Ui.cs 머리말).
        // ⚠️ 이모지 금지 — 폰트에 글리프가 없어 □ 로 찍힌다(§2-9-11).

        void OnGUI()
        {
            if (_shellGallery) { DrawGalleryLabels(); return; }   // 갤러리는 전용 라벨만 그린다
            // 안개탄(§2-9-14) — 원작 "안개가 껴서 고립된다". 화면을 하얗게 덮는다(기후 안개 서술과 같은 그림).
            if (ImpairedMine(ImpairKind.Fog))
            {
                if (_fogTex == null)
                {
                    _fogTex = new Texture2D(1, 1);
                    _fogTex.SetPixel(0, 0, new Color(0.93f, 0.95f, 0.97f, 0.86f));
                    _fogTex.Apply();
                }
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _fogTex);
            }
            if (_picking) { DrawPicker(); return; }
            if (_hudOff) return;
            float W = Screen.width, H = Screen.height;
            if (ScreenGUI()) return;                 // 타이틀·탱크 선택·설정은 전투 HUD 를 덮는다
            // 결과도 덮는다(전투 HUD 가 표 뒤로 비치면 숫자가 안 읽힌다). 일시정지는 상황이 보여야 하니 겹쳐 그린다.
            if (_screen == GameScreen.Result) { DrawResult(W, H); return; }

            var u = Current;
            if (u == null) { Ui.Text(new Rect(20, 20, 600, 30), "초기화 실패 — 로그 확인", 16, Ui.Bad); return; }

            HudTopBar(W);
            HudTeamPanel(u);
            HudCurrent(u, H);
            HudPhaseTimer(W);
            if (IsPlayerTurn && _phase == Phase.Fire)
            {
                HudWeapons(u, W, H);
                HudPowerBar(u, W, H);
            }
            HudRange(u, W, H);
            HudItems(u, W, H);
            HudLog(W, H);
            if (!_practice && !_gallery) { DrawTurnOrder(W); DrawMiniMap(W, H); }   // 갤러리 13대는 TurnOrder 미등록 — 그리면 매 프레임 예외
            DrawPopups();

            if (_screen == GameScreen.Pause) DrawPause(W, H);
            else if (_phase == Phase.GameOver) HudGameOver(W, H);
        }

        /// <summary>상단 중앙 — 라운드·날씨·바람(도형 화살표)·난이도·보급·fps. 연습장이면 연습 지표로 바뀐다.</summary>
        void HudTopBar(float W)
        {
            var r = new Rect(W * 0.5f - 300f, 8f, 600f, 46f);
            Ui.Box(r);

            if (_practice)
            {
                float acc = _practiceShots > 0 ? _practiceHits * 100f / _practiceShots : 0f;
                string bestTxt = _practiceBestMiss < float.MaxValue ? $"{_practiceBestMiss:F1}m" : "-";
                string lastTxt = _practiceLastMiss >= 0f ? $"{_practiceLastMiss:F1}m" : "-";
                Ui.Text(new Rect(r.x + 12f, r.y + 4f, 260f, 20f), "TANKFALL 연습장", 15, Ui.Ink, TextAnchor.MiddleLeft, true);
                Ui.Text(new Rect(r.x + 12f, r.y + 23f, 380f, 18f),
                        $"{_practiceHits}/{_practiceShots}발 ({acc:F0}%)   이번 {lastTxt} / 최고 {bestTxt}   F2=정답", 12, Ui.Dim);
                // 명중률 바 — 숫자보다 추세가 먼저 읽힌다
                var ab = new Rect(r.xMax - 180f, r.y + 26f, 130f, 8f);
                Ui.Bar(ab, acc / 100f, Ui.Good, null, Ui.Border);
                HudWind(new Vector2(r.xMax - 30f, r.y + 23f));
                if (!string.IsNullOrEmpty(_practiceAnswer))
                    Ui.TextShadow(new Rect(r.x, r.yMax + 4f, r.width, 20f), _practiceAnswer, 14, Ui.Mark, TextAnchor.MiddleCenter, true);
                return;
            }

            Ui.Text(new Rect(r.x + 12f, r.y + 4f, 100f, 20f), "TANKFALL", 15, Ui.Ink, TextAnchor.MiddleLeft, true);
            Ui.Text(new Rect(r.x + 116f, r.y + 5f, 120f, 18f), $"라운드 {_round}", 13, Ui.Dim);
            Ui.Text(new Rect(r.x + 12f, r.y + 24f, 300f, 18f),
                    $"{WeatherName(_weather)}   AI {Difficulties[_difficulty].Name} <size=10>(F1)</size>" +
                    (_supply.Count > 0 ? $"   보급 {_supply.Count}" : ""), 12, Ui.Dim);

            // 바람(§5-3) — 방향은 화살표, 세기는 바. 숫자만 주면 "왼쪽 3" 을 매번 머리로 번역해야 한다.
            HudWind(new Vector2(r.xMax - 148f, r.y + 23f));
            var wb = new Rect(r.xMax - 126f, r.y + 19f, 76f, 9f);
            Ui.Bar(wb, _wind.magnitude / 10f, _wind.magnitude > 6f ? Ui.Bad : _wind.magnitude > 3f ? Ui.Warn : Ui.Gauge, null, Ui.Border);
            Ui.Text(new Rect(r.xMax - 126f, r.y + 27f, 76f, 16f), $"바람 {_wind.magnitude:F1}", 11, Ui.Dim);

            Ui.Text(new Rect(r.xMax - 46f, r.y + 14f, 38f, 18f),
                    $"{1f / Mathf.Max(Time.smoothDeltaTime, 1e-5f):F0}", 12, Ui.Dim, TextAnchor.MiddleRight);
            Ui.Text(new Rect(r.xMax - 46f, r.y + 28f, 38f, 14f), "fps", 9, Ui.Dim, TextAnchor.MiddleRight);
        }

        /// <summary>바람 화살표. 0°=북(+Z)이고 시계 방향 — WindArrow() 문자열 규칙과 같은 각을 쓴다.</summary>
        void HudWind(Vector2 center)
        {
            if (_wind.magnitude < 0.05f) { Ui.Text(new Rect(center.x - 8f, center.y - 8f, 16f, 16f), "-", 14, Ui.Dim, TextAnchor.MiddleCenter); return; }
            float a = Mathf.Atan2(_wind.x, _wind.y) * Mathf.Rad2Deg;
            Ui.Arrow(center, a, 26f, 4f, _wind.magnitude > 6f ? Ui.Bad : _wind.magnitude > 3f ? Ui.Warn : Ui.Gauge);
        }

        /// <summary>좌상단 — 6유닛 체력바(§8 TeamStatusUI). 색이 숫자보다 먼저 읽힌다.</summary>
        void HudTeamPanel(Unit cur)
        {
            const float RowH = 26f, PanW = 250f;
            var r = new Rect(8f, 8f, PanW, 12f + RowH * 6f + 19f);
            Ui.Box(r);
            Ui.Text(new Rect(r.x + 10f, r.y + 4f, 120f, 16f), "아군", 11, Ui.Ally, TextAnchor.MiddleLeft, true);
            Ui.Text(new Rect(r.x + PanW - 60f, r.y + 4f, 50f, 16f), "적군", 11, Ui.Enemy, TextAnchor.MiddleRight, true);

            float y = r.y + 20f;
            for (int t = 0; t < 2; t++)
                foreach (var x in _units)
                {
                    if (x.Team != t) continue;
                    var row = new Rect(r.x + 6f, y, PanW - 12f, RowH - 4f);
                    bool isCur = x == cur;
                    if (isCur) { Ui.Fill(row, new Color(1f, 1f, 1f, 0.10f)); Ui.Frame(row, x.Team == 0 ? Ui.Ally : Ui.Enemy); }

                    var teamCol = x.Team == 0 ? Ui.Ally : Ui.Enemy;
                    Ui.Fill(new Rect(row.x + 3f, row.y + 4f, 3f, row.height - 8f), teamCol);          // 팀 색 기둥
                    Ui.Text(new Rect(row.x + 12f, row.y, 84f, row.height),
                            (isCur ? "> " : "") + TankStats.Get(x.Kind).Name, 12, x.Alive ? Ui.Ink : Ui.Dim, TextAnchor.MiddleLeft, isCur);

                    var hb = new Rect(row.x + 98f, row.y + 6f, 96f, 10f);
                    float f = x.Alive ? x.HpFrac : 0f;
                    Ui.Bar(hb, f, x.Alive ? Ui.HpColor(f) : Ui.Dim, null, Ui.Border);
                    if (_items.HasShield(x.Id)) Ui.Frame(hb, Ui.Gauge, 2f);                            // 실드 = 파란 테

                    Ui.Text(new Rect(row.xMax - 52f, row.y, 48f, row.height),
                            x.Alive ? $"{x.Hp}" : "전투불능", x.Alive ? 12 : 10,
                            x.Alive ? Ui.Ink : Ui.Dim, TextAnchor.MiddleRight);
                    y += RowH;
                    if (t == 0 && x.Id % 3 == 2) { Ui.Fill(new Rect(r.x + 8f, y - 2f, PanW - 16f, 1f), Ui.Border); y += 3f; }
                }
        }

        /// <summary>좌하단 — 지금 내 탱크: 각도·포탑·이동 게이지(§8 MoveGaugeUI).</summary>
        void HudCurrent(Unit u, float H)
        {
            var r = new Rect(8f, H - 118f, 250f, 110f);
            Ui.Box(r);
            var teamCol = u.Team == 0 ? Ui.Ally : Ui.Enemy;
            Ui.Text(new Rect(r.x + 10f, r.y + 5f, 230f, 18f),
                    $"{(u.Team == 0 ? "아군" : "적군")}{u.Id % 3 + 1} · {TankStats.Get(u.Kind).Name}", 13, teamCol, TextAnchor.MiddleLeft, true);

            // 각도 — 탱크마다 상·하한이 다르다(캐롯 0~40°, 이온 20~55°). 바에 그 범위를 그려야 "왜 더 안 올라가나"가 보인다.
            var stt = TankStats.Get(u.Kind);
            var ab = new Rect(r.x + 52f, r.y + 28f, 108f, 10f);
            Ui.Text(new Rect(r.x + 10f, r.y + 23f, 42f, 18f), "각도", 11, Ui.Dim);
            float lo = Mathf.Max(MinElev, stt.MinPitch), hi = Mathf.Min(MaxElev, stt.MaxPitch);
            Ui.Bar(ab, Mathf.InverseLerp(MinElev, MaxElev, u.BarrelPitch), Ui.Warn, null, Ui.Border);
            Ui.Tick(ab, Mathf.InverseLerp(MinElev, MaxElev, lo), Ui.Dim, 1f);
            Ui.Tick(ab, Mathf.InverseLerp(MinElev, MaxElev, hi), Ui.Dim, 1f);
            Ui.Text(new Rect(ab.xMax + 4f, r.y + 23f, 42f, 18f), $"{u.BarrelPitch:F0}°", 12, Ui.Ink, TextAnchor.MiddleLeft, true);

            // 이동 게이지
            var gb = new Rect(r.x + 52f, r.y + 52f, 108f, 10f);
            Ui.Text(new Rect(r.x + 10f, r.y + 47f, 42f, 18f), "이동", 11, Ui.Dim);
            float gf = Mathf.Clamp01(u.Gauge / MoveGaugeMax);
            Ui.Bar(gb, gf, gf > 0.3f ? Ui.Gauge : Ui.Bad, null, Ui.Border);
            Ui.Text(new Rect(gb.xMax + 4f, r.y + 47f, 42f, 18f), $"{Mathf.Max(0, u.Gauge):F0}", 12, Ui.Ink, TextAnchor.MiddleLeft);

            // 포탑 방위 — 작은 나침반. 숫자 야우는 3D 에서 방향으로 안 읽힌다.
            var c = new Vector2(r.xMax - 26f, r.y + 42f);
            Ui.Fill(new Rect(c.x - 18f, c.y - 18f, 36f, 36f), new Color(1f, 1f, 1f, 0.06f));
            Ui.Frame(new Rect(c.x - 18f, c.y - 18f, 36f, 36f), Ui.Border);
            Ui.Arrow(c, u.TurretYaw, 26f, 3f, Ui.Warn);

            Ui.Text(new Rect(r.x + 10f, r.yMax - 22f, 90f, 18f),
                    $"딜레이 {(_order != null ? _order.Accumulated(u.Id) : 0)}", 11, Ui.Dim);
            DrawStatusIcons(u, new Rect(r.x + 96f, r.yMax - 21f, 150f, 16f));
        }

        /// <summary>상단 중앙 아래 — 페이즈 타이머 바(§2-1). 2페이즈 턴은 타이머가 안 보이면 규칙이 아니라 불편함이다.</summary>
        void HudPhaseTimer(float W)
        {
            if (!IsPlayerTurn || (_phase != Phase.Move && _phase != Phase.Fire))
            {
                string s = _phase == Phase.AiThink ? "적군 조준 중" : _phase == Phase.Flying ? "포탄 비행 중" : null;
                if (s != null) Ui.TextShadow(new Rect(W * 0.5f - 150f, 60f, 300f, 22f), s, 14, Ui.Dim, TextAnchor.MiddleCenter);
                return;
            }
            bool move = _phase == Phase.Move;
            float total = move ? MovePhaseSec : FirePhaseSec;
            float left = Mathf.Max(0f, _phaseTimer);
            var col = left <= 3f ? Ui.Bad : move ? Ui.Gauge : Ui.Power;

            var r = new Rect(W * 0.5f - 170f, 60f, 340f, 18f);
            Ui.Bar(r, left / total, col, Ui.Panel, Ui.Border);
            Ui.TextShadow(new Rect(r.x + 8f, r.y, 120f, r.height), move ? "MOVE" : "FIRE", 12, Ui.Ink, TextAnchor.MiddleLeft, true);
            Ui.TextShadow(new Rect(r.xMax - 68f, r.y, 60f, r.height), $"{left:F1}s", 12, Ui.Ink, TextAnchor.MiddleRight, true);
            if (move)
                Ui.TextShadow(new Rect(r.x, r.yMax + 2f, r.width, 16f),
                              "Space / 우클릭 = 조준 진입(되돌릴 수 없음)", 10, Ui.Dim, TextAnchor.MiddleCenter);
        }

        /// <summary>우하단 — 무기 슬롯 3개(§8). 지금 뭐가 장전됐는지 안 보이면 선택지가 아니라 사고다.</summary>
        void HudWeapons(Unit u, float W, float H)
        {
            var sp = TankStats.For(u.Kind, ShellKind.Special, u.HpFrac, u.W);
            var r = new Rect(W - 268f, H - 118f, 260f, 110f);
            Ui.Box(r);

            var names = new[] { "일반탄", sp.Name, "SS", "궁극" };
            bool ssReady = u.Skill.CanSs(), ultReady = u.Skill.CanUltimate();
            for (int i = 0; i < 4; i++)
            {
                var slot = new Rect(r.x + 6f + i * 62f, r.y + 8f, 58f, 40f);
                bool sel = i == 0 ? u.Shell == ShellKind.Normal
                         : i == 1 ? (u.Shell == ShellKind.Special && !_useSs && !_useUlt)
                         : i == 2 ? _useSs
                         : _useUlt;
                bool usable = i < 2 || (i == 2 ? ssReady : ultReady);
                Ui.Fill(slot, sel ? new Color(1f, 0.72f, 0.25f, 0.22f) : Ui.Slot);
                Ui.Frame(slot, sel ? Ui.Power : Ui.Border, sel ? 2f : 1f);
                Ui.Text(new Rect(slot.x + 4f, slot.y + 2f, 20f, 14f), $"{i + 1}", 11, sel ? Ui.Power : Ui.Dim, TextAnchor.MiddleLeft, true);
                Ui.Text(new Rect(slot.x, slot.y + 16f, slot.width, 18f), names[i], 10,
                        usable ? (sel ? Ui.Ink : Ui.Dim) : new Color(0.45f, 0.45f, 0.48f), TextAnchor.MiddleCenter, sel);
            }

            // 나이스샷 포인트 — SS 해금까지 얼마나 남았는지(§2-9-2). 차오르는 게 보여야 표시점을 맞출 이유가 생긴다.
            // 나이스샷 게이지 — 문턱이 둘이다(SS 2점 · 궁극 4점). 두 눈금을 같이 그려야 "얼마나 더" 가 보인다.
            var pb = new Rect(r.x + 8f, r.y + 56f, r.width - 16f, 9f);
            Ui.Bar(pb, u.Skill.Points / (float)NiceShot.UltimateCost, ultReady ? Ui.Power : ssReady ? Ui.Mark : Ui.Gauge, null, Ui.Border);
            Ui.Ticks(pb, NiceShot.UltimateCost, new Color(0f, 0f, 0f, 0.35f));
            Ui.Tick(pb, NiceShot.SsCost / (float)NiceShot.UltimateCost, Ui.Mark, 2f);     // SS 문턱
            Ui.Text(new Rect(r.x + 8f, r.y + 66f, r.width - 16f, 16f),
                    ultReady ? $"<b>궁극 사용 가능</b> (4)  나이스샷 {u.Skill.Points}/{NiceShot.UltimateCost}"
                    : ssReady ? $"<b>SS 사용 가능</b> (3)  나이스샷 {u.Skill.Points}/{NiceShot.UltimateCost}"
                              : $"나이스샷 {u.Skill.Points}/{NiceShot.UltimateCost}  <size=9>(SS {NiceShot.SsCost} · 궁극 {NiceShot.UltimateCost})</size>",
                    11, ultReady ? Ui.Power : ssReady ? Ui.Mark : Ui.Dim);

            // 방해탄(§2-9-14) — 나에게 걸린 것. (궁극기 표시는 위의 나이스샷 게이지가 이미 한다)
            var imb = new System.Text.StringBuilder();
            foreach (ImpairKind ik in System.Enum.GetValues(typeof(ImpairKind)))
            {
                if (ik == ImpairKind.None) continue;
                int left = _impair.TurnsLeft(u.Id, ik);
                if (left > 0) imb.Append($"  <color=#f88>{Impair.Name(ik)} {left}턴</color>");
            }
            if (imb.Length > 0)
                Ui.Text(new Rect(r.x + 8f, r.yMax - 40f, r.width - 16f, 18f), "방해:" + imb, 10, Ui.Dim);
            Ui.Text(new Rect(r.x + 8f, r.yMax - 22f, r.width - 16f, 18f),
                    $"폭발 {u.St.BlastRadius:F1}m · 굴착 {u.St.CraterRadius:F1}m · 직격 +{u.St.DirectDamage:F0}", 10, Ui.Dim);
        }

        /// <summary>
        /// 하단 중앙 — 파워 게이지(§11). 이 게임에서 제일 중요한 위젯이다.
        /// 표시점(§2-9-2 나이스샷)을 세로선으로 겹쳐 그려서 **멈출 자리가 눈에 보이게** 한다.
        /// </summary>
        void HudPowerBar(Unit u, float W, float H)
        {
            float bw = Mathf.Min(560f, W * 0.42f);
            var r = new Rect(W * 0.5f - bw * 0.5f, H - 78f, bw, 24f);

            Ui.Fill(new Rect(r.x - 4f, r.y - 4f, r.width + 8f, r.height + 8f), Ui.Panel);
            float p = Mathf.Clamp01(_power);
            // 파워 구간 색: 낮음 파랑 → 중간 주황 → 최대 부근 빨강. 끝까지 당기는 게 항상 정답이 아니라는 걸 색으로 알린다.
            var fill = p > 0.92f ? Ui.Bad : p > 0.6f ? Ui.Power : Ui.Gauge;
            Ui.Bar(r, p, fill, Ui.Slot, Ui.Border);
            Ui.Ticks(r, 10, new Color(0f, 0f, 0f, 0.30f));
            Ui.Tick(r, _mark, Ui.Mark, 3f);                       // 나이스샷 표시점(Q/E)

            Ui.TextShadow(new Rect(r.x, r.y - 19f, 90f, 18f), "POWER", 12, Ui.Dim, TextAnchor.MiddleLeft, true);
            Ui.TextShadow(new Rect(r.xMax - 120f, r.y - 19f, 120f, 18f),
                          $"표시점 {_mark * 100:F0} <size=10>(Q/E)</size>", 11, Ui.Mark, TextAnchor.MiddleRight);
            Ui.TextShadow(r, _charging ? $"{_power * 100:F0}" : "Space 를 눌러 차징", _charging ? 16 : 12,
                          Ui.Ink, TextAnchor.MiddleCenter, _charging);

            if (_niceFlash != null)
                Ui.TextShadow(new Rect(r.x, r.y - 40f, r.width, 20f), _niceFlash, 14, Ui.Mark, TextAnchor.MiddleCenter, true);
        }

        /// <summary>
        /// §2-6 해소 — **거리는 준다, 탄착점은 안 준다.**
        /// 기획서 §58 은 예상 탄착점을 금지하고 §60 은 "저 거리면 45도 파워 65" 학습을 원한다.
        /// 거리를 숨기면 그 학습이 성립하지 않는다 — 눈대중으로 150m 와 170m 를 못 가른다.
        /// </summary>
        void HudRange(Unit u, float W, float H)
        {
            Unit near = null; float best = float.MaxValue;
            foreach (var o in _units)
                if (o.Alive && o.Team != u.Team)
                {
                    float d = (o.Pos - u.Pos).sqrMagnitude;
                    if (d < best) { best = d; near = o; }
                }
            if (near == null) return;
            Vector3 d3 = near.Pos - u.Pos;
            float horiz = new Vector2(d3.x, d3.z).magnitude;
            Ui.TextShadow(new Rect(W * 0.5f - 300f, H - 122f, 600f, 18f),
                          $"최근접 {(near.Team == 0 ? "아군" : "적군")}{near.Id % 3 + 1}   거리 <b>{horiz:F0}m</b>   고도차 <b>{d3.y:+0;-0;0}m</b>   <size=10>(탄착 예측선 없음 §58)</size>",
                          12, Ui.Dim, TextAnchor.MiddleCenter);
        }

        /// <summary>우상단 — 아이템 슬롯([ ] 선택, Enter 사용). 숫자키는 탄종이 쓴다.</summary>
        void HudItems(Unit u, float W, float H)
        {
            if (_itemSlots <= 0 || u.Team != 0) return;
            var bag = _items.Bag(u.Id);
            var r = new Rect(W - 268f, 8f, 260f, 30f + Mathf.Max(1, bag.Count) * 20f);
            Ui.Box(r);
            Ui.Text(new Rect(r.x + 8f, r.y + 4f, 240f, 16f), "아이템 <size=10>([ ] 선택 · Enter 사용)</size>", 11, Ui.Dim, TextAnchor.MiddleLeft, true);
            if (bag.Count == 0) { Ui.Text(new Rect(r.x + 8f, r.y + 22f, 240f, 18f), "없음", 12, Ui.Dim); return; }

            for (int i = 0; i < bag.Count; i++)
            {
                var info = Items.Get(bag[i]);
                var row = new Rect(r.x + 6f, r.y + 22f + i * 20f, r.width - 12f, 18f);
                bool sel = i == _itemSel;
                if (sel) { Ui.Fill(row, new Color(1f, 1f, 1f, 0.10f)); Ui.Frame(row, Ui.Warn); }
                Ui.Text(new Rect(row.x + 6f, row.y, 100f, row.height), info.Name, 12, sel ? Ui.Ink : Ui.Dim, TextAnchor.MiddleLeft, sel);
                Ui.Text(new Rect(row.x + 104f, row.y, row.width - 110f, row.height), info.Desc, 10, Ui.Dim, TextAnchor.MiddleLeft);
            }
        }

        void HudLog(float W, float H)
        {
            if (!string.IsNullOrEmpty(_log))
                Ui.TextShadow(new Rect(W * 0.5f - 400f, H - 36f, 800f, 18f), _log, 12, Ui.Ink, TextAnchor.MiddleCenter);
            Ui.TextShadow(new Rect(W * 0.5f - 400f, H - 20f, 800f, 16f),
                          "WASD 이동   방향키 포탑·포신   Space 길게눌러 파워 → 놓으면 발사   우클릭 카메라", 10, Ui.Dim, TextAnchor.MiddleCenter);
        }

        void HudGameOver(float W, float H)
        {
            Ui.Fill(new Rect(0f, H * 0.36f, W, 120f), new Color(0.03f, 0.05f, 0.08f, 0.82f));
            Ui.TextShadow(new Rect(0f, H * 0.38f, W, 70f), _winner, 40, ResultColor(), TextAnchor.MiddleCenter, true);
            Ui.TextShadow(new Rect(0f, H * 0.38f + 68f, W, 24f), $"라운드 {_round} 종료", 14, Ui.Dim, TextAnchor.MiddleCenter);
        }

        static string WindArrow(Vector2 w)
        {
            if (w.magnitude < 0.05f) return "·";
            float a = Mathf.Atan2(w.x, w.y) * Mathf.Rad2Deg;
            string[] arr = { "↑", "↗", "→", "↘", "↓", "↙", "←", "↖" };
            int i = Mathf.RoundToInt(((a + 360f) % 360f) / 45f) % 8;
            return arr[i];
        }

        // ---------------- 성능 분해 측정 (-perf) ----------------
        //
        // "37fps 다"만으로는 아무것도 못 고친다. 무엇이 몇 ms 인지 갈라야 한다.
        // 프로파일러 없이 재는 방법: **한 번에 하나씩 꺼 보고 프레임 시간 차이를 본다.**
        // 각 단계마다 워밍업 30프레임 후 120프레임의 **중앙값**을 쓴다(평균은 GC 스파이크에 흔들린다).

        bool _perf, _hudOff, _perfInit;

        /// <summary>
        /// §2-1 2페이즈 턴이 실제로 도는지 재는 모드. `-autoshot` 은 Update 를 우회하므로
        /// MOVE/FIRE 를 한 번도 타지 않는다 — 만든 걸 재지 않고 넘어가지 않기 위한 장치다.
        /// 플레이어 입력만 합성하고 **페이즈 전환·타이머·강제 발사는 진짜 코드를 그대로 쓴다.**
        /// </summary>
        bool _phaseCheck;

        /// <summary>
        /// 3종 실루엣 대조 모드. **"구분된다"는 주장은 나란히 놓고 봐야 검증된다** —
        /// 각 종을 따로 보면 다 탱크처럼 보이므로 차이를 과대평가하게 된다.
        /// 정면·측면·부감 세 각도로 찍는 이유도 같다: 한 각도에서만 갈리면 게임에선 안 갈린다.
        /// </summary>
        bool _gallery;

        // ── 연습장(§68) ──
        // 기획서 §68 의 연습장. §1("포격 감각이 실력을 결정한다")·§4("조작은 쉽게, 포격은 어렵게")를
        // 가르치는 자리다. 전투와 같은 탄도·지형·바람을 쓰되 **적이 반격하지 않고 표적이 되살아난다** —
        // 한 판을 이기는 게 목적이 아니라 같은 조건을 반복해서 감각을 만드는 게 목적이기 때문이다.
        // ── 헬기 보급(§2-9-11) ── 게임과 하네스가 같은 Sim/SupplyDrop 을 쓴다.
        //    원작: 일정 확률로 헬기가 나타나 상자를 떨구고 사라진다 / 이동으로 주우면 아이템 획득 /
        //    상자를 부수면 안의 아이템도 사라진다(출처는 Sim/SupplyDrop.cs 머리말).
        readonly SupplyDrop _supply = new SupplyDrop();
        Rng _supplyRng = new Rng(0x2B31Du);
        readonly List<Transform> _crateGos = new List<Transform>();
        Material _crateMat;
        Transform _heli;                 // 떨구고 지나가는 헬기(가로지르는 판때기 한 장)
        float _heliTimer;
        Vector3 _heliFrom, _heliTo;

        float GroundOf(float x, float z, float nearY)
        {
            float g = TankGroundProbe.GroundBelow(_vol, x, z, nearY + 6f);
            return float.IsNegativeInfinity(g) ? nearY : g;
        }

        /// <summary>라운드가 바뀔 때 호출. 살아 있는 탱크를 앵커로 넘긴다 — 닿을 수 있는 자리에 떨어져야 보급이다.</summary>
        /// <summary>
        /// 보급 앵커 + 팀. **팀을 같이 넘겨야** 상자가 양 팀에 반반 떨어진다 —
        /// 안 넘기면 유닛이 많은(이기는) 쪽에 몰려 우위가 증폭된다(SupplyDrop.RollDrop 머리말).
        /// ⚠️ 호출부가 셋이라 여기 한 곳에서만 만든다(같은 로직이 흩어지면 한 곳만 고치고 넘어간다).
        /// </summary>
        List<Vec3> SupplyAnchors(out List<int> teams)
        {
            var anchors = new List<Vec3>();
            teams = new List<int>();
            foreach (var u in _units)
                if (u.Alive) { var p = u.Pos; anchors.Add(new Vec3(p.x, p.y, p.z)); teams.Add(u.Team); }
            return anchors;
        }

        void RollSupply()
        {
            if (_itemSlots == 0) return;
            var anchors = SupplyAnchors(out var anchorTeams);
            if (anchors.Count == 0) return;
            int n = _supply.RollDrop(ref _supplyRng, MapSize, (x, z) => TankGroundProbe.GroundBelow(_vol, x, z, 80f), anchors, anchorTeams);
            if (n <= 0) return;
            var last = _supply.Crates[_supply.Count - 1];
            ShowHelicopter(last.X, last.Z);
            _log = "[보급] 헬기가 상자를 떨궜다 — 이동해서 주우면 아이템, 부수면 사라진다";
            RefreshCrates();
        }

        void ShowHelicopter(float x, float z)
        {
            if (_heli == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "SupplyHeli";
                Destroy(go.GetComponent<Collider>());
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = MakeMat(new Color(0.20f, 0.24f, 0.28f), 0.6f);
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.transform.localScale = new Vector3(5.0f, 1.2f, 2.0f);
                _heli = go.transform;
            }
            float y = GroundOf(x, z, 20f) + 26f;
            _heliFrom = new Vector3(x - 70f, y, z);
            _heliTo = new Vector3(x + 70f, y, z);
            _heliTimer = 2.2f;
            _heli.position = _heliFrom;
            _heli.gameObject.SetActive(true);
        }

        void TickHelicopter(float dt)
        {
            if (_heli == null || !_heli.gameObject.activeSelf) return;
            _heliTimer -= dt;
            if (_heliTimer <= 0f) { _heli.gameObject.SetActive(false); return; }
            _heli.position = Vector3.Lerp(_heliTo, _heliFrom, _heliTimer / 2.2f);
        }

        /// <summary>상자 뷰를 상태에 맞춘다. 지형이 깎였으면 먼저 내려앉힌다.</summary>
        void RefreshCrates()
        {
            if (_crateMat == null) _crateMat = MakeMat(new Color(0.85f, 0.65f, 0.20f), 0.35f);
            int used = 0;
            foreach (var c in _supply.Crates)
            {
                if (used >= _crateGos.Count)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = $"Crate{_crateGos.Count}";
                    Destroy(go.GetComponent<Collider>());
                    go.GetComponent<MeshRenderer>().sharedMaterial = _crateMat;
                    _crateGos.Add(go.transform);
                }
                var t = _crateGos[used++];
                t.gameObject.SetActive(true);
                t.position = new Vector3(c.X, GroundOf(c.X, c.Z, c.Y) + 1.0f, c.Z);
                t.localScale = Vector3.one * 2.0f;
            }
            for (int i = used; i < _crateGos.Count; i++) _crateGos[i].gameObject.SetActive(false);
        }

        /// <summary>이 유닛이 서 있는 자리의 상자를 줍는다(원작: "이동으로 상자를 얻으면 아이템").</summary>
        bool TryPickupSupply(Unit u) { var p = u.Pos; return TryPickupSupplyAt(u, p.x, p.y, p.z); }

        bool TryPickupSupplyAt(Unit u, float x, float y, float z)
        {
            if (_itemSlots == 0 || _supply.Count == 0) return false;
            var got = _supply.TryPickup(x, y, z);
            if (got == ItemKind.None) return false;
            _items.Bag(u.Id).Add(got);
            _log = $"[보급] {(u.Team == 0 ? "아군" : "적군")}{u.Id % 3 + 1} 획득 — {Items.Get(got).Name}";
            RefreshCrates();
            return true;
        }

        /// <summary>
        /// `-supplyselftest` — 헬기 보급이 **게임에서 실제로 동작하는지** 스스로 확인한다.
        /// ⚠️ 이게 없으면 보급은 검증 불가다: 기본 `-autoshot` 은 라운드 1 에서 끝나는데
        ///    투하는 라운드가 바뀔 때 일어나므로 한 번도 안 굴러간다("컴파일됨 = 동작함" 이 아니다).
        /// 세 가지를 순서대로 잰다 — ① 앵커 투하가 닿을 자리에 떨어지는가 ② 걸어가서 주워지는가
        /// ③ 폭발이 상자를 부수는가. 하나라도 실패하면 종료 코드 1 로 죽는다.
        /// </summary>
        void SupplySelfTestStep()
        {
            _frame++;
            var me = _units[0];
            if (_frame == 1)
            {
                _camYaw = 160f; _camPitch = 30f; _camDist = 45f; UpdateCamera(0f);
                var anchors = new List<Vec3> { new Vec3(me.Pos.x, me.Pos.y, me.Pos.z) };
                for (int t = 0; t < 40 && _supply.Count == 0; t++)
                    _supply.RollDrop(ref _supplyRng, MapSize, (x, z) => TankGroundProbe.GroundBelow(_vol, x, z, 80f), anchors, SupplyAnchors(out var _at) != null ? _at : null);
                if (_supply.Count == 0) { Debug.Log("[Tankfall] ❌ 보급 자체검사 — 40번 굴려도 상자가 안 떨어졌다(투하 로직 고장)"); Application.Quit(1); return; }
                var c0 = _supply.Crates[0];
                float d0 = Mathf.Sqrt((c0.X - me.Pos.x) * (c0.X - me.Pos.x) + (c0.Z - me.Pos.z) * (c0.Z - me.Pos.z));
                Debug.Log($"[Tankfall] 보급 자체검사 ① 투하 거리 {d0:F1}m (앵커 {SupplyDrop.AnchorMin}~{SupplyDrop.AnchorMax}m)");
                _supplyTestBag0 = _items.Count(me.Id);   // ⚠️ 여기서 재야 한다 — 뒤에서 재면 "안 주웠는데 통과"가 된다
                RefreshCrates();
                Shot("보급_투하");
                return;
            }
            if (_supply.Count > 0 && _items.Count(me.Id) <= _supplyTestBag0)
            {
                var c = _supply.Crates[0];
                me.Heading = Mathf.Atan2(c.X - me.Pos.x, c.Z - me.Pos.z) * Mathf.Rad2Deg;
                me.Gauge = MoveGaugeMax;                 // 자체검사는 게이지 제한을 안 본다(이동 로직만 본다)
                DriveUnit(me, 1f, 1f / 30f);
                GroundUnit(me, 1f / 30f);
                UpdateCamera(0.1f);
                if (_frame > 600)
                {
                    Debug.Log($"[Tankfall] ❌ 보급 자체검사 — 600프레임 걸어도 못 주웠다(이동/획득 고장, 남은 상자 {_supply.Count})");
                    Application.Quit(1);
                }
                return;
            }
            if (!_supplyTestPicked)
            {
                _supplyTestPicked = true;
                Debug.Log($"[Tankfall] 보급 자체검사 ② 획득 성공 — 가방 {_supplyTestBag0} → {_items.Count(me.Id)} ({_frame}프레임 걸어감)");
                Shot("보급_획득");
                // ③ 폭발로 부수기
                var anchors = new List<Vec3> { new Vec3(me.Pos.x, me.Pos.y, me.Pos.z) };
                for (int t = 0; t < 40 && _supply.Count == 0; t++)
                    _supply.RollDrop(ref _supplyRng, MapSize, (x, z) => TankGroundProbe.GroundBelow(_vol, x, z, 80f), anchors, SupplyAnchors(out var _at) != null ? _at : null);
                if (_supply.Count == 0) { Debug.Log("[Tankfall] ❌ 보급 자체검사 — 파괴 시험용 상자를 못 만들었다"); Application.Quit(1); return; }
                var c = _supply.Crates[0];
                int broke = _supply.DestroyNear(c.X, c.Y, c.Z, 1f);
                RefreshCrates();
                if (broke != 1) { Debug.Log($"[Tankfall] ❌ 보급 자체검사 ③ 폭발이 상자를 안 부쉈다(부순 수 {broke})"); Application.Quit(1); return; }
                Debug.Log("[Tankfall] 보급 자체검사 ③ 폭발로 파괴 확인");
                Debug.Log("[Tankfall] ✅ 헬기 보급(§2-9-11) — 투하·획득·파괴 전부 게임에서 동작한다");
                VerifyShots();
                Application.Quit(0);
            }
        }

        bool _climateSelfTest;

        /// <summary>
        /// `-climateselftest` — 기후(§2-9-15)가 **실제로 탄에 작용하는지** 확인한다.
        /// 켠 상태와 끈 상태를 **같은 사격으로 나란히** 비교한다(네거티브 컨트롤이 곧 대조군이다).
        /// </summary>
        void ClimateSelfTestStep()
        {
            int fail = 0;
            var me = _units[0];
            var p0 = new Vec3(me.Fire.position.x, me.Fire.position.y + 1f, me.Fire.position.z);
            var st0 = TankStats.For(me.Kind, ShellKind.Normal, 1f, Weather.Clear);
            float speed = st0.SpeedAt(0.85f);
            var accel = Ballistics.Accel(0f, 0f);
            // 적 쪽(+x)으로 완만하게 쏜다 — 맵 한가운데를 지나가는 궤적이어야 기후에 닿는다.
            var v0 = Ballistics.VelocityFrom(90f, 42f, speed);

            // ── 증폭벽 ──
            _air.Clear();
            var plain = ProjectileSimulator.Simulate(_vol, p0, v0, accel, null, me.Id, MapSize, _air);
            if (plain.DamageScale != 1f) { Debug.Log($"[Tankfall] ❌ 기후 없는데 증폭이 걸렸다({plain.DamageScale})"); fail++; }
            _air.Clear();
            _air.AddWall(new AmpWall { X = p0.X + 40f, Z = p0.Z, HalfLen = 60f, MinY = 0f, MaxY = 120f });
            var amped = ProjectileSimulator.Simulate(_vol, p0, v0, accel, null, me.Id, MapSize, _air);
            if (amped.DamageScale != AirField.AmpScale)
            { Debug.Log($"[Tankfall] ❌ 증폭벽을 지났는데 배율이 {amped.DamageScale} (원작 {AirField.AmpScale})"); fail++; }
            else Debug.Log($"[Tankfall] 기후 자체검사 증폭벽 — 통과 시 피해 ×{amped.DamageScale} (안 지나면 ×{plain.DamageScale})");

            // ── 회오리 ──
            _air.Clear();
            if (plain.Tornadoed) { Debug.Log("[Tankfall] ❌ 회오리가 없는데 휘말렸다"); fail++; }
            float tx = p0.X + 40f;
            _air.Clear();
            _air.AddTornado(new Tornado { X = tx, Z = p0.Z, Radius = 30f, TopY = AirField.TornadoTop });
            var tor = ProjectileSimulator.Simulate(_vol, p0, v0, accel, null, me.Id, MapSize, _air);
            if (!tor.Tornadoed) { Debug.Log("[Tankfall] ❌ 회오리 기둥을 지났는데 안 휘말렸다"); fail++; }
            else if (!tor.Hit) { Debug.Log("[Tankfall] ❌ 회오리에 휘말린 탄이 어디에도 안 떨어졌다"); fail++; }
            else
            {
                // 원작: 빨려 올라갔다가 떨어진다 → 착탄점이 회오리 근처여야 하고 원래 착탄점과 달라야 한다.
                float near = Mathf.Sqrt((tor.Impact.X - tx) * (tor.Impact.X - tx) + (tor.Impact.Z - p0.Z) * (tor.Impact.Z - p0.Z));
                float moved = Mathf.Abs(tor.Impact.X - plain.Impact.X);
                if (near > 40f) { Debug.Log($"[Tankfall] ❌ 회오리에 걸렸는데 {near:F0}m 나 떨어진 곳에 떨어졌다"); fail++; }
                else if (moved < 5f) { Debug.Log($"[Tankfall] ❌ 회오리가 궤적을 안 바꿨다(이동 {moved:F1}m)"); fail++; }
                else Debug.Log($"[Tankfall] 기후 자체검사 회오리 — 착탄이 {moved:F0}m 밀려 회오리에서 {near:F0}m 지점에 떨어짐");
            }

            _air.Clear(); RefreshAir();
            if (fail == 0) Debug.Log("[Tankfall] ✅ 기후(§2-9-15) — 증폭벽·회오리가 실제로 탄에 작용한다");
            else Debug.Log($"[Tankfall] ❌ 기후 자체검사 실패 {fail}건");
            Application.Quit(fail == 0 ? 0 : 1);
        }

        bool _impairSelfTest;

        /// <summary>
        /// `-impairselftest` — 방해탄(§2-9-14)이 **게임에서** 도는지 스스로 확인한다.
        /// ⚠️ 화면 방해 4종은 "상태가 켜졌다"로 끝내면 안 된다 — **화면이 실제로 달라지는지**까지 잰다
        ///    (카메라 회전이 바뀌는가, 안개가 적 렌더러를 끄는가). 안 그러면 선언만 하고 죽은 값이 된다.
        /// </summary>
        void ImpairSelfTestStep()
        {
            // 내 팀(0) 탱크가 행동할 때까지 돌린다 — 화면 방해는 내 탱크에 걸려야 내 화면을 건드린다.
            for (int guard = 0; guard < 12 && Current.Team != 0; guard++) AdvanceTurn();
            var me = Current;
            int fail = 0;
            if (me.Team != 0) { Debug.Log("[Tankfall] ❌ 방해 자체검사 — 아군 턴을 못 찾았다"); Application.Quit(1); return; }

            foreach (ImpairKind k in System.Enum.GetValues(typeof(ImpairKind)))
            {
                if (k == ImpairKind.None) continue;
                _impair.Clear();
                _impair.Apply(me.Id, k);

                // ① 원작 지속 턴이 그대로 들어갔는가
                int want = Impair.Turns(k);
                if (_impair.TurnsLeft(me.Id, k) != want)
                { Debug.Log($"[Tankfall] ❌ {Impair.Name(k)}: 지속턴 {_impair.TurnsLeft(me.Id, k)} (원작 {want})"); fail++; continue; }

                // ② 효과가 실제로 나오는가
                switch (k)
                {
                    case ImpairKind.LockPower:
                        if (_impair.ClampPower(me.Id, 0.2f) != Impair.PowerFloor)
                        { Debug.Log("[Tankfall] ❌ 파워고정탄이 하한을 안 걸었다"); fail++; }
                        if (_impair.ClampPower(me.Id, 0.9f) != 0.9f)
                        { Debug.Log("[Tankfall] ❌ 파워고정탄이 하한 위 파워까지 건드렸다"); fail++; }
                        break;
                    case ImpairKind.LockAngle:
                        if (!_impair.AngleLocked(me.Id)) { Debug.Log("[Tankfall] ❌ 각도고정탄이 안 걸렸다"); fail++; }
                        break;
                    case ImpairKind.Fog:
                        RefreshFogVisibility();
                        bool anyEnemyVisible = false;
                        foreach (var o in _units)
                            if (o.Team != 0 && o.Root != null)
                                foreach (var mr in o.Root.GetComponentsInChildren<MeshRenderer>(true)) if (mr.enabled) anyEnemyVisible = true;
                        if (anyEnemyVisible) { Debug.Log("[Tankfall] ❌ 안개탄인데 적 탱크가 아직 보인다"); fail++; }
                        _impair.Clear(); RefreshFogVisibility();
                        bool backVisible = false;
                        foreach (var o in _units)
                            if (o.Team != 0 && o.Root != null)
                                foreach (var mr in o.Root.GetComponentsInChildren<MeshRenderer>(true)) if (mr.enabled) backVisible = true;
                        if (!backVisible) { Debug.Log("[Tankfall] ❌ 안개가 풀렸는데 적이 안 돌아왔다"); fail++; }
                        _impair.Apply(me.Id, k);
                        break;
                    case ImpairKind.FlipScreen:
                    case ImpairKind.Wobble:
                        // 카메라가 실제로 달라지는지 — 같은 프레임 조건에서 걸기 전/후를 비교한다.
                        _impair.Clear(); _impairClock = 0.7f; UpdateCamera(0f);
                        var before = _cam.transform.rotation;
                        _impair.Apply(me.Id, k); _impairClock = 0.7f; UpdateCamera(0f);
                        if (Quaternion.Angle(before, _cam.transform.rotation) < 1f)
                        { Debug.Log($"[Tankfall] ❌ {Impair.Name(k)}: 카메라가 그대로다(화면이 안 바뀐다)"); fail++; }
                        break;
                    case ImpairKind.Confuse:
                        // 원작 "자신의 턴이 와도 화면이 자신의 위치로 오지 않는다"
                        _impair.Clear(); UpdateCamera(0f);
                        var camSelf = _cam.transform.position;
                        _impair.Apply(me.Id, k);
                        var others = _units.FindAll(x => x.Alive && x.Id != me.Id);
                        _confuseFocus = others.Count > 0 ? others[0].Pos + Vector3.up * 2.2f : me.Pos;
                        UpdateCamera(0f);
                        if ((camSelf - _cam.transform.position).magnitude < 1f)
                        { Debug.Log("[Tankfall] ❌ 바보탄인데 카메라가 여전히 내 자리다"); fail++; }
                        break;
                }

                // ③ 수명이 다하면 풀리는가
                for (int t = 0; t < want; t++) _impair.TickStartOfTurn(me.Id);
                if (_impair.Has(me.Id, k)) { Debug.Log($"[Tankfall] ❌ {Impair.Name(k)}: {want}턴 뒤에도 안 풀린다"); fail++; }

                Debug.Log($"[Tankfall] 방해 자체검사 {Impair.Name(k),-10} {want}턴 · 효과 확인");
            }
            _impair.Clear(); RefreshFogVisibility();
            if (fail == 0) Debug.Log("[Tankfall] ✅ 방해탄(§2-9-14) — 6종 전부 지속턴·실제 효과·해제 확인");
            else Debug.Log($"[Tankfall] ❌ 방해 자체검사 실패 {fail}건");
            Application.Quit(fail == 0 ? 0 : 1);
        }

        bool _ultSelfTest;

        /// <summary>
        /// `-ultselftest` — 궁극기(§49)가 **게임에서** 도는지 스스로 확인한다.
        ///
        /// 궁극기는 "2번탄을 핵급으로 키우는 것"(오너 확정 2026-09-17 "궁극기가 핵 쏘는 거라니까").
        /// 그래서 검사할 것은 셋이다: **열리는가 · 커지는가 · 소모되는가**.
        ///
        /// ⚠️ 이 검사가 왜 있어야 하는가 — 궁극기가 **한 번도 발동 안 된 채로 통과하던 적이 있다.**
        ///    AI 가 2점에서 SS 를 써버려 4점에 영영 못 닿았는데, 하네스의 궁극기 ON/OFF 승률이
        ///    숫자 하나까지 같아서야 들통났다(NiceShot.AiSaveForUltimate 머리말).
        ///    "코드가 있다" 와 "게임에서 열린다" 는 다르다 — 게이지를 실제로 채워서 연다.
        /// ⚠️ 네거티브 컨트롤: 문턱 직전(UltimateCost-1)에서는 **닫혀 있어야** 한다.
        /// </summary>
        void UltSelfTestStep()
        {
            int fail = 0, done = 0;
            foreach (TankKind k in System.Enum.GetValues(typeof(TankKind)))
            {
                string name = TankStats.Get(k).Name;
                var sp = TankStats.For(k, ShellKind.Special, 1f, Weather.Clear);

                // ① 열리는가 — 나이스샷을 실제로 쌓아서 연다. 문턱 직전에는 닫혀 있어야 한다(네거티브 컨트롤).
                var gauge = new SkillGauge();
                for (int i = 0; i < NiceShot.UltimateCost - 1; i++) gauge.OnNiceShot();
                if (gauge.CanUltimate()) { Debug.Log($"[Tankfall] ❌ {name}: 문턱 직전인데 궁극기가 열렸다"); fail++; continue; }
                gauge.OnNiceShot();
                if (!gauge.CanUltimate()) { Debug.Log($"[Tankfall] ❌ {name}: 문턱을 채웠는데 안 열린다"); fail++; continue; }

                // ② 커지는가 — 핵이라면 2번탄보다 확실히 세야 한다. 피해·폭발·굴착 중 하나도 안 크면 실패.
                var ult = NiceShot.ApplyUltimate(sp, k);
                float dmgMul = sp.BaseDamage > 0f ? ult.BaseDamage / sp.BaseDamage : 0f;
                float blastMul = sp.BlastRadius > 0f ? ult.BlastRadius / sp.BlastRadius : 0f;
                float craterMul = sp.CraterRadius > 0f ? ult.CraterRadius / sp.CraterRadius : 0f;
                if (dmgMul <= 1.01f && blastMul <= 1.01f && craterMul <= 1.01f)
                { Debug.Log($"[Tankfall] ❌ {name}: 궁극기가 2번탄과 똑같다(피해 ×{dmgMul:F2})"); fail++; continue; }
                // 핵급의 최소선 — 피해 1.5배 **또는** 폭발/굴착 1.8배. 둘 다 못 넘으면 "핵" 이라 부를 수 없다.
                if (dmgMul < 1.5f && blastMul < 1.8f && craterMul < 1.8f)
                { Debug.Log($"[Tankfall] ❌ {name}: 핵급이 아니다(피해 ×{dmgMul:F2} 폭발 ×{blastMul:F2} 굴착 ×{craterMul:F2})"); fail++; continue; }

                // ③ 소모되는가 — 안 비면 영구 버프가 된다.
                gauge.SpendUltimate();
                if (gauge.CanUltimate()) { Debug.Log($"[Tankfall] ❌ {name}: 쓰고 나서도 궁극기가 열려 있다"); fail++; continue; }

                Debug.Log($"[Tankfall] 궁극 자체검사 {name,-10} 피해 ×{dmgMul:F1} · 폭발 ×{blastMul:F1} · 굴착 ×{craterMul:F1}");
                done++;
            }
            if (fail == 0 && done >= 13)
                Debug.Log($"[Tankfall] ✅ 궁극기(§49, 핵) — 13종 {done}개 전부 열림·핵급·소모 확인");
            else
                Debug.Log($"[Tankfall] ❌ 궁극 자체검사 실패 {fail}건 (확인 {done}/13)");
            Application.Quit(fail == 0 && done >= 13 ? 0 : 1);
        }

        bool _supplySelfTest;
        int _supplyTestBag0 = -1;
        bool _supplyTestPicked;

        // ── 아이템(§2-9-10) ── 게임과 하네스가 같은 Sim/ItemState 를 쓴다.
        readonly ItemState _items = new ItemState();
        // ── 방해탄(§2-9-14) ── 규칙·지속턴의 단일 소스는 Sim/Impair.cs(원작 출처 머리말에 있음).
        readonly ImpairState _impair = new ImpairState();
        // ── 기후: 증폭벽·회오리(§2-9-15) ── 규칙·출처는 Sim/AirFeatures.cs 머리말.
        readonly AirField _air = new AirField();
        Rng _airRng = new Rng(0x51C0Du);
        readonly List<Transform> _airGos = new List<Transform>();
        float _tornadoSpin;
        // ── Boom 모드(§2-9-16) ── 지뢰밭·지진·유성. 규칙·출처는 Sim/BoomMode.cs 머리말.
        bool _boom, _boomSelfTest;
        Rng _boomRng = new Rng(0x7A11Fu);
        readonly List<(float X, float Z)> _boomSpots = new List<(float X, float Z)>();
        float _impairClock;          // 멀미탄 울렁임 위상
        Texture2D _fogTex;           // 안개탄 화면 덮개
        int _itemSlots = 2;              // -items N 으로 조절, 0 이면 끔
        int _itemSel;                    // 선택된 가방 칸
        Rng _itemRng = new Rng(0x17E45u);

        bool _practice;
        int _practiceShots, _practiceHits;
        float _practiceBestMiss = float.MaxValue, _practiceLastMiss = -1f;
        string _practiceAnswer = "";
        Vec3 _practiceImpact;
        int _shooterId = -1;
        /// <summary>더블파이어로 한 발 더 쏠 때 같은 각도·파워를 그대로 재사용한다(원작 "같은 힘과 각도").</summary>
        bool _pendingDoubleFire; float _dfYaw, _dfPitch, _dfPower;
        float _practiceMissAtImpact = float.MaxValue;   // 착탄 **순간**의 표적까지 거리(지형이 깎이기 전)
        /// <summary>`-practiceselftest`: 정답 보기가 낸 각도·파워로 **자동으로 쏴 보고** 실제로 맞는지 센다.
        /// 사람 입력이 필요한 모드라 이게 없으면 연습장은 자동 검증이 불가능하다. 정답 기능의 네거티브 컨트롤이기도 하다 —
        /// 정답대로 쐈는데 안 맞으면 역산(§5-7)이나 채점이 고장난 것이다.</summary>
        bool _practiceSelfTest;
        string _practiceSolveNote = "";
        bool _practiceSolveVerified;                 // 이번 해가 시뮬 검증을 통과한 해인가(폴백이 아닌가)
        int _practiceVerifiedShots, _practiceVerifiedHits;
        int _practiceSelfTestShots;
        Phase _lastLoggedPhase = Phase.GameOver;
        int _pcTurns, _pcForced, _pcMoveSkips;
        float _pcMoveHold;
        Vector3 _impactFocus; bool _hasImpactFocus;
        TankStats _shooterStats = TankStats.Get(TankKind.Carrot);

        /// <summary>이벤트 단위 비용 계측. 프레임 시간(1.06ms)이 아니라 **한 번 일어날 때 멈칫하는가**를 잰다.</summary>
        bool _timeEvents;
        int _perfStage, _perfFrame;
        readonly List<float> _perfSamples = new List<float>();

        static readonly string[] PerfStages =
        {
            "A 전체(기준)",
            "B HUD(OnGUI) 끔",
            "C 지형 그림자 끔",
            "D 지형 그림자+HUD 끔",
            "E 지형 렌더러 끔",
            "F 지형+탱크 전부 끔",
        };

        void ApplyPerfStage(int st)
        {
            _hudOff = (st == 1 || st == 3 || st == 5);
            _terrain.SetShadows(st < 2);
            _terrain.SetVisible(st < 4);
            foreach (var u in _units)
                if (u.Root != null)
                    foreach (var mr in u.Root.GetComponentsInChildren<MeshRenderer>())
                        mr.enabled = (st < 5);
        }

        void PerfStep()
        {
            if (!_perfInit)
            {
                _perfInit = true;
                // vsync·프레임 상한이 켜져 있으면 진짜 비용이 안 보인다
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = -1;
                _camYaw = 160f; _camPitch = 24f; _camDist = 55f; UpdateCamera(0f);
                ApplyPerfStage(0);
                Debug.Log($"[Tankfall] 성능 분해 시작 · 청크 {_terrain.ChunkCount} · 삼각형 {_terrain.TriangleCount} " +
                          $"· {Screen.width}x{Screen.height} · {SystemInfo.graphicsDeviceName}");
            }
            _perfFrame++;

            if (_perfFrame <= 30) return;                       // 워밍업
            _perfSamples.Add(Time.unscaledDeltaTime * 1000f);
            if (_perfSamples.Count < 120) return;

            _perfSamples.Sort();
            float med = _perfSamples[_perfSamples.Count / 2];
            Debug.Log($"[Tankfall] PERF {PerfStages[_perfStage],-22} {med,6:F2} ms  ({1000f / med,5:F1} fps)");

            _perfSamples.Clear();
            _perfFrame = 0;
            _perfStage++;
            if (_perfStage >= PerfStages.Length)
            {
                Debug.Log("[Tankfall] 성능 분해 완료");
                Application.Quit(0);
                return;
            }
            ApplyPerfStage(_perfStage);
        }

        // ---------------- 3종 실루엣 대조(-gallery) ----------------

        const float GalGap = 8f;      // 갤러리 줄 간격
        const float GalZ = 100f;
        float _galY;                  // 갤러리 줄의 바닥 높이(= 배경 바닥판 윗면)

        void GalleryStep()
        {
            _frame++;
            if (_frame == 1) BuildGalleryLineup();

            // ⚠️ 13대를 한 장에 넣으면 폭이 96m 라 탱크 하나가 30픽셀이 된다.
            //    실루엣을 검증하려고 찍는 사진인데 실루엣이 안 보이면 찍을 이유가 없다.
            //    **계열별로 나눠 찍고** 전체는 확인용 한 장만 남긴다.
            Pose(20, 180f, 14f, 60f, 100f, "20_전체_정면");
            Pose(35, 180f, 9f, 20f, Cx(0), "21_고전_정면");     // 캐터펄트·크로스보우·캐논
            Pose(50, 180f, 9f, 20f, Cx(3), "22_근대_정면");     // 캐롯·듀크·마인랜더
            Pose(65, 180f, 9f, 20f, Cx(6), "23_현대_정면");     // 미사일·멀티미사일·슈퍼탱크
            Pose(80, 180f, 9f, 26f, Cx(9, 4), "24_미래_정면");  // 레이저·이온·포세이돈·세크윈드
            Pose(95, 90f, 9f, 60f, 100f, "25_전체_측면");       // 길이·보기륜이 읽힌다
            Pose(110, 135f, 50f, 72f, 100f, "26_전체_부감");    // 포탑 기둥 칸수가 읽힌다
            if (_frame == 125)
            {
                Debug.Log("[Tankfall] gallery 완료");
                VerifyShots();
                Application.Quit(0);
            }
        }

        /// <summary>계열 첫 인덱스로부터 그 무리의 화면 중심 x.</summary>
        static float Cx(int first, int count = 3) => 100f + (first + (count - 1) * 0.5f - 6f) * GalGap;

        /// <summary>
        /// 전투용 6대를 치우고 **13종 전부**를 한 줄로 세운다.
        ///
        /// ⚠️ 전투 편성(3종)을 그대로 찍으면 나머지 10종을 한 번도 못 본다 — 그림이 틀려도 모른다.
        ///    팀은 한 대씩 번갈아 준다. 팀 색(띠·기둥)이 종 색(차체)을 덮지 않는지 같이 확인해야 하기 때문이다.
        /// </summary>
        void BuildGalleryLineup()
        {
            _terrain.SetVisible(false);
            // ⚠️ 지형을 꺼도 배경 바닥판(Environment.Apron)은 남는다. 예전엔 탱크를 y=0 에 세워서
            //    바닥판(맵 가장자리 지면 높이)보다 낮으면 측면·부감 샷에 탱크가 한 대도 안 나왔다.
            //    바닥판 윗면 높이에 세운다.
            _galY = _env != null ? _env.ApronTopY + 0.02f : 0f;
            foreach (var u in _units) Destroy(u.Root.gameObject);
            _units.Clear();

            var track = MakeMat(new Color(0.22f, 0.20f, 0.24f), 0f);
            var wood = MakeMat(new Color(0.56f, 0.37f, 0.20f), 0.05f);   // 통나무 범퍼
            var teamMat = new[]
            {
                MakeMat(new Color(0.15f, 0.48f, 0.98f), 0.45f),
                MakeMat(new Color(0.98f, 0.20f, 0.24f), 0.45f),
            };

            for (int i = 0; i < TankStats.Count; i++)
            {
                var kind = (TankKind)i;
                int team = i % 2;
                var root = ProceduralTank.Build(TankShape.Of(kind),
                                                MakeMat(TankShape.BodyColor(kind), 0.22f),
                                                track, teamMat[team], out var tur, out var bar, out var fp, wood);
                root.position = new Vector3(100f + (i - 6) * GalGap, _galY, GalZ);
                _units.Add(new Unit { Id = i, Team = team, Kind = kind, HpMax = TankStats.Get(kind).Hp,
                                      Hp = TankStats.Get(kind).Hp,
                                      Root = root, Turret = tur, Barrel = bar, Fire = fp });
            }
            _log = "포트리스 13종 — 고전 3 · 근대 3 · 현대 3 · 미래 4   |   차체=기종색, 바퀴·포신=팀색";
        }

        /// <summary>탱크를 heading 으로 돌려 세우고, 카메라는 줄에 수직인 고정 위치에서 찍는다.</summary>
        void Pose(int at, float heading, float camPitch, float dist, float centerX, string name)
        {
            if (_frame != at) return;
            foreach (var u in _units)
            {
                u.Heading = heading;
                // ⚠️ ApplyAim 은 포탑·포신만 돌린다. 차체 회전은 평소 GroundUnit 이 지형 법선에
                //    맞추면서 넣어주는데, 갤러리는 지형을 껐으니 호출되지 않는다 —
                //    Heading 만 바꾸고 끝내면 정면 샷과 측면 샷이 똑같이 나온다(실제로 그랬다).
                u.Root.rotation = Quaternion.Euler(0f, heading, 0f);
                u.TurretYaw = 0f;
                u.BarrelPitch = 12f;
                ApplyAim(u);
            }
            // ⚠️ 카메라를 돌리면 안 된다. 탱크를 X축으로 나란히 세워 놓고 카메라를 90° 돌리면
            //    전부 일렬로 겹쳐 한 대처럼 보인다(실제로 그렇게 찍혔다).
            //    **카메라는 줄에 항상 수직으로 두고, 보고 싶은 면은 탱크를 돌려서 만든다.**
            var focus = new Vector3(centerX, _galY + 1.2f, GalZ);
            var rot = Quaternion.Euler(camPitch, 0f, 0f);     // 야우 0 고정 = 줄에 수직
            _cam.transform.position = focus + rot * Vector3.back * dist;
            _cam.transform.LookAt(focus);
            Shot(name);
        }

        int _flyFrame, _volley, _resolveFrame;
        float _nukeShotAt;           // 0 이 아니면 그 시각에 버섯구름을 찍는다(-forceult 확인용)

        /// <summary>
        /// ⚠️ 예전엔 `for (i=0; i<400; i++) FlyStep(0.02f)` 로 **한 프레임에 비행을 다 돌렸다**.
        ///    그러면 궤적도 착탄 순간도 찍을 수 없고(사후 결과만 남는다),
        ///    HUD 의 smoothDeltaTime 이 튀어 "37 fps" 라는 가짜 성능 문제까지 만들었다(§7-7).
        ///    이제 실제 프레임에 걸쳐 진행시키고, **포탄이 날 때와 터지는 순간**을 찍는다.
        /// </summary>
        void AutoShotStep()
        {
            _frame++;

            if (_phase == Phase.Flying)
            {
                FlyStep(1f / 30f);              // 프레임당 path 4스텝 — 비행 3.8초가 약 110프레임
                UpdateCamera(0.05f);
                _flyFrame++;
                // 초기 유도·자세 제어(§Guidance)는 **발사 0.27초 부근**이 정점이다 — 0.67초(아래)만 찍으면
                // 이미 전환이 끝나가 "그냥 날아가는 탄" 으로 보인다. 그 단계를 확인하려면 이 한 장이 필요하다.
                // 확인 방법: ./tools/unity_build.sh run -roster Missile,Missile,Missile
                if (_flyFrame == 8) Shot($"11b_초기유도_{_volley + 1}");
                if (_flyFrame == 20) Shot($"{12 + _volley * 2}_포탄비행_{_volley + 1}");
                if (_phase != Phase.Flying)     // 이 프레임에 착탄했다
                {
                    UpdateCamera(0f);           // 카메라를 착탄점으로 즉시 스냅
                    Shot($"{13 + _volley * 2}_착탄순간_{_volley + 1}");
                    _volley++;
                    _resolveFrame = _frame;
                    // 버섯구름은 착탄 **뒤**에 자란다(기둥 1.5s · 갓 0.6s 지연). 착탄순간 한 장만 찍으면 영영 안 보인다.
                    // ⚠️ 프레임이 아니라 **실제 시간**으로 재야 한다 — 파티클은 Time.deltaTime 으로 늙는데
                    //    자동사격의 비행은 1/30 고정 스텝이라 둘이 어긋난다(60fps 에서 30프레임 = 0.5초).
                    _nukeShotAt = _shooterUlt ? Time.time + 1.7f : 0f;
                }
                return;
            }

            switch (_frame)
            {
                case 15: _camYaw = 160f; _camPitch = 24f; _camDist = 40f; UpdateCamera(0f); break;
                case 20: Shot("11_전투개시"); break;
                case 25: AiShoot(); _flyFrame = 0; break;
            }

            // 핵이면 버섯구름이 다 자란 뒤 한 장 더 찍는다.
            if (_nukeShotAt > 0f && Time.time >= _nukeShotAt)
            {
                Shot($"13b_버섯구름_{_volley}");
                _nukeShotAt = 0f;
                _resolveFrame = _frame;          // 다음 사격을 이 시점부터 다시 센다
                return;
            }
            if (_nukeShotAt > 0f) { UpdateCamera(0.05f); TickFx(1f / 30f); return; }

            // 착탄 연출을 20프레임 보여준 뒤 다음 사격
            if (_volley > 0 && _frame == _resolveFrame + 20)
            {
                if (_volley < 3)
                {
                    _phase = Phase.Move;
                    AdvanceTurn();   // ⚠️ 여기도 같은 규칙. 교대로 돌리면 자동사격 스크린샷이 게임과 다른 순서로 찍힌다
                    AiShoot(); _flyFrame = 0;
                }
                else
                {
                    _camDist = 60f; _camPitch = 30f; UpdateCamera(0f);
                    Shot("18_지형파괴_결과");
                }
            }
            if (_volley >= 3 && _frame == _resolveFrame + 26)
            {
                Debug.Log($"[Tankfall] battle autoshot 완료 · 삼각형 {_terrain.TriangleCount} · 라운드 {_round}");
                VerifyShots();
                Application.Quit(0);
            }
            if (_phase == Phase.Resolve) { UpdateCamera(0.05f); TickFx(1f / 30f); }
        }

        /// <summary>스크린샷이 저장될 경로. `Shot` 과 UI 자체검사의 쌍 비교가 **같은 계산**을 쓰게 한다.</summary>
        string ShotPath(string name)
        {
            string dir = System.IO.Path.IsPathRooted(_shotDir)
                ? _shotDir
                : System.IO.Path.Combine(Application.dataPath, "..", _shotDir);
            return System.IO.Path.Combine(System.IO.Path.GetFullPath(dir), name + ".png");
        }

        void Shot(string name)
        {
            string path = ShotPath(name);
            try { System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)); }
            catch (System.Exception e) { Debug.LogError($"[Tankfall] 스크린샷 폴더 생성 실패 {path}: {e.Message}"); return; }

            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"[Tankfall] 스크린샷 {path} · {_log}");
            _shotPaths.Add(path);
        }

        readonly List<string> _shotPaths = new List<string>();

        /// <summary>
        /// 찍었다고 로그만 남기고 **쓸모없는 그림**이 남는 사고를 막는다.
        ///
        /// ⚠️ 2026-09-17 사고: 예전엔 여기서 `File.Exists` 만 셌다. `-nographics` 로 돌리면
        ///    렌더 타깃이 없어 PNG 가 **전부 새까맣게** 나오는데, 파일은 멀쩡히 생기므로
        ///    로그에는 `스크린샷 검증 8/8 확인` 이 그대로 찍혔다 — 완전한 거짓 통과였다.
        ///    "파일이 있다 ≠ 그림이 나왔다". 그래서 이제 **픽셀을 실제로 읽어** 판정한다:
        ///    (a) 파일이 있고 (b) 단색이 아니어야 통과. 빌드 스크립트의 -nographics 제거도 같은 사고의 수리다.
        /// </summary>
        void VerifyShots()
        {
            int ok = 0, blank = 0, missing = 0;
            foreach (var p in _shotPaths)
            {
                if (!System.IO.File.Exists(p)) { missing++; continue; }
                if (IsBlankPng(p)) { blank++; Debug.LogError($"[Tankfall] 스크린샷이 단색이다(렌더 실패 의심): {p}"); continue; }
                ok++;
            }
            if (ok == _shotPaths.Count) Debug.Log($"[Tankfall] 스크린샷 검증 {ok}/{_shotPaths.Count} 확인 (내용까지 검사)");
            else Debug.LogError($"[Tankfall] 스크린샷 실패 — 정상 {ok} · 단색 {blank} · 누락 {missing} / {_shotPaths.Count} · 경로 {_shotDir}");
        }

        /// <summary>PNG 를 읽어 단색인지 본다. 검은 화면·흰 화면을 "찍혔다"로 넘기지 않기 위한 네거티브 컨트롤.</summary>
        static bool IsBlankPng(string path)
        {
            try
            {
                var bytes = System.IO.File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(bytes)) { Destroy(tex); return true; }
                var px = tex.GetPixels32();
                Destroy(tex);
                if (px.Length == 0) return true;
                // 밝기 분산이 거의 없으면 단색이다. 하늘만 찍힌 화면도 HUD 가 있으면 여기를 통과한다.
                double sum = 0, sum2 = 0;
                int step = Mathf.Max(1, px.Length / 20000);          // 전 픽셀을 다 볼 필요는 없다
                int n = 0;
                for (int i = 0; i < px.Length; i += step)
                {
                    double v = (px[i].r * 0.299 + px[i].g * 0.587 + px[i].b * 0.114);
                    sum += v; sum2 += v * v; n++;
                }
                double mean = sum / n, var = sum2 / n - mean * mean;
                return var < 4.0;                                     // 표준편차 2 미만 = 사실상 단색
            }
            catch (System.Exception e) { Debug.LogError($"[Tankfall] 스크린샷 검사 실패 {path}: {e.Message}"); return true; }
        }
    }
}
