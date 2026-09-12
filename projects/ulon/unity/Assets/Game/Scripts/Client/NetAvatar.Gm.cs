using Ulon.Server;
using Ulon.Shared;
using FishNet.Object;

namespace Ulon.Client
{
    public sealed partial class NetAvatar
    {
        [ServerRpc]
        public void RpcGmWarpPlaza()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.GmWarpPlaza(GetComponent<WorldBody>());
            if (result.Applied)
                SaveNow();
            else if (!string.IsNullOrEmpty(result.FailReason))
                SendHint("GM 거절 — " + result.FailReason);
        }

        [ServerRpc]
        public void RpcGmWarpTest()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.GmWarpTest(GetComponent<WorldBody>());
            if (result.Applied)
                SaveNow();
            else if (!string.IsNullOrEmpty(result.FailReason))
                SendHint("GM 거절 — " + result.FailReason);
        }

        [ServerRpc]
        public void RpcGmSetSkill(int skill, float value)
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.GmSetSkill(GetComponent<WorldBody>(), (SkillId)skill, value);
            if (result.Applied)
                SaveNow();
            else if (!string.IsNullOrEmpty(result.FailReason))
                SendHint("GM 거절 — " + result.FailReason);
        }

        [ServerRpc]
        public void RpcGmGive(string template, int amount)
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.GmGive(GetComponent<WorldBody>(), template, amount);
            if (result.Applied)
                SaveNow();
            else if (!string.IsNullOrEmpty(result.FailReason))
                SendHint("GM 거절 — " + result.FailReason);
        }

        [ServerRpc]
        public void RpcGmTake(string template)
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.GmTake(GetComponent<WorldBody>(), template);
            if (result.Applied)
                SaveNow();
            else if (!string.IsNullOrEmpty(result.FailReason))
                SendHint("GM 거절 — " + result.FailReason);
        }

        [ServerRpc]
        public void RpcGmSpawn()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.GmSpawnSkeleton(GetComponent<WorldBody>());
            if (!result.Applied && !string.IsNullOrEmpty(result.FailReason))
                SendHint("GM 거절 — " + result.FailReason);
        }

        [ServerRpc]
        public void RpcGmDespawn()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.GmDespawnExtra(GetComponent<WorldBody>());
            if (!result.Applied && !string.IsNullOrEmpty(result.FailReason))
                SendHint("GM 거절 — " + result.FailReason);
        }

        [ServerRpc]
        public void RpcGmReload()
        {
            if (OfflineWorld.Instance == null)
                return;
            string line = OfflineWorld.Instance.GmReloadLedgers(GetComponent<WorldBody>());
            SendHint(line ?? "");
        }

        [ServerRpc]
        public void RpcGmBackup()
        {
            if (OfflineWorld.Instance == null)
                return;
            string path = OfflineWorld.Instance.GmBackup(GetComponent<WorldBody>());
            SendHint(string.IsNullOrEmpty(path) ? "GM 거절 — " + GmAuthority.Denied : "백업 " + path);
        }

        [ServerRpc]
        public void RpcGmFreeze(bool frozen)
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.GmFreeze(GetComponent<WorldBody>(), frozen);
            if (!result.Applied && !string.IsNullOrEmpty(result.FailReason))
                SendHint("GM 거절 — " + result.FailReason);
        }
    }
}
