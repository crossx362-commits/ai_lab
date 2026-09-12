namespace Ulon.Shared
{
    /// <summary>UO식 장비 칸. 값은 화면 칸·데이터 `slot` 문자열이 같이 읽는다.</summary>
    public enum EquipSlot
    {
        None = 0,
        Head,
        Neck,
        RightHand,
        LeftHand,
        Chest,
        Hands,
        Ring,
        Legs,
        Boots,
        Cloak,
        Belt
    }

    /// <summary>
    /// 장비 칸 원장(기획 §18.13 Paperdoll). 지금 서버 장착은 한 칸(EquippedOf)이라
    /// 화면도 그 한 개를 맞는 칸에만 그린다. 빈 칸은 그림만 — 드래그 앤 드롭은 여기 없다.
    /// </summary>
    public static class EquipSlots
    {
        /// <summary>네거티브 컨트롤 — 매핑을 끄면 칸이 전부 비어야 한다.</summary>
        public static bool NcHideMapping;

        public static readonly EquipSlot[] HudSlots =
        {
            EquipSlot.Head, EquipSlot.Neck,
            EquipSlot.RightHand, EquipSlot.LeftHand,
            EquipSlot.Chest, EquipSlot.Hands,
            EquipSlot.Legs, EquipSlot.Ring,
            EquipSlot.Boots, EquipSlot.Cloak
        };

        public static string LabelOf(EquipSlot slot)
        {
            switch (slot)
            {
                case EquipSlot.Head: return "투구";
                case EquipSlot.Neck: return "목걸이";
                case EquipSlot.RightHand: return "오른손";
                case EquipSlot.LeftHand: return "왼손";
                case EquipSlot.Chest: return "갑옷";
                case EquipSlot.Hands: return "장갑";
                case EquipSlot.Ring: return "반지";
                case EquipSlot.Legs: return "다리";
                case EquipSlot.Boots: return "신발";
                case EquipSlot.Cloak: return "망토";
                case EquipSlot.Belt: return "허리";
                default: return "";
            }
        }

        public static EquipSlot Of(string templateId)
        {
            if (NcHideMapping)
                return EquipSlot.None;
            if (string.IsNullOrEmpty(templateId))
                return EquipSlot.None;
            if (ItemData.TryGet(templateId, out var data) && !string.IsNullOrEmpty(data.slot))
                return Parse(data.slot);
            return Fallback(templateId);
        }

        /// <summary>장착 id가 이 칸의 물건이면 id, 아니면 빈 문자열. HUD와 게이트가 같이 읽는다.</summary>
        public static string Occupied(string equippedId, EquipSlot slot)
        {
            if (slot == EquipSlot.None || string.IsNullOrEmpty(equippedId))
                return "";
            return Of(equippedId) == slot ? equippedId : "";
        }

        public static EquipSlot Parse(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return EquipSlot.None;
            switch (raw)
            {
                case "head": return EquipSlot.Head;
                case "neck": return EquipSlot.Neck;
                case "right_hand": return EquipSlot.RightHand;
                case "left_hand": return EquipSlot.LeftHand;
                case "chest": return EquipSlot.Chest;
                case "hands": return EquipSlot.Hands;
                case "ring": return EquipSlot.Ring;
                case "legs": return EquipSlot.Legs;
                case "boots": return EquipSlot.Boots;
                case "cloak": return EquipSlot.Cloak;
                case "belt": return EquipSlot.Belt;
                default: return EquipSlot.None;
            }
        }

        static EquipSlot Fallback(string id)
        {
            if (id == ItemCatalog.WoodenShield)
                return EquipSlot.LeftHand;
            if (id == ItemCatalog.IronPlate)
                return EquipSlot.Chest;
            if (id == ItemCatalog.IronSword || id == ItemCatalog.WoodenClub ||
                id == ItemCatalog.WoodenBow || id == ItemCatalog.WoodenSpear ||
                id == ItemCatalog.Pickaxe || id == ItemCatalog.Hatchet ||
                id == ItemCatalog.FishingPole || id == ItemCatalog.Lute)
                return EquipSlot.RightHand;
            return EquipSlot.None;
        }
    }
}
