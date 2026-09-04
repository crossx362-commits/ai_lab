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
        const string RogueFbx = "Assets/_ThirdParty/KayKit/Adventurers/RAW/Characters/Rogue.fbx";
        const string SkeletonFbx = "Assets/_ThirdParty/KayKit/Skeletons/RAW/Characters/Skeleton_Warrior.fbx";
        const string SkeletonMageFbx = "Assets/_ThirdParty/KayKit/Skeletons/RAW/Characters/Skeleton_Mage.fbx";
        const string SkeletonMinionFbx = "Assets/_ThirdParty/KayKit/Skeletons/RAW/Characters/Skeleton_Minion.fbx";
        const string SkeletonRogueFbx = "Assets/_ThirdParty/KayKit/Skeletons/RAW/Characters/Skeleton_Rogue.fbx";
        const string SwordFbx = "Assets/_ThirdParty/KayKit/Adventurers/RAW/Weapons/sword_1handed.fbx";
        const string ShieldFbx = "Assets/_ThirdParty/KayKit/Adventurers/RAW/Weapons/shield_round.fbx";

        [MenuItem("Ulon/Fix Character Animation")]
        public static void FixCharacterAnimation()
        {
            EditorSceneManager.OpenScene(ScenePath);
            ConfigureHumanoid(KnightFbx, true);
            ConfigureHumanoid(BarbarianFbx, true);
            ConfigureHumanoid(MageFbx, true);
            ConfigureHumanoid(RogueFbx, true);
            ConfigureHumanoid(SkeletonFbx, true);
            ConfigureHumanoid(SkeletonMageFbx, true);
            ConfigureHumanoid(SkeletonMinionFbx, true);
            ConfigureHumanoid(SkeletonRogueFbx, true);
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
            StripAndAssign(GameObject.Find("Raider"), controller);
            StripAndAssign(GameObject.Find("Rogue"), controller);
            StripAndAssign(GameObject.Find("Knight"), controller);
            StripAndAssign(GameObject.Find("Acolyte"), controller);
            StripAndAssign(GameObject.Find("Minion"), controller);
            StripAndAssign(GameObject.Find("SkelRogue"), controller);
            StripAndAssign(GameObject.Find(Dungeon1.BossObject), controller);
            StripAndAssign(GameObject.Find(Dungeon2.BossObject), controller);
            StripAndAssign(GameObject.Find(FieldBoss.Object), controller);
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
            EnsureEastField();
            EnsureSouthField();
            EnsureNorthField();
            EnsureFieldBoss();
            EnsureCampfire();
            EnsureMortar();
            EnsureLockedCrate();
            EnsureHousingPlot();
            EnsureTameCritter();
            EnsureTameBoar();
            EnsureMoongate();
            EnsureStable();
            Debug.Log("[Ulon] Slice props: vein/forge meshes + OakTree lumber + EastField + SouthField + NorthField");
        }

        [MenuItem("Ulon/Dress Village")]
        public static void DressVillage()
        {
            BuildEnvPrefabs();
            BuildKayKitPrefabs();
            EditorSceneManager.OpenScene(ScenePath);
            SetupLighting();
            SetupSky();
            ImproveGround();
            DressVillageInOpenScene();
            EnsureHousingPlot();
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[Ulon] Village: road-aligned houses, plaza off cross, hunt north, env prefabs");
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
            SnapBuildingsToGround();
            EnsureNamedVisualsAndController();
            RelinkKayKitInScene();
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
            PlacePaddockFences(parent, Fence, Gate);
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
            {
                AssignMat(grassFbx[i], grassMat);
                AssignMat(EnvPrefabPath(grassFbx[i]), grassMat);
            }
            for (int i = 0; i < dirtFbx.Length; i++)
            {
                AssignMat(dirtFbx[i], dirtMat);
                AssignMat(EnvPrefabPath(dirtFbx[i]), dirtMat);
            }
            PaintSceneByName(grassMat, new[] { "grass", "grass_large", "plant_bush", "plant_bushLarge", "ResinBush", "FieldFlax", "grass_leafs", "ground_grass" });
            PaintSceneByName(dirtMat, new[] { "rock_smallA", "rock_largeA", "ground_pathTile" });
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
                bool hit = AncestorNameMatches(rends[i].transform, names);
                if (!hit)
                {
                    var cur = rends[i].sharedMaterials;
                    for (int s = 0; s < cur.Length && !hit; s++)
                    {
                        if (cur[s] != null && NameMatches(cur[s].name, names))
                            hit = true;
                    }
                }
                if (!hit)
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
                if (string.Equals(name, names[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        static bool AncestorNameMatches(Transform t, string[] names)
        {
            while (t != null)
            {
                if (NameMatches(t.name, names))
                    return true;
                t = t.parent;
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
                if (n == "Player" || n == "Companion" || n == "Skeleton" || n == "Bandit" || n == "Raider" || n == "Rogue" || n == "Knight" || n == "Acolyte" || n == "Minion" || n == "SkelRogue" || n == "Trainer")
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
            AssertNoFenceRing();
            AssertHousesOnGround();
        }

        static void PlaceLandmarks()
        {
            MoveNamed("Player", new Vector3(0f, 0f, 0f), Vector3.zero);
            MoveNamed("Companion", new Vector3(-2.4f, 0f, 1.8f), new Vector3(0f, 180f, 0f));
            MoveNamed("Skeleton", new Vector3(0.4f, 0f, 13.2f), new Vector3(0f, 180f, 0f));
            EnsureHuntMobs();
            MoveNamed("Bandit", new Vector3(-1.6f, 0f, 13.2f), new Vector3(0f, 180f, 0f));
            MoveNamed("Raider", new Vector3(2.4f, 0f, 13.2f), new Vector3(0f, 180f, 0f));
            MoveNamed("Rogue", new Vector3(-3.8f, 0f, 13.2f), new Vector3(0f, 180f, 0f));
            MoveNamed("Knight", new Vector3(4.4f, 0f, 13.2f), new Vector3(0f, 180f, 0f));
            MoveNamed("Acolyte", new Vector3(6.4f, 0f, 13.2f), new Vector3(0f, 180f, 0f));
            MoveNamed("Minion", new Vector3(8.4f, 0f, 13.2f), new Vector3(0f, 180f, 0f));
            MoveNamed("SkelRogue", new Vector3(10.4f, 0f, 13.2f), new Vector3(0f, 180f, 0f));
            EnsureFieldBoss();
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
            EnsureCampfire();
            EnsureMortar();
            EnsureLockedCrate();
            EnsureHousingPlot();
            EnsureTameCritter();
            EnsureTameBoar();
            EnsureMoongate();
            EnsureStable();
            EnsureEastField();
            EnsureSouthField();
            EnsureNorthField();
            EnsureDungeon1();
            EnsureDungeon2();
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

            EnsureHuntMob("Bandit", MobCatalog.Bandit, MageFbx, new Vector3(-1.6f, 0f, 13.2f));
            EnsureHuntMob("Raider", MobCatalog.Raider, BarbarianFbx, new Vector3(2.4f, 0f, 13.2f));
            EnsureHuntMob("Rogue", MobCatalog.Rogue, RogueFbx, new Vector3(-3.8f, 0f, 13.2f));
            EnsureHuntMob("Knight", MobCatalog.Knight, KnightFbx, new Vector3(4.4f, 0f, 13.2f));
            EnsureHuntMob("Acolyte", MobCatalog.Acolyte, SkeletonMageFbx, new Vector3(6.4f, 0f, 13.2f));
            EnsureHuntMob("Minion", MobCatalog.Minion, SkeletonMinionFbx, new Vector3(8.4f, 0f, 13.2f));
            EnsureHuntMob("SkelRogue", MobCatalog.SkelRogue, SkeletonRogueFbx, new Vector3(10.4f, 0f, 13.2f));
            MoveNamed("SkelRogue", new Vector3(10.4f, 0f, 13.2f), new Vector3(0f, 180f, 0f));
        }

        static void EnsureHuntMob(string goName, string mobId, string fbx, Vector3 pos)
        {
            var go = GameObject.Find(goName);
            if (go != null)
            {
                BindMob(go, mobId);
                return;
            }

            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (ctrl == null)
                return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(fbx) == null)
                return;
            ConfigureHumanoid(fbx, true);
            var spawned = SpawnActor(
                goName,
                fbx,
                pos,
                MobCatalog.HeightOf(mobId),
                ctrl,
                false,
                true,
                MobCatalog.DisplayNameOf(mobId),
                MobCatalog.MaxHpOf(mobId));
            BindMob(spawned, mobId);
            HideExtraGear(spawned);
            spawned.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
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


        public static void EnsureEastField()
        {
            const string TreeH = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/tree-high.fbx";
            const string RockA = "Assets/_ThirdParty/Kenney/Nature/RAW/Models/rock_largeA.fbx";
            const string RockS = "Assets/_ThirdParty/Kenney/Nature/RAW/Models/rock_smallA.fbx";
            const string Bush = "Assets/_ThirdParty/Kenney/Nature/RAW/Models/plant_bush.fbx";
            const string Tuft = "Assets/_ThirdParty/Kenney/Nature/RAW/Models/grass_large.fbx";
            var old = GameObject.Find("EastField");
            if (old != null)
                UnityEngine.Object.DestroyImmediate(old);
            var root = new GameObject("EastField");
            Transform parent = root.transform;
            var oak = Place(TreeH, new Vector3(18.2f, 0f, 2.4f), Vector3.zero);
            if (oak == null)
                throw new InvalidOperationException("동쪽 필드 나무 모델 없음");
            oak.name = "FieldOak";
            oak.transform.SetParent(parent, true);
            var node = oak.GetComponent<ResourceNode>() ?? oak.AddComponent<ResourceNode>();
            node.ResourceId = "wood";
            node.DisplayName = "들판 참나무";
            node.GatherSkill = SkillId.Lumberjacking;
            node.Remaining = 12;
            node.Capacity = 12;
            node.RespawnSeconds = 8f;
            node.Difficulty = 10f;
            EnsureCollider(oak);
            Decor(parent, RockA, new Vector3(19.6f, 0f, 0.6f), new Vector3(0f, 20f, 0f));
            Decor(parent, RockS, new Vector3(17.1f, 0f, 4.2f), new Vector3(0f, 40f, 0f));
            Decor(parent, Bush, new Vector3(19.8f, 0f, 3.8f), new Vector3(0f, 70f, 0f));
            Decor(parent, Tuft, new Vector3(17.4f, 0f, 1.1f), new Vector3(0f, 15f, 0f));
            Decor(parent, Tuft, new Vector3(20.2f, 0f, 2.2f), new Vector3(0f, 95f, 0f));
        }

        public static void EnsureSouthField()
        {
            const string Bush = "Assets/_ThirdParty/Kenney/Nature/RAW/Models/plant_bushLarge.fbx";
            const string BushS = "Assets/_ThirdParty/Kenney/Nature/RAW/Models/plant_bush.fbx";
            const string RockA = "Assets/_ThirdParty/Kenney/Nature/RAW/Models/rock_largeA.fbx";
            const string RockS = "Assets/_ThirdParty/Kenney/Nature/RAW/Models/rock_smallA.fbx";
            const string Tuft = "Assets/_ThirdParty/Kenney/Nature/RAW/Models/grass_large.fbx";
            string[] models = { Bush, BushS, RockA, RockS, Tuft };
            for (int i = 0; i < models.Length; i++)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(models[i]) == null)
                    ConfigureProp(models[i]);
            }
            var old = GameObject.Find("SouthField");
            if (old != null)
                UnityEngine.Object.DestroyImmediate(old);
            var stray = GameObject.Find("FieldFlax");
            if (stray != null)
                UnityEngine.Object.DestroyImmediate(stray);
            var root = new GameObject("SouthField");
            root.transform.position = new Vector3(3.4f, 0f, -19.6f);
            Transform parent = root.transform;
            var flax = Place(Bush, new Vector3(3.4f, 0f, -19.6f), Vector3.zero);
            if (flax == null)
                throw new InvalidOperationException("남쪽 필드 덤불 모델 없음");
            flax.name = "FieldFlax";
            flax.transform.SetParent(parent, true);
            var node = flax.GetComponent<ResourceNode>() ?? flax.AddComponent<ResourceNode>();
            node.ResourceId = ItemCatalog.Cloth;
            node.DisplayName = "들판 아마";
            node.GatherSkill = SkillId.Tailoring;
            node.Remaining = 10;
            node.Capacity = 10;
            node.RespawnSeconds = 8f;
            node.Difficulty = 10f;
            EnsureCollider(flax);
            Decor(parent, RockA, new Vector3(5.1f, 0f, -18.4f), new Vector3(0f, 30f, 0f));
            Decor(parent, RockS, new Vector3(1.8f, 0f, -20.6f), new Vector3(0f, 80f, 0f));
            Decor(parent, BushS, new Vector3(4.8f, 0f, -21.0f), new Vector3(0f, 50f, 0f));
            Decor(parent, Tuft, new Vector3(2.2f, 0f, -18.7f), new Vector3(0f, 15f, 0f));
            Decor(parent, Tuft, new Vector3(4.6f, 0f, -19.2f), new Vector3(0f, 110f, 0f));
        }


        public static void EnsureNorthField()
        {
            const string RockA = "Assets/_ThirdParty/Kenney/Nature/RAW/Models/rock_largeA.fbx";
            const string RockS = "Assets/_ThirdParty/Kenney/Nature/RAW/Models/rock_smallA.fbx";
            const string Bush = "Assets/_ThirdParty/Kenney/Nature/RAW/Models/plant_bush.fbx";
            const string Tuft = "Assets/_ThirdParty/Kenney/Nature/RAW/Models/grass_large.fbx";
            string[] models = { RockA, RockS, Bush, Tuft };
            for (int i = 0; i < models.Length; i++)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(models[i]) == null)
                    ConfigureProp(models[i]);
            }
            var old = GameObject.Find("NorthField");
            if (old != null)
                UnityEngine.Object.DestroyImmediate(old);
            var stray = GameObject.Find("FieldOre");
            if (stray != null)
                UnityEngine.Object.DestroyImmediate(stray);
            var root = new GameObject("NorthField");
            root.transform.position = new Vector3(-12.2f, 0f, 20.4f);
            Transform parent = root.transform;
            var ore = Place(RockA, new Vector3(-12.2f, 0f, 20.4f), Vector3.zero);
            if (ore == null)
                throw new InvalidOperationException("북쪽 필드 바위 모델 없음");
            ore.name = "FieldOre";
            ore.transform.SetParent(parent, true);
            var node = ore.GetComponent<ResourceNode>() ?? ore.AddComponent<ResourceNode>();
            node.ResourceId = "iron_ore";
            node.DisplayName = "들판 광맥";
            node.GatherSkill = SkillId.Mining;
            node.Remaining = 10;
            node.Capacity = 10;
            node.RespawnSeconds = 8f;
            node.Difficulty = 10f;
            EnsureCollider(ore);
            Decor(parent, RockS, new Vector3(-13.6f, 0f, 19.2f), new Vector3(0f, 40f, 0f));
            Decor(parent, RockS, new Vector3(-10.8f, 0f, 21.6f), new Vector3(0f, 80f, 0f));
            Decor(parent, Bush, new Vector3(-11.0f, 0f, 19.0f), new Vector3(0f, 55f, 0f));
            Decor(parent, Tuft, new Vector3(-13.0f, 0f, 21.2f), new Vector3(0f, 15f, 0f));
            Decor(parent, Tuft, new Vector3(-10.6f, 0f, 20.1f), new Vector3(0f, 110f, 0f));
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }

        public static void EnsureDungeon1()
        {
            const string RockW = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/rock-wide.fbx";
            const string RockL = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/rock-large.fbx";
            const string RockS = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/rock-small.fbx";
            const string Arch = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/wall-arch.fbx";
            const string Lantern = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/lantern.fbx";
            const string Planks = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/planks.fbx";
            const string RockN = "Assets/_ThirdParty/Kenney/Nature/RAW/Models/rock_largeA.fbx";
            string[] models = { RockW, RockL, RockS, Arch, Lantern, Planks, RockN };
            for (int i = 0; i < models.Length; i++)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(models[i]) == null)
                    ConfigureProp(models[i]);
            }

            var old = GameObject.Find(Dungeon1.RootObject);
            if (old != null)
                UnityEngine.Object.DestroyImmediate(old);
            var stray = GameObject.Find(Dungeon1.MobObject);
            if (stray != null)
                UnityEngine.Object.DestroyImmediate(stray);
            var strayBoss = GameObject.Find(Dungeon1.BossObject);
            if (strayBoss != null)
                UnityEngine.Object.DestroyImmediate(strayBoss);

            var root = new GameObject(Dungeon1.RootObject);
            Transform parent = root.transform;

            var entrance = Place(Arch, new Vector3(Dungeon1.EntranceX, 0f, Dungeon1.EntranceZ), new Vector3(0f, 90f, 0f));
            if (entrance == null)
                entrance = Place(RockW, new Vector3(Dungeon1.EntranceX, 0f, Dungeon1.EntranceZ), Vector3.zero);
            if (entrance == null)
                throw new InvalidOperationException("던전 1 입구 모델 없음");
            entrance.name = Dungeon1.EntranceObject;
            entrance.transform.SetParent(parent, true);
            var eg = entrance.GetComponent<DungeonGate>() ?? entrance.AddComponent<DungeonGate>();
            eg.DungeonId = Dungeon1.Id;
            eg.IsExit = false;
            eg.DisplayName = "던전 입구";
            EnsureCollider(entrance);
            Decor(parent, RockW, new Vector3(Dungeon1.EntranceX - 1.4f, 0f, Dungeon1.EntranceZ + 0.6f), new Vector3(0f, 20f, 0f));
            Decor(parent, RockL, new Vector3(Dungeon1.EntranceX - 1.1f, 0f, Dungeon1.EntranceZ - 1.1f), new Vector3(0f, 50f, 0f));
            Decor(parent, RockN, new Vector3(Dungeon1.EntranceX + 0.8f, 0f, Dungeon1.EntranceZ + 1.3f), new Vector3(0f, 10f, 0f));

            var interior = new GameObject(Dungeon1.InteriorObject);
            interior.transform.SetParent(parent, false);
            interior.transform.position = OnGround(new Vector3(Dungeon1.InteriorX, 0f, Dungeon1.InteriorZ));
            Transform room = interior.transform;
            Decor(room, Planks, new Vector3(Dungeon1.InteriorX, 0.02f, Dungeon1.InteriorZ), Vector3.zero);
            Decor(room, RockW, new Vector3(Dungeon1.InteriorX + 3.2f, 0f, Dungeon1.InteriorZ), new Vector3(0f, 90f, 0f));
            Decor(room, RockW, new Vector3(Dungeon1.InteriorX - 3.2f, 0f, Dungeon1.InteriorZ), new Vector3(0f, 90f, 0f));
            Decor(room, RockW, new Vector3(Dungeon1.InteriorX, 0f, Dungeon1.InteriorZ + 3.2f), Vector3.zero);
            Decor(room, RockL, new Vector3(Dungeon1.InteriorX + 2.2f, 0f, Dungeon1.InteriorZ + 2.2f), new Vector3(0f, 30f, 0f));
            Decor(room, RockS, new Vector3(Dungeon1.InteriorX - 1.6f, 0f, Dungeon1.InteriorZ - 1.8f), new Vector3(0f, 70f, 0f));
            Decor(room, Lantern, new Vector3(Dungeon1.InteriorX - 2.4f, 0f, Dungeon1.InteriorZ + 2.1f), Vector3.zero);

            var exitGo = Place(Arch, new Vector3(Dungeon1.ExitX, 0f, Dungeon1.ExitZ), new Vector3(0f, 225f, 0f));
            if (exitGo == null)
                exitGo = Place(RockS, new Vector3(Dungeon1.ExitX, 0f, Dungeon1.ExitZ), Vector3.zero);
            if (exitGo == null)
                throw new InvalidOperationException("던전 1 출구 모델 없음");
            exitGo.name = Dungeon1.ExitObject;
            exitGo.transform.SetParent(parent, true);
            var xg = exitGo.GetComponent<DungeonGate>() ?? exitGo.AddComponent<DungeonGate>();
            xg.DungeonId = Dungeon1.Id;
            xg.IsExit = true;
            xg.DisplayName = "던전 출구";
            EnsureCollider(exitGo);

            EnsureDungeonMob(parent);
            EnsureDungeonBoss(parent);
        }

        public static void EnsureDungeon2()
        {
            const string RockW = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/rock-wide.fbx";
            const string RockL = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/rock-large.fbx";
            const string RockS = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/rock-small.fbx";
            const string Arch = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/wall-arch.fbx";
            const string Lantern = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/lantern.fbx";
            const string Planks = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/planks.fbx";
            const string RockN = "Assets/_ThirdParty/Kenney/Nature/RAW/Models/rock_largeA.fbx";
            string[] models = { RockW, RockL, RockS, Arch, Lantern, Planks, RockN };
            for (int i = 0; i < models.Length; i++)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(models[i]) == null)
                    ConfigureProp(models[i]);
            }

            var old = GameObject.Find(Dungeon2.RootObject);
            if (old != null)
                UnityEngine.Object.DestroyImmediate(old);
            var stray = GameObject.Find(Dungeon2.MobObject);
            if (stray != null)
                UnityEngine.Object.DestroyImmediate(stray);
            var strayBoss = GameObject.Find(Dungeon2.BossObject);
            if (strayBoss != null)
                UnityEngine.Object.DestroyImmediate(strayBoss);

            var root = new GameObject(Dungeon2.RootObject);
            Transform parent = root.transform;

            var entrance = Place(Arch, new Vector3(Dungeon2.EntranceX, 0f, Dungeon2.EntranceZ), new Vector3(0f, -90f, 0f));
            if (entrance == null)
                entrance = Place(RockW, new Vector3(Dungeon2.EntranceX, 0f, Dungeon2.EntranceZ), Vector3.zero);
            if (entrance == null)
                throw new InvalidOperationException("던전 2 입구 모델 없음");
            entrance.name = Dungeon2.EntranceObject;
            entrance.transform.SetParent(parent, true);
            var eg = entrance.GetComponent<DungeonGate>() ?? entrance.AddComponent<DungeonGate>();
            eg.DungeonId = Dungeon2.Id;
            eg.IsExit = false;
            eg.DisplayName = "던전 2 입구";
            EnsureCollider(entrance);
            Decor(parent, RockW, new Vector3(Dungeon2.EntranceX + 1.4f, 0f, Dungeon2.EntranceZ + 0.6f), new Vector3(0f, 20f, 0f));
            Decor(parent, RockL, new Vector3(Dungeon2.EntranceX + 1.1f, 0f, Dungeon2.EntranceZ - 1.1f), new Vector3(0f, 50f, 0f));
            Decor(parent, RockN, new Vector3(Dungeon2.EntranceX - 0.8f, 0f, Dungeon2.EntranceZ + 1.3f), new Vector3(0f, 10f, 0f));

            var interior = new GameObject(Dungeon2.InteriorObject);
            interior.transform.SetParent(parent, false);
            interior.transform.position = OnGround(new Vector3(Dungeon2.InteriorX, 0f, Dungeon2.InteriorZ));
            Transform room = interior.transform;
            Decor(room, Planks, new Vector3(Dungeon2.InteriorX, 0.02f, Dungeon2.InteriorZ), Vector3.zero);
            Decor(room, RockW, new Vector3(Dungeon2.InteriorX + 3.2f, 0f, Dungeon2.InteriorZ), new Vector3(0f, 90f, 0f));
            Decor(room, RockW, new Vector3(Dungeon2.InteriorX - 3.2f, 0f, Dungeon2.InteriorZ), new Vector3(0f, 90f, 0f));
            Decor(room, RockW, new Vector3(Dungeon2.InteriorX, 0f, Dungeon2.InteriorZ + 3.2f), Vector3.zero);
            Decor(room, RockL, new Vector3(Dungeon2.InteriorX + 2.2f, 0f, Dungeon2.InteriorZ + 2.2f), new Vector3(0f, 30f, 0f));
            Decor(room, RockS, new Vector3(Dungeon2.InteriorX - 1.6f, 0f, Dungeon2.InteriorZ - 1.8f), new Vector3(0f, 70f, 0f));
            Decor(room, Lantern, new Vector3(Dungeon2.InteriorX - 2.4f, 0f, Dungeon2.InteriorZ + 2.1f), Vector3.zero);

            var exitGo = Place(Arch, new Vector3(Dungeon2.ExitX, 0f, Dungeon2.ExitZ), new Vector3(0f, 45f, 0f));
            if (exitGo == null)
                exitGo = Place(RockS, new Vector3(Dungeon2.ExitX, 0f, Dungeon2.ExitZ), Vector3.zero);
            if (exitGo == null)
                throw new InvalidOperationException("던전 2 출구 모델 없음");
            exitGo.name = Dungeon2.ExitObject;
            exitGo.transform.SetParent(parent, true);
            var xg = exitGo.GetComponent<DungeonGate>() ?? exitGo.AddComponent<DungeonGate>();
            xg.DungeonId = Dungeon2.Id;
            xg.IsExit = true;
            xg.DisplayName = "던전 2 출구";
            EnsureCollider(exitGo);

            EnsureDungeon2Mob(parent);
            EnsureDungeon2Boss(parent);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }

        public static void EnsureFieldBoss()
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (ctrl == null)
                return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(MageFbx) == null)
                return;
            var stray = GameObject.Find(FieldBoss.Object);
            if (stray != null)
                UnityEngine.Object.DestroyImmediate(stray);
            ConfigureHumanoid(MageFbx, true);
            var spawned = SpawnActor(
                FieldBoss.Object,
                MageFbx,
                new Vector3(FieldBoss.X, 0f, FieldBoss.Z),
                MobCatalog.HeightOf(MobCatalog.Hexarch),
                ctrl,
                false,
                true,
                MobCatalog.DisplayNameOf(MobCatalog.Hexarch),
                MobCatalog.MaxHpOf(MobCatalog.Hexarch));
            BindMob(spawned, MobCatalog.Hexarch);
            HideExtraGear(spawned);
        }


        static void EnsureDungeonMob(Transform parent)
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (ctrl == null)
                return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(SkeletonFbx) == null)
                return;
            ConfigureHumanoid(SkeletonFbx, true);
            var spawned = SpawnActor(
                Dungeon1.MobObject,
                SkeletonFbx,
                new Vector3(Dungeon1.MobX, 0f, Dungeon1.MobZ),
                MobCatalog.HeightOf(MobCatalog.Skeleton),
                ctrl,
                false,
                true,
                MobCatalog.DisplayNameOf(MobCatalog.Skeleton),
                MobCatalog.MaxHpOf(MobCatalog.Skeleton));
            BindMob(spawned, MobCatalog.Skeleton);
            HideExtraGear(spawned);
            if (parent != null)
                spawned.transform.SetParent(parent, true);
        }

        static void EnsureDungeonBoss(Transform parent)
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (ctrl == null)
                return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(SkeletonFbx) == null)
                return;
            ConfigureHumanoid(SkeletonFbx, true);
            var spawned = SpawnActor(
                Dungeon1.BossObject,
                SkeletonFbx,
                new Vector3(Dungeon1.BossX, 0f, Dungeon1.BossZ),
                MobCatalog.HeightOf(MobCatalog.BoneWarden),
                ctrl,
                false,
                true,
                MobCatalog.DisplayNameOf(MobCatalog.BoneWarden),
                MobCatalog.MaxHpOf(MobCatalog.BoneWarden));
            BindMob(spawned, MobCatalog.BoneWarden);
            HideExtraGear(spawned);
            if (parent != null)
                spawned.transform.SetParent(parent, true);
        }

        static void EnsureDungeon2Mob(Transform parent)
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (ctrl == null)
                return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(MageFbx) == null)
                return;
            ConfigureHumanoid(MageFbx, true);
            var spawned = SpawnActor(
                Dungeon2.MobObject,
                MageFbx,
                new Vector3(Dungeon2.MobX, 0f, Dungeon2.MobZ),
                MobCatalog.HeightOf(MobCatalog.Bandit),
                ctrl,
                false,
                true,
                MobCatalog.DisplayNameOf(MobCatalog.Bandit),
                MobCatalog.MaxHpOf(MobCatalog.Bandit));
            BindMob(spawned, MobCatalog.Bandit);
            HideExtraGear(spawned);
            if (parent != null)
                spawned.transform.SetParent(parent, true);
        }

        static void EnsureDungeon2Boss(Transform parent)
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (ctrl == null)
                return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(RogueFbx) == null)
                return;
            ConfigureHumanoid(RogueFbx, true);
            var spawned = SpawnActor(
                Dungeon2.BossObject,
                RogueFbx,
                new Vector3(Dungeon2.BossX, 0f, Dungeon2.BossZ),
                MobCatalog.HeightOf(MobCatalog.ShadowCaptain),
                ctrl,
                false,
                true,
                MobCatalog.DisplayNameOf(MobCatalog.ShadowCaptain),
                MobCatalog.MaxHpOf(MobCatalog.ShadowCaptain));
            BindMob(spawned, MobCatalog.ShadowCaptain);
            HideExtraGear(spawned);
            if (parent != null)
                spawned.transform.SetParent(parent, true);
        }



        public static void EnsureLockedCrate()
        {
            const string fbx = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/cart.fbx";
            Vector3 pos = new Vector3(-7.4f, 0f, 6.4f);
            var go = GameObject.Find("LockedCrate");
            if (go == null)
                go = GameObject.Find("cart");
            if (go == null)
                go = Place(fbx, pos, Vector3.zero);
            else
                go.transform.SetPositionAndRotation(OnGround(pos), Quaternion.identity);
            if (go == null)
                return;
            go.name = "LockedCrate";
            var crate = go.GetComponent<LockedCrate>() ?? go.AddComponent<LockedCrate>();
            crate.DisplayName = "잠긴 상자";
            EnsureCollider(go);
        }

        public static void EnsureHouseVendor()
        {
            var plot = GameObject.Find(HousingPlot.RootObject);
            if (plot == null)
                return;
            var stray = GameObject.Find(HousingPlot.VendorObject);
            if (stray != null && stray.transform.parent != plot.transform)
            {
                UnityEngine.Object.DestroyImmediate(stray);
                stray = null;
            }
            Vector3 pos = OnGround(new Vector3(HousingPlot.X - 1.6f, 0f, HousingPlot.Z + 1.4f));
            GameObject go = stray;
            if (go == null)
            {
                const string prefabPath = "Assets/Game/Prefabs/Env/Stall.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                    go = Place("Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/stall.fbx", pos, new Vector3(0f, 90f, 0f));
                else
                {
                    go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, 90f, 0f));
                    SnapRootToGround(go);
                }
            }
            if (go == null)
                go = new GameObject(HousingPlot.VendorObject);
            go.name = HousingPlot.VendorObject;
            go.transform.SetParent(plot.transform, true);
            go.transform.position = pos;
            SnapRootToGround(go);
            var hv = go.GetComponent<HouseVendor>() ?? go.AddComponent<HouseVendor>();
            hv.PlotId = HousingPlot.Id;
            hv.DisplayName = "주택 상점";
            hv.InteractRange = HousingPlot.InteractRange;
            EnsureCollider(go);
        }

        public static void EnsureHousingPlot()
        {
            if (GameObject.Find(HousingPlot.RootObject) != null)
            {
                EnsureHouseVendor();
                return;
            }
            const string Fence = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/fence.fbx";
            const string Poles = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/poles.fbx";
            const string Bench = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/stall-bench.fbx";
            const string Wall = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/wall-window-glass.fbx";
            const string Door = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/wall-door.fbx";
            const string Roof = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/roof.fbx";
            const string Chimney = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/chimney.fbx";
            const string Gable = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/roof-gable-end.fbx";
            const string Gate = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/fence-gate.fbx";
            string[] models = { Fence, Gate, Poles, Bench, Wall, Door, Roof, Chimney, Gable };
            for (int i = 0; i < models.Length; i++)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(models[i]) == null)
                    ConfigureProp(models[i]);
            }
            var old = GameObject.Find(HousingPlot.RootObject);
            if (old != null)
                UnityEngine.Object.DestroyImmediate(old);
            var strayStation = GameObject.Find(HousingPlot.StationObject);
            if (strayStation != null)
                UnityEngine.Object.DestroyImmediate(strayStation);
            var strayChest = GameObject.Find(HousingPlot.ChestObject);
            if (strayChest != null)
                UnityEngine.Object.DestroyImmediate(strayChest);
            var root = new GameObject(HousingPlot.RootObject);
            Vector3 center = OnGround(new Vector3(HousingPlot.X, 0f, HousingPlot.Z));
            root.transform.position = center;
            Transform parent = root.transform;
            float half = 2.2f;
            float step = PrefabRunLength(Fence);
            float gateGap = 1.35f;
            Decor(parent, Gate, new Vector3(HousingPlot.X, 0f, HousingPlot.Z + half), Vector3.zero);
            PlaceRun(parent, Fence, HousingPlot.X - half, HousingPlot.Z - half, HousingPlot.X + half, HousingPlot.Z - half, 0f, step, 0f, 0f, 0f);
            PlaceRun(parent, Fence, HousingPlot.X - half, HousingPlot.Z + half, HousingPlot.X + half, HousingPlot.Z + half, 0f, step, HousingPlot.X, HousingPlot.Z + half, gateGap);
            PlaceRun(parent, Fence, HousingPlot.X - half, HousingPlot.Z - half, HousingPlot.X - half, HousingPlot.Z + half, 90f, step, 0f, 0f, 0f);
            PlaceRun(parent, Fence, HousingPlot.X + half, HousingPlot.Z - half, HousingPlot.X + half, HousingPlot.Z + half, 90f, step, 0f, 0f, 0f);
            var station = Place(Poles, center, Vector3.zero);
            if (station == null)
                station = new GameObject(HousingPlot.StationObject);
            station.name = HousingPlot.StationObject;
            station.transform.SetParent(parent, true);
            station.transform.position = center;
            var hs = station.GetComponent<HousePlotStation>() ?? station.AddComponent<HousePlotStation>();
            hs.PlotId = HousingPlot.Id;
            hs.DisplayName = "주택 부지";
            hs.InteractRange = HousingPlot.InteractRange;
            EnsureCollider(station);

            var house = new GameObject(HousingPlot.HouseObject);
            house.transform.SetParent(parent, false);
            house.transform.localPosition = new Vector3(-1f, 0f, -0.2f);
            Transform hp = house.transform;
            int width = 2;
            int depth = 2;
            for (int z = 0; z < depth; z++)
            {
                DecorLocal(hp, Wall, new Vector3(0.5f, 0f, z + 0.5f), Vector3.zero);
                DecorLocal(hp, Wall, new Vector3(width - 0.5f, 0f, z + 0.5f), new Vector3(0f, 180f, 0f));
            }
            for (int x = 0; x < width; x++)
            {
                string south = x == 1 ? Door : Wall;
                DecorLocal(hp, south, new Vector3(x + 0.5f, 0f, 0.5f), new Vector3(0f, 90f, 0f));
                DecorLocal(hp, Wall, new Vector3(x + 0.5f, 0f, depth - 0.5f), new Vector3(0f, 270f, 0f));
            }
            for (int z = 0; z < depth; z++)
            {
                DecorLocal(hp, Roof, new Vector3(0.5f, 1f, z + 0.5f), Vector3.zero);
                DecorLocal(hp, Roof, new Vector3(1.5f, 1f, z + 0.5f), new Vector3(0f, 180f, 0f));
            }
            DecorLocal(hp, Gable, new Vector3(1f, 1f, 0.5f), new Vector3(0f, 90f, 0f));
            DecorLocal(hp, Gable, new Vector3(1f, 1f, depth - 0.5f), new Vector3(0f, 270f, 0f));
            DecorLocal(hp, Chimney, new Vector3(1.65f, 1f, depth - 0.55f), Vector3.zero);
            SnapRootToGround(house);
            house.SetActive(false);

            Vector3 chestPos = OnGround(new Vector3(HousingPlot.X + 1.6f, 0f, HousingPlot.Z - 1.4f));
            var chest = Place(Bench, chestPos, new Vector3(0f, 90f, 0f));
            if (chest == null)
                chest = new GameObject(HousingPlot.ChestObject);
            chest.name = HousingPlot.ChestObject;
            chest.transform.SetParent(parent, true);
            var hc = chest.GetComponent<HouseChest>() ?? chest.AddComponent<HouseChest>();
            hc.PlotId = HousingPlot.Id;
            hc.DisplayName = "주택 상자";
            hc.InteractRange = HousingPlot.InteractRange;
            EnsureCollider(chest);
            EnsureHouseVendor();
        }


        public static void EnsureTameCritter()
        {
            const string fbx = "Assets/_ThirdParty/Kenney/Nature/RAW/Models/plant_bushLarge.fbx";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(fbx) == null)
                ConfigureProp(fbx);
            var old = GameObject.Find(TameCritter.Object);
            if (old != null)
                UnityEngine.Object.DestroyImmediate(old);
            Vector3 pos = new Vector3(TameCritter.X, 0f, TameCritter.Z);
            var go = Place(fbx, pos, Vector3.zero);
            if (go == null)
                throw new InvalidOperationException("조련 대상 Kenney Nature 메시 없음");
            go.name = TameCritter.Object;
            go.transform.SetPositionAndRotation(OnGround(pos), Quaternion.identity);
            EnsureCollider(go);
            var body = go.GetComponent<WorldBody>() ?? go.AddComponent<WorldBody>();
            body.MobId = TameCritter.Id;
            body.DisplayName = TameCritter.DisplayName;
            body.IsEnemy = false;
            body.IsAvatar = false;
            body.Tameable = true;
            body.ControlSlots = TameCritter.ControlSlots;
            body.OwnerCharacterId = "";
            body.PetFollow = false;
            body.ApplyMobCatalog();
            body.ResetHp();
        }



        public static void EnsureTameBoar()
        {
            const string fbx = "Assets/_ThirdParty/Kenney/Nature/RAW/Models/plant_bush.fbx";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(fbx) == null)
                ConfigureProp(fbx);
            var old = GameObject.Find(TameBoar.Object);
            if (old != null)
                UnityEngine.Object.DestroyImmediate(old);
            Vector3 pos = new Vector3(TameBoar.X, 0f, TameBoar.Z);
            var go = Place(fbx, pos, Vector3.zero);
            if (go == null)
                throw new InvalidOperationException("조련 멧돼지 Kenney Nature 메시 없음");
            go.name = TameBoar.Object;
            go.transform.SetPositionAndRotation(OnGround(pos), Quaternion.identity);
            EnsureCollider(go);
            var body = go.GetComponent<WorldBody>() ?? go.AddComponent<WorldBody>();
            body.MobId = TameBoar.Id;
            body.DisplayName = TameBoar.DisplayName;
            body.IsEnemy = false;
            body.IsAvatar = false;
            body.Tameable = true;
            body.ControlSlots = TameBoar.ControlSlots;
            body.OwnerCharacterId = "";
            body.PetFollow = false;
            body.ApplyMobCatalog();
            body.ResetHp();
        }

        public static void EnsureMoongate()
        {
            const string Arch = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/wall-arch.fbx";
            const string Lantern = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/lantern.fbx";
            string[] models = { Arch, Lantern };
            for (int i = 0; i < models.Length; i++)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(models[i]) == null)
                    ConfigureProp(models[i]);
            }
            var old = GameObject.Find(TravelGate.Object);
            if (old != null)
                UnityEngine.Object.DestroyImmediate(old);
            Vector3 pos = new Vector3(TravelGate.X, 0f, TravelGate.Z);
            var go = Place(Arch, pos, new Vector3(0f, 0f, 0f));
            if (go == null)
                throw new InvalidOperationException("문게이트 Kenney arch 없음");
            go.name = TravelGate.Object;
            go.transform.SetPositionAndRotation(OnGround(pos), Quaternion.Euler(0f, 0f, 0f));
            var moon = go.GetComponent<Moongate>() ?? go.AddComponent<Moongate>();
            moon.DisplayName = TravelGate.DisplayName;
            moon.InteractRange = TravelGate.InteractRange;
            EnsureCollider(go);
            Decor(go.transform, Lantern, new Vector3(TravelGate.X + 1.1f, 0f, TravelGate.Z + 0.4f), Vector3.zero);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }

        public static void EnsureStable()
        {
            const string stallPath = "Assets/Game/Prefabs/Env/Stall.prefab";
            const string polesPath = "Assets/Game/Prefabs/Env/Poles.prefab";
            Vector3 pos = OnGround(new Vector3(StableYard.X, 0f, StableYard.Z));
            var go = GameObject.Find(StableYard.Object);
            if (go != null)
            {
                string existing = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
                if (string.IsNullOrEmpty(existing) || existing.IndexOf("/RAW/", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    UnityEngine.Object.DestroyImmediate(go);
                    go = null;
                }
            }
            if (go == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(stallPath);
                if (prefab != null)
                {
                    go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, 90f, 0f));
                    SnapRootToGround(go);
                }
                else
                    go = Place("Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/stall.fbx", pos, new Vector3(0f, 90f, 0f));
            }
            if (go == null)
                go = new GameObject(StableYard.Object);
            go.name = StableYard.Object;
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, 90f, 0f));
            SnapRootToGround(go);
            var sm = go.GetComponent<StableMaster>() ?? go.AddComponent<StableMaster>();
            sm.DisplayName = StableYard.DisplayName;
            sm.InteractRange = StableYard.InteractRange;
            EnsureCollider(go);
            Transform poles = go.transform.Find(StableYard.PolesObject);
            if (poles == null)
            {
                var polesPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(polesPath);
                if (polesPrefab != null)
                {
                    var polesGo = (GameObject)PrefabUtility.InstantiatePrefab(polesPrefab);
                    polesGo.name = StableYard.PolesObject;
                    polesGo.transform.SetParent(go.transform, true);
                    Vector3 polesPos = OnGround(new Vector3(StableYard.X + 1.6f, 0f, StableYard.Z));
                    polesGo.transform.SetPositionAndRotation(polesPos, Quaternion.identity);
                    SnapRootToGround(polesGo);
                }
            }
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }

        public static void EnsureMortar()
        {
            const string fbx = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/stall-stool.fbx";
            Vector3 pos = new Vector3(-6.8f, 0f, -6.6f);
            var go = GameObject.Find("Mortar");
            if (go == null)
                go = Place(fbx, pos, Vector3.zero);
            else
                go.transform.SetPositionAndRotation(OnGround(pos), Quaternion.identity);
            if (go == null)
                return;
            go.name = "Mortar";
            var station = go.GetComponent<CraftStation>() ?? go.AddComponent<CraftStation>();
            station.RecipeId = "health_potion";
            station.DisplayName = "절구";
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

        public static void EnsureCampfire()
        {
            const string fbx = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/lantern.fbx";
            Vector3 pos = new Vector3(-9.2f, 0f, -6.8f);
            var go = GameObject.Find("Campfire");
            if (go == null)
                go = Place(fbx, pos, Vector3.zero);
            else
                go.transform.SetPositionAndRotation(OnGround(pos), Quaternion.identity);
            if (go == null)
                return;
            go.name = "Campfire";
            var station = go.GetComponent<CraftStation>() ?? go.AddComponent<CraftStation>();
            station.RecipeId = "cooked_fish";
            station.DisplayName = "화덕";
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
            else
                SnapRootToGround(go);
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

        static void SnapRootToGround(GameObject go)
        {
            if (go == null)
                return;
            Bounds b = CombinedBounds(go);
            if (b.size.sqrMagnitude < 0.0001f)
                return;
            float g = GroundY(b.center.x, b.center.z);
            g = Mathf.Max(g, GroundY(b.min.x, b.min.z));
            g = Mathf.Max(g, GroundY(b.min.x, b.max.z));
            g = Mathf.Max(g, GroundY(b.max.x, b.min.z));
            g = Mathf.Max(g, GroundY(b.max.x, b.max.z));
            float dy = g - b.min.y;
            if (Mathf.Abs(dy) < 0.0005f)
                return;
            go.transform.position += new Vector3(0f, dy, 0f);
        }

        static void SnapBuildingsToGround()
        {
            var all = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                var go = all[i];
                if (go == null || !go.scene.IsValid())
                    continue;
                if (IsCharacterArt(go.transform))
                    continue;
                string n = go.name;
                if (n == "House" || n == "Banker" || n == "Forge" || n == "Vendor" || n == "Healer"
                    || n == "FishingSpot" || n == "watermill" || n == "Campfire" || n == "Mortar"
                    || n == "OakTree" || n == "IronVein" || n == HousingPlot.HouseObject
                    || n == HousingPlot.VendorObject)
                    SnapRootToGround(go);
            }
        }

        static void AssertHousesOnGround()
        {
            var all = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var sunk = new List<string>();
            for (int i = 0; i < all.Length; i++)
            {
                var go = all[i];
                if (go == null || !go.scene.IsValid())
                    continue;
                string n = go.name;
                bool house = n == "House" || n == "Banker" || n == "Forge" || n == "Vendor" || n == "Healer"
                    || n == "watermill" || n == HousingPlot.HouseObject || n == HousingPlot.VendorObject;
                if (!house)
                    continue;
                Bounds b = CombinedBounds(go);
                if (b.min.y < -0.05f)
                    sunk.Add(n + " minY=" + b.min.y.ToString("0.###"));
            }
            if (sunk.Count > 0)
                throw new InvalidOperationException("건물이 땅속에 있음: " + string.Join("; ", sunk));
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

        static void PlacePaddockFences(Transform parent, string fence, string gate)
        {
            float step = PrefabRunLength(fence);
            // House lots: 3-sided paddocks opening to the road. Not a town wall / cross palisade.
            PlaceLotU(parent, fence, gate, -8.3f, 1.15f, -4.7f, 4.05f, 0, step);
            PlaceLotU(parent, fence, gate, 1.7f, 1.15f, 5.3f, 4.05f, 0, step);
            PlaceLotU(parent, fence, gate, -8.3f, -4.85f, -4.7f, -2.0f, 2, step);
            PlaceLotU(parent, fence, gate, 1.7f, -4.85f, 5.3f, -2.0f, 2, step);
            PlaceLotU(parent, fence, gate, -1.0f, -5.55f, 2.05f, -2.05f, 3, step);
            PlaceLotU(parent, fence, gate, -3.05f, 2.05f, 0.05f, 5.05f, 1, step);

            // Field paddocks: large lot enclosures, gentle polylines, gate toward village roads.
            PlaceCurvedLoop(parent, fence, gate, new[]
            {
                new Vector3(16.9f, 0f, -2.8f), new Vector3(19.4f, 0f, -4.2f), new Vector3(22.6f, 0f, -2.2f),
                new Vector3(23.4f, 0f, 1.8f), new Vector3(22.4f, 0f, 6.4f), new Vector3(19.0f, 0f, 7.4f),
                new Vector3(17.0f, 0f, 5.6f), new Vector3(16.8f, 0f, 1.6f)
            }, 7, new Vector3(0f, 90f, 0f));
            PlaceCurvedLoop(parent, fence, gate, new[]
            {
                new Vector3(-2.2f, 0f, -17.0f), new Vector3(2.8f, 0f, -17.2f), new Vector3(8.2f, 0f, -17.0f),
                new Vector3(9.6f, 0f, -19.4f), new Vector3(7.6f, 0f, -23.4f), new Vector3(1.6f, 0f, -24.4f),
                new Vector3(-2.8f, 0f, -22.6f), new Vector3(-3.4f, 0f, -19.2f)
            }, 1, Vector3.zero);
            PlaceCurvedLoop(parent, fence, gate, new[]
            {
                new Vector3(-17.8f, 0f, 17.0f), new Vector3(-12.2f, 0f, 17.0f), new Vector3(-7.2f, 0f, 17.4f),
                new Vector3(-5.8f, 0f, 21.2f), new Vector3(-8.4f, 0f, 24.8f), new Vector3(-13.6f, 0f, 25.4f),
                new Vector3(-18.2f, 0f, 23.0f), new Vector3(-18.8f, 0f, 19.2f)
            }, 1, Vector3.zero);

            // Road gates where plaza roads leave town — openings, not a ring wall.
            Decor(parent, gate, new Vector3(-8.2f, 0f, 0f), new Vector3(0f, 90f, 0f));
            Decor(parent, gate, new Vector3(10.2f, 0f, 0f), new Vector3(0f, 90f, 0f));
            Decor(parent, gate, new Vector3(0f, 0f, -7.2f), Vector3.zero);
            Decor(parent, gate, new Vector3(0f, 0f, 11.2f), Vector3.zero);
        }

        static void PlaceLotU(Transform parent, string fence, string gate, float x0, float z0, float x1, float z1, int openSide, float step)
        {
            float gx = (x0 + x1) * 0.5f;
            float gz = (z0 + z1) * 0.5f;
            if (openSide != 0)
                PlaceRun(parent, fence, x0, z0, x1, z0, 0f, step, 0f, 0f, 0f);
            if (openSide != 1)
                PlaceRun(parent, fence, x1, z0, x1, z1, 90f, step, 0f, 0f, 0f);
            if (openSide != 2)
                PlaceRun(parent, fence, x0, z1, x1, z1, 0f, step, 0f, 0f, 0f);
            if (openSide != 3)
                PlaceRun(parent, fence, x0, z0, x0, z1, 90f, step, 0f, 0f, 0f);
            Vector3 ge = openSide == 1 || openSide == 3 ? new Vector3(0f, 90f, 0f) : Vector3.zero;
            float x = openSide == 1 ? x1 : (openSide == 3 ? x0 : gx);
            float z = openSide == 0 ? z0 : (openSide == 2 ? z1 : gz);
            Decor(parent, gate, new Vector3(x, 0f, z), ge);
        }

        static void PlaceCurvedLoop(Transform parent, string fence, string gate, Vector3[] pts, int gateIndex, Vector3 gateEuler)
        {
            float step = PrefabRunLength(fence);
            int n = pts.Length;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Vector3 a = pts[i];
                Vector3 b = pts[j];
                Vector3 mid = (a + b) * 0.5f;
                Vector3 outward = new Vector3(mid.x, 0f, mid.z);
                if (outward.sqrMagnitude > 0.01f)
                    outward.Normalize();
                mid += outward * 0.45f;
                if (i == gateIndex)
                {
                    Decor(parent, gate, new Vector3(mid.x, 0f, mid.z), gateEuler);
                    continue;
                }
                PlaceRun(parent, fence, a.x, a.z, mid.x, mid.z, 0f, step, 0f, 0f, 0f);
                PlaceRun(parent, fence, mid.x, mid.z, b.x, b.z, 0f, step, 0f, 0f, 0f);
            }
        }

        static void PlaceVillagePalisade(Transform parent, string fence, string gate, string hedge)
        {
            PlacePaddockFences(parent, fence, gate);
        }

        static float PrefabRunLength(string path)
        {
            if (path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                path = EnsureEnvPrefab(path);
            if (string.IsNullOrEmpty(path) || AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                return 1f;
            var go = PrefabUtility.LoadPrefabContents(path);
            Bounds b = CombinedBounds(go);
            float len = Mathf.Max(b.size.x, b.size.z);
            PrefabUtility.UnloadPrefabContents(go);
            return Mathf.Clamp(len, 0.75f, 2.5f);
        }

        static void PlaceRun(Transform parent, string model, float x0, float z0, float x1, float z1, float yaw, float step, float skipX, float skipZ, float skipR)
        {
            float dx = x1 - x0;
            float dz = z1 - z0;
            float len = Mathf.Sqrt(dx * dx + dz * dz);
            if (len < 0.05f)
                return;
            if (step < 0.4f)
                step = 0.8f;
            int n = Mathf.Max(1, Mathf.RoundToInt(len / step));
            float actual = len / n;
            float ux = dx / len;
            float uz = dz / len;
            float along = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
            for (int i = 0; i < n; i++)
            {
                float t = (i + 0.5f) * actual;
                float x = x0 + ux * t;
                float z = z0 + uz * t;
                if (skipR > 0.01f)
                {
                    float sx = x - skipX;
                    float sz = z - skipZ;
                    if (sx * sx + sz * sz < skipR * skipR)
                        continue;
                }
                Decor(parent, model, new Vector3(x, 0f, z), new Vector3(0f, along, 0f));
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
            SnapRootToGround(root);
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
            if (visual == null)
                visual = FindMeshChildToNameVisual(root.transform);
            GameObject host = visual != null ? visual.gameObject : root;
            var anim = host.GetComponentInChildren<Animator>(true);
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
            else if (rootName == "Skeleton" || rootName == Dungeon1.MobObject || rootName == Dungeon1.BossObject)
                fbx = SkeletonFbx;
            else if (rootName == "Bandit" || rootName == "Trainer" || rootName == Dungeon2.MobObject || rootName == FieldBoss.Object)
                fbx = MageFbx;
            else if (rootName == "Raider")
                fbx = BarbarianFbx;
            else if (rootName == "Rogue" || rootName == Dungeon2.BossObject)
                fbx = RogueFbx;
            else if (rootName == "Knight")
                fbx = KnightFbx;
            else if (rootName == "Acolyte")
                fbx = SkeletonMageFbx;
            else if (rootName == "Minion")
                fbx = SkeletonMinionFbx;
            else if (rootName == "SkelRogue")
                fbx = SkeletonRogueFbx;
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
            ConfigureHumanoid(RogueFbx, true);
            ConfigureHumanoid(SkeletonFbx, true);
            ConfigureHumanoid(SkeletonMageFbx, true);
            ConfigureHumanoid(SkeletonRogueFbx, true);
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
            var raider = SpawnActor("Raider", BarbarianFbx, new Vector3(2.4f, 0f, 13.2f), MobCatalog.HeightOf(MobCatalog.Raider), controller, false, true, MobCatalog.DisplayNameOf(MobCatalog.Raider), MobCatalog.MaxHpOf(MobCatalog.Raider));
            BindMob(raider, MobCatalog.Raider);
            HideExtraGear(raider);
            var rogue = SpawnActor("Rogue", RogueFbx, new Vector3(-3.8f, 0f, 13.2f), MobCatalog.HeightOf(MobCatalog.Rogue), controller, false, true, MobCatalog.DisplayNameOf(MobCatalog.Rogue), MobCatalog.MaxHpOf(MobCatalog.Rogue));
            BindMob(rogue, MobCatalog.Rogue);
            HideExtraGear(rogue);
            var knight = SpawnActor("Knight", KnightFbx, new Vector3(4.4f, 0f, 13.2f), MobCatalog.HeightOf(MobCatalog.Knight), controller, false, true, MobCatalog.DisplayNameOf(MobCatalog.Knight), MobCatalog.MaxHpOf(MobCatalog.Knight));
            BindMob(knight, MobCatalog.Knight);
            HideExtraGear(knight);
            var acolyte = SpawnActor("Acolyte", SkeletonMageFbx, new Vector3(6.4f, 0f, 13.2f), MobCatalog.HeightOf(MobCatalog.Acolyte), controller, false, true, MobCatalog.DisplayNameOf(MobCatalog.Acolyte), MobCatalog.MaxHpOf(MobCatalog.Acolyte));
            BindMob(acolyte, MobCatalog.Acolyte);
            HideExtraGear(acolyte);
            var minion = SpawnActor("Minion", SkeletonMinionFbx, new Vector3(8.4f, 0f, 13.2f), MobCatalog.HeightOf(MobCatalog.Minion), controller, false, true, MobCatalog.DisplayNameOf(MobCatalog.Minion), MobCatalog.MaxHpOf(MobCatalog.Minion));
            BindMob(minion, MobCatalog.Minion);
            HideExtraGear(minion);
            var skelRogue = SpawnActor("SkelRogue", SkeletonRogueFbx, new Vector3(10.4f, 0f, 13.2f), MobCatalog.HeightOf(MobCatalog.SkelRogue), controller, false, true, MobCatalog.DisplayNameOf(MobCatalog.SkelRogue), MobCatalog.MaxHpOf(MobCatalog.SkelRogue));
            BindMob(skelRogue, MobCatalog.SkelRogue);
            HideExtraGear(skelRogue);
            var hexarch = SpawnActor(FieldBoss.Object, MageFbx, new Vector3(FieldBoss.X, 0f, FieldBoss.Z), MobCatalog.HeightOf(MobCatalog.Hexarch), controller, false, true, MobCatalog.DisplayNameOf(MobCatalog.Hexarch), MobCatalog.MaxHpOf(MobCatalog.Hexarch));
            BindMob(hexarch, MobCatalog.Hexarch);
            HideExtraGear(hexarch);

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
            string displayName = Path.GetFileNameWithoutExtension(path);
            if (path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                path = EnsureEnvPrefab(path);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = displayName;
            float yaw = rot.eulerAngles.y;
            go.transform.SetPositionAndRotation(OnGround(new Vector3(pos.x, 0f, pos.z)) + Vector3.up * pos.y, Quaternion.Euler(0f, yaw, 0f));
            SnapRootToGround(go);
            return go;
        }

        static GameObject SpawnActor(string name, string fbx, Vector3 pos, float height, RuntimeAnimatorController controller, bool player, bool enemy, string display, float hp)
        {
            string prefabPath = EnsureKayKitPrefab(fbx);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (model == null)
                throw new InvalidOperationException("모델 없음: " + fbx + " prefab=" + prefabPath);

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
            var anim = visual.GetComponentInChildren<Animator>(true);
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
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnsureKayKitPrefab(path));
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

        internal static bool ConfigureHumanoid(string path, bool importClips)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
                throw new InvalidOperationException("importer 없음: " + path);
            if (IsHumanoidConfigured(importer, importClips))
                return false;
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
            return true;
        }

        static bool IsHumanoidConfigured(ModelImporter importer, bool importClips)
        {
            if (importer.animationType != ModelImporterAnimationType.Human
                || importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel
                || importer.optimizeGameObjects
                || importer.importAnimation != importClips
                || importer.animationCompression != ModelImporterAnimationCompression.Off
                || importer.materialImportMode != ModelImporterMaterialImportMode.ImportStandard)
                return false;

            if (importClips && !LocomotionClipsConfigured(importer.clipAnimations))
                return false;

            var desc = importer.humanDescription;
            if (desc.hasTranslationDoF)
                return false;
            return SameHumanBones(desc.human, BuildHuman(desc.skeleton));
        }

        static bool LocomotionClipsConfigured(ModelImporterClipAnimation[] clips)
        {
            if (clips == null || clips.Length == 0)
                return false;
            for (int i = 0; i < clips.Length; i++)
            {
                string n = clips[i].name.ToLowerInvariant();
                bool shouldLoop = n.Contains("idle") || n.Contains("walk") || n.Contains("run")
                    || n.Contains("aiming") || n.Contains("shooting");
                if (clips[i].loopTime != shouldLoop || clips[i].loopPose != shouldLoop)
                    return false;
            }
            return true;
        }

        static bool SameHumanBones(HumanBone[] actual, HumanBone[] expected)
        {
            if (actual == null || expected == null || actual.Length != expected.Length)
                return false;
            for (int i = 0; i < actual.Length; i++)
            {
                if (actual[i].humanName != expected[i].humanName
                    || actual[i].boneName != expected[i].boneName
                    || actual[i].limit.useDefaultValues != expected[i].limit.useDefaultValues)
                    return false;
            }
            return true;
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

        const string EnvPrefabFolder = "Assets/Game/Prefabs/Env";

        [MenuItem("Ulon/Build Env Prefabs")]
        public static void BuildEnvPrefabs()
        {
            EnsureEnvFolder();
            string[] src = EnvFbxSources();
            int n = 0;
            for (int i = 0; i < src.Length; i++)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(src[i]) == null)
                    ConfigureProp(src[i]);
                string created = EnsureEnvPrefab(src[i], true);
                if (!string.IsNullOrEmpty(created))
                    n++;
            }
            EnsureEnvPrefab("Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/fence.fbx", true);
            EnsureEnvPrefab("Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/fence-gate.fbx", true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Ulon] Env prefabs built: " + n + " under " + EnvPrefabFolder);
        }

        static string[] KenneyProps()
        {
            return EnvFbxSources();
        }

        static string[] EnvFbxSources()
        {
            return new[]
            {
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/banner-red.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/cart-high.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/cart.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/chimney.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/fence-gate.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/fence.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/fountain-round.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/hedge-large.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/hedge.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/lantern.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/overhang.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/planks.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/poles.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/road.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/rock-large.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/rock-small.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/rock-wide.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/roof-gable-end.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/roof-high-gable-end.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/roof-high.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/roof.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/stairs-wood.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/stall-bench.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/stall-green.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/stall-red.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/stall-stool.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/stall.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/tree-crooked.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/tree-high-crooked.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/tree-high-round.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/tree-high.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/tree.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/wall-arch.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/wall-door.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/wall-window-glass.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/wall-window-shutters.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/wall-wood-door.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/wall-wood-window-glass.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/watermill.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/windmill.fbx",
                "Assets/_ThirdParty/Kenney/Nature/RAW/Models/grass.fbx",
                "Assets/_ThirdParty/Kenney/Nature/RAW/Models/grass_large.fbx",
                "Assets/_ThirdParty/Kenney/Nature/RAW/Models/grass_leafs.fbx",
                "Assets/_ThirdParty/Kenney/Nature/RAW/Models/ground_grass.fbx",
                "Assets/_ThirdParty/Kenney/Nature/RAW/Models/ground_pathTile.fbx",
                "Assets/_ThirdParty/Kenney/Nature/RAW/Models/plant_bush.fbx",
                "Assets/_ThirdParty/Kenney/Nature/RAW/Models/plant_bushLarge.fbx",
                "Assets/_ThirdParty/Kenney/Nature/RAW/Models/rock_largeA.fbx",
                "Assets/_ThirdParty/Kenney/Nature/RAW/Models/rock_smallA.fbx"
            };
        }

        static void EnsureEnvFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Game/Prefabs"))
                AssetDatabase.CreateFolder("Assets/Game", "Prefabs");
            if (!AssetDatabase.IsValidFolder(EnvPrefabFolder))
                AssetDatabase.CreateFolder("Assets/Game/Prefabs", "Env");
        }

        static string EnvPrefabPath(string fbxPath)
        {
            return EnvPrefabFolder + "/" + EnvPrefabName(fbxPath) + ".prefab";
        }

        static string EnvPrefabName(string fbxPath)
        {
            string n = Path.GetFileNameWithoutExtension(fbxPath);
            var sb = new System.Text.StringBuilder();
            bool cap = true;
            for (int i = 0; i < n.Length; i++)
            {
                char c = n[i];
                if (c == '-' || c == '_')
                {
                    cap = true;
                    continue;
                }
                sb.Append(cap ? char.ToUpperInvariant(c) : c);
                cap = false;
            }
            return sb.ToString();
        }

        static bool IsFenceModel(string fbxPath)
        {
            string n = Path.GetFileNameWithoutExtension(fbxPath);
            return n == "fence" || n == "fence-gate";
        }

        static string EnsureEnvPrefab(string fbxPath, bool rebuild = false)
        {
            if (string.IsNullOrEmpty(fbxPath) || !fbxPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                return fbxPath;
            EnsureEnvFolder();
            string prefabPath = EnvPrefabPath(fbxPath);
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null && !rebuild)
                return prefabPath;
            return CreateEnvPrefab(fbxPath, prefabPath);
        }

        static string CreateEnvPrefab(string fbxPath, string prefabPath)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (model == null)
            {
                Debug.LogWarning("[Ulon] Env prefab skipped, missing fbx: " + fbxPath);
                return null;
            }
            var root = new GameObject(EnvPrefabName(fbxPath));
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = Vector3.one;
            if (IsFenceModel(fbxPath))
                BakeFenceUpright(visual);
            SnapVisualFeet(root, visual);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                AssetDatabase.DeleteAsset(prefabPath);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefabPath;
        }

        static void BakeFenceUpright(GameObject visual)
        {
            // Kenney fence.fbx is already Y-up post-and-rail (run along Z, ~0.38m tall).
            // Do not stand the 1m run on end — that made palisade stakes.
            Quaternion[] cands =
            {
                Quaternion.identity,
                Quaternion.Euler(0f, 90f, 0f),
                Quaternion.Euler(-90f, 0f, 0f),
                Quaternion.Euler(90f, 0f, 0f)
            };
            Quaternion best = cands[0];
            Vector3 bestSize = Vector3.zero;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < cands.Length; i++)
            {
                visual.transform.localRotation = cands[i];
                Vector3 size = CombinedBounds(visual).size;
                float score = FenceScore(size, cands[i]);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = cands[i];
                    bestSize = size;
                }
            }
            visual.transform.localRotation = best;
            Vector3 e = best.eulerAngles;
            Debug.Log("[Ulon] Fence Visual bake localEuler=(" +
                      e.x.ToString("0.##") + "," + e.y.ToString("0.##") + "," + e.z.ToString("0.##") +
                      ") bounds=(" + bestSize.x.ToString("0.###") + "," + bestSize.y.ToString("0.###") + "," +
                      bestSize.z.ToString("0.###") + ") score=" + bestScore.ToString("0.##"));
        }

        static float FenceScore(Vector3 size, Quaternion rot)
        {
            float run = Mathf.Max(size.x, size.z);
            float thick = Mathf.Min(size.x, size.z);
            float score = run * 40f - Mathf.Abs(size.y - 0.38f) * 25f;
            if (size.y < 0.2f)
                score -= 120f;
            if (size.y > run * 0.9f)
                score -= 120f;
            if (run > 0.7f && size.y < run * 0.7f)
                score += 80f;
            if (thick < 0.2f)
                score += 10f;
            Vector3 e = rot.eulerAngles;
            if (Mathf.Abs(Mathf.DeltaAngle(e.x, 0f)) < 1f && Mathf.Abs(Mathf.DeltaAngle(e.z, 0f)) < 1f)
                score += 30f;
            else
                score -= 40f;
            return score;
        }

        static Bounds CombinedBounds(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>(true);
            bool any = false;
            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            for (int i = 0; i < rends.Length; i++)
            {
                if (!any)
                {
                    b = rends[i].bounds;
                    any = true;
                }
                else
                    b.Encapsulate(rends[i].bounds);
            }
            if (!any)
            {
                var filters = go.GetComponentsInChildren<MeshFilter>(true);
                for (int i = 0; i < filters.Length; i++)
                {
                    if (filters[i].sharedMesh == null)
                        continue;
                    Bounds mb = filters[i].sharedMesh.bounds;
                    Vector3 worldCenter = filters[i].transform.TransformPoint(mb.center);
                    Vector3 worldSize = Vector3.Scale(mb.size, filters[i].transform.lossyScale);
                    var wb = new Bounds(worldCenter, worldSize);
                    if (!any)
                    {
                        b = wb;
                        any = true;
                    }
                    else
                        b.Encapsulate(wb);
                }
            }
            return b;
        }

        static void SnapVisualFeet(GameObject root, GameObject visual)
        {
            Bounds b = CombinedBounds(root);
            Vector3 lp = visual.transform.localPosition;
            lp.y += -b.min.y;
            if (root.name == "Fence" || root.name == "FenceGate")
            {
                lp.x += -(b.center.x - root.transform.position.x);
                lp.z += -(b.center.z - root.transform.position.z);
            }
            visual.transform.localPosition = lp;
        }

        static Transform FindMeshChildToNameVisual(Transform root)
        {
            Transform best = null;
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (c.GetComponentInChildren<SkinnedMeshRenderer>(true) == null
                    && c.GetComponentInChildren<MeshRenderer>(true) == null
                    && c.GetComponentInChildren<Animator>(true) == null)
                    continue;
                if (best == null)
                    best = c;
            }
            if (best != null)
                best.name = "Visual";
            return best;
        }

        static void EnsureNamedVisualsAndController()
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (ctrl == null)
                return;
            string[] names =
            {
                "Player", "Companion", "Bandit", FieldBoss.Object
            };
            for (int i = 0; i < names.Length; i++)
            {
                var go = GameObject.Find(names[i]);
                if (go == null)
                    continue;
                StripAndAssign(go, ctrl);
            }
        }

        static void RelinkKayKitInScene()
        {
            BuildKayKitPrefabs();
            string[] names =
            {
                "Player", "Companion", "Skeleton", "Bandit", "Raider", "Rogue", "Knight",
                "Acolyte", "Minion", "SkelRogue", "Trainer",
                FieldBoss.Object, Dungeon1.MobObject, Dungeon1.BossObject,
                Dungeon2.MobObject, Dungeon2.BossObject
            };
            for (int i = 0; i < names.Length; i++)
            {
                var go = GameObject.Find(names[i]);
                if (go == null)
                    continue;
                RelinkKayKitVisual(go);
                RelinkKayKitGear(go);
            }
        }

        static void RelinkKayKitVisual(GameObject actor)
        {
            Transform visual = actor.transform.Find("Visual");
            if (visual == null)
                return;
            string src = PrefabSourcePath(visual.gameObject);
            if (string.IsNullOrEmpty(src) || src.IndexOf("/KayKit/", StringComparison.OrdinalIgnoreCase) < 0)
                return;
            if (src.IndexOf("/RAW/", StringComparison.OrdinalIgnoreCase) < 0 && src.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                return;
            string fbx = src.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) ? src : FindKayKitFbxFromPrefab(src);
            if (string.IsNullOrEmpty(fbx))
                return;
            var anim = visual.GetComponentInChildren<Animator>(true);
            RuntimeAnimatorController ctrl = anim != null ? anim.runtimeAnimatorController : AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            var cc = actor.GetComponent<CharacterController>();
            float height = cc != null ? cc.height : 1.8f;
            UnityEngine.Object.DestroyImmediate(visual.gameObject);
            string prefabPath = EnsureKayKitPrefab(fbx, true);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                return;
            var nv = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            nv.name = "Visual";
            nv.transform.SetParent(actor.transform, false);
            FitHeight(nv.transform, actor.transform, height);
            var na = nv.GetComponentInChildren<Animator>(true);
            if (na == null)
                na = nv.AddComponent<Animator>();
            if (na.avatar == null)
                na.avatar = AvatarFor(actor.name);
            na.runtimeAnimatorController = ctrl;
            na.applyRootMotion = false;
            na.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var skins = nv.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int s = 0; s < skins.Length; s++)
                skins[s].updateWhenOffscreen = true;
            var sockets = actor.GetComponent<EquipmentSockets>();
            if (sockets != null)
                sockets.Bind(nv.transform);
        }

        static void RelinkKayKitGear(GameObject actor)
        {
            var sockets = actor.GetComponent<EquipmentSockets>();
            Transform[] nodes = actor.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i] == null)
                    continue;
                string src = PrefabSourcePath(nodes[i].gameObject);
                if (string.IsNullOrEmpty(src) || src.IndexOf("/KayKit/", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (!src.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (src.IndexOf("/Weapons/", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                Transform parent = nodes[i].parent;
                Vector3 lp = nodes[i].localPosition;
                Quaternion lr = nodes[i].localRotation;
                string n = nodes[i].name;
                UnityEngine.Object.DestroyImmediate(nodes[i].gameObject);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnsureKayKitPrefab(src, true));
                if (prefab == null)
                    continue;
                var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                item.name = n;
                item.transform.SetParent(parent, false);
                item.transform.localPosition = lp;
                item.transform.localRotation = lr;
            }
        }

        static string PrefabSourcePath(GameObject go)
        {
            var src = PrefabUtility.GetCorrespondingObjectFromOriginalSource(go);
            if (src == null)
                src = PrefabUtility.GetCorrespondingObjectFromSource(go);
            if (src == null)
                return null;
            return AssetDatabase.GetAssetPath(src);
        }

        static string FindKayKitFbxFromPrefab(string prefabPath)
        {
            string n = Path.GetFileNameWithoutExtension(prefabPath);
            string[] all =
            {
                KnightFbx, BarbarianFbx, MageFbx, RogueFbx,
                SkeletonFbx, SkeletonMageFbx, SkeletonMinionFbx, SkeletonRogueFbx,
                SwordFbx, ShieldFbx
            };
            for (int i = 0; i < all.Length; i++)
            {
                if (EnvPrefabName(all[i]) == n)
                    return all[i];
            }
            return null;
        }

        static void AssertNoFenceRing()
        {
            int nSide = 0, sSide = 0, eSide = 0, wSide = 0;
            var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                string n = all[i].name;
                if (n != "fence" && n != "Fence")
                    continue;
                Vector3 p = all[i].position;
                bool onX = Mathf.Abs(Mathf.Abs(p.x) - 15f) < 1.15f && Mathf.Abs(p.z) < 16.2f;
                bool onZ = Mathf.Abs(Mathf.Abs(p.z) - 15f) < 1.15f && Mathf.Abs(p.x) < 16.2f;
                if (onX && p.x < 0f) wSide++;
                else if (onX && p.x > 0f) eSide++;
                if (onZ && p.z < 0f) sSide++;
                else if (onZ && p.z > 0f) nSide++;
            }
            if (nSide >= 6 && sSide >= 6 && eSide >= 6 && wSide >= 6)
                throw new InvalidOperationException("r=15 prison-square leftover n=" + nSide + " s=" + sSide + " e=" + eSide + " w=" + wSide);
        }

        const string CharPrefabFolder = "Assets/Game/Prefabs/Characters";
        const string WeaponPrefabFolder = "Assets/Game/Prefabs/Weapons";

        [MenuItem("Ulon/Build KayKit Prefabs")]
        public static void BuildKayKitPrefabs()
        {
            EnsureKayKitFolders();
            string[] src =
            {
                KnightFbx, BarbarianFbx, MageFbx, RogueFbx,
                SkeletonFbx, SkeletonMageFbx, SkeletonMinionFbx, SkeletonRogueFbx,
                SwordFbx, ShieldFbx
            };
            int n = 0;
            for (int i = 0; i < src.Length; i++)
            {
                if (src[i].IndexOf("/Characters/", StringComparison.Ordinal) >= 0)
                    ConfigureHumanoid(src[i], true);
                else
                    ConfigureProp(src[i]);
                string created = EnsureKayKitPrefab(src[i], true);
                if (!string.IsNullOrEmpty(created))
                    n++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Ulon] KayKit prefabs built: " + n + " under Prefabs/Characters and Prefabs/Weapons");
        }

        static void EnsureKayKitFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Game/Prefabs"))
                AssetDatabase.CreateFolder("Assets/Game", "Prefabs");
            if (!AssetDatabase.IsValidFolder(CharPrefabFolder))
                AssetDatabase.CreateFolder("Assets/Game/Prefabs", "Characters");
            if (!AssetDatabase.IsValidFolder(WeaponPrefabFolder))
                AssetDatabase.CreateFolder("Assets/Game/Prefabs", "Weapons");
        }

        static bool IsKayKitWeapon(string fbxPath)
        {
            return fbxPath.IndexOf("/Weapons/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static string KayKitPrefabPath(string fbxPath)
        {
            string folder = IsKayKitWeapon(fbxPath) ? WeaponPrefabFolder : CharPrefabFolder;
            return folder + "/" + EnvPrefabName(fbxPath) + ".prefab";
        }

        static string EnsureKayKitPrefab(string fbxPath, bool rebuild = false)
        {
            if (string.IsNullOrEmpty(fbxPath))
                return fbxPath;
            if (!fbxPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                return fbxPath;
            if (fbxPath.IndexOf("/KayKit/", StringComparison.OrdinalIgnoreCase) < 0)
                return fbxPath;
            EnsureKayKitFolders();
            string prefabPath = KayKitPrefabPath(fbxPath);
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null && !rebuild)
                return prefabPath;
            return CreateKayKitPrefab(fbxPath, prefabPath);
        }

        static string CreateKayKitPrefab(string fbxPath, string prefabPath)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (model == null)
            {
                Debug.LogWarning("[Ulon] KayKit prefab skipped, missing fbx: " + fbxPath);
                return null;
            }
            var root = new GameObject(EnvPrefabName(fbxPath));
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;
            var anim = visual.GetComponentInChildren<Animator>(true);
            if (anim == null && !IsKayKitWeapon(fbxPath))
            {
                anim = visual.AddComponent<Animator>();
                var src = model.GetComponent<Animator>();
                if (src != null)
                    anim.avatar = src.avatar;
            }
            SnapVisualFeet(root, visual);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                AssetDatabase.DeleteAsset(prefabPath);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefabPath;
        }


    }
}
