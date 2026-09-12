using System;
using System.Collections.Generic;
using Ulon.Server;
using Ulon.Shared;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ulon.Editor
{
    /// <summary>
    /// 씬 구역 루트(VillageDecor·Region_* 등) 아래 소품을 **종류 폴더**로 묶는다.
    /// 구역 루트 이름은 게이트가 찾으니 바꾸지 않는다. 폴더 이름은 `Kind` 접두사다.
    /// 씬 루트 배우·시설도 같은 접두사 폴더로 묶되, 페이드는 폴더가 아니라 **자가 루트**를 본다.
    /// </summary>
    public static partial class VisualSliceBuilder
    {
        public const string KindPrefix = "Kind";

        public static bool IsKindFolder(string name)
        {
            return !string.IsNullOrEmpty(name) &&
                   name.StartsWith(KindPrefix, StringComparison.Ordinal) &&
                   name.Length > KindPrefix.Length;
        }

        public static string KindFolderName(string kind)
        {
            return KindPrefix + kind;
        }

        /// <summary>에셋·오브젝트 이름 → 종류. 폴더 자신은 빈 문자열.</summary>
        public static string KindOf(string name)
        {
            if (string.IsNullOrEmpty(name) || IsKindFolder(name))
                return "";
            string n = name.ToLowerInvariant();
            int cut = n.IndexOf(" (", StringComparison.Ordinal);
            if (cut > 0)
                n = n.Substring(0, cut);
            if (n.StartsWith("fence", StringComparison.Ordinal))
                return "Fence";
            if (n.StartsWith("tree", StringComparison.Ordinal) || n.IndexOf("oak", StringComparison.Ordinal) >= 0)
                return "Tree";
            if (n.StartsWith("rock", StringComparison.Ordinal) || n.IndexOf("rock", StringComparison.Ordinal) >= 0 ||
                n.IndexOf("ore", StringComparison.Ordinal) >= 0 || n.IndexOf("vein", StringComparison.Ordinal) >= 0)
                return "Rock";
            if (n.StartsWith("grass", StringComparison.Ordinal) || n.StartsWith("plant", StringComparison.Ordinal) ||
                n.StartsWith("hedge", StringComparison.Ordinal) || n.IndexOf("bush", StringComparison.Ordinal) >= 0 ||
                n.IndexOf("flax", StringComparison.Ordinal) >= 0 || n.IndexOf("tuft", StringComparison.Ordinal) >= 0)
                return "Plant";
            if (n.StartsWith("lantern", StringComparison.Ordinal) || n.StartsWith("torch", StringComparison.Ordinal) ||
                n.IndexOf("torch", StringComparison.Ordinal) >= 0 || n.IndexOf("light", StringComparison.Ordinal) >= 0 ||
                n.StartsWith("fountain", StringComparison.Ordinal) || n.StartsWith("campfire", StringComparison.Ordinal))
                return "Light";
            if (n.StartsWith("road", StringComparison.Ordinal))
                return "Road";
            if (n == "house" || n.StartsWith("wall", StringComparison.Ordinal) || n.StartsWith("roof", StringComparison.Ordinal) ||
                n.StartsWith("chimney", StringComparison.Ordinal) || n.StartsWith("overhang", StringComparison.Ordinal))
                return "House";
            if (n.StartsWith("stall", StringComparison.Ordinal) || n.StartsWith("market", StringComparison.Ordinal) ||
                n.StartsWith("plazamarket", StringComparison.Ordinal))
                return "Stall";
            if (n.StartsWith("cart", StringComparison.Ordinal))
                return "Cart";
            if (n.StartsWith("banner", StringComparison.Ordinal))
                return "Banner";
            if (n.StartsWith("plank", StringComparison.Ordinal))
                return "Plank";
            if (n.StartsWith("pole", StringComparison.Ordinal))
                return "Pole";
            if (n.StartsWith("stair", StringComparison.Ordinal))
                return "Stair";
            return "Misc";
        }

        /// <summary>구역·던전 방처럼 **담는 통**. 페이드·종류 묶음의 자가 루트가 아니다.</summary>
        public static bool IsZoneContainer(string name)
        {
            if (string.IsNullOrEmpty(name) || IsKindFolder(name))
                return false;
            if (name == "VillageDecor" || name == "PlainScatter" || name == "HuntCover" ||
                name == "EastField" || name == "SouthField" || name == "NorthField")
                return true;
            if (name == WorldRegions.MeadowObject || name == WorldRegions.ForestObject ||
                name == WorldRegions.MineObject || name == WorldRegions.TestChamberObject)
                return true;
            return name == Dungeon1.InteriorObject || name == Dungeon2.InteriorObject ||
                   name == Dungeon3.InteriorObject;
        }

        /// <summary>
        /// 시야 페이드가 자리를 재는 **배치 단위**. 종류 폴더·구역 통은 원점에 서서 자식 좌표를 삼킨다.
        /// 부모는 씬 루트이거나 그 통/폴더 한 겹이어야 한다(벽 조각은 집의 일부가 단위다).
        /// </summary>
        public static bool IsFadeSelfRoot(Transform t)
        {
            if (t == null)
                return false;
            if (IsKindFolder(t.name) || IsZoneContainer(t.name) || GroundFit.SkipContainer(t.name))
                return false;
            var p = t.parent;
            if (p == null)
                return true;
            return IsKindFolder(p.name) || IsZoneContainer(p.name);
        }

        /// <summary>
        /// 씬 루트에 흩어진 배우·시설·나무·수레·가로등만 종류로 묶는다.
        /// 구역 통·지형·카메라·Directional Light는 그대로 둔다. Misc는 루트에서 안 묶는다.
        /// </summary>
        public static string SceneRootKind(Transform t)
        {
            if (t == null)
                return "";
            string n = t.name;
            if (IsKindFolder(n) || IsZoneContainer(n) || GroundFit.SkipContainer(n))
                return "";
            if (RoleLook.TryGet(n, out _) || IsBuildingObject(n))
                return n == HousingPlot.HouseObject || n == "House" ? "House" : "Facility";
            if (t.GetComponent<CharacterController>() != null || t.GetComponent<WorldBody>() != null)
                return "Actor";
            string prop = KindOf(n);
            if (prop == "Tree" || prop == "Cart" || prop == "Light")
                return prop;
            return "";
        }

        public static Transform SceneKindFolder(string kind)
        {
            if (string.IsNullOrEmpty(kind))
                return null;
            string folder = KindFolderName(kind);
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null && roots[i].name == folder)
                    return roots[i].transform;
            }
            var go = new GameObject(folder);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        public static Transform KindFolder(Transform parent, string kind)
        {
            if (parent == null || string.IsNullOrEmpty(kind))
                return parent;
            string folder = KindFolderName(kind);
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c.name == folder)
                    return c;
            }
            var go = new GameObject(folder);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        public static void ParentUnderKind(Transform zone, Transform child)
        {
            if (zone == null || child == null)
                return;
            string kind = KindOf(child.name);
            if (string.IsNullOrEmpty(kind))
                return;
            var folder = KindFolder(zone, kind);
            if (child.parent == folder)
                return;
            child.SetParent(folder, true);
        }

        [MenuItem("Ulon/Organize Scene By Kind")]
        public static void OrganizeSceneByKindMenu()
        {
            int moved = EnsureSceneKindFolders();
            EditorSceneManagerMark();
            Debug.Log("[Ulon] 종류 폴더로 옮긴 소품 " + moved + "개");
        }

        static void EditorSceneManagerMark()
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        /// <summary>배치가 끝난 뒤 한 번. 이미 묶여 있으면 0.</summary>
        public static int EnsureSceneKindFolders()
        {
            int moved = 0;
            var names = new List<string>
            {
                "VillageDecor", "PlainScatter", "HuntCover",
                "EastField", "SouthField", "NorthField",
                WorldRegions.TestChamberObject
            };
            var regions = WorldRegions.All;
            for (int i = 0; i < regions.Length; i++)
            {
                if (!string.IsNullOrEmpty(regions[i].Object) && !names.Contains(regions[i].Object))
                    names.Add(regions[i].Object);
            }
            for (int i = 0; i < names.Count; i++)
            {
                var go = GameObject.Find(names[i]);
                if (go != null)
                    moved += OrganizeZone(go.transform);
            }
            moved += EnsureSceneRootActorFacilityKinds();
            if (moved > 0)
                Debug.Log("[Ulon] 씬 종류 폴더 — 옮긴 소품 " + moved + "개");
            return moved;
        }

        /// <summary>씬 루트 배우·시설·나무·수레·가로등을 Kind* 아래로. 월드 좌표 유지.</summary>
        public static int EnsureSceneRootActorFacilityKinds()
        {
            int moved = 0;
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            var list = new List<GameObject>(roots.Length);
            for (int i = 0; i < roots.Length; i++)
                list.Add(roots[i]);
            for (int i = 0; i < list.Count; i++)
            {
                var go = list[i];
                if (go == null)
                    continue;
                string kind = SceneRootKind(go.transform);
                if (string.IsNullOrEmpty(kind))
                    continue;
                var folder = SceneKindFolder(kind);
                if (folder == null || go.transform == folder || go.transform.parent == folder)
                    continue;
                go.transform.SetParent(folder, true);
                moved++;
            }
            var leftover = scene.GetRootGameObjects();
            for (int i = 0; i < leftover.Length; i++)
            {
                var go = leftover[i];
                if (go != null && IsKindFolder(go.name) && go.transform.childCount == 0)
                    UnityEngine.Object.DestroyImmediate(go);
            }
            return moved;
        }

        static int OrganizeZone(Transform root)
        {
            var kids = new List<Transform>();
            for (int i = 0; i < root.childCount; i++)
                kids.Add(root.GetChild(i));
            int moved = 0;
            for (int i = 0; i < kids.Count; i++)
            {
                var kid = kids[i];
                if (kid == null)
                    continue;
                if (IsKindFolder(kid.name))
                {
                    var inner = new List<Transform>();
                    for (int c = 0; c < kid.childCount; c++)
                        inner.Add(kid.GetChild(c));
                    for (int c = 0; c < inner.Count; c++)
                    {
                        if (inner[c] == null)
                            continue;
                        string want = KindFolderName(KindOf(inner[c].name));
                        if (want == kid.name)
                            continue;
                        ParentUnderKind(root, inner[c]);
                        moved++;
                    }
                    continue;
                }
                if (kid.parent == root)
                {
                    ParentUnderKind(root, kid);
                    moved++;
                }
            }
            var empty = new List<GameObject>();
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (IsKindFolder(c.name) && c.childCount == 0)
                    empty.Add(c.gameObject);
            }
            for (int i = 0; i < empty.Count; i++)
                UnityEngine.Object.DestroyImmediate(empty[i]);
            return moved;
        }
    }
}
