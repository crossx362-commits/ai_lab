using System;
using System.Collections.Generic;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **아마밭이 밭으로 읽히는가**(검수 완료 기준 2026-09-07, 랩 ⑤).
    ///
    /// 반려 원문: 「20cm 덤불 하나가 아마밭」. 「존재하는가」가 아니라 **밭으로 읽히는가**를 재야 한다.
    /// 밭을 밭으로 만드는 것은 셋이다 — ⓐ 갈아엎은 흙(지표 도포) ⓑ **줄줄이 반복되는 작물**
    /// ⓒ 두른 울타리. 셋을 각각 화면에서 읽히는 성질로 잰다:
    ///   ⓐ 밭 한가운데 지표의 `Tilled` 도포 비중 (풀이 이기면 풀밭이다)
    ///   ⓑ 작물 포기 수 + **줄이 몇 개인지**(z를 묶어 센다 — 한 줄에 몰아 심으면 밭이 아니라 화단이다)
    ///   ⓒ 네 변에 울타리 조각이 있는지
    /// 「몇 개 놓았다」를 세지 않는다 — 놓은 것이 아니라 **씬에 실제로 선 것**을 전수로 읽는다.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        const float FlaxTilledMin = 0.45f;      // 밭 한가운데에서 흙이 이 이상이어야 「갈아엎은 밭」
        const int FlaxRowMin = 4;               // 이랑이 최소 몇 줄 읽혀야 하는가
        const float FlaxRowGap = 0.6f;          // 이 이상 떨어지면 다른 줄로 센다

        static List<Transform> FlaxStalks()
        {
            var found = new List<Transform>();
            var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
                if (all[i].name == "FlaxStalk" || all[i].name == "FieldFlax")
                    found.Add(all[i]);
            return found;
        }

        static void AssertFlaxFieldReadsAsField()
        {
            // ⓐ 흙 — 빌더가 칠할 때 쓴 같은 함수를 읽는다(두 벌로 적으면 어긋난다).
            float tilled = WorldSplat.FlaxCoverAt(WorldSplat.FlaxX, WorldSplat.FlaxZ);
            if (tilled < FlaxTilledMin)
                throw new InvalidOperationException("아마밭 한가운데의 갈아엎은 흙 도포가 " + tilled.ToString("0.00") +
                    "뿐입니다 — 최소 " + FlaxTilledMin.ToString("0.00") + ". 바닥이 잔디면 「풀밭에 놓인 풀」로 읽힙니다(§8.1).");

            // ⓑ 작물과 줄 — 씬에 실제로 선 것을 전수로.
            var stalks = FlaxStalks();
            if (stalks.Count == 0)
                throw new InvalidOperationException("아마밭에 작물이 하나도 없습니다 — 잰 것이 없습니다(0이면 실패).");
            var rows = new List<float>();
            var inside = 0;
            for (int i = 0; i < stalks.Count; i++)
            {
                var p = stalks[i].position;
                if (Mathf.Abs(p.x - WorldSplat.FlaxX) > WorldSplat.FlaxHalfX ||
                    Mathf.Abs(p.z - WorldSplat.FlaxZ) > WorldSplat.FlaxHalfZ)
                    continue;                       // 울타리 밖의 것은 밭이 아니다
                inside++;
                bool merged = false;
                for (int r = 0; r < rows.Count; r++)
                    if (Mathf.Abs(rows[r] - p.z) < FlaxRowGap) { merged = true; break; }
                if (!merged)
                    rows.Add(p.z);
            }
            int want = VisualSliceBuilder.FlaxRows * VisualSliceBuilder.FlaxPerRow / 2;
            if (inside < want)
                throw new InvalidOperationException("아마밭 안의 작물이 " + inside + "포기뿐입니다 — 최소 " + want +
                    ". 한 포기짜리 「밭」은 화면에서 덤불이다(검수 반려 원문: 20cm 덤불 하나가 아마밭이었다).");
            if (rows.Count < FlaxRowMin)
                throw new InvalidOperationException("아마밭의 이랑이 " + rows.Count + "줄뿐입니다 — 최소 " + FlaxRowMin +
                    ". 한 줄에 몰아 심으면 밭이 아니라 화단으로 읽힙니다.");

            // ⓒ 울타리 — 네 변에 조각이 실제로 서 있는가.
            var fenceSides = new bool[4];
            // 이름은 **놓인 오브젝트**에 붙는다 — 렌더러는 그 안의 `Visual`이라 이름으로 못 찾는다
            // (처음 판이 그래서 「울타리 0변」을 냈다). 뿌리 쪽 이름을 보고, 자리는 렌더러 바운드로 잰다.
            var all = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                bool isFence = false;
                for (var t = all[i].transform; t != null; t = t.parent)
                    if (t.name.IndexOf("fence", StringComparison.OrdinalIgnoreCase) >= 0) { isFence = true; break; }
                if (!isFence)
                    continue;
                var p = all[i].bounds.center;
                float dx = p.x - WorldSplat.FlaxX, dz = p.z - WorldSplat.FlaxZ;
                if (Mathf.Abs(dx) > WorldSplat.FlaxHalfX + 1.5f || Mathf.Abs(dz) > WorldSplat.FlaxHalfZ + 1.5f)
                    continue;
                if (dz < -WorldSplat.FlaxHalfZ + 0.8f) fenceSides[0] = true;
                if (dz > WorldSplat.FlaxHalfZ - 0.8f) fenceSides[1] = true;
                if (dx < -WorldSplat.FlaxHalfX + 0.8f) fenceSides[2] = true;
                if (dx > WorldSplat.FlaxHalfX - 0.8f) fenceSides[3] = true;
            }
            int sides = 0;
            for (int i = 0; i < 4; i++) if (fenceSides[i]) sides++;
            if (sides < 4)
                throw new InvalidOperationException("아마밭을 두른 울타리가 " + sides +
                    "변뿐입니다 — 네 변이 있어야 「뙈기」로 읽힙니다(농경지 반려와 같은 규칙).");

            // **밭 한가운데를 가로지르는 울타리가 있으면 밭이 아니다** — 첫 판이 그렇게 찍혔다(빗살 무늬).
            // 네 변에서 1.2m 넘게 안쪽으로 들어온 울타리 조각을 센다.
            var intruders = new List<string>();
            for (int i = 0; i < all.Length; i++)
            {
                bool isFence = false;
                for (var t = all[i].transform; t != null; t = t.parent)
                    if (t.name.IndexOf("fence", StringComparison.OrdinalIgnoreCase) >= 0) { isFence = true; break; }
                if (!isFence)
                    continue;
                var p = all[i].bounds.center;
                if (Mathf.Abs(p.x - WorldSplat.FlaxX) < WorldSplat.FlaxHalfX - 1.2f &&
                    Mathf.Abs(p.z - WorldSplat.FlaxZ) < WorldSplat.FlaxHalfZ - 1.2f)
                    intruders.Add(GroundFit.NodePath(all[i].transform) + p.ToString("F1"));
            }
            if (intruders.Count > 0)
                throw new InvalidOperationException("아마밭 **안쪽**에 울타리 조각 " + intruders.Count + "개가 서 있습니다: " +
                    string.Join(", ", intruders.GetRange(0, Mathf.Min(6, intruders.Count))) +
                    " — 밭을 가로지르는 울타리는 경작지가 아니라 우리로 읽힙니다.");

            Debug.Log("[Ulon] 아마밭 — 흙 도포 " + tilled.ToString("0.00") + ", 작물 " + inside + "포기·이랑 " +
                      rows.Count + "줄, 울타리 " + sides + "변·안쪽 침범 0개");
        }

        /// <summary>NC — **작물을 실제로 지우면** 빨간불이어야 한다(반려 당시 상태 = 한 포기).</summary>
        static void AssertFlaxFieldNegativeControl()
        {
            var stalks = FlaxStalks();
            if (stalks.Count == 0)
                throw new InvalidOperationException("아마밭 NC 대상(작물)이 없습니다 — 잰 것이 없습니다(0이면 실패).");
            var off = new List<GameObject>();
            bool red = false;
            string message = "";
            try
            {
                for (int i = 0; i < stalks.Count; i++)
                {
                    if (stalks[i].name == "FieldFlax")
                        continue;                    // 반려 당시처럼 「한 포기만」 남긴다
                    off.Add(stalks[i].gameObject);
                    stalks[i].gameObject.SetActive(false);
                }
                if (FlaxStalks().Count > 1)
                    throw new InvalidOperationException("아마밭 NC가 결함을 못 만들었습니다 — 아직 " +
                        FlaxStalks().Count + "포기가 남아 있습니다.");
                try { AssertFlaxFieldReadsAsField(); }
                catch (InvalidOperationException e) { red = true; message = e.Message; }
            }
            finally
            {
                for (int i = 0; i < off.Count; i++)
                    off[i].SetActive(true);
            }
            if (!red)
                throw new InvalidOperationException("아마밭 네거티브 컨트롤 실패 — 한 포기만 남겼는데 통과했습니다.");
            Debug.Log("[Ulon] 아마밭 네거티브 컨트롤 통과 — 반려 당시처럼 한 포기만 남기면 FAIL: " + message);
        }
    }
}
