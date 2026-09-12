using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ulon.Editor
{
    /// <summary>
    /// 씬 루트 원점에 겹친 가로등·아치·벤치·덤불. 묶기 전부터 있던 잔재다.
    /// 마을(VillageDecor)에 같은 종류의 제자리 배치가 있어 지워도 광장은 남지만,
    /// 삭제는 사람 결정이다. 이 자는 **아직 있는 개수**를 지키고, 없애면 빨간불이다.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        const float OriginKeepRadius = 0.5f;
        const int OriginKeepLight = 4;
        const int OriginKeepArch = 4;
        const int OriginKeepBench = 2;
        const int OriginKeepPlant = 4;

        static void AssertOriginKeep()
        {
            string reason = OriginKeepReason(true);
            if (!string.IsNullOrEmpty(reason))
                throw new InvalidOperationException(reason);
        }

        static string OriginKeepReason(bool log)
        {
            OriginKeepCount(out int light, out int arch, out int bench, out int plant);
            if (light == 0 && arch == 0 && bench == 0 && plant == 0)
                return "원점 겹침 소품을 하나도 못 찾았습니다 — 잰 것이 없습니다(0이면 실패). 삭제는 사람 결정입니다.";
            if (light < OriginKeepLight)
                return "원점 가로등 " + OriginKeepLight + "개가 있어야 하는데 " + light + "개입니다. 사람 결정 전 삭제하지 않습니다.";
            if (arch < OriginKeepArch)
                return "원점 아치 " + OriginKeepArch + "개가 있어야 하는데 " + arch + "개입니다. 사람 결정 전 삭제하지 않습니다.";
            if (bench < OriginKeepBench)
                return "원점 벤치 " + OriginKeepBench + "개가 있어야 하는데 " + bench + "개입니다. 사람 결정 전 삭제하지 않습니다.";
            if (plant < OriginKeepPlant)
                return "원점 덤불 " + OriginKeepPlant + "개가 있어야 하는데 " + plant + "개입니다. 사람 결정 전 삭제하지 않습니다.";
            if (log)
                Debug.Log("[Ulon] 원점 겹침 유지 — 가로등 " + light + " · 아치 " + arch +
                          " · 벤치 " + bench + " · 덤불 " + plant + " (삭제 안 함)");
            return "";
        }

        static void OriginKeepCount(out int light, out int arch, out int bench, out int plant)
        {
            light = 0;
            arch = 0;
            bench = 0;
            plant = 0;
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                var folder = roots[r];
                if (folder == null || !VisualSliceBuilder.IsKindFolder(folder.name))
                    continue;
                for (int i = 0; i < folder.transform.childCount; i++)
                {
                    var c = folder.transform.GetChild(i);
                    if (c == null || !c.gameObject.activeInHierarchy)
                        continue;
                    if (new Vector2(c.position.x, c.position.z).magnitude > OriginKeepRadius)
                        continue;
                    string kind = VisualSliceBuilder.SceneRootKind(c);
                    if (kind == "Light")
                        light++;
                    else if (kind == "Arch")
                        arch++;
                    else if (kind == "Seat" && c.name.IndexOf("bench", StringComparison.OrdinalIgnoreCase) >= 0)
                        bench++;
                    else if (kind == "Plant")
                        plant++;
                }
            }
        }

        static Transform OriginKeepLeaf(string kind)
        {
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            string folder = VisualSliceBuilder.KindFolderName(kind);
            for (int r = 0; r < roots.Length; r++)
            {
                if (roots[r] == null || roots[r].name != folder)
                    continue;
                for (int i = 0; i < roots[r].transform.childCount; i++)
                {
                    var c = roots[r].transform.GetChild(i);
                    if (c == null || !c.gameObject.activeInHierarchy)
                        continue;
                    if (new Vector2(c.position.x, c.position.z).magnitude > OriginKeepRadius)
                        continue;
                    if (VisualSliceBuilder.SceneRootKind(c) == kind)
                        return c;
                }
            }
            return null;
        }

        static void AssertOriginKeepNegativeControl()
        {
            Transform leaf = OriginKeepLeaf("Light") ?? OriginKeepLeaf("Arch") ??
                             OriginKeepLeaf("Seat") ?? OriginKeepLeaf("Plant");
            if (leaf == null)
                throw new InvalidOperationException("원점 겹침 NC — 가로등·아치·벤치·덤불을 못 찾았습니다(0이면 실패).");
            string before = OriginKeepReason(true);
            if (!string.IsNullOrEmpty(before))
                throw new InvalidOperationException("원점 겹침 NC 실패 — 손대기 전부터 빨간불입니다: " + before);
            bool red;
            try
            {
                leaf.gameObject.SetActive(false);
                red = !string.IsNullOrEmpty(OriginKeepReason(false));
            }
            finally
            {
                leaf.gameObject.SetActive(true);
            }
            if (!red)
                throw new InvalidOperationException("원점 겹침 NC 실패 — 하나를 껐는데 통과했습니다. 빈 통과입니다.");
            if (!string.IsNullOrEmpty(OriginKeepReason(false)))
                throw new InvalidOperationException("원점 겹침 NC 실패 — 되돌렸는데 빨간불이 남았습니다(계측이 세계를 바꿨습니다).");
            Debug.Log("[Ulon] 원점 겹침 양방향 NC 통과 — 끄면 FAIL · 되돌리면 통과");
        }
    }
}
