using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// **나무가 몇 색인가**(검수 랩 ⑤ 「초록 한 톤」). 세는 도구가 먼저 답을 줬다: 야외 나무
        /// **144그루 전부 한 색**. 종류·크기를 아무리 섞어도 색이 하나면 화면은 한 덩이다.
        /// 자는 「몇 그루냐」가 아니라 **「몇 색이냐, 한 색이 다 먹지 않았느냐」**를 묻는다.
        /// </summary>
        const int TreeToneMinKinds = 3;         // 이보다 적으면 갈린 티가 안 난다
        const float TreeToneMaxShare = 0.55f;   // 한 색이 이보다 많으면 나머지는 곁들이다

        static void AssertTreeTones()
        {
            AssertTreeToneNegativeControl();

            var byTone = new Dictionary<string, int>();
            int trees = 0;
            var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (!VisualSliceBuilder.IsTreeName(t.name) || (t.parent != null && VisualSliceBuilder.IsTreeName(t.parent.name)))
                    continue;
                var r = t.GetComponentInChildren<Renderer>(true);
                if (r == null || r.sharedMaterial == null)
                    continue;
                trees++;
                string key = ColorUtility.ToHtmlStringRGB(r.sharedMaterial.color);
                byTone.TryGetValue(key, out int n);
                byTone[key] = n + 1;
            }
            if (trees == 0)
                throw new InvalidOperationException("야외에 나무가 한 그루도 없습니다 — 이 자는 아무것도 재지 않았습니다.");

            int top = 0;
            string topKey = "(없음)";
            foreach (var kv in byTone)
                if (kv.Value > top) { top = kv.Value; topKey = kv.Key; }
            float share = (float)top / trees;
            Debug.Log("[Ulon] §8.2 나무 색 — " + trees + "그루 · " + byTone.Count + "색 · 가장 흔한 #" +
                      topKey + " " + (share * 100f).ToString("0") + "% (하한 " + TreeToneMinKinds +
                      "색, 상한 " + (TreeToneMaxShare * 100f).ToString("0") + "%)");
            string verdict = TreeToneVerdict(byTone.Count, share);
            if (verdict != null)
                throw new InvalidOperationException(verdict);
        }

        /// <summary>판정만 하는 자리 — 표본과 떼어 놓아야 반대쪽 한계를 그냥 부를 수 있다.</summary>
        static string TreeToneVerdict(int kinds, float topShare)
        {
            if (kinds < TreeToneMinKinds)
                return "야외 나무가 " + kinds + "색뿐입니다 — 최소 " + TreeToneMinKinds +
                       "색. 한 색이면 숲이 한 덩이 초록으로 읽힙니다(§8.2).";
            if (topShare > TreeToneMaxShare)
                return "야외 나무의 한 색이 " + (topShare * 100f).ToString("0") + "%를 차지합니다 — 상한 " +
                       (TreeToneMaxShare * 100f).ToString("0") + "%. 나머지 색은 곁들이일 뿐입니다(§8.2).";
            return null;
        }

        static void AssertTreeToneNegativeControl()
        {
            if (TreeToneVerdict(1, 1f) == null)
                throw new InvalidOperationException("반대쪽 한계 실패 — 나무가 한 색인데도 색 자가 통과했습니다.");
            if (TreeToneVerdict(4, 0.9f) == null)
                throw new InvalidOperationException("반대쪽 한계 실패 — 한 색이 90%인데도 색 자가 통과했습니다.");
            if (TreeToneVerdict(4, 0.30f) != null)
                throw new InvalidOperationException("반대쪽 한계 실패 — 넷이 고르게 갈렸는데 색 자가 걸렸습니다.");
        }
    }
}
