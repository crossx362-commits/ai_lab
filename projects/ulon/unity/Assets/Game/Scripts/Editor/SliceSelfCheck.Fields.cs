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
        /// <summary>
        /// 맵 크기는 원장(WorldTerrain.Span)이 정한다 — 필드·콘텐츠를 추가하면서 슬쩍 키우면 안 된다.
        /// 같은 식이 세 곳에 복붙돼 있었다(2026-09-06 산·바다 작업에서 발견) — 한 곳으로 모은다.
        /// </summary>
        static void AssertMapSizeIsLedger()
        {
            var terrain = GameObject.Find("Ground");
            var t = terrain != null ? terrain.GetComponent<Terrain>() : null;
            if (t == null || t.terrainData == null)
                throw new InvalidOperationException("Ground Terrain이 있어야 합니다.");
            Vector3 size = t.terrainData.size;
            if (Math.Abs(size.x - WorldTerrain.Span) > 0.1f || Math.Abs(size.z - WorldTerrain.Span) > 0.1f)
                throw new InvalidOperationException("맵 크기가 " + size.x + "x" + size.z + "입니다 — 원장(WorldTerrain.Span) " + WorldTerrain.Span + "와 다릅니다.");
        }

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
            AssertMapSizeIsLedger();

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
            AssertMapSizeIsLedger();

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
            AssertMapSizeIsLedger();

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


    }
}
