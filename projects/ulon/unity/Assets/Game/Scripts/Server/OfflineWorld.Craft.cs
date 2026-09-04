using System.Collections.Generic;
using UnityEngine;
using Ulon.Shared;

namespace Ulon.Server
{
    public sealed partial class OfflineWorld
    {
        public AttackResult TryMoveToPouch(WorldBody body, string templateId, string pouchInstanceId)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            if (body.Ghost)
                return new AttackResult { FailReason = "ghost" };
            var bag = Bag(body);
            if (string.IsNullOrEmpty(pouchInstanceId))
                pouchInstanceId = bag.PouchInstanceId();
            if (string.IsNullOrEmpty(pouchInstanceId))
                return new AttackResult { FailReason = "no_pouch" };
            if (ItemCatalog.IsContainer(templateId))
                return new AttackResult { FailReason = "nested_depth" };
            if (!bag.TryMoveToPouch(templateId, pouchInstanceId))
                return new AttackResult { FailReason = "no_item" };
            return new AttackResult { Applied = true, Hit = true };
        }

        public AttackResult TryTakeFromPouch(WorldBody body, string templateId, string pouchInstanceId)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            if (body.Ghost)
                return new AttackResult { FailReason = "ghost" };
            var bag = Bag(body);
            if (string.IsNullOrEmpty(pouchInstanceId))
                pouchInstanceId = bag.PouchInstanceId();
            if (string.IsNullOrEmpty(pouchInstanceId))
                return new AttackResult { FailReason = "no_pouch" };
            if (!bag.TryTakeFromPouch(templateId, pouchInstanceId))
                return new AttackResult { FailReason = "no_item" };
            return new AttackResult { Applied = true, Hit = true };
        }

        public AttackResult DepositAll(WorldBody body)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            var bag = Bag(body);
            if (bag.Items.Count == 0)
                return new AttackResult { FailReason = "empty_bag" };
            var vault = Vault(body);
            for (int i = 0; i < bag.Items.Count; i++)
                vault.Add(bag.Items[i]);
            bag.Items.Clear();
            return new AttackResult { Applied = true, Hit = true };
        }

        public AttackResult WithdrawAll(WorldBody body)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            var vault = Vault(body);
            if (vault.Items.Count == 0)
                return new AttackResult { FailReason = "empty_bank" };
            var bag = Bag(body);
            for (int i = 0; i < vault.Items.Count; i++)
                bag.Add(vault.Items[i]);
            vault.Items.Clear();
            return new AttackResult { Applied = true, Hit = true };
        }

        public AttackResult TryCraft(WorldBody body, CraftStation station)
        {
            return TryCraft(body, station, null);
        }

        public AttackResult TryCraft(WorldBody body, CraftStation station, string recipeId)
        {
            if (body == null || station == null)
                return new AttackResult { FailReason = "no_station" };
            if (body.Ghost)
                return new AttackResult { FailReason = "ghost" };
            float dist = Vector3.Distance(body.transform.position, station.transform.position);
            if (dist > station.InteractRange)
                return new AttackResult { FailReason = "range" };
            var home = CraftRecipes.Find(station.RecipeId);
            var recipe = CraftRecipes.Find(string.IsNullOrEmpty(recipeId) ? station.RecipeId : recipeId);
            if (recipe == null || home == null)
                return new AttackResult { FailReason = "unknown_recipe" };
            if (recipe.Skill != home.Skill)
                return new AttackResult { FailReason = "wrong_station" };
            var bag = body.GetComponent<InventoryBag>() ?? body.gameObject.AddComponent<InventoryBag>();
            int have = CountItem(bag, recipe.Ingredient);
            if (have < recipe.Count)
            {
                if (!recipe.CanRepair || have < 1 || !bag.RepairOne(10))
                    return new AttackResult { FailReason = "missing_" + recipe.Ingredient };
                ConsumeItem(bag, recipe.Ingredient, 1);
                return new AttackResult { Applied = true, Hit = true };
            }
            float projected = bag.TotalWeight()
                - ItemCatalog.WeightOf(recipe.Ingredient) * recipe.Count
                + ItemCatalog.WeightOf(recipe.Output);
            if (projected > ItemCatalog.CarryCap(StatsOf(body).Str))
            {
                LastWeightMessage = WeightRefuseMessage(StatsOf(body).Str, bag);
                return new AttackResult { FailReason = "overweight" };
            }
            ConsumeItem(bag, recipe.Ingredient, recipe.Count);
            var skills = SkillsOf(body);
            int uses = ItemCatalog.MaxUsesOf(recipe.Output);
            string maker = uses > 0 ? MakerOf(body) : "";
            bool exceptional = uses > 0 && ExceptionalCraft.Roll(skills.Get(recipe.Skill));
            if (exceptional)
                uses += ExceptionalCraft.UsesBonus;
            var made = new ItemRecord
            {
                TemplateId = recipe.Output,
                Amount = 1,
                Uses = uses,
                MakerId = maker,
                Exceptional = exceptional
            };
            bag.Add(made);
            SkillGain.TryRaise(skills, recipe.Skill, recipe.Difficulty, out float before, out float after, StatsOf(body));
            if (body.IsAvatar)
                body.RecalcFromStr(StatsOf(body).Str);
            OpLog.Write("craft", PersistDriver.AccountKey(), station.gameObject.name, recipe.Output);
            return new AttackResult { Applied = true, Hit = true, SkillBefore = before, SkillAfter = after };
        }

        static string MakerOf(WorldBody body)
        {
            if (body != null && !string.IsNullOrEmpty(body.CharacterId))
                return body.CharacterId;
            return PersistDriver.AccountKey();
        }

        public AttackResult TryAcceptOrder(WorldBody body, CraftStation station = null)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            if (station == null)
                station = FindStation("Forge");
            float dist = 999f;
            bool has = station != null;
            if (has)
                dist = Vector3.Distance(body.transform.position, station.transform.position);
            if (!has || dist > CraftOrderRules.InteractRange)
            {
                var vendor = FindVendor("Vendor");
                if (vendor != null)
                {
                    float vd = Vector3.Distance(body.transform.position, vendor.transform.position);
                    if (vd <= vendor.InteractRange)
                    {
                        has = true;
                        dist = vd;
                    }
                }
            }
            var gate = CraftOrderResolve.Accept(new CraftOrderRequest
            {
                Ghost = body.Ghost,
                HasStation = has,
                Distance = dist,
                Range = CraftOrderRules.InteractRange,
                ActiveOrder = body.ActiveCraftOrder ?? "",
                OfferItem = CraftOrderRules.DefaultItem
            });
            if (!gate.Applied)
            {
                LastCraftOrderMessage = CraftOrderMessage(gate.FailReason, body.ActiveCraftOrder);
                return gate;
            }
            body.ActiveCraftOrder = CraftOrderRules.DefaultItem;
            LastCraftOrderMessage = "제작의뢰 수락: " + CraftOrderRules.DefaultItem + " x" + CraftOrderRules.Amount;
            OpLog.Write("craft_order_accept", PersistDriver.AccountKey(), body.DisplayName, body.ActiveCraftOrder);
            return gate;
        }

        public AttackResult TryTurnInOrder(WorldBody body, CraftStation station = null)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            if (station == null)
                station = FindStation("Forge");
            float dist = 999f;
            bool has = station != null;
            if (has)
                dist = Vector3.Distance(body.transform.position, station.transform.position);
            if (!has || dist > CraftOrderRules.InteractRange)
            {
                var vendor = FindVendor("Vendor");
                if (vendor != null)
                {
                    float vd = Vector3.Distance(body.transform.position, vendor.transform.position);
                    if (vd <= vendor.InteractRange)
                    {
                        has = true;
                        dist = vd;
                    }
                }
            }
            string order = body.ActiveCraftOrder ?? "";
            var bag = body.GetComponent<InventoryBag>();
            string maker = MakerOf(body);
            bool match = HasCraftedByMaker(bag, order, maker);
            var gate = CraftOrderResolve.TurnIn(new CraftOrderRequest
            {
                Ghost = body.Ghost,
                HasStation = has,
                Distance = dist,
                Range = CraftOrderRules.InteractRange,
                ActiveOrder = order,
                HasMatchingCrafted = match
            });
            if (!gate.Applied)
            {
                LastCraftOrderMessage = CraftOrderMessage(gate.FailReason, order);
                return gate;
            }
            if (!TakeCraftedByMaker(bag, order, maker))
            {
                LastCraftOrderMessage = CraftOrderMessage("wrong_item", order);
                return new AttackResult { FailReason = "wrong_item" };
            }
            body.Gold += CraftOrderRules.GoldReward;
            body.ActiveCraftOrder = "";
            var skills = SkillsOf(body);
            SkillGain.TryRaise(skills, SkillId.Blacksmithing, CraftOrderRules.SkillDifficulty, out float before, out float after, StatsOf(body));
            LastCraftOrderMessage = "제작의뢰 납품 +" + CraftOrderRules.GoldReward + "G";
            OpLog.Write("craft_order_turnin", PersistDriver.AccountKey(), body.DisplayName, order);
            return new AttackResult { Applied = true, Hit = true, Damage = CraftOrderRules.GoldReward, SkillBefore = before, SkillAfter = after };
        }

        static string CraftOrderMessage(string reason, string order)
        {
            if (reason == "ghost") return "유령은 제작의뢰를 할 수 없습니다.";
            if (reason == "no_station") return "대장간/상점이 필요합니다.";
            if (reason == "range") return "대장간/상점에 더 가까이 가십시오.";
            if (reason == "already") return "이미 진행 중인 제작의뢰가 있습니다.";
            if (reason == "no_order") return "수락한 제작의뢰가 없습니다.";
            if (reason == "wrong_item") return "의뢰 품목(직접 제작)이 가방에 없습니다: " + order;
            return reason ?? "";
        }

        static bool HasCraftedByMaker(InventoryBag bag, string templateId, string makerId)
        {
            if (bag == null || string.IsNullOrEmpty(templateId))
                return false;
            for (int i = 0; i < bag.Items.Count; i++)
            {
                var it = bag.Items[i];
                if (it.TemplateId != templateId || it.Amount <= 0)
                    continue;
                if (it.MakerId == makerId)
                    return true;
            }
            return false;
        }

        static bool TakeCraftedByMaker(InventoryBag bag, string templateId, string makerId)
        {
            if (bag == null || string.IsNullOrEmpty(templateId))
                return false;
            for (int i = bag.Items.Count - 1; i >= 0; i--)
            {
                var it = bag.Items[i];
                if (it.TemplateId != templateId || it.Amount <= 0)
                    continue;
                if (it.MakerId != makerId)
                    continue;
                if (ItemCatalog.Stackable(templateId))
                {
                    it.Amount -= 1;
                    if (it.Amount <= 0)
                        bag.Items.RemoveAt(i);
                    else
                        bag.Items[i] = it;
                }
                else
                    bag.Items.RemoveAt(i);
                return true;
            }
            return false;
        }

        static int CountItem(InventoryBag bag, string template)
        {
            int n = 0;
            for (int i = 0; i < bag.Items.Count; i++)
                if (bag.Items[i].TemplateId == template)
                    n += bag.Items[i].Amount;
            return n;
        }

        static void ConsumeItem(InventoryBag bag, string template, int amount)
        {
            int left = amount;
            for (int i = bag.Items.Count - 1; i >= 0 && left > 0; i--)
            {
                var it = bag.Items[i];
                if (it.TemplateId != template)
                    continue;
                int take = it.Amount < left ? it.Amount : left;
                it.Amount -= take;
                left -= take;
                if (it.Amount <= 0)
                    bag.Items.RemoveAt(i);
                else
                    bag.Items[i] = it;
            }
        }

    }
}
