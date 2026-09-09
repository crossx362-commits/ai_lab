using System.Collections.Generic;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class OutdoorCensus
    {
        /// <summary>
        /// **울타리가 건물을 뚫고 지나가나**(`26_behind_bank` 화면, 2026-09-09).
        ///
        /// 부지 집 둘레에 친 울타리가 집 벽을 관통해 화면에서 「담장 안의 집」이 아니라
        /// 「집에 박힌 말뚝」으로 읽힌다. 재는 것: 이름이 울타리인 조각과 **집 몸통** 바운드의 겹침 —
        /// 겹친 조각 수와 가장 깊이 박힌 깊이(m). 몸통은 지붕을 뺀 벽 부분을 쓴다(처마 밑을
        /// 지나는 것은 관통이 아니다).
        /// </summary>
        public static void RunFenceClash()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var fences = new List<Transform>();
            var walls = new List<Transform>();
            for (int i = 0; i < all.Length; i++)
            {
                string n = all[i].name.ToLowerInvariant();
                if (n.Contains("fence"))
                    fences.Add(all[i]);
                else if (n.Contains("wall") || n.StartsWith("house") || n.Contains("building"))
                    walls.Add(all[i]);
            }
            int clash = 0;
            float deepest = 0f;
            string worst = "";
            for (int f = 0; f < fences.Count; f++)
            {
                if (!GroundFit.WorldBounds(fences[f], out Bounds fb))
                    continue;
                for (int w = 0; w < walls.Count; w++)
                {
                    if (walls[w] == fences[f] || fences[f].IsChildOf(walls[w]) || walls[w].IsChildOf(fences[f]))
                        continue;
                    if (!GroundFit.WorldBounds(walls[w], out Bounds wb) || !fb.Intersects(wb))
                        continue;
                    var min = Vector3.Max(fb.min, wb.min);
                    var max = Vector3.Min(fb.max, wb.max);
                    float depth = Mathf.Min(max.x - min.x, max.z - min.z);
                    if (depth <= 0.05f)
                        continue;
                    clash++;
                    if (depth > deepest)
                    {
                        deepest = depth;
                        worst = GroundFit.NodePath(fences[f]) + " ← " + GroundFit.NodePath(walls[w]);
                    }
                }
            }
            // **판별 테스트** — 울타리 하나를 벽 한가운데로 옮겨 보고 이 셈이 무는지 본다.
            // 0을 보고하는 자는 「깨끗하다」와 「아무것도 안 센다」를 스스로 갈라야 한다.
            string probe = "판별 못 함";
            if (fences.Count > 0 && walls.Count > 0 && GroundFit.WorldBounds(walls[0], out Bounds pb))
            {
                var victim = fences[0];
                var keep = victim.position;
                victim.position = pb.center;
                float d = 0f;
                if (GroundFit.WorldBounds(victim, out Bounds vb) && vb.Intersects(pb))
                {
                    var mn = Vector3.Max(vb.min, pb.min);
                    var mx = Vector3.Min(vb.max, pb.max);
                    d = Mathf.Min(mx.x - mn.x, mx.z - mn.z);
                }
                victim.position = keep;
                probe = "울타리를 벽 한가운데 두면 " + d.ToString("0.00") + "m";
            }
            Debug.Log("[Census] 울타리-건물 관통 — 울타리 조각 " + fences.Count + "개 · 벽 " + walls.Count +
                      "개 · 겹친 쌍 " + clash + " · 가장 깊이 " + deepest.ToString("0.00") + "m (" + worst + ") · [판별] " + probe);
            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(0);
        }
    }
}
