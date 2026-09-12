using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **민가 바깥 모서리가 막혀 있는가**(INBOX 2026-09-12 18:20 남은 구멍).
    ///
    /// Kenney 직선 벽은 칸의 −X 한 면(두께 0.1m)이다. 모서리 칸에 직선 둘만 두면
    /// 바깥 직각이 비어 화면에서 집이 뚫린다(샷 `loop26_house_ridge`). 막는 조각은
    /// `wall-corner`(L자, 바운드 X·Z 둘 다 0.4m 넘음). 이름은 단서일 뿐이고 자는 바운드다.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        const float CornerMinXZ = 0.4f;

        static void AssertHouseCorners()
        {
            string reason = HouseCornerReason(true);
            if (!string.IsNullOrEmpty(reason))
                throw new InvalidOperationException(reason);
        }

        static string HouseCornerReason(bool log)
        {
            var houses = new List<Transform>();
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == "House")
                    houses.Add(t);
            if (houses.Count == 0)
                return "민가를 한 채도 못 찾았습니다 — 잰 것이 없습니다(0이면 실패).";

            int measured = 0;
            var thin = new List<string>();
            for (int h = 0; h < houses.Count; h++)
            {
                if (!houses[h].gameObject.activeInHierarchy)
                    continue;
                int n = 0;
                foreach (Transform c in houses[h])
                {
                    if (c.name.IndexOf("wall-corner", StringComparison.Ordinal) < 0)
                        continue;
                    if (!c.gameObject.activeSelf)
                        continue;
                    var rs = c.GetComponentsInChildren<Renderer>(true);
                    if (rs.Length == 0)
                        continue;
                    Bounds b = rs[0].bounds;
                    for (int i = 1; i < rs.Length; i++)
                        b.Encapsulate(rs[i].bounds);
                    measured++;
                    if (b.size.x < CornerMinXZ || b.size.z < CornerMinXZ)
                        thin.Add(houses[h].position.ToString("F1") + " " + c.name +
                                 " xz=" + b.size.x.ToString("0.00") + "," + b.size.z.ToString("0.00"));
                    else
                        n++;
                }
                if (n < 3)
                    thin.Add(houses[h].position.ToString("F1") + " L자 " + n + "개(하한 3)");
            }
            if (measured < 6)
                return "wall-corner 를 " + measured + "개밖에 못 쟀습니다 — 잰 것이 없습니다(0이면 실패).";
            if (log)
                Debug.Log("[Ulon] 민가 모서리 — " + houses.Count + "채 · L자 " + measured + "개 · 구멍 " + thin.Count);
            if (thin.Count > 0)
                return "민가 모서리가 안 막힌 집: " + string.Join(", ", thin) +
                       " — 직선 벽만 두면 바깥 직각이 빕니다. `PlaceHouseWalls` 가 wall-corner 를 둡니다.";
            return "";
        }

        static void AssertHouseCornersNegativeControl()
        {
            Transform house = null;
            var disabled = new List<GameObject>();
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name != "House" || !t.gameObject.activeInHierarchy)
                    continue;
                foreach (Transform c in t)
                    if (c.name.IndexOf("wall-corner", StringComparison.Ordinal) >= 0)
                    { house = t; break; }
                if (house != null)
                    break;
            }
            if (house == null)
                throw new InvalidOperationException("wall-corner 가 있는 민가가 없습니다 — 잰 것이 없습니다(0이면 실패).");
            string before = HouseCornerReason(true);
            if (!string.IsNullOrEmpty(before))
                throw new InvalidOperationException("모서리 NC 실패 — 손대기 전부터 빨간불입니다: " + before);
            try
            {
                foreach (Transform c in house)
                    if (c.name.IndexOf("wall-corner", StringComparison.Ordinal) >= 0)
                    {
                        disabled.Add(c.gameObject);
                        c.gameObject.SetActive(false);
                    }
                bool red = !string.IsNullOrEmpty(HouseCornerReason(false));
                if (!red)
                    throw new InvalidOperationException("모서리 NC 실패 — wall-corner 를 껐는데 통과했습니다. 빈 통과입니다.");
            }
            finally
            {
                for (int i = 0; i < disabled.Count; i++)
                    if (disabled[i] != null)
                        disabled[i].SetActive(true);
            }
            if (!string.IsNullOrEmpty(HouseCornerReason(false)))
                throw new InvalidOperationException("모서리 NC 실패 — 되돌렸는데 빨간불이 남았습니다(계측이 세계를 바꿨습니다).");
            Debug.Log("[Ulon] 민가 모서리 양방향 NC 통과 — wall-corner 끄면 FAIL · 켜면 통과");
        }
    }
}
