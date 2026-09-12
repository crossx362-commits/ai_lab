using FishNet.Object;
using UnityEngine;
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
        public void RpcRequestMove(Vector3 dest, bool stop)
        {
            var motor = GetComponent<ClickMotor>();
            if (motor == null)
                return;
            if (stop)
            {
                motor.ApplyServerStop();
                return;
            }
            if (!MoveAuthority.Accept(dest))
                return;
            motor.ApplyServerDestination(dest);
        }
    }
}
