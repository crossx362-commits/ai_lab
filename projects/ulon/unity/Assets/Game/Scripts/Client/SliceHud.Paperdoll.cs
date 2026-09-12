using UnityEngine;
using Ulon.Shared;
using Ulon.Server;

namespace Ulon.Client
{
    public sealed partial class SliceHud : MonoBehaviour
    {
        /// <summary>
        /// 가방 판 위쪽의 장비 인형. 칸 그림이 목적이다(기획 §18.13).
        /// 서버 장착은 아직 한 개라 맞는 칸 하나만 채워진다. 빈 칸은 자리만 보여 준다.
        /// </summary>
        void DrawPaperdoll(WorldBody me, NetAvatar net)
        {
            string equipped = OfflineWorld.Instance != null ? OfflineWorld.Instance.EquippedOf(me) : "";
            GUILayout.Label("장비 인형");
            RowSlot(me, net, equipped, EquipSlot.Head, EquipSlot.None, EquipSlot.Neck);
            RowSlot(me, net, equipped, EquipSlot.RightHand, EquipSlot.None, EquipSlot.LeftHand);
            RowSlot(me, net, equipped, EquipSlot.Hands, EquipSlot.Chest, EquipSlot.Ring);
            RowSlot(me, net, equipped, EquipSlot.Boots, EquipSlot.Legs, EquipSlot.Cloak);
            GUILayout.Space(4f);
        }

        void RowSlot(WorldBody me, NetAvatar net, string equipped, EquipSlot left, EquipSlot mid, EquipSlot right)
        {
            GUILayout.BeginHorizontal();
            PaperSlot(me, net, equipped, left);
            if (mid == EquipSlot.None)
                PaperFigure(me);
            else
                PaperSlot(me, net, equipped, mid);
            PaperSlot(me, net, equipped, right);
            GUILayout.EndHorizontal();
        }

        void PaperFigure(WorldBody me)
        {
            string name = me != null ? me.DisplayName : "";
            string title = "";
            if (me != null && OfflineWorld.Instance != null)
                title = OfflineWorld.Instance.TitleOf(me);
            string line = string.IsNullOrEmpty(name) ? "인형" : name;
            if (!string.IsNullOrEmpty(title))
                line = line + "\n" + title;
            GUILayout.Box(line, GUILayout.Width(88f), GUILayout.Height(52f));
        }

        void PaperSlot(WorldBody me, NetAvatar net, string equipped, EquipSlot slot)
        {
            string held = EquipSlots.Occupied(equipped, slot);
            string label = EquipSlots.LabelOf(slot);
            if (held != "")
                label = label + " · " + ItemCatalog.DisplayNameOf(held);
            if (Btn(label, GUILayout.Width(88f), GUILayout.Height(40f)))
                ClickPaperSlot(me, net, slot, held);
        }

        void ClickPaperSlot(WorldBody me, NetAvatar net, EquipSlot slot, string held)
        {
            if (me != null && me.Ghost)
                return;
            if (held != "")
            {
                Unequip(net);
                return;
            }
            var bag = me != null ? me.GetComponent<InventoryBag>() : null;
            int count = bag != null ? bag.Items.Count : 0;
            string picked = bagPick >= 0 && bagPick < count ? bag.Items[bagPick].TemplateId : "";
            if (picked == "")
                return;
            if (EquipSlots.Of(picked) != slot)
                return;
            EquipItem(net, picked);
        }
    }
}
