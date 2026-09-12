using FishNet.Object;
using UnityEngine;
using Ulon.Server;
using Ulon.Shared;

namespace Ulon.Client
{
    public sealed partial class NetAvatar
    {
        public override void OnStartServer()
        {
            base.OnStartServer();
            var motor = GetComponent<ClickMotor>();
            if (motor != null)
                motor.enabled = true;
        }

        /// <summary>
        /// 클라가 보낸 목적지. 서버 모터가 걷는다. 섬 밖은 버린다(§7.2).
        /// </summary>
        [ServerRpc]
        public void RpcRequestMove(Vector3 dest, bool stop, bool running)
        {
            var motor = GetComponent<ClickMotor>();
            if (motor == null)
                return;
            var bag = GetComponent<InventoryBag>();
            var world = OfflineWorld.Instance;
            var body = GetComponent<WorldBody>();
            bool overweight = bag != null && world != null && body != null &&
                              bag.Overweight(world.StatsOf(body).Str);
            float stam = body != null ? body.Stamina : 1f;
            running = running && CarryMove.CanRun(overweight) && RunStamina.CanRun(stam);
            motor.ApplyServerRunning(running);
            if (stop)
            {
                motor.ApplyServerStop();
                return;
            }
            if (world != null && body != null)
                world.TryInterruptCastByMove(body);
            if (!MoveAuthority.Accept(dest))
                return;
            motor.ApplyServerDestination(dest);
        }
    }
}
