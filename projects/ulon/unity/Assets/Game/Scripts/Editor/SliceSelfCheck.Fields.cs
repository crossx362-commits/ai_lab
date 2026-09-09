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


    }
}
