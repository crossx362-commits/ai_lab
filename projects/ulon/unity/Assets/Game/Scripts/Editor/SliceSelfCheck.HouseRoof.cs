using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **민가 지붕 조각이 서로 겹치지 않는가**(2026-09-09, 검수 발견 「지붕이 한 장짜리 판」).
    ///
    /// 박공을 용마루 한가운데에 90°로 돌려 세워 놓아, 박공 한 장이 네 지붕 조각 위를 통째로 덮었다.
    /// 화면에서는 두께도 반대 사면도 없는 **판때기 한 장**으로 읽혔다(`48_person_Healer`).
    ///
    /// 재는 것은 **이름이 아니라 자리**다: 조각 이름이 무엇이든, 한 지붕 안에서 두 조각의 바운드가
    /// 서로를 절반 넘게 먹고 있으면 그것은 겹쳐 놓은 것이다. 이름으로 「박공이 x=1.0에 있으면 빨간불」로
    /// 재면 다음 판때기가 다른 조각으로 오는 순간 샌다(이름 원장은 언제나 샌다).
    ///
    /// 양방향 NC: 아무 집의 지붕 조각 하나를 복제해 옆 조각 위에 포개면 **빨간불**이어야 하고,
    /// 지우면 **다시 초록**이어야 한다. 잰 뒤 조각 수가 그대로인지도 본다 —
    /// 계측이 세계를 바꾸면 그 판의 초록도 빨강도 못 믿는다.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        /// <summary>겹침 허용치 — 작은 조각 부피의 이 비율까지는 맞물림(처마·용마루 겹침)으로 본다.</summary>
        const float RoofOverlapMax = 0.5f;

        static void AssertHouseRoofPiecesDontOverlap()
        {
            string reason = HouseRoofOverlapReason(true);
            if (!string.IsNullOrEmpty(reason))
                throw new InvalidOperationException(reason);
        }

        /// <summary>빨간불 사유(없으면 빈 문자열) — 게이트와 NC가 **같은 자**를 쓴다.</summary>
        static string HouseRoofOverlapReason(bool log)
        {
            var houses = new List<Transform>();
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == "House")
                    houses.Add(t);
            if (houses.Count == 0)
                return "민가를 한 채도 못 찾았습니다 — 잰 것이 없습니다(0이면 실패).";

            int measured = 0;
            var offenders = new List<string>();
            for (int h = 0; h < houses.Count; h++)
            {
                var pieces = RoofPieceBounds(houses[h]);
                if (pieces.Count < 2)
                    continue;
                measured += pieces.Count;
                for (int i = 0; i < pieces.Count; i++)
                    for (int k = i + 1; k < pieces.Count; k++)
                    {
                        float frac = OverlapFraction(pieces[i].Value, pieces[k].Value);
                        if (frac > RoofOverlapMax)
                            offenders.Add(houses[h].position.ToString("F1") + " " + pieces[i].Key + "×" + pieces[k].Key +
                                          " " + (frac * 100f).ToString("0") + "% 겹침");
                    }
            }
            if (measured < 4)
                return "지붕 조각을 " + measured + "개밖에 못 쟀습니다 — 잰 것이 없습니다(0이면 실패).";
            if (log)
                Debug.Log("[Ulon] 민가 지붕 겹침 — " + houses.Count + "채 · 조각 " + measured +
                          "개(허용 " + (RoofOverlapMax * 100f).ToString("0") + "%) · 겹친 짝 " + offenders.Count + "쌍");
            if (offenders.Count > 0)
                return "지붕 조각이 서로 포개진 민가 " + offenders.Count + "쌍: " + string.Join(", ", offenders) +
                       " — 포갠 조각은 화면에서 두께 없는 판때기 한 장으로 읽힙니다. " +
                       "박공은 용마루 위가 아니라 **끝 칸**에 지붕 조각 대신 놓입니다(`VisualSliceBuilder.PlaceHouseRoof`).";
            return "";
        }

        static List<KeyValuePair<string, Bounds>> RoofPieceBounds(Transform house)
        {
            var list = new List<KeyValuePair<string, Bounds>>();
            foreach (Transform c in house)
            {
                if (!c.name.StartsWith("roof", StringComparison.Ordinal))
                    continue;
                var renderers = c.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                    continue;
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    b.Encapsulate(renderers[i].bounds);
                list.Add(new KeyValuePair<string, Bounds>(c.name, b));
            }
            return list;
        }

        /// <summary>두 바운드가 겹치는 부피 / 작은 쪽 부피.</summary>
        static float OverlapFraction(Bounds a, Bounds b)
        {
            float x = Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x);
            float y = Mathf.Min(a.max.y, b.max.y) - Mathf.Max(a.min.y, b.min.y);
            float z = Mathf.Min(a.max.z, b.max.z) - Mathf.Max(a.min.z, b.min.z);
            if (x <= 0f || y <= 0f || z <= 0f)
                return 0f;
            float small = Mathf.Min(a.size.x * a.size.y * a.size.z, b.size.x * b.size.y * b.size.z);
            return small <= 0f ? 0f : (x * y * z) / small;
        }

        /// <summary>양방향 NC — 조각 하나를 옆 조각 위에 포개면 빨간불, 지우면 다시 초록.</summary>
        static void AssertHouseRoofPiecesDontOverlapNegativeControl()
        {
            Transform house = null;
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == "House" && RoofPieceBounds(t).Count >= 2)
                { house = t; break; }
            if (house == null)
                throw new InvalidOperationException("지붕 조각이 둘 이상인 민가가 없습니다 — 잰 것이 없습니다(0이면 실패).");

            string before = HouseRoofOverlapReason(true);
            if (!string.IsNullOrEmpty(before))
                throw new InvalidOperationException("지붕 겹침 NC 실패 — 손대기 전부터 빨간불입니다: " + before);
            int pieces = RoofPieceBounds(house).Count;

            Transform sample = null;
            foreach (Transform c in house)
                if (c.name.StartsWith("roof", StringComparison.Ordinal))
                { sample = c; break; }
            var clone = UnityEngine.Object.Instantiate(sample.gameObject, sample.parent);
            bool red;
            try
            {
                clone.name = sample.name;                       // 이름은 그대로 — 자가 이름으로 재면 안 된다
                clone.transform.localPosition = sample.localPosition;
                clone.transform.localRotation = sample.localRotation;
                red = !string.IsNullOrEmpty(HouseRoofOverlapReason(false));
            }
            finally { UnityEngine.Object.DestroyImmediate(clone); }
            if (!red)
                throw new InvalidOperationException("지붕 겹침 NC 실패 — 조각을 그대로 포갰는데 통과했습니다. 빈 통과입니다.");
            if (!string.IsNullOrEmpty(HouseRoofOverlapReason(false)))
                throw new InvalidOperationException("지붕 겹침 NC 실패 — 포갠 것을 지웠는데 빨간불이 남았습니다(계측이 세계를 바꿨습니다).");
            if (RoofPieceBounds(house).Count != pieces)
                throw new InvalidOperationException("잰 뒤 지붕 조각 수가 달라졌습니다 — 계측이 세계를 바꿨습니다.");
            Debug.Log("[Ulon] 민가 지붕 겹침 양방향 NC 통과 — 조각을 포개면 FAIL · 지우면 다시 통과");
        }
    }
}
