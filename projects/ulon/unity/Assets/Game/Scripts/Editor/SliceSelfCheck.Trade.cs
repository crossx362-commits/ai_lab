using System;
using UnityEngine;
using Ulon.Server;
using Ulon.Shared;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        static void AssertSecureTrade()
        {
            var worldGo = new GameObject("selfcheck-trade-world");
            GameObject aGo = null;
            GameObject bGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();

                aGo = new GameObject("selfcheck-trade-a");
                aGo.transform.position = new Vector3(0.4f, 0f, 0.4f);
                var a = aGo.AddComponent<WorldBody>();
                a.DisplayName = "거래A";
                a.IsAvatar = true;
                a.CharacterId = "trade-a";
                a.AccountId = "trade-a-acc";
                a.RecalcFromStr(40);
                a.ResetHp();
                a.Gold = 100;
                var bagA = aGo.AddComponent<InventoryBag>();
                bagA.Add("iron_sword", 1);
                world.StatsOf(a).ForceSet(40, 25, 25);

                bGo = new GameObject("selfcheck-trade-b");
                bGo.transform.position = aGo.transform.position;
                var b = bGo.AddComponent<WorldBody>();
                b.DisplayName = "거래B";
                b.IsAvatar = true;
                b.CharacterId = "trade-b";
                b.AccountId = "trade-b-acc";
                b.RecalcFromStr(40);
                b.ResetHp();
                b.Gold = 0;
                bGo.AddComponent<InventoryBag>();
                world.StatsOf(b).ForceSet(40, 25, 25);

                a.Ghost = true;
                var ghosted = world.TryTrade(a, b);
                if (ghosted.Applied || ghosted.FailReason != "ghost")
                    throw new InvalidOperationException("유령 거래는 ghost여야 합니다: " + ghosted.FailReason);
                a.Ghost = false;

                var begin = world.TryTrade(a, b);
                if (!begin.Applied)
                    throw new InvalidOperationException("거래 시작 실패: " + begin.FailReason);

                world.SetTradeOffer(a, "iron_sword");
                world.SetTradeGold(a, 10);
                if (a.Trade == null || a.Trade.AcceptA || a.Trade.AcceptB)
                    throw new InvalidOperationException("제안 변경 뒤 수락이 풀려 있어야 합니다.");

                world.ConfirmTrade(a);
                var waiting = world.ConfirmTrade(a);
                if (waiting.Applied || waiting.FailReason != "waiting")
                    throw new InvalidOperationException("한쪽만 수락이면 waiting이어야 합니다: " + waiting.FailReason);

                world.SetTradeGold(a, 999);
                world.ConfirmTrade(a);
                var poor = world.ConfirmTrade(b);
                if (poor.Applied || poor.FailReason != "gold")
                    throw new InvalidOperationException("골드 부족은 gold여야 합니다: " + poor.FailReason);
                if (a.Gold != 100 || b.Gold != 0)
                    throw new InvalidOperationException("골드 부족 거래가 골드를 옮기면 안 됩니다.");

                world.SetTradeGold(a, 10);
                world.SetTradeOffer(a, "iron_sword");
                world.ConfirmTrade(a);
                var done = world.ConfirmTrade(b);
                if (!done.Applied || !done.Hit)
                    throw new InvalidOperationException("골드+검 거래 실패: " + done.FailReason);
                if (a.Gold != 90 || b.Gold != 10)
                    throw new InvalidOperationException("골드가 안 옮겨짐 A=" + a.Gold + " B=" + b.Gold);
                if (CountOf(bagA, "iron_sword") != 0)
                    throw new InvalidOperationException("철검이 A 가방에 남아 있습니다.");
                var bagB = b.GetComponent<InventoryBag>();
                if (CountOf(bagB, "iron_sword") != 1)
                    throw new InvalidOperationException("철검이 B 가방에 없습니다.");

                var session = new TradeSession { A = a, B = b, OfferA = "iron_sword", GoldA = 10, Settled = true, AcceptA = true, AcceptB = true };
                a.Trade = session;
                b.Trade = session;
                int goldB = b.Gold;
                var dup = world.ConfirmTrade(b);
                if (dup.Applied)
                    throw new InvalidOperationException("정산된 세션을 다시 적용하면 안 됩니다.");
                if (dup.FailReason != "duplicate")
                    throw new InvalidOperationException("중복 거래는 duplicate여야 합니다: " + dup.FailReason);
                if (b.Gold != goldB)
                    throw new InvalidOperationException("중복 거래가 골드를 또 옮겼습니다.");

                Debug.Log("[Ulon] 안전 거래 — 골드 이전 · 제안 변경 시 수락 해제 · 중복 지급 거절");
            }
            finally
            {
                if (aGo != null) UnityEngine.Object.DestroyImmediate(aGo);
                if (bGo != null) UnityEngine.Object.DestroyImmediate(bGo);
                if (worldGo != null) UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }

        static void AssertSecureTradeNegativeControl()
        {
            bool was = TradeResolve.NcAllowDuplicate;
            bool red = false;
            try
            {
                TradeResolve.NcAllowDuplicate = true;
                try { AssertSecureTrade(); }
                catch (InvalidOperationException) { red = true; }
            }
            finally { TradeResolve.NcAllowDuplicate = was; }
            if (!red)
                throw new InvalidOperationException("안전 거래 네거티브 컨트롤 실패 — NcAllowDuplicate 인데 통과했습니다.");
            Debug.Log("[Ulon] 안전 거래 네거티브 컨트롤 통과 — NcAllowDuplicate 이면 FAIL");
        }
    }
}
