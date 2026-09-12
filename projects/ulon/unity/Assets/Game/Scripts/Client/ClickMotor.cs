using UnityEngine;
using Ulon.Server;

namespace Ulon.Client
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class ClickMotor : MonoBehaviour
    {
        [SerializeField] float speed = 4.2f;
        [SerializeField] float arrive = 0.15f;
        [SerializeField] float gravity = -20f;

        CharacterController controller;
        Vector3 destination;
        bool hasDestination;
        float vertical;

        public bool Moving => hasDestination;
        public float PlanarSpeed { get; private set; }

        Vector3 lastSentDest;
        bool lastSentStop = true;
        const float SendEps = 0.5f;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            destination = transform.position;
        }

        public void SetDestination(Vector3 world)
        {
            ApplyServerDestination(world);
            TrySendMove(false);
        }

        public void Stop()
        {
            ApplyServerStop();
            TrySendMove(true);
        }

        /// <summary>서버 모터가 목적지를 받는다. RPC를 다시 보내지 않는다.</summary>
        public void ApplyServerDestination(Vector3 world)
        {
            destination = world;
            destination.y = transform.position.y;
            hasDestination = true;
        }

        public void ApplyServerStop() => hasDestination = false;

        void TrySendMove(bool stop)
        {
            var net = GetComponent<NetAvatar>();
            if (net == null || !net.IsClientInitialized || net.IsServerInitialized)
                return;
            if (stop)
            {
                if (lastSentStop)
                    return;
                lastSentStop = true;
                net.RpcRequestMove(transform.position, true);
                return;
            }
            if (!lastSentStop && (destination - lastSentDest).sqrMagnitude < SendEps * SendEps)
                return;
            lastSentStop = false;
            lastSentDest = destination;
            net.RpcRequestMove(destination, false);
        }

        void Update()
        {
            if (PersistDriver.Creating || PersistDriver.Frozen)
            {
                PlanarSpeed = 0f;
                return;
            }
            Vector3 planar = Vector3.zero;
            if (hasDestination)
            {
                Vector3 delta = destination - transform.position;
                delta.y = 0f;
                if (delta.magnitude <= arrive)
                    hasDestination = false;
                else
                    planar = delta.normalized * speed;
            }

            Vector3 wasd = ReadWasd();
            if (wasd.sqrMagnitude > 0.01f)
            {
                hasDestination = false;
                planar = wasd * speed;
            }

            var bag = GetComponent<InventoryBag>();
            var world = OfflineWorld.Instance;
            var body = GetComponent<WorldBody>();
            if (bag != null && world != null && body != null && bag.Overweight(world.StatsOf(body).Str))
                planar *= 0.35f;

            if (planar.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(planar), 12f * Time.deltaTime);

            PlanarSpeed = planar.magnitude;

            if (controller.isGrounded && vertical < 0f)
                vertical = -1f;
            else
                vertical += gravity * Time.deltaTime;

            controller.Move((planar + Vector3.up * vertical) * Time.deltaTime);
        }

        Vector3 ReadWasd()
        {
            float x = Input.GetAxisRaw("Horizontal");
            float z = Input.GetAxisRaw("Vertical");
            if (Mathf.Abs(x) < 0.01f && Mathf.Abs(z) < 0.01f)
                return Vector3.zero;
            Camera cam = Camera.main;
            Vector3 forward = cam != null ? cam.transform.forward : Vector3.forward;
            Vector3 right = cam != null ? cam.transform.right : Vector3.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();
            return (forward * z + right * x).normalized;
        }
    }
}
