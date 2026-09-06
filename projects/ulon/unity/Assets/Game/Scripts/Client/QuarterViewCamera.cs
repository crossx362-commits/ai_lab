using UnityEngine;

namespace Ulon.Client
{
    /// <summary>
    /// 고정 3/4 쿼터뷰. 줌만 허용하고 자유 회전은 하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QuarterViewCamera : MonoBehaviour
    {
        [SerializeField] Transform follow;
        [SerializeField] float distance = 18f;
        [SerializeField] float minDistance = 8f;
        [SerializeField] float maxDistance = 36f;
        [SerializeField] float zoomSpeed = 8f;
        [SerializeField] float pitch = 35f;
        [SerializeField] float yaw = 45f;
        [SerializeField] bool orthographic;
        [SerializeField] float orthographicSize = 8f;

        public void SetFollow(Transform target)
        {
            follow = target;
            // 실내 차폐 페이드도 같은 대상을 본다(검수 2026-09-06 P0 — 천장 위 카메라).
            var fade = GetComponent<DungeonSightFade>();
            if (fade == null)
                fade = gameObject.AddComponent<DungeonSightFade>();
            fade.SetTarget(target);
        }

        /// <summary>Assert가 같은 값으로 카메라를 놓기 위해 읽는다 — 검증 카메라가 플레이 카메라와 달라 P0를 놓쳤다.</summary>
        public float Pitch => pitch;
        public float Yaw => yaw;
        public float Distance => distance;
        public float MinDistance => minDistance;

        void OnEnable()
        {
            var cam = GetComponent<Camera>();
            if (cam == null)
                return;
            if (GetComponent<DungeonSightFade>() == null)
                gameObject.AddComponent<DungeonSightFade>();
            cam.clearFlags = CameraClearFlags.Skybox;
            if (cam.farClipPlane < 90f)
                cam.farClipPlane = 90f;
        }

        void LateUpdate()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.0001f)
                distance = Mathf.Clamp(distance - scroll * zoomSpeed, minDistance, maxDistance);

            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 target = follow != null ? follow.position : Vector3.zero;
            transform.SetPositionAndRotation(target - rot * Vector3.forward * distance, rot);

            var cam = GetComponent<Camera>();
            if (cam == null)
                return;
            cam.orthographic = orthographic;
            if (orthographic)
                cam.orthographicSize = Mathf.Clamp(orthographicSize * (distance / 18f), 4f, 20f);
        }
    }
}
