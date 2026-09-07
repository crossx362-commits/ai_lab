using System.Collections.Generic;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **마을 사람 외형 감사 도구**(게이트 아님, 검수 랩 ③사람 착수 2026-09-07).
    ///
    /// 「5역할이 서로 다른 모습인가」를 고치기 전에 **지금 무엇이 같은지를 먼저 잰다**.
    /// 역할마다 ① 어떤 모델인지 ② 몸 재질의 색 ③ 모델 안에 들어 있는 장비 노드가 켜졌는지 꺼졌는지를 찍는다.
    /// ③이 이 감사의 핵심이다 — KayKit 모델은 검·지팡이·단검을 **FBX 안에** 품고 있어서
    /// 새 파일을 안 받아도 역할을 손에 들려 구분할 수 있다(그 목록을 눈으로 봐야 고를 수 있다).
    ///
    /// Unity -batchmode -nographics -quit -projectPath unity \
    ///   -executeMethod Ulon.Editor.PersonLookAudit.Run -logFile unity/Logs/person_look.log
    /// </summary>
    public static class PersonLookAudit
    {
        public static void Run()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity");
            var people = VillagerLook.Villagers();
            Debug.Log("[Ulon] 사람 외형 감사 — 마을 역할 사람 " + people.Count + "명");
            foreach (var go in people)
            {
                string model = MobArt.ModelOf(go, out MobArt.Model m, out string via) ? m.Prefix + "(" + via + ")" : "(원장 밖)";
                Color body = VillagerLook.BodyColor(go, out string bodyVia);
                var on = new List<string>();
                var off = new List<string>();
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                {
                    if (!VisualSliceBuilder.IsGearName(t.name))
                        continue;
                    var r = t.GetComponent<Renderer>();
                    bool visible = r != null && r.enabled && t.gameObject.activeInHierarchy;
                    (visible ? on : off).Add(t.name);
                }
                // **바운드에 무엇이 들어가는가** — 근접 샷 거리가 사람 키에서 나오므로 여기서 새면
                // 카메라가 뒤로 물러나 사람이 콩알이 된다(첫 촬영본 47).
                foreach (var r in go.GetComponentsInChildren<Renderer>(false))
                    if (r.enabled)
                        Debug.Log("[Ulon] 사람 바운드 조각 " + go.name + " ← " + r.transform.parent?.name + "/" + r.gameObject.name +
                                  " " + r.bounds.size.ToString("0.00") + " 위 " + r.bounds.max.y.ToString("0.00"));
                var vis = go.transform.Find("Visual");
                Debug.Log("[Ulon] 사람 방향 " + go.name + " — 루트 forward " + go.transform.forward.ToString("0.00") +
                          " / Visual 로컬 y " + (vis != null ? vis.localEulerAngles.y.ToString("0") : "(없음)") +
                          " / Visual world forward " + (vis != null ? vis.forward.ToString("0.00") : "-"));
                Debug.Log("[Ulon] 사람 " + go.name + " — 모델 " + model +
                          " / 몸 색 " + ColorText(body) + "(" + bodyVia + ")" +
                          " / 든 것 [" + string.Join(", ", on) + "]" +
                          " / 모델 안에 있으나 꺼진 것 [" + string.Join(", ", off) + "]");
            }
        }

        public static string ColorText(Color c) =>
            "(" + c.r.ToString("0.00") + "," + c.g.ToString("0.00") + "," + c.b.ToString("0.00") + ")";
    }
}
