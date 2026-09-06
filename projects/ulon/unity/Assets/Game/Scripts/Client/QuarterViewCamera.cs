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
        // 기획서 §4.2는 고정 3/4 쿼터뷰 + **줌 허용**이다. 피치·요는 그대로 두고 실내에서만 줌을 더 허용한다.
        // 실외 최소 8m면 카메라 높이가 8×sin35≈4.6m라 지면 높이의 암반 뚜껑 위에 머물러
        // 화면이 「지붕 윗면」이 된다(검수 2026-09-06). 실내에서는 방 안으로 들어와야 한다.
        // **상수로 둔다.** [SerializeField] 기본값은 이미 씬에 저장된 컴포넌트에는 적용되지 않아
        // 코드를 고쳐도 씬은 옛 값을 쓴다 — 네거티브 컨트롤이 통과해버려서 발견했다(2026-09-06).
        // 실측(2026-09-06): 실내 비율 90%↑를 지키는 상한은 던전 3이 6.1m·던전 1이 6.3m에서 무너진다.
        // 한계는 방 크기가 아니라 지표 — 눈높이가 지면을 넘으면 화면이 통째로 잔디가 된다. 여유를 두고 5.8m.
        // **깊이에서 유도한다**(검수 2026-09-06 요구) — 여기 숫자를 손으로 고치면 다음에 방 깊이를 바꾼
        // 사람이 두 값을 따로 맞춰야 한다. 원장은 WorldTerrain.DungeonDepth 하나뿐이다.
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
        public float IndoorDistance => Ulon.Shared.WorldTerrain.IndoorDistanceFor(pitch);

        /// <summary>
        /// 이 지점이 던전 실내인가 — 머리 위가 차폐물로 막혀 있고 **동시에 지표보다 아래**여야 실내다.
        ///
        /// 예전에는 「머리 위에 차폐 레이어가 있으면 실내」였다. 그 레이어에는 나무·마을 고정물도 들어가므로
        /// (야외 차폐 페이드, 검수 랩 D) **나무 밑에 서기만 해도 카메라가 실내 줌으로 당겨진다.**
        /// 실내의 정의는 레이어가 아니라 **지형**이다 — 방은 지표 아래에 판 것이다(규칙 명확화, 공개).
        /// </summary>
        public static bool IsIndoor(Vector3 point)
        {
            int layer = LayerMask.NameToLayer(DungeonSightFade.BlockerLayer);
            if (layer < 0)
                return false;
            if (!Physics.Raycast(point + Vector3.up * 0.2f, Vector3.up, 30f, 1 << layer, QueryTriggerInteraction.Ignore))
                return false;
            var terrain = Terrain.activeTerrain;
            if (terrain == null)
                return true;
            float ground = terrain.SampleHeight(point) + terrain.transform.position.y;
            return point.y < ground - 1.0f;
        }

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
            float useDist = IsIndoor(target) ? Mathf.Min(distance, IndoorDistance) : distance;
            transform.SetPositionAndRotation(target - rot * Vector3.forward * useDist, rot);

            var cam = GetComponent<Camera>();
            if (cam == null)
                return;
            cam.orthographic = orthographic;
            if (orthographic)
                cam.orthographicSize = Mathf.Clamp(orthographicSize * (distance / 18f), 4f, 20f);
        }
    }
}
