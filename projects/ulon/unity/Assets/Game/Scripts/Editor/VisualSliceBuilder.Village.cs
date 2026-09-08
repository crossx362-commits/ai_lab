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
// 이 파일이 담는 것: 마을 드레싱·재질·랜드마크·사냥몹·밭 — 마을 화면을 만드는 패스.
// 동작 변경 0 — 구간을 순서 그대로 옮기기만 했다(순서를 바꾸면 주석과 몸통의 짝이 깨진다).
namespace Ulon.Editor
{
    public static partial class VisualSliceBuilder
    {
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
            Debug.Log("[Ulon] 건물 시야 페이드 — 레이어 올린 렌더러 " + EnsureBuildingsFadeable() + "개");
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
            var grassMat = KenneyGrassMat();
            var dirtMat = KenneyDirtMat();
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
            // 밝은 회색(0.30~0.56)은 햇빛에서 흰 스티로폼으로 보였다 — 그래서 0.15~0.34로 낮췄더니
            // **이번엔 대낮에 새까만 덩어리**가 됐다(검수 관찰 「25번 검은 직육면체」의 진짜 정체:
            // `rock-large`·`rock-wide`·`IronVein`이었다 — 나는 처음에 던전 텍스처 소품으로 잘못 짚었다).
            // 흰 스티로폼과 검은 덩어리 사이, 햇빛 아래 **돌로 읽히는** 0.34~0.58로 올린다.
            var rockMat = MakeNoiseMat("MountainRockProp", new Color(0.34f, 0.32f, 0.30f), new Color(0.58f, 0.55f, 0.50f));
            if (rockMat != null)
            {
                rockMat.SetFloat("_Glossiness", 0.08f);
                if (rockMat.HasProperty("_MainTex"))
                    rockMat.mainTextureScale = new Vector2(2.5f, 2.5f);
                EditorUtility.SetDirty(rockMat);
            }
            // 광맥도 같은 이유로 바닥을 올린다 — 철빛은 어두워야 하지만 **검은 상자**면 안 읽힌다.
            var veinMat = MakeNoiseMat("IronVeinRock", new Color(0.34f, 0.30f, 0.26f), new Color(0.66f, 0.44f, 0.20f));
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
            // **풀·흙 도포도 여기로 옮긴다**(검수 판정 2026-09-08 「재현성 결함」).
            // 증상: 이 트리에서는 `Dungeon1/ground_pathTile/Visual/:dirt` 618개가 무텍스처로 걸리는데
            // 공유 트리는 같은 소스로 통과했다. 원인은 도포 시점이었다 — 풀·흙 도포는
            // `ApplyVillageMaterials`, 즉 **마을을 짓는 도중**에만 돌았고, 던전 입구·방의 돌길 타일은
            // 그 뒤에 세워진다. 그래서 「이번 판에서 그 물건이 언제 생겼나」에 따라 결과가 갈렸다
            // (같은 소스, 다른 결과 = 재현성 결함이지 환경 차이가 아니다).
            // 바위가 이미 이 함수로 옮겨온 것과 같은 이유다: **소품이 전부 놓인 뒤에 칠한다.**
            var grassMat = KenneyGrassMat();
            var dirtMat = KenneyDirtMat();
            int greens = PaintSceneByName(grassMat, new[] { "grass", "grass_large", "plant_bush", "plant_bushLarge", "ResinBush", "FieldFlax", "grass_leafs", "ground_grass" });
            int dirts = PaintSceneByName(dirtMat, new[] { "rock_smallA", "rock_largeA", "ground_pathTile" });
            Debug.Log("[Ulon] 소품 재질 — 바위 렌더러 " + rocks + "개 암석 도포, 광맥 " + veins + "개 철광 도포, " +
                      "풀 " + greens + "개, 흙·돌길 " + dirts + "개");
        }

        /// <summary>
        /// **야외에 던전 텍스처를 두지 않는다**(검수 판정 2026-09-08, 25번 샷 「검은 직육면체」).
        ///
        /// 왜 붙었나: 마을 시설 드레싱이 궤짝·통·잡석을 `KayKit/Dungeon`에서 가져다 쓴다 —
        /// Kenney 마을 킷에 궤짝·통 메시가 **없기 때문**이다(실측: crate/barrel/box 0개).
        /// 그 조각들은 횃불 밝기를 전제한 `dungeon_texture`를 달고 있어서, 햇빛 아래 Kenney 밝은
        /// 소품 옆에 놓이면 **새까만 덩어리**로 읽힌다. 조각을 바꿀 수는 없으니(대체 메시가 없다)
        /// **재질을 마을 톤으로 갈아 끼운다.**
        ///
        /// 대상은 이름이 아니라 **규칙**으로 모은다: 던전 뿌리 밖에 있으면서 던전 텍스처를 쓰는 렌더러 전수.
        /// (이름 목록으로 잡으면 다음에 새 조각을 가져다 쓸 때 또 새까맣게 나온다.)
        /// </summary>
        public static int EnsureOutdoorPropMaterials()
        {
            var wood = MakeNoiseMat("VillageCrateWood", new Color(0.34f, 0.23f, 0.13f), new Color(0.62f, 0.45f, 0.26f));
            var stone = MakeNoiseMat("VillageFieldStone", new Color(0.33f, 0.31f, 0.28f), new Color(0.58f, 0.55f, 0.50f));
            if (wood == null || stone == null)
                return 0;
            var rends = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int painted = 0;
            for (int i = 0; i < rends.Length; i++)
            {
                if (!IsOutdoorDungeonTextured(rends[i]))
                    continue;
                // **경로 전체**로 본다 — 렌더러 이름은 `default`, 부모는 `Visual`이라 그 둘만 보면
                // 모닥불 돌(`Campfire/FacPartStone1/Visual/default`)이 「나무」로 칠해진다(실측 오답).
                string n = GroundFit.NodePath(rends[i].transform);
                bool rubble = n.IndexOf("Stone", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              n.IndexOf("Rubble", StringComparison.OrdinalIgnoreCase) >= 0;
                rends[i].sharedMaterial = rubble ? stone : wood;
                EditorUtility.SetDirty(rends[i]);
                painted++;
                Debug.Log("[Ulon] 야외 소품 재질 교체 — " + GroundFit.NodePath(rends[i].transform) +
                          " 던전 텍스처 → " + (rubble ? "마을 돌" : "마을 나무"));
            }
            Debug.Log("[Ulon] 야외 던전텍스처 정리 — " + painted + "개 교체(마을 킷에 궤짝·통 메시가 없어 던전 조각을 쓰고 있다)");
            return painted;
        }

        /// <summary>야외(던전 뿌리 밖)에 서 있으면서 던전 텍스처를 쓰는 렌더러인가 — 게이트와 공용.</summary>
        public static bool IsOutdoorDungeonTextured(Renderer r)
        {
            if (r == null || r.sharedMaterial == null || !r.sharedMaterial.HasProperty("_MainTex"))
                return false;
            var tex = r.sharedMaterial.mainTexture;
            // **텍스처 이름이 아니라 그 파일이 사는 자리**로 본다 — 이름은 임포터·리네임에 흔들리지만
            // 「던전 킷 폴더에서 온 텍스처인가」는 자산의 성질이다(검수 우선순위 ④).
            string texPath = UnityEditor.AssetDatabase.GetAssetPath(tex);
            if (tex == null || string.IsNullOrEmpty(texPath) ||
                texPath.IndexOf("/Dungeon/", StringComparison.OrdinalIgnoreCase) < 0)
                return false;
            // 던전 자리(지하 방·입구 문틀) 안이면 던전 텍스처가 **맞는 자리**다 —
            // 예전엔 `root.name`에 "Dungeon"이 있는지로 봤고, 그건 이름을 갈면 새는 자였다.
            return !(GroundFit.WorldBounds(r.transform, out Bounds wb) && GroundFit.InDungeonPlace(wb));
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

        /// <summary>
        /// 마을 랜드마크가 **없으면 만든다**(멱등).
        ///
        /// 첫 드레싱은 킷이 뿌려 둔 오브젝트(`stall`·`fountain-round`·`plant_bushLarge`)를 **이름만 바꿔**
        /// 랜드마크로 썼다. 그래서 마을을 **두 번째로 드레싱하면 재료가 없다** — 2026-09-09에 실제로
        /// `Forge`→`Vendor` 순으로 사라져 게이트가 「마을 랜드마크가 있어야 합니다」로 멈췄고,
        /// 지붕 한 곳을 고치려던 랩이 마을을 다시 굽지 못해 막혔다.
        /// 「고치는 자를 고치는 랩이 세계를 고치는 랩보다 먼저」다 — 다시 굽는 도구가 한 번만 도는 도구면
        /// 그 도구로 세운 세계는 손댈 수 없다.
        /// </summary>
        static GameObject EnsureLandmarkObject(string name, string modelPath, string legacyName, Vector3 pos, Vector3 euler)
        {
            var go = GameObject.Find(name);
            if (go == null && !string.IsNullOrEmpty(legacyName))
            {
                go = GameObject.Find(legacyName);          // 옛 방식: 킷이 놓아 둔 물건을 이름만 바꿔 쓴다
                if (go != null)
                    go.name = name;
            }
            if (go == null)
            {
                go = Place(modelPath, pos, euler);          // 재료가 없으면 **만든다** — 두 번째 판을 위해
                if (go == null)
                    throw new InvalidOperationException("랜드마크 모델이 없습니다: " + modelPath);
                go.name = name;
                Debug.Log("[Ulon] 마을 랜드마크 " + name + " 새로 세움 — 재드레싱에도 남는다");
            }
            go.transform.SetPositionAndRotation(OnGround(new Vector3(pos.x, 0f, pos.z)) + Vector3.up * pos.y,
                Quaternion.Euler(0f, euler.y, 0f));
            SnapRootToGround(go);
            return go;
        }

        static void PlaceLandmarks()
        {
            MoveNamed("Player", new Vector3(0f, 0f, 0f), Vector3.zero);
            MoveNamed(CompanionObject, CompanionSpot, new Vector3(0f, 180f, 0f));
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
            var forgeGo = EnsureLandmarkObject("Forge", "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/stall.fbx", null,
                new Vector3(-6.8f, 0f, 3.4f), new Vector3(0f, 90f, 0f));
            var cs = forgeGo.GetComponent<CraftStation>() ?? forgeGo.AddComponent<CraftStation>();
            cs.RecipeId = "iron_sword";
            cs.DisplayName = "대장간";
            EnsureCollider(forgeGo);
            EnsureCarpenterLandmark();
            var vendorGo = EnsureLandmarkObject("Vendor", "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/stall.fbx", "stall",
                new Vector3(-5.2f, 0f, 3.4f), new Vector3(0f, 90f, 0f));
            var vs = vendorGo.GetComponent<VendorStation>() ?? vendorGo.AddComponent<VendorStation>();
            vs.DisplayName = "잡화";
            EnsureCollider(vendorGo);
            EnsureLandmarkObject("Healer", "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/fountain-round.fbx", "fountain-round",
                new Vector3(-3.6f, 0f, -3.6f), Vector3.zero);
            var ironGo = EnsureLandmarkObject("IronVein", "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/rock-large.fbx", null,
                new Vector3(9.8f, 0f, -3.4f), Vector3.zero);
            var ironNode = ironGo.GetComponent<ResourceNode>() ?? ironGo.AddComponent<ResourceNode>();
            ironNode.ResourceId = "iron_ore";
            ironNode.DisplayName = "철 광맥";
            MoveNamed("OakTree", new Vector3(12f, 0f, 12.5f), Vector3.zero);
            EnsureLandmarkObject("ResinBush", "Assets/_ThirdParty/Kenney/Nature/RAW/Models/plant_bushLarge.fbx",
                "plant_bushLarge", new Vector3(4.6f, 0f, -3.6f), Vector3.zero);
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
            // **`Skeleton`도 없으면 세운다.** 예전엔 찾아서 묶기만 했다 — 자격 패스가 지우면
            // 다시 세우는 코드가 파이프라인에 없어 **동료와 똑같이 영영 사라질** 자리였다(랩 ③ 전수 조사).
            EnsureHuntMob("Skeleton", MobCatalog.Skeleton, SkeletonFbx, new Vector3(0.6f, 0f, 13.6f));
            EnsureHuntMob("Bandit", MobCatalog.Bandit, RogueFbx, new Vector3(-1.6f, 0f, 13.2f));
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

        /// <summary>아마밭 이랑 수·한 줄에 심는 수 — 게이트가 같은 값을 읽는다(두 벌로 적지 않는다).</summary>
        public const int FlaxRows = 5;
        public const int FlaxPerRow = 9;

        public static void EnsureSouthField()
        {
            const string Bush = "Assets/_ThirdParty/Kenney/Nature/RAW/Models/plant_bushLarge.fbx";
            const string BushS = "Assets/_ThirdParty/Kenney/Nature/RAW/Models/plant_bush.fbx";
            const string RockA = "Assets/_ThirdParty/Kenney/Nature/RAW/Models/rock_largeA.fbx";
            const string RockS = "Assets/_ThirdParty/Kenney/Nature/RAW/Models/rock_smallA.fbx";
            const string Tuft = "Assets/_ThirdParty/Kenney/Nature/RAW/Models/grass_large.fbx";
            const string Crop = "Assets/_ThirdParty/Kenney/Nature/RAW/Models/grass_leafs.fbx";
            // 농경지·마을과 **같은 울타리 모델**을 쓴다. Nature 킷의 fence.fbx는 축이 반대라
            // `FenceRun`의 yaw 규칙에서 조각이 가로로 서서 밭을 빗처럼 가로질렀다(첫 샷이 그랬다).
            const string Fence = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/fence.fbx";
            string[] models = { Bush, BushS, RockA, RockS, Tuft, Crop, Fence };
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
            root.transform.position = new Vector3(WorldSplat.FlaxX, 0f, WorldSplat.FlaxZ);
            Transform parent = root.transform;

            // **밭으로 읽히게 한다**(검수 반려: 20cm 덤불 하나가 「아마밭」이었다).
            // 밭은 ⓐ 갈아엎은 흙(도포는 `WorldSplat.FlaxCoverAt`) ⓑ **줄줄이 반복되는 작물**
            // ⓒ 두른 울타리 — 셋이 같이 있어야 「경작지」다. 농경지 지역(BuildMeadow)과 같은 규칙을 쓴다.
            float hx = WorldSplat.FlaxHalfX, hz = WorldSplat.FlaxHalfZ;
            FenceRun(parent, Fence, new Vector2(WorldSplat.FlaxX - hx, WorldSplat.FlaxZ - hz), new Vector2(WorldSplat.FlaxX + hx, WorldSplat.FlaxZ - hz));
            FenceRun(parent, Fence, new Vector2(WorldSplat.FlaxX - hx, WorldSplat.FlaxZ + hz), new Vector2(WorldSplat.FlaxX + hx, WorldSplat.FlaxZ + hz));
            FenceRun(parent, Fence, new Vector2(WorldSplat.FlaxX - hx, WorldSplat.FlaxZ - hz), new Vector2(WorldSplat.FlaxX - hx, WorldSplat.FlaxZ + hz));
            FenceRun(parent, Fence, new Vector2(WorldSplat.FlaxX + hx, WorldSplat.FlaxZ - hz), new Vector2(WorldSplat.FlaxX + hx, WorldSplat.FlaxZ + hz));
            // 이랑 — **줄 간격은 넓게, 줄 안은 촘촘하게**. 격자로 고르게 뿌리면 이랑이 안 읽힌다.
            int crops = 0;
            for (int row = 0; row < FlaxRows; row++)
            {
                float z = WorldSplat.FlaxZ - hz + 1.0f + row * ((hz * 2f - 2.0f) / (FlaxRows - 1));
                for (int col = 0; col < FlaxPerRow; col++)
                {
                    float x = WorldSplat.FlaxX - hx + 0.8f + col * ((hx * 2f - 1.6f) / (FlaxPerRow - 1));
                    int seed = row * 37 + col * 11;
                    var stalk = Place(Crop, new Vector3(x, 0f, z), new Vector3(0f, WorldRegions.Rand(seed, 1, 0f, 360f), 0f));
                    if (stalk == null)
                        continue;
                    stalk.name = "FlaxStalk";
                    stalk.transform.SetParent(parent, true);
                    // 한 포기는 무릎 아래라 조망에서 흙 얼룩으로 보인다 — 농경지와 같은 배율로 키운다.
                    stalk.transform.localScale = stalk.transform.localScale * WorldRegions.Rand(seed, 2, 1.7f, 2.4f);
                    crops++;
                }
            }
            if (crops < FlaxRows * FlaxPerRow / 2)
                throw new InvalidOperationException("아마밭 작물이 " + crops + "포기뿐입니다 — 모델을 못 놓았습니다(0이면 실패).");

            // 수확 대상 한 포기는 **밭 한가운데의 작물**이다(밭 밖의 덤불이 아니라).
            var flax = Place(Crop, new Vector3(WorldSplat.FlaxX, 0f, WorldSplat.FlaxZ), Vector3.zero);
            if (flax == null)
                throw new InvalidOperationException("아마밭 작물 모델 없음");
            flax.name = "FieldFlax";
            flax.transform.SetParent(parent, true);
            flax.transform.localScale = flax.transform.localScale * 2.6f;   // 수확 대상은 조금 더 크게(어디를 캐는지 보이게)
            var node = flax.GetComponent<ResourceNode>() ?? flax.AddComponent<ResourceNode>();
            node.ResourceId = ItemCatalog.Cloth;
            node.DisplayName = "들판 아마";
            node.GatherSkill = SkillId.Tailoring;
            node.Remaining = 10;
            node.Capacity = 10;
            node.RespawnSeconds = 8f;
            node.Difficulty = 10f;
            EnsureCollider(flax);
            // 곁가지는 **울타리 밖**에 둔다 — 밭 한가운데 바위가 박혀 있으면 경작지로 안 읽힌다.
            Decor(parent, RockA, new Vector3(WorldSplat.FlaxX + hx + 1.6f, 0f, WorldSplat.FlaxZ + 1.2f), new Vector3(0f, 30f, 0f));
            Decor(parent, RockS, new Vector3(WorldSplat.FlaxX - hx - 1.4f, 0f, WorldSplat.FlaxZ - 1.0f), new Vector3(0f, 80f, 0f));
            Decor(parent, BushS, new Vector3(WorldSplat.FlaxX + hx + 1.2f, 0f, WorldSplat.FlaxZ - hz - 1.3f), new Vector3(0f, 50f, 0f));
            Decor(parent, Tuft, new Vector3(WorldSplat.FlaxX - hx - 1.1f, 0f, WorldSplat.FlaxZ + hz + 1.0f), new Vector3(0f, 15f, 0f));
            Decor(parent, Tuft, new Vector3(WorldSplat.FlaxX + 0.6f, 0f, WorldSplat.FlaxZ - hz - 1.5f), new Vector3(0f, 110f, 0f));
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
    }
}
