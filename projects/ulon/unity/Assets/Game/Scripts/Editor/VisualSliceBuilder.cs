using System;
using System.Collections.Generic;
using System.IO;
using Ulon.Client;
using Ulon.Server;
using Ulon.Shared;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ulon.Editor
{
    public static class VisualSliceBuilder
    {
        const string ScenePath = "Assets/Game/Scenes/Bootstrap.unity";
        const string ControllerPath = "Assets/Game/Art/Characters/SharedLocomotion.controller";
        const string KnightFbx = "Assets/_ThirdParty/KayKit/Adventurers/RAW/Characters/Knight.fbx";
        const string BarbarianFbx = "Assets/_ThirdParty/KayKit/Adventurers/RAW/Characters/Barbarian.fbx";
        const string MageFbx = "Assets/_ThirdParty/KayKit/Adventurers/RAW/Characters/Mage.fbx";
        const string SkeletonFbx = "Assets/_ThirdParty/KayKit/Skeletons/RAW/Characters/Skeleton_Warrior.fbx";
        const string SwordFbx = "Assets/_ThirdParty/KayKit/Adventurers/RAW/Weapons/sword_1handed.fbx";
        const string ShieldFbx = "Assets/_ThirdParty/KayKit/Adventurers/RAW/Weapons/shield_round.fbx";

        [MenuItem("Ulon/Fix Character Animation")]
        public static void FixCharacterAnimation()
        {
            EditorSceneManager.OpenScene(ScenePath);
            ConfigureHumanoid(KnightFbx, true);
            ConfigureHumanoid(BarbarianFbx, true);
            ConfigureHumanoid(MageFbx, true);
            ConfigureHumanoid(SkeletonFbx, true);
            AnimationClip[] clips = LoadClips(KnightFbx);
            AnimationClip idle = BestClip(clips, new[] { "idle" }, new[] { "attack", "walk", "run", "combat" });
            AnimationClip walk = BestClip(clips, new[] { "walking", "walk" }, new[] { "attack", "strafe" });
            AnimationClip run = BestClip(clips, new[] { "running", "run" }, new[] { "attack" });
            AnimationClip attack = BestClip(clips, new[] { "1h_melee_attack", "attack_chop", "melee_attack", "attack" }, new[] { "idle" });
            if (idle == null)
                throw new InvalidOperationException("Idle 클립 없음");
            AnimatorController controller = BuildController(idle, walk, run, attack);
            StripAndAssign(GameObject.Find("Player"), controller);
            StripAndAssign(GameObject.Find("Companion"), controller);
            StripAndAssign(GameObject.Find("Skeleton"), controller);
            StripAndAssign(GameObject.Find("Bandit"), controller);
            var prefab = PrefabUtility.LoadPrefabContents("Assets/Game/Prefabs/NetPlayer.prefab");
            StripAndAssign(prefab, controller);
            PrefabUtility.SaveAsPrefabAsset(prefab, "Assets/Game/Prefabs/NetPlayer.prefab");
            PrefabUtility.UnloadPrefabContents(prefab);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[Ulon] Animation fix. idle=" + idle.name + " loop=" + idle.isLooping
                      + " walk=" + (walk != null ? walk.name : "-")
                      + " run=" + (run != null ? run.name : "-"));
        }

        [MenuItem("Ulon/Fix Slice Props")]
        public static void FixSliceProps()
        {
            EditorSceneManager.OpenScene(ScenePath);
            ReplaceNamedWithModel("IronVein", "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/rock-large.fbx",
                go =>
                {
                    var node = go.GetComponent<ResourceNode>() ?? go.AddComponent<ResourceNode>();
                    node.ResourceId = "iron_ore";
                    node.DisplayName = "철 광맥";
                    node.GatherSkill = SkillId.Mining;
                    node.Remaining = 12;
                    node.Capacity = 12;
                    node.RespawnSeconds = 8f;
                    node.Difficulty = 10f;
                });
            ReplaceNamedWithModel("Forge", "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/stall.fbx",
                go =>
                {
                    var station = go.GetComponent<CraftStation>() ?? go.AddComponent<CraftStation>();
                    station.RecipeId = "iron_sword";
                    station.DisplayName = "대장간";
                });
            EnsureCarpenterLandmark();
            GameObject tree = GameObject.Find("OakTree");
            if (tree == null)
                tree = GameObject.Find("tree-high");
            if (tree == null)
                tree = GameObject.Find("tree");
            if (tree != null)
            {
                tree.name = "OakTree";
                var node = tree.GetComponent<ResourceNode>() ?? tree.AddComponent<ResourceNode>();
                node.ResourceId = "wood";
                node.DisplayName = "참나무";
                node.GatherSkill = SkillId.Lumberjacking;
                node.Remaining = 12;
                node.Capacity = 12;
                node.RespawnSeconds = 8f;
                node.Difficulty = 10f;
                EnsureCollider(tree);
            }
            GameObject bush = GameObject.Find("ResinBush");
            if (bush == null)
                bush = GameObject.Find("plant_bushLarge");
            if (bush == null)
                bush = GameObject.Find("plant_bush");
            if (bush != null)
            {
                bush.name = "ResinBush";
                var resin = bush.GetComponent<ResourceNode>() ?? bush.AddComponent<ResourceNode>();
                resin.ResourceId = SpellCast.Reagent;
                resin.DisplayName = "수지 덤불";
                resin.GatherSkill = SkillId.Magery;
                resin.Remaining = 8;
                resin.Capacity = 8;
                resin.Difficulty = 8f;
                resin.RespawnSeconds = 8f;
                EnsureCollider(bush);
            }
            GameObject mill = GameObject.Find("Banker");
            if (mill == null)
                mill = GameObject.Find("windmill");
            if (mill != null)
            {
                mill.name = "Banker";
                var bank = mill.GetComponent<BankStation>() ?? mill.AddComponent<BankStation>();
                bank.DisplayName = "은행";
                EnsureCollider(mill);
            }
            GameObject fountain = GameObject.Find("Healer");
            if (fountain == null)
                fountain = GameObject.Find("fountain-round");
            if (fountain != null)
            {
                fountain.name = "Healer";
                var healer = fountain.GetComponent<HealerStation>() ?? fountain.AddComponent<HealerStation>();
                healer.DisplayName = "치유사";
                EnsureCollider(fountain);
            }
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[Ulon] Slice props: vein/forge meshes + OakTree lumber");
        }

        [MenuItem("Ulon/Dress Village")]
        public static void DressVillage()
        {
            EditorSceneManager.OpenScene(ScenePath);
            SetupLighting();
            SetupSky();
            ImproveGround();
            DressVillageInOpenScene();
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[Ulon] Village: road-aligned houses, plaza off cross, hunt north");
        }

        public static void BatchDressVillage()
        {
            DressVillage();
        }

        static void DressVillageInOpenScene()
        {
            PlaceLandmarks();
            var old = GameObject.Find("VillageDecor");
            if (old != null)
                UnityEngine.Object.DestroyImmediate(old);
            var root = new GameObject("VillageDecor");
            Transform parent = root.transform;
            const string Fence = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/fence.fbx";
            const string Gate = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/fence-gate.fbx";
            const string Grass = "Assets/_ThirdParty/Kenney/Nature/RAW/Models/grass.fbx";
            const string Tree = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/tree.fbx";
            const string TreeH = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/tree-high.fbx";
            const string RockS = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/rock-small.fbx";
            const string RockN = "Assets/_ThirdParty/Kenney/Nature/RAW/Models/rock_smallA.fbx";
            const string Bush = "Assets/_ThirdParty/Kenney/Nature/RAW/Models/plant_bush.fbx";
            const string Hedge = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/hedge.fbx";
            const string Lantern = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/lantern.fbx";
            const string Wall = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/wall-window-glass.fbx";
            const string Shutters = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/wall-window-shutters.fbx";
            const string Door = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/wall-door.fbx";
            const string RoofHigh = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/roof-high.fbx";
            const string Chimney = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/chimney.fbx";
            const string WoodWall = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/wall-wood-window-glass.fbx";
            const string WoodDoor = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/wall-wood-door.fbx";
            const string Roof = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/roof.fbx";
            string[] extra =
            {
                Fence, Gate, Grass, RockS, RockN, Hedge, Lantern, Wall, Shutters, Door, RoofHigh, Chimney, WoodWall,
                WoodDoor, Roof,
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/stall-bench.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/stall-stool.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/stall-red.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/stall-green.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/cart-high.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/overhang.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/hedge-large.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/tree-high-round.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/tree-crooked.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/tree-high-crooked.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/road.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/roof-high-gable-end.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/roof-gable-end.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/banner-red.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/stairs-wood.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/rock-wide.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/poles.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/watermill.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/wall-arch.fbx"
            };
            for (int i = 0; i < extra.Length; i++)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(extra[i]) == null)
                    ConfigureProp(extra[i]);
            }

            ApplyVillageDecor(parent, Fence, Gate, Hedge, Lantern, Tree, TreeH, Bush, RockS, RockN,
                Wall, Shutters, Door, RoofHigh, Chimney, WoodWall, WoodDoor, Roof);
            FramePlayCamera();
            ApplyVillageMaterials();
            AssertVillageVisuals();
        }

        static void ApplyVillageDecor(Transform parent, string Fence, string Gate, string Hedge, string Lantern,
            string Tree, string TreeH, string Bush, string RockS, string RockN,
            string Wall, string Shutters, string Door, string RoofHigh, string Chimney,
            string WoodWall, string WoodDoor, string Roof)
        {
            PlaceVillageFloor(parent);
            PlaceFenceRing(parent, Fence, Gate);
            PlaceTownFill(parent, Fence, Hedge, Lantern, Tree, TreeH, Bush, RockS, RockN);
            // Door-south houses sit just north of the EW road. Pivot is local SW.
            PlaceHouse(parent, new Vector3(-7.5f, 0f, 1.2f), 0f, 2, Wall, Door, RoofHigh, Chimney, false);
            PlaceHouse(parent, new Vector3(2.5f, 0f, 1.2f), 0f, 2, Shutters, Door, Roof, Chimney, false);
            // Door-north (yaw 180): pivot is local SW which lands at the NE of the footprint.
            PlaceHouse(parent, new Vector3(-5.5f, 0f, -2.1f), 180f, 2, WoodWall, WoodDoor, RoofHigh, Chimney, false);
            PlaceHouse(parent, new Vector3(4.5f, 0f, -2.1f), 180f, 2, Wall, Door, Roof, Chimney, false);
            // Door-west (yaw 90) along the east curb of the NS road.
            PlaceHouse(parent, new Vector3(1.2f, 0f, -4.8f), 90f, 2, WoodWall, WoodDoor, RoofHigh, Chimney, true);
            // Door-east (yaw 270) along the west curb.
            PlaceHouse(parent, new Vector3(-2.2f, 0f, 4.2f), 270f, 2, Shutters, Door, RoofHigh, Chimney, true);
        }

        [MenuItem("Ulon/Assert Village Visuals")]
        public static void AssertVillageVisualsMenu()
        {
            AssertVillageVisuals();
            Debug.Log("[Ulon] Village visuals OK");
        }

        static void ApplyVillageMaterials()
        {
            var grassMat = MakeNoiseMat("KenneyGrass", new Color(0.32f, 0.52f, 0.2f), new Color(0.24f, 0.42f, 0.14f));
            var dirtMat = MakeNoiseMat("KenneyDirt", new Color(0.52f, 0.38f, 0.24f), new Color(0.4f, 0.28f, 0.16f));
            string[] grassFbx =
            {
                "Assets/_ThirdParty/Kenney/Nature/RAW/Models/grass_large.fbx",
                "Assets/_ThirdParty/Kenney/Nature/RAW/Models/grass.fbx",
                "Assets/_ThirdParty/Kenney/Nature/RAW/Models/grass_leafs.fbx",
                "Assets/_ThirdParty/Kenney/Nature/RAW/Models/plant_bush.fbx",
                "Assets/_ThirdParty/Kenney/Nature/RAW/Models/plant_bushLarge.fbx",
                "Assets/_ThirdParty/Kenney/Nature/RAW/Models/grass_leafs.fbx",
                "Assets/_ThirdParty/Kenney/Nature/RAW/Models/ground_grass.fbx"
            };
            string[] dirtFbx =
            {
                "Assets/_ThirdParty/Kenney/Nature/RAW/Models/rock_smallA.fbx",
                "Assets/_ThirdParty/Kenney/Nature/RAW/Models/rock_largeA.fbx",
                "Assets/_ThirdParty/Kenney/Nature/RAW/Models/ground_pathTile.fbx"
            };
            for (int i = 0; i < grassFbx.Length; i++)
                AssignMat(grassFbx[i], grassMat);
            for (int i = 0; i < dirtFbx.Length; i++)
                AssignMat(dirtFbx[i], dirtMat);
            PaintSceneByName(grassMat, new[] { "grass_large", "plant_bush", "plant_bushLarge", "ResinBush", "grass_leafs" });
            PaintSceneByName(dirtMat, new[] { "rock_smallA", "rock_largeA" });
            var roadMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Art/Env/KenneyRoad.mat");
            if (roadMat != null)
                PaintSceneByName(roadMat, new[] { "road" });
        }

        static void PaintSceneByName(Material mat, string[] names)
        {
            if (mat == null || names == null)
                return;
            var rends = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < rends.Length; i++)
            {
                if (IsCharacterArt(rends[i].transform))
                    continue;
                if (!NameMatches(rends[i].gameObject.name, names) && !NameMatches(RootName(rends[i].transform), names))
                    continue;
                var slots = rends[i].sharedMaterials;
                for (int s = 0; s < slots.Length; s++)
                    slots[s] = mat;
                rends[i].sharedMaterials = slots;
            }
        }

        static bool NameMatches(string name, string[] names)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            for (int i = 0; i < names.Length; i++)
            {
                if (name == names[i])
                    return true;
            }
            return false;
        }

        static string RootName(Transform t)
        {
            while (t.parent != null)
                t = t.parent;
            return t.name;
        }

        static bool IsCharacterArt(Transform t)
        {
            while (t != null)
            {
                string n = t.name;
                if (n == "Player" || n == "Companion" || n == "Skeleton" || n == "Bandit" || n == "Trainer")
                    return true;
                t = t.parent;
            }
            return false;
        }

        static void AssertVillageVisuals()
        {
            var ground = GameObject.Find("Ground");
            if (ground == null || ground.GetComponent<Terrain>() == null)
                throw new InvalidOperationException("Ground는 Terrain이어야 한다. 단색 Plane/ground_grass 타일 금지.");
            var bad = new List<string>();
            var rends = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < rends.Length; i++)
            {
                if (!rends[i].enabled)
                    continue;
                if (IsCharacterArt(rends[i].transform))
                    continue;
                var mats = rends[i].sharedMaterials;
                for (int m = 0; m < mats.Length; m++)
                {
                    var mat = mats[m];
                    if (mat == null)
                    {
                        bad.Add(rends[i].gameObject.name + ":null-mat");
                        continue;
                    }
                    if (mat.name == "Water" || mat.name.IndexOf("Water", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;
                    if (mat.name.IndexOf("Default-Material", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        bad.Add(rends[i].gameObject.name + ":Default-Material");
                        continue;
                    }
                    Texture tex = mat.mainTexture;
                    if (tex == null && (mat.name == "grass" || mat.name == "dirt" || mat.name.StartsWith("grass", StringComparison.Ordinal) || mat.name.StartsWith("dirt", StringComparison.Ordinal)))
                    {
                        bad.Add(rends[i].gameObject.name + ":" + mat.name + "-untextured");
                        continue;
                    }
                    if (!mat.HasProperty("_Color"))
                        continue;
                    Color c = mat.color;
                    bool magenta = c.r > 0.8f && c.g < 0.2f && c.b > 0.8f;
                    bool cyan = tex == null && c.g > 0.7f && c.b > 0.7f && c.r < 0.45f;
                    if (magenta || cyan)
                        bad.Add(rends[i].gameObject.name + ":" + mat.name);
                }
            }
            if (bad.Count > 0)
                throw new InvalidOperationException("월드 비주얼 금지: " + string.Join("; ", bad));
        }

        static void PlaceLandmarks()
        {
            MoveNamed("Player", new Vector3(0f, 0f, 0f), Vector3.zero);
            MoveNamed("Companion", new Vector3(-2.4f, 0f, 1.8f), new Vector3(0f, 180f, 0f));
            MoveNamed("Skeleton", new Vector3(0.4f, 0f, 13.2f), new Vector3(0f, 180f, 0f));
            EnsureHuntMobs();
            MoveNamed("Bandit", new Vector3(-1.6f, 0f, 13.2f), new Vector3(0f, 180f, 0f));
            MoveNamed("Banker", new Vector3(-10.5f, 0f, 8.5f), Vector3.zero);
            MoveNamed("Forge", new Vector3(-6.8f, 0f, 3.4f), new Vector3(0f, 90f, 0f));
            EnsureCarpenterLandmark();
            MoveNamed("Vendor", new Vector3(-5.2f, 0f, 3.4f), new Vector3(0f, 90f, 0f));
            MoveNamed("stall", new Vector3(-5.2f, 0f, 3.4f), new Vector3(0f, 90f, 0f));
            var stall = GameObject.Find("stall");
            if (stall != null)
                stall.name = "Vendor";
            var vendorGo = GameObject.Find("Vendor");
            if (vendorGo != null)
            {
                var vs = vendorGo.GetComponent<VendorStation>() ?? vendorGo.AddComponent<VendorStation>();
                vs.DisplayName = "잡화";
                EnsureCollider(vendorGo);
            }
            MoveNamed("Healer", new Vector3(-3.6f, 0f, -3.6f), Vector3.zero);
            MoveNamed("fountain-round", new Vector3(-3.6f, 0f, -3.6f), Vector3.zero);
            var healer = GameObject.Find("fountain-round");
            if (healer != null)
                healer.name = "Healer";
            MoveNamed("IronVein", new Vector3(9.8f, 0f, -3.4f), Vector3.zero);
            MoveNamed("OakTree", new Vector3(12f, 0f, 12.5f), Vector3.zero);
            MoveNamed("ResinBush", new Vector3(4.6f, 0f, -3.6f), Vector3.zero);
            MoveNamed("plant_bushLarge", new Vector3(4.6f, 0f, -3.6f), Vector3.zero);
            var bush = GameObject.Find("plant_bushLarge");
            if (bush != null)
                bush.name = "ResinBush";
            MoveNamed("cart", new Vector3(-7.4f, 0f, 6.4f), Vector3.zero);
            MoveNamed("SpawnA", new Vector3(-1.2f, 0f, 1.4f), Vector3.zero);
            MoveNamed("SpawnB", new Vector3(1.2f, 0f, 1.4f), Vector3.zero);
            EnsureTrainerNpc();
            MoveNamed("Trainer", new Vector3(3.6f, 0f, 2.6f), new Vector3(0f, 180f, 0f));
            var healerGo = GameObject.Find("Healer");
            if (healerGo != null)
            {
                var hs = healerGo.GetComponent<HealerStation>() ?? healerGo.AddComponent<HealerStation>();
                hs.DisplayName = "치유사";
                EnsureCollider(healerGo);
            }
            var resinGo = GameObject.Find("ResinBush");
            if (resinGo != null)
            {
                var node = resinGo.GetComponent<ResourceNode>() ?? resinGo.AddComponent<ResourceNode>();
                node.ResourceId = SpellCast.Reagent;
                node.DisplayName = "수지 덤불";
                node.GatherSkill = SkillId.Magery;
                node.Remaining = 8;
                node.Capacity = 8;
                node.Difficulty = 8f;
                EnsureCollider(resinGo);
            }
            EnsureFishSpot();
        }

        static void EnsureTrainerNpc()
        {
            if (GameObject.Find("Trainer") != null)
                return;
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (ctrl == null)
                return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(MageFbx) == null)
                return;
            ConfigureHumanoid(MageFbx, true);
            var t = SpawnActor("Trainer", MageFbx, new Vector3(3.5f, 0f, 2.4f), 1.75f, ctrl, false, false, "훈련사", 50f);
            HideExtraGear(t);
            var ts = t.AddComponent<TrainerStation>();
            ts.DisplayName = "훈련사";
        }

        public static void EnsureHuntMobs()
        {
            var skel = GameObject.Find("Skeleton");
            if (skel != null)
                BindMob(skel, MobCatalog.Skeleton);

            if (GameObject.Find("Bandit") != null)
            {
                BindMob(GameObject.Find("Bandit"), MobCatalog.Bandit);
                return;
            }

            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (ctrl == null)
                return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(MageFbx) == null)
                return;
            ConfigureHumanoid(MageFbx, true);
            var bandit = SpawnActor(
                "Bandit",
                MageFbx,
                new Vector3(-1.6f, 0f, 13.2f),
                MobCatalog.HeightOf(MobCatalog.Bandit),
                ctrl,
                false,
                true,
                MobCatalog.DisplayNameOf(MobCatalog.Bandit),
                MobCatalog.MaxHpOf(MobCatalog.Bandit));
            BindMob(bandit, MobCatalog.Bandit);
            HideExtraGear(bandit);
        }

        static void BindMob(GameObject go, string mobId)
        {
            if (go == null)
                return;
            var body = go.GetComponent<WorldBody>();
            if (body == null)
                return;
            body.MobId = mobId;
            body.IsEnemy = true;
            body.ApplyMobCatalog();
        }

        static void EnsureCarpenterLandmark()
        {
            const string fbx = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/stall-bench.fbx";
            Vector3 pos = new Vector3(-6.8f, 0f, 5.2f);
            Vector3 euler = new Vector3(0f, 90f, 0f);
            var go = GameObject.Find("Carpenter");
            if (go == null)
                go = GameObject.Find("stall-bench");
            if (go == null)
                go = Place(fbx, pos, euler);
            else
                go.transform.SetPositionAndRotation(OnGround(pos), Quaternion.Euler(0f, euler.y, 0f));
            if (go == null)
                return;
            go.name = "Carpenter";
            var station = go.GetComponent<CraftStation>() ?? go.AddComponent<CraftStation>();
            station.RecipeId = "wooden_club";
            station.DisplayName = "목공소";
            EnsureCollider(go);
        }

        public static void EnsureFishSpot()
        {
            const string fbx = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/watermill.fbx";
            Vector3 pos = new Vector3(-11.5f, 0f, -8.5f);
            var go = GameObject.Find("FishingSpot");
            if (go == null)
                go = GameObject.Find("watermill");
            if (go == null)
                go = Place(fbx, pos, Vector3.zero);
            if (go == null)
                return;
            go.name = "FishingSpot";
            var node = go.GetComponent<ResourceNode>() ?? go.AddComponent<ResourceNode>();
            node.ResourceId = ItemCatalog.Fish;
            node.DisplayName = "물가";
            node.GatherSkill = SkillId.Fishing;
            node.Remaining = 12;
            node.Capacity = 12;
            node.Difficulty = 10f;
            node.RespawnSeconds = 8f;
            node.InteractRange = 2.8f;
            EnsureCollider(go);
        }

        static void MoveNamed(string name, Vector3 pos, Vector3 euler)
        {
            var go = GameObject.Find(name);
            if (go == null)
                return;
            var cc = go.GetComponent<CharacterController>();
            if (cc != null)
                cc.enabled = false;
            go.transform.SetPositionAndRotation(OnGround(pos), Quaternion.Euler(0f, euler.y, 0f));
            if (cc != null)
                cc.enabled = true;
        }

        static float GroundY(float x, float z)
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null)
            {
                var ground = GameObject.Find("Ground");
                if (ground != null)
                    terrain = ground.GetComponent<Terrain>();
            }
            if (terrain == null)
                return 0f;
            return terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
        }

        static Vector3 OnGround(Vector3 pos)
        {
            pos.y = GroundY(pos.x, pos.z) + pos.y;
            return pos;
        }

        static void SetupSky()
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Game/Art/Env"));
            string skyPath = "Assets/Game/Art/Env/SliceSky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            Shader shader = Shader.Find("Skybox/Procedural");
            if (sky == null && shader != null)
            {
                sky = new Material(shader);
                AssetDatabase.CreateAsset(sky, skyPath);
            }
            if (sky != null && shader != null)
            {
                sky.shader = shader;
                sky.SetFloat("_SunSize", 0.04f);
                sky.SetFloat("_AtmosphereThickness", 0.95f);
                sky.SetColor("_SkyTint", new Color(0.4f, 0.58f, 0.95f));
                sky.SetColor("_GroundColor", new Color(0.58f, 0.72f, 0.88f));
                sky.SetFloat("_Exposure", 1.15f);
                EditorUtility.SetDirty(sky);
                RenderSettings.skybox = sky;
            }
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.55f, 0.70f, 0.86f);
            RenderSettings.fogStartDistance = 42f;
            RenderSettings.fogEndDistance = 115f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.58f, 0.62f, 0.55f);
        }

        static void ImproveGround()
        {
            EnsureVillageTerrain();
        }

        static void PlaceVillageFloor(Transform parent)
        {
            const string Road = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/road.fbx";
            ConfigureProp(Road);
            var grassA = MakeNoiseMat("KenneyGrass", new Color(0.30f, 0.50f, 0.18f), new Color(0.22f, 0.40f, 0.12f));
            var dirtMat = MakeNoiseMat("KenneyDirt", new Color(0.52f, 0.38f, 0.24f), new Color(0.4f, 0.28f, 0.16f));
            AssignMat("Assets/_ThirdParty/Kenney/Nature/RAW/Models/ground_pathTile.fbx", dirtMat);
            AssignMat("Assets/_ThirdParty/Kenney/Nature/RAW/Models/grass_large.fbx", grassA);
            for (int x = -18; x < 18; x++)
            {
                for (int z = -18; z < 18; z++)
                {
                    if (!PlazaPath(x, z))
                        continue;
                    Decor(parent, Road, new Vector3(x + 0.5f, 0.02f, z + 0.5f), Vector3.zero);
                }
            }
        }

        static void TintRenderer(GameObject go, Material mat)
        {
            if (go == null || mat == null)
                return;
            var rends = go.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                var slots = rends[i].sharedMaterials;
                for (int s = 0; s < slots.Length; s++)
                    slots[s] = mat;
                rends[i].sharedMaterials = slots;
            }
        }

        static bool PlazaPath(int x, int z)
        {
            bool eastWest = (z == 0 || z == -1) && x >= -8 && x <= 9;
            bool northSouth = (x == 0 || x == -1) && z >= -7 && z <= 10;
            return eastWest || northSouth;
        }

        static void PlaceTownFill(Transform parent, string fence, string hedge, string lantern, string tree, string treeH, string bush, string rockS, string rockN)
        {
            const string Tuft = "Assets/_ThirdParty/Kenney/Nature/RAW/Models/grass_large.fbx";
            const string Leaf = "Assets/_ThirdParty/Kenney/Nature/RAW/Models/grass_leafs.fbx";
            const string Bench = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/stall-bench.fbx";
            const string Stool = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/stall-stool.fbx";
            const string StallR = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/stall-red.fbx";
            const string StallG = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/stall-green.fbx";
            const string CartH = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/cart-high.fbx";
            const string HedgeL = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/hedge-large.fbx";
            const string TreeR = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/tree-high-round.fbx";
            const string TreeC = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/tree-crooked.fbx";
            const string TreeHC = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/tree-high-crooked.fbx";
            const string RockW = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/rock-wide.fbx";
            const string Poles = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/poles.fbx";
            const string Mill = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/watermill.fbx";
            Decor(parent, lantern, new Vector3(-2.2f, 0f, -2.2f), Vector3.zero);
            Decor(parent, lantern, new Vector3(2.2f, 0f, -2.2f), Vector3.zero);
            Decor(parent, lantern, new Vector3(-2.2f, 0f, 2.2f), Vector3.zero);
            Decor(parent, lantern, new Vector3(2.2f, 0f, 2.2f), Vector3.zero);
            Decor(parent, lantern, new Vector3(-7.6f, 0f, 2.2f), Vector3.zero);
            Decor(parent, lantern, new Vector3(8.6f, 0f, -2.2f), Vector3.zero);
            Decor(parent, lantern, new Vector3(2.2f, 0f, 9.4f), Vector3.zero);
            Decor(parent, lantern, new Vector3(-2.2f, 0f, -6.4f), Vector3.zero);
            Decor(parent, Bench, new Vector3(-5.4f, 0f, -2.4f), new Vector3(0f, 90f, 0f));
            Decor(parent, Bench, new Vector3(-2.4f, 0f, -5.4f), Vector3.zero);
            Decor(parent, Stool, new Vector3(-4.6f, 0f, -3.6f), Vector3.zero);
            Decor(parent, Stool, new Vector3(-2.8f, 0f, -4.6f), Vector3.zero);
            Decor(parent, StallR, new Vector3(-5.2f, 0f, 4.8f), new Vector3(0f, 90f, 0f));
            Decor(parent, StallG, new Vector3(4.8f, 0f, -5.2f), new Vector3(0f, 270f, 0f));
            Decor(parent, CartH, new Vector3(-6.4f, 0f, 3.6f), Vector3.zero);
            Decor(parent, Mill, new Vector3(-11.5f, 0f, -8.5f), Vector3.zero);
            Decor(parent, HedgeL, new Vector3(-4.2f, 0f, 7.4f), Vector3.zero);
            Decor(parent, HedgeL, new Vector3(5.4f, 0f, 7.4f), Vector3.zero);
            Decor(parent, hedge, new Vector3(-7.4f, 0f, 1.6f), new Vector3(0f, 90f, 0f));
            Decor(parent, hedge, new Vector3(7.4f, 0f, -1.6f), new Vector3(0f, 90f, 0f));
            Decor(parent, Poles, new Vector3(-2.4f, 0f, 8.6f), Vector3.zero);
            Decor(parent, tree, new Vector3(-14.5f, 0f, -13.5f), Vector3.zero);
            Decor(parent, treeH, new Vector3(14.8f, 0f, -13.2f), new Vector3(0f, 20f, 0f));
            Decor(parent, TreeR, new Vector3(14.5f, 0f, 13.8f), new Vector3(0f, 90f, 0f));
            Decor(parent, TreeHC, new Vector3(-14.2f, 0f, 13.5f), Vector3.zero);
            Decor(parent, TreeC, new Vector3(-13.2f, 0f, 6.4f), new Vector3(0f, 30f, 0f));
            Decor(parent, treeH, new Vector3(13.4f, 0f, 5.2f), new Vector3(0f, 70f, 0f));
            Decor(parent, TreeR, new Vector3(-6.8f, 0f, -13.4f), Vector3.zero);
            Decor(parent, tree, new Vector3(6.6f, 0f, -13.6f), new Vector3(0f, 50f, 0f));
            Decor(parent, TreeC, new Vector3(13.8f, 0f, -6.4f), new Vector3(0f, 15f, 0f));
            Decor(parent, treeH, new Vector3(-13.6f, 0f, -7.8f), Vector3.zero);
            Decor(parent, bush, new Vector3(-9.2f, 0f, -7.1f), Vector3.zero);
            Decor(parent, bush, new Vector3(7.6f, 0f, -7.4f), new Vector3(0f, 80f, 0f));
            Decor(parent, bush, new Vector3(-2.4f, 0f, 10.6f), new Vector3(0f, 40f, 0f));
            Decor(parent, rockS, new Vector3(8.6f, 0f, 5.4f), Vector3.zero);
            Decor(parent, rockN, new Vector3(-11.6f, 0f, 5.2f), new Vector3(0f, 25f, 0f));
            Decor(parent, RockW, new Vector3(9.4f, 0f, -6.8f), new Vector3(0f, 15f, 0f));
            int[] spots = { -12, -9, -7, -4, 4, 6, 9, 11 };
            for (int i = 0; i < spots.Length; i++)
            {
                int x = spots[i];
                int z = spots[(i * 3 + 1) % spots.Length];
                if (PlazaPath(x, z) || PlazaPath(x - 1, z) || PlazaPath(x, z - 1))
                    continue;
                Decor(parent, Tuft, new Vector3(x + 0.3f, 0f, z - 0.2f), new Vector3(0f, i * 35f, 0f));
                if (i % 2 == 0)
                    Decor(parent, Leaf, new Vector3(x - 0.8f, 0f, z + 0.6f), new Vector3(0f, i * 50f, 0f));
            }
        }

        static void FramePlayCamera()
        {
            Camera cam = UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (cam == null)
                return;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.farClipPlane = 120f;
            cam.backgroundColor = new Color(0.55f, 0.7f, 0.85f);
            var player = GameObject.Find("Player");
            var qv = cam.GetComponent<QuarterViewCamera>() ?? cam.gameObject.AddComponent<QuarterViewCamera>();
            if (player != null)
                qv.SetFollow(player.transform);
            Quaternion rot = Quaternion.Euler(35f, 45f, 0f);
            Vector3 target = player != null ? player.transform.position : Vector3.zero;
            cam.transform.SetPositionAndRotation(target - rot * Vector3.forward * 16f, rot);
        }

        static void PlaceFenceRing(Transform parent, string fence, string gate)
        {
            float s = 15f;
            float step = 1f;
            Decor(parent, gate, new Vector3(0f, 0f, -s), Vector3.zero);
            Decor(parent, gate, new Vector3(0f, 0f, s), new Vector3(0f, 180f, 0f));
            Decor(parent, gate, new Vector3(-s, 0f, 0f), new Vector3(0f, 90f, 0f));
            Decor(parent, gate, new Vector3(s, 0f, 0f), new Vector3(0f, 270f, 0f));
            for (float x = -s; x <= s + 0.01f; x += step)
            {
                if (Mathf.Abs(x) < 1.6f)
                    continue;
                Decor(parent, fence, new Vector3(x, 0f, -s), Vector3.zero);
                Decor(parent, fence, new Vector3(x, 0f, s), Vector3.zero);
            }
            for (float z = -s + step; z <= s - step + 0.01f; z += step)
            {
                if (Mathf.Abs(z) < 1.6f)
                    continue;
                Decor(parent, fence, new Vector3(-s, 0f, z), new Vector3(0f, 90f, 0f));
                Decor(parent, fence, new Vector3(s, 0f, z), new Vector3(0f, 90f, 0f));
            }
        }

        static Material MakeNoiseMat(string name, Color a, Color b)
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Game/Art/Env"));
            string texPath = "Assets/Game/Art/Env/" + name + ".png";
            string matPath = "Assets/Game/Art/Env/" + name + ".mat";
            var tex = new Texture2D(128, 128, TextureFormat.RGB24, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Repeat;
            for (int y = 0; y < 128; y++)
            {
                for (int x = 0; x < 128; x++)
                {
                    int h = (x * 374761 + y * 668265 + x * y * 13) & 255;
                    int h2 = (x * 127 + y * 311) & 255;
                    float t = (h / 255f) * 0.65f + (h2 / 255f) * 0.35f;
                    tex.SetPixel(x, y, Color.Lerp(a, b, t));
                }
            }
            tex.Apply();
            File.WriteAllBytes(Path.Combine(Application.dataPath, "Game/Art/Env/" + name + ".png"), tex.EncodeToPNG());
            AssetDatabase.ImportAsset(texPath);
            var importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (importer != null)
            {
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.SaveAndReimport();
            }
            tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            Shader shader = Shader.Find("Standard");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.shader = shader;
            mat.color = Color.white;
            mat.mainTexture = tex;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void AssignMat(string fbx, Material mat)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (go == null || mat == null)
                return;
            var rends = go.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                var slots = rends[i].sharedMaterials;
                for (int s = 0; s < slots.Length; s++)
                    slots[s] = mat;
                rends[i].sharedMaterials = slots;
            }
        }

        static void Decor(Transform parent, string path, Vector3 pos, Vector3 euler)
        {
            var go = Place(path, pos, Quaternion.Euler(0f, euler.y, 0f));
            if (go == null)
                return;
            go.transform.SetParent(parent, true);
        }

        static void DecorLocal(Transform parent, string path, Vector3 localPos, Vector3 localEuler)
        {
            var go = Place(path, Vector3.zero, Quaternion.Euler(localEuler));
            if (go == null)
                return;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler);
        }

        static void PlaceHouse(Transform parent, Vector3 sw, float yaw, int depth, string wall, string door, string roof, string chimney, bool tall)
        {
            var root = new GameObject("House");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(OnGround(new Vector3(sw.x, 0f, sw.z)), Quaternion.Euler(0f, yaw, 0f));
            Transform hp = root.transform;
            int width = 2;
            int floors = tall ? 2 : 1;
            const string Overhang = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/overhang.fbx";
            const string Banner = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/banner-red.fbx";
            string gable = roof.IndexOf("roof-high", StringComparison.Ordinal) >= 0
                ? "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/roof-high-gable-end.fbx"
                : "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/roof-gable-end.fbx";
            for (int floor = 0; floor < floors; floor++)
            {
                float y = floor;
                for (int z = 0; z < depth; z++)
                {
                    DecorLocal(hp, wall, new Vector3(0.5f, y, z + 0.5f), Vector3.zero);
                    DecorLocal(hp, wall, new Vector3(width - 0.5f, y, z + 0.5f), new Vector3(0f, 180f, 0f));
                }
                for (int x = 0; x < width; x++)
                {
                    string south = floor == 0 && x == 1 ? door : wall;
                    DecorLocal(hp, south, new Vector3(x + 0.5f, y, 0.5f), new Vector3(0f, 90f, 0f));
                    DecorLocal(hp, wall, new Vector3(x + 0.5f, y, depth - 0.5f), new Vector3(0f, 270f, 0f));
                }
            }
            float roofY = floors;
            for (int z = 0; z < depth; z++)
            {
                DecorLocal(hp, roof, new Vector3(0.5f, roofY, z + 0.5f), Vector3.zero);
                DecorLocal(hp, roof, new Vector3(1.5f, roofY, z + 0.5f), new Vector3(0f, 180f, 0f));
            }
            DecorLocal(hp, gable, new Vector3(1f, roofY, 0.5f), new Vector3(0f, 90f, 0f));
            DecorLocal(hp, gable, new Vector3(1f, roofY, depth - 0.5f), new Vector3(0f, 270f, 0f));
            DecorLocal(hp, chimney, new Vector3(1.65f, roofY, depth - 0.55f), Vector3.zero);
            DecorLocal(hp, Overhang, new Vector3(1.5f, floors, 0.05f), new Vector3(0f, 90f, 0f));
            if (tall)
                DecorLocal(hp, Banner, new Vector3(1f, floors + 0.35f, 0.15f), new Vector3(0f, 180f, 0f));
        }

        static void ReplaceNamedWithModel(string name, string fbx, System.Action<GameObject> setup)
        {
            var old = GameObject.Find(name);
            Vector3 pos = old != null ? old.transform.position : Vector3.zero;
            Vector3 euler = old != null ? old.transform.eulerAngles : Vector3.zero;
            if (old != null)
                UnityEngine.Object.DestroyImmediate(old);
            var go = Place(fbx, pos, euler);
            if (go == null)
                throw new InvalidOperationException("모델 없음: " + fbx);
            go.name = name;
            EnsureCollider(go);
            setup(go);
        }

        static void EnsureCollider(GameObject go)
        {
            if (go.GetComponentInChildren<Collider>() != null)
                return;
            var filters = go.GetComponentsInChildren<MeshFilter>();
            if (filters.Length == 0)
            {
                go.AddComponent<BoxCollider>();
                return;
            }
            for (int i = 0; i < filters.Length; i++)
            {
                if (filters[i].sharedMesh == null)
                    continue;
                var mc = filters[i].gameObject.GetComponent<MeshCollider>();
                if (mc == null)
                    mc = filters[i].gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = filters[i].sharedMesh;
            }
        }

        public static void BatchFixAndReport()
        {
            FixCharacterAnimation();
            var sb = new System.Text.StringBuilder();
            var importer = AssetImporter.GetAtPath(KnightFbx) as ModelImporter;
            sb.AppendLine("type=" + importer.animationType);
            var humans = importer.humanDescription.human;
            for (int i = 0; i < humans.Length; i++)
                sb.AppendLine(humans[i].humanName + "->" + humans[i].boneName);
            AnimationClip[] clips = LoadClips(KnightFbx);
            AnimationClip idle = BestClip(clips, new[] { "idle" }, new[] { "attack", "walk", "run", "combat" });
            if (idle != null)
            {
                var binds = AnimationUtility.GetCurveBindings(idle);
                sb.AppendLine("idle=" + idle.name + " humanMotion=" + idle.isHumanMotion + " binds=" + binds.Length);
                int n = Mathf.Min(12, binds.Length);
                for (int i = 0; i < n; i++)
                    sb.AppendLine("  " + binds[i].path + " / " + binds[i].propertyName);
            }
            var player = GameObject.Find("Player");
            var anim = player != null ? player.GetComponentInChildren<Animator>() : null;
            if (anim != null && anim.avatar != null)
                sb.AppendLine("playerAvatar=" + anim.avatar.name + " human=" + anim.avatar.isHuman + " valid=" + anim.avatar.isValid);
            var companion = GameObject.Find("Companion");
            var canim = companion != null ? companion.GetComponentInChildren<Animator>() : null;
            if (canim != null && canim.avatar != null)
                sb.AppendLine("companionAvatar=" + canim.avatar.name + " human=" + canim.avatar.isHuman);
            if (anim != null && canim != null)
                sb.AppendLine("sharedCtrl=" + (anim.runtimeAnimatorController == canim.runtimeAnimatorController));
            string outPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../../builds/humanoid-report.txt"));
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            File.WriteAllText(outPath, sb.ToString());
            Debug.Log("[Ulon] " + sb.ToString());
        }

        static void StripAndAssign(GameObject root, RuntimeAnimatorController controller)
        {
            if (root == null)
                return;
            var rootAnim = root.GetComponent<Animator>();
            if (rootAnim != null)
                UnityEngine.Object.DestroyImmediate(rootAnim);
            Transform visual = root.transform.Find("Visual");
            GameObject host = visual != null ? visual.gameObject : root;
            var anim = host.GetComponent<Animator>();
            if (anim == null)
                anim = host.AddComponent<Animator>();
            anim.avatar = AvatarFor(root.name);
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var smrs = host.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < smrs.Length; i++)
                smrs[i].updateWhenOffscreen = true;
        }

        static Avatar AvatarFor(string rootName)
        {
            string fbx = KnightFbx;
            if (rootName == "Companion")
                fbx = BarbarianFbx;
            else if (rootName == "Skeleton")
                fbx = SkeletonFbx;
            else if (rootName == "Bandit" || rootName == "Trainer")
                fbx = MageFbx;
            Avatar human = null;
            Avatar any = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(fbx))
            {
                var av = o as Avatar;
                if (av == null)
                    continue;
                any = av;
                if (av.isHuman)
                    human = av;
            }
            return human != null ? human : any;
        }

        [MenuItem("Ulon/Build Visual Slice")]
        public static void Build()
        {
            if (!File.Exists(Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, KnightFbx)))
            {
                Debug.LogWarning("[Ulon] KayKit Knight.fbx 없음. 캡슐 부트스트랩으로 폴백.");
                CreateBootstrapScene.Create();
                return;
            }

            ConfigureHumanoid(KnightFbx, true);
            ConfigureHumanoid(BarbarianFbx, true);
            ConfigureHumanoid(MageFbx, true);
            ConfigureHumanoid(SkeletonFbx, true);
            ConfigureProp(SwordFbx);
            ConfigureProp(ShieldFbx);
            foreach (string prop in KenneyProps())
                ConfigureProp(prop);

            AnimationClip[] clips = LoadClips(KnightFbx);
            AnimationClip idle = BestClip(clips, new[] { "idle" }, new[] { "attack", "walk", "run", "combat" });
            AnimationClip walk = BestClip(clips, new[] { "walking", "walk" }, new[] { "attack", "strafe" });
            AnimationClip run = BestClip(clips, new[] { "running", "run" }, new[] { "attack" });
            AnimationClip attack = BestClip(clips, new[] { "1h_melee_attack", "attack_chop", "melee_attack", "attack" }, new[] { "idle" });
            if (idle == null)
                throw new InvalidOperationException("Knight FBX에서 Idle 클립을 찾지 못했습니다. 클립: " + ClipNames(clips));

            AnimatorController controller = BuildController(idle, walk, run, attack);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            SetupLighting();
            MakeGround();
            PlaceKenney();

            var player = SpawnActor("Player", KnightFbx, new Vector3(0f, 0f, 0f), 1.8f, controller, true, false, "나", 50f);
            AttachGear(player, SwordFbx, ShieldFbx);
            HideExtraGear(player);
            var companion = SpawnActor("Companion", BarbarianFbx, new Vector3(-2.2f, 0f, 1.4f), 1.85f, controller, false, false, "동료", 50f);
            HideExtraGear(companion);
            var skeleton = SpawnActor("Skeleton", SkeletonFbx, new Vector3(5.2f, 0f, 3.6f), MobCatalog.HeightOf(MobCatalog.Skeleton), controller, false, true, MobCatalog.DisplayNameOf(MobCatalog.Skeleton), MobCatalog.MaxHpOf(MobCatalog.Skeleton));
            BindMob(skeleton, MobCatalog.Skeleton);
            HideExtraGear(skeleton);
            var bandit = SpawnActor("Bandit", MageFbx, new Vector3(7.4f, 0f, 3.6f), MobCatalog.HeightOf(MobCatalog.Bandit), controller, false, true, MobCatalog.DisplayNameOf(MobCatalog.Bandit), MobCatalog.MaxHpOf(MobCatalog.Bandit));
            BindMob(bandit, MobCatalog.Bandit);
            HideExtraGear(bandit);

            var world = new GameObject("OfflineWorld");
            world.AddComponent<OfflineWorld>();
            world.AddComponent<SliceHud>();
            world.AddComponent<PersistDriver>();

            Camera cam = UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (cam != null)
            {
                var qv = cam.GetComponent<QuarterViewCamera>() ?? cam.gameObject.AddComponent<QuarterViewCamera>();
                qv.SetFollow(player.transform);
            }

            SetupSky();
            DressVillageInOpenScene();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("[Ulon] Visual slice 씬 저장. clips idle=" + idle.name
                      + " walk=" + (walk != null ? walk.name : "-")
                      + " run=" + (run != null ? run.name : "-")
                      + " attack=" + (attack != null ? attack.name : "-"));
        }

        static void SetupLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.52f);
            Light sun = UnityEngine.Object.FindAnyObjectByType<Light>();
            if (sun == null)
                return;
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.85f);
            sun.intensity = 1.18f;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            sun.shadows = LightShadows.Soft;
        }

        static void MakeGround()
        {
            EnsureVillageTerrain();
        }

        static void EnsureVillageTerrain()
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Game/Art/Env"));
            var grass = MakeNoiseMat("KenneyGrass", new Color(0.30f, 0.50f, 0.18f), new Color(0.22f, 0.40f, 0.12f));
            var tex = grass != null ? grass.mainTexture as Texture2D : null;
            if (tex == null)
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/Art/Env/KenneyGrass.png");
            const string DataPath = "Assets/Game/Art/Env/VillageTerrain.asset";
            const string LayerPath = "Assets/Game/Art/Env/VillageGrass.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(LayerPath);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, LayerPath);
            }
            layer.diffuseTexture = tex;
            layer.tileSize = new Vector2(12f, 12f);
            EditorUtility.SetDirty(layer);
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(DataPath);
            if (data == null)
            {
                data = new TerrainData();
                AssetDatabase.CreateAsset(data, DataPath);
            }
            int res = 257;
            data.heightmapResolution = res;
            data.size = new Vector3(180f, 5f, 180f);
            data.terrainLayers = new[] { layer };
            float[,] heights = new float[res, res];
            float half = 90f;
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    float wx = (x / (float)(res - 1)) * 180f - half;
                    float wz = (z / (float)(res - 1)) * 180f - half;
                    float dist = Mathf.Sqrt(wx * wx + wz * wz);
                    float n = Mathf.PerlinNoise(wx * 0.028f + 12.3f, wz * 0.028f + 4.7f) * 0.55f;
                    n += Mathf.PerlinNoise(wx * 0.07f + 30f, wz * 0.07f) * 0.28f;
                    n += Mathf.PerlinNoise(wx * 0.18f, wz * 0.18f + 18f) * 0.17f;
                    float flatten = 1f;
                    if (dist > 23f)
                        flatten = dist >= 48f ? 0f : 1f - (dist - 23f) / 25f;
                    heights[z, x] = n * (1f - flatten) * 0.55f;
                }
            }
            data.SetHeights(0, 0, heights);
            int ar = data.alphamapResolution;
            var alpha = new float[ar, ar, 1];
            for (int z = 0; z < ar; z++)
            {
                for (int x = 0; x < ar; x++)
                    alpha[z, x, 0] = 1f;
            }
            data.SetAlphamaps(0, 0, alpha);
            EditorUtility.SetDirty(data);
            var found = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = found.Length - 1; i >= 0; i--)
            {
                if (found[i] == null || found[i].name != "Ground")
                    continue;
                if (found[i].GetComponent<Terrain>() != null)
                    continue;
                UnityEngine.Object.DestroyImmediate(found[i].gameObject);
            }
            var go = GameObject.Find("Ground");
            if (go == null)
            {
                go = Terrain.CreateTerrainGameObject(data);
                go.name = "Ground";
            }
            var terrain = go.GetComponent<Terrain>();
            terrain.terrainData = data;
            go.transform.position = new Vector3(-90f, 0f, -90f);
            terrain.heightmapPixelError = 5f;
            terrain.basemapDistance = 90f;
            terrain.shadowCastingMode = ShadowCastingMode.On;
            var col = go.GetComponent<TerrainCollider>();
            if (col != null)
                col.terrainData = data;
        }

        static void PlaceKenney()
        {
            var millGo = Place("Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/windmill.fbx", new Vector3(-10.5f, 0f, 8.5f), Vector3.zero);
            if (millGo != null)
            {
                millGo.name = "Banker";
                var bank = millGo.AddComponent<BankStation>();
                bank.DisplayName = "은행";
            }
            Place("Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/stall.fbx", new Vector3(-5.2f, 0f, 3.4f), new Vector3(0f, 90f, 0f));
            Place("Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/fountain-round.fbx", new Vector3(-3.6f, 0f, -3.6f), Vector3.zero);
            Place("Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/tree-high.fbx", new Vector3(9f, 0f, 7f), Vector3.zero);
            Place("Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/tree.fbx", new Vector3(-7f, 0f, -6f), Vector3.zero);
            Place("Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/cart.fbx", new Vector3(-7.4f, 0f, 6.4f), Vector3.zero);
            Place("Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/rock-large.fbx", new Vector3(7.5f, 0f, -3f), Vector3.zero);
            Place("Assets/_ThirdParty/Kenney/Nature/RAW/Models/plant_bushLarge.fbx", new Vector3(4.6f, 0f, -3.6f), Vector3.zero);
            Place("Assets/_ThirdParty/Kenney/Nature/RAW/Models/plant_bush.fbx", new Vector3(-6.2f, 0f, -5.4f), Vector3.zero);
            Place("Assets/_ThirdParty/Kenney/Nature/RAW/Models/rock_largeA.fbx", new Vector3(8.5f, 0f, 2.4f), Vector3.zero);
            var veinGo = Place("Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/rock-large.fbx", new Vector3(9.8f, 0f, -3.4f), Vector3.zero);
            if (veinGo != null)
            {
                veinGo.name = "IronVein";
                var node = veinGo.AddComponent<ResourceNode>();
                node.ResourceId = "iron_ore";
                node.DisplayName = "철 광맥";
            }
            var forgeGo = Place("Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/stall.fbx", new Vector3(-6.8f, 0f, 3.4f), new Vector3(0f, 90f, 0f));
            if (forgeGo != null)
            {
                forgeGo.name = "Forge";
                var station = forgeGo.AddComponent<CraftStation>();
                station.RecipeId = "iron_sword";
                station.DisplayName = "대장간";
            }
            EnsureCarpenterLandmark();
        }

        static GameObject Place(string path, Vector3 pos, Vector3 euler)
        {
            return Place(path, pos, Quaternion.Euler(euler));
        }

        static GameObject Place(string path, Vector3 pos, Quaternion rot)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = Path.GetFileNameWithoutExtension(path);
            Vector3 e = rot.eulerAngles;
            rot = Quaternion.Euler(0f, e.y, 0f);
            go.transform.SetPositionAndRotation(OnGround(new Vector3(pos.x, 0f, pos.z)) + Vector3.up * pos.y, rot);
            return go;
        }

        static GameObject SpawnActor(string name, string fbx, Vector3 pos, float height, RuntimeAnimatorController controller, bool player, bool enemy, string display, float hp)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (model == null)
                throw new InvalidOperationException("모델 없음: " + fbx);

            var root = new GameObject(name);
            root.transform.position = OnGround(pos);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            FitHeight(visual.transform, root.transform, height);

            var cc = root.AddComponent<CharacterController>();
            cc.height = height;
            cc.radius = Mathf.Clamp(height * 0.18f, 0.22f, 0.4f);
            cc.center = new Vector3(0f, height * 0.5f, 0f);

            foreach (var extra in root.GetComponents<Animator>())
                UnityEngine.Object.DestroyImmediate(extra);
            var anim = visual.GetComponent<Animator>();
            if (anim == null)
                anim = visual.AddComponent<Animator>();
            if (anim.avatar == null)
            {
                var src = model.GetComponent<Animator>();
                if (src != null)
                    anim.avatar = src.avatar;
            }
            if (anim.avatar == null)
                anim.avatar = AvatarFor(name);
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var skins = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skins.Length; i++)
                skins[i].updateWhenOffscreen = true;
            root.AddComponent<CharacterAnim>();
            var sockets = root.AddComponent<EquipmentSockets>();
            sockets.Bind(visual.transform);

            var body = root.AddComponent<WorldBody>();
            body.IsEnemy = enemy;
            body.IsAvatar = player;
            body.DisplayName = display;
            body.MaxHp = hp;

            if (player)
            {
                root.AddComponent<ClickMotor>();
                root.AddComponent<LocalAvatar>();
                root.AddComponent<InventoryBag>();
            }

            return root;
        }

        static void AttachGear(GameObject actor, string swordPath, string shieldPath)
        {
            var sockets = actor.GetComponent<EquipmentSockets>();
            if (sockets == null)
                return;
            AttachIf(sockets, swordPath, sockets.RightHand);
            AttachIf(sockets, shieldPath, sockets.LeftHand);
        }

        static void HideExtraGear(GameObject actor)
        {
            string[] keep = { "1H_Sword", "Round_Shield", "sword_1handed", "shield_round" };
            foreach (var t in actor.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                bool gear = ContainsGearName(n);
                if (!gear)
                    continue;
                bool keepIt = false;
                for (int i = 0; i < keep.Length; i++)
                    if (n == keep[i])
                        keepIt = true;
                if (!keepIt)
                    t.gameObject.SetActive(false);
            }
        }

        static bool ContainsGearName(string n)
        {
            return n.IndexOf("Sword", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Shield", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Axe", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Staff", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Bow", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Quiver", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Dagger", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Wand", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Crossbow", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static void AttachIf(EquipmentSockets sockets, string path, Transform socket)
        {
            if (socket == null)
            {
                Debug.LogWarning("[Ulon] 소켓을 못 찾아 장비를 건너뜁니다: " + path);
                return;
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                return;
            var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            sockets.Attach(item, socket);
        }

        static void FitHeight(Transform visual, Transform root, float target)
        {
            visual.localPosition = Vector3.zero;
            visual.localScale = Vector3.one;
            var rends = visual.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0)
                return;
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++)
                b.Encapsulate(rends[i].bounds);
            if (b.size.y < 0.01f)
                return;
            visual.localScale = Vector3.one * (target / b.size.y);
            b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++)
                b.Encapsulate(rends[i].bounds);
            visual.localPosition = new Vector3(0f, visual.localPosition.y - (b.min.y - root.position.y), 0f);
        }

        static AnimatorController BuildController(AnimationClip idle, AnimationClip walk, AnimationClip run, AnimationClip attack)
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Game/Art/Characters"));
            if (File.Exists(ControllerPath))
                AssetDatabase.DeleteAsset(ControllerPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var parameters = controller.parameters;
            bool hasSpeed = false, hasAttack = false;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name == "Speed") hasSpeed = true;
                if (parameters[i].name == "Attack") hasAttack = true;
            }
            if (!hasSpeed)
                controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            if (!hasAttack)
                controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

            var sm = controller.layers[0].stateMachine;
            var locomo = sm.AddState("Locomotion");
            var tree = new BlendTree
            {
                name = "Move",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            locomo.motion = tree;
            tree.AddChild(idle, 0f);
            if (walk != null)
                tree.AddChild(walk, 2.3f);
            if (run != null)
                tree.AddChild(run, 4.4f);
            tree.minThreshold = 0f;
            tree.maxThreshold = run != null ? 4.4f : (walk != null ? 2.3f : 1f);
            sm.defaultState = locomo;

            if (attack != null)
            {
                var atk = sm.AddState("Attack");
                atk.motion = attack;
                var toAtk = locomo.AddTransition(atk);
                toAtk.hasExitTime = false;
                toAtk.duration = 0.05f;
                toAtk.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
                var back = atk.AddTransition(locomo);
                back.hasExitTime = true;
                back.exitTime = 0.85f;
                back.duration = 0.1f;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        static void ConfigureHumanoid(string path, bool importClips)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
                throw new InvalidOperationException("importer 없음: " + path);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.optimizeGameObjects = false;
            importer.importAnimation = importClips;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            if (importClips)
                LoopLocomotionClips(importer);
            importer.SaveAndReimport();

            importer = AssetImporter.GetAtPath(path) as ModelImporter;
            var desc = importer.humanDescription;
            desc.human = BuildHuman(desc.skeleton);
            desc.hasTranslationDoF = false;
            importer.humanDescription = desc;
            importer.SaveAndReimport();
        }

        static bool HasSkel(SkeletonBone[] skel, string name)
        {
            for (int i = 0; i < skel.Length; i++)
                if (skel[i].name == name)
                    return true;
            return false;
        }

        static HumanBone Human(string humanName, string boneName)
        {
            return new HumanBone
            {
                humanName = humanName,
                boneName = boneName,
                limit = new HumanLimit { useDefaultValues = true }
            };
        }

        static HumanBone[] BuildHuman(SkeletonBone[] skel)
        {
            var list = new List<HumanBone>();
            void Add(string humanName, params string[] bones)
            {
                for (int i = 0; i < bones.Length; i++)
                {
                    if (!HasSkel(skel, bones[i]))
                        continue;
                    list.Add(Human(humanName, bones[i]));
                    return;
                }
            }

            Add("Hips", "hips");
            Add("Spine", "spine");
            Add("Chest", "chest");
            Add("Head", "head");
            Add("LeftUpperLeg", "upperleg.l");
            Add("RightUpperLeg", "upperleg.r");
            Add("LeftLowerLeg", "lowerleg.l");
            Add("RightLowerLeg", "lowerleg.r");
            Add("LeftFoot", "foot.l");
            Add("RightFoot", "foot.r");
            Add("LeftToes", "toes.l");
            Add("RightToes", "toes.r");
            Add("LeftUpperArm", "upperarm.l");
            Add("RightUpperArm", "upperarm.r");
            Add("LeftLowerArm", "lowerarm.l");
            Add("RightLowerArm", "lowerarm.r");
            Add("LeftHand", "wrist.l", "hand.l");
            Add("RightHand", "wrist.r", "hand.r");
            return list.ToArray();
        }

        static void LoopLocomotionClips(ModelImporter importer)
        {
            var clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0)
                return;
            for (int i = 0; i < clips.Length; i++)
            {
                string n = clips[i].name.ToLowerInvariant();
                bool loop = n.Contains("idle") || n.Contains("walk") || n.Contains("run")
                    || n.Contains("aiming") || n.Contains("shooting");
                clips[i].loopTime = loop;
                clips[i].loopPose = loop;
            }
            importer.clipAnimations = clips;
        }

        static void ConfigureProp(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
                return;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
        }

        static AnimationClip[] LoadClips(string path)
        {
            var list = new List<AnimationClip>();
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                    list.Add(clip);
            }
            return list.ToArray();
        }

        static AnimationClip BestClip(AnimationClip[] clips, string[] keys, string[] exclude)
        {
            AnimationClip best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < clips.Length; i++)
            {
                string n = clips[i].name.ToLowerInvariant();
                bool banned = false;
                for (int e = 0; e < exclude.Length; e++)
                    if (n.Contains(exclude[e])) { banned = true; break; }
                if (banned)
                    continue;
                for (int k = 0; k < keys.Length; k++)
                {
                    if (!n.Contains(keys[k]))
                        continue;
                    int score = 100 - n.Length - k * 10;
                    if (n == keys[k]) score += 50;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = clips[i];
                    }
                }
            }
            return best;
        }

        static string ClipNames(AnimationClip[] clips)
        {
            var names = new string[Math.Min(clips.Length, 20)];
            for (int i = 0; i < names.Length; i++)
                names[i] = clips[i].name;
            return string.Join(", ", names);
        }

        static string[] KenneyProps()
        {
            return new[]
            {
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/windmill.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/stall.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/fountain-round.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/tree-high.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/tree.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/cart.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/rock-large.fbx",
                "Assets/_ThirdParty/Kenney/Nature/RAW/Models/plant_bushLarge.fbx",
                "Assets/_ThirdParty/Kenney/Nature/RAW/Models/plant_bush.fbx",
                "Assets/_ThirdParty/Kenney/Nature/RAW/Models/rock_largeA.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/fence.fbx",
                "Assets/_ThirdParty/Kenney/Nature/RAW/Models/grass.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/rock-small.fbx",
                "Assets/_ThirdParty/Kenney/Nature/RAW/Models/rock_smallA.fbx"
            };
        }
    }
}
