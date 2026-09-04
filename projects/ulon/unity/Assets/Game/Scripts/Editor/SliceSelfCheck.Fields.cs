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
        static void AssertEastFieldSlice()
        {
            var field = GameObject.Find("EastField");
            if (field == null)
                throw new InvalidOperationException("마을 옆 동쪽 필드(EastField)가 있어야 합니다.");
            var oak = GameObject.Find("OakTree");
            if (oak == null)
                throw new InvalidOperationException("마을 OakTree를 필드가 대체하면 안 됩니다.");
            var villageNode = oak.GetComponent<ResourceNode>();
            if (villageNode == null || villageNode.GatherSkill != SkillId.Lumberjacking)
                throw new InvalidOperationException("마을 OakTree 벌목 노드가 유지되어야 합니다.");
            var go = GameObject.Find("FieldOak");
            if (go == null)
                throw new InvalidOperationException("동쪽 필드에 FieldOak가 있어야 합니다.");
            var node = go.GetComponent<ResourceNode>();
            if (node == null || node.GatherSkill != SkillId.Lumberjacking || node.ResourceId != "wood")
                throw new InvalidOperationException("FieldOak는 벌목 ResourceNode여야 합니다.");
            Vector3 pos = go.transform.position;
            if (GuardZone.Contains(pos.x, pos.z))
                throw new InvalidOperationException("동쪽 필드는 가드존 밖이어야 합니다.");
            var terrain = GameObject.Find("Ground");
            var data = terrain != null ? terrain.GetComponent<Terrain>() : null;
            if (data == null || data.terrainData == null)
                throw new InvalidOperationException("Ground Terrain이 있어야 합니다.");
            Vector3 size = data.terrainData.size;
            if (Math.Abs(size.x - 180f) > 0.1f || Math.Abs(size.z - 180f) > 0.1f)
                throw new InvalidOperationException("필드 추가로 맵 크기를 키우면 안 됩니다.");

            var worldGo = new GameObject("selfcheck-field-world");
            GameObject bodyGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                bodyGo = new GameObject("selfcheck-field-body");
                bodyGo.transform.position = go.transform.position;
                var body = bodyGo.AddComponent<WorldBody>();
                body.IsAvatar = true;
                var bag = bodyGo.AddComponent<InventoryBag>();
                var noTool = world.TryGather(body, node);
                if (noTool.Applied)
                    throw new InvalidOperationException("도끼 없이 들판 벌목되면 안 됩니다.");
                bag.Add(new ItemRecord { TemplateId = ItemCatalog.Hatchet, Amount = 1, Uses = 4 });
                float lumber0 = world.SkillsOf(body).Get(SkillId.Lumberjacking);
                var ok = world.TryGather(body, node);
                if (!ok.Applied)
                    throw new InvalidOperationException("들판 벌목 실패: " + ok.FailReason);
                if (world.SkillsOf(body).Get(SkillId.Lumberjacking) < lumber0 + 0.09f)
                    throw new InvalidOperationException("들판 벌목 후 벌목이 올라야 합니다.");
                int wood = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == "wood")
                        wood += bag.Items[i].Amount;
                if (wood < 1)
                    throw new InvalidOperationException("들판 나무가 가방에 있어야 합니다.");
            }
            finally
            {
                if (bodyGo != null)
                    UnityEngine.Object.DestroyImmediate(bodyGo);
                UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }


        static void AssertSouthFieldSlice()
        {
            var east = GameObject.Find("EastField");
            if (east == null)
                throw new InvalidOperationException("남쪽 필드가 동쪽 필드를 대체하면 안 됩니다.");
            var field = GameObject.Find("SouthField");
            if (field == null)
                throw new InvalidOperationException("마을 남쪽 필드(SouthField)가 있어야 합니다.");
            var flax = GameObject.Find("FieldFlax");
            if (flax == null)
                throw new InvalidOperationException("남쪽 필드에 FieldFlax가 있어야 합니다.");
            var node = flax.GetComponent<ResourceNode>();
            if (node == null || node.GatherSkill != SkillId.Tailoring || node.ResourceId != ItemCatalog.Cloth)
                throw new InvalidOperationException("FieldFlax는 재봉 ResourceNode(천)여야 합니다.");
            Vector3 pos = flax.transform.position;
            if (GuardZone.Contains(pos.x, pos.z))
                throw new InvalidOperationException("남쪽 필드는 가드존 밖이어야 합니다.");
            if (pos.z > -16.5f)
                throw new InvalidOperationException("남쪽 필드는 마을 울타리 남쪽이어야 합니다.");
            var oak = GameObject.Find("FieldOak");
            if (oak == null)
                throw new InvalidOperationException("동쪽 FieldOak가 유지되어야 합니다.");
            if (Vector3.Distance(pos, oak.transform.position) < 14f)
                throw new InvalidOperationException("남쪽 필드는 동쪽 필드와 떨어져 있어야 합니다.");
            var hunt = GameObject.Find("Raider");
            if (hunt != null && Vector3.Distance(pos, hunt.transform.position) < 12f)
                throw new InvalidOperationException("남쪽 필드가 사냥 라인을 건드리면 안 됩니다.");
            var fish = GameObject.Find("FishingSpot");
            if (fish != null && Vector3.Distance(pos, fish.transform.position) < 8f)
                throw new InvalidOperationException("남쪽 필드가 물가를 건드리면 안 됩니다.");
            var terrain = GameObject.Find("Ground");
            var data = terrain != null ? terrain.GetComponent<Terrain>() : null;
            if (data == null || data.terrainData == null)
                throw new InvalidOperationException("Ground Terrain이 있어야 합니다.");
            Vector3 size = data.terrainData.size;
            if (Math.Abs(size.x - 180f) > 0.1f || Math.Abs(size.z - 180f) > 0.1f)
                throw new InvalidOperationException("필드 추가로 맵 크기를 키우면 안 됩니다.");

            var worldGo = new GameObject("selfcheck-south-world");
            GameObject bodyGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                bodyGo = new GameObject("selfcheck-south-body");
                bodyGo.transform.position = flax.transform.position;
                var body = bodyGo.AddComponent<WorldBody>();
                body.IsAvatar = true;
                var bag = bodyGo.AddComponent<InventoryBag>();
                float tailor0 = world.SkillsOf(body).Get(SkillId.Tailoring);
                var ok = world.TryGather(body, node);
                if (!ok.Applied)
                    throw new InvalidOperationException("남쪽 아마 채집 실패: " + ok.FailReason);
                if (world.SkillsOf(body).Get(SkillId.Tailoring) < tailor0 + 0.09f)
                    throw new InvalidOperationException("아마 채집 후 재봉이 올라야 합니다.");
                int cloth = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.Cloth)
                        cloth += bag.Items[i].Amount;
                if (cloth < 1)
                    throw new InvalidOperationException("아마에서 나온 천이 가방에 있어야 합니다.");
            }
            finally
            {
                if (bodyGo != null)
                    UnityEngine.Object.DestroyImmediate(bodyGo);
                UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }


        static void AssertNorthFieldSlice()
        {
            var east = GameObject.Find("EastField");
            if (east == null)
                throw new InvalidOperationException("북쪽 필드가 동쪽 필드를 대체하면 안 됩니다.");
            var south = GameObject.Find("SouthField");
            if (south == null)
                throw new InvalidOperationException("북쪽 필드가 남쪽 필드를 대체하면 안 됩니다.");
            var field = GameObject.Find("NorthField");
            if (field == null)
                throw new InvalidOperationException("마을 북쪽 필드(NorthField)가 있어야 합니다.");
            var ore = GameObject.Find("FieldOre");
            if (ore == null)
                throw new InvalidOperationException("북쪽 필드에 FieldOre가 있어야 합니다.");
            var node = ore.GetComponent<ResourceNode>();
            if (node == null || node.GatherSkill != SkillId.Mining || node.ResourceId != "iron_ore")
                throw new InvalidOperationException("FieldOre는 채광 ResourceNode(철광)여야 합니다.");
            Vector3 pos = ore.transform.position;
            if (GuardZone.Contains(pos.x, pos.z))
                throw new InvalidOperationException("북쪽 필드는 가드존 밖이어야 합니다.");
            if (pos.z < 16.5f)
                throw new InvalidOperationException("북쪽 필드는 마을 울타리 북쪽이어야 합니다.");
            var oak = GameObject.Find("FieldOak");
            if (oak == null)
                throw new InvalidOperationException("동쪽 FieldOak가 유지되어야 합니다.");
            if (Vector3.Distance(pos, oak.transform.position) < 14f)
                throw new InvalidOperationException("북쪽 필드는 동쪽 필드와 떨어져 있어야 합니다.");
            var flax = GameObject.Find("FieldFlax");
            if (flax == null)
                throw new InvalidOperationException("남쪽 FieldFlax가 유지되어야 합니다.");
            if (Vector3.Distance(pos, flax.transform.position) < 14f)
                throw new InvalidOperationException("북쪽 필드는 남쪽 필드와 떨어져 있어야 합니다.");
            var hunt = GameObject.Find("Raider");
            if (hunt != null && Vector3.Distance(pos, hunt.transform.position) < 12f)
                throw new InvalidOperationException("북쪽 필드가 사냥 라인을 건드리면 안 됩니다.");
            var gate = GameObject.Find(Dungeon1.EntranceObject);
            if (gate != null && Vector3.Distance(pos, gate.transform.position) < 12f)
                throw new InvalidOperationException("북쪽 필드가 던전 1 서쪽 입구를 건드리면 안 됩니다.");
            var vein = GameObject.Find("IronVein");
            if (vein == null)
                throw new InvalidOperationException("마을 IronVein을 필드가 대체하면 안 됩니다.");
            var villageNode = vein.GetComponent<ResourceNode>();
            if (villageNode == null || villageNode.GatherSkill != SkillId.Mining)
                throw new InvalidOperationException("마을 IronVein 채광 노드가 유지되어야 합니다.");
            var terrain = GameObject.Find("Ground");
            var data = terrain != null ? terrain.GetComponent<Terrain>() : null;
            if (data == null || data.terrainData == null)
                throw new InvalidOperationException("Ground Terrain이 있어야 합니다.");
            Vector3 size = data.terrainData.size;
            if (Math.Abs(size.x - 180f) > 0.1f || Math.Abs(size.z - 180f) > 0.1f)
                throw new InvalidOperationException("필드 추가로 맵 크기를 키우면 안 됩니다.");

            var worldGo = new GameObject("selfcheck-north-world");
            GameObject bodyGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                bodyGo = new GameObject("selfcheck-north-body");
                bodyGo.transform.position = ore.transform.position;
                var body = bodyGo.AddComponent<WorldBody>();
                body.IsAvatar = true;
                var bag = bodyGo.AddComponent<InventoryBag>();
                var noTool = world.TryGather(body, node);
                if (noTool.Applied)
                    throw new InvalidOperationException("곡괭이 없이 들판 채광되면 안 됩니다.");
                bag.Add(new ItemRecord { TemplateId = ItemCatalog.Pickaxe, Amount = 1, Uses = 4 });
                float mine0 = world.SkillsOf(body).Get(SkillId.Mining);
                var ok = world.TryGather(body, node);
                if (!ok.Applied)
                    throw new InvalidOperationException("북쪽 광맥 채집 실패: " + ok.FailReason);
                if (world.SkillsOf(body).Get(SkillId.Mining) < mine0 + 0.09f)
                    throw new InvalidOperationException("광맥 채집 후 채광이 올라야 합니다.");
                int oreCount = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == "iron_ore")
                        oreCount += bag.Items[i].Amount;
                if (oreCount < 1)
                    throw new InvalidOperationException("들판 철광이 가방에 있어야 합니다.");
            }
            finally
            {
                if (bodyGo != null)
                    UnityEngine.Object.DestroyImmediate(bodyGo);
                UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }

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


        static void AssertFencingSlice()
        {
            if (StatSet.PrimaryOf(SkillId.Fencing) != StatId.Dex)
                throw new InvalidOperationException("창술 Primary는 DEX이어야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Fencing) != "창술" || SkillTitles.JobOf(SkillId.Fencing) != "창수")
                throw new InvalidOperationException("창술 스킬명/직업명이 기획과 같아야 합니다.");
            if (ItemCatalog.FencingRange <= ItemCatalog.MeleeRange || ItemCatalog.FencingRange >= ItemCatalog.ArcheryRange)
                throw new InvalidOperationException("창 사거리는 근접과 활 사이여야 합니다.");
            var rec = CraftRecipes.Find("wooden_spear");
            if (rec == null || rec.Ingredient != "wood" || rec.Count != 2 || rec.Output != ItemCatalog.WoodenSpear
                || rec.Skill != SkillId.Carpentry || !rec.CanRepair)
                throw new InvalidOperationException("나무 2 → 나무창 목공 레시피가 있어야 합니다.");
            if (ItemCatalog.CombatSkillOf(ItemCatalog.WoodenSpear) != SkillId.Fencing)
                throw new InvalidOperationException("나무창 전투 스킬은 창술이어야 합니다.");
            if (Math.Abs(ItemCatalog.CombatRangeOf(SkillId.Fencing) - ItemCatalog.FencingRange) > 0.0001f)
                throw new InvalidOperationException("창술 CombatRange가 FencingRange여야 합니다.");
            if (ItemCatalog.WeightOf(ItemCatalog.WoodenSpear) <= 0f || ItemCatalog.BuyPrice(ItemCatalog.WoodenSpear) <= 0
                || ItemCatalog.MaxUsesOf(ItemCatalog.WoodenSpear) <= 0)
                throw new InvalidOperationException("나무창 무게/가격/내구가 없습니다.");

            var missSkills = new SkillSet();
            var far = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 7f,
                Range = ItemCatalog.FencingRange,
                WeaponSkill = SkillId.Fencing,
                Skills = missSkills,
                TargetAlive = true
            });
            if (far.Applied)
                throw new InvalidOperationException("창 사거리 밖 공격이 들어가면 안 됩니다.");
            if (Math.Abs(missSkills.Get(SkillId.Fencing)) > 0.0001f || Math.Abs(missSkills.Get(SkillId.Swordsmanship)) > 0.0001f)
                throw new InvalidOperationException("실패한 창 공격은 스킬을 올리면 안 됩니다.");

            var hitSkills = new SkillSet();
            var hit = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 4f,
                Range = ItemCatalog.FencingRange,
                Now = 1f,
                NextAttackAt = 0f,
                WeaponSkill = SkillId.Fencing,
                Skills = hitSkills,
                TargetAlive = true
            });
            if (!hit.Applied || !hit.Hit)
                throw new InvalidOperationException("창 사거리 안 공격이 들어가야 합니다.");
            if (Math.Abs(hitSkills.Get(SkillId.Fencing) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("창술 0.0→0.1이어야 합니다.");
            if (Math.Abs(hitSkills.Get(SkillId.Tactics) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("창 공격 후 전술 0.0→0.1이어야 합니다.");
            if (Math.Abs(hitSkills.Get(SkillId.Anatomy) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("창 공격 후 해부학 0.0→0.1이어야 합니다.");
            if (Math.Abs(hitSkills.Get(SkillId.Swordsmanship)) > 0.0001f || Math.Abs(hitSkills.Get(SkillId.Archery)) > 0.0001f)
                throw new InvalidOperationException("창 공격은 검술/궁술을 올리면 안 됩니다.");

            var meleeAtSpear = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 4f,
                Skills = new SkillSet(),
                TargetAlive = true
            });
            if (meleeAtSpear.Applied)
                throw new InvalidOperationException("근접은 창 사거리에서 들어가면 안 됩니다.");

            var dexLow = new StatSet();
            dexLow.ForceSet(50, 10, 25);
            var dexHigh = new StatSet();
            dexHigh.ForceSet(50, 50, 25);
            var low = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 1f,
                Range = ItemCatalog.FencingRange,
                Now = 2f,
                WeaponSkill = SkillId.Fencing,
                Skills = new SkillSet(),
                Stats = dexLow,
                TargetAlive = true
            });
            var high = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 1f,
                Range = ItemCatalog.FencingRange,
                Now = 2f,
                WeaponSkill = SkillId.Fencing,
                Skills = new SkillSet(),
                Stats = dexHigh,
                TargetAlive = true
            });
            if (high.Damage <= low.Damage)
                throw new InvalidOperationException("창술 피해는 DEX 보정이 있어야 합니다.");

            var gain = new SkillSet();
            var stats = new StatSet();
            int dexWas = stats.Dex;
            SkillGain.TryRaise(gain, SkillId.Fencing, 20f, out _, out float after, stats);
            if (Math.Abs(after - 0.1f) > 0.0001f)
                throw new InvalidOperationException("창술 숙련 0.0→0.1이어야 합니다.");
            if (stats.Dex != dexWas + 1)
                throw new InvalidOperationException("창술 상승 시 DEX가 올라야 합니다.");

            var created = CharacterCreate.Build("fence-check", "창수", 0, 20, 40, 20,
                new[] { SkillId.Fencing, SkillId.Carpentry, SkillId.Tactics },
                new[] { 50f, 30f, 20f });
            bool hasSpear = false, hasWood = false;
            for (int i = 0; i < created.Inventory.Length; i++)
            {
                if (created.Inventory[i].TemplateId == ItemCatalog.WoodenSpear)
                    hasSpear = true;
                if (created.Inventory[i].TemplateId == "wood")
                    hasWood = true;
            }
            if (!hasSpear)
                throw new InvalidOperationException("창술 시작은 나무창을 줘야 합니다.");
            if (!hasWood)
                throw new InvalidOperationException("목공 시작은 나무를 줘야 합니다.");

            var go = new GameObject("selfcheck-fence");
            GameObject worldGo = null;
            GameObject stGo = null;
            GameObject dummy = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-fence-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.CharacterId = "fence-mark";
                var bag = go.AddComponent<InventoryBag>();
                stGo = new GameObject("selfcheck-fence-st");
                stGo.transform.position = go.transform.position;
                var station = stGo.AddComponent<CraftStation>();
                station.RecipeId = "wooden_spear";
                station.DisplayName = "목공소";
                var noIng = world.TryCraft(body, station);
                if (noIng.Applied)
                    throw new InvalidOperationException("재료 없이 나무창이 되면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Carpentry)) > 0.0001f)
                    throw new InvalidOperationException("실패한 목공은 스킬을 올리면 안 됩니다.");
                bag.Add("wood", 2);
                var made = world.TryCraft(body, station);
                if (!made.Applied)
                    throw new InvalidOperationException("나무창 제작 실패: " + made.FailReason);
                bool spear = false;
                int woodLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    if (bag.Items[i].TemplateId == ItemCatalog.WoodenSpear)
                    {
                        spear = true;
                        if (bag.Items[i].MakerId != "fence-mark")
                            throw new InvalidOperationException("나무창 Maker Mark가 있어야 합니다.");
                    }
                    if (bag.Items[i].TemplateId == "wood")
                        woodLeft += bag.Items[i].Amount;
                }
                if (!spear || woodLeft != 0)
                    throw new InvalidOperationException("나무 2 → 나무창 1이어야 합니다.");

                dummy = new GameObject("selfcheck-fence-skel");
                var skel = dummy.AddComponent<WorldBody>();
                skel.IsEnemy = true;
                skel.MaxHp = 40f;
                skel.ResetHp();
                dummy.transform.position = go.transform.position + new Vector3(0f, 0f, 4.2f);
                var shot = world.TryAttack(body, skel);
                if (!shot.Applied)
                    throw new InvalidOperationException("창 중거리 공격 실패: " + shot.FailReason);
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Fencing) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("창 공격 후 창술 0.1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Tactics) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("창 공격 후 전술 0.1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Anatomy) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("창 공격 후 해부학 0.1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Swordsmanship)) > 0.0001f)
                    throw new InvalidOperationException("창 공격은 검술을 올리면 안 됩니다.");
                if (skel.Hp >= 40f)
                    throw new InvalidOperationException("창 공격이 피해를 줘야 합니다.");

                dummy.transform.position = go.transform.position + new Vector3(0f, 0f, 7.5f);
                var tooFar = world.TryAttack(body, skel);
                if (tooFar.Applied)
                    throw new InvalidOperationException("창은 활 사거리에서 들어가면 안 됩니다.");

                var locked = new SkillSet();
                locked.SetLock(SkillId.Fencing, SkillLock.Locked);
                SkillGain.TryRaise(locked, SkillId.Fencing, 20f, out _, out _);
                if (Math.Abs(locked.Get(SkillId.Fencing)) > 0.0001f)
                    throw new InvalidOperationException("잠긴 창술은 오르면 안 됩니다.");
            }
            finally
            {
                if (dummy != null)
                    UnityEngine.Object.DestroyImmediate(dummy);
                UnityEngine.Object.DestroyImmediate(go);
                if (stGo != null)
                    UnityEngine.Object.DestroyImmediate(stGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }


        static void AssertMaceSlice()
        {
            if (StatSet.PrimaryOf(SkillId.Mace) != StatId.Str)
                throw new InvalidOperationException("둔기술 Primary는 STR이어야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Mace) != "둔기술" || SkillTitles.JobOf(SkillId.Mace) != "둔기수")
                throw new InvalidOperationException("둔기술 스킬명/직업명이 기획과 같아야 합니다.");
            if (ItemCatalog.CombatSkillOf(ItemCatalog.WoodenClub) != SkillId.Mace)
                throw new InvalidOperationException("나무곤봉 전투 스킬은 둔기술이어야 합니다.");
            if (ItemCatalog.CombatSkillOf(ItemCatalog.IronSword) != SkillId.Swordsmanship)
                throw new InvalidOperationException("철검은 검술이어야 합니다.");
            if (ItemCatalog.CombatSkillOf(ItemCatalog.WoodenSpear) != SkillId.Fencing)
                throw new InvalidOperationException("나무창은 창술이어야 합니다.");
            if (Math.Abs(ItemCatalog.CombatRangeOf(SkillId.Mace) - ItemCatalog.MeleeRange) > 0.0001f)
                throw new InvalidOperationException("둔기술 CombatRange는 근접이어야 합니다.");
            var rec = CraftRecipes.Find("wooden_club");
            if (rec == null || rec.Ingredient != "wood" || rec.Count != 2 || rec.Output != ItemCatalog.WoodenClub
                || rec.Skill != SkillId.Carpentry || !rec.CanRepair)
                throw new InvalidOperationException("나무 2 → 나무곤봉 목공 레시피가 있어야 합니다.");
            if (ItemCatalog.WeightOf(ItemCatalog.WoodenClub) <= 0f || ItemCatalog.BuyPrice(ItemCatalog.WoodenClub) <= 0
                || ItemCatalog.MaxUsesOf(ItemCatalog.WoodenClub) <= 0)
                throw new InvalidOperationException("나무곤봉 무게/가격/내구가 없습니다.");

            var clubOnly = new[] { new ItemRecord { TemplateId = ItemCatalog.WoodenClub, Amount = 1, Uses = 10 } };
            if (ItemCatalog.CombatWeaponOf(clubOnly) != ItemCatalog.WoodenClub)
                throw new InvalidOperationException("곤봉만 있으면 전투 무기는 나무곤봉이어야 합니다.");
            var clubAndSword = new[]
            {
                new ItemRecord { TemplateId = ItemCatalog.WoodenClub, Amount = 1, Uses = 10 },
                new ItemRecord { TemplateId = ItemCatalog.IronSword, Amount = 1, Uses = 10 }
            };
            if (ItemCatalog.CombatWeaponOf(clubAndSword) != ItemCatalog.IronSword)
                throw new InvalidOperationException("철검이 있으면 검이 곤봉보다 우선이어야 합니다.");

            var missSkills = new SkillSet();
            var far = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 4f,
                Range = ItemCatalog.MeleeRange,
                WeaponSkill = SkillId.Mace,
                Skills = missSkills,
                TargetAlive = true
            });
            if (far.Applied)
                throw new InvalidOperationException("둔기 사거리 밖 공격이 들어가면 안 됩니다.");
            if (Math.Abs(missSkills.Get(SkillId.Mace)) > 0.0001f || Math.Abs(missSkills.Get(SkillId.Swordsmanship)) > 0.0001f
                || Math.Abs(missSkills.Get(SkillId.Fencing)) > 0.0001f)
                throw new InvalidOperationException("실패한 둔기 공격은 스킬을 올리면 안 됩니다.");

            var hitSkills = new SkillSet();
            var hit = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 1f,
                Range = ItemCatalog.MeleeRange,
                Now = 1f,
                NextAttackAt = 0f,
                WeaponSkill = SkillId.Mace,
                Skills = hitSkills,
                TargetAlive = true
            });
            if (!hit.Applied || !hit.Hit)
                throw new InvalidOperationException("둔기 근접 공격이 들어가야 합니다.");
            if (Math.Abs(hitSkills.Get(SkillId.Mace) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("둔기술 0.0→0.1이어야 합니다.");
            if (Math.Abs(hitSkills.Get(SkillId.Tactics) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("둔기 공격 후 전술 0.0→0.1이어야 합니다.");
            if (Math.Abs(hitSkills.Get(SkillId.Anatomy) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("둔기 공격 후 해부학 0.0→0.1이어야 합니다.");
            if (Math.Abs(hitSkills.Get(SkillId.Swordsmanship)) > 0.0001f || Math.Abs(hitSkills.Get(SkillId.Fencing)) > 0.0001f
                || Math.Abs(hitSkills.Get(SkillId.Archery)) > 0.0001f)
                throw new InvalidOperationException("둔기 공격은 검술/창술/궁술을 올리면 안 됩니다.");

            var strLow = new StatSet();
            strLow.ForceSet(10, 50, 25);
            var strHigh = new StatSet();
            strHigh.ForceSet(50, 50, 25);
            var low = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 1f,
                Range = ItemCatalog.MeleeRange,
                Now = 2f,
                WeaponSkill = SkillId.Mace,
                Skills = new SkillSet(),
                Stats = strLow,
                TargetAlive = true
            });
            var high = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 1f,
                Range = ItemCatalog.MeleeRange,
                Now = 2f,
                WeaponSkill = SkillId.Mace,
                Skills = new SkillSet(),
                Stats = strHigh,
                TargetAlive = true
            });
            if (high.Damage <= low.Damage)
                throw new InvalidOperationException("둔기술 피해는 STR 보정이 있어야 합니다.");
            var dexHigh = new StatSet();
            dexHigh.ForceSet(10, 90, 25);
            var dexHit = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 1f,
                Range = ItemCatalog.MeleeRange,
                Now = 3f,
                WeaponSkill = SkillId.Mace,
                Skills = new SkillSet(),
                Stats = dexHigh,
                TargetAlive = true
            });
            if (dexHit.Damage != low.Damage)
                throw new InvalidOperationException("둔기술 피해는 DEX가 아니라 STR이어야 합니다.");

            var gain = new SkillSet();
            var stats = new StatSet();
            int strWas = stats.Str;
            int dexWas = stats.Dex;
            SkillGain.TryRaise(gain, SkillId.Mace, 20f, out _, out float after, stats);
            if (Math.Abs(after - 0.1f) > 0.0001f)
                throw new InvalidOperationException("둔기술 숙련 0.0→0.1이어야 합니다.");
            if (stats.Str != strWas + 1)
                throw new InvalidOperationException("둔기술 상승 시 STR이 올라야 합니다.");
            if (stats.Dex != dexWas)
                throw new InvalidOperationException("둔기술 상승 시 DEX가 올라가면 안 됩니다.");

            var created = CharacterCreate.Build("mace-check", "둔기수", 0, 40, 20, 20,
                new[] { SkillId.Mace, SkillId.Carpentry, SkillId.Tactics },
                new[] { 50f, 30f, 20f });
            bool hasClub = false, hasWood = false;
            for (int i = 0; i < created.Inventory.Length; i++)
            {
                if (created.Inventory[i].TemplateId == ItemCatalog.WoodenClub)
                    hasClub = true;
                if (created.Inventory[i].TemplateId == "wood")
                    hasWood = true;
            }
            if (!hasClub)
                throw new InvalidOperationException("둔기술 시작은 나무곤봉을 줘야 합니다.");
            if (!hasWood)
                throw new InvalidOperationException("목공 시작은 나무를 줘야 합니다.");

            var go = new GameObject("selfcheck-mace");
            GameObject worldGo = null;
            GameObject stGo = null;
            GameObject dummy = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-mace-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.CharacterId = "mace-mark";
                var bag = go.AddComponent<InventoryBag>();
                stGo = new GameObject("selfcheck-mace-st");
                stGo.transform.position = go.transform.position;
                var station = stGo.AddComponent<CraftStation>();
                station.RecipeId = "wooden_club";
                station.DisplayName = "목공소";
                var noIng = world.TryCraft(body, station);
                if (noIng.Applied)
                    throw new InvalidOperationException("재료 없이 나무곤봉이 되면 안 됩니다.");
                bag.Add("wood", 2);
                var made = world.TryCraft(body, station);
                if (!made.Applied)
                    throw new InvalidOperationException("나무곤봉 제작 실패: " + made.FailReason);
                bool club = false;
                int woodLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    if (bag.Items[i].TemplateId == ItemCatalog.WoodenClub)
                    {
                        club = true;
                        if (bag.Items[i].MakerId != "mace-mark")
                            throw new InvalidOperationException("나무곤봉 Maker Mark가 있어야 합니다.");
                    }
                    if (bag.Items[i].TemplateId == "wood")
                        woodLeft += bag.Items[i].Amount;
                }
                if (!club || woodLeft != 0)
                    throw new InvalidOperationException("나무 2 → 나무곤봉 1이어야 합니다.");

                dummy = new GameObject("selfcheck-mace-skel");
                var skel = dummy.AddComponent<WorldBody>();
                skel.IsEnemy = true;
                skel.MaxHp = 40f;
                skel.ResetHp();
                dummy.transform.position = go.transform.position + new Vector3(0f, 0f, 1.2f);
                var shot = world.TryAttack(body, skel);
                if (!shot.Applied)
                    throw new InvalidOperationException("둔기 근접 공격 실패: " + shot.FailReason);
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Mace) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("둔기 공격 후 둔기술 0.1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Tactics) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("둔기 공격 후 전술 0.1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Anatomy) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("둔기 공격 후 해부학 0.1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Swordsmanship)) > 0.0001f)
                    throw new InvalidOperationException("둔기 공격은 검술을 올리면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Fencing)) > 0.0001f)
                    throw new InvalidOperationException("둔기 공격은 창술을 올리면 안 됩니다.");
                if (skel.Hp >= 40f)
                    throw new InvalidOperationException("둔기 공격이 피해를 줘야 합니다.");

                dummy.transform.position = go.transform.position + new Vector3(0f, 0f, 4.2f);
                var tooFar = world.TryAttack(body, skel);
                if (tooFar.Applied)
                    throw new InvalidOperationException("둔기는 창 사거리에서 들어가면 안 됩니다.");

                var locked = new SkillSet();
                locked.SetLock(SkillId.Mace, SkillLock.Locked);
                SkillGain.TryRaise(locked, SkillId.Mace, 20f, out _, out _);
                if (Math.Abs(locked.Get(SkillId.Mace)) > 0.0001f)
                    throw new InvalidOperationException("잠긴 둔기술은 오르면 안 됩니다.");
            }
            finally
            {
                if (dummy != null)
                    UnityEngine.Object.DestroyImmediate(dummy);
                UnityEngine.Object.DestroyImmediate(go);
                if (stGo != null)
                    UnityEngine.Object.DestroyImmediate(stGo);
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

        static void AssertHealingResurrect()
        {
            AssertDungeon3Leftover();
            if (SkillNames.KoreanOf(SkillId.Healing) != "치유")
                throw new InvalidOperationException("치유 스킬명을 바꾸면 안 됩니다.");
            if (StatSet.PrimaryOf(SkillId.Healing) != StatId.Dex)
                throw new InvalidOperationException("치유 Primary는 DEX이어야 합니다.");

            var ghostHealer = BandageResurrectResolve.Resolve(new BandageResurrectRequest
            {
                HealerGhost = true,
                TargetGhost = true,
                TargetAvatar = true,
                HasBandage = true,
                Distance = 1f,
                Skills = new SkillSet()
            });
            if (ghostHealer.Applied || ghostHealer.FailReason != "ghost")
                throw new InvalidOperationException("유령 시술자 붕대 부활은 실패해야 합니다.");

            var noGhost = BandageResurrectResolve.Resolve(new BandageResurrectRequest
            {
                TargetGhost = false,
                TargetAvatar = true,
                HasBandage = true,
                Distance = 1f,
                Skills = new SkillSet()
            });
            if (noGhost.Applied || noGhost.FailReason != "not_ghost")
                throw new InvalidOperationException("유령 아닌 대상 붕대 부활은 실패해야 합니다.");

            var notAvatar = BandageResurrectResolve.Resolve(new BandageResurrectRequest
            {
                TargetGhost = true,
                TargetAvatar = false,
                HasBandage = true,
                Distance = 1f,
                Skills = new SkillSet()
            });
            if (notAvatar.Applied || notAvatar.FailReason != "not_ghost")
                throw new InvalidOperationException("아바타가 아닌 Ghost 붕대 부활은 실패해야 합니다.");

            var noBnSkills = new SkillSet();
            var noBn = BandageResurrectResolve.Resolve(new BandageResurrectRequest
            {
                TargetGhost = true,
                TargetAvatar = true,
                HasBandage = false,
                Distance = 1f,
                Skills = noBnSkills
            });
            if (noBn.Applied || noBn.FailReason != "no_bandage")
                throw new InvalidOperationException("붕대 없는 부활은 실패해야 합니다.");
            if (Math.Abs(noBnSkills.Get(SkillId.Healing)) > 0.0001f)
                throw new InvalidOperationException("실패한 붕대 부활은 스킬을 올리면 안 됩니다.");

            var farSkills = new SkillSet();
            var far = BandageResurrectResolve.Resolve(new BandageResurrectRequest
            {
                TargetGhost = true,
                TargetAvatar = true,
                HasBandage = true,
                Distance = ItemCatalog.MeleeRange + 1f,
                Range = ItemCatalog.MeleeRange,
                Skills = farSkills
            });
            if (far.Applied || far.FailReason != "range")
                throw new InvalidOperationException("사거리 밖 붕대 부활은 실패해야 합니다.");
            if (Math.Abs(farSkills.Get(SkillId.Healing)) > 0.0001f)
                throw new InvalidOperationException("사거리 밖 붕대 부활은 스킬을 올리면 안 됩니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            int dexWas = stats.Dex;
            var ok = BandageResurrectResolve.Resolve(new BandageResurrectRequest
            {
                TargetGhost = true,
                TargetAvatar = true,
                HasBandage = true,
                Distance = 1f,
                Skills = skills,
                Stats = stats,
                Difficulty = BandageResurrectResolve.Difficulty
            });
            if (!ok.Applied)
                throw new InvalidOperationException("붕대 부활 Resolve는 성공해야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Healing) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("성공 붕대 부활 후 Healing 0.1이어야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Magery)) > 0.0001f)
                throw new InvalidOperationException("붕대 부활은 마법을 올리면 안 됩니다.");
            if (Math.Abs(skills.Get(SkillId.Veterinary)) > 0.0001f)
                throw new InvalidOperationException("붕대 부활은 수의학을 올리면 안 됩니다.");
            if (stats.Dex != dexWas + 1)
                throw new InvalidOperationException("붕대 부활 상승 시 DEX가 올라야 합니다.");

            var forceSkills = new SkillSet();
            var forceStats = new StatSet();
            var forced = BandageResurrectResolve.Resolve(new BandageResurrectRequest
            {
                TargetGhost = true,
                TargetAvatar = true,
                HasBandage = true,
                Distance = 1f,
                Skills = forceSkills,
                Stats = forceStats,
                Force = true
            });
            if (!forced.Applied || Math.Abs(forceSkills.Get(SkillId.Healing) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("Force 경로도 Healing 0.1이어야 합니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.Healing, SkillLock.Locked);
            var lockedOk = BandageResurrectResolve.Resolve(new BandageResurrectRequest
            {
                TargetGhost = true,
                TargetAvatar = true,
                HasBandage = true,
                Distance = 1f,
                Skills = locked,
                Force = true
            });
            if (!lockedOk.Applied)
                throw new InvalidOperationException("잠긴 Healing도 부활 적용은 되어야 합니다.");
            if (Math.Abs(locked.Get(SkillId.Healing)) > 0.0001f)
                throw new InvalidOperationException("잠긴 Healing은 오르면 안 됩니다.");

            OfflineWorld.Instance?.ResetHousePlot();
            var healerGo = new GameObject("selfcheck-healrez-healer");
            var ghostGo = new GameObject("selfcheck-healrez-ghost");
            GameObject worldGo = null;
            GameObject stationGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-healrez-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                world.ResetHousePlot();

                healerGo.transform.position = new Vector3(42f, 0f, 0f);
                var healer = healerGo.AddComponent<WorldBody>();
                healer.IsAvatar = true;
                healer.RecalcFromStr(30);
                healer.ResetHp();
                var bag = healerGo.AddComponent<InventoryBag>();
                bag.Add(ItemCatalog.Bandage, 2);

                ghostGo.transform.position = healerGo.transform.position;
                var ghost = ghostGo.AddComponent<WorldBody>();
                ghost.IsAvatar = true;
                ghost.DisplayName = "유령";
                ghost.RecalcFromStr(30);
                ghost.ResetHp();
                ghost.Ghost = true;
                ghost.SetHp(0f);
                if (!ghost.Ghost)
                    throw new InvalidOperationException("대상은 Ghost여야 합니다.");

                var living = world.TryResurrectBandage(healer, healer);
                if (living.Applied)
                    throw new InvalidOperationException("살아있는 시술자 자신은 붕대 부활되면 안 됩니다.");

                var hit = world.TryResurrectBandage(healer, ghost);
                if (!hit.Applied || ghost.Ghost)
                    throw new InvalidOperationException("서버 붕대 부활 실패: " + hit.FailReason);
                if (Math.Abs(world.SkillsOf(healer).Get(SkillId.Healing) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("서버 붕대 부활 후 Healing 0.1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(healer).Get(SkillId.Magery)) > 0.0001f)
                    throw new InvalidOperationException("서버 붕대 부활은 마법을 올리면 안 됩니다.");
                int left = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.Bandage)
                        left += bag.Items[i].Amount;
                if (left != 1)
                    throw new InvalidOperationException("성공 붕대 부활은 붕대 1을 소모해야 합니다.");
                if (ghost.Hp <= 0f || ghost.Ghost)
                    throw new InvalidOperationException("부활 후 HP가 회복되어야 합니다.");
                if (string.IsNullOrEmpty(world.LastHealRezMessage) || world.LastHealRezMessage.IndexOf("부활", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("붕대 부활 메시지가 있어야 합니다.");

                // TryHeal routes to resurrect when target ghost
                ghost.Ghost = true;
                ghost.SetHp(0f);
                ghost.Ghost = true;
                var viaHeal = world.TryHeal(healer, ghost);
                if (!viaHeal.Applied || ghost.Ghost)
                    throw new InvalidOperationException("TryHeal(ghost)는 붕대 부활이어야 합니다: " + viaHeal.FailReason);

                // HealerStation path stays
                ghost.Ghost = true;
                ghost.SetHp(0f);
                ghost.Ghost = true;
                stationGo = new GameObject("selfcheck-healrez-station");
                stationGo.transform.position = ghostGo.transform.position;
                var station = stationGo.AddComponent<HealerStation>();
                var stationRez = world.TryResurrect(ghost, station);
                if (!stationRez.Applied || ghost.Ghost)
                    throw new InvalidOperationException("HealerStation TryResurrect가 유지되어야 합니다: " + stationRez.FailReason);

                // fail: too far
                ghost.Ghost = true;
                ghost.SetHp(0f);
                ghost.Ghost = true;
                bag.Add(ItemCatalog.Bandage, 1);
                ghostGo.transform.position = healerGo.transform.position + new Vector3(ItemCatalog.MeleeRange + 2f, 0f, 0f);
                var ranged = world.TryResurrectBandage(healer, ghost);
                if (ranged.Applied)
                    throw new InvalidOperationException("먼 거리 붕대 부활은 실패해야 합니다.");

                // fail: healer ghost
                healer.Ghost = true;
                ghostGo.transform.position = healerGo.transform.position;
                var hg = world.TryResurrectBandage(healer, ghost);
                if (hg.Applied)
                    throw new InvalidOperationException("유령 시술자 서버 부활은 실패해야 합니다.");
                healer.Ghost = false;
                healer.ResetHp();

                AssertDungeon3Leftover("붕대 부활 후");
            }
            finally
            {
                OfflineWorld.Instance?.ResetHousePlot();
                UnityEngine.Object.DestroyImmediate(healerGo);
                UnityEngine.Object.DestroyImmediate(ghostGo);
                if (stationGo != null)
                    UnityEngine.Object.DestroyImmediate(stationGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }

        static void AssertBandageDetox()
        {
            AssertDungeon3Leftover();
            string[] keep = { "Forge", "Vendor", "Healer", HousingPlot.VendorObject, StableYard.Object };
            for (int i = 0; i < keep.Length; i++)
            {
                if (GameObject.Find(keep[i]) == null)
                    throw new InvalidOperationException("마을 랜드마크가 있어야 합니다: " + keep[i]);
            }
            var decor = GameObject.Find("VillageDecor");
            if (decor == null || decor.transform.childCount < 200)
                throw new InvalidOperationException("VillageDecor 울타리/집을 지우면 안 됩니다.");

            if (SkillNames.KoreanOf(SkillId.Healing) != "치유")
                throw new InvalidOperationException("치유 스킬명을 바꾸면 안 됩니다.");

            var ghostSkills = new SkillSet();
            var ghostFail = BandageCurePoisonResolve.Resolve(new BandageCurePoisonRequest
            {
                HealerGhost = false,
                TargetGhost = true,
                TargetAlive = false,
                PoisonTicks = PoisoningResolve.TickCount,
                HasBandage = true,
                Distance = 1f,
                Skills = ghostSkills
            });
            if (ghostFail.Applied || ghostFail.FailReason != "ghost")
                throw new InvalidOperationException("유령 대상 붕대 해독은 실패해야 합니다.");
            if (Math.Abs(ghostSkills.Get(SkillId.Healing)) > 0.0001f)
                throw new InvalidOperationException("실패한 해독은 Healing을 올리면 안 됩니다.");

            var healerGhost = BandageCurePoisonResolve.Resolve(new BandageCurePoisonRequest
            {
                HealerGhost = true,
                TargetGhost = false,
                TargetAlive = true,
                PoisonTicks = PoisoningResolve.TickCount,
                HasBandage = true,
                Distance = 1f,
                Skills = new SkillSet()
            });
            if (healerGhost.Applied || healerGhost.FailReason != "ghost")
                throw new InvalidOperationException("유령 시술자 붕대 해독은 실패해야 합니다.");

            var noPoisonSkills = new SkillSet();
            var noPoison = BandageCurePoisonResolve.Resolve(new BandageCurePoisonRequest
            {
                TargetAlive = true,
                PoisonTicks = 0,
                HasBandage = true,
                Distance = 1f,
                Skills = noPoisonSkills
            });
            if (noPoison.Applied || noPoison.FailReason != "no_poison")
                throw new InvalidOperationException("독 없는 붕대 해독은 실패해야 합니다.");

            var noBnSkills = new SkillSet();
            var noBn = BandageCurePoisonResolve.Resolve(new BandageCurePoisonRequest
            {
                TargetAlive = true,
                PoisonTicks = PoisoningResolve.TickCount,
                HasBandage = false,
                Distance = 1f,
                Skills = noBnSkills
            });
            if (noBn.Applied || noBn.FailReason != "no_bandage")
                throw new InvalidOperationException("붕대 없는 해독은 실패해야 합니다.");
            if (Math.Abs(noBnSkills.Get(SkillId.Healing)) > 0.0001f)
                throw new InvalidOperationException("붕대 없는 해독은 스킬을 올리면 안 됩니다.");

            var farSkills = new SkillSet();
            var far = BandageCurePoisonResolve.Resolve(new BandageCurePoisonRequest
            {
                TargetAlive = true,
                PoisonTicks = PoisoningResolve.TickCount,
                HasBandage = true,
                Distance = ItemCatalog.MeleeRange + 1f,
                Range = ItemCatalog.MeleeRange,
                Skills = farSkills
            });
            if (far.Applied || far.FailReason != "range")
                throw new InvalidOperationException("사거리 밖 붕대 해독은 실패해야 합니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            int dexWas = stats.Dex;
            var ok = BandageCurePoisonResolve.Resolve(new BandageCurePoisonRequest
            {
                TargetAlive = true,
                PoisonTicks = PoisoningResolve.TickCount,
                HasBandage = true,
                Distance = 1f,
                Skills = skills,
                Stats = stats,
                Difficulty = BandageCurePoisonResolve.Difficulty
            });
            if (!ok.Applied)
                throw new InvalidOperationException("붕대 해독 Resolve는 성공해야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Healing) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("성공 붕대 해독 후 Healing 0.1이어야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Magery)) > 0.0001f)
                throw new InvalidOperationException("붕대 해독은 마법을 올리면 안 됩니다.");
            if (Math.Abs(skills.Get(SkillId.Veterinary)) > 0.0001f)
                throw new InvalidOperationException("붕대 해독은 수의학을 올리면 안 됩니다.");
            if (stats.Dex != dexWas + 1)
                throw new InvalidOperationException("붕대 해독 상승 시 DEX가 올라야 합니다.");

            var forceSkills = new SkillSet();
            var forced = BandageCurePoisonResolve.Resolve(new BandageCurePoisonRequest
            {
                TargetAlive = true,
                PoisonTicks = PoisoningResolve.TickCount,
                HasBandage = true,
                Distance = 1f,
                Skills = forceSkills,
                Stats = new StatSet(),
                Force = true
            });
            if (!forced.Applied || Math.Abs(forceSkills.Get(SkillId.Healing) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("Force 경로도 Healing 0.1이어야 합니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.Healing, SkillLock.Locked);
            var lockedOk = BandageCurePoisonResolve.Resolve(new BandageCurePoisonRequest
            {
                TargetAlive = true,
                PoisonTicks = PoisoningResolve.TickCount,
                HasBandage = true,
                Distance = 1f,
                Skills = locked,
                Force = true
            });
            if (!lockedOk.Applied)
                throw new InvalidOperationException("잠긴 Healing도 해독 적용은 되어야 합니다.");
            if (Math.Abs(locked.Get(SkillId.Healing)) > 0.0001f)
                throw new InvalidOperationException("잠긴 Healing은 오르면 안 됩니다.");

            OfflineWorld.Instance?.ResetHousePlot();
            var healerGo = new GameObject("selfcheck-detox-healer");
            var allyGo = new GameObject("selfcheck-detox-ally");
            GameObject worldGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-detox-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                world.ResetHousePlot();

                healerGo.transform.position = new Vector3(42f, 0f, 0f);
                var healer = healerGo.AddComponent<WorldBody>();
                healer.IsAvatar = true;
                healer.DisplayName = "치유사";
                healer.RecalcFromStr(30);
                healer.ResetHp();
                var bag = healerGo.AddComponent<InventoryBag>();
                bag.Add(ItemCatalog.Bandage, 3);

                // self cure
                healer.PoisonTicks = PoisoningResolve.TickCount;
                healer.NextPoisonAt = UnityEngine.Time.time + 10f;
                var selfHit = world.TryCurePoison(healer, healer);
                if (!selfHit.Applied)
                    throw new InvalidOperationException("자가 붕대 해독 실패: " + selfHit.FailReason);
                if (healer.PoisonTicks != 0 || healer.NextPoisonAt != 0f)
                    throw new InvalidOperationException("해독 후 독 틱이 남아 있으면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(healer).Get(SkillId.Healing) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("서버 해독 후 Healing 0.1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(healer).Get(SkillId.Magery)) > 0.0001f)
                    throw new InvalidOperationException("붕대 해독은 Magery를 올리면 안 됩니다.");
                int left = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.Bandage)
                        left += bag.Items[i].Amount;
                if (left != 2)
                    throw new InvalidOperationException("성공 해독은 붕대 1을 소모해야 합니다.");
                if (string.IsNullOrEmpty(world.LastCurePoisonMessage) || world.LastCurePoisonMessage.IndexOf("해독", System.StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("해독 메시지가 있어야 합니다.");

                // no poison fail
                var none = world.TryCurePoison(healer, healer);
                if (none.Applied || none.FailReason != "no_poison")
                    throw new InvalidOperationException("독 없을 때 TryCurePoison은 no_poison이어야 합니다.");

                // ally via TryHeal route
                allyGo.transform.position = healerGo.transform.position;
                var ally = allyGo.AddComponent<WorldBody>();
                ally.IsAvatar = true;
                ally.IsEnemy = false;
                ally.DisplayName = "동료";
                ally.RecalcFromStr(30);
                ally.ResetHp();
                ally.PoisonTicks = PoisoningResolve.TickCount;
                ally.NextPoisonAt = UnityEngine.Time.time + 5f;
                float healBefore = world.SkillsOf(healer).Get(SkillId.Healing);
                var viaHeal = world.TryHeal(healer, ally);
                if (!viaHeal.Applied)
                    throw new InvalidOperationException("TryHeal(poisoned)는 해독이어야 합니다: " + viaHeal.FailReason);
                if (ally.PoisonTicks != 0 || ally.NextPoisonAt != 0f)
                    throw new InvalidOperationException("TryHeal 해독 후 독이 남아 있으면 안 됩니다.");
                if (world.SkillsOf(healer).Get(SkillId.Healing) < healBefore)
                    throw new InvalidOperationException("TryHeal 해독 경로도 Healing이 유지/상승해야 합니다.");

                // range fail
                ally.PoisonTicks = PoisoningResolve.TickCount;
                allyGo.transform.position = healerGo.transform.position + new Vector3(ItemCatalog.MeleeRange + 2f, 0f, 0f);
                var ranged = world.TryCurePoison(healer, ally);
                if (ranged.Applied || ranged.FailReason != "range")
                    throw new InvalidOperationException("먼 거리 해독은 range 실패여야 합니다.");

                // no bandage
                allyGo.transform.position = healerGo.transform.position;
                ally.PoisonTicks = PoisoningResolve.TickCount;
                for (int i = bag.Items.Count - 1; i >= 0; i--)
                    if (bag.Items[i].TemplateId == ItemCatalog.Bandage)
                        bag.Items.RemoveAt(i);
                var noBag = world.TryCurePoison(healer, ally);
                if (noBag.Applied || noBag.FailReason != "no_bandage")
                    throw new InvalidOperationException("붕대 없으면 no_bandage여야 합니다.");
                if (ally.PoisonTicks != PoisoningResolve.TickCount)
                    throw new InvalidOperationException("실패한 해독은 독을 지우면 안 됩니다.");

                // ghost target
                bag.Add(ItemCatalog.Bandage, 1);
                ally.Ghost = true;
                ally.SetHp(0f);
                ally.Ghost = true;
                ally.PoisonTicks = PoisoningResolve.TickCount;
                var gh = world.TryCurePoison(healer, ally);
                if (gh.Applied || gh.FailReason != "ghost")
                    throw new InvalidOperationException("유령 대상 서버 해독은 ghost 실패여야 합니다.");

                AssertDungeon3Leftover("붕대 해독 후");
                world.ResetHousePlot();
            }
            finally
            {
                OfflineWorld.Instance?.ResetHousePlot();
                UnityEngine.Object.DestroyImmediate(healerGo);
                UnityEngine.Object.DestroyImmediate(allyGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }

    }
}
