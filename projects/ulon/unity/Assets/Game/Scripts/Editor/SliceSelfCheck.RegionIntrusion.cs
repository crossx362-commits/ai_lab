using System;
using System.Collections.Generic;
using UnityEngine;
using Ulon.Shared;

namespace Ulon.Editor
{
    /// <summary>
    /// **지역 안에는 그 지역이 놓은 것만 선다**(검수 지시 2026-09-09 — 「밭 한가운데 소품 무더기」).
    ///
    /// 세어 보니 무더기의 정체는 **문게이트**였다: 시설 원장(`TravelGate`)이 모듈 좌표로 잡은 자리가
    /// 킷 배율(×2.1)을 타고 남동쪽으로 밀려나, 나중에 생긴 농경지(`WorldRegions.Meadow`) 한가운데
    /// 작물 이랑 위에 섰다. 마을 울타리 한 폐곡선도 같은 이유로 북서 숲 속에 들어가 있었다.
    /// **원장 둘이 서로를 모르면 좌표는 언젠가 겹친다** — 그래서 이 자는 「누가 놓았나」를 본다:
    /// 뿌리가 `Region_*`가 아닌 배치물이 **그 지역이 놓은 물건 곁에** 서 있으면 빨간불이다.
    ///
    /// 반경으로 재지 않는 이유(실측): 지역 반경은 도포 범위라 넓어서, 반경만 보면 던전 2 입구·옛
    /// 북쪽 밭까지 34건이 걸린다 — 화면에서 아무 문제도 아닌 것들이다. **증상은 「지역 안」이 아니라
    /// 「지역 물건 사이」였다**: 문게이트는 작물에서 1.6m, 숲 속 마을 울타리는 나무에서 1.4m.
    ///
    /// 사람·짐승은 지나다니는 것이라 대상이 아니고(같은 이유로 다른 자들도 뺀다), 바닥 깔개도 뺀다.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// 남의 배치물이 지역 물건에 이보다 가까우면 「끼어들었다」로 본다.
        /// 유도(실측에서): 화면에서 틀린 둘은 **작물에서 1.6m·나무에서 1.4m**였고, 결백한 이웃들
        /// (경계의 풀·덤불·작은 돌 4건)은 **2.1~2.7m**였다. 그 사이인 2.0m를 자로 쓴다 —
        /// 이랑 간격(2.1m)보다 좁으니 「한 이랑 안에 남의 것이 섰다」와 같은 말이다.
        /// **가장 가까운 이웃 거리도 매 판 찍는다**(아래) — 자가 언제부터 아슬아슬해졌는지 보이라고.
        /// </summary>
        const float RegionIntrusionRange = 2.0f;

        /// <summary>가장 가까웠던 남의 배치물 — 걸리지 않은 것까지 찍어야 자가 아슬아슬한지 보인다.</summary>
        static float NearestOutsider;
        static string NearestOutsiderWhat = "(없음)";

        /// <summary>지역 물건 곁에 선 남의 배치물을 모은다(자는 재기만 한다). 굽는 쪽도 이 자를 부른다.</summary>
        internal static void CollectRegionIntruders(List<string> found, List<Transform> nodes = null)
        {
            NearestOutsider = float.MaxValue;
            NearestOutsiderWhat = "(없음)";
            var stack = new Stack<Transform>();
            var all = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].scene.IsValid() && all[i].transform.parent == null)
                    stack.Push(all[i].transform);

            // 먼저 **그 지역이 놓은 것**을 모은다 — 무엇에 끼어들었는지 재려면 기준이 있어야 한다.
            var mine = new List<Vector3>();
            var mineOwner = new List<string>();
            var mineName = new List<string>();
            var regions = WorldRegions.All;
            for (int r = 0; r < regions.Length; r++)
            {
                var root = GameObject.Find(regions[r].Object);
                if (root == null)
                    continue;
                foreach (var t in root.GetComponentsInChildren<Transform>(false))
                {
                    if (t.GetComponent<Renderer>() == null)
                        continue;
                    // **지역이 놓은 짐승은 기준이 아니다** — 몹은 돌아다니는 것이라 그 곁을 비워 둘
                    // 이유가 없다(실측: 「숲의 Visual에서 2.1m」로 걸린 넷은 전부 사슴이었다).
                    if (t.GetComponentInParent<Ulon.Server.WorldBody>() != null)
                        continue;
                    if (!GroundFit.WorldBounds(t, out Bounds b) || IsFlatMat(b))
                        continue;
                    mine.Add(b.center);
                    mineOwner.Add(regions[r].Name);
                    mineName.Add(t.name);
                }
            }
            if (mine.Count == 0)
                throw new InvalidOperationException("지역이 놓은 물건을 하나도 못 읽었습니다 — 잰 것이 없습니다(0이면 실패).");

            while (stack.Count > 0)
            {
                var t = stack.Pop();
                if (t.name == "Ground" || t.name.StartsWith("Terrain", StringComparison.Ordinal))
                    continue;
                if (t.GetComponentInChildren<Ulon.Server.WorldBody>(true) != null)
                    continue;                                   // 사람·짐승은 지나다니는 것이다
                if (t.GetComponentInChildren<Renderer>(true) == null)
                    continue;
                if (t.GetComponent<Renderer>() == null && t.childCount > 1)
                {
                    for (int c = 0; c < t.childCount; c++)
                        stack.Push(t.GetChild(c));
                    continue;
                }
                if (!t.gameObject.activeInHierarchy)
                    continue;
                var root = t;
                while (root.parent != null)
                    root = root.parent;
                if (root.name.StartsWith("Region_", StringComparison.Ordinal))
                    continue;                                   // 그 지역이 놓은 것은 그 지역 것이다
                if (!GroundFit.WorldBounds(t, out Bounds wb) || IsFlatMat(wb))
                    continue;
                float best = float.MaxValue;
                int hit = -1;
                for (int m = 0; m < mine.Count; m++)
                {
                    float d = new Vector2(wb.center.x - mine[m].x, wb.center.z - mine[m].z).magnitude;
                    if (d < best) { best = d; hit = m; }
                }
                if (hit < 0)
                    continue;
                if (best < NearestOutsider)
                {
                    NearestOutsider = best;
                    NearestOutsiderWhat = root.name + "/" + t.name + " ↔ " + mineOwner[hit] + "의 " + mineName[hit];
                }
                if (best > RegionIntrusionRange)
                    continue;
                found.Add(mineOwner[hit] + "의 " + mineName[hit] + "에서 " + best.ToString("0.0") + "m 앞에 " +
                          root.name + "/" + t.name + " @(" + wb.center.x.ToString("0.0") + "," +
                          wb.center.z.ToString("0.0") + ")");
                if (nodes != null)
                    nodes.Add(t);
            }
        }

        /// <summary>
        /// **던전 입구 마당을 지역이 덮지 않았는가** — 반대 방향의 같은 병이다.
        /// 실측(2026-09-09): 검수가 「밭 한가운데 소품 무더기」로 잡은 것은 던전 2 입구였고,
        /// 농경지 셋째 뙈기가 그 위를 덮고 있었다. 그러니 「남의 것이 들어왔나」만 재면 절반만 보는 것이다.
        /// 마당 반경은 굽는 쪽과 **같은 값**(`VisualSliceBuilder.LandmarkYard`)을 읽는다 — 자를 두 벌 두지 않는다.
        /// </summary>
        static void CollectMouthCoverers(List<string> found)
        {
            var mouths = new[]
            {
                new Vector3(Dungeon1.EntranceX, 0f, Dungeon1.EntranceZ),
                new Vector3(Dungeon2.EntranceX, 0f, Dungeon2.EntranceZ),
                new Vector3(Dungeon3.EntranceX, 0f, Dungeon3.EntranceZ),
            };
            var regions = WorldRegions.All;
            for (int r = 0; r < regions.Length; r++)
            {
                var root = GameObject.Find(regions[r].Object);
                if (root == null)
                    continue;
                foreach (var t in root.GetComponentsInChildren<Transform>(false))
                {
                    if (t.GetComponent<Renderer>() == null)
                        continue;
                    if (t.GetComponentInParent<Ulon.Server.WorldBody>() != null)
                        continue;
                    if (!GroundFit.WorldBounds(t, out Bounds b) || IsFlatMat(b))
                        continue;
                    for (int m = 0; m < mouths.Length; m++)
                    {
                        float d = new Vector2(b.center.x - mouths[m].x, b.center.z - mouths[m].z).magnitude;
                        if (d >= VisualSliceBuilder.LandmarkYard)
                            continue;
                        found.Add("던전 입구(" + mouths[m].x.ToString("0.0") + "," + mouths[m].z.ToString("0.0") +
                                  ") 마당 " + VisualSliceBuilder.LandmarkYard.ToString("0.0") + "m 안에 " +
                                  regions[r].Name + "의 " + t.name + " — " + d.ToString("0.0") + "m");
                        break;
                    }
                }
            }
        }

        static string RegionIntrusionReason(bool log)
        {
            var found = new List<string>();
            CollectRegionIntruders(found);
            CollectMouthCoverers(found);
            if (log)
                Debug.Log("[Ulon] 지역 침입 — 지역 " + WorldRegions.All.Length + "곳 · 물건 곁(" +
                          RegionIntrusionRange.ToString("0.0") + "m)에 낀 남의 배치물 " + found.Count +
                          "개 · 가장 가까운 이웃 " +
                          (NearestOutsider == float.MaxValue ? "(없음)" : NearestOutsider.ToString("0.0") + "m " + NearestOutsiderWhat));
            if (found.Count == 0)
                return "";
            found.Sort(StringComparer.Ordinal);
            return "지역이 놓은 물건 곁에 남의 배치물이 서 있습니다 — " + found.Count + "건:\n  " +
                   string.Join("\n  ", found.GetRange(0, Mathf.Min(found.Count, 8))) +
                   (found.Count > 8 ? "\n  … 외 " + (found.Count - 8) + "건" : "") +
                   "\n원장이 둘이면 좌표는 언젠가 겹칩니다 — 놓는 쪽 원장의 자리를 옮기십시오(§8.2).";
        }

        static void AssertNoRegionIntrusion()
        {
            string reason = RegionIntrusionReason(true);
            if (!string.IsNullOrEmpty(reason))
                throw new InvalidOperationException(reason);
        }

        /// <summary>양방향 NC — 마을 소품 하나를 지역 한가운데로 옮기면 빨간불, 되돌리면 초록.</summary>
        static void AssertNoRegionIntrusionNegativeControl()
        {
            var props = new List<Transform>();
            var boxes = new List<Bounds>();
            var n1 = new List<string>();
            var b1 = new List<Bounds>();
            CollectVillage(props, boxes, n1, b1);
            // **희생양은 정해서 고른다** — 씬 순회 순서로 뽑으면 판마다 다른 것을 겨눠 초록도 빨간불도
            // 우연이 된다(「무작위로 뽑은 희생양은 NC가 아니다」).
            Transform victim = null;
            for (int i = 0; i < props.Count; i++)
                if (props[i] != null && !IsFlatMat(boxes[i]) &&
                    (victim == null || string.CompareOrdinal(props[i].name, victim.name) < 0))
                    victim = props[i];
            if (victim == null)
                throw new InvalidOperationException("지역 침입 NC 대상 소품이 없습니다(0이면 실패).");

            var kept = victim.position;
            var r = WorldRegions.Meadow;
            bool red;
            try
            {
                victim.position = new Vector3(r.X, victim.position.y, r.Z);   // 지역 한가운데 = 그 지역 물건들 사이
                Physics.SyncTransforms();
                red = !string.IsNullOrEmpty(RegionIntrusionReason(false));
            }
            finally
            {
                victim.position = kept;
                Physics.SyncTransforms();
            }
            if (!red)
                throw new InvalidOperationException("지역 침입 네거티브 컨트롤 실패 — " + victim.name +
                    "을(를) " + r.Name + " 한가운데로 옮겼는데 통과했습니다.");
            string back = RegionIntrusionReason(false);
            if (!string.IsNullOrEmpty(back))
                throw new InvalidOperationException("지역 침입 네거티브 컨트롤 실패 — 되돌렸는데도 빨간불입니다: " + back);
            Debug.Log("[Ulon] 지역 침입 양방향 NC 통과 — 마을 소품을 지역에 넣으면 FAIL · 되돌리면 통과");
        }
    }
}
