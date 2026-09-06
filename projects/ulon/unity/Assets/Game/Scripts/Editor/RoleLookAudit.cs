using System;
using System.Collections.Generic;
using Ulon.Server;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ulon.Editor
{
    /// <summary>
    /// **감사 도구(게이트 아님)** — 「무엇이 무슨 역할이고, 지금 무슨 모델로 보이나」를 표로 찍는다.
    /// 은행이 풍차 날개, 훈련사가 마법사, 야생하트가 덤불 — 전부 같은 계열인데 하나씩 우연히 발견돼 왔다.
    /// 역할↔외형 대조표(`docs/ROLE_LOOK_TABLE.md`)의 **근거**를 사람 손이 아니라 씬에서 뽑기 위한 것이다.
    ///
    /// 실행: Unity -batchmode -nographics -quit -projectPath unity -executeMethod Ulon.Editor.RoleLookAudit.Run
    /// </summary>
    public static class RoleLookAudit
    {
        public static void Run()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.rootCount == 0)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity");

            Debug.Log("[Ulon] 역할-외형 감사 — 오브젝트 | 역할 | 표시명 | 보이는 메시 | 크기(가로×높이×세로)");
            var seen = new HashSet<GameObject>();
            Report<BankStation>(seen, s => "은행", s => s.DisplayName);
            Report<VendorStation>(seen, s => "상점", s => s.DisplayName);
            Report<CraftStation>(seen, s => "제작대", s => s.DisplayName);
            Report<TrainerStation>(seen, s => "훈련소", s => s.DisplayName);
            Report<HealerStation>(seen, s => "치유소", s => s.DisplayName);
            Report<HousePlotStation>(seen, s => "집터", s => s.DisplayName);
            Report<StableMaster>(seen, s => "마구간", s => s.DisplayName);
            Report<ResourceNode>(seen, s => "자원", s => s.ResourceId);
            Report<WorldBody>(seen, s => s.IsEnemy ? "적" : (s.IsAvatar ? "플레이어" : "사람/생물"), s => s.DisplayName);
        }

        static void Report<T>(HashSet<GameObject> seen, Func<T, string> role, Func<T, string> label) where T : MonoBehaviour
        {
            var all = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                var go = all[i].gameObject;
                if (!seen.Add(go))
                    continue;
                Debug.Log("[Ulon] 역할 " + go.name + " | " + Safe(() => role(all[i])) + " | " + Safe(() => label(all[i])) +
                          " | " + Mesh(go) + " | " + Size(go));
            }
        }

        static string Safe(Func<string> f)
        {
            try { string s = f(); return string.IsNullOrEmpty(s) ? "(없음)" : s; }
            catch { return "(못 읽음)"; }
        }

        static string Mesh(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>(true);
            var names = new List<string>();
            for (int i = 0; i < rends.Length; i++)
            {
                if (!rends[i].enabled || !rends[i].gameObject.activeInHierarchy)
                    continue;
                var mf = rends[i].GetComponent<MeshFilter>();
                Mesh m = mf != null ? mf.sharedMesh : null;
                if (m == null && rends[i] is SkinnedMeshRenderer smr)
                    m = smr.sharedMesh;
                if (m == null)
                    continue;
                string path = UnityEditor.AssetDatabase.GetAssetPath(m);
                string shortPath = string.IsNullOrEmpty(path) ? m.name : System.IO.Path.GetFileName(path);
                if (!names.Contains(shortPath))
                    names.Add(shortPath);
                if (names.Count >= 4)
                    break;
            }
            return names.Count == 0 ? "(메시 없음)" : string.Join("+", names);
        }

        static string Size(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>(true);
            bool any = false;
            Bounds b = new Bounds();
            for (int i = 0; i < rends.Length; i++)
            {
                if (!rends[i].enabled || !rends[i].gameObject.activeInHierarchy)
                    continue;
                if (!any) { b = rends[i].bounds; any = true; }
                else b.Encapsulate(rends[i].bounds);
            }
            return any ? b.size.ToString("0.0") : "(없음)";
        }
    }
}
