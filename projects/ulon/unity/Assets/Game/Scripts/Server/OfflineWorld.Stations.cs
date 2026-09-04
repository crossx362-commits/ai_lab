using System.Collections.Generic;
using UnityEngine;
using Ulon.Shared;

namespace Ulon.Server
{
    public sealed partial class OfflineWorld
    {
        public static ResourceNode FindNode(string name)
        {
            var nodes = Object.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
            for (int i = 0; i < nodes.Length; i++)
                if (nodes[i].gameObject.name == name)
                    return nodes[i];
            return null;
        }

        public static CraftStation FindStation(string name)
        {
            var stations = Object.FindObjectsByType<CraftStation>(FindObjectsSortMode.None);
            for (int i = 0; i < stations.Length; i++)
                if (stations[i].gameObject.name == name)
                    return stations[i];
            return null;
        }

        public static BankStation FindBank(string name)
        {
            var banks = Object.FindObjectsByType<BankStation>(FindObjectsSortMode.None);
            for (int i = 0; i < banks.Length; i++)
                if (banks[i].gameObject.name == name)
                    return banks[i];
            return null;
        }

        public static HousePlotStation FindHouseStation(string name)
        {
            var stations = Object.FindObjectsByType<HousePlotStation>(FindObjectsSortMode.None);
            for (int i = 0; i < stations.Length; i++)
                if (stations[i].gameObject.name == name)
                    return stations[i];
            return null;
        }

        public static HouseChest FindHouseChest(string name)
        {
            var chests = Object.FindObjectsByType<HouseChest>(FindObjectsSortMode.None);
            for (int i = 0; i < chests.Length; i++)
                if (chests[i].gameObject.name == name)
                    return chests[i];
            return null;
        }

        public static HouseVendor FindHouseVendor(string name)
        {
            var list = Object.FindObjectsByType<HouseVendor>(FindObjectsSortMode.None);
            for (int i = 0; i < list.Length; i++)
                if (list[i].gameObject.name == name)
                    return list[i];
            return list.Length > 0 ? list[0] : null;
        }

        static void EnsureBanker()
        {
            var go = GameObject.Find("Banker");
            if (go == null)
                go = GameObject.Find("windmill");
            if (go == null)
                return;
            go.name = "Banker";
            if (go.GetComponent<BankStation>() == null)
            {
                var bank = go.AddComponent<BankStation>();
                bank.DisplayName = "은행";
            }
            if (go.GetComponentInChildren<Collider>() == null)
                go.AddComponent<BoxCollider>();
        }

        static void EnsureHealer()
        {
            var go = GameObject.Find("Healer");
            if (go == null)
                go = GameObject.Find("fountain-round");
            if (go == null)
                return;
            go.name = "Healer";
            if (go.GetComponent<HealerStation>() == null)
            {
                var healer = go.AddComponent<HealerStation>();
                healer.DisplayName = "치유사";
            }
            if (go.GetComponentInChildren<Collider>() == null)
                go.AddComponent<BoxCollider>();
        }

        static void EnsureResinBush()
        {
            var go = GameObject.Find("ResinBush");
            if (go == null)
                go = GameObject.Find("plant_bushLarge");
            if (go == null)
                go = GameObject.Find("plant_bush");
            if (go == null)
                return;
            go.name = "ResinBush";
            var node = go.GetComponent<ResourceNode>() ?? go.AddComponent<ResourceNode>();
            node.ResourceId = SpellCast.Reagent;
            node.DisplayName = "수지 덤불";
            node.GatherSkill = SkillId.Magery;
            if (node.Remaining <= 0)
                node.Remaining = 8;
            node.Capacity = 8;
            node.Difficulty = 8f;
            node.RespawnSeconds = 8f;
            if (go.GetComponentInChildren<Collider>() == null)
                go.AddComponent<BoxCollider>();
        }


        static void EnsureFieldOak()
        {
            var go = GameObject.Find("FieldOak");
            if (go == null)
                return;
            var node = go.GetComponent<ResourceNode>() ?? go.AddComponent<ResourceNode>();
            node.ResourceId = "wood";
            node.DisplayName = "들판 참나무";
            node.GatherSkill = SkillId.Lumberjacking;
            if (node.Remaining <= 0)
                node.Remaining = 12;
            node.Capacity = 12;
            node.Difficulty = 10f;
            node.RespawnSeconds = 8f;
            if (go.GetComponentInChildren<Collider>() == null)
                go.AddComponent<BoxCollider>();
        }

        static void EnsureFieldFlax()
        {
            var go = GameObject.Find("FieldFlax");
            if (go == null)
                return;
            var node = go.GetComponent<ResourceNode>() ?? go.AddComponent<ResourceNode>();
            node.ResourceId = ItemCatalog.Cloth;
            node.DisplayName = "들판 아마";
            node.GatherSkill = SkillId.Tailoring;
            if (node.Remaining <= 0)
                node.Remaining = 10;
            node.Capacity = 10;
            node.Difficulty = 10f;
            node.RespawnSeconds = 8f;
            if (go.GetComponentInChildren<Collider>() == null)
                go.AddComponent<BoxCollider>();
        }


        static void EnsureFieldOre()
        {
            var go = GameObject.Find("FieldOre");
            if (go == null)
                return;
            var node = go.GetComponent<ResourceNode>() ?? go.AddComponent<ResourceNode>();
            node.ResourceId = "iron_ore";
            node.DisplayName = "들판 광맥";
            node.GatherSkill = SkillId.Mining;
            if (node.Remaining <= 0)
                node.Remaining = 10;
            node.Capacity = 10;
            node.Difficulty = 10f;
            node.RespawnSeconds = 8f;
            if (go.GetComponentInChildren<Collider>() == null)
                go.AddComponent<BoxCollider>();
        }

        static void EnsureFishSpot()
        {
            var go = GameObject.Find("FishingSpot");
            if (go == null)
                go = GameObject.Find("watermill");
            if (go == null)
                return;
            go.name = "FishingSpot";
            var node = go.GetComponent<ResourceNode>() ?? go.AddComponent<ResourceNode>();
            node.ResourceId = ItemCatalog.Fish;
            node.DisplayName = "물가";
            node.GatherSkill = SkillId.Fishing;
            if (node.Remaining <= 0)
                node.Remaining = 12;
            node.Capacity = 12;
            node.Difficulty = 10f;
            node.RespawnSeconds = 8f;
            node.InteractRange = 2.8f;
            if (go.GetComponentInChildren<Collider>() == null)
                go.AddComponent<BoxCollider>();
        }

        static void EnsureCampfire()
        {
            var go = GameObject.Find("Campfire");
            if (go == null)
            {
                var mill = GameObject.Find("FishingSpot");
                go = new GameObject("Campfire");
                if (mill != null)
                    go.transform.position = mill.transform.position + new Vector3(2.3f, 0f, 1.7f);
                else
                    go.transform.position = new Vector3(-9.2f, 0f, -6.8f);
                go.AddComponent<BoxCollider>();
            }
            go.name = "Campfire";
            var station = go.GetComponent<CraftStation>() ?? go.AddComponent<CraftStation>();
            station.RecipeId = "cooked_fish";
            station.DisplayName = "화덕";
            if (go.GetComponentInChildren<Collider>() == null)
                go.AddComponent<BoxCollider>();
        }


        static void EnsureMortar()
        {
            var go = GameObject.Find("Mortar");
            if (go == null)
            {
                var fire = GameObject.Find("Campfire");
                go = new GameObject("Mortar");
                if (fire != null)
                    go.transform.position = fire.transform.position + new Vector3(2.4f, 0f, 0.2f);
                else
                    go.transform.position = new Vector3(-6.8f, 0f, -6.6f);
                go.AddComponent<BoxCollider>();
            }
            go.name = "Mortar";
            var station = go.GetComponent<CraftStation>() ?? go.AddComponent<CraftStation>();
            station.RecipeId = "health_potion";
            station.DisplayName = "절구";
            if (go.GetComponentInChildren<Collider>() == null)
                go.AddComponent<BoxCollider>();
        }


        static void EnsureLockedCrate()
        {
            var go = GameObject.Find("LockedCrate");
            if (go == null)
                go = GameObject.Find("cart");
            if (go == null)
            {
                go = new GameObject("LockedCrate");
                go.transform.position = new Vector3(-7.4f, 0f, 6.4f);
                go.AddComponent<BoxCollider>();
            }
            go.name = "LockedCrate";
            var crate = go.GetComponent<LockedCrate>() ?? go.AddComponent<LockedCrate>();
            crate.DisplayName = "잠긴 상자";
            if (go.GetComponentInChildren<Collider>() == null)
                go.AddComponent<BoxCollider>();
        }

        static void EnsureVendor()
        {
            var go = GameObject.Find("Vendor");
            if (go == null)
                go = GameObject.Find("stall");
            if (go == null)
                return;
            go.name = "Vendor";
            if (go.GetComponent<VendorStation>() == null)
            {
                var v = go.AddComponent<VendorStation>();
                v.DisplayName = "잡화";
            }
            if (go.GetComponentInChildren<Collider>() == null)
                go.AddComponent<BoxCollider>();
        }


        static void EnsureCarpenter()
        {
            var go = GameObject.Find("Carpenter");
            if (go == null)
                go = GameObject.Find("stall-bench");
            if (go == null)
            {
                var forge = GameObject.Find("Forge");
                go = new GameObject("Carpenter");
                if (forge != null)
                    go.transform.position = forge.transform.position + new Vector3(-2.1f, 0f, 1.8f);
                go.AddComponent<BoxCollider>();
            }
            go.name = "Carpenter";
            var station = go.GetComponent<CraftStation>() ?? go.AddComponent<CraftStation>();
            station.RecipeId = "wooden_club";
            station.DisplayName = "목공소";
            if (go.GetComponentInChildren<Collider>() == null)
                go.AddComponent<BoxCollider>();
        }

        static void EnsureTrainer()
        {
            var go = GameObject.Find("Trainer");
            if (go == null)
                return;
            if (go.GetComponent<TrainerStation>() == null)
            {
                var t = go.AddComponent<TrainerStation>();
                t.DisplayName = "훈련사";
            }
        }

        public static HealerStation FindHealer(string name)
        {
            var list = Object.FindObjectsByType<HealerStation>(FindObjectsSortMode.None);
            for (int i = 0; i < list.Length; i++)
                if (list[i].gameObject.name == name)
                    return list[i];
            return null;
        }

        public static CorpseNode FindCorpse(string ownerId)
        {
            var list = Object.FindObjectsByType<CorpseNode>(FindObjectsSortMode.None);
            for (int i = 0; i < list.Length; i++)
                if (list[i].OwnerId == ownerId)
                    return list[i];
            return null;
        }

    }
}
