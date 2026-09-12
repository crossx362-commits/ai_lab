using UnityEngine;
using Ulon.Server;
using Ulon.Shared;

namespace Ulon.Client
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class ClickMotor : MonoBehaviour
    {
        [SerializeField] float arrive = 0.15f;
        [SerializeField] float gravity = -20f;

        CharacterController controller;
        Vector3 destination;
        bool hasDestination;
        float vertical;

        public bool Moving => hasDestination;
        public bool Running { get; private set; }
        public float PlanarSpeed { get; private set; }

        Vector3 lastSentDest;
        bool lastSentStop = true;
        bool lastSentRun;
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
            var ow = OfflineWorld.Instance;
            var body = GetComponent<WorldBody>();
            ow?.TryInterruptCastByMove(body);
            destination = world;
            destination.y = transform.position.y;
            hasDestination = true;
        }

        public void ApplyServerStop() => hasDestination = false;

        public void ApplyServerRunning(bool run) => Running = run && CanRunNow();

        /// <summary>소유 클라 입력. 모터가 꺼져 있어도 RPC는 나간다. 과적·기진이면 달리기를 끈다(§18.5·§18.2).</summary>
        public void SetRunning(bool run)
        {
            run = run && CanRunNow();
            if (Running == run)
                return;
            Running = run;
            if (hasDestination)
                TrySendMove(false);
        }

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
                net.RpcRequestMove(transform.position, true, Running);
                return;
            }
            if (!lastSentStop && lastSentRun == Running &&
                (destination - lastSentDest).sqrMagnitude < SendEps * SendEps)
                return;
            lastSentStop = false;
            lastSentRun = Running;
            lastSentDest = destination;
            net.RpcRequestMove(destination, false, Running);
        }

        void Update()
        {
            if (PersistDriver.Creating || PersistDriver.Frozen)
            {
                PlanarSpeed = 0f;
                return;
            }
            if (!CanRunNow())
                Running = false;
            float speed = MoveSpeed.MetersPerSecond(Running);
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
                var ow = OfflineWorld.Instance;
                var body = GetComponent<WorldBody>();
                ow?.TryInterruptCastByMove(body);
                hasDestination = false;
                planar = wasd * speed;
            }

            var stamBody = GetComponent<WorldBody>();
            stamBody?.TickStamina(Running && planar.sqrMagnitude > 0.01f, Time.deltaTime);
            if (!CanRunNow())
                Running = false;

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

        bool IsLight()
        {
            var bag = GetComponent<InventoryBag>();
            var world = OfflineWorld.Instance;
            var body = GetComponent<WorldBody>();
            if (bag == null || world == null || body == null)
                return true;
            return !bag.Overweight(world.StatsOf(body).Str);
        }

        bool CanRunNow()
        {
            var body = GetComponent<WorldBody>();
            float stam = body != null ? body.Stamina : 1f;
            return CarryMove.CanRun(!IsLight()) && RunStamina.CanRun(stam);
        }
    }
}
