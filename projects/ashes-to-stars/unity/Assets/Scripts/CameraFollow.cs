using UnityEngine;

/// <summary>
/// 쿼터뷰 카메라 추종.
///
/// 오픈월드(§6)를 돌아다니는 게임이라 카메라가 고정이면 화면 밖으로 나간다.
/// 다만 **즉시 따라붙으면 멀미가 나고, 너무 느리면 답답하다** — 뱀서류는
/// 화면 밖에서 몹이 밀려오는 장르라 시야 확보가 곧 생존이므로,
/// 부드럽게 따라가되 **진행 방향을 살짝 앞서 보여주는** 방식을 쓴다.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    public Transform Target;

    [Tooltip("따라붙는 부드러움 — 작을수록 빠릿, 클수록 느긋")]
    public float Smooth = 0.12f;

    [Tooltip("진행 방향으로 미리 내다보는 거리")]
    public float LookAhead = 1.6f;

    [Tooltip("이만큼 안에서는 카메라가 아예 안 움직인다 (미세 떨림 방지)")]
    public float DeadZone = 0.25f;

    Vector3 _vel;
    Vector2 _lastTargetPos;
    Vector2 _lookDir;

    // ── 화면 흔들림 ────────────────────────────────
    // 타격감은 이펙트보다 화면 자체의 반응에서 더 크게 온다(Nijman, "The art of screenshake").
    // 비용이 0이고 아트가 0장이라 이 게임처럼 화면에 수백 마리가 나오는 판에서 특히 값이 크다.
    // ⚠️ 추종 결과에 **더한다** — SmoothDamp 대상 좌표를 흔들면 카메라가 목표를 따라 튄다.
    float _shakeAmp, _shakeT;

    /// <summary>화면을 흔든다. amp는 월드 단위(0.15~0.5 권장), dur는 초.</summary>
    public void Shake(float amp, float dur = 0.18f)
    {
        _shakeAmp = Mathf.Max(_shakeAmp, amp);
        _shakeT = Mathf.Max(_shakeT, dur);
    }

    void LateUpdate()
    {
        if (Target == null) return;

        Vector2 t = Target.position;

        // 이동 방향 추정 — 화면 좌표 기준(쿼터뷰라 y가 눌려 있어도 방향은 유효)
        Vector2 delta = t - _lastTargetPos;
        if (delta.sqrMagnitude > 1e-5f)
            _lookDir = Vector2.Lerp(_lookDir, delta.normalized, 0.08f);
        _lastTargetPos = t;

        Vector3 want = new Vector3(t.x + _lookDir.x * LookAhead,
                                   t.y + _lookDir.y * LookAhead,
                                   transform.position.z);

        // 데드존 — 제자리 미동에 카메라가 흔들리지 않게
        Vector2 flat = new Vector2(want.x - transform.position.x, want.y - transform.position.y);
        if (flat.magnitude >= DeadZone)
            transform.position = Vector3.SmoothDamp(transform.position, want, ref _vel, Smooth);

        ApplyShake();
    }

    void ApplyShake()
    {
        if (_shakeT <= 0f) return;
        _shakeT -= Time.deltaTime;
        if (_shakeT <= 0f) { _shakeAmp = 0f; return; }

        // 감쇠하는 무작위 오프셋. 프레임마다 방향이 바뀌어야 '충격'으로 읽힌다
        float k = _shakeAmp * (_shakeT / 0.18f);
        transform.position += new Vector3(Random.Range(-k, k), Random.Range(-k, k) * 0.6f, 0f);
    }
}
