using System;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// **절벽 바위**(검수 랩 ⑦). 묻는 것은 「바위가 있나」가 아니라 셋이다:
        /// ①**가파른 자리에** 섰나(완만한 데 놓으면 들바위지 암벽이 아니다)
        /// ②**지표에 박혀** 있나(급경사에서 바운드 스냅은 바위를 허공에 띄운다)
        /// ③몇 개나 되나(한둘로는 세로줄이 안 끊긴다).
        /// </summary>
        const int CliffRockCountMin = 12;
        const float CliffRockFloatMax = 1.2f;    // 발치가 지표 위로 이만큼 넘게 뜨면 「붙어 있다」가 아니다

        static void AssertCliffRocks()
        {
            AssertCliffRockNegativeControl();

            int count = 0, flat = 0, floating = 0;
            float worstFloat = 0f;
            string worstName = "(없음)";
            var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (!t.name.StartsWith("CliffRock", StringComparison.Ordinal))
                    continue;
                count++;
                var p = t.position;
                float h = WorldTerrain.HeightAt(p.x, p.z);
                float dh = Mathf.Max(Mathf.Abs(WorldTerrain.HeightAt(p.x + 2f, p.z) - h),
                                     Mathf.Abs(WorldTerrain.HeightAt(p.x, p.z + 2f) - h));
                if (Mathf.Atan2(dh, 2f) * Mathf.Rad2Deg < VisualSliceBuilder.CliffRockSlopeMin - 6f)
                    flat++;
                var rends = t.GetComponentsInChildren<Renderer>(true);
                if (rends.Length == 0)
                    continue;
                var b = rends[0].bounds;
                for (int k = 1; k < rends.Length; k++)
                    b.Encapsulate(rends[k].bounds);
                float lift = b.min.y - h;
                if (lift > worstFloat) { worstFloat = lift; worstName = t.name; }
                if (lift > CliffRockFloatMax)
                    floating++;
            }
            Debug.Log("[Ulon] §8.2 절벽 바위 — " + count + "개(하한 " + CliffRockCountMin + ") · 완만한 자리 " +
                      flat + "개 · 뜬 것 " + floating + "개 · 가장 많이 뜬 " + worstName + " " +
                      worstFloat.ToString("0.00") + "m(상한 " + CliffRockFloatMax + "m)");

            string verdict = CliffRockVerdict(count, flat, floating);
            if (verdict != null)
                throw new InvalidOperationException(verdict);
        }

        static string CliffRockVerdict(int count, int flat, int floating)
        {
            if (count < CliffRockCountMin)
                return "절벽 바위가 " + count + "개뿐입니다 — 최소 " + CliffRockCountMin +
                       ". 한둘로는 늘어난 세로줄이 안 끊깁니다(§8.2).";
            if (flat > count / 4)
                return "절벽 바위 " + flat + "개가 완만한 자리에 섰습니다(전체 " + count +
                       ") — 암벽에 박힌 바위가 아니라 들바위로 읽힙니다(§8.2).";
            if (floating > 0)
                return "절벽 바위 " + floating + "개가 지표 위로 " + CliffRockFloatMax +
                       "m 넘게 떴습니다 — 급경사에서 바운드 스냅은 바위를 허공에 세웁니다(§8.2).";
            return null;
        }

        static void AssertCliffRockNegativeControl()
        {
            if (CliffRockVerdict(3, 0, 0) == null)
                throw new InvalidOperationException("반대쪽 한계 실패 — 바위 3개인데도 절벽 자가 통과했습니다.");
            if (CliffRockVerdict(20, 12, 0) == null)
                throw new InvalidOperationException("반대쪽 한계 실패 — 절반이 완만한 자리인데도 통과했습니다.");
            if (CliffRockVerdict(20, 1, 2) == null)
                throw new InvalidOperationException("반대쪽 한계 실패 — 바위가 떴는데도 통과했습니다.");
            if (CliffRockVerdict(20, 1, 0) != null)
                throw new InvalidOperationException("반대쪽 한계 실패 — 멀쩡한 배치인데 절벽 자가 걸렸습니다.");
        }
    }
}
