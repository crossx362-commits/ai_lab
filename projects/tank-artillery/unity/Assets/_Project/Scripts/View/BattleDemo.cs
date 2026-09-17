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
    public sealed class BattleDemo : MonoBehaviour
    {
        // --- 지형 (§7-1) ---
        const float MapSize = 200f, Voxel = 0.5f, OriginY = -20f;
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
            public Vector3 Pos => Root.position;
            public Vec3 Center => new Vec3(Root.position.x, Root.position.y + 1.2f, Root.position.z);
        }

        SdfVolume _vol;
        TerrainView _terrain;
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
        string _niceFlash;        // "나이스샷!" 표시
        readonly StatusEffects _status = new StatusEffects();   // 독·화상·속박
        readonly HazardField _hazards = new HazardField();      // 지뢰·지속불
        readonly List<(Vec3 impact, int direct, float scale)> _pendingShots = new List<(Vec3, int, float)>();
        TankKind _shooterKind; ShellKind _shooterShell;
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
        Material _mineMat, _fireMat, _cloudMat;
        float _beamTimer;
        TankKind[] _roster = DefaultRoster;   // -roster Cannon,Carrot,Laser 로 바꿀 수 있다(연출 확인용)
        MapKind _map = MapKind.TwinHills;
        bool _forceSpecial;                    // -forcespecial: AI 가 항상 2번탄 — 위성탄·독구름 같은 연출을 확인할 때만
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
                if (args[i] == "-autoshot") _autoShot = true;
                else if (args[i] == "-perf") _perf = true;
                else if (args[i] == "-timeevents") _timeEvents = true;
                else if (args[i] == "-phasecheck") _phaseCheck = true;
                else if (args[i] == "-gallery") _gallery = true;
                else if (args[i] == "-forcespecial") _forceSpecial = true;
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
                else if (args[i] == "-practice") _practice = true;
                else if (args[i] == "-practiceselftest") { _practice = true; _practiceSelfTest = true; }
                else if (args[i] == "-supplyselftest") _supplySelfTest = true;
                else if (args[i] == "-ultselftest") _ultSelfTest = true;
                else if (args[i] == "-impairselftest") _impairSelfTest = true;
                else if (args[i] == "-climateselftest") _climateSelfTest = true;
                else if (args[i] == "-boom") _boom = true;                     // Boom 모드(§2-9-16)
                else if (args[i] == "-boomselftest") { _boom = true; _boomSelfTest = true; }
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
                    if (list.Count == 3) _roster = list.ToArray();
                    else Debug.LogWarning($"[Tankfall] -roster 는 정확히 3종이어야 한다(받은 것 {list.Count}) — 기본 로스터 사용");
                }
            }
            // ⚠️ 예전엔 이 기본값이 `_autoShot` 일 때만 걸렸다. -gallery 를 추가하니 폴더가 null 이라
            //    Shot() 이 매 프레임 ArgumentNullException 을 던지고 사진은 한 장도 안 남았다.
            //    **찍는 모드가 늘 때마다 재발할 조건**이라 조건 자체를 없앤다.
            if (string.IsNullOrEmpty(_shotDir)) _shotDir = "Screenshots";

            SetupWorld();
            _vol = new SdfVolume(Voxel, ChunkN, OriginY, Height, Mathf.RoundToInt(MapSize / Voxel),
                                MapHeightFunction.Grad(_map));

            var go = new GameObject("Terrain");
            _terrain = go.AddComponent<TerrainView>();
            _terrain.Init(_vol, ChunkN, MakeMat(new Color(0.46f, 0.44f, 0.36f), 0.05f));
            int cells = Mathf.RoundToInt(MapSize / Voxel);
            _terrain.BuildRegion(0, cells / ChunkN, _vol.GridY(-2f) / ChunkN, _vol.GridY(40f) / ChunkN + 1,
                                 0, cells / ChunkN);

            // ⚠️ 날씨는 **스폰보다 먼저** 정한다(유닛이 생성될 때 `W` 사본을 뜬다). 바람과 달리 판 내내 안 바뀐다 —
            //    "이번 판은 눈"이 보이면 포세이돈을 고를지가 선택지가 되기 때문이다(성장 없는 PvP, §62).
            SetWeather(_weatherForced ?? (Random.value < SnowChance ? Weather.Snow : Weather.Clear));
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
            _log = $"전투 개시 — {MapHeightFunction.Name(_map)} · {WeatherName(_weather)} · AI {Difficulties[_difficulty].Name} · {TankStats.Get(_roster[0]).Name}·{TankStats.Get(_roster[1]).Name}·{TankStats.Get(_roster[2]).Name}  (파랑 vs 빨강)";
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
            // 턴 순서: 팀 교차 A1 B1 A2 B2 A3 B3 (§52)
            _units.Sort((a, b) => (a.Id % 3) * 2 + a.Team - ((b.Id % 3) * 2 + b.Team));
            _status.Clear(); _hazards.Clear(); _pendingShots.Clear(); RefreshHazards();
            _supply.Clear(); RefreshCrates();
            _ult.Clear();
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
            _turn = 0;   // 전원 누적 0 → 첫 등록(아군1)이 먼저. TurnOrder.Next 와 일치한다
        }

        void SetupWorld()
        {
            var camGo = new GameObject("MainCamera");
            _cam = camGo.AddComponent<Camera>();
            _cam.tag = "MainCamera";
            _cam.farClipPlane = 800f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.55f, 0.66f, 0.78f);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(46f, 35f, 0f);
            sun.intensity = 1.15f; sun.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.62f, 0.72f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.42f, 0.40f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.20f, 0.18f);
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
        }

        void Update()
        {
            if (_units.Count == 0) return;                 // Start 가 실패한 경우
            if (_gallery) { GalleryStep(); return; }
            if (_perf) { PerfStep(); return; }
            if (_supplySelfTest) { SupplySelfTestStep(); return; }
            if (_ultSelfTest) { UltSelfTestStep(); return; }
            if (_impairSelfTest) { ImpairSelfTestStep(); return; }
            if (_climateSelfTest) { ClimateSelfTestStep(); return; }
            if (_boomSelfTest) { BoomSelfTestStep(); return; }
            if (_autoShot) { AutoShotStep(); return; }
            float dt = Time.deltaTime;
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
                    if (!IsPlayerTurn) { _phase = Phase.AiThink; _phaseTimer = 0.9f; break; }
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
        void PlayerMove(float dt)
        {
            var u = Current;
            float turn = (Input.GetKey(KeyCode.A) ? -1f : 0f) + (Input.GetKey(KeyCode.D) ? 1f : 0f);
            float fwd = (Input.GetKey(KeyCode.W) ? 1f : 0f) + (Input.GetKey(KeyCode.S) ? -1f : 0f);
            u.Heading += turn * TurnSpeed * dt;
            if (Mathf.Abs(fwd) > 0.01f && u.Gauge > 0f) DriveUnit(u, fwd, dt);
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
            if (Input.GetKeyDown(KeyCode.Alpha1)) { u.Shell = ShellKind.Normal; _useSs = false; }
            // 원작: 2번탄은 무한. 예전 "3발 제한 + 2라운드 해금"은 내 발명이었고 걷어냈다(§2-9).
            if (Input.GetKeyDown(KeyCode.Alpha2)) { u.Shell = ShellKind.Special; _useSs = false; }
            // SS = 나이스샷 2점. 없는데 누르면 아무 일도 없다(HUD 가 이유를 보여준다)
            if (Input.GetKeyDown(KeyCode.Alpha3) && u.Skill.CanSs()) { u.Shell = ShellKind.Special; _useSs = true; }
            if (Input.GetKeyDown(KeyCode.R)) FireUltimate(u);   // 궁극기(§2-9-12). 1·2·3 은 탄종, [ ] 는 아이템이 이미 쓴다
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
                    if (NiceShot.Judge(_mark, _power)) { u.Skill.OnNiceShot(); _ult.OnNiceShot(u.Id); _niceFlash = $"나이스샷! 포인트 {u.Skill.Points}"; }
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
                    if (!u.Alive) { u.Root.gameObject.SetActive(false); }
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
                    enemies.Add(new AiGunner.Target { Id = o.Id, Center = o.Center, Defense = o.St.Defense });
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
            float worldYaw = u.Heading + turretYaw;
            var baseSt = TankStats.For(u.Kind, ShellKind.Normal, u.HpFrac, u.W);
            float speed = baseSt.SpeedAt(power);
            var p0 = new Vec3(u.Fire.position.x, u.Fire.position.y, u.Fire.position.z);
            var accel = baseSt.AccelWith(_wind.x, _wind.y);
            var boxes = new List<TankHitbox>();
            foreach (var o in _units) if (o.Alive) boxes.Add(new TankHitbox { Id = o.Id, Center = o.Center, Radius = TankRadius });

            // 1) 기준 탄도(패턴 중앙)
            var res = ProjectileSimulator.Simulate(_vol, p0, Ballistics.VelocityFrom(worldYaw, pitch, speed), accel, boxes, u.Id, MapSize, _air);

            // 2) 탄종. 플레이어는 FIRE 페이즈에서 골랐고, AI 는 착탄점을 본 뒤 고른다(§8) — 탄도가 같으니 조준을 다시 풀 필요가 없다.
            var shell = u.Shell;
            bool ss = IsPlayerTurn && _useSs;
            if (!IsPlayerTurn && res.Hit)
            {
                var foes = new List<AiGunner.Target>();
                foreach (var o in _units)
                    if (o.Alive && o.Team != u.Team) foes.Add(new AiGunner.Target { Id = o.Id, Center = o.Center, Defense = o.St.Defense });
                Vec3? satImpact = null; int satDirect = -1;
                if (ShellEffects.Of(u.Kind, ShellKind.Special).Type == ShellEffects.EffectType.SatelliteStrike)
                    satImpact = SatelliteStrike.Resolve(_vol, res.Impact.X, res.Impact.Z, res.Impact.Y + 60f, boxes, out satDirect);
                var normalHits = AiGunner.SimulatePattern(_vol, p0, worldYaw, pitch, speed, accel, boxes, u.Id, MapSize,
                                                          Spread.Pattern(u.Kind, ShellKind.Normal), res, _air);
                var specialHits = satImpact.HasValue ? null
                    : AiGunner.SimulatePattern(_vol, p0, worldYaw, pitch, speed, accel, boxes, u.Id, MapSize,
                                               Spread.Pattern(u.Kind, ShellKind.Special), res, _air);
                shell = AiGunner.PickShell(baseSt, normalHits, specialHits, foes, satImpact, satDirect);
                ss = shell == ShellKind.Special && u.Skill.CanSs();
                // AI 나이스샷 — 게이지 정지 정밀도를 난이도별 확률로 대신한다 [추정]
                if (NiceShot.AiJudge(AiDifficulty, ref _aiRng)) { u.Skill.OnNiceShot(); _ult.OnNiceShot(u.Id); }
            }
            // 연출 확인용 강제(-forcespecial) — AI 뿐 아니라 자동사격의 플레이어 탱크(ForceFire 경로)에도 걸어야 아군 사격에서도 보인다
            if (_forceSpecial) shell = ShellKind.Special;
            var st = TankStats.For(u.Kind, shell, u.HpFrac, u.W);
            if (shell == ShellKind.Special && ss && u.Skill.CanSs()) { st = NiceShot.ApplySs(st); u.Skill.SpendSs(); }
            // 파워업: 원작 분류가 "능력 아이템(공격력 강화)". ⚠️ 속도를 올리면 이미 끝낸 조준이 통째로 빗나간다.
            if (_items.HasPowerUp(u.Id)) { st.BaseDamage *= Items.PowerUpScale; st.DirectDamage *= Items.PowerUpScale; }
            // 궁극기(§2-9-12): 과충전·지진은 이번 사격 수치를 바꾸고, 연사는 발수를 늘린다.
            var ultKind = _ult.Armed(u.Id) ? Ultimate.Of(u.Kind) : UltimateKind.None;
            if (ultKind != UltimateKind.None) st = Ultimate.ApplyToShot(st, ultKind);
            _shooterStats = st; _shooterKind = u.Kind; _shooterShell = shell;
            _shooterId = u.Id;
            // ⚠️ 쏜 뒤 일반탄으로 되돌린다. 안 그러면 다음 턴에 "고른 적 없는 2번탄"이 나가는 것처럼 보인다.
            u.Shell = ShellKind.Normal; _useSs = false;

            // 3) 다탄두: 고른 탄종의 패턴대로 전부 계산하고, 부탄은 각자 궤적으로 같이 날린다(카메라는 중앙 탄).
            var pattern = Spread.Pattern(u.Kind, shell);
            _pendingShots.Clear();
            _subPaths.Clear();
            foreach (var pt in pattern)
            {
                bool center = pt.YawOffsetDeg == 0f && pt.PitchOffsetDeg == 0f;
                var sub = pattern.Count == 1 || center ? res
                    : ProjectileSimulator.Simulate(_vol, p0, Ballistics.VelocityFrom(worldYaw + pt.YawOffsetDeg, pitch + pt.PitchOffsetDeg, speed), accel, boxes, u.Id, MapSize, _air);
                // 기후(§2-9-15): 증폭벽을 지난 탄은 피해 1.5배(원작). 부탄마다 따로 판정한다.
                if (sub.Hit) _pendingShots.Add((sub.Impact, sub.DirectHitTankId, pt.DamageScale * sub.DamageScale));
                if (sub.DamageScale > 1f) _log += "  [증폭벽]";
                if (sub.Tornadoed) _log += "  [회오리]";
                if (!center && sub.Path != null && sub.Path.Count > 1) _subPaths.Add(sub.Path);
            }
            while (_subShells.Count < _subPaths.Count)
            {
                var sg2 = new GameObject($"SubShell{_subShells.Count}");
                sg2.AddComponent<MeshFilter>().sharedMesh = ProceduralTank.Ball(0.32f);
                sg2.AddComponent<MeshRenderer>().sharedMaterial = MakeMat(new Color(0.10f, 0.10f, 0.12f), 0.4f);
                _subShells.Add(sg2.transform);
            }
            for (int si = 0; si < _subShells.Count; si++) _subShells[si].gameObject.SetActive(si < _subPaths.Count);

            _shotPath = res.Path; _shotT = 0f;
            _pendingImpact = res.Impact; _pendingDirect = res.DirectHitTankId;

            if (_shell == null)
            {
                var sg = new GameObject("Shell");
                sg.AddComponent<MeshFilter>().sharedMesh = ProceduralTank.Ball(0.45f);
                sg.AddComponent<MeshRenderer>().sharedMaterial = MakeMat(new Color(0.10f, 0.10f, 0.12f), 0.4f);
                _shell = sg.transform;
            }
            // 더블파이어: 같은 각도·파워를 기억해 착탄 뒤 한 발 더 쏜다(원작 "같은 힘과 각도").
            if (_items.HasDoubleFire(u.Id) && !_pendingDoubleFire)
            { _pendingDoubleFire = true; _dfYaw = turretYaw; _dfPitch = pitch; _dfPower = power; }
            // 궁극 연사: 같은 각도·파워로 남은 발을 이어 쏜다.
            if (ultKind == UltimateKind.Volley && _pendingVolley == 0)
            { _pendingVolley = Ultimate.VolleyShots - 1; _vYaw = turretYaw; _vPitch = pitch; _vPower = power; }
            _shell.gameObject.SetActive(res.Hit || res.Path.Count > 1);
            _phase = Phase.Flying;
            _log = $"{(u.Team == 0 ? "아군" : "적군")} 발사 — 각 {pitch:F0}° 파워 {power * 100:F0} 바람 {_wind.magnitude:F1}"
                 + (shell == ShellKind.Special ? $"  [{st.Name}]" : "") + (pattern.Count > 1 ? $" ×{pattern.Count}" : "");
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
            // 부탄: 같은 시각(_shotT)의 자기 궤적 위치. 먼저 떨어진 부탄은 숨긴다(착탄 처리는 중앙 탄 착탄 때 한꺼번에).
            for (int si = 0; si < _subPaths.Count; si++)
            {
                var sp = _subPaths[si]; var tr = _subShells[si];
                if (i >= sp.Count - 1) { tr.gameObject.SetActive(false); continue; }
                var sa = sp[i]; var sb = sp[i + 1];
                tr.position = new Vector3(Mathf.Lerp(sa.X, sb.X, f), Mathf.Lerp(sa.Y, sb.Y, f), Mathf.Lerp(sa.Z, sb.Z, f));
            }
        }

        /// <summary>
        /// 설치물 스냅샷을 화면에 맞춘다. 지뢰는 공, 장판(지속불·독구름)은 **지면을 따라가는 구슬 고리**.
        /// ⚠️ 처음엔 장판을 얇은 원반 하나로 그렸는데 크레이터가 사발 모양이라 중앙 높이의 원반은 사발 벽 아래로 들어가
        ///    화면에 아무것도 안 보였다(스크린샷으로 확인). 고리 구슬은 점마다 지면을 재서 놓으니 경사·사발 어디서나 보인다.
        /// </summary>
        void RefreshHazards()
        {
            _hazards.Snapshot(_hazardBuf);
            if (_mineMat == null)
            {
                _mineMat = MakeMat(new Color(0.12f, 0.12f, 0.12f), 0.5f);
                _fireMat = MakeMat(new Color(1.0f, 0.45f, 0.10f), 0.2f);
                _cloudMat = MakeMat(new Color(0.45f, 0.90f, 0.30f), 0.2f);
            }
            const int RingN = 12;
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
            foreach (var h in _hazardBuf)
            {
                if (h.Kind == 0)
                {
                    var tr = Take();
                    tr.GetComponent<MeshRenderer>().sharedMaterial = _mineMat;
                    tr.position = new Vector3(h.X, GroundAt(h.X, h.Z, h.Y) + 0.5f, h.Z);
                    tr.localScale = Vector3.one * 0.6f;
                    continue;
                }
                var mat = h.Kind == 2 ? _cloudMat : _fireMat;
                for (int k = 0; k < RingN; k++)
                {
                    float a = k * (Mathf.PI * 2f / RingN);
                    float x = h.X + Mathf.Cos(a) * h.Radius, z = h.Z + Mathf.Sin(a) * h.Radius;
                    var tr = Take();
                    tr.GetComponent<MeshRenderer>().sharedMaterial = mat;
                    tr.position = new Vector3(x, GroundAt(x, z, h.Y) + 0.4f, z);
                    tr.localScale = Vector3.one * 0.45f;
                }
                // 중앙 표식(남은 턴 수만큼 크기 — 3턴 0.9, 1턴 0.5)
                var c = Take();
                c.GetComponent<MeshRenderer>().sharedMaterial = mat;
                c.position = new Vector3(h.X, GroundAt(h.X, h.Z, h.Y) + 0.6f, h.Z);
                c.localScale = Vector3.one * (0.3f + 0.2f * h.TurnsLeft);
            }
            for (int i = used; i < _hazardGos.Count; i++) _hazardGos[i].gameObject.SetActive(false);
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
            if (_beam == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = "SatelliteBeam";
                Destroy(go.GetComponent<Collider>());
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = MakeMat(new Color(0.55f, 0.95f, 1.0f), 0.9f);
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // 60m 기둥 그림자가 맵을 가로지른다(스크린샷으로 확인)
                _beam = go.transform;
            }
            const float H = 60f;
            _beam.position = new Vector3(at.X, at.Y + H * 0.5f, at.Z);
            _beam.localScale = new Vector3(0.9f, H * 0.5f, 0.9f);   // 기본 실린더 높이 2 → y 배율 = 높이/2
            _beam.gameObject.SetActive(true);
            _beamTimer = 0.9f;
        }

        void Impact()
        {
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
                var fx = ShellEffects.Of(_shooterKind, _shooterShell);
                var boxes = new List<TankHitbox>();
                foreach (var o in _units) if (o.Alive) boxes.Add(new TankHitbox { Id = o.Id, Center = o.Center, Radius = TankRadius });
                float craterEach = _shooterStats.CraterRadius * (_pendingShots.Count > 1 ? 0.65f : 1f);   // 다탄두 발당 굴착 [추정]
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
                    var blast = SdfDeformer.SubtractSphere(_vol, new BlastRequest(impact.X, impact.Y, impact.Z, craterEach));
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
                        if (_items.ConsumeShield(o.Id)) { dmgLog += $"  [{(o.Team == 0 ? "아군" : "적군")}{o.Id % 3 + 1} 실드]"; continue; }
                        // 벙커(§2-9-12): 피해 적용 **직전**에 통과시킨다.
                        int rawDmg = dmg;
                        dmg = _ult.Mitigate(o.Id, dmg);
                        if (dmg < rawDmg) dmgLog += "  [벙커]";
                        o.Hp = Mathf.Max(0, o.Hp - dmg);
                        if (o.Team != Current.Team) _ult.OnDamageDealt(Current.Id, dmg);
                        _ult.OnDamageTaken(o.Id, dmg);
                        dmgLog += $"  {(o.Team == 0 ? "아군" : "적군")}{o.Id % 3 + 1} −{dmg}{(direct ? "(직격)" : "")}";
                        if (fx.Type == ShellEffects.EffectType.Poison) { _status.Poison(o.Id, fx.Param1, fx.Param2, o.Kind); dmgLog += "[독]"; }
                        if (fx.Type == ShellEffects.EffectType.Root) { _status.Root(o.Id, fx.Param1); dmgLog += "[속박]"; }
                        // 방해탄(§2-9-14): 맞은 적에게 건다.
                        var imk = _items.ImpairShot(Current.Id);
                        if (imk != ImpairKind.None && o.Team != Current.Team)
                        { _impair.Apply(o.Id, imk); dmgLog += $"[{Impair.Name(imk)}]"; }
                        if (!o.Alive) { o.Root.gameObject.SetActive(false); dmgLog += "☠"; }
                    }
                    // 자리에 남는 효과
                    // 눈이면 불·독가스는 아예 안 남는다(원작 규칙 — SetWeather 머리말의 출처 참조).
                    bool snowKillsField = _weather == Weather.Snow;
                    if (fx.Type == ShellEffects.EffectType.Burn)
                    { if (snowKillsField) dmgLog += "  [지속불 — 눈에 꺼짐]"; else { _hazards.PlaceFire(impact.X, impact.Y, impact.Z, _shooterStats.BlastRadius, fx.Param1, fx.Param2); dmgLog += "  [지속불]"; } }
                    if (fx.Type == ShellEffects.EffectType.PoisonCloud)
                    { if (snowKillsField) dmgLog += "  [독구름 — 눈에 꺼짐]"; else { _hazards.PlaceFire(impact.X, impact.Y, impact.Z, _shooterStats.BlastRadius, fx.Param1, fx.Param2, 1); dmgLog += "  [독구름]"; } }
                    if (fx.Type == ShellEffects.EffectType.Mine) { _hazards.PlaceMine(impact.X, impact.Y, impact.Z, 4f, fx.Param1, -1); dmgLog += "  [지뢰 설치]"; }
                }
                if (swB != null) Debug.Log($"[Tankfall] EVENT 착탄처리 ×{_pendingShots.Count} {swB.Elapsed.TotalMilliseconds:F2} ms");
                _pendingShots.Clear();
                RefreshHazards();
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
                    dmgLog += $"  {(o.Team == 0 ? "아군" : "적군")}{o.Id % 3 + 1} −{fall}(낙하 {op.y - GroundVisualLift - g:F0}m)";
                    if (!o.Alive) { o.Root.gameObject.SetActive(false); dmgLog += "☠"; }
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

        void ApplySuddenDeath()
        {
            if (_round <= SuddenDeathRound) return;
            int downed = 0;
            foreach (var u in _units)
                if (u.Alive)
                {
                    u.Hp = Mathf.Max(0, u.Hp - Mathf.Max(1, Mathf.RoundToInt(u.St.Hp * SuddenDeathPct)));
                    if (!u.Alive) downed++;
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
            // 궁극 연사(§2-9-12): 남은 발을 이어 쏜다. 턴은 넘기지 않는다.
            if (_pendingVolley > 0)
            {
                var vu = _units.Find(x => x.Id == _shooterId);
                if (vu != null && vu.Alive)
                {
                    _pendingVolley--;
                    _log = $"[궁극] 연사 — 남은 {_pendingVolley + 1}발";
                    FireFrom(vu, _vYaw + (_pendingVolley - 1) * Ultimate.VolleySpreadDeg, _vPitch, _vPower);
                    return;
                }
                _pendingVolley = 0;
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
            if (_shooterId >= 0) { _items.ClearShotFlags(_shooterId); _ult.ClearShotFlags(_shooterId); }

            ApplySuddenDeath();

            int aliveA = 0, aliveB = 0;
            foreach (var u in _units) { if (!u.Alive) continue; if (u.Team == 0) aliveA++; else aliveB++; }
            if (aliveA == 0 || aliveB == 0)
            {
                _winner = aliveA > 0 ? "아군 승리" : "적군 승리";
                _phase = Phase.GameOver;
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
                if (dot + hz <= 0) break;
                cu.Hp = Mathf.Max(0, cu.Hp - dot - hz);
                _log = $"{(cu.Team == 0 ? "아군" : "적군")}{cu.Id % 3 + 1} 턴 시작 피해{(dot > 0 ? $" 지속 −{dot}" : "")}{(hz > 0 ? $" 설치물 −{hz}" : "")}";
                if (cu.Alive) break;
                cu.Root.gameObject.SetActive(false);
            }
            _niceFlash = null;
            // 라운드 = 행동 수 / 유닛 수. 서든데스가 이 값을 본다
            int round = _order.ActionsTaken / _units.Count + 1;
            if (round != _round) { _round = round; RollWind(); RollSupply(); }

            TryPickupSupply(Current);        // 상자 위에 서 있으면 이동 없이도 줍는다
            _items.TickStartOfTurn(Current.Id);
            _ult.TickStartOfTurn(Current.Id);
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
            _log = $"[아이템] {info.Name} — {info.Desc}";
            // 로그로도 남긴다 — HUD 문자열만 쓰면 자동 검증에서 아이템이 도는지 확인할 방법이 없다.
            Debug.Log($"[Tankfall] 아이템 {(u.Team == 0 ? "아군" : "적군")}{u.Id % 3 + 1} {info.Name}");
            if (_itemSel > 0) _itemSel--;
            // 턴을 먹는 아이템은 사격 없이 턴을 넘긴다.
            if (r == ItemState.UseResult.AppliedEndsTurn && !info.AppliesToShot) NextTurn();
        }

        /// <summary>
        /// 궁극기(§2-9-12) 발동. 게이지가 가득 찼을 때만. 즉발형(벙커·정화)은 여기서 바로 끝나고,
        /// 사격형(과충전·연사·지진)은 다음 사격에 실린다.
        /// </summary>
        void FireUltimate(Unit u)
        {
            if (!_ult.Ready(u.Id)) return;
            var kind = Ultimate.Of(u.Kind);
            bool ok = _ult.Fire(u.Id, u.Team, u.Kind,
                (id, f) => { var t = _units.Find(x => x.Id == id); if (t != null) t.Hp = Mathf.Min(t.HpMax, t.Hp + (int)(t.HpMax * f)); },
                (tm, f) => { foreach (var t in _units) if (t.Alive && t.Team == tm) t.Hp = Mathf.Min(t.HpMax, t.Hp + (int)(t.HpMax * f)); },
                id => _status.Cleanse(id));
            if (!ok) return;
            var info = Ultimate.Get(kind);
            _log = $"[궁극] {(u.Team == 0 ? "아군" : "적군")}{u.Id % 3 + 1} {info.Name} — {info.Desc}";
            Debug.Log($"[Tankfall] 궁극기 {(u.Team == 0 ? "아군" : "적군")}{u.Id % 3 + 1} {TankStats.Get(u.Kind).Name} {info.Name}");
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
            FireUltimate(u);   // 궁극기도 하네스와 같은 정책 [추정] — 차면 바로 쓴다
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
                if (!Ballistics.SolveLaunchAngles(from, target.Center, speed, accel, out var low, out var high)) continue;
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
                        Ballistics.VelocityFrom(cand.YawDeg, cand.PitchDeg, speed), accel, boxes, me.Id, MapSize);
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
                    dmg = _ult.Mitigate(o.Id, dmg);
                    o.Hp = Mathf.Max(0, o.Hp - dmg);
                    _ult.OnDamageTaken(o.Id, dmg);
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
        }

        void OnGUI()
        {
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
            if (_hudOff) return;
            var st = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
            GUI.Box(new Rect(8, 8, 560, 196), "");
            GUILayout.BeginArea(new Rect(16, 14, 546, 188));

            var u = Current;
            if (u == null) { GUI.Label(new Rect(20, 20, 600, 30), "초기화 실패 — 로그 확인"); return; }
            string wd = WindArrow(_wind);
            string dl = _order != null && Current != null ? $"  ·  딜레이 {_order.Accumulated(Current.Id)}" : "";
            if (_practice)
            {
                float acc = _practiceShots > 0 ? _practiceHits * 100f / _practiceShots : 0f;
                string bestTxt = _practiceBestMiss < float.MaxValue ? $"{_practiceBestMiss:F1}m" : "-";
                string lastTxt = _practiceLastMiss >= 0f ? $"{_practiceLastMiss:F1}m" : "-";
                GUILayout.Label($"<b>TANKFALL 연습장</b>  {WeatherName(_weather)}  ·  바람 {wd} {_wind.magnitude:F1}  ·  " +
                                $"{_practiceHits}/{_practiceShots}발 ({acc:F0}%)  ·  이번 {lastTxt} / 최고 {bestTxt}  ·  F2=정답 보기", st);
                if (!string.IsNullOrEmpty(_practiceAnswer)) GUILayout.Label($"<b>{_practiceAnswer}</b>", st);
            }
            else
            GUILayout.Label($"<b>TANKFALL</b>  라운드 {_round}  ·  {WeatherName(_weather)}  ·  바람 {wd} {_wind.magnitude:F1}{dl}  ·  AI {Difficulties[_difficulty].Name}(F1){(_supply.Count > 0 ? $"  ·  보급상자 {_supply.Count}" : "")}  ·  {1f / Mathf.Max(Time.smoothDeltaTime, 1e-5f):F0} fps", st);

            string a = "", b = "";
            foreach (var x in _units)
            {
                string cell = x.Alive ? $"{x.Hp,4}" : "  ☠ ";
                if (x == u) cell = $"<b>[{cell.Trim()}]</b>";
                if (x.Team == 0) a += cell + " "; else b += cell + " ";
            }
            GUILayout.Label($"<color=#7fb0ff>아군 {a}</color>   <color=#ff8a80>적군 {b}</color>", st);
            GUILayout.Label($"차례: <b>{(u.Team == 0 ? "아군" : "적군")}{u.Id % 3 + 1}</b>   각도 <b>{u.BarrelPitch:F0}°</b>   포탑 {u.TurretYaw:F0}°   이동 {Mathf.Max(0, u.Gauge):F0}"
                            + (_items.HasShield(u.Id) ? "   <b>[실드]</b>" : ""), st);

            // 아이템 가방 — [ ] 로 고르고 Enter 로 사용(숫자키는 탄종이 쓴다)
            if (_itemSlots > 0 && u.Team == 0)
            {
                var bag = _items.Bag(u.Id);
                if (bag.Count == 0) GUILayout.Label("아이템: 없음", st);
                else
                {
                    string line = "아이템([ ]선택, Enter사용): ";
                    for (int bi = 0; bi < bag.Count; bi++)
                    {
                        var info = Items.Get(bag[bi]);
                        line += bi == _itemSel ? $"<b>▶{info.Name}</b>  " : $"{info.Name}  ";
                    }
                    if (_itemSel < bag.Count) line += $"— {Items.Get(bag[_itemSel]).Desc}";
                    GUILayout.Label(line, st);
                }
            }

            // 페이즈 + 남은 시간(§2-1). 타이머가 안 보이면 2페이즈 턴은 규칙이 아니라 그냥 불편함이다.
            if (IsPlayerTurn && (_phase == Phase.Move || _phase == Phase.Fire))
            {
                bool move = _phase == Phase.Move;
                float left = Mathf.Max(0f, _phaseTimer);
                string col = left <= 3f ? "#ff6b6b" : move ? "#9ad1ff" : "#ffd479";
                GUILayout.Label($"<color={col}><b>{(move ? "MOVE" : "FIRE")}</b>  {left:F1}초</color>" +
                                (move ? "   <size=12>스페이스/우클릭 = 조준 진입(되돌릴 수 없음)</size>" : ""), st);
            }

            // 무기 슬롯(§8). 지금 뭐가 장전됐는지 안 보이면 선택지가 아니라 사고다.
            if (IsPlayerTurn && _phase == Phase.Fire)
            {
                var sp = TankStats.For(u.Kind, ShellKind.Special, u.HpFrac, u.W);
                string n1 = u.Shell == ShellKind.Normal ? "<b>[1 일반탄]</b>" : "<size=12>1 일반탄</size>";
                string n2 = u.Shell == ShellKind.Special && !_useSs ? $"<b>[2 {sp.Name}]</b>" : $"<size=12>2 {sp.Name}</size>";
                string n3 = u.Skill.CanSs()
                    ? (_useSs ? $"<b>[3 SS {sp.Name}]</b>" : "<size=12>3 <color=#ff8>SS 준비</color></size>")
                    : $"<size=12><color=#777>3 SS — 나이스샷 {u.Skill.Points}/{NiceShot.SsCost}</color></size>";
                GUILayout.Label($"{n1}   {n2}   {n3}   <size=12>폭발 {u.St.BlastRadius:F1}m · 굴착 {u.St.CraterRadius:F1}m · 직격 +{u.St.DirectDamage:F0}</size>", st);
                // 궁극기(§2-9-12) — 게이지와 이 기종의 궁극기 이름.
                var uinfo = Ultimate.Get(Ultimate.Of(u.Kind));
                float ug = _ult.Gauge(u.Id);
                int ubars = Mathf.Clamp(Mathf.RoundToInt(ug / Ultimate.Full * 10f), 0, 10);
                var ub = new System.Text.StringBuilder();
                for (int i = 0; i < 10; i++) ub.Append(i < ubars ? '█' : '·');
                // 방해탄(§2-9-14) — 나에게 걸린 것.
                var imb = new System.Text.StringBuilder();
                foreach (ImpairKind ik in System.Enum.GetValues(typeof(ImpairKind)))
                {
                    if (ik == ImpairKind.None) continue;
                    int left = _impair.TurnsLeft(u.Id, ik);
                    if (left > 0) imb.Append($"  <color=#f88>{Impair.Name(ik)} {left}턴</color>");
                }
                if (imb.Length > 0) GUILayout.Label($"<size=12>방해:{imb}</size>", st);
                GUILayout.Label(_ult.Ready(u.Id)
                    ? $"<color=#ff8><b>[R 궁극 — {uinfo.Name}] 준비!</b></color>   <size=12>{uinfo.Desc}</size>"
                    : $"<size=12><color=#999>R 궁극 {uinfo.Name} [{ub}] {ug:F0}/{Ultimate.Full:F0}</color></size>", st);
            }

            // §2-6 해소 — **거리는 준다, 탄착점은 안 준다.**
            // 기획서 §58 은 예상 탄착점을 금지하고 §60 은 "저 거리면 45도 파워 65" 학습을 원한다.
            // 거리를 숨기면 그 학습이 성립하지 않는다(눈대중으로 150m 와 170m 를 못 가른다).
            // 거리·고도차는 포병이 관측으로 얻는 값이고, 그걸로 각·파워를 **스스로 고르는 것**이 실력이다.
            {
                Unit near = null; float best = float.MaxValue;
                foreach (var o in _units)
                    if (o.Alive && o.Team != u.Team)
                    {
                        float d = (o.Pos - u.Pos).sqrMagnitude;
                        if (d < best) { best = d; near = o; }
                    }
                if (near != null)
                {
                    Vector3 d3 = near.Pos - u.Pos;
                    float horiz = new Vector2(d3.x, d3.z).magnitude;
                    GUILayout.Label($"<size=12>최근접 적 {(near.Team == 0 ? "아군" : "적군")}{near.Id % 3 + 1} — " +
                                    $"거리 <b>{horiz:F0}m</b>  고도차 <b>{d3.y:+0;-0;0}m</b>  (탄착 예측선 없음 §58)</size>", st);
                }
            }

            if (_phase == Phase.Fire && IsPlayerTurn)
            {
                int bars = Mathf.RoundToInt(_power * 20f), mk = Mathf.Clamp(Mathf.RoundToInt(_mark * 19f), 0, 19);
                var gb = new System.Text.StringBuilder();
                for (int i = 0; i < 20; i++) gb.Append(i == mk ? '┃' : i < bars ? '█' : '·');   // ┃ = 나이스샷 표시점
                GUILayout.Label($"POWER [{gb}] {_power * 100:F0}   <size=11>표시점 {_mark * 100:F0} (Q/E)</size>"
                                + (_niceFlash != null ? $"   <color=#8f8><b>{_niceFlash}</b></color>" : ""), st);
            }
            else GUILayout.Label(_phase == Phase.AiThink ? "적군 조준 중…" : _phase == Phase.Flying ? "포탄 비행 중…" : " ", st);

            GUILayout.Label(_log, st);
            GUILayout.Label("<b>WASD</b> 이동  <b>←→↑↓</b> 포탑·포신  <b>Space</b> 길게눌러 파워→놓으면 발사  <b>우클릭</b> 카메라", st);
            GUILayout.EndArea();

            if (_phase == Phase.GameOver)
            {
                var big = new GUIStyle(GUI.skin.label) { fontSize = 40, alignment = TextAnchor.MiddleCenter, richText = true };
                GUI.Label(new Rect(0, Screen.height * 0.4f, Screen.width, 60), $"<b>{_winner}</b>", big);
            }
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
        void RollSupply()
        {
            if (_itemSlots == 0) return;
            var anchors = new List<Vec3>();
            foreach (var u in _units) if (u.Alive) { var p = u.Pos; anchors.Add(new Vec3(p.x, p.y, p.z)); }
            if (anchors.Count == 0) return;
            int n = _supply.RollDrop(ref _supplyRng, MapSize, (x, z) => TankGroundProbe.GroundBelow(_vol, x, z, 80f), anchors);
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
                    _supply.RollDrop(ref _supplyRng, MapSize, (x, z) => TankGroundProbe.GroundBelow(_vol, x, z, 80f), anchors);
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
                    _supply.RollDrop(ref _supplyRng, MapSize, (x, z) => TankGroundProbe.GroundBelow(_vol, x, z, 80f), anchors);
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
        /// `-ultselftest` — 궁극기(§2-9-12)가 **게임에서** 도는지 스스로 확인한다.
        /// 하네스는 이미 12종 전부 판당 0.9~2.7회 발동을 보여주지만, 그건 하네스다.
        /// 여기서는 실제 빌드의 유닛·상태·피해 경로로 5종 전부를 한 번씩 재고, 하나라도 어긋나면 rc=1.
        /// ⚠️ 게이지는 손으로 넣지 않는다 — **진짜 피해 경로**(OnDamageTaken)로 채워야 충전이 검증된다.
        /// </summary>
        void UltSelfTestStep()
        {
            int fail = 0, done = 0;
            foreach (TankKind k in System.Enum.GetValues(typeof(TankKind)))
            {
                var kind = Ultimate.Of(k);
                var info = Ultimate.Get(kind);
                var probe = new UltimateState();
                int id = 0;

                // ① 충전: 피해를 20씩 넣어 게이지가 차는가(만충 전엔 발동이 거부돼야 한다)
                if (probe.Fire(id, 0, k, null, null, null)) { Debug.Log($"[Tankfall] ❌ {info.Name}: 게이지 0 인데 발동됐다"); fail++; }
                for (int i = 0; i < 200 && !probe.Ready(id); i++) probe.OnDamageTaken(id, 20);
                if (!probe.Ready(id)) { Debug.Log($"[Tankfall] ❌ {info.Name}: 피해를 넣어도 게이지가 안 찬다"); fail++; continue; }

                // ② 발동과 효과
                int healed = 0, teamHealed = 0, cleansed = 0;
                bool ok = probe.Fire(id, 0, k, (_, f) => healed++, (_, f) => teamHealed++, _ => cleansed++);
                if (!ok) { Debug.Log($"[Tankfall] ❌ {info.Name}: 만충인데 발동 실패"); fail++; continue; }
                if (probe.Gauge(id) != 0f) { Debug.Log($"[Tankfall] ❌ {info.Name}: 발동 후 게이지가 안 비었다"); fail++; }

                switch (kind)
                {
                    case UltimateKind.Bunker:
                        if (healed == 0) { Debug.Log($"[Tankfall] ❌ 벙커가 회복을 안 했다"); fail++; }
                        if (probe.Mitigate(id, 100) >= 100) { Debug.Log($"[Tankfall] ❌ 벙커가 피해를 안 줄였다"); fail++; }
                        break;
                    case UltimateKind.Purge:
                        if (cleansed == 0 || teamHealed == 0) { Debug.Log($"[Tankfall] ❌ 정화가 해제/팀회복을 안 했다"); fail++; }
                        break;
                    default:
                        if (!probe.Armed(id)) { Debug.Log($"[Tankfall] ❌ {info.Name}: 사격에 실리지 않았다"); fail++; }
                        var baseSt = TankStats.For(k, ShellKind.Normal, 1f, Weather.Clear);
                        var buffed = Ultimate.ApplyToShot(baseSt, kind);
                        if (kind == UltimateKind.Overcharge && buffed.DirectDamage <= baseSt.DirectDamage)
                        { Debug.Log($"[Tankfall] ❌ 과충전이 피해를 안 올렸다"); fail++; }
                        if (kind == UltimateKind.Quake && buffed.CraterRadius <= baseSt.CraterRadius)
                        { Debug.Log($"[Tankfall] ❌ 지진이 굴착을 안 키웠다"); fail++; }
                        break;
                }

                // ③ 사격 플래그가 남으면 영구 버프가 된다
                probe.ClearShotFlags(id);
                if (probe.Armed(id)) { Debug.Log($"[Tankfall] ❌ {info.Name}: 사격 플래그가 안 지워졌다"); fail++; }

                Debug.Log($"[Tankfall] 궁극 자체검사 {TankStats.Get(k).Name,-10} {info.Name} 확인");
                done++;
            }
            if (fail == 0 && done >= 13)
                Debug.Log($"[Tankfall] ✅ 궁극기(§2-9-12) — 13종 {done}개 전부 충전·발동·효과·해제 확인");
            else
                Debug.Log($"[Tankfall] ❌ 궁극 자체검사 실패 {fail}건 (확인 {done}/13)");
            Application.Quit(fail == 0 && done >= 13 ? 0 : 1);
        }

        bool _supplySelfTest;
        int _supplyTestBag0 = -1;
        bool _supplyTestPicked;

        // ── 아이템(§2-9-10) ── 게임과 하네스가 같은 Sim/ItemState 를 쓴다.
        readonly ItemState _items = new ItemState();
        // ── 궁극기(§2-9-12) ── 게임과 하네스가 같은 Sim/UltimateState 를 쓴다.
        readonly UltimateState _ult = new UltimateState();
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
        int _pendingVolley;          // 궁극 연사로 더 쏠 발수
        float _vYaw, _vPitch, _vPower;
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
                root.position = new Vector3(100f + (i - 6) * GalGap, 0f, GalZ);
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
            var focus = new Vector3(centerX, 1.2f, GalZ);
            var rot = Quaternion.Euler(camPitch, 0f, 0f);     // 야우 0 고정 = 줄에 수직
            _cam.transform.position = focus + rot * Vector3.back * dist;
            _cam.transform.LookAt(focus);
            Shot(name);
        }

        int _flyFrame, _volley, _resolveFrame;

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
                if (_flyFrame == 20) Shot($"{12 + _volley * 2}_포탄비행_{_volley + 1}");
                if (_phase != Phase.Flying)     // 이 프레임에 착탄했다
                {
                    UpdateCamera(0f);           // 카메라를 착탄점으로 즉시 스냅
                    Shot($"{13 + _volley * 2}_착탄순간_{_volley + 1}");
                    _volley++;
                    _resolveFrame = _frame;
                }
                return;
            }

            switch (_frame)
            {
                case 15: _camYaw = 160f; _camPitch = 24f; _camDist = 40f; UpdateCamera(0f); break;
                case 20: Shot("11_전투개시"); break;
                case 25: AiShoot(); _flyFrame = 0; break;
            }

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

        void Shot(string name)
        {
            string dir = System.IO.Path.IsPathRooted(_shotDir)
                ? _shotDir
                : System.IO.Path.Combine(Application.dataPath, "..", _shotDir);
            dir = System.IO.Path.GetFullPath(dir);
            try { System.IO.Directory.CreateDirectory(dir); }
            catch (System.Exception e) { Debug.LogError($"[Tankfall] 스크린샷 폴더 생성 실패 {dir}: {e.Message}"); return; }

            string path = System.IO.Path.Combine(dir, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"[Tankfall] 스크린샷 {path} · {_log}");
            _shotPaths.Add(path);
        }

        readonly List<string> _shotPaths = new List<string>();

        /// <summary>찍었다고 로그만 남기고 파일이 없는 사고를 막는다 — 종료 직전에 실물을 센다.</summary>
        void VerifyShots()
        {
            int ok = 0;
            foreach (var p in _shotPaths) if (System.IO.File.Exists(p)) ok++;
            if (ok == _shotPaths.Count) Debug.Log($"[Tankfall] 스크린샷 검증 {ok}/{_shotPaths.Count} 확인");
            else Debug.LogError($"[Tankfall] 스크린샷 누락 {ok}/{_shotPaths.Count} — 경로: {_shotDir}");
        }
    }
}
