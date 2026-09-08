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
    public static partial class VisualSliceBuilder
    {
        const string ScenePath = "Assets/Game/Scenes/Bootstrap.unity";
        const string ControllerPath = "Assets/Game/Art/Characters/SharedLocomotion.controller";
        const string KnightFbx = "Assets/_ThirdParty/KayKit/Adventurers/RAW/Characters/Knight.fbx";
        // Barbarian은 **맨몸 상반신**이라 잡몹·동료로 쓰지 않는다(검수 2026-09-06: 살색 덩어리로 읽힌다).
        // 자격은 `MobArt` 원장이 강제한다 — 여기서 지우기만 하면 다음 사람이 다시 넣는다.
        const string BarbarianFbx = "Assets/_ThirdParty/KayKit/Adventurers/RAW/Characters/Barbarian.fbx";
        const string MageFbx = "Assets/_ThirdParty/KayKit/Adventurers/RAW/Characters/Mage.fbx";
        const string RogueFbx = "Assets/_ThirdParty/KayKit/Adventurers/RAW/Characters/Rogue.fbx";
        // 후드 로브 — **치유사 전용**(2026-09-08 도입). 판금(Knight)이라 「경비로 읽힌다」던 §8.1
        // 불합격을 푸는 자리다. 다른 역할에 돌려쓰면 그 식별 축이 다시 무너진다.
        const string RogueHoodedFbx = "Assets/_ThirdParty/KayKit/Adventurers/RAW/Characters/RogueHooded.fbx";
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
    }
}
