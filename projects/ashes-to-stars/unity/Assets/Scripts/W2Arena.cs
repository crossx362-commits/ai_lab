using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 재와 별 — W2 조작감 검증 (§21 프로토타입 V2)
///
/// 검증 질문: 대시·구르기(무적 0.3초 / 쿨 6초)로 탄막 회피의 손맛이 나오는가.
///   성공 기준(§21): 테스터가 "방금 피했다"고 인지하는 순간이 **판당 3회 이상**.
///
/// "손맛"은 주관이라 그대로는 못 잰다. 그래서 이렇게 객관화했다:
///   **무적 프레임이 실제 피격을 흡수한 횟수(absorb)** = "방금 피했다"의 순간.
///   무적이 없었으면 맞았을 판정이 무적 때문에 무효가 된 경우만 센다.
///   이 수치가 판당 3 이상이면 기준 충족, 0에 가까우면 무적 창이 너무 짧거나
///   탄막이 회피를 요구하지 않는다는 뜻이다.
///
/// 조작 (§5):
///   이동 WASD/방향키 — 잡몹 구간이므로 공격은 자동
///   대시 Space — 무적 0.3초, 쿨 6초
///
/// --bot 인자를 주면 자동 회피 봇이 대신 플레이한다(사람 없이 기계 검증용).
/// </summary>
public class W2Arena : MonoBehaviour
{
    [Header("이동기 사양 (§5)")]
    public float DashIFrame = 0.30f;
    public float DashCooldown = 6.0f;
    public float DashDistance = 3.0f;      // 기본 이동 3초분
    public float DashTime = 0.18f;

    [Header("플레이어")]
    public float MoveSpeed = 4.2f;

    [Header("잡몹 속도 배율 (§18-11 — 카이팅 시뮬레이션 권장값)")]
    public float ChaserRatio = 0.90f;    // 0.64였을 때 직선 도주가 지배 전략이 됐다
    public float SwarmerRatio = 0.85f;
    public float RangedRatio = 0.65f;
    public int MaxHp = 100;

    [Header("판(run) 설정")]
    public float RunSeconds = 60f;
    public int MobCount = 120;
    public float SpawnRadius = 16f;

    const float ISO_Y = StressTest.ISO_Y;
    static Vector3 ToScreen(Vector2 w, float z = 0f) => new Vector3(w.x, w.y * ISO_Y, z);

    // ── 플레이어 ─────────────────────────────────────────
    Transform _pl;
    SpriteRenderer _plSr;
    Vector2 _plPos;
    int _hp;
    float _iframe, _dashCd, _dashT;
    Vector2 _dashDir;
    float _hitCd;                          // 피격 무적(연타 방지)

    // ── 몹·투사체 ────────────────────────────────────────
    const int MAXM = 300, MAXP = 300;
    Transform[] _mTr; SpriteRenderer[] _mSr;
    Vector2[] _mPos, _mVel; int[] _mAi; float[] _mCd; bool[] _mOn;
    Transform[] _pTr; Vector2[] _pPos, _pVel; float[] _pLife; bool[] _pOn;
    int _pCur;

    // ── 계측 ─────────────────────────────────────────────
    int _dashes, _absorbs, _hits;          // absorb = 무적으로 흡수한 피격 = "방금 피했다"
    float _surroundedT;                    // 근접 몹 3체 이상에 둘러싸인 누적 시간 (카이팅 억제 지표)
    float _t, _closestCall = 99f;
    bool _bot;
    bool _midShot;
    string _outPath;
    readonly StringBuilder _events = new StringBuilder();

    // ── HUD ──────────────────────────────────────────────
    GUIStyle _hud;

    void Awake()
    {
        QualitySettings.vSyncCount = 1;    // 조작감 테스트는 60fps 고정이 정직하다
        Application.targetFrameRate = 60;

        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--bot") _bot = true;
            if (args[i] == "--out" && i + 1 < args.Length) _outPath = args[i + 1];
            if (args[i] == "--seconds" && i + 1 < args.Length)
                float.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out RunSeconds);
        }
        _outPath ??= Path.Combine(Application.persistentDataPath, "w2_result.csv");

        Build();
    }

    void Build()
    {
        var bank = SpriteBank.Load();
        GroundBuilder.Build(bank, SpawnRadius + 30f);   // 카메라 이동 범위를 넉넉히 덮게

        var pgo = new GameObject("Player", typeof(SpriteRenderer));
        _plSr = pgo.GetComponent<SpriteRenderer>();
        _plSr.sprite = bank.Player;
        _plSr.sharedMaterial = bank.Mat;
        _plSr.sortingOrder = 500;
        // 크기는 SpriteBank가 스프라이트마다 실측해 PPU로 맞춘다.
        // 여기서 손으로 곱하던 3.0배가 "캐릭터가 몹보다 6배 큰" 원인이었다.
        pgo.transform.localScale = Vector3.one;
        _pl = pgo.transform;
        _hp = MaxHp;

        // 카메라가 플레이어를 따라다니게 (§6 오픈월드 — 고정이면 화면 밖으로 나간다)
        var cam = Camera.main;
        if (cam == null) cam = Object.FindFirstObjectByType<Camera>();   // 태그 누락에도 견디게
        if (cam != null)
        {
            var f = cam.GetComponent<CameraFollow>() ?? cam.gameObject.AddComponent<CameraFollow>();
            f.Target = _pl;
            Debug.Log("[W2] 카메라 추종 연결됨: " + cam.name);
        }
        else Debug.LogError("[W2] 카메라를 못 찾았다 — 추종 불가");

        _mTr = new Transform[MAXM]; _mSr = new SpriteRenderer[MAXM];
        _mPos = new Vector2[MAXM]; _mVel = new Vector2[MAXM];
        _mAi = new int[MAXM]; _mCd = new float[MAXM]; _mOn = new bool[MAXM];
        var mr = new GameObject("Mobs").transform;
        for (int i = 0; i < MAXM; i++)
        {
            var go = new GameObject("m", typeof(SpriteRenderer));
            go.transform.SetParent(mr, false);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sharedMaterial = bank.Mat;
            go.SetActive(false);
            _mTr[i] = go.transform; _mSr[i] = sr;
        }

        _pTr = new Transform[MAXP]; _pPos = new Vector2[MAXP];
        _pVel = new Vector2[MAXP]; _pLife = new float[MAXP]; _pOn = new bool[MAXP];
        var pr = new GameObject("Shots").transform;
        for (int i = 0; i < MAXP; i++)
        {
            var go = new GameObject("s", typeof(SpriteRenderer));
            go.transform.SetParent(pr, false);
            go.transform.localScale = Vector3.one * 1.1f;
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = bank.Projectile;
            sr.sharedMaterial = bank.Mat;
            sr.sortingOrder = 400;
            go.SetActive(false);
            _pTr[i] = go.transform;
        }

        for (int i = 0; i < MobCount; i++) Spawn(i);
        _events.AppendLine("t,event,detail");
    }

    void Spawn(int i)
    {
        if (i >= MAXM) return;
        float a = Random.value * Mathf.PI * 2f;
        _mPos[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(9f, SpawnRadius);
        _mAi[i] = (i % 10 < 4) ? 0 : (i % 10 < 7) ? 1 : 3;   // 추적40 포위30 원거리30
        _mCd[i] = Random.value * 2f;
        _mOn[i] = true;
        _mSr[i].sprite = SpriteBank.Cached.Mob(_mAi[i] == 3 ? 2 : _mAi[i] == 1 ? 1 : 0);
        _mTr[i].gameObject.SetActive(true);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        _t += dt;
        if (_t >= RunSeconds) { Finish(); return; }

        TickPlayer(dt);
        TickMobs(dt);
        TickShots(dt);

        // 근접 몹 3체 이상이 3유닛 안에 있으면 "포위됨" — 도망만 쳐서 벗어날 수 있는가를 잰다
        int near = 0;
        for (int i = 0; i < MAXM; i++)
            if (_mOn[i] && _mAi[i] != 3 && (_mPos[i] - _plPos).sqrMagnitude < 9f) near++;
        if (near >= 3) _surroundedT += dt;

        if (!_midShot && _t > RunSeconds * 0.5f)
        {
            _midShot = true;
            ScreenCapture.CaptureScreenshot(
                Path.Combine(Path.GetDirectoryName(_outPath), "w2_mid.png"));
        }
    }

    void TickPlayer(float dt)
    {
        _iframe = Mathf.Max(0f, _iframe - dt);
        _dashCd = Mathf.Max(0f, _dashCd - dt);
        _hitCd = Mathf.Max(0f, _hitCd - dt);

        Vector2 input;
        bool wantDash;
        if (_bot) BotThink(out input, out wantDash);
        else
        {
            input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            wantDash = Input.GetKeyDown(KeyCode.Space);
        }
        if (input.sqrMagnitude > 1f) input.Normalize();

        if (_dashT > 0f)
        {
            _dashT -= dt;
            _plPos += _dashDir * (DashDistance / DashTime) * dt;
        }
        else
        {
            if (wantDash && _dashCd <= 0f)
            {
                _dashDir = input.sqrMagnitude > 0.01f ? input.normalized : Vector2.down;
                _dashT = DashTime;
                _dashCd = DashCooldown;
                _iframe = DashIFrame;
                _dashes++;
                Log("dash", $"cd={DashCooldown}");
            }
            _plPos += input * MoveSpeed * dt;
        }

        Vector2 prev = _plPos;
        _plPos = Vector2.ClampMagnitude(_plPos, SpawnRadius + 4f);
        _pl.position = ToScreen(_plPos, -1f);
        // 무적 중에는 반투명 — 피드백이 없으면 유저가 무적을 인지하지 못한다
        _plSr.color = _iframe > 0f ? new Color(1f, 1f, 1f, 0.45f) : Color.white;

        // 걷기 2프레임 토글. 정지하면 대기 프레임으로 돌아온다.
        // CharAnim의 2번째 인자는 bool이 아니라 Motion이다 — 프레임 구성이 바뀌어도
        // 호출부가 프레임 번호를 몰라도 되게 하려는 것(SpriteBank 주석 참조).
        bool moving = (_plPos - prev).sqrMagnitude > 1e-6f;
        var motion = moving ? SpriteBank.Motion.Walk : SpriteBank.Motion.Idle;
        _plSr.sprite = SpriteBank.Cached.CharAnim(SpriteBank.Job.Tank, motion, Time.time);
        // 좌우 반전 — 방향별 스프라이트가 아직 없으므로 진행 방향만 뒤집어 표현한다
        if (Mathf.Abs(_plPos.x - prev.x) > 1e-5f) _plSr.flipX = _plPos.x < prev.x;
    }

    /// <summary>자동 회피 봇 — 사람 없이 기계적으로 메커니즘을 검증한다</summary>
    void BotThink(out Vector2 input, out bool dash)
    {
        // 가장 가까운 위협(투사체 또는 몹)에서 멀어지는 방향으로 이동
        Vector2 away = Vector2.zero;
        float nearest = 99f;
        for (int i = 0; i < MAXP; i++)
        {
            if (!_pOn[i]) continue;
            Vector2 d = _plPos - _pPos[i];
            float m = d.magnitude;
            if (m < nearest) nearest = m;
            if (m < 4f) away += d / (m + 0.01f) * (4f - m);
        }
        for (int i = 0; i < MAXM; i++)
        {
            if (!_mOn[i]) continue;
            Vector2 d = _plPos - _mPos[i];
            float m = d.magnitude;
            if (m < nearest) nearest = m;
            if (m < 3f) away += d / (m + 0.01f) * (3f - m) * 0.7f;
        }
        // 도망만 치면 무리를 벗어나 검증이 무의미해진다 — 사람은 무리 속에서 싸운다.
        // 중심에서 멀어질수록 되돌아오는 힘을 걸어 교전 거리를 유지시킨다.
        float r = _plPos.magnitude;
        if (r > 6f) away += (-_plPos / (r + 0.01f)) * (r - 6f) * 1.2f;

        input = away.sqrMagnitude > 0.001f ? away.normalized : Vector2.zero;
        // 위협이 코앞이면 대시 — 사람이 "위험!"이라 느낄 거리에서만
        dash = nearest < 1.4f && _dashCd <= 0f;
    }

    void TickMobs(float dt)
    {
        for (int i = 0; i < MAXM; i++)
        {
            if (!_mOn[i]) continue;
            Vector2 p = _mPos[i];
            Vector2 to = _plPos - p;
            float dist = to.magnitude + 1e-4f;
            Vector2 dir = to / dist;
            Vector2 want; float spd;

            if (_mAi[i] == 1)                       // 포위형
            {
                Vector2 tan = new Vector2(-dir.y, dir.x);
                float w = Mathf.Clamp01((dist - 3f) / 5f);
                want = (dir * w + tan * (1f - w)).normalized; spd = MoveSpeed * SwarmerRatio;
            }
            else if (_mAi[i] == 3)                  // 원거리형
            {
                float keep = 7f;
                want = dist > keep + 1f ? dir : (dist < keep - 1f ? -dir : new Vector2(-dir.y, dir.x));
                spd = MoveSpeed * RangedRatio;
                _mCd[i] -= dt;
                if (_mCd[i] <= 0f && dist < 13f) { _mCd[i] = 2.2f; Fire(p, dir); }
            }
            else { want = dir; spd = MoveSpeed * ChaserRatio; }   // 추적형

            _mVel[i] = Vector2.Lerp(_mVel[i], want * spd, 1f - Mathf.Exp(-6f * dt));
            p += _mVel[i] * dt;
            _mPos[i] = p;
            _mTr[i].position = ToScreen(p);
            _mSr[i].sortingOrder = (int)(-p.y * 16f);

            // 접촉 판정
            if (dist < 0.75f) TryHit(6, "mob");
        }
    }

    void Fire(Vector2 from, Vector2 dir)
    {
        for (int n = 0; n < MAXP; n++)
        {
            int i = _pCur; _pCur = (_pCur + 1) % MAXP;
            if (_pOn[i]) continue;
            _pOn[i] = true; _pPos[i] = from;
            _pVel[i] = dir * 5.5f;             // 느린 탄속 — 회피 가능해야 한다(§10-2)
            _pLife[i] = 4f;
            _pTr[i].gameObject.SetActive(true);
            return;
        }
    }

    void TickShots(float dt)
    {
        for (int i = 0; i < MAXP; i++)
        {
            if (!_pOn[i]) continue;
            _pLife[i] -= dt;
            _pPos[i] += _pVel[i] * dt;
            _pTr[i].position = ToScreen(_pPos[i], -0.5f);

            float d = Vector2.Distance(_pPos[i], _plPos);
            if (d < 0.5f)
            {
                if (TryHit(9, "shot")) { _pOn[i] = false; _pTr[i].gameObject.SetActive(false); }
                continue;
            }
            if (d < _closestCall) _closestCall = d;
            if (_pLife[i] <= 0f) { _pOn[i] = false; _pTr[i].gameObject.SetActive(false); }
        }
    }

    /// <returns>true = 실제로 피해가 들어감 / false = 무적으로 흡수</returns>
    bool TryHit(int dmg, string src)
    {
        if (_iframe > 0f)
        {
            _absorbs++;                         // ★ "방금 피했다"의 순간
            Log("absorb", src);
            return false;
        }
        if (_hitCd > 0f) return false;
        _hitCd = 0.4f;
        _hp -= dmg;
        _hits++;
        Log("hit", $"{src} hp={_hp}");
        if (_hp <= 0) { Log("death", $"t={_t:F1}"); _hp = MaxHp; }   // 판은 계속 — 표본 확보
        return true;
    }

    void Log(string ev, string detail) => _events.AppendLine($"{_t:F2},{ev},{detail}");

    void OnGUI()
    {
        _hud ??= new GUIStyle(GUI.skin.label) { fontSize = 18, normal = { textColor = Color.white } };
        float cd = Mathf.Max(0f, _dashCd);
        GUI.Label(new Rect(16, 12, 700, 26),
            $"HP {_hp}/{MaxHp}   대시 {(cd <= 0 ? "준비" : cd.ToString("F1") + "s")}   " +
            $"회피성공(absorb) {_absorbs}   피격 {_hits}   대시 {_dashes}   {(_bot ? "[BOT]" : "[수동: WASD 이동 / Space 대시]")}", _hud);
        GUI.Label(new Rect(16, 38, 700, 26), $"남은 시간 {Mathf.Max(0, RunSeconds - _t):F0}s", _hud);
    }

    void Finish()
    {
        float perRun = _absorbs;                    // 이 판에서의 "방금 피했다" 횟수
        string verdict = perRun >= 3f ? "PASS" : "FAIL";
        var ci = CultureInfo.InvariantCulture;

        var sb = new StringBuilder();
        float surroundPct = _surroundedT / Mathf.Max(1f, RunSeconds) * 100f;
        sb.AppendLine("run_seconds,mode,chaser_ratio,dashes,absorbs,hits,surrounded_pct,absorb_per_run,verdict");
        sb.AppendLine(string.Join(",",
            RunSeconds.ToString("F0", ci), _bot ? "bot" : "human",
            ChaserRatio.ToString("F2", ci),
            _dashes.ToString(ci), _absorbs.ToString(ci), _hits.ToString(ci),
            surroundPct.ToString("F1", ci),
            perRun.ToString("F1", ci), verdict));
        File.WriteAllText(_outPath, sb.ToString());

        var evPath = Path.Combine(Path.GetDirectoryName(_outPath), "w2_events.csv");
        File.WriteAllText(evPath, _events.ToString());
        ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(_outPath), "w2_shot.png"));

        Debug.Log($"[W2] 속도비 {ChaserRatio:F2} — 대시 {_dashes} / 회피성공 {_absorbs} / " +
                  $"피격 {_hits} / 포위시간 {_surroundedT:F1}s({surroundPct:F0}%) — {verdict}");
        Debug.Log("[W2] ===RESULT===\n" + sb);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
