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
// 이 파일이 담는 것: Env·KayKit 프리팹 빌드와 재연결 — 프리팹 자산 파이프라인.
// 동작 변경 0 — 구간을 순서 그대로 옮기기만 했다(순서를 바꾸면 주석과 몸통의 짝이 깨진다).
namespace Ulon.Editor
{
    public static partial class VisualSliceBuilder
    {

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

        /// <summary>유니티가 모델로 읽는 확장자(팩마다 배포 형식이 다르다 — FBX·OBJ 둘 다 온다).</summary>
        public static bool IsModelPath(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   (path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".obj", StringComparison.OrdinalIgnoreCase));
        }

        static string EnsureEnvPrefab(string fbxPath, bool rebuild = false)
        {
            if (!IsModelPath(fbxPath))
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
            // **지우고 새로 만들면 씬에 있던 인스턴스가 끊긴다** — 프리팹 자산을 지우는 순간 그것을 쓰던
            // 씬 오브젝트는 연결이 끊겨 이름이 프리팹 이름으로 돌아가고 붙여 둔 기능 컴포넌트를 잃는다.
            // 마을을 두 번째로 드레싱할 때 `Forge`·`Vendor`·`Healer`·주택 부지 표지·기사가 차례로
            // 사라진 원인이 이 한 줄이었다(2026-09-09 실측 — 게이트가 넷을 차례로 잡았다).
            // 같은 경로에 그대로 덮어쓰면 GUID가 유지되어 인스턴스가 살아 있는 채 내용만 갱신된다.
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
            // **지우고 새로 만들면 씬에 있던 인스턴스가 끊긴다** — 프리팹 자산을 지우는 순간 그것을 쓰던
            // 씬 오브젝트는 연결이 끊겨 이름이 프리팹 이름으로 돌아가고 붙여 둔 기능 컴포넌트를 잃는다.
            // 마을을 두 번째로 드레싱할 때 `Forge`·`Vendor`·`Healer`·주택 부지 표지·기사가 차례로
            // 사라진 원인이 이 한 줄이었다(2026-09-09 실측 — 게이트가 넷을 차례로 잡았다).
            // 같은 경로에 그대로 덮어쓰면 GUID가 유지되어 인스턴스가 살아 있는 채 내용만 갱신된다.
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefabPath;
        }
    }
}
