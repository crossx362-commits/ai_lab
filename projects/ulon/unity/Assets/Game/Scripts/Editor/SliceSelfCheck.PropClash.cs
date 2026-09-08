using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **마을 소품끼리 파고들지 않았는가**(검수 승인 랩 2026-09-09 — `33_campfire` 장작이 울타리를 관통).
    ///
    /// 야외에는 소품↔소품 자가 **아예 없었다**(`GATE_BLINDSPOTS.md`의 사각지대, 실물로 확인).
    /// 그렇다고 「겹치면 실패」로 만들면 못 쓴다 — 세어 보니 마을 물린 쌍 201건 중
    /// **울타리끼리 53·풀끼리 40·나무 수관끼리 12**가 정상적으로 이어 붙은 것이었다.
    ///
    /// 그래서 자는 **무엇이 무엇에 물렸는지**를 본다:
    /// ① 같은 물건끼리(울타리 줄·풀 무리·기둥 줄)는 이어 붙는 것이 제 모양이다 — 봐준다.
    /// ② 자연물끼리(나무·덤불·풀)도 수관이 겹치는 것이 자연이다 — 봐준다.
    /// ③ **그 밖에는 결함이다** — 장작이 울타리를 뚫고, 수레가 울타리에 박히고, 나무가 절구를 삼킨다.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// 물린 깊이가 **얇은 쪽 두께의 이 비율**을 넘으면 「파고들었다」로 본다.
        /// 던전 소품 자(`PropOverlapFrac`)와 같은 값을 쓴다 — 같은 질문에 자를 두 벌 두지 않는다.
        /// </summary>
        const float VillagePropOverlapFrac = 0.5f;

        /// <summary>
        /// **이만큼은 물려야 결함이다.** 비율만 쓰면 얇은 것(울타리 살 0.16m)에 닿기만 해도 걸린다 —
        /// 울타리에 바짝 댄 수레가 판마다 좌우로 밀려 다녔다(실측 2026-09-09). 화면에서 「뚫었다」로
        /// 읽히려면 한 뼘은 들어가야 한다: 사람 어깨너비(≈0.9m)의 4분의 1.
        /// 걸리지 않은 **가장 깊이 물린 쌍**도 매 판 찍는다 — 자가 아슬아슬해지면 보이라고.
        /// </summary>
        const float VillagePropMinBite = 0.25f;

        static float DeepestForgiven;
        static string DeepestForgivenWhat = "(없음)";

        /// <summary>자연물 — 심어 놓은 것이 아니라 자란 것. 서로 겹치는 것이 제 모양이다.</summary>
        static bool IsNaturalProp(string name) =>
            name.StartsWith("grass", StringComparison.Ordinal) ||
            name.StartsWith("plant_", StringComparison.Ordinal) ||
            name.StartsWith("tree", StringComparison.Ordinal) ||
            name.StartsWith("hedge", StringComparison.Ordinal) ||
            name.StartsWith("ResinBush", StringComparison.Ordinal);

        /// <summary>같은 물건인가 — 이름에서 유니티가 붙인 사본 번호만 떼고 비교한다.</summary>
        static string PropKind(string name)
        {
            int cut = name.IndexOf(" (", StringComparison.Ordinal);
            string k = cut > 0 ? name.Substring(0, cut) : name;
            int tail = k.Length;
            while (tail > 0 && char.IsDigit(k[tail - 1]))
                tail--;
            return k.Substring(0, tail);
        }

        static bool PropPairForgiven(string a, string b) =>
            PropKind(a) == PropKind(b) ||
            // **줄 조각끼리도 이어 붙는 것이 제 모양이다** — 울타리 사이의 문(`fence-gate`)은 이름이
            // 달라도 그 줄의 일부다(실측 2026-09-09: 이것 하나 때문에 자가 울었다).
            (VisualSliceBuilder.IsRunPiece(a) && VisualSliceBuilder.IsRunPiece(b)) ||
            (IsNaturalProp(a) && IsNaturalProp(b));

        /// <summary>물린 쌍을 모은다(굽는 쪽도 이 자를 부른다 — 재는 함수는 하나다).</summary>
        internal static void CollectPropClashes(List<int> ia, List<int> ib, List<float> deep,
                                                List<Transform> props, List<Bounds> boxes)
        {
            DeepestForgiven = 0f;
            DeepestForgivenWhat = "(없음)";
            var bn = new List<string>();
            var bb = new List<Bounds>();
            CollectVillage(props, boxes, bn, bb);
            for (int a = 0; a < props.Count; a++)
            {
                for (int b = a + 1; b < props.Count; b++)
                {
                    if (props[a] == null || props[b] == null)
                        continue;
                    if (PropPairForgiven(props[a].name, props[b].name))
                        continue;
                    if (!boxes[a].Intersects(boxes[b]))
                        continue;
                    float least = float.MaxValue;
                    for (int ax = 0; ax < 3; ax++)
                        least = Mathf.Min(least, Mathf.Max(0f,
                            Mathf.Min(boxes[a].max[ax], boxes[b].max[ax]) -
                            Mathf.Max(boxes[a].min[ax], boxes[b].min[ax])));
                    float thin = Mathf.Min(
                        Mathf.Min(boxes[a].size.x, Mathf.Min(boxes[a].size.y, boxes[a].size.z)),
                        Mathf.Min(boxes[b].size.x, Mathf.Min(boxes[b].size.y, boxes[b].size.z)));
                    // 하한과 **상한**을 둘 다 둔다: 얇은 것은 닿기만 해도 걸리지 않게(하한 0.25m),
                    // 두꺼운 것끼리는 깊게 물려도 숨지 않게(상한 0.40m — 실측: 수레가 좌판에 0.62m
                    // 박혀 있는데 「얇은 쪽의 절반」 규칙만 쓰면 봐줬다, 2026-09-09).
                    float need = Mathf.Clamp(thin * VillagePropOverlapFrac, VillagePropMinBite, 0.40f);
                    if (least < need)
                    {
                        if (least > DeepestForgiven)
                        {
                            DeepestForgiven = least;
                            DeepestForgivenWhat = props[a].name + " ↔ " + props[b].name;
                        }
                        continue;
                    }
                    ia.Add(a);
                    ib.Add(b);
                    deep.Add(least);
                }
            }
        }

        static string VillagePropClashPairsReason(bool log)
        {
            var ia = new List<int>();
            var ib = new List<int>();
            var deep = new List<float>();
            var props = new List<Transform>();
            var boxes = new List<Bounds>();
            CollectPropClashes(ia, ib, deep, props, boxes);
            if (props.Count == 0)
                return "마을 소품을 하나도 못 읽었습니다 — 잰 것이 없습니다(0이면 실패).";
            if (log)
                Debug.Log("[Ulon] 마을 소품끼리 — 소품 " + props.Count + "개 · 파고든 쌍 " + ia.Count +
                          "건(같은 물건끼리·자연물끼리는 봐준다, 하한 " + VillagePropMinBite.ToString("0.00") +
                          "m) · 봐준 것 중 가장 깊은 것 " + DeepestForgiven.ToString("0.00") + "m " + DeepestForgivenWhat);
            if (ia.Count == 0)
                return "";
            var bad = new List<string>();
            for (int i = 0; i < ia.Count; i++)
                bad.Add(props[ia[i]].name + " ↔ " + props[ib[i]].name + " 물린 깊이 " +
                        deep[i].ToString("0.00") + "m @(" + boxes[ia[i]].center.x.ToString("0.0") + "," +
                        boxes[ia[i]].center.z.ToString("0.0") + ")");
            bad.Sort(StringComparer.Ordinal);
            return "마을 소품이 서로 파고들었습니다 — " + bad.Count + "건:\n  " +
                   string.Join("\n  ", bad.GetRange(0, Mathf.Min(bad.Count, 8))) +
                   (bad.Count > 8 ? "\n  … 외 " + (bad.Count - 8) + "건" : "") +
                   "\n장작이 울타리를 뚫고 서 있으면 그 자리는 만든 자리로 안 읽힙니다(§8.2).";
        }

        /// <summary>
        /// **불과 나무 담 사이는 이만큼 띄운다.** 한 걸음(사람 보폭 ≈1m) — 화면에서 「불이 담에 옮겨붙겠다」로
        /// 안 읽히는 최소 거리다(검수 판정 2026-09-09). 자와 굽는 쪽(`KeepFireOffWalls`)이 같은 값을 읽는다.
        /// </summary>
        internal const float FireWallGap = 1.0f;

        static string FireWallReason(bool log)
        {
            var fire = GameObject.Find("Campfire");
            if (fire == null)
                throw new InvalidOperationException("화덕(Campfire)이 없습니다 — 잰 것이 없습니다(0이면 실패).");
            var decor = GameObject.Find("VillageDecor");
            if (decor == null)
                throw new InvalidOperationException("VillageDecor가 없습니다 — 잰 것이 없습니다(0이면 실패).");
            Transform near = null;
            float best = float.MaxValue;
            int walls = 0;
            foreach (var t in decor.GetComponentsInChildren<Transform>(false))
            {
                if (!t.name.StartsWith("fence", StringComparison.Ordinal) &&
                    !t.name.StartsWith("hedge", StringComparison.Ordinal))
                    continue;
                if (t.GetComponentInChildren<Renderer>(true) == null)
                    continue;
                walls++;
                float d = new Vector2(t.position.x - fire.transform.position.x,
                                      t.position.z - fire.transform.position.z).magnitude;
                if (d < best) { best = d; near = t; }
            }
            if (walls == 0)
                throw new InvalidOperationException("마을 담을 한 조각도 못 읽었습니다 — 잰 것이 없습니다(0이면 실패).");
            if (log)
                Debug.Log("[Ulon] 불과 담 — 가장 가까운 담 " + best.ToString("0.00") + "m " +
                          (near == null ? "(없음)" : near.name) + "(하한 " + FireWallGap.ToString("0.0") + "m)");
            if (best >= FireWallGap)
                return "";
            return "화덕이 나무 담에서 " + best.ToString("0.00") + "m입니다(하한 " + FireWallGap.ToString("0.0") +
                   "m) — 불이 담에 옮겨붙을 자리에 있으면 그 자리는 만든 자리로 안 읽힙니다.";
        }

        static void AssertFireOffWalls()
        {
            string reason = FireWallReason(true);
            if (!string.IsNullOrEmpty(reason))
                throw new InvalidOperationException(reason);
        }

        /// <summary>양방향 NC — 화덕을 담에 붙이면 빨간불, 되돌리면 초록.</summary>
        static void AssertFireOffWallsNegativeControl()
        {
            var fire = GameObject.Find("Campfire");
            var decor = GameObject.Find("VillageDecor");
            if (fire == null || decor == null)
                throw new InvalidOperationException("불·담 NC 대상이 없습니다(0이면 실패).");
            Transform wall = null;
            foreach (var t in decor.GetComponentsInChildren<Transform>(false))
            {
                if (!t.name.StartsWith("fence", StringComparison.Ordinal))
                    continue;
                if (t.GetComponentInChildren<Renderer>(true) == null)
                    continue;
                if (wall == null || string.CompareOrdinal(t.name, wall.name) < 0)
                    wall = t;                                // 희생양은 정해서 고른다
            }
            if (wall == null)
                throw new InvalidOperationException("불·담 NC 대상 담이 없습니다(0이면 실패).");
            var kept = fire.transform.position;
            bool red;
            try
            {
                fire.transform.position = new Vector3(wall.position.x + 0.2f, kept.y, wall.position.z);
                Physics.SyncTransforms();
                red = !string.IsNullOrEmpty(FireWallReason(false));
            }
            finally
            {
                fire.transform.position = kept;
                Physics.SyncTransforms();
            }
            if (!red)
                throw new InvalidOperationException("불·담 네거티브 컨트롤 실패 — 화덕을 담에 붙였는데 통과했습니다.");
            string back = FireWallReason(false);
            if (!string.IsNullOrEmpty(back))
                throw new InvalidOperationException("불·담 네거티브 컨트롤 실패 — 되돌렸는데도 빨간불입니다: " + back);
            Debug.Log("[Ulon] 불과 담 양방향 NC 통과 — 붙이면 FAIL · 되돌리면 통과");
        }

        static void AssertVillagePropsNotClashing()
        {
            string reason = VillagePropClashPairsReason(true);
            if (!string.IsNullOrEmpty(reason))
                throw new InvalidOperationException(reason);
        }

        /// <summary>양방향 NC — 다른 종류 소품 둘을 겹치면 빨간불, 되돌리면 초록.</summary>
        static void AssertVillagePropsNotClashingNegativeControl()
        {
            var props = new List<Transform>();
            var boxes = new List<Bounds>();
            var bn = new List<string>();
            var bb = new List<Bounds>();
            CollectVillage(props, boxes, bn, bb);
            // 희생양은 정해서 고른다 — 이름 순으로 **서로 종류가 다른** 첫 쌍.
            Transform mover = null, host = null;
            Bounds hostBox = new Bounds();
            for (int i = 0; i < props.Count; i++)
            {
                if (props[i] == null || IsFlatMat(boxes[i]))
                    continue;
                if (mover == null || string.CompareOrdinal(props[i].name, mover.name) < 0)
                { mover = props[i]; }
            }
            for (int i = 0; i < props.Count; i++)
            {
                if (props[i] == null || props[i] == mover || IsFlatMat(boxes[i]))
                    continue;
                if (PropPairForgiven(props[i].name, mover.name))
                    continue;
                if (host == null || string.CompareOrdinal(props[i].name, host.name) < 0)
                { host = props[i]; hostBox = boxes[i]; }
            }
            if (mover == null || host == null)
                throw new InvalidOperationException("마을 소품 NC 대상이 없습니다 — 종류가 다른 쌍이 없습니다(0이면 실패).");

            var kept = mover.position;
            bool red;
            try
            {
                mover.position = hostBox.center;
                Physics.SyncTransforms();
                red = !string.IsNullOrEmpty(VillagePropClashPairsReason(false));
            }
            finally
            {
                mover.position = kept;
                Physics.SyncTransforms();
            }
            if (!red)
                throw new InvalidOperationException("마을 소품 네거티브 컨트롤 실패 — " + mover.name + "을(를) " +
                    host.name + " 자리에 겹쳤는데 통과했습니다.");
            string back = VillagePropClashPairsReason(false);
            if (!string.IsNullOrEmpty(back))
                throw new InvalidOperationException("마을 소품 네거티브 컨트롤 실패 — 되돌렸는데도 빨간불입니다: " + back);
            Debug.Log("[Ulon] 마을 소품끼리 양방향 NC 통과 — 종류가 다른 둘을 겹치면 FAIL · 되돌리면 통과");
        }
    }
}
