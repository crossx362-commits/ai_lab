using UnityEngine;

namespace AshesToStars
{
    /// <summary>집·나무가 마을 길 한가운데 서지 않는지 검사한다.</summary>
    public static class FieldDecorRoadSelfCheck
    {
        public static void Run()
        {
            const float R = 14f;
            var mid = FieldDecor.SampleMainRoad(R, 0.5f);
            Debug.Assert(FieldDecor.WouldBlockRoad(mid, R, 2.4f),
                "[FieldDecorRoadSelfCheck] 큰길 한가운데는 나무를 막아야 한다");
            Debug.Assert(FieldDecor.WouldBlockRoad(mid, R, 3.2f),
                "[FieldDecorRoadSelfCheck] 큰길 한가운데는 집을 막아야 한다");

            Vector2? off = null;
            for (int y = -12; y <= 12 && off == null; y += 3)
                for (int x = -12; x <= 12; x += 3)
                {
                    var p = new Vector2(x, y);
                    if (!FieldDecor.WouldBlockRoad(p, R, 2.4f)) { off = p; break; }
                }
            Debug.Assert(off.HasValue,
                "[FieldDecorRoadSelfCheck] 길이 아닌 자리를 하나도 못 찾았다");

            Debug.Log("[FieldDecorRoadSelfCheck] PASS");
        }
    }
}
