using System.Collections.Generic;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **발 높이 감사 도구**(게이트 아님, 검수 의심 2026-09-07: 「38_healer에서 사람 둘이 떠 보인다」).
    ///
    /// 씬의 모든 액터(CharacterController를 단 것)에 대해 ① 발 높이 - 지표 높이 ② 그 액터가
    /// 발 높이 게이트의 **대상 집합에 있는지**를 같이 찍는다. 두 번째가 이 감사의 핵심이다 —
    /// 게이트가 통과했는데 화면이 떠 보이면, 틀린 것은 값이 아니라 **대상 수집**이다
    /// (마을이 10m 묻혀 있던 사건과 같은 종류).
    ///
    /// Unity -batchmode -nographics -quit -projectPath unity \
    ///   -executeMethod Ulon.Editor.FootAudit.Run -logFile unity/Logs/foot_audit.log
    /// </summary>
    public static class FootAudit
    {
        public static void Run()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity");
            var targets = GroundFit.Candidates();
            var inGate = new HashSet<Transform>(targets);
            var actors = Object.FindObjectsByType<CharacterController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var terrain = Terrain.activeTerrain;
            Debug.Log("[Ulon] 발 높이 감사 — 액터 " + actors.Length + "체 / 발 높이 게이트 대상 " + targets.Count + "개");
            for (int i = 0; i < actors.Length; i++)
            {
                var t = actors[i].transform;
                if (!GroundFit.WorldBounds(t, out Bounds b))
                    continue;
                float ground = terrain != null
                    ? terrain.SampleHeight(t.position) + terrain.transform.position.y
                    : 0f;
                // 게이트가 이 액터를 **직접** 재는가, 아니면 조상의 바운드에 묻혀 있는가.
                bool self = inGate.Contains(t);
                string via = self ? "직접" : "대상 아님";
                if (!self)
                    for (var p = t.parent; p != null; p = p.parent)
                        if (inGate.Contains(p))
                        {
                            via = "조상 「" + p.name + "」 바운드에 섞임";
                            break;
                        }
                // 「떠 보인다」는 발 높이 말고 **아랫도리가 없다**로도 생긴다 — 꺼진 메시를 같이 찍는다.
                var off = new List<string>();
                var rends = t.GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < rends.Length; r++)
                    if (!rends[r].enabled || !rends[r].gameObject.activeInHierarchy)
                        off.Add(rends[r].gameObject.name);
                if (off.Count > 0)
                    Debug.Log("[Ulon] 꺼진 메시 " + t.name + " — " + string.Join(", ", off));
                // 게이트와 **같은 자**로 잰다 — 발 밑 표면과의 차(마을·지하를 한 규칙으로).
                string surf = "발 밑에 바닥 없음";
                var from = new Vector3(b.center.x, b.min.y + 2.0f, b.center.z);
                var hits = Physics.RaycastAll(from, Vector3.down, 10f, ~0, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (u, v) => u.distance.CompareTo(v.distance));
                for (int h = 0; h < hits.Length; h++)
                {
                    if (hits[h].collider == null || hits[h].collider.transform.IsChildOf(t))
                        continue;
                    surf = "표면 " + hits[h].point.y.ToString("0.00") + " 차 " + (b.min.y - hits[h].point.y).ToString("0.00") +
                           "m (" + hits[h].collider.transform.root.name + "/" + hits[h].collider.name + ")";
                    break;
                }
                Debug.Log("[Ulon] 발밑 " + t.name + " — " + surf);
                Debug.Log("[Ulon] 발 " + t.name + " — 발 y " + b.min.y.ToString("0.00") +
                          " / 지표 " + ground.ToString("0.00") +
                          " / 차이 " + (b.min.y - ground).ToString("0.00") + "m · 게이트 " + via);
            }
        }
    }
}
