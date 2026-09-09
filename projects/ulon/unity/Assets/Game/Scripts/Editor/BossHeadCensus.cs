using UnityEngine;
using Ulon.Shared;

namespace Ulon.Editor
{
    /// <summary>
    /// **보스 머리가 왜 뭉개 보이나 — 세기만 한다**(검수 화면 랩 1, 2026-09-09).
    ///
    /// 화면(`17_boss_closeup`)에서 왕관 아래 머리가 원뿔처럼 늘어나고 얼굴이 반쯤 덮여 눈이 하나만
    /// 보인다. 원인 후보는 셋이고 **세기 전에는 고르지 않는다**:
    ///   ① 모델 자체(프리팹이 원래 그렇게 생겼다)
    ///   ② 크기 맞추기가 만든 왜곡(비균등 스케일·머리 장비 확대)
    ///   ③ 겹침(머리 장비 둘이 한 자리에 얹혀 얼굴을 덮는다)
    ///
    /// 그래서 보스 아래 렌더러를 전부 적고, **머리 높이대(위 45%)**에 있는 것만 따로 모아
    /// 서로 얼마나 겹치는지(부피 교집합 비율)와 각자의 스케일이 균등한지를 적는다.
    /// 같은 몸을 쓰는 다른 몹도 같이 재서 **보스만 그런지**를 가른다.
    ///
    /// **첫 판이 틀린 자리**(자를 세울 땐 그 자의 구멍도 적는다): 머리대를 위 25%로 잡았더니
    /// IronTyrant는 **머리가 그 밑에 있어 왕관만 세었다** — 「머리대에 머리가 없다」는 세계가 아니라
    /// 자의 구멍이었다. 45%로 넓히자 `Knight_Head↔Knight_Helmet 100%`가 바로 나왔다.
    ///
    /// **이 자가 못 보는 것**: 메시 안쪽 모양은 안 본다 — 바운즈만 잰다. 두 렌더러가 겹쳐도
    /// 실제로 얼굴을 가리는지는 화면으로 봐야 하고, 반대로 안 겹쳐도 뭉개 보일 수 있다(모델 자체).
    /// </summary>
    public static class BossHeadCensus
    {
        public static void Run()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            Count();
            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(0);
        }

        [UnityEditor.MenuItem("Ulon/Count Boss Head")]
        public static void Count()
        {
            foreach (string name in new[] { Dungeon3.BossObject, Dungeon1.BossObject, Dungeon2.BossObject, "Skeleton" })
            {
                var go = GameObject.Find(name);
                if (go == null)
                {
                    Debug.Log("[Census] 머리 " + name + " — 못 찾음");
                    continue;
                }
                One(name, go.transform);
            }
        }

        static void One(string name, Transform root)
        {
            if (!GroundFit.WorldBounds(root, out Bounds all))
            {
                Debug.Log("[Census] 머리 " + name + " — 몸을 못 쟀다");
                return;
            }
            // 위 25%로 잡았더니 IronTyrant는 **머리가 그 밑에 있어** 왕관만 세었다 —
            // 「머리대에 머리가 없다」는 자의 구멍이었다. 얼굴이 드는 높이까지 넓힌다(위 45%).
            float headFloor = all.max.y - all.size.y * 0.45f;
            var rends = root.GetComponentsInChildren<Renderer>(true);
            var head = new System.Collections.Generic.List<Renderer>();
            var rows = new System.Collections.Generic.List<string>();
            for (int i = 0; i < rends.Length; i++)
            {
                if (!rends[i].enabled || rends[i] is ParticleSystemRenderer)
                    continue;
                var b = rends[i].bounds;
                if (b.center.y < headFloor)
                    continue;
                head.Add(rends[i]);
                var s = rends[i].transform.lossyScale;
                bool even = Mathf.Abs(s.x - s.y) < s.x * 0.02f && Mathf.Abs(s.x - s.z) < s.x * 0.02f;
                rows.Add(rends[i].name + " 크기 " + b.size.x.ToString("0.00") + "×" + b.size.y.ToString("0.00") +
                         "×" + b.size.z.ToString("0.00") + "m · 배율 " + s.x.ToString("0.000") + "/" +
                         s.y.ToString("0.000") + "/" + s.z.ToString("0.000") + (even ? "" : " ← **비균등**"));
            }

            // **머리대에서 서로 겹치는 쌍** — 장비 둘이 한 자리에 얹히면 얼굴이 덮인다.
            var pairs = new System.Collections.Generic.List<string>();
            for (int i = 0; i < head.Count; i++)
                for (int j = i + 1; j < head.Count; j++)
                {
                    var a = head[i].bounds;
                    var b = head[j].bounds;
                    if (!a.Intersects(b))
                        continue;
                    var min = Vector3.Max(a.min, b.min);
                    var max = Vector3.Min(a.max, b.max);
                    var d = Vector3.Max(max - min, Vector3.zero);
                    float inter = d.x * d.y * d.z;
                    float small = Mathf.Min(a.size.x * a.size.y * a.size.z, b.size.x * b.size.y * b.size.z);
                    if (small < 1e-6f)
                        continue;
                    float share = inter / small;
                    if (share > 0.15f)
                        pairs.Add(head[i].name + "↔" + head[j].name + " " + (share * 100f).ToString("0") + "%");
                }

            Debug.Log("[Census] 머리 " + name + " — 몸 " + all.size.y.ToString("0.00") + "m · 머리대(위 45%) 렌더러 " +
                      head.Count + "개 · 겹치는 쌍 " + pairs.Count + "개" +
                      (pairs.Count > 0 ? ": " + string.Join(", ", pairs) : "") +
                      "\n  " + string.Join("\n  ", rows));
        }
    }
}
