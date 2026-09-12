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
    }
}
