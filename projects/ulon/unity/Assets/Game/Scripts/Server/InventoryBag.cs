using System;
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
            string parent = rec.ParentContainerId ?? "";
            if (ItemCatalog.Stackable(rec.TemplateId))
            {
                for (int i = 0; i < Items.Count; i++)
                {
                    var it = Items[i];
                    if (it.TemplateId != rec.TemplateId)
                        continue;
                    if ((it.ParentContainerId ?? "") != parent)
                        continue;
                    it.Amount += rec.Amount;
                    Items[i] = it;
                    return;
                }
            }
            rec.Slot = Items.Count;
            if (rec.Uses <= 0)
                rec.Uses = ItemCatalog.MaxUsesOf(rec.TemplateId);
            if (string.IsNullOrEmpty(rec.InstanceId))
                rec.InstanceId = Guid.NewGuid().ToString("N");
            Items.Add(rec);
        }

        public float TotalWeight() => ItemCatalog.WeightOf(Items);

        public bool Overweight(int str) => TotalWeight() > ItemCatalog.CarryCap(str);

        public bool CanCarry(int str, string templateId, int amount = 1)
        {
            int n = amount < 1 ? 1 : amount;
            return TotalWeight() + ItemCatalog.WeightOf(templateId) * n <= ItemCatalog.CarryCap(str);
        }

        public bool CanCarryWeight(int str, float extra)
        {
            return TotalWeight() + extra <= ItemCatalog.CarryCap(str);
        }

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
                int max = ExceptionalCraft.MaxUsesOf(Items[i]);
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
            int maxUses = ExceptionalCraft.MaxUsesOf(it);
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


        public string PouchInstanceId()
        {
            for (int i = 0; i < Items.Count; i++)
            {
                if (!ItemCatalog.IsContainer(Items[i].TemplateId))
                    continue;
                if (!string.IsNullOrEmpty(Items[i].ParentContainerId))
                    continue;
                if (string.IsNullOrEmpty(Items[i].InstanceId))
                {
                    var it = Items[i];
                    it.InstanceId = Guid.NewGuid().ToString("N");
                    Items[i] = it;
                }
                return Items[i].InstanceId;
            }
            return "";
        }

        public bool TryMoveToPouch(string templateId, string pouchInstanceId)
        {
            if (string.IsNullOrEmpty(templateId) || string.IsNullOrEmpty(pouchInstanceId))
                return false;
            if (ItemCatalog.IsContainer(templateId))
                return false;
            int pouchIdx = -1;
            for (int i = 0; i < Items.Count; i++)
            {
                var it = Items[i];
                if (it.InstanceId != pouchInstanceId || !ItemCatalog.IsContainer(it.TemplateId))
                    continue;
                if (!string.IsNullOrEmpty(it.ParentContainerId))
                    return false;
                pouchIdx = i;
                break;
            }
            if (pouchIdx < 0)
                return false;
            int itemIdx = -1;
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                var it = Items[i];
                if (it.TemplateId != templateId)
                    continue;
                if (!string.IsNullOrEmpty(it.ParentContainerId))
                    continue;
                if (ItemCatalog.IsContainer(it.TemplateId))
                    continue;
                itemIdx = i;
                break;
            }
            if (itemIdx < 0)
                return false;
            var item = Items[itemIdx];
            if (ItemCatalog.Stackable(templateId) && item.Amount > 1)
            {
                item.Amount -= 1;
                Items[itemIdx] = item;
                Add(new ItemRecord
                {
                    TemplateId = templateId,
                    Amount = 1,
                    Uses = item.Uses,
                    ParentContainerId = pouchInstanceId
                });
                return true;
            }
            item.ParentContainerId = pouchInstanceId;
            Items[itemIdx] = item;
            return true;
        }

        public bool TryTakeFromPouch(string templateId, string pouchInstanceId)
        {
            if (string.IsNullOrEmpty(templateId) || string.IsNullOrEmpty(pouchInstanceId))
                return false;
            int itemIdx = -1;
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                var it = Items[i];
                if (it.TemplateId != templateId)
                    continue;
                if ((it.ParentContainerId ?? "") != pouchInstanceId)
                    continue;
                itemIdx = i;
                break;
            }
            if (itemIdx < 0)
                return false;
            var taken = Items[itemIdx];
            if (ItemCatalog.Stackable(templateId) && taken.Amount > 1)
            {
                taken.Amount -= 1;
                Items[itemIdx] = taken;
                Add(new ItemRecord
                {
                    TemplateId = templateId,
                    Amount = 1,
                    Uses = taken.Uses,
                    ParentContainerId = ""
                });
                return true;
            }
            Items.RemoveAt(itemIdx);
            taken.ParentContainerId = "";
            Add(taken);
            return true;
        }

        public int CountInPouch(string templateId, string pouchInstanceId)
        {
            int n = 0;
            if (string.IsNullOrEmpty(pouchInstanceId))
                return 0;
            for (int i = 0; i < Items.Count; i++)
            {
                var it = Items[i];
                if (it.TemplateId != templateId)
                    continue;
                if ((it.ParentContainerId ?? "") != pouchInstanceId)
                    continue;
                n += it.Amount < 1 ? 1 : it.Amount;
            }
            return n;
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
            string parent = rec.ParentContainerId ?? "";
            if (ItemCatalog.Stackable(rec.TemplateId))
            {
                for (int i = 0; i < Items.Count; i++)
                {
                    var it = Items[i];
                    if (it.TemplateId != rec.TemplateId)
                        continue;
                    if ((it.ParentContainerId ?? "") != parent)
                        continue;
                    it.Amount += rec.Amount;
                    Items[i] = it;
                    return;
                }
            }
            rec.Slot = Items.Count;
            if (rec.Uses <= 0)
                rec.Uses = ItemCatalog.MaxUsesOf(rec.TemplateId);
            if (string.IsNullOrEmpty(rec.InstanceId))
                rec.InstanceId = Guid.NewGuid().ToString("N");
            Items.Add(rec);
        }

        public ItemRecord[] ToArray() => Items.ToArray();
    }
}
