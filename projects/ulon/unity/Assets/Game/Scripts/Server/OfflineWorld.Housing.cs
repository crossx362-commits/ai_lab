using System.Collections.Generic;
using UnityEngine;
using Ulon.Shared;

namespace Ulon.Server
{
    public sealed partial class OfflineWorld
    {
        bool ActorOwnsAnyHouse(string characterId, string accountId)
        {
            foreach (var kv in houses)
            {
                var rec = kv.Value;
                if (rec == null || string.IsNullOrEmpty(rec.OwnerCharacterId))
                    continue;
                if (!string.IsNullOrEmpty(characterId) && rec.OwnerCharacterId == characterId)
                    return true;
                if (!string.IsNullOrEmpty(accountId) && rec.OwnerAccountId == accountId)
                    return true;
            }
            return false;
        }

        HouseRequest MakeHouseRequest(WorldBody body, Vector3 at, float range, HouseRecord rec, bool hasItem, bool chestHas)
        {
            string cid = CharacterOf(body);
            string aid = AccountOf(body);
            return new HouseRequest
            {
                PlotExists = true,
                Occupied = rec != null && !string.IsNullOrEmpty(rec.OwnerCharacterId),
                ActorOwnsHouse = ActorOwnsAnyHouse(cid, aid),
                Distance = body != null ? Vector3.Distance(body.transform.position, at) : 99f,
                Range = range,
                Ghost = body != null && body.Ghost,
                Gold = body != null ? body.Gold : 0,
                ActorCharacterId = cid,
                ActorAccountId = aid,
                OwnerCharacterId = rec != null ? rec.OwnerCharacterId : "",
                OwnerAccountId = rec != null ? rec.OwnerAccountId : "",
                HasBackpackItem = hasItem,
                ChestHasItem = chestHas
            };
        }

        void ShowHouse(string plotId, bool claimed)
        {
            var plot = GameObject.Find(HousingPlot.RootObject);
            if (plot == null)
                return;
            Transform house = plot.transform.Find(HousingPlot.HouseObject);
            if (house != null)
                house.gameObject.SetActive(claimed);
        }

        public AttackResult TryClaimHouse(WorldBody body, HousePlotStation station)
        {
            if (body == null || station == null)
                return new AttackResult { FailReason = "no_plot" };
            string plotId = string.IsNullOrEmpty(station.PlotId) ? HousingPlot.Id : station.PlotId;
            var rec = RecordOf(plotId);
            var req = MakeHouseRequest(body, station.transform.position, station.InteractRange, rec, false, false);
            var result = HouseResolve.Claim(req);
            if (!result.Applied)
                return result;
            rec.OwnerCharacterId = CharacterOf(body);
            rec.OwnerAccountId = AccountOf(body);
            rec.PublicFlag = 0;
            body.Gold -= HousingPlot.ClaimGold;
            ShowHouse(plotId, true);
            PersistPlot(plotId);
            return result;
        }

        public AttackResult TryLockdown(WorldBody body, HouseChest chest, string templateId = null)
        {
            if (body == null || chest == null)
                return new AttackResult { FailReason = "no_plot" };
            string plotId = string.IsNullOrEmpty(chest.PlotId) ? HousingPlot.Id : chest.PlotId;
            var rec = RecordOf(plotId);
            var bag = Bag(body);
            if (string.IsNullOrEmpty(templateId))
                templateId = ItemCatalog.Cloth;
            bool has = CountItem(bag, templateId) > 0;
            var req = MakeHouseRequest(body, chest.transform.position, chest.InteractRange, rec, has, rec.Items.Count > 0);
            var result = HouseResolve.Lockdown(req);
            if (!result.Applied)
                return result;
            ItemRecord moved = default;
            moved.TemplateId = templateId;
            moved.Amount = 1;
            for (int i = 0; i < bag.Items.Count; i++)
            {
                if (bag.Items[i].TemplateId != templateId)
                    continue;
                moved = bag.Items[i];
                moved.Amount = 1;
                break;
            }
            ConsumeItem(bag, templateId, 1);
            moved.Slot = rec.Items.Count;
            rec.Items.Add(moved);
            PersistPlot(plotId);
            return result;
        }

        public AttackResult TrySecureTake(WorldBody body, HouseChest chest)
        {
            if (body == null || chest == null)
                return new AttackResult { FailReason = "no_plot" };
            string plotId = string.IsNullOrEmpty(chest.PlotId) ? HousingPlot.Id : chest.PlotId;
            var rec = RecordOf(plotId);
            var req = MakeHouseRequest(body, chest.transform.position, chest.InteractRange, rec, false, rec.Items.Count > 0);
            var result = HouseResolve.SecureTake(req);
            if (!result.Applied)
                return result;
            ItemRecord it = rec.Items[rec.Items.Count - 1];
            rec.Items.RemoveAt(rec.Items.Count - 1);
            Bag(body).Add(it);
            PersistPlot(plotId);
            return result;
        }

        public bool OwnsPlot(WorldBody body, string plotId)
        {
            var rec = RecordOf(plotId);
            return rec != null && HouseResolve.IsOwner(new HouseRequest
            {
                ActorCharacterId = CharacterOf(body),
                OwnerCharacterId = rec.OwnerCharacterId
            });
        }

        bool VendorListed(HouseRecord rec)
        {
            return rec != null && !string.IsNullOrEmpty(rec.VendorItem.TemplateId) && rec.VendorItem.Amount > 0;
        }

        public AttackResult TryListVendor(WorldBody body, HouseVendor vendor, string templateId = null)
        {
            if (body == null || vendor == null)
                return new AttackResult { FailReason = "no_plot" };
            string plotId = string.IsNullOrEmpty(vendor.PlotId) ? HousingPlot.Id : vendor.PlotId;
            var rec = RecordOf(plotId);
            var bag = Bag(body);
            if (string.IsNullOrEmpty(templateId))
                templateId = ItemCatalog.Cloth;
            bool has = CountItem(bag, templateId) > 0;
            int price = ItemCatalog.BuyPrice(templateId);
            var req = MakeHouseRequest(body, vendor.transform.position, vendor.InteractRange, rec, has, rec.Items.Count > 0);
            req.PublicHouse = rec.PublicFlag != 0;
            req.VendorSlotFull = VendorListed(rec);
            req.VendorHasItem = VendorListed(rec);
            req.VendorPrice = price;
            var result = HouseResolve.ListVendor(req);
            if (!result.Applied)
                return result;
            ItemRecord moved = default;
            moved.TemplateId = templateId;
            moved.Amount = 1;
            for (int i = 0; i < bag.Items.Count; i++)
            {
                if (bag.Items[i].TemplateId != templateId)
                    continue;
                moved = bag.Items[i];
                moved.Amount = 1;
                break;
            }
            ConsumeItem(bag, templateId, 1);
            moved.Slot = HousingPlot.VendorSlot;
            rec.VendorItem = moved;
            rec.PublicFlag = 1;
            PersistPlot(plotId);
            return result;
        }

        public AttackResult TryBuyHouseVendor(WorldBody body, HouseVendor vendor)
        {
            if (body == null || vendor == null)
                return new AttackResult { FailReason = "no_plot" };
            string plotId = string.IsNullOrEmpty(vendor.PlotId) ? HousingPlot.Id : vendor.PlotId;
            var rec = RecordOf(plotId);
            int price = VendorListed(rec) ? ItemCatalog.BuyPrice(rec.VendorItem.TemplateId) : 0;
            var req = MakeHouseRequest(body, vendor.transform.position, vendor.InteractRange, rec, false, rec.Items.Count > 0);
            req.PublicHouse = rec.PublicFlag != 0;
            req.VendorSlotFull = VendorListed(rec);
            req.VendorHasItem = VendorListed(rec);
            req.VendorPrice = price;
            var result = HouseResolve.BuyVendor(req);
            if (!result.Applied)
                return result;
            ItemRecord sold = rec.VendorItem;
            rec.VendorItem = default;
            body.Gold -= price;
            var owner = FindOwnerBody(rec.OwnerCharacterId);
            if (owner != null)
                owner.Gold += price;
            Bag(body).Add(sold);
            PersistPlot(plotId);
            return result;
        }

        WorldBody FindOwnerBody(string characterId)
        {
            if (string.IsNullOrEmpty(characterId))
                return null;
            var list = Object.FindObjectsByType<WorldBody>(FindObjectsSortMode.None);
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] != null && list[i].CharacterId == characterId)
                    return list[i];
            }
            return null;
        }

        public void ResetHousePlot(string plotId = null)
        {
            if (string.IsNullOrEmpty(plotId))
                plotId = HousingPlot.Id;
            var rec = RecordOf(plotId);
            rec.OwnerCharacterId = "";
            rec.OwnerAccountId = "";
            rec.PublicFlag = 0;
            rec.Items.Clear();
            rec.VendorItem = default;
            ShowHouse(plotId, false);
            PersistPlot(plotId);
        }

        public void LoadPlot(string plotId)
        {
            var snap = CharacterStore.LoadHouse(plotId);
            if (snap == null)
                return;
            var rec = RecordOf(string.IsNullOrEmpty(snap.PlotId) ? plotId : snap.PlotId);
            rec.OwnerCharacterId = snap.OwnerCharacterId ?? "";
            rec.OwnerAccountId = snap.AccountId ?? "";
            rec.PublicFlag = snap.PublicFlag;
            rec.Items.Clear();
            rec.VendorItem = default;
            if (snap.Items != null)
            {
                for (int i = 0; i < snap.Items.Length; i++)
                {
                    var it = snap.Items[i];
                    if (string.IsNullOrEmpty(it.TemplateId) || it.Amount <= 0)
                        continue;
                    if (it.Slot >= HousingPlot.VendorSlot)
                        rec.VendorItem = it;
                    else
                        rec.Items.Add(it);
                }
            }
            ShowHouse(rec.PlotId, !string.IsNullOrEmpty(rec.OwnerCharacterId));
        }

        void PersistPlot(string plotId)
        {
            if (!Application.isPlaying)
                return;
            var rec = RecordOf(plotId);
            int extra = VendorListed(rec) ? 1 : 0;
            var items = new ItemRecord[rec.Items.Count + extra];
            for (int i = 0; i < rec.Items.Count; i++)
            {
                var it = rec.Items[i];
                it.Slot = i;
                items[i] = it;
            }
            if (extra > 0)
            {
                var v = rec.VendorItem;
                v.Slot = HousingPlot.VendorSlot;
                items[rec.Items.Count] = v;
            }
            CharacterStore.SaveHouse(new HouseSnapshot
            {
                PlotId = rec.PlotId,
                OwnerCharacterId = rec.OwnerCharacterId,
                AccountId = rec.OwnerAccountId,
                PublicFlag = rec.PublicFlag,
                Items = items
            });
        }

    }
}
