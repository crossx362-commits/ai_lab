using FishNet.Object;
using Ulon.Server;

namespace Ulon.Client
{
    public sealed partial class NetAvatar
    {
        [ServerRpc]
        public void RpcTradeGold(int gold)
        {
            OfflineWorld.Instance?.SetTradeGold(GetComponent<WorldBody>(), gold);
            BroadcastTrade();
        }
    }
}
