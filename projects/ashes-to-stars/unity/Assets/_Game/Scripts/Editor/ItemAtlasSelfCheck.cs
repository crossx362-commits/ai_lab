using UnityEngine;

namespace AshesToStars
{
    /// <summary>성장·보상 아이템 아틀라스가 Resources에서 읽히고, 등록 조각이 원본 안에 있는지 검사한다.</summary>
    public static class ItemAtlasSelfCheck
    {
        public static void Run()
        {
            Debug.Assert(ItemAtlas.IsReady, "[ItemAtlasSelfCheck] 아이템 아틀라스를 Resources/ui에서 읽지 못했다");

            foreach (var key in ItemAtlas.RequiredKeys)
            {
                var rect = ItemAtlas.RectFor(key);
                Debug.Assert(rect.width > 0 && rect.height > 0,
                    $"[ItemAtlasSelfCheck] {key}: 빈 영역");
                Debug.Assert(rect.xMin >= 0 && rect.yMin >= 0 &&
                             rect.xMax <= ItemAtlas.Width && rect.yMax <= ItemAtlas.Height,
                    $"[ItemAtlasSelfCheck] {key}: 아틀라스 밖 영역 {rect}");
            }

            Debug.Assert(ItemAtlas.KeyForSlot(EquipSlot.Weapon) == "sword"
                         && ItemAtlas.KeyForSlot(EquipSlot.Helm) == "helmet"
                         && ItemAtlas.KeyForSlot(EquipSlot.Armor) == "armor"
                         && ItemAtlas.KeyForSlot(EquipSlot.Gloves) == "gloves"
                         && ItemAtlas.KeyForSlot(EquipSlot.Boots) == "boots"
                         && ItemAtlas.KeyForSlot(EquipSlot.Accessory) == "amulet",
                "[ItemAtlasSelfCheck] 6부위 슬롯 키가 아틀라스 조각과 어긋난다");
            Debug.Assert(ItemAtlas.KeyFor(Economy.LifeItem.RevivalTea) == "revival_tea"
                         && ItemAtlas.KeyFor(Economy.LifeItem.AdvancementMaterial) == "advancement_material"
                         && ItemAtlas.KeyFor(Economy.LifeItem.RebornStone) == "reborn_stone"
                         && ItemAtlas.KeyFor(Economy.LifeItem.EnhanceStone) == "gold",
                "[ItemAtlasSelfCheck] 목숨·전직·강화 아이템 키가 어긋난다");
            Debug.Assert(ItemAtlas.KeyFor(Economy.LifeItem.EnhanceStone) != "building_smith",
                "[ItemAtlasSelfCheck] 강화는 건물 실루엣이 아니라 강화석 조각이어야 한다");
            Debug.Assert(ItemAtlas.SmithMaterials.Length == 7, "[ItemAtlasSelfCheck] 대장간 재료 7종이 아니다");
            for (int i = 0; i < ItemAtlas.SmithMaterials.Length; i++)
            {
                string key = ItemAtlas.KeyFor(ItemAtlas.SmithMaterials[i]);
                Debug.Assert(!string.IsNullOrEmpty(key) && ItemAtlas.RectFor(key).width > 0,
                    $"[ItemAtlasSelfCheck] 대장간 재료 {ItemAtlas.SmithMaterials[i]} 조각 없음");
            }
            var dummy = new GearItem { Slot = EquipSlot.Weapon };
            Debug.Assert(ItemAtlas.KeyForGear(dummy) == "sword",
                "[ItemAtlasSelfCheck] 장착 장비 아이콘이 부위와 어긋난다");
            Debug.Assert(ItemAtlas.KeyForGear(null) == null,
                "[ItemAtlasSelfCheck] 빈 장비는 아이콘이 없어야 한다");
            Debug.Assert(dummy.Grade == GearGrade.Common,
                "[ItemAtlasSelfCheck] 기본 장비 등급은 일반이다");
            _ = nameof(ItemAtlas.DrawHud);
            _ = nameof(ItemAtlas.DrawGear);

            Debug.Log("[ItemAtlasSelfCheck] PASS");
        }
    }
}
