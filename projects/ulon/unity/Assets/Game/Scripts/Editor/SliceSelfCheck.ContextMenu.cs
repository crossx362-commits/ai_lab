using System;
using System.IO;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        static void AssertContextMenu()
        {
            string hudPath = Path.Combine(Application.dataPath, "Game/Scripts/Client/SliceHud.Context.cs");
            string ledgerPath = Path.Combine(Application.dataPath, "Game/Scripts/Shared/ContextKinds.cs");
            if (!File.Exists(hudPath))
                throw new InvalidOperationException("컨텍스트 메뉴 HUD가 없습니다: " + hudPath);
            if (!File.Exists(ledgerPath))
                throw new InvalidOperationException("컨텍스트 메뉴 원장이 없습니다: " + ledgerPath);
            string hud = File.ReadAllText(hudPath);
            string ledger = File.ReadAllText(ledgerPath);
            if (hud.IndexOf("DrawContextMenu", StringComparison.Ordinal) < 0 ||
                hud.IndexOf("OpenWorldContextFromInput", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("우클릭 메뉴를 그리지 않습니다.");
            if (hud.IndexOf("RpcTrainer", StringComparison.Ordinal) < 0 ||
                hud.IndexOf("RpcPetCommand", StringComparison.Ordinal) < 0 ||
                hud.IndexOf("RpcHouseLockdown", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("훈련·펫·집 보안이 서버 요청을 안 탑니다.");

            string[] need =
            {
                ContextKinds.TrainOpen, ContextKinds.Follow, ContextKinds.Stay, ContextKinds.Guard,
                ContextKinds.Lockdown, ContextKinds.Take, ContextKinds.Claim
            };
            var missing = new System.Collections.Generic.List<string>();
            for (int i = 0; i < need.Length; i++)
            {
                if (ledger.IndexOf(need[i], StringComparison.Ordinal) < 0)
                    missing.Add("원장 " + need[i]);
                if (hud.IndexOf("\"" + need[i] + "\"", StringComparison.Ordinal) < 0)
                    missing.Add("HUD " + need[i]);
            }
            if (missing.Count > 0)
                throw new InvalidOperationException("컨텍스트 메뉴 라벨이 빠졌습니다: " + string.Join(", ", missing));

            if (!ContainsAll(ContextKinds.LabelsOf(ContextKind.Trainer), ContextKinds.TrainOpen))
                throw new InvalidOperationException("훈련 메뉴에 훈련 열기가 없습니다.");
            if (!ContainsAll(ContextKinds.LabelsOf(ContextKind.Pet), ContextKinds.Follow, ContextKinds.Stay, ContextKinds.Guard))
                throw new InvalidOperationException("펫 메뉴에 따라와/기다려/지켜라가 없습니다.");
            if (!ContainsAll(ContextKinds.LabelsOf(ContextKind.HouseChest), ContextKinds.Lockdown, ContextKinds.Take))
                throw new InvalidOperationException("집 보안 메뉴에 잠금/꺼내기가 없습니다.");

            Debug.Log("[Ulon] 컨텍스트 메뉴 — 훈련·펫 명령·집 보안 라벨·서버 요청");
        }

        static bool ContainsAll(string[] have, params string[] want)
        {
            for (int i = 0; i < want.Length; i++)
            {
                bool ok = false;
                for (int j = 0; j < have.Length; j++)
                    if (have[j] == want[i])
                        ok = true;
                if (!ok)
                    return false;
            }
            return true;
        }

        static void AssertContextMenuNegativeControl()
        {
            bool was = ContextKinds.NcHide;
            bool red = false;
            try
            {
                ContextKinds.NcHide = true;
                try { AssertContextMenu(); }
                catch (InvalidOperationException) { red = true; }
            }
            finally { ContextKinds.NcHide = was; }
            if (!red)
                throw new InvalidOperationException("컨텍스트 메뉴 네거티브 컨트롤 실패 — NcHide 인데 통과했습니다.");
            Debug.Log("[Ulon] 컨텍스트 메뉴 네거티브 컨트롤 통과 — NcHide 이면 FAIL");
        }
    }
}
