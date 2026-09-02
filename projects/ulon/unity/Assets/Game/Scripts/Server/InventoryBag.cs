using System.Collections.Generic;
using UnityEngine;
using Ulon.Shared;

namespace Ulon.Server
{
    public sealed class InventoryBag : MonoBehaviour
    {
        public readonly List<ItemRecord> Items = new List<ItemRecord>();

        public void Replace(ItemRecord[] records)
        {
            Items.Clear();
            if (records == null)
                return;
            for (int i = 0; i < records.Length; i++)
            {
                if (string.IsNullOrEmpty(records[i].TemplateId) || records[i].Amount <= 0)
                    continue;
                Items.Add(records[i]);
            }
        }

        public void Add(string templateId, int amount)
        {
            Add(new ItemRecord { TemplateId = templateId, Amount = amount, Uses = ItemCatalog.MaxUsesOf(templateId) });
        }

        public void Add(ItemRecord rec)
        {
            if (string.IsNullOrEmpty(rec.TemplateId) || rec.Amount <= 0)
                return;
            if (ItemCatalog.Stackable(rec.TemplateId))
            {
                for (int i = 0; i < Items.Count; i++)
                {
                    var it = Items[i];
                    if (it.TemplateId != rec.TemplateId)
                        continue;
                    it.Amount += rec.Amount;
                    Items[i] = it;
                    return;
                }
            }
            rec.Slot = Items.Count;
            if (rec.Uses <= 0)
                rec.Uses = ItemCatalog.MaxUsesOf(rec.TemplateId);
            Items.Add(rec);
        }

        public float TotalWeight() => ItemCatalog.WeightOf(Items);

        public bool Overweight(int str) => TotalWeight() > ItemCatalog.CarryCap(str);

        public int ToolUses(string templateId)
        {
            for (int i = 0; i < Items.Count; i++)
                if (Items[i].TemplateId == templateId)
                    return Items[i].Uses;
            return 0;
        }

        public bool WearTool(string templateId)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                var it = Items[i];
                if (it.TemplateId != templateId || it.Uses <= 0)
                    continue;
                it.Uses -= 1;
                if (it.Uses <= 0)
                    Items.RemoveAt(i);
                else
                    Items[i] = it;
                return true;
            }
            return false;
        }

        public bool RepairOne(int restore)
        {
            int best = -1;
            int missing = 0;
            for (int i = 0; i < Items.Count; i++)
            {
                int max = ItemCatalog.MaxUsesOf(Items[i].TemplateId);
                if (max <= 0 || Items[i].Uses >= max)
                    continue;
                int need = max - Items[i].Uses;
                if (need > missing)
                {
                    missing = need;
                    best = i;
                }
            }
            if (best < 0)
                return false;
            var it = Items[best];
            int maxUses = ItemCatalog.MaxUsesOf(it.TemplateId);
            it.Uses += restore;
            if (it.Uses > maxUses)
                it.Uses = maxUses;
            Items[best] = it;
            return true;
        }

        public bool TakeOne(string templateId)
        {
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                var it = Items[i];
                if (it.TemplateId != templateId)
                    continue;
                if (ItemCatalog.Stackable(templateId))
                {
                    it.Amount -= 1;
                    if (it.Amount <= 0)
                        Items.RemoveAt(i);
                    else
                        Items[i] = it;
                    return true;
                }
                Items.RemoveAt(i);
                return true;
            }
            return false;
        }

        public ItemRecord[] ToArray() => Items.ToArray();
    }

    public sealed class BankVault : MonoBehaviour
    {
        public readonly List<ItemRecord> Items = new List<ItemRecord>();

        public void Replace(ItemRecord[] records)
        {
            Items.Clear();
            if (records == null)
                return;
            for (int i = 0; i < records.Length; i++)
            {
                if (string.IsNullOrEmpty(records[i].TemplateId) || records[i].Amount <= 0)
                    continue;
                Items.Add(records[i]);
            }
        }

        public void Add(string templateId, int amount)
        {
            Add(new ItemRecord { TemplateId = templateId, Amount = amount, Uses = ItemCatalog.MaxUsesOf(templateId) });
        }

        public void Add(ItemRecord rec)
        {
            if (string.IsNullOrEmpty(rec.TemplateId) || rec.Amount <= 0)
                return;
            if (ItemCatalog.Stackable(rec.TemplateId))
            {
                for (int i = 0; i < Items.Count; i++)
                {
                    var it = Items[i];
                    if (it.TemplateId != rec.TemplateId)
                        continue;
                    it.Amount += rec.Amount;
                    Items[i] = it;
                    return;
                }
            }
            rec.Slot = Items.Count;
            if (rec.Uses <= 0)
                rec.Uses = ItemCatalog.MaxUsesOf(rec.TemplateId);
            Items.Add(rec);
        }

        public ItemRecord[] ToArray() => Items.ToArray();
    }
}
