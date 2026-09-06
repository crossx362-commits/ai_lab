using System;
using System.Collections.Generic;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// §8.2 — 실내 소품이 **등록 CC0 모델**인지 본다(검수 2026-09-06 반려: 색칠 큐브로 채웠다).
        /// 맨바닥 비율 게이트(`AssertRoomFurnished`)는 「덮기만 하면」 통과하므로 이 결함을 못 잡는다.
        /// 두 게이트는 서로 다른 것을 잰다 — **분포**는 맨바닥 비율이, **자격**은 이 게이트가.
        /// </summary>
        /// <summary>
        /// **마을·지역·사냥터까지 전수로** 잰다(검수 랩 C). 전에는 던전 방만 봐서 「거인의 통」·색칠 큐브가
        /// 방 밖에서는 얼마든지 들어올 수 있었다. 대상 수집은 공용 원장 `PropScope`가 한다 —
        /// 구역 루트가 없거나 소품이 하한보다 적으면 **조용히 통과하지 않고 실패**한다.
        /// </summary>
        static void AssertPropsQualified()
        {
            int total = 0;
            var zones = PropScope.Zones();
            if (zones.Length == 0)
                throw new InvalidOperationException("소품 구역 원장이 비었습니다 — 잰 것이 없습니다(0이면 실패).");
            for (int i = 0; i < zones.Length; i++)
                total += CheckZoneProps(zones[i]);
            if (total == 0)
                throw new InvalidOperationException("소품이 한 개도 없습니다 — 잰 것이 없습니다(0이면 실패).");
            Debug.Log("[Ulon] 소품 자격 통과 — 구역 " + zones.Length + "곳 " + total +
                      "개 전부 PropArt 등록 CC0 모델(프리미티브 0개, 마을·지역·던전 전수)");
        }

        static int CheckZoneProps(PropScope.Zone zone)
        {
            var props = PropScope.Props(zone, out GameObject root);
            int n = 0;
            var kinds = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < props.Count; i++)
            {
                var t = props[i];
                string why = PropArt.ReasonUnqualified(t.gameObject);
                if (why != "")
                    throw new InvalidOperationException(zone.Label + "의 소품 " + t.name + "이(가) 자격 미달입니다 — " + why +
                        ". 등록된 CC0 모델만 쓴다(자격 원장 Editor/PropArt.cs).");
                if (zone.Indoor)
                {
                    string kind = PropArt.ModelKeyOf(t.gameObject);
                    kinds[kind] = (kinds.TryGetValue(kind, out int had) ? had : 0) + 1;
                }
                n++;
            }
            // 종류 편중은 **방 규칙**이다 — 야외는 나무가 숲의 대부분인 게 정상이다.
            if (zone.Indoor)
                foreach (var kv in kinds)
                {
                    float share = kv.Value / (float)n;
                    if (share > PropKindShareMax)
                        throw new InvalidOperationException(zone.Label + "의 소품이 " + kv.Key + " 한 종류로 " +
                            (share * 100f).ToString("0") + "%입니다 — 상한 " + (PropKindShareMax * 100f).ToString("0") +
                            "%. 한 종류로 채우면 방이 그 물건 창고로 읽힌다(검수 2026-09-06).");
                }
            Debug.Log("[Ulon] 소품 자격 " + zone.Label + " " + n + "개" +
                      (zone.Indoor ? " / " + kinds.Count + "종(최다 상한 " + PropKindShareMax + ")" : ""));
            return n;
        }

        /// <summary>한 종류가 방 소품에서 차지할 수 있는 최대 비율 — 넘으면 방이 「돌무더기 창고」가 된다(검수 반려).</summary>
        const float PropKindShareMax = 0.40f;

        static int CheckProps(string label, string interiorObject)
        {
            var interior = GameObject.Find(interiorObject);
            if (interior == null)
                throw new InvalidOperationException(interiorObject + "이(가) 없습니다.");
            int n = 0;
            var kinds = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var t in PropNodes(interior))
            {
                string why = PropArt.ReasonUnqualified(t.gameObject);
                if (why != "")
                    throw new InvalidOperationException(label + "의 소품 " + t.name + "이(가) 자격 미달입니다 — " + why +
                        ". 등록된 CC0 소품 프리팹만 쓴다(자격 원장 Editor/PropArt.cs).");
                string kind = PropArt.ModelKeyOf(t.gameObject);
                kinds[kind] = (kinds.TryGetValue(kind, out int had) ? had : 0) + 1;
                n++;
            }
            // 종류 편중 — 자격을 통과해도 한 종류가 방을 덮으면 화면이 망가진다(바위 44개 = 채석장).
            foreach (var kv in kinds)
            {
                float share = kv.Value / (float)n;
                if (share > PropKindShareMax)
                    throw new InvalidOperationException(label + "의 소품이 " + kv.Key + " 한 종류로 " +
                        (share * 100f).ToString("0") + "%입니다 — 상한 " + (PropKindShareMax * 100f).ToString("0") +
                        "%. 한 종류로 채우면 던전 방이 아니라 그 물건 창고로 읽힌다(검수 2026-09-06).");
            }
            Debug.Log("[Ulon] 실내 소품 종류 " + label + " " + n + "개 / " + kinds.Count + "종, 최다 종류 비율 상한 " + PropKindShareMax);
            return n;
        }

        static List<Transform> PropNodes(GameObject interior)
        {
            var list = new List<Transform>();
            for (int c = 0; c < interior.transform.childCount; c++)
            {
                var child = interior.transform.GetChild(c);
                if (child.name.StartsWith("DungeonFurn", StringComparison.Ordinal))
                    list.Add(child);
            }
            return list;
        }

        /// <summary>네거티브 컨트롤 — 방에 색칠 큐브 소품을 실제로 하나 넣으면 빨간불이어야 한다.</summary>
        static void AssertRoomPropsNegativeControl()
        {
            var interior = GameObject.Find(Dungeon1.InteriorObject);
            if (interior == null)
                throw new InvalidOperationException("던전 1 실내가 없어 소품 자격 네거티브 컨트롤을 할 수 없습니다.");
            var fake = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fake.name = "DungeonFurnFakeCube";
            fake.transform.SetParent(interior.transform, true);
            fake.transform.position = interior.transform.position;
            bool red = false;
            try
            {
                try { AssertPropsQualified(); }
                catch (InvalidOperationException) { red = true; }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fake);
            }
            if (!red)
                throw new InvalidOperationException("소품 자격 네거티브 컨트롤 실패 — 색칠 큐브를 소품으로 넣었는데도 통과했습니다.");
            Debug.Log("[Ulon] 실내 소품 자격 네거티브 컨트롤 통과 — 프리미티브 큐브를 넣으면 FAIL");

            // 종류 편중 네거티브 컨트롤 — 한 종류를 실제로 잔뜩 복제해 넣으면 빨간불이어야 한다.
            var seed = PropNodes(interior).Find(t => t.name.StartsWith("DungeonFurnRubble", StringComparison.Ordinal));
            if (seed == null)
                throw new InvalidOperationException("복제할 잔해 소품이 없습니다.");
            var clones = new List<GameObject>();
            bool kindRed = false;
            try
            {
                for (int i = 0; i < 30; i++)
                {
                    var c = UnityEngine.Object.Instantiate(seed.gameObject, seed.parent);
                    c.name = "DungeonFurnRubbleClone" + i;
                    clones.Add(c);
                }
                try { AssertPropsQualified(); }
                catch (InvalidOperationException) { kindRed = true; }
            }
            finally
            {
                for (int i = 0; i < clones.Count; i++)
                    UnityEngine.Object.DestroyImmediate(clones[i]);
            }
            if (!kindRed)
                throw new InvalidOperationException("소품 종류 편중 네거티브 컨트롤 실패 — 바위를 30개 더 넣었는데도 통과했습니다.");
            Debug.Log("[Ulon] 실내 소품 종류 편중 네거티브 컨트롤 통과 — 한 종류를 물량으로 넣으면 FAIL");
        }
    }
}
