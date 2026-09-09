using System.Collections.Generic;
using UnityEngine;
using Ulon.Shared;

namespace Ulon.Editor
{
    /// <summary>
    /// **입구 돌길이 「땅」인가 「깔개」인가를 센다**(검수 관찰 2026-09-09: 「`07`의 돌길이 풀밭 위에
    /// 얹힌 갈색 깔개로 읽힌다」). 고치지 않는다 — 세기만 한다.
    ///
    /// 무엇을 묻는지 먼저 적는다. 「깔개」는 세 가지가 겹쳐 나는 인상이라 셋을 따로 잰다:
    /// ①**얹혔나** — 타일 밑면이 지표에서 얼마나 떠 있나(뜨면 옆면에 그림자 선이 생겨 판때기가 된다),
    /// ②**한 장인가** — 이웃 타일이 겹쳐 이어 붙어 한 판이 되나(가장자리가 자로 그은 직선이 된다),
    /// ③**얼마나 차지하나** — QA 눈에서 그 타일들이 화면의 몇 %를 먹나(실루엣, `Screen.cs`의 자를 재사용).
    ///
    /// **이 자가 못 보는 것**: 색이다. 갈색 판인지 회색 돌인지는 여기서 안 묻는다 — 화면(PNG)이 답한다.
    /// 이 자가 말할 수 있는 것은 「무엇이 어디에 어떤 크기로 놓였나」까지다.
    /// </summary>
    public static partial class EntranceCensus
    {
        /// <summary>입구 돌길 타일의 이름 앞머리 — 킷 모델 이름 그대로 씬에 선다.</summary>
        public const string PathTilePrefix = "ground_pathTile";

        public static void RunPathCensus()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            PathReport("07/D1", Dungeon1.RootObject, Dungeon1.EntranceX, Dungeon1.EntranceZ);
            PathReport("09/D2", Dungeon2.RootObject, Dungeon2.EntranceX, Dungeon2.EntranceZ);
            PathReport("11/D3", Dungeon3.RootObject, Dungeon3.EntranceX, Dungeon3.EntranceZ);
            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(0);
        }

        /// <summary>
        /// **화면 아래쪽을 채운 것이 무엇인가** — 눈에서 격자로 광선을 쏘아 맞은 것의 이름을 센다.
        /// 「돌길」이라 부르던 것이 정말 돌길 타일인지부터 확인하려고 세운다(이름으로 짐작하지 않는다).
        /// </summary>
        public static void RunPathProbe()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            ProbeScreen("07/D1", Dungeon1.EntranceX, Dungeon1.EntranceZ);
            ProbeScreen("09/D2", Dungeon2.EntranceX, Dungeon2.EntranceZ);
            ProbeScreen("11/D3", Dungeon3.EntranceX, Dungeon3.EntranceZ);
            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(0);
        }

        static void ProbeScreen(string tag, float ex, float ez)
        {
            ShotEye(ex, ez, out Vector3 eye, out Vector3 look);
            var fwd = (look - eye).normalized;
            var right = Vector3.Cross(Vector3.up, fwd).normalized;
            var up = Vector3.Cross(fwd, right);
            float tanY = Mathf.Tan(ShotFov * 0.5f * Mathf.Deg2Rad), tanX = tanY * (ScreenW / (float)ScreenH);
            var count = new Dictionary<string, int>();
            int total = 0;
            for (int r = 0; r < 90; r++)
                for (int c = 0; c < 160; c++)
                {
                    float sx = (c + 0.5f) / 160f * 2f - 1f, sy = 1f - (r + 0.5f) / 90f * 2f;
                    var dir = (fwd + right * (sx * tanX) + up * (sy * tanY)).normalized;
                    total++;
                    if (!Physics.Raycast(eye, dir, out RaycastHit hit, 400f))
                        continue;
                    string n = hit.transform.name;
                    count[n] = count.TryGetValue(n, out int v) ? v + 1 : 1;
                }
            var names = new List<KeyValuePair<string, int>>(count);
            names.Sort((a, b) => b.Value.CompareTo(a.Value));
            var parts = new List<string>();
            for (int i = 0; i < names.Count && i < 8; i++)
                parts.Add(names[i].Key + " " + (100f * names[i].Value / total).ToString("0.0") + "%");
            Debug.Log("[Ulon] 화면 광선 " + tag + " — " + string.Join(" · ", parts));
        }

        /// <summary>
        /// **갈색 띠가 무슨 겹인가, 가장자리가 어떻게 끝나나** — 지표 규칙(`WorldSplat.CoverAt`)을
        /// 입구 앞에서 직접 읽는다. 화면의 색은 겹이 정하고, 「깔개」라는 인상은 **가장자리**가 정한다.
        /// </summary>
        public static void RunPathSplat()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            SplatProfile("07/D1", Dungeon1.EntranceX, Dungeon1.EntranceZ);
            SplatProfile("09/D2", Dungeon2.EntranceX, Dungeon2.EntranceZ);
            SplatProfile("11/D3", Dungeon3.EntranceX, Dungeon3.EntranceZ);
            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(0);
        }

        static readonly string[] LayerNames =
            { "풀", "바위", "모래", "갈이흙", "부엽토", "자갈", "**길**", "돌포장", "마른풀", "암면" };

        static void SplatProfile(string tag, float ex, float ez)
        {
            // 입구 둘레 24m를 1m 격자로 훑어 **겹별 몫**을 낸다 — 무엇이 갈색인지부터 이름으로 안다.
            var share = new Dictionary<int, int>();
            int n = 0;
            for (float dx = -12f; dx <= 12f; dx += 1f)
                for (float dz = -12f; dz <= 12f; dz += 1f)
                {
                    int l = WorldSplat.CoverAt(ex + dx, ez + dz, out float w);
                    int key = (l < 0 || w < 0.5f) ? WorldSplat.Grass : l;   // 약하게 덮인 자리는 풀로 읽는다
                    share[key] = share.TryGetValue(key, out int v) ? v + 1 : 1;
                    n++;
                }
            var parts = new List<string>();
            foreach (var kv in share)
                parts.Add((kv.Key < LayerNames.Length ? LayerNames[kv.Key] : kv.Key.ToString()) +
                          " " + (100f * kv.Value / n).ToString("0") + "%");
            // 가장자리 단면 — 띠를 가로질러 1m씩 걸으며 세기를 찍는다(0으로 떨어지는 **모양**이 인상을 만든다).
            var prof = new List<string>();
            for (float d = -10f; d <= 10f; d += 1f)
            {
                int l = WorldSplat.CoverAt(ex + d, ez, out float w);
                prof.Add((l < 0 ? "-" : (l < LayerNames.Length ? LayerNames[l] : l.ToString())) + w.ToString("0.00"));
            }
            Debug.Log("[Ulon] 지표 셈 " + tag + " — 입구 둘레 24m: " + string.Join(" · ", parts) +
                      "\n  x 단면(−10~+10m): " + string.Join(" ", prof));
        }

        static void PathReport(string tag, string rootName, float ex, float ez)
        {
            var root = GameObject.Find(rootName);
            if (root == null)
            {
                Debug.Log("[Ulon] 돌길 셈 " + tag + " — 루트 " + rootName + " 없음");
                return;
            }
            var tiles = FindChildren(root.transform, PathTilePrefix);
            if (tiles.Count == 0)
            {
                Debug.Log("[Ulon] 돌길 셈 " + tag + " — 타일 0개(이름 앞머리 " + PathTilePrefix + ")");
                return;
            }
            ShotEye(ex, ez, out Vector3 eye, out Vector3 look);
            var sil = Union(tiles, eye, look);
            // 타일 하나하나의 상자와 지표
            var line = new List<string>();
            Bounds all = new Bounds();
            bool got = false;
            for (int i = 0; i < tiles.Count; i++)
            {
                if (!Box(tiles[i], out Bounds b))
                    continue;
                if (!got) { all = b; got = true; }
                else all.Encapsulate(b);
                float gy = GroundAt(new Vector3(b.center.x, b.max.y + 50f, b.center.z));
                line.Add(tiles[i].name + " 크기 " + b.size.ToString("0.00") +
                         " 밑면 " + (b.min.y - gy).ToString("+0.00;-0.00") + "m");
            }
            // 이웃 사이 틈 — 음수면 겹쳐서 한 판이 된다
            var gaps = new List<string>();
            for (int i = 1; i < tiles.Count; i++)
            {
                if (!Box(tiles[i - 1], out Bounds a) || !Box(tiles[i], out Bounds b))
                    continue;
                float center = Vector3.Distance(new Vector3(a.center.x, 0f, a.center.z),
                                                new Vector3(b.center.x, 0f, b.center.z));
                float half = (a.size.x + a.size.z) * 0.25f + (b.size.x + b.size.z) * 0.25f;
                gaps.Add((center - half).ToString("+0.00;-0.00"));
            }
            Debug.Log("[Ulon] 돌길 셈 " + tag + " — 타일 " + tiles.Count + "개 · **화면 몫 " +
                      (100f * sil.Pixels / (float)(ScreenW * ScreenH)).ToString("0.0") + "%**(" + sil.Pixels + "px) · " +
                      "덮은 땅 " + all.size.x.ToString("0.0") + "×" + all.size.z.ToString("0.0") + "m\n  " +
                      string.Join("\n  ", line) + "\n  이웃 틈(음수=겹침): " + string.Join(" · ", gaps));
        }

        /// <summary>
        /// **입구 둘레에 무엇이 서 있나** — 던전 문 앞이 남의 땅(밭·숲·광산) 한복판인가를 센다
        /// (`09`에서 문틀이 밭 이랑 한복판에 서 있다는 관찰, 2026-09-09). 고치지 않는다 — 세기만.
        /// 지역 거리는 **중심과 반경**으로, 소품은 **이름 무리**로 센다(이름 하나하나는 못 읽는다).
        /// </summary>
        public static void RunEntranceSurround()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            Surround("07/D1", Dungeon1.EntranceX, Dungeon1.EntranceZ);
            Surround("09/D2", Dungeon2.EntranceX, Dungeon2.EntranceZ);
            Surround("11/D3", Dungeon3.EntranceX, Dungeon3.EntranceZ);
            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(0);
        }

        static void Surround(string tag, float ex, float ez)
        {
            var regions = WorldRegions.All;
            var reg = new List<string>();
            for (int i = 0; i < regions.Length; i++)
            {
                float d = new Vector2(ex - regions[i].X, ez - regions[i].Z).magnitude;
                reg.Add(regions[i].Name + " " + d.ToString("0.0") + "m/반경 " + regions[i].Radius.ToString("0") +
                        (d <= regions[i].Radius ? " **안**" : ""));
            }
            // 둘레 12m 안의 소품을 이름 앞머리로 묶어 센다(같은 킷 모델은 뒤에 숫자가 붙는다).
            var groups = new Dictionary<string, int>();
            foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var t = r.transform;
                if (t.root.name == "Terrain" || t.name == "Ground")
                    continue;
                var p = t.position;
                if (new Vector2(p.x - ex, p.z - ez).magnitude > 12f)
                    continue;
                string n = t.root.name == t.name ? t.name : t.root.name + "/" + t.name;
                n = n.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
                groups[n] = groups.TryGetValue(n, out int v) ? v + 1 : 1;
            }
            var list = new List<KeyValuePair<string, int>>(groups);
            list.Sort((a, b) => b.Value.CompareTo(a.Value));
            var parts = new List<string>();
            for (int i = 0; i < list.Count && i < 10; i++)
                parts.Add(list[i].Key + "×" + list[i].Value);
            Debug.Log("[Ulon] 입구 둘레 " + tag + " — 지역: " + string.Join(" · ", reg) +
                      "\n  12m 안 소품: " + string.Join(" · ", parts));
        }

        /// <summary>
        /// **한 입구 앞에 선 남의 물건을 하나씩 센다** — 무리로만 세면 「일곱 개」까지만 알고
        /// 그것이 화면에서 무엇을 하는지는 모른다(검수 관찰: `11` 입구 12m 안에 집터 소품 일곱).
        /// 거리·크기와 함께 **QA 눈에서의 화면 픽셀**을 같이 찍는다 — 화면에 안 나오는 물건은
        /// 배치 이야기이지 그림 이야기가 아니다.
        /// </summary>
        public static void RunEntranceNeighbors()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            Neighbors("11/D3", Dungeon3.RootObject, Dungeon3.EntranceX, Dungeon3.EntranceZ);
            Neighbors("07/D1", Dungeon1.RootObject, Dungeon1.EntranceX, Dungeon1.EntranceZ);
            Neighbors("09/D2", Dungeon2.RootObject, Dungeon2.EntranceX, Dungeon2.EntranceZ);
            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(0);
        }

        static void Neighbors(string tag, string rootName, float ex, float ez)
        {
            ShotEye(ex, ez, out Vector3 eye, out Vector3 look);
            var mine = GameObject.Find(rootName);
            var lines = new List<string>();
            foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var t = r.transform;
                if (t.root.name == "Terrain" || t.name == "Ground") continue;
                if (mine != null && t.IsChildOf(mine.transform)) continue;   // 이 던전의 제 물건은 남이 아니다
                float d = new Vector2(t.position.x - ex, t.position.z - ez).magnitude;
                if (d > 12f) continue;
                var sil = Draw(t, eye, look);
                lines.Add(t.root.name + "/" + t.name + " " + d.ToString("0.0") + "m 크기 " +
                          r.bounds.size.ToString("0.0") + " 화면 " +
                          (100f * sil.Pixels / (ScreenW * ScreenH)).ToString("0.0") + "%");
            }
            Debug.Log("[Ulon] 입구 이웃 " + tag + " — 12m 안 남의 물건 " + lines.Count + "개\n  " +
                      string.Join("\n  ", lines));
        }

        /// <summary>이 자리의 지표 높이 — 위에서 쏴서 가장 낮은 것(같은 규칙을 `ShotEye`도 쓴다).</summary>
        static float GroundAt(Vector3 above)
        {
            var hits = Physics.RaycastAll(above, Vector3.down, 1000f);
            float gy = float.MaxValue;
            for (int i = 0; i < hits.Length; i++) gy = Mathf.Min(gy, hits[i].point.y);
            return gy == float.MaxValue ? 0f : gy;
        }

        /// <summary>켜진 렌더러만으로 상자를 잰다 — 꺼진 것은 화면에 없다(`Draw`와 같은 규칙).</summary>
        static bool Box(Transform t, out Bounds box)
        {
            box = new Bounds();
            bool any = false;
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled || r is ParticleSystemRenderer)
                    continue;
                if (!any) { box = r.bounds; any = true; }
                else box.Encapsulate(r.bounds);
            }
            return any;
        }
    }
}
