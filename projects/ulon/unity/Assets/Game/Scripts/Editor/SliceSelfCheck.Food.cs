using System;
using System.IO;
using FishNet.Object;
using Ulon.Server;
using Ulon.Shared;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        // **먹고 마시는 것**(랩 ㉯) — 낚시·요리·연금.
        // 담는 것: 재료를 구해 끓이고 담그는 규칙(한 줄기로 이어지는 손). 안 담는 것: 무기·치유·땅.
        // 낚시가 요리와 한 파일인 이유: 물에서 건진 것이 그대로 냄비로 간다 — 고칠 때 같이 열린다.
        static void AssertFishingSlice()
        {
            if (StatSet.PrimaryOf(SkillId.Fishing) != StatId.Dex)
                throw new InvalidOperationException("낚시 Primary는 DEX이어야 합니다.");
            if (ItemCatalog.ToolFor(SkillId.Fishing) != ItemCatalog.FishingPole)
                throw new InvalidOperationException("낚시는 낚싯대가 도구여야 합니다.");
            if (ItemCatalog.MaxUsesOf(ItemCatalog.FishingPole) != 20)
                throw new InvalidOperationException("낚싯대 내구 20");
            if (ItemCatalog.BuyPrice(ItemCatalog.FishingPole) <= 0 || ItemCatalog.SellPrice(ItemCatalog.Fish) <= 0)
                throw new InvalidOperationException("낚싯대/생선 상점 가격이 없습니다.");

            var gain = new SkillSet();
            var stats = new StatSet();
            int dexWas = stats.Dex;
            SkillGain.TryRaise(gain, SkillId.Fishing, 10f, out _, out float after, stats);
            if (Math.Abs(after - 0.1f) > 0.0001f)
                throw new InvalidOperationException("낚시 0.0→0.1이어야 합니다.");
            if (stats.Dex != dexWas + 1)
                throw new InvalidOperationException("낚시 상승 시 DEX가 올라야 합니다.");

            var created = CharacterCreate.Build("fish-check", "낚시", 0, 20, 40, 20,
                new[] { SkillId.Fishing, SkillId.Mining, SkillId.Swordsmanship },
                new[] { 50f, 30f, 20f });
            bool hasPole = false;
            for (int i = 0; i < created.Inventory.Length; i++)
                if (created.Inventory[i].TemplateId == ItemCatalog.FishingPole && created.Inventory[i].Uses == 20)
                    hasPole = true;
            if (!hasPole)
                throw new InvalidOperationException("낚시 시작은 낚싯대를 줘야 합니다.");

            var spot = GameObject.Find("FishingSpot");
            if (spot == null)
                throw new InvalidOperationException("마을에 물가(FishingSpot)가 있어야 합니다.");
            var sceneNode = spot.GetComponent<ResourceNode>();
            if (sceneNode == null || sceneNode.GatherSkill != SkillId.Fishing || sceneNode.ResourceId != ItemCatalog.Fish)
                throw new InvalidOperationException("물가는 낚시 ResourceNode여야 합니다.");

            var go = new GameObject("selfcheck-fish");
            GameObject worldGo = null;
            GameObject nodeGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-fish-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                var bag = go.AddComponent<InventoryBag>();
                nodeGo = new GameObject("selfcheck-fish-node");
                nodeGo.transform.position = go.transform.position;
                var node = nodeGo.AddComponent<ResourceNode>();
                node.ResourceId = ItemCatalog.Fish;
                node.DisplayName = "물가";
                node.GatherSkill = SkillId.Fishing;
                node.Remaining = 5;
                node.Capacity = 5;
                node.Difficulty = 10f;
                var noTool = world.TryGather(body, node);
                if (noTool.Applied)
                    throw new InvalidOperationException("낚싯대 없이 낚시되면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Fishing)) > 0.0001f)
                    throw new InvalidOperationException("실패한 낚시는 스킬을 올리면 안 됩니다.");
                bag.Add(new ItemRecord { TemplateId = ItemCatalog.FishingPole, Amount = 1, Uses = 1 });
                var ok = world.TryGather(body, node);
                if (!ok.Applied)
                    throw new InvalidOperationException("낚싯대 낚시 실패: " + ok.FailReason);
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Fishing) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 낚시 후 0.1이어야 합니다.");
                int fish = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.Fish)
                        fish += bag.Items[i].Amount;
                if (fish < 1)
                    throw new InvalidOperationException("잡은 생선이 가방에 있어야 합니다.");
                var broken = world.TryGather(body, node);
                if (broken.Applied)
                    throw new InvalidOperationException("내구 0 낚싯대로 낚시되면 안 됩니다.");

                var locked = new SkillSet();
                locked.SetLock(SkillId.Fishing, SkillLock.Locked);
                SkillGain.TryRaise(locked, SkillId.Fishing, 10f, out _, out _);
                if (Math.Abs(locked.Get(SkillId.Fishing)) > 0.0001f)
                    throw new InvalidOperationException("잠긴 낚시는 오르면 안 됩니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (nodeGo != null)
                    UnityEngine.Object.DestroyImmediate(nodeGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }
        static void AssertCookingSlice()
        {
            if (StatSet.PrimaryOf(SkillId.Cooking) != StatId.Dex)
                throw new InvalidOperationException("요리 Primary는 DEX이어야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Cooking) != "요리" || SkillTitles.JobOf(SkillId.Cooking) != "요리사")
                throw new InvalidOperationException("요리 스킬명/직업명이 기획과 같아야 합니다.");
            var fishRec = CraftRecipes.Find("cooked_fish");
            if (fishRec == null || fishRec.Ingredient != ItemCatalog.Fish || fishRec.Output != ItemCatalog.CookedFood
                || fishRec.Skill != SkillId.Cooking || fishRec.Count != 1)
                throw new InvalidOperationException("생선 1 → 요리음식 레시피가 있어야 합니다.");
            var wrap = CraftRecipes.Find("cooked_wrap");
            if (wrap == null || wrap.Ingredient != ItemCatalog.Cloth || wrap.Output != ItemCatalog.CookedFood
                || wrap.Skill != SkillId.Cooking)
                throw new InvalidOperationException("천(재봉 인접) → 요리음식 레시피가 있어야 합니다.");
            if (ItemCatalog.SellPrice(ItemCatalog.CookedFood) <= 0 || ItemCatalog.WeightOf(ItemCatalog.CookedFood) <= 0f)
                throw new InvalidOperationException("요리음식 무게/가격이 없습니다.");

            var gain = new SkillSet();
            var stats = new StatSet();
            int dexWas = stats.Dex;
            SkillGain.TryRaise(gain, SkillId.Cooking, 10f, out _, out float after, stats);
            if (Math.Abs(after - 0.1f) > 0.0001f)
                throw new InvalidOperationException("요리 0.0→0.1이어야 합니다.");
            if (stats.Dex != dexWas + 1)
                throw new InvalidOperationException("요리 상승 시 DEX가 올라야 합니다.");

            var created = CharacterCreate.Build("cook-check", "요리", 0, 20, 40, 20,
                new[] { SkillId.Cooking, SkillId.Fishing, SkillId.Swordsmanship },
                new[] { 50f, 30f, 20f });
            bool hasFish = false, hasPole = false;
            for (int i = 0; i < created.Inventory.Length; i++)
            {
                if (created.Inventory[i].TemplateId == ItemCatalog.Fish && created.Inventory[i].Amount >= 1)
                    hasFish = true;
                if (created.Inventory[i].TemplateId == ItemCatalog.FishingPole)
                    hasPole = true;
            }
            if (!hasFish)
                throw new InvalidOperationException("요리 시작은 생선을 줘야 합니다.");
            if (!hasPole)
                throw new InvalidOperationException("낚시 시작은 낚싯대를 줘야 합니다.");

            var fire = GameObject.Find("Campfire");
            if (fire == null)
                throw new InvalidOperationException("마을에 화덕(Campfire)이 있어야 합니다.");
            var sceneSt = fire.GetComponent<CraftStation>();
            if (sceneSt == null || sceneSt.RecipeId != "cooked_fish")
                throw new InvalidOperationException("화덕은 생선 요리 CraftStation이어야 합니다.");

            var go = new GameObject("selfcheck-cook");
            GameObject worldGo = null;
            GameObject stGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-cook-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                var bag = go.AddComponent<InventoryBag>();
                stGo = new GameObject("selfcheck-cook-st");
                stGo.transform.position = go.transform.position;
                var station = stGo.AddComponent<CraftStation>();
                station.RecipeId = "cooked_fish";
                station.DisplayName = "화덕";
                var noIng = world.TryCraft(body, station);
                if (noIng.Applied)
                    throw new InvalidOperationException("재료 없이 요리되면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Cooking)) > 0.0001f)
                    throw new InvalidOperationException("실패한 요리는 스킬을 올리면 안 됩니다.");
                bag.Add(ItemCatalog.Fish, 1);
                var ok = world.TryCraft(body, station);
                if (!ok.Applied)
                    throw new InvalidOperationException("생선 요리 실패: " + ok.FailReason);
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Cooking) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 요리 후 0.1이어야 합니다.");
                int cooked = 0, fishLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    if (bag.Items[i].TemplateId == ItemCatalog.CookedFood)
                        cooked += bag.Items[i].Amount;
                    if (bag.Items[i].TemplateId == ItemCatalog.Fish)
                        fishLeft += bag.Items[i].Amount;
                }
                if (cooked < 1 || fishLeft != 0)
                    throw new InvalidOperationException("생선 1 → 요리음식 1이어야 합니다.");

                bag.Add(ItemCatalog.Cloth, 1);
                var wrapOk = world.TryCraft(body, station, "cooked_wrap");
                if (!wrapOk.Applied)
                    throw new InvalidOperationException("천 요리 실패: " + wrapOk.FailReason);

                var locked = new SkillSet();
                locked.SetLock(SkillId.Cooking, SkillLock.Locked);
                SkillGain.TryRaise(locked, SkillId.Cooking, 10f, out _, out _);
                if (Math.Abs(locked.Get(SkillId.Cooking)) > 0.0001f)
                    throw new InvalidOperationException("잠긴 요리는 오르면 안 됩니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (stGo != null)
                    UnityEngine.Object.DestroyImmediate(stGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }

        static void AssertAlchemySlice()
        {
            if (StatSet.PrimaryOf(SkillId.Alchemy) != StatId.Int)
                throw new InvalidOperationException("연금술 Primary는 INT이어야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Alchemy) != "연금술" || SkillTitles.JobOf(SkillId.Alchemy) != "연금술사")
                throw new InvalidOperationException("연금술 스킬명/직업명이 기획과 같아야 합니다.");
            var rec = CraftRecipes.Find("health_potion");
            if (rec == null || rec.Ingredient != ItemCatalog.Cloth || rec.Output != ItemCatalog.HealthPotion
                || rec.Skill != SkillId.Alchemy || rec.Count != 1)
                throw new InvalidOperationException("천 1 → 회복물약 레시피가 있어야 합니다.");
            if (ItemCatalog.SellPrice(ItemCatalog.HealthPotion) <= 0 || ItemCatalog.WeightOf(ItemCatalog.HealthPotion) <= 0f)
                throw new InvalidOperationException("회복물약 무게/가격이 없습니다.");

            var gain = new SkillSet();
            var stats = new StatSet();
            int intWas = stats.Int;
            SkillGain.TryRaise(gain, SkillId.Alchemy, 10f, out _, out float after, stats);
            if (Math.Abs(after - 0.1f) > 0.0001f)
                throw new InvalidOperationException("연금술 0.0→0.1이어야 합니다.");
            if (stats.Int != intWas + 1)
                throw new InvalidOperationException("연금술 상승 시 INT가 올라야 합니다.");

            var created = CharacterCreate.Build("alch-check", "연금", 0, 20, 20, 40,
                new[] { SkillId.Alchemy, SkillId.Tailoring, SkillId.Magery },
                new[] { 50f, 30f, 20f });
            bool hasCloth = false;
            for (int i = 0; i < created.Inventory.Length; i++)
            {
                if (created.Inventory[i].TemplateId == ItemCatalog.Cloth && created.Inventory[i].Amount >= 1)
                    hasCloth = true;
            }
            if (!hasCloth)
                throw new InvalidOperationException("연금술 시작은 천을 줘야 합니다.");

            var mortar = GameObject.Find("Mortar");
            if (mortar == null)
                throw new InvalidOperationException("마을에 절구(Mortar)가 있어야 합니다.");
            var sceneSt = mortar.GetComponent<CraftStation>();
            if (sceneSt == null || sceneSt.RecipeId != "health_potion")
                throw new InvalidOperationException("절구는 회복물약 CraftStation이어야 합니다.");

            var go = new GameObject("selfcheck-alch");
            GameObject worldGo = null;
            GameObject stGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-alch-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.RecalcFromStr(30);
                var bag = go.AddComponent<InventoryBag>();
                stGo = new GameObject("selfcheck-alch-st");
                stGo.transform.position = go.transform.position;
                var station = stGo.AddComponent<CraftStation>();
                station.RecipeId = "health_potion";
                station.DisplayName = "절구";
                var noIng = world.TryCraft(body, station);
                if (noIng.Applied)
                    throw new InvalidOperationException("재료 없이 연금되면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Alchemy)) > 0.0001f)
                    throw new InvalidOperationException("실패한 연금은 스킬을 올리면 안 됩니다.");
                bag.Add(ItemCatalog.Cloth, 1);
                var ok = world.TryCraft(body, station);
                if (!ok.Applied)
                    throw new InvalidOperationException("천 연금 실패: " + ok.FailReason);
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Alchemy) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 연금 후 0.1이어야 합니다.");
                int pots = 0, clothLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    if (bag.Items[i].TemplateId == ItemCatalog.HealthPotion)
                        pots += bag.Items[i].Amount;
                    if (bag.Items[i].TemplateId == ItemCatalog.Cloth)
                        clothLeft += bag.Items[i].Amount;
                }
                if (pots < 1 || clothLeft != 0)
                    throw new InvalidOperationException("천 1 → 회복물약 1이어야 합니다.");

                float hpWas = body.Hp;
                body.SetHp(Math.Max(1f, body.MaxHp - 20f));
                var drink = world.TryDrink(body);
                if (!drink.Applied)
                    throw new InvalidOperationException("물약 마시기 실패: " + drink.FailReason);
                if (body.Hp <= hpWas - 20f + 0.01f)
                    throw new InvalidOperationException("물약은 HP를 회복해야 합니다.");
                int potsLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.HealthPotion)
                        potsLeft += bag.Items[i].Amount;
                if (potsLeft != 0)
                    throw new InvalidOperationException("마신 물약은 소모되어야 합니다.");

                var locked = new SkillSet();
                locked.SetLock(SkillId.Alchemy, SkillLock.Locked);
                SkillGain.TryRaise(locked, SkillId.Alchemy, 10f, out _, out _);
                if (Math.Abs(locked.Get(SkillId.Alchemy)) > 0.0001f)
                    throw new InvalidOperationException("잠긴 연금술은 오르면 안 됩니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (stGo != null)
                    UnityEngine.Object.DestroyImmediate(stGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }
    }
}
