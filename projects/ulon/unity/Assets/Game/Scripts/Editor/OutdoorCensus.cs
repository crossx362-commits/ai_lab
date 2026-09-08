using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Ulon.Shared;

namespace Ulon.Editor
{
    /// <summary>
    /// **세는 도구**(검수 지시 2026-09-09 — 「먼저 세십시오」). 야외 소품이 몇 개이고 어디 있고
    /// 서로 물려 있는지만 로그로 내놓는다. **아무것도 고치지 않고 아무것도 판정하지 않는다** —
    /// 게이트는 세고 난 뒤에 만든다(무엇을 재야 하는지 모르는 채 만든 자는 엉뚱한 것을 잰다).
    /// </summary>
    public static class OutdoorCensus
    {
        [UnityEditor.MenuItem("Ulon/Count Outdoor Props")]
        public static void RunMenu() { Run(); }

        public static void Run()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != scenePath)
                EditorSceneManager.OpenScene(scenePath);

            foreach (var wb in UnityEngine.Object.FindObjectsByType<Ulon.Server.WorldBody>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (GroundFit.WorldBounds(wb.transform, out Bounds ab))
                    Debug.Log("[Census] 사람·짐승 " + wb.name + " @(" + ab.center.x.ToString("0.0") + "," +
                              ab.center.z.ToString("0.0") + ") 크기 " + ab.size.ToString("0.00"));

            var nodes = new List<Transform>();
            var boxes = new List<Bounds>();
            Collect(nodes, boxes);

            var zoneCount = new Dictionary<string, int>();
            for (int i = 0; i < nodes.Count; i++)
            {
                string z = Zone(boxes[i].center);
                zoneCount.TryGetValue(z, out int n);
                zoneCount[z] = n + 1;
            }
            var keys = new List<string>(zoneCount.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (var k in keys)
                Debug.Log("[Census] 구역 " + k + " — 소품 " + zoneCount[k] + "개");

            // 서로 물린 쌍 — 얇은 쪽 두께의 절반보다 깊게 물리면 「파고들었다」로 본다(던전 자와 같은 기준).
            var pairs = new List<string>();
            for (int a = 0; a < boxes.Count; a++)
                for (int b = a + 1; b < boxes.Count; b++)
                {
                    if (!boxes[a].Intersects(boxes[b]))
                        continue;
                    float deep = float.MaxValue;
                    for (int ax = 0; ax < 3; ax++)
                        deep = Mathf.Min(deep, Mathf.Max(0f,
                            Mathf.Min(boxes[a].max[ax], boxes[b].max[ax]) - Mathf.Max(boxes[a].min[ax], boxes[b].min[ax])));
                    float thin = Mathf.Min(Thickness(boxes[a]), Thickness(boxes[b]));
                    if (deep < thin * 0.5f)
                        continue;
                    pairs.Add(Zone(boxes[a].center) + " | " + nodes[a].name + " ↔ " + nodes[b].name +
                              " 물린 깊이 " + deep.ToString("0.00") + "m (얇은 쪽 " + thin.ToString("0.00") + "m) @(" +
                              boxes[a].center.x.ToString("0.0") + "," + boxes[a].center.z.ToString("0.0") + ")");
                }
            pairs.Sort(StringComparer.Ordinal);
            Debug.Log("[Census] 물린 쌍 " + pairs.Count + "건");
            foreach (var s in pairs)
                Debug.Log("[Census] " + s);

            // **지역 반경 안에 서 있는데 그 지역이 놓지 않은 것** — 남의 원장이 잡은 자리다.
            for (int i = 0; i < nodes.Count; i++)
            {
                string z = Zone(boxes[i].center);
                if (z == "마을" || z == "그밖")
                    continue;
                var root = nodes[i];
                while (root.parent != null)
                    root = root.parent;
                if (root.name.StartsWith("Region_", StringComparison.Ordinal))
                    continue;
                // 그 지역이 놓은 것과 얼마나 가까운가 — 「지역 안」보다 「지역 물건 사이」가 실제 증상이다.
                float near = float.MaxValue;
                string nearName = "(없음)";
                for (int j = 0; j < nodes.Count; j++)
                {
                    var jr = nodes[j];
                    while (jr.parent != null)
                        jr = jr.parent;
                    if (!jr.name.StartsWith("Region_", StringComparison.Ordinal))
                        continue;
                    float d = Vector2.Distance(new Vector2(boxes[i].center.x, boxes[i].center.z),
                                               new Vector2(boxes[j].center.x, boxes[j].center.z));
                    if (d < near) { near = d; nearName = nodes[j].name; }
                }
                Debug.Log("[Census] 남의 것 " + z + " 안에 " + root.name + "/" + nodes[i].name + " @(" +
                          boxes[i].center.x.ToString("0.0") + "," + boxes[i].center.z.ToString("0.0") +
                          ") 크기 " + boxes[i].size.ToString("0.00") + " · 그 지역 물건까지 " +
                          near.ToString("0.0") + "m(" + nearName + ")");
            }

            // 한 구역의 물건을 이름으로 못 박는다 — 어느 패스가 놓았는지 추적하려면 이름이 필요하다.
            // 볼 구역은 환경변수 `ULON_CENSUS_ZONE`로 고른다(기본 밭). 「무엇이 있나」를 세는 자리다.
            string want = Environment.GetEnvironmentVariable("ULON_CENSUS_ZONE");
            if (string.IsNullOrEmpty(want))
                want = "밭";
            for (int i = 0; i < nodes.Count; i++)
            {
                if (Zone(boxes[i].center) != want)
                    continue;
                Debug.Log("[Census] " + want + " 소품 " + nodes[i].name + " @(" + boxes[i].center.x.ToString("0.0") + "," +
                          boxes[i].center.z.ToString("0.0") + ") 크기 " + boxes[i].size.ToString("0.00") +
                          " 부모 " + (nodes[i].parent == null ? "(루트)" : nodes[i].parent.name));
            }
            if (UnityEngine.Application.isBatchMode)
                EditorApplication.Exit(0);
        }

        static float Thickness(Bounds b) => Mathf.Min(b.size.x, Mathf.Min(b.size.y, b.size.z));

        static string Zone(Vector3 p)
        {
            if (Mathf.Abs(p.x) <= VisualSliceBuilder.VillageFadeRadius &&
                Mathf.Abs(p.z) <= VisualSliceBuilder.VillageFadeRadius)
                return "마을";
            if (Near(p, WorldRegions.Meadow)) return "밭";
            if (Near(p, WorldRegions.Forest)) return "숲";
            if (Near(p, WorldRegions.Mine)) return "광산";
            if (Near(p, WorldRegions.TestChamber)) return "테스트";
            return "그밖";
        }

        static bool Near(Vector3 p, WorldRegions.Region r) =>
            new Vector2(p.x - r.X, p.z - r.Z).magnitude <= r.Radius;

        /// <summary>`CollectVillage`와 같은 규칙(묶음은 한 겹 내려간다·사람은 뺀다)이되 **마을 밖도 센다**.</summary>
        static void Collect(List<Transform> nodes, List<Bounds> boxes)
        {
            var stack = new Stack<Transform>();
            var all = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].scene.IsValid() && all[i].transform.parent == null)
                    stack.Push(all[i].transform);

            while (stack.Count > 0)
            {
                var t = stack.Pop();
                if (t.name == "Ground" || t.name.StartsWith("Terrain", StringComparison.Ordinal))
                    continue;
                if (t.GetComponentInChildren<Ulon.Server.WorldBody>(true) != null)
                    continue;
                if (t.GetComponentInChildren<Renderer>(true) == null)
                    continue;
                if (VisualSliceBuilder.IsBuildingObject(t.name))
                {
                    if (GroundFit.WorldBounds(t, out Bounds bb))
                        Debug.Log("[Census] 건물 " + t.name + " @(" + bb.center.x.ToString("0.0") + "," +
                                  bb.center.z.ToString("0.0") + ") 크기 " + bb.size.ToString("0.00"));
                    continue;
                }
                if (t.GetComponent<Renderer>() == null && t.childCount > 1)
                {
                    for (int c = 0; c < t.childCount; c++)
                        stack.Push(t.GetChild(c));
                    continue;
                }
                if (!t.gameObject.activeInHierarchy)
                    continue;
                if (!GroundFit.WorldBounds(t, out Bounds wb) || SliceSelfCheck.IsFlatMat(wb))
                    continue;
                nodes.Add(t);
                boxes.Add(wb);
            }
        }
    }
}
