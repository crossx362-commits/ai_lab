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
        // Barbarian은 **맨몸 상반신**이라 잡몹·동료로 쓰지 않는다(검수 2026-09-06: 살색 덩어리로 읽힌다).
        // 자격은 `MobArt` 원장이 강제한다 — 여기서 지우기만 하면 다음 사람이 다시 넣는다.
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
            StripAndAssign(GameObject.Find(Dungeon3.BossObject), controller);
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

        /// <summary>
        /// 마을 밖 소품 재질(검수 2026-09-06 반려 A). Kenney FantasyTown 바위는 **흰 무텍스처**라
        /// 잔디 위에 스티로폼 덩어리로 보였다 — 산 도포와 같은 암석 계열로 칠하고, 광맥은 따로 구분한다.
        /// 씬을 다 만든 뒤에 부른다(나중에 놓인 소품까지 칠해야 한다).
        /// </summary>
        public static void EnsureWorldPropMaterials()
        {
            // 밝은 회색(0.30~0.56)은 햇빛에서 흰 스티로폼으로 보였다 — 산자락 암석 톤으로 낮춘다(실측 후 재조정).
            var rockMat = MakeNoiseMat("MountainRockProp", new Color(0.15f, 0.14f, 0.13f), new Color(0.34f, 0.31f, 0.27f));
            if (rockMat != null)
            {
                rockMat.SetFloat("_Glossiness", 0.08f);
                if (rockMat.HasProperty("_MainTex"))
                    rockMat.mainTextureScale = new Vector2(2.5f, 2.5f);
                EditorUtility.SetDirty(rockMat);
            }
            var veinMat = MakeNoiseMat("IronVeinRock", new Color(0.24f, 0.21f, 0.18f), new Color(0.62f, 0.38f, 0.16f));
            string[] rockFbx =
            {
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/rock-large.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/rock-wide.fbx",
                "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/rock-small.fbx",
            };
            // FBX 임포트 에셋에 재질을 꽂는 것은 **디스크에 남지 않는다**(다시 열면 흰 Kenney 재질로 돌아온다 —
            // 이번에 셀프체크는 「102개 도포」라고 찍는데 샷은 그대로 흰 바위였던 원인이다).
            // 실제 자산인 Env 프리팹에 꽂고 저장한다.
            for (int i = 0; i < rockFbx.Length; i++)
                AssignMatAsset(EnvPrefabPath(rockFbx[i]), rockMat);
            AssetDatabase.SaveAssets();
            int rocks = PaintSceneByName(rockMat, new[] { "rock-large", "rock-wide", "rock-small" });
            int veins = PaintSceneByName(veinMat, new[] { "MineVein1", "MineVein2", "MineVein3", "IronVein" });
            Debug.Log("[Ulon] 소품 재질 — 바위 렌더러 " + rocks + "개 암석 도포, 광맥 " + veins + "개 철광 도포");
        }

        static int PaintSceneByName(Material mat, string[] names)
        {
            int painted = 0;
            if (mat == null || names == null)
                return painted;
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
                painted++;
            }
            return painted;
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

        /// <summary>캐릭터 아트 판정을 Assert에서도 쓴다(같은 규칙을 두 벌 두면 갈라진다).</summary>
        public static bool IsCharacterArtPublic(Transform t) => IsCharacterArt(t);

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
            // 사냥터 — 모두 z=13.2에 세워 두니 **7종 일직선 진열**이었다(옛 반려). 깊이·간격·바라보는 방향을
            // 흩어 무리로 읽히게 한다. 좌표는 사냥 구역 안(마을 북쪽 z 10~17)에 남긴다.
            MoveNamed("Skeleton", new Vector3(0.6f, 0f, 13.6f), new Vector3(0f, 168f, 0f));
            EnsureHuntMobs();
            MoveNamed("Bandit", new Vector3(-2.6f, 0f, 11.4f), new Vector3(0f, 196f, 0f));
            MoveNamed("Raider", new Vector3(2.2f, 0f, 15.8f), new Vector3(0f, 152f, 0f));
            MoveNamed("Rogue", new Vector3(-4.6f, 0f, 14.9f), new Vector3(0f, 208f, 0f));
            MoveNamed("Knight", new Vector3(4.9f, 0f, 11.2f), new Vector3(0f, 174f, 0f));
            MoveNamed("Acolyte", new Vector3(6.8f, 0f, 15.4f), new Vector3(0f, 160f, 0f));
            MoveNamed("Minion", new Vector3(8.7f, 0f, 12.1f), new Vector3(0f, 186f, 0f));
            MoveNamed("SkelRogue", new Vector3(10.2f, 0f, 16.2f), new Vector3(0f, 150f, 0f));
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
            EnsureHuntMob("Raider", MobCatalog.Raider, KnightFbx, new Vector3(2.4f, 0f, 13.2f));
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
            DressMob(spawned);
            spawned.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        }

        static void BindMob(GameObject go, string mobId)
        {
            if (go == null)
                return;
            // WorldBody가 없으면 조용히 return 하던 자리다. 그러면 몹은 영원히 바인드되지
            // 않은 채 남고, 실패는 수백 줄 떨어진 셀프체크에서 "N번째 몬스터가 사냥 구역에
            // 있어야 합니다"로 터진다(Rogue·Knight가 실제로 이 상태였다). Ensure 계약대로
            // 없으면 붙인다 — 이 파일의 VendorStation/HealerStation과 같은 방식.
            var body = go.GetComponent<WorldBody>() ?? go.AddComponent<WorldBody>();
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
            BuildDungeonEntrance(parent, new Vector3(Dungeon1.EntranceX, 0f, Dungeon1.EntranceZ), 90f);
            RoomRubble(parent, new Vector3(Dungeon1.EntranceX - 2.9f, 0f, Dungeon1.EntranceZ - 2.6f), 0.8f);   // 흰 Kenney 바위는 돌벽과 재질이 붕 뜬다 — 던전 톤 잔해로, 문 앞이 아니라 옆으로.

            var interior = new GameObject(Dungeon1.InteriorObject);
            interior.transform.SetParent(parent, false);
            interior.transform.position = OnGround(new Vector3(Dungeon1.InteriorX, 0f, Dungeon1.InteriorZ));
            Transform room = interior.transform;
            BuildDungeonRoom(room, new Vector3(Dungeon1.InteriorX, 0f, Dungeon1.InteriorZ), Dungeon1.RoomHalf, Dungeon1.RoomHeight, "West");
            RoomRubble(room, new Vector3(Dungeon1.InteriorX + 3.4f, 0f, Dungeon1.InteriorZ + 3.4f), 0.9f);
            RoomRubble(room, new Vector3(Dungeon1.InteriorX - 2.6f, 0f, Dungeon1.InteriorZ - 2.8f), 0.6f);

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
            SinkIntoDungeon(parent, Dungeon1.ExitObject);
            StandOnRoomFloor(new Vector3(Dungeon1.InteriorX, 0f, Dungeon1.InteriorZ), Dungeon1.MobObject, Dungeon1.BossObject);
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
            BuildDungeonEntrance(parent, new Vector3(Dungeon2.EntranceX, 0f, Dungeon2.EntranceZ), -90f);
            RoomRubble(parent, new Vector3(Dungeon2.EntranceX + 2.9f, 0f, Dungeon2.EntranceZ - 2.6f), 0.8f);   // 흰 Kenney 바위는 돌벽과 재질이 붕 뜬다 — 던전 톤 잔해로, 문 앞이 아니라 옆으로.

            var interior = new GameObject(Dungeon2.InteriorObject);
            interior.transform.SetParent(parent, false);
            interior.transform.position = OnGround(new Vector3(Dungeon2.InteriorX, 0f, Dungeon2.InteriorZ));
            Transform room = interior.transform;
            BuildDungeonRoom(room, new Vector3(Dungeon2.InteriorX, 0f, Dungeon2.InteriorZ), Dungeon2.RoomHalf, Dungeon2.RoomHeight, "East");
            RoomRubble(room, new Vector3(Dungeon2.InteriorX + 3.4f, 0f, Dungeon2.InteriorZ + 3.4f), 0.9f);
            RoomRubble(room, new Vector3(Dungeon2.InteriorX - 2.6f, 0f, Dungeon2.InteriorZ - 2.8f), 0.6f);

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
            SinkIntoDungeon(parent, Dungeon2.ExitObject);
            StandOnRoomFloor(new Vector3(Dungeon2.InteriorX, 0f, Dungeon2.InteriorZ), Dungeon2.MobObject, Dungeon2.BossObject);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }

        // 던전2와 같은 구성이되 보스는 아직 없다 — 전용 캐릭터 에셋이 오면 얹는다.
        public static void EnsureDungeon3()
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

            var old = GameObject.Find(Dungeon3.RootObject);
            if (old != null)
                UnityEngine.Object.DestroyImmediate(old);
            var stray = GameObject.Find(Dungeon3.MobObject);
            if (stray != null)
                UnityEngine.Object.DestroyImmediate(stray);
            var strayBoss = GameObject.Find(Dungeon3.BossObject);
            if (strayBoss != null)
                UnityEngine.Object.DestroyImmediate(strayBoss);

            var root = new GameObject(Dungeon3.RootObject);
            Transform parent = root.transform;

            var entrance = Place(Arch, new Vector3(Dungeon3.EntranceX, 0f, Dungeon3.EntranceZ), new Vector3(0f, 45f, 0f));
            if (entrance == null)
                entrance = Place(RockW, new Vector3(Dungeon3.EntranceX, 0f, Dungeon3.EntranceZ), Vector3.zero);
            if (entrance == null)
                throw new InvalidOperationException("던전 3 입구 모델 없음");
            entrance.name = Dungeon3.EntranceObject;
            entrance.transform.SetParent(parent, true);
            var eg = entrance.GetComponent<DungeonGate>() ?? entrance.AddComponent<DungeonGate>();
            eg.DungeonId = Dungeon3.Id;
            eg.IsExit = false;
            eg.DisplayName = "던전 3 입구";
            EnsureCollider(entrance);
            BuildDungeonEntrance(parent, new Vector3(Dungeon3.EntranceX, 0f, Dungeon3.EntranceZ), 45f);
            RoomRubble(parent, new Vector3(Dungeon3.EntranceX + 2.9f, 0f, Dungeon3.EntranceZ - 2.6f), 0.8f);   // 흰 Kenney 바위는 돌벽과 재질이 붕 뜬다 — 던전 톤 잔해로, 문 앞이 아니라 옆으로.

            var interior = new GameObject(Dungeon3.InteriorObject);
            interior.transform.SetParent(parent, false);
            interior.transform.position = OnGround(new Vector3(Dungeon3.InteriorX, 0f, Dungeon3.InteriorZ));
            Transform room = interior.transform;
            BuildDungeonRoom(room, new Vector3(Dungeon3.InteriorX, 0f, Dungeon3.InteriorZ), Dungeon3.RoomHalf, Dungeon3.RoomHeight, "West");
            RoomRubble(room, new Vector3(Dungeon3.InteriorX + 3.4f, 0f, Dungeon3.InteriorZ + 3.4f), 0.9f);
            RoomRubble(room, new Vector3(Dungeon3.InteriorX - 2.6f, 0f, Dungeon3.InteriorZ - 2.8f), 0.6f);

            var exitGo = Place(Arch, new Vector3(Dungeon3.ExitX, 0f, Dungeon3.ExitZ), new Vector3(0f, 45f, 0f));
            if (exitGo == null)
                exitGo = Place(RockS, new Vector3(Dungeon3.ExitX, 0f, Dungeon3.ExitZ), Vector3.zero);
            if (exitGo == null)
                throw new InvalidOperationException("던전 3 출구 모델 없음");
            exitGo.name = Dungeon3.ExitObject;
            exitGo.transform.SetParent(parent, true);
            var xg = exitGo.GetComponent<DungeonGate>() ?? exitGo.AddComponent<DungeonGate>();
            xg.DungeonId = Dungeon3.Id;
            xg.IsExit = true;
            xg.DisplayName = "던전 3 출구";
            EnsureCollider(exitGo);

            EnsureDungeon3Signpost(parent);
            EnsureDungeon3Mob(parent);
            EnsureDungeon3Boss(parent);
            SinkIntoDungeon(parent, Dungeon3.ExitObject);
            StandOnRoomFloor(new Vector3(Dungeon3.InteriorX, 0f, Dungeon3.InteriorZ), Dungeon3.MobObject, Dungeon3.BossObject);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }

        static void EnsureDungeon3Mob(Transform parent)
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (ctrl == null)
                return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(KnightFbx) == null)
                return;
            ConfigureHumanoid(KnightFbx, true);
            var spawned = SpawnActor(
                Dungeon3.MobObject,
                KnightFbx,
                new Vector3(Dungeon3.MobX, 0f, Dungeon3.MobZ),
                MobCatalog.HeightOf(MobCatalog.Raider),
                ctrl,
                false,
                true,
                MobCatalog.DisplayNameOf(MobCatalog.Raider),
                MobCatalog.MaxHpOf(MobCatalog.Raider));
            BindMob(spawned, MobCatalog.Raider);
            HideExtraGear(spawned);
            DressMob(spawned);
            if (parent != null)
                spawned.transform.SetParent(parent, true);
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
            DressBoss(spawned, new Color(0.35f, 1f, 0.6f));       // 독기 어린 녹빛 — 헥사크
            HideExtraGear(spawned);
            DressMob(spawned);
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
            DressMob(spawned);
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
            DressBoss(spawned, new Color(0.55f, 0.85f, 1f));      // 창백한 푸른빛 — 언데드 수문장
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
            DressMob(spawned);
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
            DressBoss(spawned, new Color(0.65f, 0.35f, 1f));      // 보랏빛 — 그림자
            if (parent != null)
                spawned.transform.SetParent(parent, true);
        }

        // 전용 캐릭터 에셋이 오기 전까지 KayKit Knight를 쓴다. 키 2.60/HP 210으로
        // 다른 보스 셋과 구분되며, 에셋이 오면 이 FBX만 갈아끼우면 된다.
        static void EnsureDungeon3Boss(Transform parent)
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (ctrl == null)
                return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(KnightFbx) == null)
                return;
            ConfigureHumanoid(KnightFbx, true);
            var spawned = SpawnActor(
                Dungeon3.BossObject,
                KnightFbx,
                new Vector3(Dungeon3.BossX, 0f, Dungeon3.BossZ),
                MobCatalog.HeightOf(MobCatalog.IronTyrant),
                ctrl,
                false,
                true,
                MobCatalog.DisplayNameOf(MobCatalog.IronTyrant),
                MobCatalog.MaxHpOf(MobCatalog.IronTyrant));
            BindMob(spawned, MobCatalog.IronTyrant);
            HideExtraGear(spawned);
            DressBoss(spawned, new Color(1f, 0.45f, 0.2f));       // 달군 쇳빛 — 강철폭군
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
            EnsureWorldAtmosphere();
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
            // 실내 차폐 페이드도 씬에 박아 둔다 — 런타임에만 붙이면 에디터 검증이 못 본다(검수 2026-09-06 P0).
            if (cam.GetComponent<DungeonSightFade>() == null)
                cam.gameObject.AddComponent<DungeonSightFade>();
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
            return MakeNoiseMat(name, a, b, 0);
        }

        /// <summary>pattern 0 = 잔풀 잡음, 1 = 굵은 층리(암석). 풀과 바위가 같은 무늬면 산이 두 색으로만 읽힌다(검수).</summary>
        static Material MakeNoiseMat(string name, Color a, Color b, int pattern)
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
                    float t;
                    if (pattern == 1)
                    {
                        // 굵은 덩어리 + 사선 층리 — 잔풀 잡음과 눈에 띄게 다른 무늬.
                        int bx = x / 6, by = y / 6;
                        int hb = ((bx * 92837) ^ (by * 68927) ^ ((bx + by) * 15731)) & 255;
                        int band = ((x + y * 3) / 9 * 47) & 255;
                        int grit = (x * 61 + y * 149) & 63;
                        t = (hb / 255f) * 0.52f + (band / 255f) * 0.28f + (grit / 63f) * 0.20f;
                    }
                    else if (pattern == 2)
                    {
                        // 이랑 — 갈아엎은 밭. 줄무늬가 있어야 「경작지」로 읽힌다.
                        int furrow = ((y / 5) % 2) * 70;
                        int grain = (x * 53 + y * 17) & 63;
                        t = Mathf.Clamp01((furrow + grain) / 133f);
                    }
                    else
                    {
                        int h = (x * 374761 + y * 668265 + x * y * 13) & 255;
                        int h2 = (x * 127 + y * 311) & 255;
                        t = (h / 255f) * 0.65f + (h2 / 255f) * 0.35f;
                    }
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

        /// <summary>프리팹 에셋에 재질을 꽂고 **디스크에 기록**한다(SetDirty/SavePrefabAsset 없이는 사라진다).</summary>
        static void AssignMatAsset(string prefabPath, Material mat)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
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
            EditorUtility.SetDirty(go);
            PrefabUtility.SavePrefabAsset(go);
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

        /// <summary>
        /// 던전 방 하나를 실내로 세운다 — 바닥·벽 링(문 한 칸)·모서리 기둥·천장·등불.
        /// 던전 1·2·3이 이 함수 하나를 호출한다(같은 코드를 세 번 쓰지 않는다).
        /// 검수 2026-09-06 P0-1: 옛 내부는 잔디밭에 바위 몇 개라 하늘이 그대로 보였다.
        /// </summary>
        /// <summary>
        /// 던전 입구를 "문"으로 읽히게 꾸민다(검수 2026-09-06 P0-2).
        /// 옛 입구는 wall-arch 한 장 + 흰 바위라 길가 돌무더기로 보였다.
        /// 아치 양옆 등불(점광)·붉은 깃발·마을 쪽에서 이어지는 돌길 타일을 공용으로 세운다.
        /// approachYaw는 플레이어가 걸어오는 방향(도(度))이다.
        /// </summary>
        /// <summary>
        /// 입구 앞에 남아 있는 흰 Kenney 바위를 씬에서 치운다 — Ensure*는 오브젝트가 있으면 일찍 반환하므로
        /// 코드에서 배치를 지워도 이미 만들어진 씬에는 그대로 남는다(검수 2026-09-06 반려: 던전 1 문구멍을 가렸다).
        /// </summary>
        public static void EnsureEntranceClearance()
        {
            ClearRocksNear(new Vector3(Dungeon1.EntranceX, 0f, Dungeon1.EntranceZ));
            ClearRocksNear(new Vector3(Dungeon2.EntranceX, 0f, Dungeon2.EntranceZ));
            ClearRocksNear(new Vector3(Dungeon3.EntranceX, 0f, Dungeon3.EntranceZ));
        }

        static void ClearRocksNear(Vector3 pos)
        {
            var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = all.Length - 1; i >= 0; i--)
            {
                var t = all[i];
                if (t == null || t.parent == null)
                    continue;
                if (t.name.IndexOf("rock", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                var p = t.position;
                if ((new Vector2(p.x - pos.x, p.z - pos.z)).magnitude > 4f)
                    continue;
                UnityEngine.Object.DestroyImmediate(t.gameObject);
            }
        }

        public static void BuildDungeonEntrance(Transform parent, Vector3 pos, float approachYaw)
        {
            const string Lantern = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/lantern.fbx";
            const string Banner = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/banner-red.fbx";
            const string PathTile = "Assets/_ThirdParty/Kenney/Nature/RAW/Models/ground_pathTile.fbx";
            string[] models = { Lantern, Banner, PathTile };
            for (int i = 0; i < models.Length; i++)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(models[i]) == null)
                    ConfigureProp(models[i]);
            }

            float rad = approachYaw * Mathf.Deg2Rad;
            var fwd = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));   // 입구가 바라보는 쪽(=접근로)
            var right = new Vector3(fwd.z, 0f, -fwd.x);

            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 flank = pos + right * (1.7f * side);
                Decor(parent, Lantern, flank + fwd * 1.3f, new Vector3(0f, approachYaw, 0f));
                var bannerGo = Place(Banner, flank + fwd * 0.75f + Vector3.up * 1.6f, new Vector3(0f, approachYaw, 0f));
                if (bannerGo != null)
                {
                    bannerGo.transform.SetParent(parent, true);
                    bannerGo.transform.localScale = bannerGo.transform.localScale * 1.8f;   // 얇은 판때기로 보이던 것을 키운다
                }
                var lightGo = new GameObject("DungeonEntranceLight");
                lightGo.transform.SetParent(parent, true);
                lightGo.transform.position = OnGround(flank) + Vector3.up * 2.2f;
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.72f, 0.42f);
                light.intensity = 3.2f;
                light.range = 9f;
                light.shadows = LightShadows.None;
            }

            for (int i = 1; i <= 4; i++)
                Decor(parent, PathTile, pos + fwd * (1.6f * i), new Vector3(0f, approachYaw, 0f));

            // 문틀 — wall-arch 한 장은 45° 시점에서 얇은 판때기로 보인다. 기둥 2 + 상인방 + 어두운 문구멍으로 문을 만든다.
            var wallMat = MakeNoiseMat("DungeonWall", new Color(0.16f, 0.15f, 0.17f), new Color(0.27f, 0.26f, 0.28f));
            var portalMat = MakeNoiseMat("DungeonPortal", new Color(0.03f, 0.03f, 0.05f), new Color(0.08f, 0.07f, 0.10f));
            var frame = new GameObject("DungeonEntranceFrame");
            frame.transform.SetParent(parent, true);
            float gy = OnGround(pos).y;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 pillar = pos + right * (1.15f * side);
                RoomSlab(frame.transform, "EntrancePillar", new Vector3(pillar.x, gy + 1.5f, pillar.z), new Vector3(0.7f, 3f, 0.7f), wallMat, approachYaw);
            }
            RoomSlab(frame.transform, "EntranceLintel", new Vector3(pos.x, gy + 3.15f, pos.z), new Vector3(3.0f, 0.6f, 1.1f), wallMat, approachYaw);
            // 문구멍은 기둥 **뒤**에 얇게 눕혀 문틀 안쪽으로 들어가 보이게 한다(검수: 앞에 선 검은 판으로 보였다).
            Vector3 back = pos - fwd * 0.55f;
            RoomSlab(frame.transform, "EntrancePortal", new Vector3(back.x, gy + 1.25f, back.z), new Vector3(1.5f, 2.5f, 0.12f), portalMat, approachYaw);
            // 옆벽 — 문틀이 벽에 뚫린 문으로 읽히게 좌우로 조금 이어 붙인다.
            for (int side = -1; side <= 1; side += 2)
            {
                // 기둥과 같은 평면·같은 두께·비슷한 높이로 — 어긋나면 계단처럼 보인다(검수 반려).
                Vector3 wing = pos + right * (2.35f * side);
                RoomSlab(frame.transform, "EntranceWing", new Vector3(wing.x, gy + 1.45f, wing.z), new Vector3(1.6f, 2.9f, 0.7f), wallMat, approachYaw);
            }
        }

        /// <summary>마을에서 던전 3이 보이도록 세우는 이정표(검수 P0-2 「우연히라도 찾을 단서가 없다」).</summary>
        public static void EnsureDungeon3Signpost(Transform parent)
        {
            const string Poles = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/poles.fbx";
            const string Banner = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/banner-red.fbx";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Poles) == null)
                ConfigureProp(Poles);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Banner) == null)
                ConfigureProp(Banner);
            var pos = new Vector3(Dungeon3.SignX, 0f, Dungeon3.SignZ);
            var go = Place(Poles, pos, new Vector3(0f, 225f, 0f));
            if (go == null)
                return;
            go.name = Dungeon3.SignObject;
            go.transform.SetParent(parent, true);
            Decor(parent, Banner, pos + new Vector3(0.6f, 0f, 0.6f), new Vector3(0f, 225f, 0f));
        }

        /// <summary>
        /// 기획서 §10.2 「보스는 일반 모델을 1.3~1.5배 확대하고 **머리장식·큰 무기·VFX·전용 기술**로 차별화한다」.
        /// 크기만으로는 같은 FBX를 쓰는 잡몹과 구분이 안 된다(검수 2026-09-06 반려 2).
        /// 새 에셋 없이 기존 파츠·스케일·조명으로 세 가지를 붙인다: 왕관(머리장식)·무기 확대·오라(VFX).
        /// </summary>
        public const string BossCrownObject = "BossCrown";
        public const string BossAuraObject = "BossAura";
        public const string BossWeaponPrefix = "BossWeapon_";

        /// <summary>
        /// 이미 씬에 있는 보스 4종에도 §10.2 차별화를 다시 적용한다(멱등).
        /// 스폰 Ensure*는 오브젝트가 있으면 일찍 반환하므로, 그 경로로만 두면 옛 보스는 영영 안 고쳐진다
        /// (헥사크가 무기 없이 남아 있던 이유다 — 검수 2026-09-06 반려).
        /// </summary>
        /// <summary>
        /// 이미 씬에 저장된 **잡몹**도 다시 꾸민다 — 스폰 Ensure*는 오브젝트가 있으면 일찍 반환하므로
        /// 드레싱을 스폰 경로에만 두면 옛 몹은 영영 술잔을 든 채 남는다(보스에서 이미 겪은 함정).
        /// </summary>
        /// <summary>사냥터 잡몹의 자리 — 좌표 원장(빌더·게이트 공용). z를 다 같이 두면 「일직선 진열」이 된다.</summary>
        public static readonly (string Name, float X, float Z, float Yaw)[] HuntSpots =
        {
            ("Skeleton", 0.6f, 13.6f, 168f),
            ("Bandit", -2.6f, 11.4f, 196f),
            ("Raider", 2.2f, 15.8f, 152f),
            ("Rogue", -4.6f, 14.9f, 208f),
            ("Knight", 4.9f, 11.2f, 174f),
            ("Acolyte", 6.8f, 15.4f, 160f),
            ("Minion", 8.7f, 12.1f, 186f),
            ("SkelRogue", 10.2f, 16.2f, 150f),
        };

        /// <summary>
        /// 사냥터 잡몹을 흩고 **지표에 세운다**. 두 가지가 겹쳐 있었다:
        ///   1) 전부 z=13.2 한 줄(옛 「7종 일직선 진열」 반려의 잔존),
        ///   2) 지형을 LandBase 10m로 올린 뒤에도 y=0에 남아 **땅속에 묻혀** 있었다 —
        ///      03_hunt_mobs 샷에 몹이 2마리만 보인 진짜 이유다(검수 2026-09-06 관찰).
        /// 배치 Ensure*는 오브젝트가 있으면 일찍 반환하므로 이 보수 패스가 따로 필요하다.
        /// </summary>
        public static void EnsureHuntMobPlacement()
        {
            int moved = 0;
            for (int i = 0; i < HuntSpots.Length; i++)
            {
                var spot = HuntSpots[i];
                var go = GameObject.Find(spot.Name);
                if (go == null)
                    continue;
                float y = GroundHeightAt(spot.X, spot.Z);
                var want = new Vector3(spot.X, y, spot.Z);
                if ((go.transform.position - want).sqrMagnitude > 0.0001f)
                    moved++;
                go.transform.position = want;
                go.transform.rotation = Quaternion.Euler(0f, spot.Yaw, 0f);
            }
            Debug.Log("[Ulon] 사냥터 배치 — " + HuntSpots.Length + "체 흩기·지표 세우기(옮긴 것 " + moved + "체)");
        }

        /// <summary>이 좌표의 지형 표면 높이(월드).</summary>
        public static float GroundHeightAt(float x, float z)
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null)
                return 0f;
            return terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
        }

        public static void EnsureMobDressing()
        {
            int n = 0;
            var bodies = UnityEngine.Object.FindObjectsByType<WorldBody>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < bodies.Length; i++)
            {
                var b = bodies[i];
                if (b == null || !b.IsEnemy)
                    continue;
                if (b.name == Dungeon1.BossObject || b.name == Dungeon2.BossObject || b.name == Dungeon3.BossObject || b.name == FieldBoss.Object)
                    continue;                                  // 보스는 DressBoss가 맡는다
                DressMob(b.gameObject);
                n++;
            }
            Debug.Log("[Ulon] 잡몹 드레싱 — " + n + "체(손 소품 제거·무기 1개·의상 켜기)");
        }

        public static void EnsureBossDressing()
        {
            DressBossNamed(Dungeon1.BossObject, new Color(0.55f, 0.85f, 1f));
            DressBossNamed(Dungeon2.BossObject, new Color(0.65f, 0.35f, 1f));
            DressBossNamed(Dungeon3.BossObject, new Color(1f, 0.45f, 0.2f));
            DressBossNamed(FieldBoss.Object, new Color(0.35f, 1f, 0.6f));
        }

        static void DressBossNamed(string objectName, Color tint)
        {
            var go = GameObject.Find(objectName);
            if (go != null)
                DressBoss(go, tint);
        }

        /// <summary>손에 드는 소품(무기 아님) — 이게 켜져 있고 무기가 꺼져 있으면 몹이 술잔을 들고 서 있다.</summary>
        public static bool IsHandPropNamePublic(string n) => IsHandPropName(n);

        static bool IsHandPropName(string n)
        {
            return n.IndexOf("Mug", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Bottle", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Cup", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Bread", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Torch", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// 잡몹 드레싱 — 보스만 꾸미고 잡몹은 FBX 기본 상태로 뒀더니 던전 3 야만인이 **술잔을 든 맨몸**으로
        /// 서 있었다(검수 2026-09-06 반려: 「왕관 쓴 보스 옆에 벗은 마네킹」). 하는 일:
        ///   1) 손 소품(잔·병 등)을 끄고 **무기 하나**를 켠다(1몹 1무기).
        ///   2) 모델이 가진 의상 메시(망토·모자·갑옷·후드)를 켠다 — 맨몸으로 두면 §8.1 실루엣이 안 읽힌다.
        /// </summary>
        public static void DressMob(GameObject actor)
        {
            if (actor == null)
                return;
            var all = actor.GetComponentsInChildren<Transform>(true);

            // 1) 무기 — 가장 큰 것 하나만 켜고 나머지 무기·손 소품은 끈다.
            Transform pick = null;
            float bestLen = -1f;
            for (int i = 0; i < all.Length; i++)
            {
                if (!IsWeaponName(all[i].name))
                    continue;
                var mf = all[i].GetComponentsInChildren<MeshFilter>(true);
                float len = 0f;
                for (int m = 0; m < mf.Length; m++)
                    if (mf[m].sharedMesh != null)
                        len = Mathf.Max(len, mf[m].sharedMesh.bounds.size.magnitude);
                if (len > bestLen)
                {
                    bestLen = len;
                    pick = all[i];
                }
            }
            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (IsHandPropName(t.name))
                {
                    t.gameObject.SetActive(false);
                    continue;
                }
                if (!IsWeaponName(t.name))
                    continue;
                bool keep = pick != null && (t == pick || t.IsChildOf(pick) || pick.IsChildOf(t));
                t.gameObject.SetActive(keep);
                if (keep)
                {
                    var rs = t.GetComponentsInChildren<Renderer>(true);
                    for (int r = 0; r < rs.Length; r++)
                        rs[r].enabled = true;
                }
            }
            if (pick != null)
                for (var t = pick; t != null && t != actor.transform; t = t.parent)
                    t.gameObject.SetActive(true);

            // 2) 의상 — 모델이 가진 것을 켠다.
            for (int i = 0; i < all.Length; i++)
            {
                if (!IsClothingName(all[i].name))
                    continue;
                all[i].gameObject.SetActive(true);
                var rs = all[i].GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < rs.Length; r++)
                    rs[r].enabled = true;
            }
        }

        /// <summary>의상 메시 이름인가(게이트도 같은 판정을 쓴다).</summary>
        public static bool IsClothingName(string n)
        {
            return n.IndexOf("Cape", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Cloak", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Hood", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Hat", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Helmet", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Armor", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Tunic", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static void DressBoss(GameObject boss, Color tint)
        {
            if (boss == null)
                return;

            // 멱등하게 — 이전에 붙은 왕관·오라는 지우고 다시 만든다(위치 규칙이 바뀌면 옛 것이 남는다).
            var old = boss.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < old.Length; i++)
            {
                if (old[i] == null)
                    continue;
                if (old[i].name == BossCrownObject || old[i].name == BossAuraObject)
                    UnityEngine.Object.DestroyImmediate(old[i].gameObject);
            }

            // 1) 큰 무기 — 손에 든 장비를 키운다. 없으면 손 소켓에 검을 새로 붙인다.
            //    개명만 하고 손에 아무것도 없는 보스가 게이트를 통과했다(검수 2026-09-06 반려).
            var weapon = FindGearTransform(boss);
            if (weapon == null)
            {
                AttachGear(boss, SwordFbx, null);
                weapon = FindGearTransform(boss);
            }
            if (weapon == null)
                weapon = AttachWeaponToHand(boss);   // EquipmentSockets가 없는 보스(헥사크)용 폴백
            if (weapon != null)
            {
                // 화면에 보이는 것이 목적이다 — 개명만 되고 꺼져 있거나 부모가 꺼진 무기가 있었다(헥사크의 완드).
                for (var t = weapon; t != null && t != boss.transform; t = t.parent)
                    t.gameObject.SetActive(true);
                var wrends = weapon.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < wrends.Length; i++)
                {
                    wrends[i].enabled = true;
                    wrends[i].gameObject.SetActive(true);
                }
                if (!weapon.name.StartsWith(BossWeaponPrefix, StringComparison.Ordinal))
                {
                    weapon.localScale = weapon.localScale * 1.55f;
                    weapon.name = BossWeaponPrefix + weapon.name;
                }
                // **손에 매단다.** 장비 소켓에 붙어 있던 무기는 키우고 나면 얼굴 옆에 떠 있는 판때기로 보였다
                // (검수 2026-09-06 관찰). 게이트도 「존재」가 아니라 손과의 거리로 잰다.
                var handBone = FindHandBone(boss);
                if (handBone != null && weapon.parent != handBone)
                {
                    var keepScale = weapon.localScale;
                    weapon.SetParent(handBone, false);
                    weapon.localPosition = Vector3.zero;
                    weapon.localRotation = Quaternion.identity;
                    weapon.localScale = keepScale;
                }

                // 완드처럼 작은 장비는 1.55배로도 「큰 무기」로 안 읽힌다 — 몸 높이의 0.45배까지 키운다.
                var ccw = boss.GetComponent<CharacterController>();
                float wantLen = (ccw != null ? ccw.height : 2f) * 0.45f;
                Bounds wb;
                if (BoundsOfEnabled(weapon, out wb))
                {
                    float len = Mathf.Max(wb.size.x, Mathf.Max(wb.size.y, wb.size.z));
                    if (len > 0.01f && len < wantLen)
                        weapon.localScale = weapon.localScale * Mathf.Min(3.5f, wantLen / len);
                }
                // 무기 덩어리 한가운데를 손에 맞추면 **칼이 얼굴 옆에 가로로 뜬다**(검수 2026-09-06 반려 2):
                // 손 안에 들어와야 하는 건 무기 전체가 아니라 **그립 끝**이고, 칼날은 팔뚝 방향으로 뻗어야 한다.
                if (handBone != null && BossFit.WeaponAxis(weapon, out Vector3 grip, out Vector3 tip))
                {
                    var along = tip - grip;
                    var fore = BossFit.ForearmDir(handBone, boss.transform);
                    if (along.sqrMagnitude > 1e-6f)
                        weapon.rotation = Quaternion.FromToRotation(along.normalized, fore) * weapon.rotation;
                    if (BossFit.WeaponAxis(weapon, out grip, out tip))
                        weapon.position += handBone.position - grip;
                }
            }

            // 1-b) **1몹 1무기**(P1 #7) — 보스 드레싱이 큰 무기를 새로 붙이면 원래 들고 있던 칼이
            //      같은 손에 그대로 남는다(기사 보스: 1H_Sword + BossWeapon_2H_Sword가 둘 다 켜져 있었다).
            //      HideExtraGear는 "1H_Sword"를 keep 목록에 두므로 이 경로를 못 잡는다.
            if (weapon != null)
            {
                var gears = boss.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < gears.Length; i++)
                {
                    var g = gears[i];
                    if (g == null || !IsWeaponName(g.name))
                        continue;
                    if (g == weapon || g.IsChildOf(weapon) || weapon.IsChildOf(g))
                        continue;
                    g.gameObject.SetActive(false);
                }
            }

            // 2) 머리장식 — 머리 **위**에 얹는다.
            //    렌더러 바운드(b.max.y)로 얹었더니 어깨 높이에 수평으로 떠서 접시처럼 보였다
            //    (에디터에서 스킨드 바운드는 못 믿는다 — 실루엣 게이트에서 이미 겪은 함정이다).
            //    CharacterController가 스폰 시 원장 키를 그대로 받으므로 그걸 머리 기준으로 쓴다.
            var crownMat = MakeNoiseMat("BossCrown", new Color(0.62f, 0.48f, 0.12f), new Color(0.86f, 0.72f, 0.26f));
            Bounds b;
            bool hasBounds = BoundsOf(boss.transform, true, out b);
            var cc = boss.GetComponent<CharacterController>();
            float headY;
            float headR;
            Vector3 axis;
            if (cc != null && cc.height > 0.01f)
            {
                axis = boss.transform.position + cc.center;
                headY = boss.transform.position.y + cc.center.y + cc.height * 0.5f;
                headR = Mathf.Max(0.16f, cc.radius * 0.62f);
            }
            else
            {
                if (!hasBounds)
                    return;
                axis = b.center;
                headY = b.max.y;
                headR = Mathf.Max(0.16f, b.size.x * 0.22f);
            }
            // CharacterController 캡슐 꼭대기는 **머리카락보다 한 뼘 위**다 — 그 위에 얹으면 관이 공중에 뜬다
            // (검수 2026-09-06 반려 1). 실제 정점(스킨드 베이크)의 정수리·머리 폭으로 다시 잡는다.
            if (BossFit.HeadMetrics(boss, out float meshTopY, out float headW, out Vector3 headC))
            {
                headY = meshTopY;
                axis = new Vector3(headC.x, axis.y, headC.z);
                headR = headW * 0.46f;      // 테 조각·뿔 기울기까지 더한 실측 지름이 머리 폭 1.26배(게이트 상한 1.4배)
            }
            var crown = new GameObject(BossCrownObject);
            crown.transform.SetParent(boss.transform, true);
            // 테 아랫면(pos.y + 0.04 − 0.05)이 정수리에 살짝 파묻히게 — 떠 있으면 얹힌 것으로 안 읽힌다.
            crown.transform.position = new Vector3(axis.x, headY - 0.02f, axis.z);
            // 테(밴드)가 없으면 머리카락 위로 뿔 두 개만 삐죽 나와 「왕관」으로 안 읽힌다(검수 관찰).
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f * Mathf.Deg2Rad;
                var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                seg.name = "CrownBand" + i;
                seg.transform.SetParent(crown.transform, true);
                seg.transform.position = crown.transform.position + new Vector3(Mathf.Sin(a) * headR, 0.04f, Mathf.Cos(a) * headR);
                seg.transform.rotation = Quaternion.Euler(0f, i * 30f, 0f);
                seg.transform.localScale = new Vector3(headR * 0.62f, 0.10f, 0.07f);
                var segRend = seg.GetComponent<Renderer>();
                if (segRend != null)
                    segRend.sharedMaterial = crownMat;
                var segCol = seg.GetComponent<Collider>();
                if (segCol != null)
                    UnityEngine.Object.DestroyImmediate(segCol);
            }
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f * Mathf.Deg2Rad;
                var spike = GameObject.CreatePrimitive(PrimitiveType.Cube);
                spike.name = "CrownSpike" + i;
                spike.transform.SetParent(crown.transform, true);
                spike.transform.position = crown.transform.position + new Vector3(Mathf.Sin(a) * headR, 0.20f, Mathf.Cos(a) * headR);
                spike.transform.localScale = new Vector3(0.11f, 0.40f, 0.11f);
                spike.transform.rotation = Quaternion.Euler(Mathf.Cos(a) * 14f, 0f, -Mathf.Sin(a) * 14f);
                var rend = spike.GetComponent<Renderer>();
                if (rend != null)
                    rend.sharedMaterial = crownMat;
                var col = spike.GetComponent<Collider>();
                if (col != null)
                    UnityEngine.Object.DestroyImmediate(col);
            }

            // 3) VFX — 보스 색 오라(점광). 멀리서도 색으로 읽힌다(§8.1 실루엣/가독성).
            var auraGo = new GameObject(BossAuraObject);
            auraGo.transform.SetParent(boss.transform, true);
            auraGo.transform.position = new Vector3(axis.x, boss.transform.position.y + (headY - boss.transform.position.y) * 0.55f, axis.z);
            var aura = auraGo.AddComponent<Light>();
            aura.type = LightType.Point;
            aura.color = tint;
            aura.intensity = 3.4f;
            aura.range = 6.5f;
            aura.shadows = LightShadows.None;
        }

        public const string DungeonBlockerLayer = "DungeonBlocker";

        /// <summary>던전 방 깊이 — 원장은 `WorldTerrain.DungeonDepth`다(실내 줌 상한이 여기서 유도된다).</summary>
        public const float DungeonDepth = WorldTerrain.DungeonDepth;
        public const string WaterObject = "SeaWater";

        /// <summary>씬의 플레이 카메라에 실내 차폐 페이드를 보장한다(검수 2026-09-06 P0).</summary>
        public static void EnsureCameraSightFade()
        {
            var cams = UnityEngine.Object.FindObjectsByType<QuarterViewCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < cams.Length; i++)
            {
                if (cams[i].GetComponent<DungeonSightFade>() == null)
                    cams[i].gameObject.AddComponent<DungeonSightFade>();
            }
        }

        /// <summary>
        /// 방 자리의 지형에 구멍을 뚫는다. 지하 방을 만들어도 Terrain 표면이 그대로 남아 있으면
        /// 페이드로 뚜껑이 걷힐 때 방이 아니라 **잔디가** 보인다(실측으로 확인, 검수 P0 마무리).
        /// </summary>
        static void PunchTerrainHole(Vector3 center, float half)
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null || terrain.terrainData == null)
                return;
            var data = terrain.terrainData;
            int res = data.holesResolution;
            Vector3 size = data.size;
            Vector3 origin = terrain.transform.position;
            float pad = 2.5f;   // 벽 바깥면·해상도 반올림까지 덮어야 방 안에 잔디 조각이 안 남는다
            int x0 = Mathf.Clamp(Mathf.FloorToInt((center.x - half - pad - origin.x) / size.x * res), 0, res - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((center.x + half + pad - origin.x) / size.x * res), 0, res - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((center.z - half - pad - origin.z) / size.z * res), 0, res - 1);
            int z1 = Mathf.Clamp(Mathf.CeilToInt((center.z + half + pad - origin.z) / size.z * res), 0, res - 1);
            int w = x1 - x0 + 1;
            int h = z1 - z0 + 1;
            if (w <= 0 || h <= 0)
                return;
            var holes = new bool[h, w];   // false = 구멍
            data.SetHoles(x0, z0, holes);
            EditorUtility.SetDirty(data);
        }

        // 뚜껑 윗면은 지표 아래로, 아랫면(=방 천장)은 예전 높이 그대로 — 두께로 맞춘다.
        // 천장이 내려오면 보스가 천장에 닿고 실내 화면 판정이 흔들린다.
        // 뚜껑 윗면은 지표 아래로, 아랫면(=방 천장)은 예전 높이 그대로 — 두께로 맞춘다.
        // 천장이 내려오면 보스가 천장에 닿고 실내 화면 판정이 흔들린다.
        public const float CapTopBelowGround = 0.60f;
        public const float CapBottomBelowGround = 2.10f;

        /// <summary>
        /// 암반 뚜껑 6×6 타일. 한 장이면 통째로 사라져 다시 잔디가 보이므로 격자로 깐다.
        /// **윗면 높이는 타일마다 그 자리의 지면에서 잰다** — 방 중심 한 곳만 재면 경사에서 뚜껑이 지표로 솟아
        /// 조망에 회색 판으로 찍힌다(검수 반려 B의 잔재가 그랬다). 아랫면은 방 천장이라 고정한다.
        /// </summary>
        static void BuildCapTiles(Transform room, Vector3 center, float span, Material ceilMat)
        {
            float centerGround = OnGround(new Vector3(center.x, 0f, center.z)).y;
            float bottom = centerGround - CapBottomBelowGround;
            int capTiles = 6;
            float capSpan = span + 16f;
            float tile = capSpan / capTiles;
            for (int cx = 0; cx < capTiles; cx++)
            {
                for (int cz = 0; cz < capTiles; cz++)
                {
                    float px = center.x - capSpan * 0.5f + tile * (cx + 0.5f);
                    float pz = center.z - capSpan * 0.5f + tile * (cz + 0.5f);
                    float top = OnGround(new Vector3(px, 0f, pz)).y - CapTopBelowGround;
                    float thick = Mathf.Max(0.8f, top - bottom);
                    RoomSlab(room, "DungeonCap", new Vector3(px, top - thick * 0.5f, pz),
                        new Vector3(tile * 1.02f, thick, tile * 1.02f), ceilMat);
                }
            }
        }

        /// <summary>
        /// 방 크기(`Dungeon*.RoomHalf`)가 바뀌면 **이미 지어진 방은 안 따라온다** — `EnsureDungeon*`이
        /// 실내 오브젝트가 있으면 일찍 반환하기 때문이다. 벽 실측 반경이 원장 값과 다르면 방 구조물만
        /// 지우고 다시 짓는다(몹·보스·소품은 이름이 달라 남는다).
        /// </summary>
        /// <summary>
        /// 씬에 이미 놓인 배치물을 **그 자리 지표**로 끌어올린다(검수 2026-09-06 A).
        /// 지형 높이를 올린 랩 이후 마을 건물·상인·장식이 지표 10m 아래에 묻혀 있었다 — 화면에서 마을이 사라졌다.
        /// 대상·바운드·기대 높이는 게이트와 같은 `GroundFit`을 쓴다.
        /// </summary>
        /// <summary>
        /// 자격 없는 모델을 쓰는 몹은 **지우고 다시 짓게** 한다 — `Ensure*`는 오브젝트가 있으면 일찍 반환하므로
        /// 모델 상수를 바꿔도 이미 저장된 씬은 옛 모델 그대로다(검수 2026-09-06 야만인 교체에서 실측).
        /// 이 패스는 `EnsureHuntMobs`·`EnsureDungeon*`보다 **먼저** 돌아야 한다.
        /// </summary>
        public static void EnsureMobArtQualified()
        {
            var names = new List<string>();
            for (int i = 0; i < HuntSpots.Length; i++)
                names.Add(HuntSpots[i].Name);
            names.Add(Dungeon1.MobObject); names.Add(Dungeon2.MobObject); names.Add(Dungeon3.MobObject);
            names.Add("Companion");
            int removed = 0;
            for (int i = 0; i < names.Count; i++)
            {
                var go = GameObject.Find(names[i]);
                if (go == null)
                    continue;
                if (MobArt.ModelOf(go, out MobArt.Model model, out string _) && model.BodyReadsClothed)
                    continue;
                UnityEngine.Object.DestroyImmediate(go);
                removed++;
            }
            if (removed > 0)
                Debug.Log("[Ulon] 자격 없는 몹 모델 " + removed + "체 제거 — 다음 Ensure에서 등록 모델로 다시 짓는다");
        }

        public static void EnsureFootOnGround()
        {
            var items = GroundFit.Candidates();
            int moved = 0;
            float worst = 0f;
            string worstName = "";
            for (int i = 0; i < items.Count; i++)
            {
                if (!GroundFit.WorldBounds(items[i], out Bounds b))
                    continue;
                float dy = GroundFit.ExpectedGroundY(items[i], b) - b.min.y;
                if (Mathf.Abs(dy) < 0.02f)
                    continue;
                items[i].position += new Vector3(0f, dy, 0f);
                moved++;
                if (Mathf.Abs(dy) > Mathf.Abs(worst)) { worst = dy; worstName = items[i].name; }
            }
            if (moved > 0)
                Debug.Log("[Ulon] 배치물 지표 스냅 — " + moved + "개 이동(최대 " + worstName + " " + worst.ToString("0.00") + "m)");
        }

        public static void EnsureRoomSize()
        {
            var rooms = new[]
            {
                new { Obj = Dungeon1.InteriorObject, X = Dungeon1.InteriorX, Z = Dungeon1.InteriorZ, Half = Dungeon1.RoomHalf, H = Dungeon1.RoomHeight, Door = "West" },
                new { Obj = Dungeon2.InteriorObject, X = Dungeon2.InteriorX, Z = Dungeon2.InteriorZ, Half = Dungeon2.RoomHalf, H = Dungeon2.RoomHeight, Door = "East" },
                new { Obj = Dungeon3.InteriorObject, X = Dungeon3.InteriorX, Z = Dungeon3.InteriorZ, Half = Dungeon3.RoomHalf, H = Dungeon3.RoomHeight, Door = "West" },
            };
            for (int i = 0; i < rooms.Length; i++)
            {
                var go = GameObject.Find(rooms[i].Obj);
                if (go == null)
                    continue;
                var room = go.transform;
                var center = new Vector3(rooms[i].X, 0f, rooms[i].Z);

                // 실측 반경 = 벽 슬래브 중심의 최대 수평 편차. 문 쪽이 비어도 나머지 세 면이 있으므로 잰다.
                float measured = -1f;
                bool hasDoorBack = false;
                bool hasRockFill = false;
                float floorSpan = -1f;
                for (int c = 0; c < room.childCount; c++)
                {
                    var child = room.GetChild(c);
                    if (child.name == "DungeonWallDoorBack")
                        hasDoorBack = true;
                    if (child.name.StartsWith("DungeonRockFill", StringComparison.Ordinal))
                        hasRockFill = true;
                    if (child.name == "DungeonFloor")
                        floorSpan = child.localScale.x;
                    if (!child.name.StartsWith("DungeonWall", StringComparison.Ordinal) || child.name.StartsWith("DungeonWallDoor", StringComparison.Ordinal))
                        continue;
                    var p = child.position;
                    measured = Mathf.Max(measured, Mathf.Max(Mathf.Abs(p.x - center.x), Mathf.Abs(p.z - center.z)));
                }
                bool sizeOk = measured >= 0f && Mathf.Abs(measured - rooms[i].Half) < 0.05f;
                bool floorOk = Mathf.Abs(floorSpan - (rooms[i].Half * 2f + RockRingGap * 2f + 4f)) < 0.05f;
                if (sizeOk && hasDoorBack && hasRockFill && floorOk)
                    continue;

                int removed = 0;
                for (int c = room.childCount - 1; c >= 0; c--)
                {
                    var child = room.GetChild(c);
                    string n = child.name;
                    bool structural = n.StartsWith("Dungeon", StringComparison.Ordinal)
                        || n.StartsWith("CapDress", StringComparison.Ordinal)
                        || n.IndexOf("lantern", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!structural)
                        continue;
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                    removed++;
                }
                BuildDungeonRoom(room, center, rooms[i].Half, rooms[i].H, rooms[i].Door);
                Debug.Log("[Ulon] 던전 방 보수 — " + rooms[i].Obj + " 반경 " + measured.ToString("0.0") + "m → " +
                    rooms[i].Half.ToString("0.0") + "m, 문 밖 통로 " + (hasDoorBack ? "있음" : "없음→신설") +
                    "·바깥 암반 " + (hasRockFill ? "있음" : "없음→신설") + ", 구조물 " + removed + "개 재건");
            }
        }

        /// <summary>
        /// 이미 만들어진 씬을 고치는 멱등 보수 패스(검수 반려 B). `Ensure*`는 오브젝트가 있으면 일찍 반환하므로
        /// 코드 수정만으로는 디스크의 씬이 안 고쳐진다. 옛 뚜껑·뚜껑 장식을 지우고 새 높이로 다시 깐다.
        /// </summary>
        public static void EnsureCapBuried()
        {
            RepairTerrainHoles();
            var ceilMat = MakeNoiseMat("DungeonCeiling", new Color(0.11f, 0.11f, 0.13f), new Color(0.18f, 0.17f, 0.20f));
            var rooms = new[]
            {
                new { Obj = Dungeon1.InteriorObject, X = Dungeon1.InteriorX, Z = Dungeon1.InteriorZ, Half = Dungeon1.RoomHalf },
                new { Obj = Dungeon2.InteriorObject, X = Dungeon2.InteriorX, Z = Dungeon2.InteriorZ, Half = Dungeon2.RoomHalf },
                new { Obj = Dungeon3.InteriorObject, X = Dungeon3.InteriorX, Z = Dungeon3.InteriorZ, Half = Dungeon3.RoomHalf },
            };
            int removed = 0;
            for (int i = 0; i < rooms.Length; i++)
            {
                var go = GameObject.Find(rooms[i].Obj);
                if (go == null)
                    continue;
                var room = go.transform;
                for (int c = room.childCount - 1; c >= 0; c--)
                {
                    var child = room.GetChild(c);
                    if (child.name == "DungeonCap" || child.name.StartsWith("CapDress", StringComparison.Ordinal))
                    {
                        UnityEngine.Object.DestroyImmediate(child.gameObject);
                        removed++;
                    }
                }
                BuildCapTiles(room, new Vector3(rooms[i].X, 0f, rooms[i].Z), rooms[i].Half * 2f, ceilMat);
            }
            Debug.Log("[Ulon] 던전 뚜껑 매설 — 옛 타일 " + removed + "장 제거 후 타일마다 그 자리 지표 " + CapTopBelowGround + "m 아래로 재배치, Terrain 홀 복구");
        }

        /// <summary>뚫어 둔 Terrain 홀을 전부 메운다 — 홀은 에셋에 남으므로 코드에서 안 뚫는 것만으로는 안 사라진다.</summary>
        static void RepairTerrainHoles()
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null || terrain.terrainData == null)
                return;
            var data = terrain.terrainData;
            int res = data.holesResolution;
            var solid = new bool[res, res];
            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                    solid[z, x] = true;
            data.SetHoles(0, 0, solid);
            EditorUtility.SetDirty(data);
        }

        /// <summary>방 벽에서 바깥 암반 링까지의 거리 — 카메라 눈의 수평 오프셋(실내 줌 8.11m × cos35° = 6.6m)보다 넉넉히.</summary>
        public const float RockRingGap = 14f;

        public static void BuildDungeonRoom(Transform room, Vector3 center, float half, float wallH, string doorSide)
        {
            // 앰비언트가 야외 값(0.55)이라 알베도를 낮춰야 실내가 낮처럼 안 보인다(§8.2).
            var floorMat = MakeNoiseMat("DungeonFloor", new Color(0.13f, 0.12f, 0.14f), new Color(0.21f, 0.20f, 0.21f));
            var wallMat = MakeNoiseMat("DungeonWall", new Color(0.11f, 0.10f, 0.12f), new Color(0.19f, 0.18f, 0.20f));
            var ceilMat = MakeNoiseMat("DungeonCeiling", new Color(0.11f, 0.11f, 0.13f), new Color(0.18f, 0.17f, 0.20f));

            // 지하화(검수 2026-09-06 P0 마무리) — 지상에 상자를 얹으면 페이드가 걷힐 때 화면 절반이 잔디밭이 된다(§8.2).
            float ground = OnGround(new Vector3(center.x, 0f, center.z)).y;
            float y = ground - DungeonDepth;
            float span = half * 2f;
            float seg = span / 3f;
            float t = 0.5f;
            wallH = DungeonDepth + 0.15f;   // 바닥에서 지면까지 — 벽 너머로 잔디가 보이지 않게

            // Terrain 홀은 더 이상 뚫지 않는다(검수 2026-09-06 반려 B) — 구멍은 조망에서 **짙은 회색 직사각형**으로 읽혔다.
            // 실내 줌(§4.2) 이후 카메라가 방 안에 있어 뚜껑이 페이드로 걷히지 않으므로, 지표는 잔디 그대로 두고
            // 뚜껑을 지표 **아래로** 묻는다(하늘·주광 차단은 뚜껑이 계속 한다).
            RepairTerrainHoles();

            // 바닥은 Terrain 홀보다 넓어야 한다 — 좁으면 방 가장자리로 하늘이 비친다(플레이캠 실측).
            // 바닥은 방보다 훨씬 넓어야 한다 — 플레이어가 귀퉁이에 서면 카메라 눈이 벽 밖에 놓이고,
            // 화면 아래쪽 시선이 바닥 판 **바깥으로 떨어져** 검은 띠가 생긴다(검수 2026-09-06 B 실측: 아래 18%).
            float floorSpan = span + RockRingGap * 2f + 4f;
            RoomSlab(room, "DungeonFloor", new Vector3(center.x, y + 0.1f, center.z), new Vector3(floorSpan, 0.2f, floorSpan), floorMat);

            for (int side = 0; side < 4; side++)
            {
                for (int i = -1; i <= 1; i++)
                {
                    string name = side == 0 ? "North" : side == 1 ? "South" : side == 2 ? "East" : "West";
                    if (i == 0 && name == doorSide)
                        continue;
                    Vector3 pos;
                    Vector3 size;
                    float off = i * seg;
                    if (side == 0) { pos = new Vector3(center.x + off, y + wallH * 0.5f, center.z + half); size = new Vector3(seg, wallH, t); }
                    else if (side == 1) { pos = new Vector3(center.x + off, y + wallH * 0.5f, center.z - half); size = new Vector3(seg, wallH, t); }
                    else if (side == 2) { pos = new Vector3(center.x + half, y + wallH * 0.5f, center.z + off); size = new Vector3(t, wallH, seg); }
                    else { pos = new Vector3(center.x - half, y + wallH * 0.5f, center.z + off); size = new Vector3(t, wallH, seg); }
                    RoomSlab(room, "DungeonWall" + name + (i + 1), pos, size, wallMat);
                }
            }

            // 문 쪽 한 칸은 벽이 없다 — 그 틈으로 쏜 시선은 **아무것도 안 맞고** 화면에 검은 삼각형으로 남는다
            // (검수 2026-09-06: 8.11m 줌 오른쪽 아래 모서리). 문 밖에 짧은 통로를 이어 막는다.
            {
                Vector3 dir = doorSide == "North" ? Vector3.forward : doorSide == "South" ? Vector3.back
                    : doorSide == "East" ? Vector3.right : Vector3.left;
                Vector3 per = new Vector3(dir.z, 0f, dir.x);
                float len = 5f;
                Vector3 mouth = center + dir * half;
                Vector3 axis(float along, float side) => new Vector3(
                    mouth.x + dir.x * along + per.x * side, 0f, mouth.z + dir.z * along + per.z * side);
                Vector3 SizeOf(float along, float across) =>
                    new Vector3(Mathf.Abs(dir.x) * along + Mathf.Abs(per.x) * across, 0f, Mathf.Abs(dir.z) * along + Mathf.Abs(per.z) * across);

                var back = axis(len, 0f);
                var bs = SizeOf(t, seg + t * 2f); bs.y = wallH;
                RoomSlab(room, "DungeonWallDoorBack", new Vector3(back.x, y + wallH * 0.5f, back.z), bs, wallMat);
                for (int s = -1; s <= 1; s += 2)
                {
                    var sideP = axis(len * 0.5f, s * (seg * 0.5f + t * 0.5f));
                    var ss = SizeOf(len, t); ss.y = wallH;
                    RoomSlab(room, "DungeonWallDoorSide" + (s + 1), new Vector3(sideP.x, y + wallH * 0.5f, sideP.z), ss, wallMat);
                }
                var mid = axis(len * 0.5f, 0f);
                var fs = SizeOf(len, seg + t * 2f);
                RoomSlab(room, "DungeonFloorDoor", new Vector3(mid.x, y + 0.1f, mid.z), new Vector3(fs.x, 0.2f, fs.z), floorMat);
                RoomSlab(room, "DungeonCeilDoor", new Vector3(mid.x, y + wallH, mid.z), new Vector3(fs.x, 0.4f, fs.z), ceilMat);

                // **방 바깥도 실내여야 한다.** 플레이어가 귀퉁이에 서면 카메라 눈이 벽 밖(지하)에 놓인다 —
                // 거기에 아무것도 없으면 화면 아래가 통째로 검다(검수 2026-09-06 B, 실측 허공 0.18).
                // 방을 한 겹 더 둘러싸는 **바깥 암반 링**을 두른다. 눈이 어디에 서든 시선 끝에 돌이 있다.
                // (속을 채운 덩어리는 답이 아니다 — 덩어리 **안**에서 쏜 레이는 아무것도 못 맞는다.)
                float ringR = half + RockRingGap;
                float ringLen = ringR * 2f + t * 2f;
                RoomSlab(room, "DungeonRockFillNorth", new Vector3(center.x, y + wallH * 0.5f, center.z + ringR), new Vector3(ringLen, wallH, t), wallMat);
                RoomSlab(room, "DungeonRockFillSouth", new Vector3(center.x, y + wallH * 0.5f, center.z - ringR), new Vector3(ringLen, wallH, t), wallMat);
                RoomSlab(room, "DungeonRockFillEast", new Vector3(center.x + ringR, y + wallH * 0.5f, center.z), new Vector3(t, wallH, ringLen), wallMat);
                RoomSlab(room, "DungeonRockFillWest", new Vector3(center.x - ringR, y + wallH * 0.5f, center.z), new Vector3(t, wallH, ringLen), wallMat);
            }

            for (int c = 0; c < 4; c++)
            {
                float sx = (c == 0 || c == 3) ? 1f : -1f;
                float sz = (c == 0 || c == 1) ? 1f : -1f;
                RoomSlab(room, "DungeonPillar" + c,
                    new Vector3(center.x + half * sx, y + wallH * 0.5f + 0.15f, center.z + half * sz),
                    new Vector3(0.9f, wallH + 0.3f, 0.9f), wallMat);
            }

            // 천장 = 지면 높이의 암반 뚜껑. 한 장이면 페이드 때 통째로 사라져 다시 잔디가 보이므로
            // 6×6 타일 격자로 깔아 시선에 걸린 몇 장만 걷히게 한다(단면으로 읽힌다).
            BuildCapTiles(room, center, span, ceilMat);

            const string Lantern = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/lantern.fbx";
            var lanternA = Place(Lantern, new Vector3(center.x - half + 1.2f, 0f, center.z + half - 1.2f), Vector3.zero);
            var lanternB = Place(Lantern, new Vector3(center.x + half - 1.2f, 0f, center.z - half + 1.2f), Vector3.zero);
            if (lanternA != null) { lanternA.transform.SetParent(room, true); SinkIntoDungeon(lanternA); }
            if (lanternB != null) { lanternB.transform.SetParent(room, true); SinkIntoDungeon(lanternB); }
            RoomTorch(room, new Vector3(center.x - half + 1.2f, y + 2.1f, center.z + half - 1.2f), half);
            RoomTorch(room, new Vector3(center.x + half - 1.2f, y + 2.1f, center.z - half + 1.2f), half);
            // 방 전체를 등불 색으로 아주 약하게 받쳐준다 — 주광은 암반 뚜껑 그림자에 막히므로(AssertDungeonLighting)
            // 이 점광들이 실내의 유일한 광원이다. 세면 낮처럼 보이니 약하게.
            RoomFill(room, new Vector3(center.x, y + 2.6f, center.z), half);
        }

        /// <summary>지상 배치 로직(Place/SnapRootToGround)을 거친 오브젝트를 방 바닥 높이로 내린다.</summary>
        public static void SinkIntoDungeon(GameObject go)
        {
            if (go == null)
                return;
            go.transform.position -= new Vector3(0f, DungeonDepth, 0f);
        }

        public static void SinkIntoDungeon(Transform parent, params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                var go = GameObject.Find(names[i]);
                if (go != null)
                    SinkIntoDungeon(go);
            }
        }

        /// <summary>
        /// 방 안 오브젝트를 **방 바닥 윗면**에 세운다. 각자 자기 자리 지면에서 깊이만큼 내리면
        /// 지면 기복만큼 어긋나 바닥에 파묻히거나 뜬다(지형 작업 후 보스가 0.31m 파묻혔다).
        /// </summary>
        public static void StandOnRoomFloor(Vector3 roomCenter, params string[] names)
        {
            float floorTop = OnGround(roomCenter).y - DungeonDepth + 0.2f;
            for (int i = 0; i < names.Length; i++)
            {
                var go = GameObject.Find(names[i]);
                if (go == null)
                    continue;
                var p = go.transform.position;
                go.transform.position = new Vector3(p.x, floorTop, p.z);
            }
        }

        static void RoomSlab(Transform parent, string name, Vector3 center, Vector3 size, Material mat)
        {
            RoomSlab(parent, name, center, size, mat, 0f);
        }

        static void RoomSlab(Transform parent, string name, Vector3 center, Vector3 size, Material mat, float yaw)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            // 카메라와 플레이어 사이에 오면 렌더를 끄는 레이어(콜라이더는 남는다 — 하늘 차단·이동 막기는 유지).
            int blocker = LayerMask.NameToLayer(DungeonBlockerLayer);
            if (blocker >= 0)
                go.layer = blocker;
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = size;
            var rend = go.GetComponent<Renderer>();
            if (rend != null && mat != null)
                rend.sharedMaterial = mat;
        }

        /// <summary>던전 톤 잔해 — Kenney 흰 저폴리 바위는 회색 돌벽과 재질이 붕 뜬다(검수 P1).</summary>
        static void RoomRubble(Transform parent, Vector3 center, float scale)
        {
            var mat = MakeNoiseMat("DungeonWall", new Color(0.16f, 0.15f, 0.17f), new Color(0.27f, 0.26f, 0.28f));
            float y = OnGround(new Vector3(center.x, 0f, center.z)).y - DungeonDepth;
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f * Mathf.Deg2Rad;
                var pos = new Vector3(center.x + Mathf.Sin(a) * scale * 0.5f, y + 0.25f * scale, center.z + Mathf.Cos(a) * scale * 0.5f);
                RoomSlab(parent, "DungeonRubble", pos, new Vector3(0.7f * scale, 0.5f * scale, 0.7f * scale), mat, i * 37f);
            }
        }

        static void RoomTorch(Transform parent, Vector3 pos, float half)
        {
            var go = new GameObject("DungeonTorch");
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.78f, 0.48f);
            light.intensity = 2.4f;
            light.range = half * 2.6f;
            light.shadows = LightShadows.None;
        }

        /// <summary>실내 받침 광원 — 던전 분위기(따뜻한 저강도)로 방 전체를 약하게 채운다.</summary>
        static void RoomFill(Transform parent, Vector3 pos, float half)
        {
            var go = new GameObject("DungeonFill");
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.95f, 0.72f, 0.45f);
            light.intensity = 0.7f;
            light.range = half * 3.2f;
            light.shadows = LightShadows.None;
        }

        /// <summary>
        /// 기획서 §6.1 지역을 **실제 장소로** 만든다(검수 2026-09-06 반려 4: 「지역 이름만 있고 화면은 빈 초록」).
        /// 배치는 결정적이다 — 매번 흔들리면 검수가 같은 화면을 못 본다.
        /// </summary>
        public static void EnsureWorldRegions()
        {
            const string TreeH = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/tree-high.fbx";
            const string TreeR = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/tree-high-round.fbx";
            const string TreeC = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/tree-crooked.fbx";
            const string Tree = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/tree.fbx";
            const string Fence = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/fence.fbx";
            const string Cart = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/cart.fbx";
            const string RockL = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/rock-large.fbx";
            const string RockW = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/rock-wide.fbx";
            const string Poles = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/poles.fbx";
            const string Bush = "Assets/_ThirdParty/Kenney/Nature/RAW/Models/plant_bush.fbx";
            const string BushL = "Assets/_ThirdParty/Kenney/Nature/RAW/Models/plant_bushLarge.fbx";
            const string Tuft = "Assets/_ThirdParty/Kenney/Nature/RAW/Models/grass_large.fbx";
            const string Crop = "Assets/_ThirdParty/Kenney/Nature/RAW/Models/grass_leafs.fbx";
            string[] models = { TreeH, TreeR, TreeC, Tree, Fence, Cart, RockL, RockW, Poles, Bush, BushL, Tuft, Crop };
            for (int i = 0; i < models.Length; i++)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(models[i]) == null)
                    ConfigureProp(models[i]);
            }

            BuildMeadow(WorldRegions.Meadow, Fence, Crop, Tuft, Cart, Bush);
            BuildForest(WorldRegions.Forest, new[] { TreeH, TreeR, TreeC, Tree }, Bush, BushL, Tuft);
            BuildMine(WorldRegions.Mine, RockL, RockW, Poles, Tuft);
            ScatterPlain(new[] { Tuft, Bush, BushL, RockW });
            BuildTestChamber(WorldRegions.TestChamber, Fence, Poles, Cart, RockW);
        }

        /// <summary>
        /// §6.1 테스트 공간 — 표적·장비 거치대·연습 기둥을 둔 울타리 마당. GM 패널 워프로만 간다.
        /// 살아 있는 몹은 두지 않는다 — GM 패널의 스켈레톤 소환으로 그 자리에서 만들어 쓴다
        /// (상시 몹을 두면 몹 수를 세는 다른 판정들이 흔들린다).
        /// </summary>
        static void BuildTestChamber(WorldRegions.Region r, string fence, string poles, string cart, string rock)
        {
            const string Lantern = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/lantern.fbx";
            const string Banner = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/banner-red.fbx";
            const string Planks = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/planks.fbx";
            const string Stall = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/stall.fbx";
            string[] models = { fence, poles, cart, rock, Lantern, Banner, Planks, Stall };
            for (int i = 0; i < models.Length; i++)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(models[i]) == null)
                    ConfigureProp(models[i]);
            }

            var parent = FreshRegion(r);
            float half = 10f;
            FenceRun(parent, fence, new Vector2(r.X - half, r.Z - half), new Vector2(r.X + half, r.Z - half));
            FenceRun(parent, fence, new Vector2(r.X - half, r.Z + half), new Vector2(r.X + half, r.Z + half));
            FenceRun(parent, fence, new Vector2(r.X - half, r.Z - half), new Vector2(r.X - half, r.Z + half));
            FenceRun(parent, fence, new Vector2(r.X + half, r.Z - half), new Vector2(r.X + half, r.Z + half));

            // 표적 3개 — 스킬/무기 피해를 눈으로 보는 자리.
            for (int i = 0; i < 3; i++)
            {
                var pos = new Vector3(r.X - 4f + i * 4f, 0f, r.Z + 6f);
                var target = Place(poles, pos, new Vector3(0f, 0f, 0f));
                if (target == null)
                    continue;
                target.name = "TestTarget" + (i + 1);
                target.transform.SetParent(parent, true);
                Decor(parent, Planks, pos + new Vector3(0f, 0f, 0.6f), new Vector3(0f, 90f, 0f));
            }
            // 장비 거치대와 작업대.
            Decor(parent, Stall, new Vector3(r.X - 6f, 0f, r.Z - 5f), new Vector3(0f, 20f, 0f));
            Decor(parent, cart, new Vector3(r.X + 6f, 0f, r.Z - 5f), new Vector3(0f, -30f, 0f));
            Decor(parent, Banner, new Vector3(r.X, 0f, r.Z - half + 0.6f), new Vector3(0f, 180f, 0f));
            for (int i = 0; i < 4; i++)
            {
                float sx = (i == 0 || i == 3) ? 1f : -1f;
                float sz = (i == 0 || i == 1) ? 1f : -1f;
                Decor(parent, Lantern, new Vector3(r.X + (half - 1.2f) * sx, 0f, r.Z + (half - 1.2f) * sz), Vector3.zero);
                var lightGo = new GameObject("TestChamberLight");
                lightGo.transform.SetParent(parent, true);
                lightGo.transform.position = OnGround(new Vector3(r.X + (half - 1.2f) * sx, 2.2f, r.Z + (half - 1.2f) * sz));
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(0.98f, 0.85f, 0.6f);
                light.intensity = 1.1f;
                light.range = 9f;
                light.shadows = LightShadows.None;
            }
            for (int i = 0; i < 6; i++)
            {
                float a = WorldRegions.Rand(i, 61, 0f, 360f) * Mathf.Deg2Rad;
                Decor(parent, rock, new Vector3(r.X + Mathf.Cos(a) * (half + 3f), 0f, r.Z + Mathf.Sin(a) * (half + 3f)), Vector3.zero);
            }
        }


        static Transform FreshRegion(WorldRegions.Region region)
        {
            var old = GameObject.Find(region.Object);
            if (old != null)
                UnityEngine.Object.DestroyImmediate(old);
            var go = new GameObject(region.Object);
            return go.transform;
        }

        /// <summary>농경지 — 울타리로 두른 밭 네 뙈기와 작물 이랑.</summary>
        static void BuildMeadow(WorldRegions.Region r, string fence, string crop, string tuft, string cart, string bush)
        {
            var parent = FreshRegion(r);
            var plots = new[]
            {
                new Vector2(r.X - 11f, r.Z - 9f), new Vector2(r.X + 10f, r.Z - 10f),
                new Vector2(r.X - 10f, r.Z + 10f), new Vector2(r.X + 11f, r.Z + 9f),
            };
            for (int p = 0; p < plots.Length; p++)
            {
                float half = 6.5f;
                // 울타리는 **폭을 재서 이어 붙인다**. 간격을 추측해 띄우면 화면에서 「밭을 두른 울타리」가 아니라
                // 들판에 말뚝이 흩어진 것으로 읽힌다(검수 반려 4의 첫 샷이 그랬다).
                FenceRun(parent, fence, new Vector2(plots[p].x - half, plots[p].y - half), new Vector2(plots[p].x + half, plots[p].y - half));
                FenceRun(parent, fence, new Vector2(plots[p].x - half, plots[p].y + half), new Vector2(plots[p].x + half, plots[p].y + half));
                FenceRun(parent, fence, new Vector2(plots[p].x - half, plots[p].y - half), new Vector2(plots[p].x - half, plots[p].y + half));
                FenceRun(parent, fence, new Vector2(plots[p].x + half, plots[p].y - half), new Vector2(plots[p].x + half, plots[p].y + half));
                for (int row = 0; row < 8; row++)
                {
                    for (int col = 0; col < 8; col++)
                    {
                        var pos = new Vector3(plots[p].x - 5.25f + col * 1.5f, 0f, plots[p].y - 5.25f + row * 1.5f);
                        int seed = p * 131 + row * 11 + col;
                        var go = Place(crop, pos, new Vector3(0f, WorldRegions.Rand(seed, 1, 0f, 360f), 0f));
                        if (go == null)
                            continue;
                        go.transform.SetParent(parent, true);
                        // 작물 한 포기는 원래 무릎 아래다 — 조망에서 흙 얼룩으로 보여 이랑이 안 읽힌다. 키운다.
                        go.transform.localScale = go.transform.localScale * WorldRegions.Rand(seed, 2, 1.7f, 2.4f);
                    }
                }
            }
            Decor(parent, cart, new Vector3(r.X, 0f, r.Z), new Vector3(0f, 35f, 0f));
            for (int i = 0; i < 10; i++)
            {
                float a = WorldRegions.Rand(i, 2, 0f, 360f) * Mathf.Deg2Rad;
                float d = WorldRegions.Rand(i, 3, 8f, r.Radius);
                Decor(parent, i % 2 == 0 ? tuft : bush, new Vector3(r.X + Mathf.Cos(a) * d, 0f, r.Z + Mathf.Sin(a) * d), new Vector3(0f, a * Mathf.Rad2Deg, 0f));
            }
        }

        static float _fenceWidth = -1f;

        /// <summary>울타리 한 장의 실제 폭(모델 크기를 추측하지 않는다).</summary>
        static float FenceWidth(string path)
        {
            if (_fenceWidth > 0f)
                return _fenceWidth;
            var probe = Place(path, new Vector3(0f, 0f, 0f), Vector3.zero);
            if (probe == null)
                return 2f;
            _fenceWidth = Mathf.Max(0.5f, CombinedBounds(probe).size.x);
            UnityEngine.Object.DestroyImmediate(probe);
            return _fenceWidth;
        }

        /// <summary>두 점을 잇는 연속된 울타리 줄.</summary>
        static void FenceRun(Transform parent, string path, Vector2 from, Vector2 to)
        {
            Vector2 d = to - from;
            float len = d.magnitude;
            if (len < 0.5f)
                return;
            float w = FenceWidth(path);
            int count = Mathf.Max(2, Mathf.CeilToInt(len / w));
            float yaw = Mathf.Abs(d.x) >= Mathf.Abs(d.y) ? 0f : 90f;
            for (int i = 0; i < count; i++)
            {
                Vector2 p = from + d * ((i + 0.5f) / count);
                Decor(parent, path, new Vector3(p.x, 0f, p.y), new Vector3(0f, yaw, 0f));
            }
        }

        /// <summary>숲 — 나무 군락. 균일 격자로 심으면 과수원으로 보이니 군집을 이룬다.</summary>
        static void BuildForest(WorldRegions.Region r, string[] trees, string bush, string bushLarge, string tuft)
        {
            var parent = FreshRegion(r);
            int index = 0;
            // 군락 7개·나무 5~9그루로는 「나무가 좀 있는 들판」이다(첫 샷). 숲으로 읽히려면 수관이 서로 겹쳐야 한다.
            for (int cluster = 0; cluster < 14; cluster++)
            {
                float ca = WorldRegions.Rand(cluster, 11, 0f, 360f) * Mathf.Deg2Rad;
                float cd = WorldRegions.Rand(cluster, 12, 3f, r.Radius - 5f);
                var center = new Vector2(r.X + Mathf.Cos(ca) * cd, r.Z + Mathf.Sin(ca) * cd);
                int count = Mathf.RoundToInt(WorldRegions.Rand(cluster, 13, 9f, 15f));
                for (int i = 0; i < count; i++)
                {
                    index++;
                    float a = WorldRegions.Rand(index, 14, 0f, 360f) * Mathf.Deg2Rad;
                    float d = WorldRegions.Rand(index, 15, 0.6f, 5.2f);
                    var pos = new Vector3(center.x + Mathf.Cos(a) * d, 0f, center.y + Mathf.Sin(a) * d);
                    var go = Place(trees[index % trees.Length], pos, new Vector3(0f, WorldRegions.Rand(index, 16, 0f, 360f), 0f));
                    if (go == null)
                        continue;
                    go.transform.SetParent(parent, true);
                    float sc = WorldRegions.Rand(index, 17, 0.85f, 1.35f);
                    go.transform.localScale = go.transform.localScale * sc;
                }
                Decor(parent, cluster % 2 == 0 ? bush : bushLarge, new Vector3(center.x + 3.2f, 0f, center.y - 2.4f), new Vector3(0f, 40f, 0f));
                Decor(parent, tuft, new Vector3(center.x - 2.6f, 0f, center.y + 3.1f), new Vector3(0f, 120f, 0f));
            }
        }

        /// <summary>광산/산지 — 산자락 바위 노두와 채광 광맥.</summary>
        static void BuildMine(WorldRegions.Region r, string rockLarge, string rockWide, string poles, string tuft)
        {
            var parent = FreshRegion(r);
            // 각도를 완전 무작위로 뽑으면 한쪽에 몰린다(사분면 게이트가 2개로 잡아냈다) — 사분면을 돌아가며 놓는다.
            for (int i = 0; i < 28; i++)
            {
                float a = ((i % 4) * 90f + WorldRegions.Rand(i, 21, 5f, 85f)) * Mathf.Deg2Rad;
                float d = WorldRegions.Rand(i, 22, 3f, r.Radius - 3f);
                var pos = new Vector3(r.X + Mathf.Cos(a) * d, 0f, r.Z + Mathf.Sin(a) * d);
                var go = Place(i % 3 == 0 ? rockWide : rockLarge, pos, new Vector3(0f, WorldRegions.Rand(i, 23, 0f, 360f), 0f));
                if (go == null)
                    continue;
                go.transform.SetParent(parent, true);
                go.transform.localScale = go.transform.localScale * WorldRegions.Rand(i, 24, 0.9f, 2.1f);
            }
            // 갱구 표시 — 기둥 두 개와 널판(마을에서 「저기가 광산」으로 읽히게).
            Decor(parent, poles, new Vector3(r.X - 2.2f, 0f, r.Z - 1.4f), new Vector3(0f, 0f, 0f));
            Decor(parent, poles, new Vector3(r.X + 2.2f, 0f, r.Z - 1.4f), new Vector3(0f, 0f, 0f));
            for (int i = 0; i < 4; i++)
                Decor(parent, tuft, new Vector3(r.X + (i - 1.5f) * 3f, 0f, r.Z + 6f), new Vector3(0f, i * 40f, 0f));

            // 채광 광맥 3개 — 지역이 「장소」이려면 할 일이 있어야 한다(§6.1 광산은 채광 지역이다).
            for (int i = 0; i < 3; i++)
            {
                var pos = new Vector3(r.X + (i - 1) * 5.5f, 0f, r.Z + 2.4f);
                var vein = Place(rockLarge, pos, new Vector3(0f, 25f * i, 0f));
                if (vein == null)
                    continue;
                vein.name = "MineVein" + (i + 1);
                vein.transform.SetParent(parent, true);
                var node = vein.GetComponent<ResourceNode>() ?? vein.AddComponent<ResourceNode>();
                node.ResourceId = "iron_ore";
                node.DisplayName = "광산 철광맥";
                node.GatherSkill = SkillId.Mining;
                node.Remaining = 14;
                node.Capacity = 14;
                node.RespawnSeconds = 10f;
                node.Difficulty = 14f;
                EnsureCollider(vein);
            }
        }

        /// <summary>평지 산포 — 마을 밖 빈 초록을 깨는 잡초·덤불·돌. 지역·마을·던전 자리는 피한다.</summary>
        static void ScatterPlain(string[] props)
        {
            var old = GameObject.Find("PlainScatter");
            if (old != null)
                UnityEngine.Object.DestroyImmediate(old);
            var parent = new GameObject("PlainScatter").transform;
            var regions = WorldRegions.All;
            int placed = 0;
            for (int i = 0; i < 460; i++)
            {
                float x = WorldRegions.Rand(i, 31, -84f, 84f);
                float z = WorldRegions.Rand(i, 32, -84f, 84f);
                float distVillage = Mathf.Sqrt(x * x + z * z);
                if (distVillage < 26f)
                    continue;                                  // 마을·광장은 그대로 둔다
                bool inRegion = false;
                for (int k = 0; k < regions.Length; k++)
                {
                    if (new Vector2(x - regions[k].X, z - regions[k].Z).magnitude < regions[k].Radius)
                        inRegion = true;
                }
                if (inRegion)
                    continue;
                if (Mathf.Abs(Mathf.Abs(x) - 68f) < 16f && Mathf.Abs(Mathf.Abs(z) - 68f) < 16f)
                    continue;                                  // 던전 뚜껑 자리
                float h = WorldTerrain.HeightAt(x, z);
                if (h < WorldTerrain.SeaLevel + 1f || h > WorldTerrain.LandBase + 6f)
                    continue;                                  // 물가·산비탈은 제외
                var go = Place(props[i % props.Length], new Vector3(x, 0f, z), new Vector3(0f, WorldRegions.Rand(i, 33, 0f, 360f), 0f));
                if (go == null)
                    continue;
                go.transform.SetParent(parent, true);
                go.transform.localScale = go.transform.localScale * WorldRegions.Rand(i, 34, 0.8f, 1.6f);
                placed++;
            }
            Debug.Log("[Ulon] 평지 산포 " + placed + "개");
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
                fbx = KnightFbx;
            else if (rootName == "Skeleton" || rootName == Dungeon1.MobObject || rootName == Dungeon1.BossObject)
                fbx = SkeletonFbx;
            else if (rootName == "Bandit" || rootName == "Trainer" || rootName == Dungeon2.MobObject || rootName == FieldBoss.Object)
                fbx = MageFbx;
            else if (rootName == "Raider" || rootName == Dungeon3.MobObject)
                fbx = KnightFbx;
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
            var companion = SpawnActor("Companion", KnightFbx, new Vector3(-2.2f, 0f, 1.4f), 1.85f, controller, false, false, "동료", 50f);
            HideExtraGear(companion);
            var skeleton = SpawnActor("Skeleton", SkeletonFbx, new Vector3(5.2f, 0f, 3.6f), MobCatalog.HeightOf(MobCatalog.Skeleton), controller, false, true, MobCatalog.DisplayNameOf(MobCatalog.Skeleton), MobCatalog.MaxHpOf(MobCatalog.Skeleton));
            BindMob(skeleton, MobCatalog.Skeleton);
            HideExtraGear(skeleton);
            var bandit = SpawnActor("Bandit", MageFbx, new Vector3(7.4f, 0f, 3.6f), MobCatalog.HeightOf(MobCatalog.Bandit), controller, false, true, MobCatalog.DisplayNameOf(MobCatalog.Bandit), MobCatalog.MaxHpOf(MobCatalog.Bandit));
            BindMob(bandit, MobCatalog.Bandit);
            HideExtraGear(bandit);
            var raider = SpawnActor("Raider", KnightFbx, new Vector3(2.4f, 0f, 13.2f), MobCatalog.HeightOf(MobCatalog.Raider), controller, false, true, MobCatalog.DisplayNameOf(MobCatalog.Raider), MobCatalog.MaxHpOf(MobCatalog.Raider));
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
            DressBoss(hexarch, new Color(0.35f, 1f, 0.6f));       // 독기 어린 녹빛 — 헥사크(§10.2)

            var world = new GameObject("OfflineWorld");
            world.AddComponent<OfflineWorld>();
            world.AddComponent<SliceHud>();
            world.AddComponent<PersistDriver>();

            Camera cam = UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (cam != null)
            {
                var qv = cam.GetComponent<QuarterViewCamera>() ?? cam.gameObject.AddComponent<QuarterViewCamera>();
                // 실내 차폐 페이드도 씬에 박아 둔다 — 런타임에만 붙이면 에디터 검증이 못 본다(검수 2026-09-06 P0).
                if (cam.GetComponent<DungeonSightFade>() == null)
                    cam.gameObject.AddComponent<DungeonSightFade>();
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

        /// <summary>지형은 셀프체크에서도 다시 만든다 — 안 그러면 원장(WorldTerrain)을 고쳐도
        /// 씬에는 디스크의 옛 지형이 남아 Assert가 옛 값을 본다(2026-09-06 실측: 180m가 계속 잡혔다).</summary>
        /// <summary>
        /// 안개 범위는 월드 크기를 따라간다 — 42~115m로는 300m 월드에서 산·바다가 통째로 안개에 묻혀
        /// §8.1 「멀리서도 즉시 읽히는 실루엣」이 성립하지 않는다(2026-09-06 조망 샷 실측).
        /// </summary>
        public static void EnsureWorldAtmosphere()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.55f, 0.70f, 0.86f);
            RenderSettings.fogStartDistance = 90f;
            RenderSettings.fogEndDistance = WorldTerrain.Span * 1.6f;
        }

        public static void EnsureVillageTerrain()
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
            // 무채색 한 장으로 보이던 바위에 갈색기·명암 폭을 준다(§8.2).
            // 풀(잡음·타일 12)과 **다른 무늬·다른 타일링**이어야 산이 별개의 지질로 읽힌다(검수 재반려).
            var rockLayer = EnsureTerrainLayer("MountainRock", new Color(0.20f, 0.18f, 0.17f), new Color(0.63f, 0.58f, 0.50f), 7f, 1);
            var sandLayer = EnsureTerrainLayer("ShoreSand", new Color(0.74f, 0.68f, 0.50f), new Color(0.85f, 0.80f, 0.62f), 8f);
            // §6.1 지역이 **지표로** 구분돼야 한다 — 바닥이 전부 같은 초록이면 소품만 얹힌 모양이다(검수 2026-09-06 관찰).
            var tilledLayer = EnsureTerrainLayer("FarmTilled", new Color(0.30f, 0.21f, 0.13f), new Color(0.47f, 0.34f, 0.21f), 3.5f, 2);
            var soilLayer = EnsureTerrainLayer("ForestSoil", new Color(0.16f, 0.13f, 0.09f), new Color(0.30f, 0.25f, 0.16f), 9f);
            var gravelLayer = EnsureTerrainLayer("MineGravel", new Color(0.28f, 0.26f, 0.24f), new Color(0.55f, 0.52f, 0.47f), 4.5f, 1);
            var roadLayer = EnsureTerrainLayer("DirtRoad", new Color(0.38f, 0.31f, 0.22f), new Color(0.58f, 0.50f, 0.37f), 5f);

            int res = 513;
            data.heightmapResolution = res;
            data.size = new Vector3(WorldTerrain.Span, WorldTerrain.MaxHeight, WorldTerrain.Span);
            data.terrainLayers = new[] { layer, rockLayer, sandLayer, tilledLayer, soilLayer, gravelLayer, roadLayer };
            float[,] heights = new float[res, res];
            float half = WorldTerrain.Span * 0.5f;
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    float wx = (x / (float)(res - 1)) * WorldTerrain.Span - half;
                    float wz = (z / (float)(res - 1)) * WorldTerrain.Span - half;
                    heights[z, x] = WorldTerrain.HeightAt(wx, wz) / WorldTerrain.MaxHeight;
                }
            }
            data.SetHeights(0, 0, heights);
            int ar = data.alphamapResolution;
            var alpha = new float[ar, ar, WorldSplat.LayerCount];
            for (int z = 0; z < ar; z++)
            {
                for (int x = 0; x < ar; x++)
                {
                    float wx = (x / (float)(ar - 1)) * WorldTerrain.Span - half;
                    float wz = (z / (float)(ar - 1)) * WorldTerrain.Span - half;
                    float h = WorldTerrain.HeightAt(wx, wz);
                    // 경사도 — 가파른 곳이 바위다. 높이만 보면 산이 회색 한 장, 밑동이 칼로 자른 듯 끊긴다(§8.2).
                    float d = WorldTerrain.Span / (ar - 1);
                    float hx = WorldTerrain.HeightAt(wx + d, wz) - WorldTerrain.HeightAt(wx - d, wz);
                    float hz = WorldTerrain.HeightAt(wx, wz + d) - WorldTerrain.HeightAt(wx, wz - d);
                    float slope = Mathf.Sqrt(hx * hx + hz * hz) / (2f * d);

                    // 경계에 노이즈를 섞어 직선으로 끊기지 않게 한다.
                    float edgeNoise = (Mathf.PerlinNoise(wx * 0.09f + 17f, wz * 0.09f + 5f) - 0.5f) * 6f;
                    float rock = Mathf.Clamp01((h - (WorldTerrain.LandBase + 10f) + edgeNoise * 1.6f) / 16f);
                    rock = Mathf.Max(rock, Mathf.Clamp01((slope - 0.58f) * 1.6f));      // 급경사는 고도와 무관하게 바위
                    float mottle = Mathf.PerlinNoise(wx * 0.021f + 3.1f, wz * 0.021f + 8.9f);
                    rock = Mathf.Max(rock, Mathf.Clamp01((mottle - 0.5f) * 2.6f) * 0.55f);   // 평지 흙·바위 얼룩
                    // 산 중턱까지 풀이 올라간다 — 상한을 얼룩으로 흔들어 풀·바위가 섞이게 한다(중턱 풀 0.19 재반려).
                    if (h < WorldTerrain.LandBase + 22f)
                        rock = Mathf.Min(rock, 0.42f + mottle * 0.45f);

                    // 물가 — 수면 ±2m는 모래. 잔디가 물에 수직으로 잘리면 §8.2 위반이다.
                    float sand = 1f - Mathf.Clamp01((Mathf.Abs(h - WorldTerrain.SeaLevel) - 0.8f) / 2.0f);
                    if (h < WorldTerrain.SeaLevel)
                        sand = 1f;                                   // 물속 바닥도 모래
                    sand = Mathf.Max(sand, 0f);

                    float grassW = Mathf.Max(0f, 1f - sand) * Mathf.Max(0f, 1f - rock);
                    float rockW = Mathf.Max(0f, 1f - sand) * rock;

                    // 지역 지표·길 — 원장(WorldSplat)이 계산하고 Assert도 같은 함수를 읽는다.
                    int cover = WorldSplat.CoverAt(wx, wz, out float coverW);
                    coverW *= Mathf.Max(0f, 1f - sand) * Mathf.Max(0f, 1f - rock * 0.45f);
                    float keep = Mathf.Max(0f, 1f - coverW);

                    var w = new float[WorldSplat.LayerCount];
                    w[WorldSplat.Grass] = grassW * keep;
                    w[WorldSplat.Rock] = rockW * keep;
                    w[WorldSplat.Sand] = sand;
                    if (cover >= 0)
                        w[cover] += coverW;
                    float sum = 0.0001f;
                    for (int c = 0; c < WorldSplat.LayerCount; c++)
                        sum += w[c];
                    for (int c = 0; c < WorldSplat.LayerCount; c++)
                        alpha[z, x, c] = w[c] / sum;
                }
            }
            data.SetAlphamaps(0, 0, alpha);
            EditorUtility.SetDirty(data);
            // TerrainData는 에셋이다 — 저장하지 않으면 씬을 다시 열 때 디스크의 옛 지형이 돌아온다.
            AssetDatabase.SaveAssets();
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
            go.transform.position = new Vector3(-half, 0f, -half);
            terrain.heightmapPixelError = 5f;
            terrain.basemapDistance = 160f;
            terrain.shadowCastingMode = ShadowCastingMode.On;
            var col = go.GetComponent<TerrainCollider>();
            if (col != null)
                col.terrainData = data;

            EnsureWater();
            EnsureWorldAtmosphere();
        }

        /// <summary>
        /// 바다·강·호수는 같은 수면 하나로 만든다 — 지형이 SeaLevel 아래로 파인 곳에서만 물이 보인다.
        /// 단색 파란 판은 §8.2 위반이라 노이즈 텍스처 재질을 쓴다(Default-Material 프리미티브 금지).
        /// </summary>
        static void EnsureWater()
        {
            var mat = MakeNoiseMat("SeaWater", new Color(0.10f, 0.28f, 0.42f), new Color(0.18f, 0.44f, 0.58f));
            if (mat != null)
            {
                mat.SetFloat("_Glossiness", 0.85f);
                mat.SetFloat("_Metallic", 0.1f);
                if (mat.HasProperty("_MainTex"))
                    mat.mainTextureScale = new Vector2(24f, 24f);
                EditorUtility.SetDirty(mat);
            }
            var go = GameObject.Find(WaterObject);
            if (go == null)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Plane);
                go.name = WaterObject;
                var c = go.GetComponent<Collider>();
                if (c != null)
                    UnityEngine.Object.DestroyImmediate(c);
            }
            go.transform.position = new Vector3(0f, WorldTerrain.SeaLevel, 0f);
            // 지형보다 훨씬 넓게 — 수면 끝이 화면에 보이면 "판때기"로 읽힌다.
            go.transform.localScale = new Vector3(WorldTerrain.Span * 0.3f, 1f, WorldTerrain.Span * 0.3f);
            var rend = go.GetComponent<Renderer>();
            if (rend != null && mat != null)
                rend.sharedMaterial = mat;
        }

        static TerrainLayer EnsureTerrainLayer(string name, Color a, Color b, float tile)
        {
            return EnsureTerrainLayer(name, a, b, tile, 0);
        }

        static TerrainLayer EnsureTerrainLayer(string name, Color a, Color b, float tile, int pattern)
        {
            var mat = MakeNoiseMat(name, a, b, pattern);
            var tex = mat != null ? mat.mainTexture as Texture2D : null;
            string path = "Assets/Game/Art/Env/" + name + ".terrainlayer";
            var tl = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (tl == null)
            {
                tl = new TerrainLayer();
                AssetDatabase.CreateAsset(tl, path);
            }
            tl.diffuseTexture = tex;
            tl.tileSize = new Vector2(tile, tile);
            EditorUtility.SetDirty(tl);
            return tl;
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

        /// <summary>
        /// 소켓 컴포넌트가 없는 액터에 무기를 붙인다 — 손 본을 찾아 그 밑에 프리팹을 얹는다.
        /// 손 본도 없으면 몸 옆에 세운다(화면에 무기가 보이는 것이 목적이다).
        /// </summary>
        /// <summary>손 본. 무기를 여기 매달지 않으면 어깨 소켓에 걸려 얼굴 옆에 뜬다(검수 2026-09-06).</summary>
        public static Transform FindHandBone(GameObject actor)
        {
            Transform hand = null;
            var all = actor.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                string n = all[i].name.ToLowerInvariant();
                if (n.IndexOf("hand") < 0)
                    continue;
                if (hand == null || n.EndsWith(".r") || n.IndexOf("right") >= 0)
                    hand = all[i];
            }
            return hand;
        }

        static Transform AttachWeaponToHand(GameObject actor)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnsureKayKitPrefab(SwordFbx));
            if (prefab == null)
                return null;
            Transform hand = FindHandBone(actor);
            var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (item == null)
                return null;
            if (hand != null)
            {
                item.transform.SetParent(hand, false);
                item.transform.localPosition = Vector3.zero;
                item.transform.localRotation = Quaternion.identity;
            }
            else
            {
                var cc = actor.GetComponent<CharacterController>();
                float h = cc != null ? cc.height : 2f;
                item.transform.SetParent(actor.transform, false);
                item.transform.localPosition = new Vector3(0.45f, h * 0.35f, 0.1f);
                item.transform.localRotation = Quaternion.Euler(0f, 0f, 12f);
            }
            item.name = "Sword_Boss";
            return item.transform;
        }

        /// <summary>손에 든(또는 붙어 있는) 무기 중 **가장 큰 것** — 첫 번째를 잡으면 완드 같은 소품이 걸린다.</summary>
        static Transform FindGearTransform(GameObject actor)
        {
            Transform best = null;
            float bestLen = -1f;
            var all = actor.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (!ContainsGearName(t.name))
                    continue;
                if (t.name.IndexOf("Shield", StringComparison.OrdinalIgnoreCase) >= 0
                    || t.name.IndexOf("Quiver", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                var rends = t.GetComponentsInChildren<Renderer>(true);
                if (rends.Length == 0)
                    continue;
                var b = rends[0].bounds;
                for (int k = 1; k < rends.Length; k++)
                    b.Encapsulate(rends[k].bounds);
                float len = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                if (len > bestLen)
                {
                    bestLen = len;
                    best = t;
                }
            }
            return best;
        }

        /// <summary>렌더가 켜진 자식들의 합 바운드.</summary>
        static bool BoundsOfEnabled(Transform t, out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;
            var rends = t.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                if (!rends[i].enabled || !rends[i].gameObject.activeInHierarchy)
                    continue;
                if (!any) { bounds = rends[i].bounds; any = true; }
                else bounds.Encapsulate(rends[i].bounds);
            }
            return any;
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
                if (n.StartsWith(BossWeaponPrefix, StringComparison.Ordinal))
                    continue;   // 보스 무기는 여기서 다시 끄면 안 된다(헥사크 완드가 이렇게 사라졌다)
                bool keepIt = false;
                for (int i = 0; i < keep.Length; i++)
                    if (n == keep[i])
                        keepIt = true;
                if (!keepIt)
                    t.gameObject.SetActive(false);
            }
        }

        /// <summary>장비 이름인가(게이트도 같은 판정을 쓴다).</summary>
        public static bool IsGearName(string n) => ContainsGearName(n);

        /// <summary>손에 드는 **무기**인가 — 방패·화살통은 무기가 아니다(1몹 1무기 판정용).</summary>
        public static bool IsWeaponName(string n)
        {
            if (!ContainsGearName(n))
                return false;
            return n.IndexOf("Shield", StringComparison.OrdinalIgnoreCase) < 0
                && n.IndexOf("Quiver", StringComparison.OrdinalIgnoreCase) < 0;
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

        /// <summary>
        /// 모자·투구는 키 측정에서 뺀다(검수 2026-09-06 P0-3). 헥사크(KayKit Mage)는 챙 넓은 모자가
        /// 전체 바운드를 키워서, 목표 키에 맞추면 몸이 쪼그라들고 모자만 보였다.
        /// </summary>
        static bool IsHeadgear(Transform t)
        {
            for (var cur = t; cur != null; cur = cur.parent)
            {
                if (cur.name.IndexOf("Hat", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        /// <summary>모자를 몸 비례로 줄인다 — 45° 시점에서 챙이 몸을 덮지 않게.</summary>
        static void SlimHeadgear(Transform visual)
        {
            var all = visual.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == visual)
                    continue;
                if (all[i].name.IndexOf("Hat", StringComparison.OrdinalIgnoreCase) >= 0)
                    all[i].localScale = all[i].localScale * HeadgearScale;
            }
        }

        const float HeadgearScale = 0.62f;

        static bool BoundsOf(Transform visual, bool includeHeadgear, out Bounds bounds)
        {
            bounds = new Bounds();
            var rends = visual.GetComponentsInChildren<Renderer>();
            bool any = false;
            for (int i = 0; i < rends.Length; i++)
            {
                if (!includeHeadgear && IsHeadgear(rends[i].transform))
                    continue;
                if (!any)
                {
                    bounds = rends[i].bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(rends[i].bounds);
                }
            }
            return any;
        }

        static void FitHeight(Transform visual, Transform root, float target)
        {
            visual.localPosition = Vector3.zero;
            visual.localScale = Vector3.one;
            var rends = visual.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0)
                return;
            SlimHeadgear(visual);
            Bounds b;
            if (!BoundsOf(visual, false, out b))
                BoundsOf(visual, true, out b);
            if (b.size.y < 0.01f)
                return;
            visual.localScale = Vector3.one * (target / b.size.y);
            if (!BoundsOf(visual, false, out b))
                BoundsOf(visual, true, out b);
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
