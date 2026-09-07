using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// §8.1 — **씬에 놓인 모든 것**의 발/밑면이 그 자리 지표(던전 안이면 방 바닥)에 닿아야 한다.
        /// 사냥터에만 걸어 뒀더니 지형 높이를 올린 랩이 마을 건물·소품을 그대로 두고 갔다
        /// (검수 2026-09-06 A). 첫 실행 실측: 마을 건물·상인·장식 38개가 지표 10m 아래에 묻혀 있었다.
        /// 판정 대상·바운드·기대 높이는 `GroundFit` 공용 원장을 쓴다 — 보수 패스와 같은 자.
        /// </summary>
        const float GlobalFootErrorMax = 0.10f;

        static void AssertFootOnGround()
        {
            var offenders = new List<(string Name, float Dy)>();
            int checkedCount = 0;
            var items = GroundFit.Candidates();
            for (int i = 0; i < items.Count; i++)
            {
                // **몸 기준**으로 통일한다 — 무기를 넣으면 「발」이 칼끝이 되고,
                // 몸을 바닥에 붙이는 보수 패스와 서로 다른 자를 보게 된다(2026-09-07 실측 Hexarch −0.87m).
                if (!GroundFit.BodyBounds(items[i], out Bounds b))
                    continue;
                float dy = b.min.y - GroundFit.ExpectedGroundY(items[i], b);
                checkedCount++;
                if (Mathf.Abs(dy) > GlobalFootErrorMax)
                    offenders.Add((GroundFit.NodePath(items[i]), dy));
            }

            offenders.Sort((a, b) => Mathf.Abs(b.Dy).CompareTo(Mathf.Abs(a.Dy)));
            // **몇 개를 쟀고 몇 개를 왜 뺐는지**를 같이 적는다(검수 조건 2026-09-07) —
            // 침묵으로 빠진 것이 259개 부양 바위를 숨겼다.
            Debug.Log("[Ulon] §8.1 전역 발 높이 — 대상 " + checkedCount + "개(배치 단위 " + items.Count +
                      "), 오차 " + GlobalFootErrorMax + "m 초과 " + offenders.Count + "개 · 선언 제외 " +
                      GroundFit.LastExcluded.Count + "개: " +
                      string.Join(", ", GroundFit.LastExcluded.GetRange(0, Mathf.Min(12, GroundFit.LastExcluded.Count))));
            for (int i = 0; i < offenders.Count && i < 15; i++)
                Debug.Log("[Ulon]   발 높이 이탈 " + offenders[i].Name + " " + offenders[i].Dy.ToString("0.00") + "m");
            if (offenders.Count > 0)
                throw new InvalidOperationException("§8.1 배치물 " + offenders.Count + "개의 발/밑면이 지표에서 " +
                    GlobalFootErrorMax + "m 넘게 벗어났습니다(최악 " + offenders[0].Name + " " + offenders[0].Dy.ToString("0.00") +
                    "m). 지형 높이를 바꾸면 그 위에 선 것도 같이 옮겨야 합니다.");
        }

        /// <summary>
        /// 네거티브 컨트롤 — **양쪽으로** 만든다(검수 조건 2026-09-07): 하나를 실제로 3m 띄우고,
        /// 하나를 3m 묻는다. 결함은 한 방향만 나지 않는다 — 산포 바위는 떠 있었고 물레방아는 묻혀 있었다.
        /// </summary>
        static void AssertFootNegativeControl()
        {
            var items = GroundFit.Candidates();
            var picks = new List<Transform>();
            for (int i = 0; i < items.Count && picks.Count < 2; i++)
                if (GroundFit.WorldBounds(items[i], out Bounds _))
                    picks.Add(items[i]);
            if (picks.Count < 2)
                throw new InvalidOperationException("발 높이 네거티브 컨트롤 대상이 " + picks.Count +
                    "개뿐입니다 — 배치물이 안 잡힙니다(0이면 실패).");

            for (int k = 0; k < 2; k++)
            {
                var victim = picks[k];
                float dy = k == 0 ? 3.0f : -3.0f;              // 띄우기 / 묻기
                var saved = victim.position;
                bool red = false;
                try
                {
                    victim.position = saved + Vector3.up * dy;
                    try { AssertFootOnGround(); }
                    catch (InvalidOperationException) { red = true; }
                }
                finally { victim.position = saved; }
                if (!red)
                    throw new InvalidOperationException("발 높이 네거티브 컨트롤 실패 — " + victim.name + "를 " +
                        dy.ToString("0") + "m 옮겼는데도 통과했습니다.");
                Debug.Log("[Ulon] 발 높이 네거티브 컨트롤 통과 — " + victim.name + " " + dy.ToString("0") + "m 이동 시 FAIL");
            }
        }
    }
}
