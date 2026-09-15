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

        /// <summary>접지 시각 보정(m). Surface Nets 정점은 셀 안 교차점 평균이라
        /// 렌더 표면이 SDF 0면보다 살짝 안쪽이다 — 그대로 앉히면 궤도가 지형에 잠겨 보인다.
        /// 물리(이동·충돌)에는 쓰지 않는다. 순수 시각 보정이다.</summary>
        const float GroundVisualLift = 0.18f;

        // --- 조작 ---
        const float DriveSpeed = 8f, TurnSpeed = 70f;
        const float TurretSpeed = 70f, BarrelSpeed = 35f;
        const float MinElev = -5f, MaxElev = 80f;
        const float ChargeRate = 0.75f;      // 파워 게이지 왕복 속도

        enum Phase { Aim, Flying, Resolve, AiThink, GameOver }

        sealed class Unit
        {
            public int Id, Team, Hp = MaxHp;
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
        Phase _phase = Phase.Aim;
        float _power, _chargeDir = 1f;
        bool _charging;
        Vector2 _wind;                   // 수평 방향 × 세기

        /// <summary>
        /// 기본 난이도. 자동 대전 40판 실측으로 고른 값 — 3% 여야 명중률 ≈50%,
        /// 한 판 ≈14분으로 기획서 §55(8~15분) 안에 들어온다. 6% 면 40분짜리 게임이 된다.
        /// </summary>
        const float AiDifficulty = AiGunner.ErrorNormal;
        Rng _aiRng = new Rng(0xA17C0DEu);   // 결정론 — UnityEngine.Random 은 리플레이를 깬다
        int _round = 1;
        string _log = "";
        float _camYaw = 180f, _camPitch = 20f, _camDist = 26f;
        float _phaseTimer;
        List<Vec3> _shotPath;
        float _shotT;
        Transform _shell;
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

        static float Height(float x, float z)
        {
            float h = 4f;
            h += 22f * Mathf.Exp(-(((x - 60f) * (x - 60f) + (z - 100f) * (z - 100f)) / 900f));
            h += 20f * Mathf.Exp(-(((x - 145f) * (x - 145f) + (z - 105f) * (z - 105f)) / 800f));
            h += 2.5f * Mathf.Sin(x * 0.06f) * Mathf.Cos(z * 0.05f);
            return h;
        }

        Unit Current => _units.Count == 0 ? null : _units[Mathf.Clamp(_turn, 0, _units.Count - 1)];
        bool IsPlayerTurn => Current != null && Current.Team == 0;

        void Start()
        {
            Application.targetFrameRate = 60;
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-autoshot") _autoShot = true;
                else if (args[i] == "-perf") _perf = true;
                else if (args[i] == "-timeevents") _timeEvents = true;
                else if (args[i] == "-shotdir" && i + 1 < args.Length) _shotDir = args[i + 1];
            }
            if (_autoShot && string.IsNullOrEmpty(_shotDir)) _shotDir = "Screenshots";

            SetupWorld();
            _vol = new SdfVolume(Voxel, ChunkN, OriginY, Height, Mathf.RoundToInt(MapSize / Voxel));

            var go = new GameObject("Terrain");
            _terrain = go.AddComponent<TerrainView>();
            _terrain.Init(_vol, ChunkN, MakeMat(new Color(0.46f, 0.44f, 0.36f), 0.05f));
            int cells = Mathf.RoundToInt(MapSize / Voxel);
            _terrain.BuildRegion(0, cells / ChunkN, _vol.GridY(-2f) / ChunkN, _vol.GridY(40f) / ChunkN + 1,
                                 0, cells / ChunkN);

            SpawnTeams();
            RollWind();
            _log = "전투 개시 — 아군(파랑) 3 vs 적군(빨강) 3";
        }

        void SpawnTeams()
        {
            var blue = MakeMat(new Color(0.28f, 0.45f, 0.72f), 0.15f);
            var red = MakeMat(new Color(0.72f, 0.28f, 0.26f), 0.15f);
            var track = MakeMat(new Color(0.15f, 0.15f, 0.17f), 0f);

            var shapes = new[] { TankShape.Balanced, TankShape.Mortar, TankShape.Sniper };
            for (int t = 0; t < 2; t++)
                for (int i = 0; i < 3; i++)
                {
                    float x = t == 0 ? 55f + i * 14f : 150f + i * 12f;
                    float z = t == 0 ? 40f + i * 8f : 155f - i * 9f;
                    var root = ProceduralTank.Build(shapes[i], t == 0 ? blue : red, track,
                                                    out var tur, out var bar, out var fp);
                    var u = new Unit { Id = t * 3 + i, Team = t, Root = root, Turret = tur, Barrel = bar, Fire = fp };
                    float g = TankGroundProbe.GroundBelow(_vol, x, z, 60f);
                    root.position = new Vector3(x, (float.IsNegativeInfinity(g) ? 10f : g) + GroundVisualLift, z);
                    // 서로 마주보게
                    u.Heading = t == 0 ? Mathf.Atan2(150f - x, 155f - z) * Mathf.Rad2Deg
                                       : Mathf.Atan2(60f - x, 45f - z) * Mathf.Rad2Deg;
                    root.rotation = Quaternion.Euler(0, u.Heading, 0);
                    _units.Add(u);
                }
            // 턴 순서: 팀 교차 A1 B1 A2 B2 A3 B3 (§52)
            _units.Sort((a, b) => (a.Id % 3) * 2 + a.Team - ((b.Id % 3) * 2 + b.Team));
            _turn = 0;
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
        void RollWind()
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            float s = Random.Range(0f, 10f);
            _wind = new Vector2(Mathf.Cos(a) * s, Mathf.Sin(a) * s);
        }

        void Update()
        {
            if (_units.Count == 0) return;                 // Start 가 실패한 경우
            if (_perf) { PerfStep(); return; }
            if (_autoShot) { AutoShotStep(); return; }
            float dt = Time.deltaTime;

            switch (_phase)
            {
                case Phase.Aim:
                    if (IsPlayerTurn) PlayerAim(dt); else { _phase = Phase.AiThink; _phaseTimer = 0.9f; }
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
        }

        // ---------------- 플레이어 ----------------

        void PlayerAim(float dt)
        {
            var u = Current;

            // 이동 (게이지 소비)
            float turn = (Input.GetKey(KeyCode.A) ? -1f : 0f) + (Input.GetKey(KeyCode.D) ? 1f : 0f);
            float fwd = (Input.GetKey(KeyCode.W) ? 1f : 0f) + (Input.GetKey(KeyCode.S) ? -1f : 0f);
            u.Heading += turn * TurnSpeed * dt;
            if (Mathf.Abs(fwd) > 0.01f && u.Gauge > 0f) DriveUnit(u, fwd, dt);
            GroundUnit(u, dt);

            // 조준
            u.TurretYaw += ((Input.GetKey(KeyCode.LeftArrow) ? -1f : 0f) + (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)) * TurretSpeed * dt;
            u.BarrelPitch = Mathf.Clamp(u.BarrelPitch + ((Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) + (Input.GetKey(KeyCode.DownArrow) ? -1f : 0f)) * BarrelSpeed * dt, MinElev, MaxElev);
            ApplyAim(u);

            // 파워 차징 — 누르는 동안 0↔100 왕복, 놓으면 발사(§11)
            if (Input.GetKeyDown(KeyCode.Space)) { _charging = true; _power = 0f; _chargeDir = 1f; }
            if (_charging)
            {
                _power += _chargeDir * ChargeRate * dt;
                if (_power >= 1f) { _power = 1f; _chargeDir = -1f; }
                if (_power <= 0f) { _power = 0f; _chargeDir = 1f; }
                if (Input.GetKeyUp(KeyCode.Space)) { _charging = false; FireFrom(u, u.TurretYaw, u.BarrelPitch, _power); }
            }
        }

        void DriveUnit(Unit u, float fwd, float dt)
        {
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
                u.Gauge -= Mathf.Abs(each) * 2.2f;             // 대략 45m 이동 가능
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
                    enemies.Add(new AiGunner.Target { Id = o.Id, Center = o.Center });
            if (enemies.Count == 0) { NextTurn(); return; }

            var from = new Vec3(u.Fire.position.x, u.Fire.position.y, u.Fire.position.z);
            var swA = _timeEvents ? System.Diagnostics.Stopwatch.StartNew() : null;
            var plan = AiGunner.Decide(_vol, from, enemies, new Vec3(_wind.x, 0f, _wind.y),
                                       AiDifficulty, ref _aiRng, MapSize, MinElev, MaxElev);
            if (swA != null)
                Debug.Log($"[Tankfall] EVENT AI조준 {swA.Elapsed.TotalMilliseconds:F2} ms " +
                          $"(적 {enemies.Count}, 파워 {plan.Power * 100:F0}, 각 {plan.PitchDeg:F1}°)");
            if (!plan.Valid) { NextTurn(); return; }

            u.TurretYaw = Mathf.DeltaAngle(u.Heading, plan.YawDeg);
            u.BarrelPitch = Mathf.Clamp(plan.PitchDeg, MinElev, MaxElev);
            ApplyAim(u);
            FireFrom(u, u.TurretYaw, u.BarrelPitch, Mathf.Clamp01(plan.Power));
        }

        // ---------------- 발사 · 비행 ----------------

        void FireFrom(Unit u, float turretYaw, float pitch, float power)
        {
            float worldYaw = u.Heading + turretYaw;
            float speed = Ballistics.PowerToSpeed(power);
            var p0 = new Vec3(u.Fire.position.x, u.Fire.position.y, u.Fire.position.z);
            var v0 = Ballistics.VelocityFrom(worldYaw, pitch, speed);
            var accel = Ballistics.Accel(_wind.x, _wind.y);

            var boxes = new List<TankHitbox>();
            foreach (var o in _units) if (o.Alive) boxes.Add(new TankHitbox { Id = o.Id, Center = o.Center, Radius = TankRadius });

            var res = ProjectileSimulator.Simulate(_vol, p0, v0, accel, boxes, u.Id, MapSize);
            _shotPath = res.Path; _shotT = 0f;
            _pendingImpact = res.Impact; _pendingDirect = res.DirectHitTankId;

            if (_shell == null)
            {
                var sg = new GameObject("Shell");
                sg.AddComponent<MeshFilter>().sharedMesh = ProceduralTank.Ball(0.45f);
                sg.AddComponent<MeshRenderer>().sharedMaterial = MakeMat(new Color(0.10f, 0.10f, 0.12f), 0.4f);
                _shell = sg.transform;
            }
            _shell.gameObject.SetActive(res.Hit || res.Path.Count > 1);
            _phase = Phase.Flying;
            _log = $"{(u.Team == 0 ? "아군" : "적군")} 발사 — 각 {pitch:F0}° 파워 {power * 100:F0} 바람 {_wind.magnitude:F1}";
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
        }

        void Impact()
        {
            if (_shell != null) _shell.gameObject.SetActive(false);
            _shotPath = null;

            var c = _pendingImpact;
            if (c.LengthSq > 0.001f)
            {
                // 지형 파괴 → 천장 붕괴(§7-6-1) → 영향 청크만 재생성
                var swB = _timeEvents ? System.Diagnostics.Stopwatch.StartNew() : null;
                var blast = SdfDeformer.SubtractSphere(_vol, new BlastRequest(c.X, c.Y, c.Z, BlastRadius));
                var collapse = CeilingCollapse.Apply(_vol, blast);
                float sdfMs = swB != null ? (float)swB.Elapsed.TotalMilliseconds : 0f;
                if (swB != null) _terrain.ResetTicks();
                _terrain.ApplyDirty(CeilingCollapse.Union(blast, collapse));
                if (swB != null)
                    Debug.Log($"[Tankfall] EVENT 착탄처리 SDF {sdfMs:F2} + 메시 " +
                              $"{(float)swB.Elapsed.TotalMilliseconds - sdfMs:F2} = {swB.Elapsed.TotalMilliseconds:F2} ms " +
                              $"[업로드 {TerrainView.Ms(_terrain.UploadTicks):F2} · 노멀 {TerrainView.Ms(_terrain.NormalTicks):F2} " +
                              $"· 경계 {TerrainView.Ms(_terrain.BoundsTicks):F2}]");

                // 피해(§6)
                string dmgLog = "";
                foreach (var o in _units)
                {
                    if (!o.Alive) continue;
                    float dist = (o.Center - c).Length;
                    bool direct = o.Id == _pendingDirect;
                    if (dist > BlastRadius && !direct) continue;
                    int dmg = Damage.Compute(dist, BlastRadius, BaseDamage, DirectDamage, direct);
                    if (dmg <= 0) continue;
                    o.Hp = Mathf.Max(0, o.Hp - dmg);
                    dmgLog += $"  {(o.Team == 0 ? "아군" : "적군")}{o.Id % 3 + 1} −{dmg}{(direct ? "(직격)" : "")}";
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
            int tick = Mathf.Max(1, Mathf.RoundToInt(MaxHp * SuddenDeathPct));
            int downed = 0;
            foreach (var u in _units)
                if (u.Alive)
                {
                    u.Hp = Mathf.Max(0, u.Hp - tick);
                    if (!u.Alive) downed++;
                }
            _log = $"☠ 서든데스 {_round}라운드 — 전원 −{tick}" + (downed > 0 ? $" ({downed}대 격파)" : "");
        }

        void NextTurn()
        {
            ApplySuddenDeath();

            int aliveA = 0, aliveB = 0;
            foreach (var u in _units) { if (!u.Alive) continue; if (u.Team == 0) aliveA++; else aliveB++; }
            if (aliveA == 0 || aliveB == 0)
            {
                _winner = aliveA > 0 ? "아군 승리" : "적군 승리";
                _phase = Phase.GameOver;
                return;
            }

            int guard = 0;
            do { _turn = (_turn + 1) % _units.Count; if (_turn == 0) { _round++; RollWind(); } }
            while (!_units[_turn].Alive && ++guard < 20);

            Current.Gauge = MoveGaugeMax;
            _power = 0f; _charging = false;
            _phase = Phase.Aim;
        }

        // ---------------- 카메라 · HUD ----------------

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
            Vector3 focus =
                _phase == Phase.Flying && _shell != null && _shell.gameObject.activeSelf
                    ? _shell.position
                    : _phase == Phase.Resolve && _hasImpactFocus
                        ? _impactFocus
                        : Current.Pos + Vector3.up * 2.2f;
            var rot = Quaternion.Euler(_camPitch, _camYaw, 0);
            var want = focus + rot * Vector3.back * _camDist;
            _cam.transform.position = dt > 0f ? Vector3.Lerp(_cam.transform.position, want, 1f - Mathf.Exp(-9f * dt)) : want;
            _cam.transform.LookAt(focus);
        }

        void OnGUI()
        {
            if (_hudOff) return;
            var st = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
            GUI.Box(new Rect(8, 8, 560, 196), "");
            GUILayout.BeginArea(new Rect(16, 14, 546, 188));

            var u = Current;
            if (u == null) { GUI.Label(new Rect(20, 20, 600, 30), "초기화 실패 — 로그 확인"); return; }
            string wd = WindArrow(_wind);
            GUILayout.Label($"<b>TANKFALL</b>  라운드 {_round}  ·  바람 {wd} {_wind.magnitude:F1}  ·  {1f / Mathf.Max(Time.smoothDeltaTime, 1e-5f):F0} fps", st);

            string a = "", b = "";
            foreach (var x in _units)
            {
                string cell = x.Alive ? $"{x.Hp,4}" : "  ☠ ";
                if (x == u) cell = $"<b>[{cell.Trim()}]</b>";
                if (x.Team == 0) a += cell + " "; else b += cell + " ";
            }
            GUILayout.Label($"<color=#7fb0ff>아군 {a}</color>   <color=#ff8a80>적군 {b}</color>", st);
            GUILayout.Label($"차례: <b>{(u.Team == 0 ? "아군" : "적군")}{u.Id % 3 + 1}</b>   각도 {u.BarrelPitch:F0}°   포탑 {u.TurretYaw:F0}°   이동 {Mathf.Max(0, u.Gauge):F0}", st);

            if (_phase == Phase.Aim && IsPlayerTurn)
            {
                int bars = Mathf.RoundToInt(_power * 20f);
                GUILayout.Label($"POWER [{new string('█', bars)}{new string('·', 20 - bars)}] {_power * 100:F0}", st);
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
        Vector3 _impactFocus; bool _hasImpactFocus;

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

        // ---------------- 자동 스크린샷 ----------------

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
                    _phase = Phase.Aim;
                    do { _turn = (_turn + 1) % _units.Count; } while (!Current.Alive);
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
            if (_phase == Phase.Resolve) UpdateCamera(0.05f);
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
