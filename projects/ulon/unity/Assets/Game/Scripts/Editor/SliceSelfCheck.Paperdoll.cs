using System;
using System.IO;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        static void AssertPaperdollSlots()
        {
            ItemData.Reload();
            if (EquipSlots.HudSlots.Length != 10)
                throw new InvalidOperationException("장비 인형 칸은 10이어야 합니다: " + EquipSlots.HudSlots.Length);
            if (EquipSlots.Of(ItemCatalog.IronSword) != EquipSlot.RightHand)
                throw new InvalidOperationException("철검은 오른손 칸이어야 합니다.");
            if (EquipSlots.Of(ItemCatalog.WoodenShield) != EquipSlot.LeftHand)
                throw new InvalidOperationException("나무 방패는 왼손 칸이어야 합니다.");
            if (EquipSlots.Of(ItemCatalog.IronPlate) != EquipSlot.Chest)
                throw new InvalidOperationException("철 갑옷은 갑옷 칸이어야 합니다.");
            if (EquipSlots.Of(ItemCatalog.Bandage) != EquipSlot.None)
                throw new InvalidOperationException("붕대는 장비 칸이 아니어야 합니다.");
            if (EquipSlots.Occupied(ItemCatalog.IronSword, EquipSlot.RightHand) != ItemCatalog.IronSword)
                throw new InvalidOperationException("장착 철검은 오른손 칸에 보여야 합니다.");
            if (EquipSlots.Occupied(ItemCatalog.IronSword, EquipSlot.Chest) != "")
                throw new InvalidOperationException("철검을 갑옷 칸에 그리면 안 됩니다.");

            string hudPath = Path.Combine(Application.dataPath, "Game/Scripts/Client/SliceHud.Paperdoll.cs");
            string ledgerPath = Path.Combine(Application.dataPath, "Game/Scripts/Shared/EquipSlots.cs");
            if (!File.Exists(hudPath))
                throw new InvalidOperationException("장비 인형 HUD 파일이 없습니다: " + hudPath);
            string hud = File.ReadAllText(hudPath);
            string ledger = File.ReadAllText(ledgerPath);
            if (hud.IndexOf("DrawPaperdoll", StringComparison.Ordinal) < 0 ||
                hud.IndexOf("PaperSlot", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("장비 인형 HUD가 칸을 그리지 않습니다.");
            var missing = new System.Collections.Generic.List<string>();
            for (int i = 0; i < EquipSlots.HudSlots.Length; i++)
            {
                var slot = EquipSlots.HudSlots[i];
                string label = EquipSlots.LabelOf(slot);
                string token = "EquipSlot." + slot;
                if (ledger.IndexOf(label, StringComparison.Ordinal) < 0)
                    missing.Add("원장 " + label);
                if (hud.IndexOf(token, StringComparison.Ordinal) < 0)
                    missing.Add("HUD " + token);
            }
            if (missing.Count > 0)
                throw new InvalidOperationException("장비 인형 칸이 빠졌습니다: " + string.Join(", ", missing));
            Debug.Log("[Ulon] 장비 인형 — 10칸·철검 오른손·방패 왼손·갑옷 가슴");
        }

        static void AssertPaperdollSlotsNegativeControl()
        {
            bool was = EquipSlots.NcHideMapping;
            bool red = false;
            try
            {
                EquipSlots.NcHideMapping = true;
                try { AssertPaperdollSlots(); }
                catch (InvalidOperationException) { red = true; }
            }
            finally { EquipSlots.NcHideMapping = was; }
            if (!red)
                throw new InvalidOperationException("장비 인형 네거티브 컨트롤 실패 — 매핑을 껐는데 통과했습니다.");
            Debug.Log("[Ulon] 장비 인형 네거티브 컨트롤 통과 — NcHideMapping 이면 FAIL");
        }
    }
}
