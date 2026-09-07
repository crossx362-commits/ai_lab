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

// **VisualSliceBuilder 분할**(오너 상시 지시 2026-09-08, 파일 비대화 정리).
// 이 파일이 담는 것: 던전 1~3과 그 안의 몹·보스, 필드 보스 — 던전 개체를 세우는 패스.
// 동작 변경 0 — 구간을 순서 그대로 옮기기만 했다(순서를 바꾸면 주석과 몸통의 짝이 깨진다).
namespace Ulon.Editor
{
    public static partial class VisualSliceBuilder
    {
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
            HideGateMesh(Dungeon1.EntranceObject);          // 문 한가운데 회색 막대로 읽히던 표적 메시
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
            HideGateMesh(Dungeon2.EntranceObject);          // 문 한가운데 회색 막대로 읽히던 표적 메시
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
            HideGateMesh(Dungeon3.EntranceObject);          // 문 한가운데 회색 막대로 읽히던 표적 메시
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
            if (AssetDatabase.LoadAssetAtPath<GameObject>(RogueFbx) == null)
                return;
            ConfigureHumanoid(RogueFbx, true);
            var spawned = SpawnActor(
                Dungeon2.MobObject,
                RogueFbx,
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
    }
}
