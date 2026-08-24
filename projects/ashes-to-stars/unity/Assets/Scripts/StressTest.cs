using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 재와 별 — W1 성능 검증 (§21 프로토타입 V1)
///
/// 검증 질문: 잡몹 500체 + 소환수 + 투사체가 동시에 도는 상태에서 60fps가 유지되는가.
///
/// 구현 방침 (기획서 §10-9 성능 예산에 적힌 대로):
///   - 오브젝트 풀링 — 런타임 Instantiate/Destroy 없음
///   - 단일 머티리얼 + 스프라이트 아틀라스로 배칭
///   - 충돌은 물리엔진이 아니라 **공간 분할 그리드**로 직접 판정 (전수 검사 금지)
///   - 몬스터 AI 4종(추적·포위·돌진·원거리)을 실제로 돌린다 — 빈 스프라이트만 띄우면 검증이 아니다
///
/// 단계적으로 부하를 올리며 각 구간의 FPS를 기록하고 CSV로 남긴다.
/// </summary>
public class StressTest : MonoBehaviour
{
    [Header("부하 단계 (잡몹 수)")]
    public int[] Steps = { 100, 200, 300, 400, 500, 700, 1000 };
    [Header("보기")] public float UnitScale = 2.2f;   // 쿼터뷰에서 유닛이 보이는 크기
    public float SecondsPerStep = 6f;
    public float WarmupSeconds = 2f;

    [Header("예산 (기획서 §10-9)")]
    public int SummonCount = 50;
    public int ProjectileBudget = 200;

    /// <summary>§10-9 투사체 풀 크기. BalanceConfig.투사체상한을 읽는다. QA_NO면 옛 200.</summary>
    public static int ProjectileHardCap => AshesToStars.ProjCap.Limit();

    [Header("월드")]
    public float WorldRadius = 26f;

    /// <summary>
    /// 쿼터뷰 투영 계수 (✅기획서 §17 — 2D 쿼터뷰).
    /// 시뮬레이션은 평면(x,y)에서 하고, 화면에 그릴 때만 세로를 눌러
    /// 30° 위에서 내려다보는 아이소메트릭 느낌을 만든다.
    /// sin(30°) = 0.5 — 블렌더에서 스프라이트를 렌더한 하강각과 같은 값이어야
    /// 스프라이트와 바닥의 원근이 어긋나지 않는다.
    /// </summary>
    public const float ISO_Y = 0.5f;

    static Vector3 ToScreen(Vector2 world, float z = 0f)
        => new Vector3(world.x, world.y * ISO_Y, z);

    // ── 유닛 데이터 (SoA: 캐시 효율을 위해 배열로 분리) ──────────
    const int MAX_UNITS = 1200;
    Transform[] _tr;
    SpriteRenderer[] _sr;
    Vector2[] _pos, _vel;
    int[] _ai;          // 0 추적 / 1 포위 / 2 돌진 / 3 원거리
    float[] _cd;        // 돌진·발사 쿨다운
    bool[] _alive;
    int _active;

    // ── 투사체 풀 ─────────────────────────────────────────
    Transform[] _pTr;
    Vector2[] _pPos, _pVel;
    float[] _pLife;
    bool[] _pAlive;
    int _pCursor;

    // ── 아군(리더 + 소환수) ────────────────────────────────
    Transform _leader;
    Vector2 _leaderPos;
    Transform[] _summonTr;
    Vector2[] _summonPos;

    // ── 공간 분할 그리드 ──────────────────────────────────
    const float CELL = 2.0f;
    const int GRID = 64;                       // 64x64 = 128m 커버
    readonly List<int>[] _cells = new List<int>[GRID * GRID];

    // ── 측정 ─────────────────────────────────────────────
    int _step = -1;
    float _stepT, _warmT;
    int _frames;
    float _accum, _worst;
    readonly StringBuilder _csv = new StringBuilder();
    string _outPath;

    void Awake()
    {
        Application.targetFrameRate = -1;      // vsync 끄고 최대치 측정
        QualitySettings.vSyncCount = 0;

        _outPath = Path.Combine(Application.persistentDataPath, "w1_result.csv");
        var argPath = GetArg("--out");
        if (!string.IsNullOrEmpty(argPath)) _outPath = argPath;

        _csv.AppendLine("mobs,summons,projectiles,avg_fps,min_fps,frame_ms_avg,frame_ms_worst,verdict");

        for (int i = 0; i < _cells.Length; i++) _cells[i] = new List<int>(8);

        BuildPools();
        NextStep();
    }

    static string GetArg(string key)
    {
        var a = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++) if (a[i] == key) return a[i + 1];
        return null;
    }

    // ── 풀 생성 ───────────────────────────────────────────
    void BuildPools()
    {
        var atlas = SpriteBank.Load();

        _tr = new Transform[MAX_UNITS];
        _sr = new SpriteRenderer[MAX_UNITS];
        _pos = new Vector2[MAX_UNITS];
        _vel = new Vector2[MAX_UNITS];
        _ai = new int[MAX_UNITS];
        _cd = new float[MAX_UNITS];
        _alive = new bool[MAX_UNITS];

        var mobRoot = new GameObject("Mobs").transform;
        for (int i = 0; i < MAX_UNITS; i++)
        {
            var go = new GameObject("m", typeof(SpriteRenderer));
            go.transform.SetParent(mobRoot, false);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = atlas.Mob(i % 3);
            sr.sharedMaterial = atlas.Mat;      // 단일 머티리얼 = 배칭
            go.transform.localScale = Vector3.one * UnitScale;
            go.SetActive(false);
            _tr[i] = go.transform;
            _sr[i] = sr;
        }

        // 상한만 ProjCap(BalanceConfig.투사체상한)을 읽는다. QA_NO면 옛 200.
        ProjectileBudget = ProjectileHardCap;
        _pTr = new Transform[ProjectileBudget];
        _pPos = new Vector2[ProjectileBudget];
        _pVel = new Vector2[ProjectileBudget];
        _pLife = new float[ProjectileBudget];
        _pAlive = new bool[ProjectileBudget];
        var pRoot = new GameObject("Projectiles").transform;
        for (int i = 0; i < ProjectileBudget; i++)
        {
            var go = new GameObject("p", typeof(SpriteRenderer));
            go.transform.SetParent(pRoot, false);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = atlas.Projectile;
            sr.sharedMaterial = atlas.Mat;
            go.transform.localScale = Vector3.one * (UnitScale * 0.45f);
            go.SetActive(false);
            _pTr[i] = go.transform;
        }

        var lgo = new GameObject("Leader", typeof(SpriteRenderer));
        var lsr = lgo.GetComponent<SpriteRenderer>();
        lsr.sprite = atlas.Player;
        lsr.sharedMaterial = atlas.Mat;
        lsr.sortingOrder = 100;
        lgo.transform.localScale = Vector3.one * (UnitScale * 1.4f);
        _leader = lgo.transform;

        _summonTr = new Transform[SummonCount];
        _summonPos = new Vector2[SummonCount];
        var sRoot = new GameObject("Summons").transform;
        for (int i = 0; i < SummonCount; i++)
        {
            var go = new GameObject("s", typeof(SpriteRenderer));
            go.transform.SetParent(sRoot, false);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = atlas.Summon;
            sr.sharedMaterial = atlas.Mat;
            go.transform.localScale = Vector3.one * (UnitScale * 0.7f);
            _summonTr[i] = go.transform;
            float a = Mathf.PI * 2f * i / SummonCount;
            _summonPos[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.5f;
        }

        GroundBuilder.Build(atlas, WorldRadius);
    }

    void NextStep()
    {
        _step++;
        if (_step >= Steps.Length) { Finish(); return; }

        int n = Steps[_step];
        for (int i = 0; i < MAX_UNITS; i++)
        {
            bool on = i < n;
            _alive[i] = on;
            _tr[i].gameObject.SetActive(on);
            if (!on) continue;
            float ang = Random.value * Mathf.PI * 2f;
            float rad = Mathf.Lerp(6f, WorldRadius, Mathf.Sqrt(Random.value));
            _pos[i] = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad;
            _ai[i] = (i % 10 < 4) ? 0 : (i % 10 < 7) ? 1 : (i % 10 < 8) ? 2 : 3;  // 40/30/10/20%
            _cd[i] = Random.value * 2f;
            _sr[i].sprite = SpriteBank.Cached.Mob(_ai[i] == 3 ? 2 : _ai[i] == 1 ? 1 : 0);
        }
        _active = n;
        _stepT = 0f; _warmT = 0f; _frames = 0; _accum = 0f; _worst = 0f;
        Debug.Log($"[W1] 단계 {_step + 1}/{Steps.Length} — 잡몹 {n} + 소환수 {SummonCount} + 투사체 최대 {ProjectileBudget}");
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // 리더는 원을 그리며 이동 — 몹이 항상 추적 상태를 유지하게
        float t = Time.time * 0.35f;
        _leaderPos = new Vector2(Mathf.Cos(t), Mathf.Sin(t * 1.3f)) * 7f;
        _leader.position = ToScreen(_leaderPos);
        for (int i = 0; i < SummonCount; i++)
        {
            float a = Mathf.PI * 2f * i / SummonCount + Time.time * 0.8f;
            _summonTr[i].position = ToScreen(_leaderPos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.5f);
        }

        RebuildGrid();
        TickUnits(dt);
        TickProjectiles(dt);
        Measure(dt);
    }

    // ── 공간 분할: 매 프레임 셀 재구성 (O(n)) ────────────────
    void RebuildGrid()
    {
        for (int i = 0; i < _cells.Length; i++) _cells[i].Clear();
        for (int i = 0; i < _active; i++)
        {
            if (!_alive[i]) continue;
            _cells[CellIndex(_pos[i])].Add(i);
        }
    }

    static int CellIndex(Vector2 p)
    {
        int cx = Mathf.Clamp((int)((p.x + GRID * CELL * 0.5f) / CELL), 0, GRID - 1);
        int cy = Mathf.Clamp((int)((p.y + GRID * CELL * 0.5f) / CELL), 0, GRID - 1);
        return cy * GRID + cx;
    }

    void TickUnits(float dt)
    {
        const float SEP = 0.55f;          // 서로 밀어내는 거리
        const float SEP2 = SEP * SEP;

        for (int i = 0; i < _active; i++)
        {
            if (!_alive[i]) continue;
            Vector2 p = _pos[i];
            Vector2 toLeader = _leaderPos - p;
            float dist = toLeader.magnitude + 1e-4f;
            Vector2 dir = toLeader / dist;
            Vector2 desired;
            float speed;

            switch (_ai[i])
            {
                case 1: // 포위형 — 접선 방향으로 감싸며 접근
                    {
                        Vector2 tangent = new Vector2(-dir.y, dir.x);
                        float w = Mathf.Clamp01((dist - 3f) / 6f);
                        desired = (dir * w + tangent * (1f - w)).normalized;
                        speed = 2.4f;
                        break;
                    }
                case 2: // 돌진형 — 쿨다운마다 가속 돌진
                    {
                        _cd[i] -= dt;
                        if (_cd[i] <= 0f) { _cd[i] = 3.5f; _vel[i] = dir * 9f; }
                        desired = dir;
                        speed = 1.6f;
                        break;
                    }
                case 3: // 원거리형 — 거리 유지하며 투사체 발사
                    {
                        float keep = 7f;
                        desired = dist > keep + 1f ? dir : (dist < keep - 1f ? -dir : new Vector2(-dir.y, dir.x));
                        speed = 2.0f;
                        _cd[i] -= dt;
                        if (_cd[i] <= 0f && dist < 14f) { _cd[i] = 2.5f; Fire(p, dir); }
                        break;
                    }
                default: // 추적형
                    desired = dir;
                    speed = 2.8f;
                    break;
            }

            // 이웃 분리 — 그리드에서 3x3 셀만 검사 (전수 검사 대비 수백 배 저렴)
            Vector2 sep = Vector2.zero;
            int ci = CellIndex(p);
            int cx = ci % GRID, cy = ci / GRID;
            for (int oy = -1; oy <= 1; oy++)
            {
                int yy = cy + oy; if (yy < 0 || yy >= GRID) continue;
                for (int ox = -1; ox <= 1; ox++)
                {
                    int xx = cx + ox; if (xx < 0 || xx >= GRID) continue;
                    var list = _cells[yy * GRID + xx];
                    for (int k = 0; k < list.Count; k++)
                    {
                        int j = list[k]; if (j == i) continue;
                        Vector2 d = p - _pos[j];
                        float m2 = d.sqrMagnitude;
                        if (m2 > 1e-6f && m2 < SEP2) sep += d / m2;
                    }
                }
            }

            Vector2 accel = desired * speed + sep * 0.9f;
            _vel[i] = Vector2.Lerp(_vel[i], accel, 1f - Mathf.Exp(-6f * dt));
            p += _vel[i] * dt;

            float r = p.magnitude;
            if (r > WorldRadius * 1.4f) p = p / r * (WorldRadius * 1.4f);

            _pos[i] = p;
            var tr = _tr[i];
            // 쿼터뷰: y가 낮을수록 앞 → 정렬 순서에 반영
            tr.position = ToScreen(p);
            _sr[i].sortingOrder = (int)(-p.y * 32f);
        }
    }

    void Fire(Vector2 from, Vector2 dir)
    {
        for (int n = 0; n < ProjectileBudget; n++)
        {
            int i = _pCursor; _pCursor = (_pCursor + 1) % ProjectileBudget;
            if (_pAlive[i]) continue;
            _pAlive[i] = true;
            _pPos[i] = from;
            _pVel[i] = dir * 6.5f;      // 느린 탄속 (§10-2: 회피 가능해야 함)
            _pLife[i] = 3f;
            _pTr[i].gameObject.SetActive(true);
            return;
        }
    }

    void TickProjectiles(float dt)
    {
        for (int i = 0; i < ProjectileBudget; i++)
        {
            if (!_pAlive[i]) continue;
            _pLife[i] -= dt;
            if (_pLife[i] <= 0f)
            {
                _pAlive[i] = false;
                _pTr[i].gameObject.SetActive(false);
                continue;
            }
            _pPos[i] += _pVel[i] * dt;
            _pTr[i].position = ToScreen(_pPos[i]);
        }
    }

    int LiveProjectiles()
    {
        int c = 0;
        for (int i = 0; i < ProjectileBudget; i++) if (_pAlive[i]) c++;
        return c;
    }

    // ── 측정·기록 ─────────────────────────────────────────
    void Measure(float dt)
    {
        if (_warmT < WarmupSeconds) { _warmT += dt; return; }   // 워밍업 구간은 제외

        _frames++;
        _accum += dt;
        if (dt > _worst) _worst = dt;
        _stepT += dt;

        if (_stepT >= SecondsPerStep)
        {
            float avgMs = (_accum / _frames) * 1000f;
            float avgFps = _frames / _accum;
            float minFps = 1f / _worst;
            string verdict = avgFps >= 60f ? "PASS" : (avgFps >= 45f ? "MARGINAL" : "FAIL");

            var ci = CultureInfo.InvariantCulture;
            _csv.AppendLine(string.Join(",",
                Steps[_step].ToString(ci), SummonCount.ToString(ci), LiveProjectiles().ToString(ci),
                avgFps.ToString("F1", ci), minFps.ToString("F1", ci),
                avgMs.ToString("F2", ci), (_worst * 1000f).ToString("F2", ci), verdict));

            Debug.Log($"[W1] 잡몹 {Steps[_step]} → 평균 {avgFps:F1}fps / 최저 {minFps:F1}fps / 프레임 {avgMs:F2}ms — {verdict}");

            // 눈으로도 확인할 수 있게 단계별 스크린샷 (수치만으로는 "정말 다 그려졌나"를 못 본다)
            var shotDir = Path.GetDirectoryName(_outPath);
            ScreenCapture.CaptureScreenshot(Path.Combine(shotDir, $"w1_{Steps[_step]}mobs.png"));

            NextStep();
        }
    }

    void Finish()
    {
        File.WriteAllText(_outPath, _csv.ToString());
        Debug.Log($"[W1] 완료 — 결과 저장: {_outPath}");
        Debug.Log("[W1] ===RESULT===\n" + _csv.ToString());
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
