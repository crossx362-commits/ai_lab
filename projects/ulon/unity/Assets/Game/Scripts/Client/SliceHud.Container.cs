using System.Collections.Generic;
using UnityEngine;
using Ulon.Shared;
using Ulon.Server;

namespace Ulon.Client
{
    public sealed partial class SliceHud : MonoBehaviour
    {
        struct DropCell
        {
            public Rect Rect;
            public string Place;
            public string InstanceId;
            public bool IsPouch;
        }

        readonly List<DropCell> dropCells = new List<DropCell>();
        bool dragArmed;
        bool itemDragging;
        string dragPlace = "";
        string dragInstance = "";
        string dragTemplate = "";
        bool dragInPouch;
        Vector2 dragOrigin;
        int bankPick = -1;

        void DrawBagGrid(InventoryBag bag)
        {
            int count = bag != null ? bag.Items.Count : 0;
            GUILayout.Label("가방" + (count > 0 ? "  " + count + "칸 · 끌어다 은행/주머니" : " · 끌어다 옮김"));
            if (count == 0)
            {
                GUILayout.BeginHorizontal();
                EmptyCell(ContainerMove.Bag);
                EmptyCell(ContainerMove.Bag);
                EmptyCell(ContainerMove.Bag);
                GUILayout.EndHorizontal();
                return;
            }
            int col = 0;
            GUILayout.BeginHorizontal();
            for (int i = 0; i < count; i++)
            {
                var it = bag.Items[i];
                bool nested = !string.IsNullOrEmpty(it.ParentContainerId);
                string line = SlotLine(bag, i, it, nested);
                SlotCell(ContainerMove.Bag, it.InstanceId, it.TemplateId, nested, ItemCatalog.IsContainer(it.TemplateId), line, i == bagPick, () =>
                {
                    bagPick = bagPick == i ? -1 : i;
                });
                col++;
                if (col != 3 || i + 1 >= count)
                    continue;
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                col = 0;
            }
            EmptyCell(ContainerMove.Bag);
            GUILayout.EndHorizontal();
        }

        void DrawBankGrid(BankVault vault)
        {
            int count = vault != null ? vault.Items.Count : 0;
            GUILayout.Label(count > 0 ? "은행  " + count + "칸 · 끌어다 가방" : "은행 · 끌어다 맡김");
            int shown = 0;
            GUILayout.BeginHorizontal();
            if (vault != null)
            {
                for (int i = 0; i < count; i++)
                {
                    var it = vault.Items[i];
                    bool nested = !string.IsNullOrEmpty(it.ParentContainerId);
                    string line = ItemCatalog.DisplayNameOf(it.TemplateId);
                    if (nested)
                        line = "ㄴ " + line;
                    if (it.Amount > 1)
                        line += " x" + it.Amount;
                    int captured = i;
                    SlotCell(ContainerMove.Bank, it.InstanceId, it.TemplateId, nested, ItemCatalog.IsContainer(it.TemplateId), line, i == bankPick, () =>
                    {
                        bankPick = bankPick == captured ? -1 : captured;
                    });
                    shown++;
                    if (shown % 3 != 0 || i + 1 >= count)
                        continue;
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }
            }
            EmptyCell(ContainerMove.Bank);
            EmptyCell(ContainerMove.Bank);
            GUILayout.EndHorizontal();
        }

        static string SlotLine(InventoryBag bag, int i, ItemRecord it, bool nested)
        {
            int dup = 0, seen = 0;
            int count = bag.Items.Count;
            for (int j = 0; j < count; j++)
                if (bag.Items[j].TemplateId == it.TemplateId) { dup++; if (j <= i) seen++; }
            string line = (nested ? "ㄴ " : "") + ItemCatalog.DisplayNameOf(it.TemplateId) +
                          (dup > 1 ? " (" + seen + "/" + dup + ")" : "") +
                          (it.Amount > 1 ? " x" + it.Amount : "") +
                          (it.Uses > 0 ? " " + it.Uses : "");
            return line;
        }

        void EmptyCell(string place)
        {
            SlotCell(place, "", "", false, false, "·", false, null);
        }

        void SlotCell(string place, string instanceId, string templateId, bool inPouch, bool isPouch, string label, bool picked, System.Action onClick)
        {
            if (Btn(picked ? "▸" + label : label, ItemStyle(picked), GUILayout.Width(88f), GUILayout.Height(36f)))
            {
                if (!itemDragging && onClick != null)
                    onClick();
            }
            Rect r = GUILayoutUtility.GetLastRect();
            var e = Event.current;
            if (e.type == EventType.Repaint)
            {
                dropCells.Add(new DropCell
                {
                    Rect = r,
                    Place = place,
                    InstanceId = instanceId,
                    IsPouch = isPouch
                });
            }
            if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition) && instanceId != "")
            {
                dragArmed = true;
                itemDragging = false;
                dragPlace = place;
                dragInstance = instanceId;
                dragTemplate = templateId;
                dragInPouch = inPouch;
                dragOrigin = e.mousePosition;
            }
            if (dragArmed && !itemDragging && e.type == EventType.MouseDrag &&
                (e.mousePosition - dragOrigin).sqrMagnitude > 64f && dragInstance == instanceId)
                itemDragging = true;
        }

        void FinishDragIfNeeded(WorldBody me, NetAvatar net)
        {
            var e = Event.current;
            if (!dragArmed)
                return;
            if (e.type != EventType.MouseUp && e.rawType != EventType.MouseUp)
                return;
            if (itemDragging)
                DropDragged(me, net, e.mousePosition);
            dragArmed = false;
            itemDragging = false;
            dragInstance = "";
            dragTemplate = "";
            dragPlace = "";
            dragInPouch = false;
        }

        void DropDragged(WorldBody me, NetAvatar net, Vector2 mouse)
        {
            if (me == null || me.Ghost)
                return;
            DropCell hit = default;
            bool found = false;
            for (int i = dropCells.Count - 1; i >= 0; i--)
            {
                if (!dropCells[i].Rect.Contains(mouse))
                    continue;
                hit = dropCells[i];
                found = true;
                break;
            }
            if (!found)
                return;
            if (hit.InstanceId == dragInstance)
                return;

            if (dragPlace == ContainerMove.Bag && hit.Place == ContainerMove.Bank)
            {
                DepositOneItem(net, dragInstance);
                return;
            }
            if (dragPlace == ContainerMove.Bank && hit.Place == ContainerMove.Bag)
            {
                WithdrawOneItem(net, dragInstance);
                return;
            }
            if (dragPlace == ContainerMove.Bag && hit.Place == ContainerMove.Bag && hit.IsPouch && !dragInPouch)
            {
                PouchInItem(net, dragTemplate);
                return;
            }
            if (dragPlace == ContainerMove.Bag && hit.Place == ContainerMove.Bag && dragInPouch && !hit.IsPouch)
            {
                PouchOutItem(net, dragTemplate);
            }
        }

        void DrawDragGhost()
        {
            if (!itemDragging || string.IsNullOrEmpty(dragTemplate))
                return;
            Vector2 p = Event.current.mousePosition;
            var r = new Rect(p.x + 10f, p.y + 8f, 120f, 24f);
            GUI.Box(r, ItemCatalog.DisplayNameOf(dragTemplate));
        }
    }
}
