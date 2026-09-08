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
// 이 파일이 담는 것: 접지·건물 페이드·하늘·지면·마을 바닥·울타리 배치·노이즈 재질 — 바닥과 재질.
// 동작 변경 0 — 구간을 순서 그대로 옮기기만 했다(순서를 바꾸면 주석과 몸통의 짝이 깨진다).
namespace Ulon.Editor
{
    public static partial class VisualSliceBuilder
    {
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

        /// <summary>
        /// **마을 건물 원장** — 「무엇이 건물인가」를 한 곳에만 적는다. 전에는 같은 이름 목록이
        /// 지표 스냅·매몰 판정에 복붙돼 있었고, 여기에 시야 페이드까지 붙으면 세 벌이 갈라진다.
        /// </summary>
        static readonly string[] BuildingObjects =
        {
            "House", "Banker", "Forge", "Vendor", "Healer", "watermill",
            HousingPlot.HouseObject, HousingPlot.VendorObject,
        };

        static bool IsBuilding(string name)
        {
            for (int i = 0; i < BuildingObjects.Length; i++)
                if (BuildingObjects[i] == name)
                    return true;
            return false;
        }

        /// <summary>마을 페이드 범위(반경 m) — 가드존 마을 구역. 이 밖은 소품 배치 헬퍼가 이미 레이어를 준다.</summary>
        public const float VillageFadeRadius = 22f;

        /// <summary>
        /// 건물도 **카메라와 플레이어 사이에 끼면 걷힌다**(검수 랩 D). 카메라 요는 고정이라
        /// 집 뒤에 서면 화면에서 사라진다 — 실측 마을 279자리 중 7자리, 최악 20%(은행 풍차).
        /// 새 기구를 만들지 않고 런타임이 이미 가진 `DungeonSightFade`가 걷도록 **레이어만** 올린다.
        /// 콜라이더는 그대로라 이동 제한·충돌은 유지된다.
        /// </summary>
        public static int EnsureBuildingsFadeable()
        {
            int layer = LayerMask.NameToLayer(DungeonBlockerLayer);
            if (layer < 0)
                throw new InvalidOperationException("레이어 " + DungeonBlockerLayer + "가 없습니다(ProjectSettings/TagManager).");
            int moved = 0;
            var all = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                var go = all[i];
                if (go == null || !go.scene.IsValid() || go.transform.parent != null)
                    continue;                                   // 루트 단위로만 판단한다(자식은 함께 딸려간다)
                // **이름 목록으로 하나씩 추가하면 두더지잡기가 된다** — 낚시터를 넣으니 화덕이 나왔다.
                // 규칙으로 쓴다: 마을 안의 **정적 렌더러는 전부** 시야에 끼면 걷힌다.
                var p = go.transform.position;
                if (Mathf.Abs(p.x) > VillageFadeRadius || Mathf.Abs(p.z) > VillageFadeRadius)
                    continue;
                if (go.name == "Ground" || go.name.StartsWith("Terrain", StringComparison.Ordinal))
                    continue;                                   // 지표는 페이드 대상이 아니다
                if (go.GetComponentInChildren<Ulon.Server.WorldBody>(true) != null)
                    continue;                                   // 사람·짐승은 투명해지면 더 이상하다
                var rends = go.GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < rends.Length; r++)
                {
                    if (rends[r].gameObject.layer == layer)
                        continue;
                    rends[r].gameObject.layer = layer;
                    moved++;
                }
            }
            return moved;
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
                if (IsBuilding(n) || n == "FishingSpot" || n == "Campfire" || n == "Mortar"
                    || n == "OakTree" || n == "IronVein")
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
                if (!IsBuilding(n))
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
            var grassA = KenneyGrassMat();
            var dirtMat = KenneyDirtMat();
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
            // 소리를 듣는 귀도 씬에 박아 둔다 — 리스너가 없으면 SFX를 아무리 울려도 들리지 않는다.
            if (UnityEngine.Object.FindAnyObjectByType<AudioListener>() == null)
                cam.gameObject.AddComponent<AudioListener>();
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
            if (IsModelPath(path))
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


        /// <summary>
        /// **한 이름에 한 색.**(검수 판정 2026-09-08, 재현성 뿌리) `MakeNoiseMat`은 이름으로 PNG를 쓰는데
        /// `KenneyGrass`를 부르는 자리가 **두 가지 색 쌍**을 넘기고 있었다(0.30/0.50/0.18 대 0.32/0.52/0.20).
        /// 그래서 매 실행 마지막에 부른 쪽이 파일을 덮어써 `Art/Env/KenneyGrass.png`가 **상시 dirty**였다 —
        /// 생성물이 소스 자리에서 판마다 흔들리면 「이 판에서만 참」인 초록이 된다.
        /// 색을 여기 한 자리에 두고 모두가 이것만 부른다(같은 로직이 여러 곳에 살면 재발한다).
        /// </summary>
        static Material KenneyGrassMat() =>
            MakeNoiseMat("KenneyGrass", new Color(0.32f, 0.52f, 0.20f), new Color(0.24f, 0.42f, 0.14f));

        static Material KenneyDirtMat() =>
            MakeNoiseMat("KenneyDirt", new Color(0.52f, 0.38f, 0.24f), new Color(0.40f, 0.28f, 0.16f));

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
    }
}
