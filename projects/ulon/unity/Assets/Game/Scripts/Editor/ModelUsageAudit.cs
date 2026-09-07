using System.Collections.Generic;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **모델 겹침 감사 도구**(게이트 아님, 검수 랩 ③ 남은 항목 2026-09-07).
    ///
    /// 「도적이 `Mage.fbx`」를 고치기 전에 **겹침이 어디로 옮겨가는지 먼저 잰다**(검수:
    /// 「결함이 사라진 게 아니라 옮겨갔을 수 있다」). 씬의 사람형 전수를 돌며
    /// 모델 · 몸 색 · 든 것을 찍고, **같은 모델을 쓰는 무리**를 묶어 보여 준다.
    /// 색이나 든 것이 다르면 화면에서는 갈린다 — 그래서 모델만 세지 않고 셋을 같이 찍는다.
    ///
    /// Unity -batchmode -nographics -quit -projectPath unity \
    ///   -executeMethod Ulon.Editor.ModelUsageAudit.Run -logFile unity/Logs/model_usage.log
    /// </summary>
    public static class ModelUsageAudit
    {
        public static void Run()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity");
            var byModel = new Dictionary<string, List<string>>();
            var actors = Object.FindObjectsByType<CharacterController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < actors.Length; i++)
            {
                var go = actors[i].gameObject;
                string model = MobArt.ModelOf(go, out MobArt.Model m, out _) ? m.Prefix : "(원장 밖)";
                var color = VillagerLook.BodyColor(go, out _);
                var gear = VillagerLook.VisibleGear(go);
                string line = go.name + " [" + PersonLookAudit.ColorText(color) + " / " +
                              (gear.Count == 0 ? "맨손" : string.Join("+", gear)) + "]";
                if (!byModel.TryGetValue(model, out var list))
                    byModel[model] = list = new List<string>();
                list.Add(line);
            }
            Debug.Log("[Ulon] 모델 겹침 감사 — 사람형 " + actors.Length + "체, 모델 " + byModel.Count + "종");
            foreach (var kv in byModel)
            {
                Debug.Log("[Ulon] 모델 " + kv.Key + " ← " + kv.Value.Count + "체: " + string.Join(", ", kv.Value));
                // **같은 모델 + 같은 색 + 같은 장비**만이 진짜 겹침이다(화면에서 안 갈린다).
                var seen = new Dictionary<string, string>();
                for (int i = 0; i < kv.Value.Count; i++)
                {
                    int b = kv.Value[i].IndexOf('[');
                    string sig = b >= 0 ? kv.Value[i].Substring(b) : kv.Value[i];
                    string who = b >= 0 ? kv.Value[i].Substring(0, b).Trim() : kv.Value[i];
                    if (seen.TryGetValue(sig, out string had))
                        Debug.Log("[Ulon] **화면에서 같은 것** " + had + " ↔ " + who + " — " + kv.Key + " " + sig);
                    else
                        seen[sig] = who;
                }
            }
        }
    }
}
