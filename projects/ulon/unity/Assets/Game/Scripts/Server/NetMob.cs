using FishNet;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Ulon.Server
{
    public sealed class NetMob : NetworkBehaviour
    {
        readonly SyncVar<float> hp = new SyncVar<float>();
        WorldBody body;

        void Awake()
        {
            body = GetComponent<WorldBody>();
            hp.OnChange += OnHpChanged;
        }

        public override void OnStartServer()
        {
            if (body == null)
                body = GetComponent<WorldBody>();
            body.ApplyMobCatalog();
            body.ResetHp();
            hp.Value = body.Hp;
        }

        public void ServerSetHp(float value)
        {
            if (!IsServerInitialized)
                return;
            hp.Value = value;
        }

        void OnHpChanged(float prev, float next, bool asServer)
        {
            if (body == null)
                return;
            body.SetHp(next);
        }
    }
}
