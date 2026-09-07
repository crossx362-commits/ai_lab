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
// 이 파일이 담는 것: 궤짝·집 상인·주택지·길들이기 짐승·문게이트·마구간·절구·낚시터·모닥불 — 마을 시설 개체.
// 동작 변경 0 — 구간을 순서 그대로 옮기기만 했다(순서를 바꾸면 주석과 몸통의 짝이 깨진다).
namespace Ulon.Editor
{
    public static partial class VisualSliceBuilder
    {
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
            // 야생하트 = **사슴 모델**(OGA CC0, 오너 승인 2026-09-07). 덤불 메시로 세우던 결함을 여기서 끝낸다.
            const string fbx = "Assets/_ThirdParty/OpenGameArt/Deer/RAW/Deer.obj";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(fbx) == null)
                ConfigureProp(fbx);
            var old = GameObject.Find(TameCritter.Object);
            if (old != null)
                UnityEngine.Object.DestroyImmediate(old);
            Vector3 pos = new Vector3(TameCritter.X, 0f, TameCritter.Z);
            var go = Place(fbx, pos, Vector3.zero);
            if (go == null)
                throw new InvalidOperationException("조련 대상 사슴 메시 없음: " + fbx);
            go.name = TameCritter.Object;
            go.transform.SetPositionAndRotation(OnGround(pos), Quaternion.identity);
            FitCreatureHeight(go, MobCatalog.HeightOf(TameCritter.Id));
            PaintCreature(go, true);
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
            // 멧돼지 = **멧돼지 모델**(OGA CC0 boar_0.blend → Boar.fbx, 오너 승인 2026-09-07).
            const string fbx = "Assets/_ThirdParty/OpenGameArt/Boar/RAW/Boar.fbx";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(fbx) == null)
                ConfigureProp(fbx);
            var old = GameObject.Find(TameBoar.Object);
            if (old != null)
                UnityEngine.Object.DestroyImmediate(old);
            Vector3 pos = new Vector3(TameBoar.X, 0f, TameBoar.Z);
            var go = Place(fbx, pos, Vector3.zero);
            if (go == null)
                throw new InvalidOperationException("조련 멧돼지 메시 없음: " + fbx);
            go.name = TameBoar.Object;
            go.transform.SetPositionAndRotation(OnGround(pos), Quaternion.identity);
            FitCreatureHeight(go, MobCatalog.HeightOf(TameBoar.Id));
            PaintCreature(go, false);
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

        /// <summary>
        /// **낚시터가 설 물가**(검수 반려 2026-09-07: 「데크가 잔디 위, 물이 화면에 없다」).
        /// 좌표를 손으로 박지 않는다 — 호수 중심에서 마을 쪽으로 걸어 나오며 **지표가 수면 위로
        /// 올라오는 지점**(물가)을 찾는다. 지형을 바꾸면 이 값도 같이 움직인다.
        /// </summary>
        public static Vector3 LakeShoreTowardVillage()
        {
            var center = new Vector2(WorldTerrain.LakeX, WorldTerrain.LakeZ);
            var dir = (new Vector2(0f, 0f) - center).normalized;      // 마을(원점) 쪽
            // **공식이 아니라 실제 지형을 잰다** — 하이트맵 해상도 때문에 공식과 실물이 갈리고,
            // 공식으로 잡은 「물가」가 실물에서는 수면보다 6m 높은 **둑 위**였다(첫 시도 실측).
            for (float d = 0f; d <= WorldTerrain.LakeRadius + 12f; d += 0.25f)
            {
                var p = center + dir * d;
                float h = GroundY(p.x, p.y);
                if (h > WorldTerrain.SeaLevel + 0.3f)
                    return new Vector3(p.x - dir.x * 0.25f, 0f, p.y - dir.y * 0.25f);   // 마지막으로 물에 잠긴 자리
            }
            return new Vector3(WorldTerrain.LakeX + WorldTerrain.LakeRadius, 0f, WorldTerrain.LakeZ);
        }

        public static void EnsureFishSpot()
        {
            // **물레방아 폴백을 없앴다**(검수 판정 2026-09-07). 낚시터가 없을 때 장식 물레방아를
            // 집어 「물가」 간판만 갈아 끼우는 경로였고, 그래서 **아무도 낚시터가 없다는 걸 몰랐다**.
            // 조용한 폴백은 강등 운영과 같다 — 없으면 낚시터 자체를 짓는다(발판이 본체다).
            const string fbx = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/planks.fbx";
            Vector3 pos = LakeShoreTowardVillage();
            var go = GameObject.Find("FishingSpot");
            if (go == null)
            {
                go = Place(fbx, pos, Vector3.zero);
                Debug.Log("[Ulon] 낚시터가 없어 새로 지었다 — 물가 발판(" + pos.ToString("0.0") + ")");
            }
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
    }
}
