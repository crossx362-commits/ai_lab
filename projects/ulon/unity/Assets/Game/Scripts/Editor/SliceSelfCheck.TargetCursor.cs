using System;
using System.IO;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        static void AssertTargetCursor()
        {
            string hudPath = Path.Combine(Application.dataPath, "Game/Scripts/Client/SliceHud.TargetCursor.cs");
            string ledgerPath = Path.Combine(Application.dataPath, "Game/Scripts/Shared/TargetKinds.cs");
            string actionsPath = Path.Combine(Application.dataPath, "Game/Scripts/Client/SliceHud.Actions.cs");
            if (!File.Exists(hudPath))
                throw new InvalidOperationException("대상 커서 HUD가 없습니다: " + hudPath);
            if (!File.Exists(ledgerPath))
                throw new InvalidOperationException("대상 커서 원장이 없습니다: " + ledgerPath);
            string hud = File.ReadAllText(hudPath);
            string ledger = File.ReadAllText(ledgerPath);
            string actions = File.Exists(actionsPath) ? File.ReadAllText(actionsPath) : "";
            if (hud.IndexOf("DrawTargetCursor", StringComparison.Ordinal) < 0 ||
                hud.IndexOf("BeginTarget", StringComparison.Ordinal) < 0 ||
                hud.IndexOf("ConfirmWorldTarget", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("대상 지정 커서를 그리지 않거나 확인 경로가 없습니다.");
            if (actions.IndexOf("EnterTarget(TargetKind.Heal", StringComparison.Ordinal) < 0 ||
                actions.IndexOf("EnterTarget(TargetKind.Spell", StringComparison.Ordinal) < 0 ||
                actions.IndexOf("EnterTarget(TargetKind.Gather", StringComparison.Ordinal) < 0 ||
                actions.IndexOf("EnterTarget(TargetKind.Interact", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("치유·주문·채집·상호작용이 지정 모드를 안 탑니다.");

            string[] need =
            {
                TargetKinds.PromptHeal, TargetKinds.PromptSpell,
                TargetKinds.PromptGather, TargetKinds.PromptInteract
            };
            var missing = new System.Collections.Generic.List<string>();
            for (int i = 0; i < need.Length; i++)
            {
                if (ledger.IndexOf(need[i], StringComparison.Ordinal) < 0)
                    missing.Add("원장 " + need[i]);
            }
            if (hud.IndexOf("TargetKinds.PromptOf", StringComparison.Ordinal) < 0)
                missing.Add("HUD PromptOf");
            if (missing.Count > 0)
                throw new InvalidOperationException("대상 커서 안내가 빠졌습니다: " + string.Join(", ", missing));

            if (string.IsNullOrEmpty(TargetKinds.PromptOf(TargetKind.Heal)) ||
                TargetKinds.PromptOf(TargetKind.Heal) != TargetKinds.PromptHeal)
                throw new InvalidOperationException("치유 커서 안내가 원장과 다릅니다.");
            if (TargetKinds.PromptOf(TargetKind.Spell) != TargetKinds.PromptSpell)
                throw new InvalidOperationException("주문 커서 안내가 원장과 다릅니다.");
            if (TargetKinds.PromptOf(TargetKind.Gather) != TargetKinds.PromptGather)
                throw new InvalidOperationException("채집 커서 안내가 원장과 다릅니다.");
            if (TargetKinds.PromptOf(TargetKind.Interact) != TargetKinds.PromptInteract)
                throw new InvalidOperationException("상호작용 커서 안내가 원장과 다릅니다.");

            Debug.Log("[Ulon] 대상 지정 커서 — 치유·주문·채집·상호작용 안내·지정 모드");
        }

        static void AssertTargetCursorNegativeControl()
        {
            bool was = TargetKinds.NcHide;
            bool red = false;
            try
            {
                TargetKinds.NcHide = true;
                try { AssertTargetCursor(); }
                catch (InvalidOperationException) { red = true; }
            }
            finally { TargetKinds.NcHide = was; }
            if (!red)
                throw new InvalidOperationException("대상 커서 네거티브 컨트롤 실패 — NcHide 인데 통과했습니다.");
            Debug.Log("[Ulon] 대상 커서 네거티브 컨트롤 통과 — NcHide 이면 FAIL");
        }
    }
}
