using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor.SceneManagement;

namespace Ulon.Editor
{
    /// <summary>
    /// **씬에 무엇이 몇 개 있는가**를 파일로 뜬다(랩 B 조건 ②, 검수 2026-09-09).
    ///
    /// 다시 구우면 **없어진 것은 화면으로 안 보인다** — 예전 전체 재빌드가 쌓인 것을 잃은 자리가 여기다.
    /// 그래서 굽기 전후로 같은 자를 대고 **이름과 개수**를 대조한다. 판정하지 않고 적기만 한다.
    /// 출력: `builds/inventory_<라벨>.txt` (이름별 개수, 이름순).
    /// </summary>
    public static class SceneInventory
    {
        public static void Run()
        {
            string label = System.Environment.GetEnvironmentVariable("ULON_INVENTORY_LABEL");
            if (string.IsNullOrEmpty(label))
                label = "scene";
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity");

            var counts = new SortedDictionary<string, int>(System.StringComparer.Ordinal);
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string name = t.name;
                counts.TryGetValue(name, out int n);
                counts[name] = n + 1;
            }
            var lines = new List<string>();
            int total = 0;
            foreach (var kv in counts)
            {
                lines.Add(kv.Value.ToString().PadLeft(5) + "  " + kv.Key);
                total += kv.Value;
            }
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../builds"));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "inventory_" + label + ".txt");
            File.WriteAllText(path, "# 오브젝트 " + total + "개 · 이름 " + counts.Count + "종\n" + string.Join("\n", lines) + "\n");
            Debug.Log("[Inventory] " + label + " — 오브젝트 " + total + "개 · 이름 " + counts.Count + "종 → " + path);
        }
    }
}
