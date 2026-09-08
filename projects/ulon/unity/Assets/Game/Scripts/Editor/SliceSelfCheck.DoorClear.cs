using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **문 개구부 정면에는 소품이 서지 않는다**(검수 지시 2026-09-09 — 가로등이 집 문 앞을 막고 있었다).
    ///
    /// 새 측정 방식을 만들지 않는다: 개구부는 **문/사람 자와 같은 자**(`OpeningHeight` — 임시 콜라이더에
    /// 수직 레이캐스트)로 찾고, 소품 목록은 **마을 소품↔건물 자와 같은 자**(`CollectVillage`)로 모은다.
    /// 이 자가 새로 정하는 것은 **문 앞에 비워 둘 깊이** 하나뿐이고, 그것도 사람 몸에서 유도한다.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// 문 앞에 비워 둘 깊이 = **사람 몸 두께(어깨너비)의 이 배수**.
        /// 유도: 문 앞 한 몸 자리가 비어 있어야 사람이 들어간다 — 1.0이면 문턱에 코가 닿고,
        /// 2.0이면 마당 절반이 금지 구역이 된다. 「한 걸음(몸 하나) + 문틀 두께」로 1.5를 쓴다.
        /// </summary>
        const float DoorClearBodyDepths = 1.5f;

        /// <summary>사람 몸 두께 — 문/사람 자와 **같은 사람**(Player)을 잰다.</summary>
        static bool PersonFootprint(out float width, out float height)
        {
            width = height = 0f;
            var who = GameObject.Find("Player");
            if (who == null || !GroundFit.PersonBounds(who.transform, out Bounds body) || body.size.y < 0.01f)
                return false;
            width = Mathf.Max(body.size.x, body.size.z);
            height = body.size.y;
            return true;
        }

        /// <summary>문 앞 통로 상자 — 벽 조각의 **얇은 축**이 법선이고, 집 중심의 **반대쪽**이 바깥이다.</summary>
        static bool DoorFrontZone(Transform house, Transform piece, float open, float bodyW, out Bounds zone)
        {
            zone = new Bounds();
            var r = piece.GetComponentInChildren<Renderer>();
            if (r == null)
                return false;
            Bounds b = r.bounds;
            bool thinX = b.size.x < b.size.z;
            float depth = bodyW * DoorClearBodyDepths;
            Vector3 n = thinX ? Vector3.right : Vector3.forward;
            // 바깥쪽은 집 중심에서 멀어지는 쪽이다 — 안쪽을 재면 「집 안의 가구」를 결함이라고 부른다.
            float side = Vector3.Dot(b.center - house.position, n) >= 0f ? 1f : -1f;
            float thick = (thinX ? b.size.x : b.size.z) * 0.5f;
            var center = b.center + n * side * (thick + depth * 0.5f);
            center.y = b.min.y + open * 0.5f;
            var size = thinX ? new Vector3(depth, open, b.size.z) : new Vector3(b.size.x, open, depth);
            zone = new Bounds(center, size);
            return true;
        }

        /// <summary>문 앞 통로 상자들 — **굽는 쪽이 같은 자를 부른다**(자는 재기만 한다).</summary>
        internal static void CollectDoorFrontZones(List<Bounds> zones)
        {
            if (!PersonFootprint(out float bodyW, out float bodyH))
                return;
            float need = bodyH * DoorOpeningFrac;
            foreach (var house in VillageHouses())
                foreach (var piece in WallPieces(house))
                {
                    float open = OpeningHeight(piece);
                    if (open < need)
                        continue;
                    if (DoorFrontZone(house, piece, open, bodyW, out Bounds zone))
                        zones.Add(zone);
                }
        }

        static string DoorFrontClearReason(bool log)
        {
            if (!PersonFootprint(out float bodyW, out float bodyH))
                return "사람을 못 쟀습니다 — 잰 것이 없습니다(0이면 실패).";
            float need = bodyH * DoorOpeningFrac;

            var props = new List<Transform>();
            var boxes = new List<Bounds>();
            var bldNames = new List<string>();
            var bldBoxes = new List<Bounds>();
            CollectVillage(props, boxes, bldNames, bldBoxes);
            if (props.Count == 0)
                return "마을 소품을 하나도 못 읽었습니다 — 잰 것이 없습니다(0이면 실패).";

            int doors = 0;
            var bad = new List<string>();
            foreach (var house in VillageHouses())
                foreach (var piece in WallPieces(house))
                {
                    float open = OpeningHeight(piece);
                    if (open < need)
                        continue;                            // 사람이 못 들어가는 창은 문이 아니다
                    if (!DoorFrontZone(house, piece, open, bodyW, out Bounds zone))
                        continue;
                    doors++;
                    for (int p = 0; p < props.Count; p++)
                    {
                        if (props[p] == null || !zone.Intersects(boxes[p]))
                            continue;
                        // 바닥에 깔린 길·발판은 지나갈 것을 막지 않는다 — 마을 자와 같은 잣대로 뺀다.
                        if (IsVillageMat(boxes[p]))
                            continue;
                        bad.Add(props[p].name + "(" + boxes[p].center.x.ToString("0.0") + "," +
                                boxes[p].center.z.ToString("0.0") + ") ↔ 문(" +
                                zone.center.x.ToString("0.0") + "," + zone.center.z.ToString("0.0") + ")");
                    }
                }
            if (doors == 0)
                return "사람이 들어갈 문을 하나도 못 찾았습니다 — 잰 것이 없습니다(0이면 실패).";
            if (log)
                Debug.Log("[Ulon] 문 앞 통로 — 문 " + doors + "개 · 소품 " + props.Count + "개 · 막은 것 " +
                          bad.Count + "개 (비울 깊이 " + (bodyW * DoorClearBodyDepths).ToString("0.00") +
                          "m = 사람 두께 " + bodyW.ToString("0.00") + "×" + DoorClearBodyDepths + ")");
            if (bad.Count == 0)
                return "";
            bad.Sort(StringComparer.Ordinal);
            return "문 개구부 정면에 소품이 서 있습니다 — " + bad.Count + "건:\n  " +
                   string.Join("\n  ", bad.GetRange(0, Mathf.Min(bad.Count, 8))) +
                   (bad.Count > 8 ? "\n  … 외 " + (bad.Count - 8) + "건" : "") +
                   "\n문 앞 한 몸 자리는 비어 있어야 들어가는 곳으로 읽힙니다(§8.1).";
        }

        static void AssertDoorFrontClear()
        {
            string reason = DoorFrontClearReason(true);
            if (!string.IsNullOrEmpty(reason))
                throw new InvalidOperationException(reason);
        }

        /// <summary>양방향 NC — 소품 하나를 문 앞에 세우면 빨간불, 치우면 초록.</summary>
        static void AssertDoorFrontClearNegativeControl()
        {
            if (!PersonFootprint(out float bodyW, out float bodyH))
                throw new InvalidOperationException("문 앞 NC — 사람을 못 쟀습니다(0이면 실패).");
            float need = bodyH * DoorOpeningFrac;

            Bounds zone = new Bounds();
            bool found = false;
            foreach (var house in VillageHouses())
            {
                foreach (var piece in WallPieces(house))
                {
                    float open = OpeningHeight(piece);
                    if (open >= need && DoorFrontZone(house, piece, open, bodyW, out zone))
                    { found = true; break; }
                }
                if (found)
                    break;
            }
            if (!found)
                throw new InvalidOperationException("문 앞 NC 대상이 없습니다 — 사람이 들어갈 문이 없습니다(0이면 실패).");

            var props = new List<Transform>();
            var boxes = new List<Bounds>();
            var n1 = new List<string>();
            var b1 = new List<Bounds>();
            CollectVillage(props, boxes, n1, b1);
            if (props.Count == 0)
                throw new InvalidOperationException("문 앞 NC 대상 소품이 없습니다(0이면 실패).");

            // **희생양은 정해서 고른다** — `props[0]`은 씬 순회 순서라 판마다 달랐고, 어떤 판에는
            // 바닥 깔개(자가 봐주는 것)나 광맥이 뽑혀 NC가 간헐적으로 통과했다(실측: `HouseChest`·`IronVein`).
            // 문 앞에서 가장 가까운 **자가 실제로 재는 소품**을 고르면 판마다 같은 것이 뽑힌다.
            Transform victim = null;
            float bestD = float.MaxValue;
            for (int p = 0; p < props.Count; p++)
            {
                if (props[p] == null || IsVillageMat(boxes[p]))
                    continue;
                float d = Vector3.Distance(boxes[p].center, zone.center);
                if (d < bestD - 0.001f ||
                    (Mathf.Abs(d - bestD) <= 0.001f && victim != null &&
                     string.CompareOrdinal(props[p].name, victim.name) < 0))
                {
                    bestD = d;
                    victim = props[p];
                }
            }
            if (victim == null)
                throw new InvalidOperationException("문 앞 NC 대상 소품이 없습니다 — 전부 바닥 깔개입니다(0이면 실패).");
            var kept = victim.position;
            bool red;
            try
            {
                // **높이까지 맞춰 옮긴다** — x·z만 옮기고 제 높이를 남겨 두면, 지형이 다른 집의 문
                // 앞으로 갔을 때 상자가 통로 상자와 y로 안 겹쳐 NC가 간헐적으로 통과했다(실측:
                // `HouseChest`. 「간헐적으로 우는 자는 자가 아니다」).
                var vb = victim.GetComponentInChildren<Renderer>() != null
                    ? victim.GetComponentInChildren<Renderer>().bounds
                    : new Bounds(victim.position, Vector3.zero);
                victim.position += zone.center - vb.center;
                Physics.SyncTransforms();
                red = !string.IsNullOrEmpty(DoorFrontClearReason(false));
            }
            finally
            {
                victim.position = kept;
                Physics.SyncTransforms();
            }
            if (!red)
                throw new InvalidOperationException("문 앞 네거티브 컨트롤 실패 — " + victim.name +
                    "을(를) 문 앞에 세웠는데 통과했습니다.");
            string back = DoorFrontClearReason(false);
            if (!string.IsNullOrEmpty(back))
                throw new InvalidOperationException("문 앞 네거티브 컨트롤 실패 — 치웠는데도 빨간불입니다: " + back);
            Debug.Log("[Ulon] 문 앞 통로 양방향 NC 통과 — 소품을 문 앞에 세우면 FAIL · 치우면 다시 통과");
        }
    }
}
