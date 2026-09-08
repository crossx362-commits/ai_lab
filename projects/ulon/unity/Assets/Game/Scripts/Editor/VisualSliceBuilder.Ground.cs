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
        /// <summary>이름으로 찾아 **모듈 좌표** 자리에 세운다 — 부르는 곳이 마을 배치뿐이라
        /// 여기서 월드로 옮긴다(랩 B). 사람·소품 모두 마을이 커지면 같이 밀려나야 한다.</summary>
        static void MoveNamed(string name, Vector3 pos, Vector3 euler)
        {
            pos = Module(pos);
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

        public static bool IsBuildingObject(string name)
        {
            for (int i = 0; i < BuildingObjects.Length; i++)
                if (BuildingObjects[i] == name)
                    return true;
            return false;
        }

        /// <summary>마을 페이드 범위(반경 m) — 가드존 마을 구역. 이 밖은 소품 배치 헬퍼가 이미 레이어를 준다.</summary>
        public const float VillageFadeRadius = 22f * KitScale;   // 마을이 커지면 마을 구역도 같이 커진다(랩 B)

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
                if (IsBuildingObject(n) || n == "FishingSpot" || n == "Campfire" || n == "Mortar"
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
                if (!IsBuildingObject(n))
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
                ApplySkyLedger(sky);          // 값은 대기 원장에서 온다 — 여기는 재질을 만들고 붙일 뿐이다
                RenderSettings.skybox = sky;
            }
            EnsureWorldAtmosphere();
        }

        static void ImproveGround()
        {
            EnsureVillageTerrain();
        }

        static void PlaceVillageFloor(Transform parent)
        {
            // **광장 바닥은 킷 판때기가 아니라 지형 도포**(WorldSplat.Cobble)다 — 킷 모델은 색상
            // 아틀라스라 UV가 한 점이어서 어떤 무늬를 씌워도 단색이 된다(2026-09-09 실측).
            var grassA = KenneyGrassMat();
            var dirtMat = KenneyDirtMat();
            AssignMat("Assets/_ThirdParty/Kenney/Nature/RAW/Models/ground_pathTile.fbx", dirtMat);
            AssignMat("Assets/_ThirdParty/Kenney/Nature/RAW/Models/grass_large.fbx", grassA);
        }

        /// <summary>
        /// **커밋된 씬의 광장 바닥을 원장으로 수렴시킨다**(멱등 보수 패스, 검수 랩 ②).
        /// `PlaceVillageFloor`는 전체 재드레싱 때만 돌아서, 바닥 규칙을 고쳐도 씬은 그대로였다
        /// (2026-09-09: 무늬를 돌포장으로 바꾸고 마당을 넓혔는데 화면이 한 픽셀도 안 바뀌었다).
        /// 여기서는 세 가지만 한다: ①빠진 바닥 칸 채우기 ②전부 돌포장 재질로 ③가로등을 마당 귀퉁이로.
        /// </summary>
        public static void EnsureVillagePlaza()
        {
            var decor = GameObject.Find("VillageDecor");
            if (decor == null)
                return;
            // ①·② **판때기를 걷어낸다** — 광장 바닥은 이제 지형 도포(WorldSplat.Cobble)다.
            // 킷 판때기는 UV가 한 점이라 무늬가 안 살고, 지형 위에 겹치면 z-파이팅만 남는다.
            var doomed = new List<GameObject>();
            foreach (var t in decor.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("road", StringComparison.Ordinal))
                    doomed.Add(t.gameObject);
            for (int i = 0; i < doomed.Count; i++)
                UnityEngine.Object.DestroyImmediate(doomed[i]);

            // ③ 마당을 넓혔으니 가운데 서 있던 가로등 넷을 귀퉁이로 옮긴다(원장 자리와 같은 값).
            var corners = new[]
            {
                new Vector3(-3.6f, 0f, -3.6f), new Vector3(3.6f, 0f, -3.6f),
                new Vector3(-3.6f, 0f, 3.6f), new Vector3(3.6f, 0f, 3.6f),
            };
            var old = new[]
            {
                new Vector3(-2.2f, 0f, -2.2f), new Vector3(2.2f, 0f, -2.2f),
                new Vector3(-2.2f, 0f, 2.2f), new Vector3(2.2f, 0f, 2.2f),
            };
            int moved = 0;
            for (int i = 0; i < old.Length; i++)
            {
                Vector3 from = Module(old[i]);
                foreach (var t in decor.GetComponentsInChildren<Transform>(true))
                {
                    if (!t.name.StartsWith("lantern", StringComparison.Ordinal))
                        continue;
                    if (new Vector2(t.position.x - from.x, t.position.z - from.z).magnitude > 0.4f)
                        continue;
                    var to = Module(corners[i]);
                    t.position = new Vector3(to.x, t.position.y, to.z);
                    SnapRootToGround(t.gameObject);
                    moved++;
                    break;
                }
            }
            // ④ **빈 중심을 채운다**(검수 랩 ②: 「빈 중심 — 우물·게시판·좌판」).
            // 우물·게시판은 등록 자산에 없다 → 세우지 않고 미결로 남긴다(가짜를 세우지 않는다, §11).
            // 있는 것으로 채운다: **분수를 광장 한가운데로**(치유사 = fountain-round, 실재 자산) +
            // 좌판·벤치·의자·수레를 마당 가장자리에 모아 「사람이 모이는 자리」로 만든다.
            var fountain = GameObject.Find("Healer");
            if (fountain != null)
            {
                var c = Module(new Vector3(-0.5f, 0f, -0.5f));
                fountain.transform.position = new Vector3(c.x, fountain.transform.position.y, c.z);
                SnapRootToGround(fountain);
            }
            const string MarketPrefix = "PlazaMarket";
            var stale = new List<GameObject>();
            foreach (var t in decor.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith(MarketPrefix, StringComparison.Ordinal))
                    stale.Add(t.gameObject);
            for (int i = 0; i < stale.Count; i++)
                UnityEngine.Object.DestroyImmediate(stale[i]);

            var market = new (string Path, Vector3 At, float Yaw)[]
            {
                ("Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/stall-red.fbx",   new Vector3(-3.0f, 0f, 2.6f), 180f),
                ("Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/stall-green.fbx", new Vector3(2.4f, 0f, 2.6f), 180f),
                ("Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/stall-bench.fbx", new Vector3(-2.8f, 0f, -2.4f), 90f),
                ("Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/stall-stool.fbx", new Vector3(2.6f, 0f, -2.6f), 0f),
                ("Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/cart.fbx",        new Vector3(3.0f, 0f, 0.8f), 90f),
            };
            int stalls = 0;
            for (int i = 0; i < market.Length; i++)
            {
                var go = Place(market[i].Path, Module(market[i].At), Quaternion.Euler(0f, market[i].Yaw, 0f));
                if (go == null)
                    continue;
                go.name = MarketPrefix + (i + 1);
                go.transform.SetParent(decor.transform, true);
                stalls++;
            }

            Debug.Log("[Ulon] 광장 좌판 " + stalls + "개 · 분수 " + (fountain != null ? "가운데로" : "없음") +
                      " (우물·게시판은 등록 자산 없음 — 미결)");
            Debug.Log("[Ulon] 광장 바닥 — 킷 판때기 " + doomed.Count + "칸을 걷었습니다(바닥은 지형 돌포장) · 가로등 " +
                      moved + "개를 귀퉁이로");
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
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

        /// <summary>
        /// 광장 바닥이 깔리는 자리(모듈 격자 1칸). **십자로만 깔면 광장이 아니라 교차로다** —
        /// 검수 2026-09-09: 「01·02가 회색 십자로」. 가운데에 **네모난 마당**을 두고 거기서 길 넷이 뻗는다.
        /// 마당 크기는 집들 사이 빈 자리에서 나온다: 집 앞마당이 모듈 ±4.8부터 시작하므로 ±3.5까지 깐다.
        /// </summary>
        static bool PlazaPath(int x, int z)
        {
            bool square = x >= -4 && x <= 3 && z >= -4 && z <= 3;
            bool eastWest = (z == 0 || z == -1) && x >= -8 && x <= 9;
            bool northSouth = (x == 0 || x == -1) && z >= -7 && z <= 10;
            return square || eastWest || northSouth;
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
            // 가로등은 **마당 네 귀퉁이**에 선다 — 마당을 넓히자 옛 자리(±2.2)는 한복판이 됐다.
            DecorM(parent, lantern, new Vector3(-3.6f, 0f, -3.6f), Vector3.zero);
            DecorM(parent, lantern, new Vector3(3.6f, 0f, -3.6f), Vector3.zero);
            DecorM(parent, lantern, new Vector3(-3.6f, 0f, 3.6f), Vector3.zero);
            DecorM(parent, lantern, new Vector3(3.6f, 0f, 3.6f), Vector3.zero);
            DecorM(parent, lantern, new Vector3(-7.6f, 0f, 2.2f), Vector3.zero);
            DecorM(parent, lantern, new Vector3(8.6f, 0f, -2.2f), Vector3.zero);
            DecorM(parent, lantern, new Vector3(2.2f, 0f, 9.4f), Vector3.zero);
            DecorM(parent, lantern, new Vector3(-2.2f, 0f, -6.4f), Vector3.zero);
            DecorM(parent, Bench, new Vector3(-5.4f, 0f, -2.4f), new Vector3(0f, 90f, 0f));
            DecorM(parent, Bench, new Vector3(-2.4f, 0f, -5.4f), Vector3.zero);
            DecorM(parent, Stool, new Vector3(-4.6f, 0f, -3.6f), Vector3.zero);
            DecorM(parent, Stool, new Vector3(-2.8f, 0f, -4.6f), Vector3.zero);
            DecorM(parent, StallR, new Vector3(-5.2f, 0f, 4.8f), new Vector3(0f, 90f, 0f));
            DecorM(parent, StallG, new Vector3(4.8f, 0f, -5.2f), new Vector3(0f, 270f, 0f));
            DecorM(parent, CartH, new Vector3(-6.4f, 0f, 3.6f), Vector3.zero);
            DecorM(parent, HedgeL, new Vector3(-4.2f, 0f, 7.4f), Vector3.zero);
            DecorM(parent, HedgeL, new Vector3(5.4f, 0f, 7.4f), Vector3.zero);
            DecorM(parent, hedge, new Vector3(-7.4f, 0f, 1.6f), new Vector3(0f, 90f, 0f));
            DecorM(parent, hedge, new Vector3(7.4f, 0f, -1.6f), new Vector3(0f, 90f, 0f));
            DecorM(parent, Poles, new Vector3(-2.4f, 0f, 8.6f), Vector3.zero);
            DecorM(parent, tree, new Vector3(-14.5f, 0f, -13.5f), Vector3.zero);
            DecorM(parent, treeH, new Vector3(14.8f, 0f, -13.2f), new Vector3(0f, 20f, 0f));
            DecorM(parent, TreeR, new Vector3(14.5f, 0f, 13.8f), new Vector3(0f, 90f, 0f));
            DecorM(parent, TreeHC, new Vector3(-14.2f, 0f, 13.5f), Vector3.zero);
            DecorM(parent, TreeC, new Vector3(-13.2f, 0f, 6.4f), new Vector3(0f, 30f, 0f));
            DecorM(parent, treeH, new Vector3(13.4f, 0f, 5.2f), new Vector3(0f, 70f, 0f));
            DecorM(parent, TreeR, new Vector3(-6.8f, 0f, -13.4f), Vector3.zero);
            DecorM(parent, tree, new Vector3(6.6f, 0f, -13.6f), new Vector3(0f, 50f, 0f));
            DecorM(parent, TreeC, new Vector3(13.8f, 0f, -6.4f), new Vector3(0f, 15f, 0f));
            DecorM(parent, treeH, new Vector3(-13.6f, 0f, -7.8f), Vector3.zero);
            DecorM(parent, bush, new Vector3(-9.2f, 0f, -7.1f), Vector3.zero);
            DecorM(parent, bush, new Vector3(7.6f, 0f, -7.4f), new Vector3(0f, 80f, 0f));
            DecorM(parent, bush, new Vector3(-2.4f, 0f, 10.6f), new Vector3(0f, 40f, 0f));
            DecorM(parent, rockS, new Vector3(8.6f, 0f, 5.4f), Vector3.zero);
            DecorM(parent, rockN, new Vector3(-11.6f, 0f, 5.2f), new Vector3(0f, 25f, 0f));
            DecorM(parent, RockW, new Vector3(9.4f, 0f, -6.8f), new Vector3(0f, 15f, 0f));
            int[] spots = { -12, -9, -7, -4, 4, 6, 9, 11 };
            for (int i = 0; i < spots.Length; i++)
            {
                int x = spots[i];
                int z = spots[(i * 3 + 1) % spots.Length];
                if (PlazaPath(x, z) || PlazaPath(x - 1, z) || PlazaPath(x, z - 1))
                    continue;
                DecorM(parent, Tuft, new Vector3(x + 0.3f, 0f, z - 0.2f), new Vector3(0f, i * 35f, 0f));
                if (i % 2 == 0)
                    DecorM(parent, Leaf, new Vector3(x - 0.8f, 0f, z + 0.6f), new Vector3(0f, i * 50f, 0f));
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

        /// <summary>
        /// **커밋된 씬에 남은 마을 장식 중 지역 물건 곁에 낀 것을 치운다**(멱등 보수 패스).
        /// 빌더 좌표만 고치면 안 고쳐진다 — `VillageDecor`는 전체 재드레싱 때만 다시 만들어지고,
        /// 셀프체크는 커밋된 씬을 그대로 읽는다(2026-09-09: 숲 속 울타리 11조각이 그렇게 남아 있었다).
        /// **재는 자는 하나다** — 어디까지가 「낀 것」인지는 `SliceSelfCheck`가 정하고 여기서는 치우기만 한다.
        /// </summary>
        public static int ClearDecorFromRegions()
        {
            var found = new System.Collections.Generic.List<string>();
            var nodes = new System.Collections.Generic.List<Transform>();
            SliceSelfCheck.CollectRegionIntruders(found, nodes);
            int gone = 0;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] == null)
                    continue;
                var root = nodes[i];
                while (root.parent != null)
                    root = root.parent;
                if (root.name != "VillageDecor")
                    continue;                                  // 남의 원장 물건은 그 원장이 옮긴다(여기서 지우면 원인이 숨는다)
                UnityEngine.Object.DestroyImmediate(nodes[i].gameObject);
                gone++;
            }
            if (gone > 0)
                Debug.Log("[Ulon] 지역 물건 곁에 남아 있던 마을 장식 " + gone + "개를 치웠습니다(자리는 원장이 정한다)");
            return gone;
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
            // **북쪽 방목장은 동쪽에 둔다** — 옛 자리(x 음수)는 킷 배율을 타고 북서 숲
            // (WorldRegions.Forest, 중심 (-46,50)·반경 30) **안**으로 들어가 마을 울타리가 나무
            // 사이에 서 있었다(2026-09-09 실측: 조각 14개, 가장 가까운 나무까지 1.4m).
            // 모양은 그대로 두고 x만 뒤집는다 — 동북쪽은 지역 셋 어디에도 안 걸린다.
            PlaceCurvedLoop(parent, fence, gate, new[]
            {
                new Vector3(17.8f, 0f, 17.0f), new Vector3(12.2f, 0f, 17.0f), new Vector3(7.2f, 0f, 17.4f),
                new Vector3(5.8f, 0f, 21.2f), new Vector3(8.4f, 0f, 24.8f), new Vector3(13.6f, 0f, 25.4f),
                new Vector3(18.2f, 0f, 23.0f), new Vector3(18.8f, 0f, 19.2f)
            }, 1, Vector3.zero);

            // Road gates where plaza roads leave town — openings, not a ring wall.
            DecorM(parent, gate, new Vector3(-8.2f, 0f, 0f), new Vector3(0f, 90f, 0f));
            DecorM(parent, gate, new Vector3(10.2f, 0f, 0f), new Vector3(0f, 90f, 0f));
            DecorM(parent, gate, new Vector3(0f, 0f, -7.2f), Vector3.zero);
            DecorM(parent, gate, new Vector3(0f, 0f, 11.2f), Vector3.zero);
        }

        /// <summary>집터를 두르는 ㄷ자 울타리 — **모듈 좌표**로 받는다(마을은 킷 격자 위에 설계됐다).
        /// 안에서 월드로 옮긴다: 자리도 조각 간격도 같은 배로 늘어나야 울타리가 이어 붙는다.</summary>
        static void PlaceLotU(Transform parent, string fence, string gate, float x0, float z0, float x1, float z1, int openSide, float step)
        {
            x0 *= KitScale; z0 *= KitScale; x1 *= KitScale; z1 *= KitScale; step *= KitScale;
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

        /// <summary>밭을 두르는 폐곡선 울타리 — 점들도 **모듈 좌표**다(`PlaceLotU`와 같은 규칙).</summary>
        static void PlaceCurvedLoop(Transform parent, string fence, string gate, Vector3[] pts, int gateIndex, Vector3 gateEuler)
        {
            float step = PrefabRunLength(fence) * KitScale;
            int n = pts.Length;
            var world = new Vector3[pts.Length];
            for (int i = 0; i < pts.Length; i++)
                world[i] = Module(pts[i]);
            pts = world;
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

        /// <summary>
        /// 광장 도로 — **알베도를 유도해서 정한다**(눈대중 금지).
        /// 포화의 진짜 원인은 해가 셋이었던 것(FindSun 주석)이고, 이 알베도는 그 위에 남기는 여유다.
        /// 유도(이 프로젝트는 **감마 공간**: m_ActiveColorSpace 0): 평지에서 NdotL = sin(50°) = 0.77 →
        /// 조도 = 1.18×0.77 + 0.58(ambient) = 1.48. 화면값 = 알베도 × 1.48이므로 옛 0.62는 0.92(235)로
        /// 정점 반사·텍스처 흰점이 얹히면 곧장 255다. 목표 화면값 0.80 → 알베도 = 0.80/1.48 = **0.54**.
        /// 잔디(알베도 0.32 → 0.47)와도 또렷이 갈린다.
        /// </summary>
        static Material KenneyStoneRoadMat() =>
            // 무늬는 **돌포장**(pattern 4) — 알베도 유도는 위 그대로 두고 무늬만 바꾼다.
            // 줄눈이 어두우므로 밝은 쪽을 돌, 어두운 쪽을 줄눈 그늘로 준다.
            MakeNoiseMat("KenneyStoneRoad", new Color(0.30f, 0.28f, 0.26f), new Color(0.58f, 0.56f, 0.51f), 4);

        static Material KenneyDirtMat() =>
            MakeNoiseMat("KenneyDirt", new Color(0.52f, 0.38f, 0.24f), new Color(0.40f, 0.28f, 0.16f));

        static Material MakeNoiseMat(string name, Color a, Color b)
        {
            return MakeNoiseMat(name, a, b, 0);
        }

        /// <summary>
        /// pattern 0 = 잔풀 잡음, 1 = 굵은 층리(암석), 2 = 이랑(밭), 3 = 부엽토(숲), 4 = 돌포장(광장).
        /// 풀·바위·길이 같은 무늬면 세계가 두세 색으로만 읽힌다(검수) — **생성기를 나누는 것이 지역 구분이다**.
        /// </summary>
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
                    else if (pattern == 3)
                    {
                        // 부엽토 — **낙엽 부스러기**. 잔풀 잡음(0)을 그대로 쓰면 숲 바닥이 마을 광장의
                        // 흙과 같은 무늬가 된다(검수 2026-09-09). 큰 얼룩 + 잎 조각 두 겹이라
                        // 9m로 깔아도 반복 무늬가 「문양」으로 안 읽힌다.
                        int px = x / 11, py = y / 9;
                        int blot = ((px * 48271) ^ (py * 16807)) & 255;                  // 넓은 얼룩
                        int leaf = ((x * 7 + y * 13) % 17 < 3) ? 200 : 40;               // 흩어진 잎 조각
                        int grain = (x * 199 + y * 83) & 127;
                        t = (blot / 255f) * 0.46f + (leaf / 255f) * 0.30f + (grain / 127f) * 0.24f;
                    }
                    else if (pattern == 4)
                    {
                        // 돌포장 — **광장 바닥**. 길·잔디·흙이 전부 잔풀 잡음(0)이라 십자로가 아스팔트로
                        // 읽혔다(검수 2026-09-09, 재질 실측: KenneyStoneRoad 68장·KenneyGrass 132·KenneyDirt 46
                        // 전부 같은 생성기). 돌은 **줄눈**이 있어야 돌로 읽힌다: 벽돌쌓기로 어긋난 칸 +
                        // 칸마다 다른 밝기 + 칸 경계의 어두운 줄눈 두 픽셀.
                        const int Cell = 16;                     // 128px에 8칸 → 1m 타일에 8개, 돌 하나 ≈ 12cm
                        int row = y / Cell;
                        int sx = (x + (row % 2) * (Cell / 2)) / Cell;   // 홀수 줄은 반 칸 어긋난다
                        int ix = (x + (row % 2) * (Cell / 2)) % Cell;
                        int iy = y % Cell;
                        bool joint = ix < 2 || iy < 2;           // 줄눈
                        int stone = ((sx * 73856093) ^ (row * 19349663)) & 255;
                        int grit = (x * 61 + y * 149) & 31;
                        t = joint ? 0.02f : Mathf.Clamp01(0.30f + (stone / 255f) * 0.55f + (grit / 31f) * 0.15f);
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
