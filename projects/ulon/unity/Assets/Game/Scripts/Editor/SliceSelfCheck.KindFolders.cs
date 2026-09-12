using System;
using System.Collections.Generic;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// 구역 루트 직계 자식이 종류 폴더(`Kind*`)인가. 묶음 없이 소품이 루트에 흩어져 있으면 빨간불.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        static readonly string[] KindZoneRoots =
        {
            "VillageDecor", "PlainScatter", "HuntCover",
            "EastField", "SouthField", "NorthField",
            WorldRegions.TestChamberObject
        };

        static void AssertSceneKindFolders()
        {
            string reason = SceneKindFolderReason(true);
            if (!string.IsNullOrEmpty(reason))
                throw new InvalidOperationException(reason);
        }

        static string SceneKindFolderReason(bool log)
        {
            var names = new List<string>(KindZoneRoots);
            var regions = WorldRegions.All;
            for (int i = 0; i < regions.Length; i++)
            {
                if (!string.IsNullOrEmpty(regions[i].Object) && !names.Contains(regions[i].Object))
                    names.Add(regions[i].Object);
            }
            int zones = 0;
            int folders = 0;
            var loose = new List<string>();
            for (int i = 0; i < names.Count; i++)
            {
                var go = GameObject.Find(names[i]);
                if (go == null)
                    continue;
                zones++;
                int here = 0;
                foreach (Transform c in go.transform)
                {
                    if (VisualSliceBuilder.IsKindFolder(c.name))
                    {
                        if (c.childCount > 0)
                        {
                            here++;
                            folders++;
                        }
                        continue;
                    }
                    if (c.GetComponentInChildren<Renderer>(false) == null && c.childCount == 0)
                        continue;
                    loose.Add(names[i] + "/" + c.name);
                }
                if (here == 0)
                    return names[i] + " 아래 종류 폴더가 없습니다 — 잰 것이 없습니다(0이면 실패).";
            }
            if (zones == 0)
                return "종류 폴더를 둘 구역 루트를 하나도 못 찾았습니다 — 잰 것이 없습니다(0이면 실패).";
            if (loose.Count > 0)
            {
                int n = Math.Min(loose.Count, 8);
                var sample = string.Join(", ", loose.GetRange(0, n).ToArray());
                return "구역 루트에 종류 폴더가 아닌 소품 " + loose.Count + "개: " + sample;
            }
            if (log)
                Debug.Log("[Ulon] 종류 폴더 — 구역 " + zones + " · 폴더 " + folders);
            return "";
        }

        static void AssertSceneKindFoldersNegativeControl()
        {
            var decor = GameObject.Find("VillageDecor");
            if (decor == null)
                throw new InvalidOperationException("VillageDecor가 없습니다 — 잰 것이 없습니다(0이면 실패).");
            Transform folder = null;
            Transform leaf = null;
            foreach (Transform c in decor.transform)
            {
                if (!VisualSliceBuilder.IsKindFolder(c.name) || c.childCount == 0)
                    continue;
                folder = c;
                leaf = c.GetChild(0);
                break;
            }
            if (leaf == null)
                throw new InvalidOperationException("종류 폴더 안 소품이 없습니다 — NC를 못 세웁니다(0이면 실패).");
            string before = SceneKindFolderReason(true);
            if (!string.IsNullOrEmpty(before))
                throw new InvalidOperationException("종류 폴더 NC 실패 — 손대기 전부터 빨간불입니다: " + before);
            bool red;
            try
            {
                leaf.SetParent(decor.transform, true);
                red = !string.IsNullOrEmpty(SceneKindFolderReason(false));
            }
            finally
            {
                leaf.SetParent(folder, true);
            }
            if (!red)
                throw new InvalidOperationException("종류 폴더 NC 실패 — 소품을 구역 루트로 올렸는데 통과했습니다. 빈 통과입니다.");
            if (!string.IsNullOrEmpty(SceneKindFolderReason(false)))
                throw new InvalidOperationException("종류 폴더 NC 실패 — 되돌렸는데 빨간불이 남았습니다(계측이 세계를 바꿨습니다).");
            Debug.Log("[Ulon] 종류 폴더 양방향 NC 통과 — 루트로 올리면 FAIL · 되돌리면 통과");
        }

        static void AssertSceneRootActorFacilityKinds()
        {
            string reason = SceneRootKindReason(true);
            if (!string.IsNullOrEmpty(reason))
                throw new InvalidOperationException(reason);
        }

        static string SceneRootKindReason(bool log)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            var loose = new List<string>();
            int folders = 0;
            int housed = 0;
            for (int i = 0; i < roots.Length; i++)
            {
                var go = roots[i];
                if (go == null)
                    continue;
                if (VisualSliceBuilder.IsKindFolder(go.name))
                {
                    if (go.transform.childCount > 0)
                    {
                        folders++;
                        housed += go.transform.childCount;
                    }
                    continue;
                }
                if (!string.IsNullOrEmpty(VisualSliceBuilder.SceneRootKind(go.transform)))
                    loose.Add(go.name);
            }
            if (folders == 0 && housed == 0 && loose.Count == 0)
                return "씬 루트 종류 폴더를 하나도 못 찾았습니다 — 잰 것이 없습니다(0이면 실패).";
            if (loose.Count > 0)
            {
                int n = Math.Min(loose.Count, 8);
                return "씬 루트에 종류 폴더 밖 배우·시설·소품 " + loose.Count + "개: " +
                       string.Join(", ", loose.GetRange(0, n).ToArray());
            }
            if (log)
                Debug.Log("[Ulon] 씬 루트 종류 폴더 — 폴더 " + folders + " · 묶인 것 " + housed);
            return "";
        }

        static Transform SceneRootKindLeaf(params string[] kinds)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            for (int k = 0; k < kinds.Length; k++)
            {
                string want = VisualSliceBuilder.KindFolderName(kinds[k]);
                Transform folder = null;
                for (int r = 0; r < roots.Length; r++)
                {
                    if (roots[r] != null && roots[r].name == want)
                    {
                        folder = roots[r].transform;
                        break;
                    }
                }
                if (folder == null)
                    continue;
                for (int i = 0; i < folder.childCount; i++)
                {
                    var c = folder.GetChild(i);
                    if (!string.IsNullOrEmpty(VisualSliceBuilder.SceneRootKind(c)))
                        return c;
                }
            }
            return null;
        }

        static void AssertSceneRootActorFacilityKindsNegativeControl()
        {
            Transform leaf = SceneRootKindLeaf("Rock", "Plant", "Arch", "Seat", "Tree", "Light", "Cart", "Facility");
            if (leaf == null)
            {
                var named = GameObject.Find("RockLarge") ?? GameObject.Find("PlantBush") ??
                            GameObject.Find("WallArch") ?? GameObject.Find("StallBench") ??
                            GameObject.Find("Tree") ?? GameObject.Find("LanternLit") ??
                            GameObject.Find("Cart") ?? GameObject.Find("Forge");
                leaf = named != null ? named.transform : null;
            }
            if (leaf == null)
                throw new InvalidOperationException("씬 루트 종류 폴더 NC — 바위·덤불·아치·좌석·나무·가로등·수레·시설을 못 찾았습니다(0이면 실패).");
            var home = leaf.parent;
            string before = SceneRootKindReason(true);
            if (!string.IsNullOrEmpty(before))
                throw new InvalidOperationException("씬 루트 종류 폴더 NC 실패 — 손대기 전부터 빨간불입니다: " + before);
            bool red;
            try
            {
                leaf.SetParent(null, true);
                red = !string.IsNullOrEmpty(SceneRootKindReason(false));
            }
            finally
            {
                if (home != null)
                    leaf.SetParent(home, true);
            }
            if (!red)
                throw new InvalidOperationException("씬 루트 종류 폴더 NC 실패 — 시설을 씬 루트로 올렸는데 통과했습니다. 빈 통과입니다.");
            if (!string.IsNullOrEmpty(SceneRootKindReason(false)))
                throw new InvalidOperationException("씬 루트 종류 폴더 NC 실패 — 되돌렸는데 빨간불이 남았습니다(계측이 세계를 바꿨습니다).");
            Debug.Log("[Ulon] 씬 루트 종류 폴더 양방향 NC 통과 — 루트로 올리면 FAIL · 되돌리면 통과");
        }
    }
}
