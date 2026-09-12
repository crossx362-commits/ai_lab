using FishNet.Object;
using Ulon.Server;
using Ulon.Shared;

namespace Ulon.Client
{
    public sealed partial class NetAvatar
    {
        [ServerRpc]
        public void RpcCycleSkillLock(int skill)
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryCycleSkillLock(GetComponent<WorldBody>(), (SkillId)skill);
            if (!result.Applied && !string.IsNullOrEmpty(result.FailReason))
                SendHint("잠금 거절 — " + result.FailReason);
        }

        [ServerRpc]
        public void RpcCycleStatLock(int stat)
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryCycleStatLock(GetComponent<WorldBody>(), (StatId)stat);
            if (!result.Applied && !string.IsNullOrEmpty(result.FailReason))
                SendHint("잠금 거절 — " + result.FailReason);
        }
    }
}
