using System.Collections.Generic;
using UnityEngine;
using Ulon.Shared;

namespace Ulon.Server
{
    public sealed partial class OfflineWorld
    {
        public AttackResult HandleDeath(WorldBody body, string ownerId)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            body.Ghost = true;
            if (body.Hp > 0f)
                body.SetHp(0f);
            var bag = Bag(body);
            var existing = FindCorpse(ownerId);
            if (existing != null)
                KillGo(existing.gameObject);
            var go = SpawnCorpseGo(body);
            var node = go.AddComponent<CorpseNode>();
            node.CorpseId = System.Guid.NewGuid().ToString("N");
            node.OwnerId = ownerId ?? "";
            node.LastKind = string.IsNullOrEmpty(body.DisplayName) ? "시체" : body.DisplayName;
            node.LastX = body.transform.position.x;
            node.LastY = body.transform.position.y;
            node.LastZ = body.transform.position.z;
            node.SpawnedAt = Time.time;
            node.DecaySeconds = 900f;
            for (int i = 0; i < bag.Items.Count; i++)
                node.Items.Add(bag.Items[i]);
            bag.Items.Clear();
            OpLog.Write("drop", ownerId ?? "", node.CorpseId, "corpse");
            return new AttackResult { Applied = true, Hit = true };
        }

        static GameObject SpawnCorpseGo(WorldBody body)
        {
            var go = new GameObject("Corpse");
            Vector3 pos = body != null ? body.transform.position : Vector3.zero;
            go.transform.position = pos;
            Transform src = body != null ? body.transform.Find("Visual") : null;
            if (src != null)
            {
                var vis = Object.Instantiate(src.gameObject, go.transform, false);
                vis.name = "Visual";
                vis.transform.localPosition = new Vector3(0f, 0.12f, 0f);
                float yaw = body.transform.eulerAngles.y;
                vis.transform.localRotation = Quaternion.Euler(90f, yaw, 0f);
                var anim = vis.GetComponent<Animator>();
                if (anim != null)
                    anim.enabled = false;
            }
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(0.9f, 0.4f, 1.6f);
            box.center = new Vector3(0f, 0.2f, 0f);
            return go;
        }

        public AttackResult TryResurrect(WorldBody body, HealerStation healer)
        {
            if (body == null || healer == null)
                return new AttackResult { FailReason = "no_healer" };
            if (!body.Ghost)
                return new AttackResult { FailReason = "not_ghost" };
            float dist = Vector3.Distance(body.transform.position, healer.transform.position);
            if (dist > healer.InteractRange)
                return new AttackResult { FailReason = "range" };
            body.Resurrect();
            return new AttackResult { Applied = true, Hit = true };
        }

        public AttackResult TryLootCorpse(WorldBody body, CorpseNode node)
        {
            if (body == null || node == null)
                return new AttackResult { FailReason = "no_corpse" };
            if (body.Ghost)
                return new AttackResult { FailReason = "ghost" };
            if (!LootAllowed(body, node))
                return new AttackResult { FailReason = "loot_right" };
            float dist = Vector3.Distance(body.transform.position, node.transform.position);
            if (dist > node.InteractRange)
                return new AttackResult { FailReason = "range" };
            var bag = Bag(body);
            for (int i = 0; i < node.Items.Count; i++)
                bag.Add(node.Items[i]);
            node.Items.Clear();
            KillGo(node.gameObject);
            OpLog.Write("drop", node.OwnerId, node.CorpseId, "loot");
            return new AttackResult { Applied = true, Hit = true };
        }

        public void RestoreCorpse(string ownerId, CharacterSnapshot snap)
        {
            if (snap == null || snap.Corpse == null || snap.Corpse.Length == 0)
                return;
            if (FindCorpse(ownerId) != null)
                return;
            var go = SpawnCorpseGo(Player);
            go.transform.position = new Vector3(snap.CorpseX, snap.CorpseY, snap.CorpseZ);
            var node = go.GetComponent<CorpseNode>();
            if (node == null)
                node = go.AddComponent<CorpseNode>();
            node.CorpseId = string.IsNullOrEmpty(snap.CorpseId) ? System.Guid.NewGuid().ToString("N") : snap.CorpseId;
            node.OwnerId = ownerId ?? "";
            node.LastKind = string.IsNullOrEmpty(snap.Name) ? "시체" : snap.Name;
            node.LastX = snap.CorpseX;
            node.LastY = snap.CorpseY;
            node.LastZ = snap.CorpseZ;
            node.SpawnedAt = Time.time;
            node.DecaySeconds = 900f;
            for (int i = 0; i < snap.Corpse.Length; i++)
                node.Items.Add(snap.Corpse[i]);
        }

        public static void WriteCorpse(CharacterSnapshot snap, string ownerId)
        {
            if (snap == null)
                return;
            var node = FindCorpse(ownerId);
            if (node == null)
            {
                snap.CorpseId = "";
                snap.Corpse = System.Array.Empty<ItemRecord>();
                return;
            }
            snap.CorpseId = node.CorpseId;
            snap.CorpseX = node.transform.position.x;
            snap.CorpseY = node.transform.position.y;
            snap.CorpseZ = node.transform.position.z;
            snap.Corpse = node.Items.ToArray();
        }


        public static LockedCrate FindCrate(string name)
        {
            var list = Object.FindObjectsByType<LockedCrate>(FindObjectsSortMode.None);
            for (int i = 0; i < list.Length; i++)
                if (list[i].gameObject.name == name)
                    return list[i];
            return list.Length > 0 ? list[0] : null;
        }

        public static VendorStation FindVendor(string name)
        {
            var list = Object.FindObjectsByType<VendorStation>(FindObjectsSortMode.None);
            for (int i = 0; i < list.Length; i++)
                if (list[i].gameObject.name == name)
                    return list[i];
            return null;
        }

        public AttackResult TryVendor(WorldBody body, VendorStation vendor)
        {
            if (body == null || vendor == null)
                return new AttackResult { FailReason = "no_vendor" };
            if (body.Ghost)
                return new AttackResult { FailReason = "ghost" };
            float dist = Vector3.Distance(body.transform.position, vendor.transform.position);
            if (dist > vendor.InteractRange)
                return new AttackResult { FailReason = "range" };
            ActiveVendor = vendor;
            return new AttackResult { Applied = true };
        }

        public string EquippedOf(WorldBody body)
        {
            if (body == null)
                return "";
            return equipped.TryGetValue(body.GetInstanceID(), out string id) ? id : "";
        }

        public AttackResult TryEquip(WorldBody body, string templateId)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            var bag = Bag(body);
            bool has = bag != null && ItemCatalog.Has(bag.Items, templateId);
            int req = ItemCatalog.StrReqOf(templateId);
            var result = EquipResolve.Equip(new EquipRequest
            {
                Ghost = body.Ghost,
                HasItem = has,
                Str = StatsOf(body).Str,
                StrReq = req,
                TemplateId = templateId
            });
            if (!result.Applied)
            {
                LastEquipMessage = EquipResolve.MessageFor(result.FailReason, templateId, req);
                return result;
            }
            equipped[body.GetInstanceID()] = templateId;
            LastEquipMessage = EquipResolve.MessageFor("", templateId, req);
            OpLog.Write("equip", PersistDriver.AccountKey(), body.DisplayName, templateId);
            return result;
        }

        public AttackResult TryUnequip(WorldBody body)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            string cur = EquippedOf(body);
            var result = EquipResolve.Unequip(new EquipRequest
            {
                Ghost = body.Ghost,
                TemplateId = cur
            });
            if (!result.Applied)
            {
                LastEquipMessage = EquipResolve.MessageFor(result.FailReason, cur, 0);
                return result;
            }
            equipped.Remove(body.GetInstanceID());
            LastEquipMessage = "해제: " + cur;
            OpLog.Write("unequip", PersistDriver.AccountKey(), body.DisplayName, cur);
            return result;
        }

        public AttackResult TryBuy(WorldBody body, string templateId)
        {
            if (body == null || ActiveVendor == null)
                return new AttackResult { FailReason = "no_vendor" };
            if (body.Ghost)
                return new AttackResult { FailReason = "ghost" };
            int price = ItemCatalog.BuyPrice(templateId);
            if (price <= 0)
                return new AttackResult { FailReason = "not_sold" };
            if (body.Gold < price)
                return new AttackResult { FailReason = "gold" };
            var bag = Bag(body);
            if (!bag.CanCarry(StatsOf(body).Str, templateId, 1))
            {
                LastWeightMessage = WeightRefuseMessage(StatsOf(body).Str, bag);
                return new AttackResult { FailReason = "overweight" };
            }
            bag.Add(templateId, 1);
            body.Gold -= price;
            return new AttackResult { Applied = true, Hit = true };
        }

        public AttackResult TrySell(WorldBody body, string templateId)
        {
            if (body == null || ActiveVendor == null)
                return new AttackResult { FailReason = "no_vendor" };
            int price = ItemCatalog.SellPrice(templateId);
            if (price <= 0)
                return new AttackResult { FailReason = "not_bought" };
            var bag = Bag(body);
            if (!bag.TakeOne(templateId))
                return new AttackResult { FailReason = "missing" };
            body.Gold += price;
            return new AttackResult { Applied = true, Hit = true };
        }

        public static TrainerStation FindTrainer(string name)
        {
            var list = Object.FindObjectsByType<TrainerStation>(FindObjectsSortMode.None);
            for (int i = 0; i < list.Length; i++)
                if (list[i].gameObject.name == name)
                    return list[i];
            return null;
        }

        public AttackResult TryTrainer(WorldBody body, TrainerStation trainer)
        {
            if (body == null || trainer == null)
                return new AttackResult { FailReason = "no_trainer" };
            if (body.Ghost)
                return new AttackResult { FailReason = "ghost" };
            float dist = Vector3.Distance(body.transform.position, trainer.transform.position);
            if (dist > trainer.InteractRange)
                return new AttackResult { FailReason = "range" };
            ActiveTrainer = trainer;
            return new AttackResult { Applied = true };
        }

        public AttackResult TryTrain(WorldBody body, SkillId skill)
        {
            if (body == null || ActiveTrainer == null)
                return new AttackResult { FailReason = "no_trainer" };
            if (body.Ghost)
                return new AttackResult { FailReason = "ghost" };
            var trainer = ActiveTrainer;
            var skills = SkillsOf(body);
            float cur = skills.Get(skill);
            if (cur >= trainer.Cap - 0.0001f)
                return new AttackResult { FailReason = "trained" };
            if (body.Gold < trainer.Cost)
                return new AttackResult { FailReason = "gold" };
            float next = cur + 1f;
            if (next > trainer.Cap)
                next = trainer.Cap;
            if (!skills.TrySet(skill, next))
                return new AttackResult { FailReason = "cap" };
            body.Gold -= trainer.Cost;
            return new AttackResult { Applied = true, Hit = true, SkillBefore = cur, SkillAfter = skills.Get(skill) };
        }

    }
}
