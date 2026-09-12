using System;
using Ulon.Server;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        static void AssertGmAuth()
        {
            string hud = HudSourceText();
            if (hud.IndexOf("RpcGmGive", StringComparison.Ordinal) < 0 ||
                hud.IndexOf("RpcGmWarpPlaza", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("GM 패널이 서버 RPC로 안 갑니다 — 클라가 제 세계에서 지급합니다.");
            if (hud.IndexOf("GmReloadLedgers", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("GM 패널에 원장 다시 읽기 배선이 없습니다.");

            var world = OfflineWorld.Instance;
            if (world == null)
                throw new InvalidOperationException("OfflineWorld 없음");

            bool wasBypass = GmAuthority.EditorBypass;
            string extra = GmAccounts.ExtraAccount;
            var strangerGo = new GameObject("selfcheck-gm-stranger");
            var listedGo = new GameObject("selfcheck-gm-listed");
            try
            {
                GmAuthority.EditorBypass = false;
                GmAccounts.ExtraAccount = "listed-gm";

                var stranger = strangerGo.AddComponent<WorldBody>();
                stranger.IsAvatar = true;
                stranger.AccountId = "stranger";
                strangerGo.AddComponent<InventoryBag>();
                var denied = world.GmGive(stranger, "iron_ore", 1);
                if (denied.Applied)
                    throw new InvalidOperationException("무인증 GM 지급이 먹혔습니다.");
                if (denied.FailReason != GmAuthority.Denied)
                    throw new InvalidOperationException("무인증 거절 사유가 unauthorized가 아닙니다: " + denied.FailReason);

                var listed = listedGo.AddComponent<WorldBody>();
                listed.IsAvatar = true;
                listed.AccountId = "listed-gm";
                listedGo.AddComponent<InventoryBag>();
                var ok = world.GmGive(listed, "iron_ore", 1);
                if (!ok.Applied)
                    throw new InvalidOperationException("원장 계정 GM 지급이 거절됐습니다: " + ok.FailReason);
                var bag = listedGo.GetComponent<InventoryBag>();
                if (bag == null || CountOf(bag, "iron_ore") < 1)
                    throw new InvalidOperationException("원장 계정 지급 뒤 가방이 비었습니다.");
            }
            finally
            {
                GmAuthority.EditorBypass = wasBypass;
                GmAccounts.ExtraAccount = extra;
                UnityEngine.Object.DestroyImmediate(strangerGo);
                UnityEngine.Object.DestroyImmediate(listedGo);
            }

            Debug.Log("[Ulon] GM 권한 — 무인증 거절 · 원장 계정 지급 · HUD RPC");
        }

        static void AssertGmAuthNegativeControl()
        {
            bool was = GmAuthority.NcOpen;
            bool red = false;
            try
            {
                GmAuthority.NcOpen = true;
                try { AssertGmAuth(); }
                catch (InvalidOperationException) { red = true; }
            }
            finally { GmAuthority.NcOpen = was; }
            if (!red)
                throw new InvalidOperationException("GM 권한 네거티브 컨트롤 실패 — NcOpen 인데 통과했습니다.");
            Debug.Log("[Ulon] GM 권한 네거티브 컨트롤 통과 — NcOpen 이면 FAIL");
        }
    }
}
