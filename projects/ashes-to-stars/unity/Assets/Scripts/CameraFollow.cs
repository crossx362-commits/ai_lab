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
        if (flat.magnitude < DeadZone) return;

        transform.position = Vector3.SmoothDamp(transform.position, want, ref _vel, Smooth);
    }
}
