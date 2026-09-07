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
            Debug.Log("[Ulon] §8.1 전역 발 높이 — 대상 " + checkedCount + "개, 오차 " + GlobalFootErrorMax + "m 초과 " + offenders.Count + "개");
            for (int i = 0; i < offenders.Count && i < 15; i++)
                Debug.Log("[Ulon]   발 높이 이탈 " + offenders[i].Name + " " + offenders[i].Dy.ToString("0.00") + "m");
            if (offenders.Count > 0)
                throw new InvalidOperationException("§8.1 배치물 " + offenders.Count + "개의 발/밑면이 지표에서 " +
                    GlobalFootErrorMax + "m 넘게 벗어났습니다(최악 " + offenders[0].Name + " " + offenders[0].Dy.ToString("0.00") +
                    "m). 지형 높이를 바꾸면 그 위에 선 것도 같이 옮겨야 합니다.");
        }

        /// <summary>네거티브 컨트롤 — 배치물 하나를 실제로 1m 내려 게이트가 빨간불이 되는지 본다.</summary>
        static void AssertFootNegativeControl()
        {
            var items = GroundFit.Candidates();
            Transform victim = null;
            for (int i = 0; i < items.Count; i++)
                if (GroundFit.WorldBounds(items[i], out Bounds _)) { victim = items[i]; break; }
            if (victim == null)
                throw new InvalidOperationException("발 높이 네거티브 컨트롤 대상이 없습니다 — 배치물이 하나도 안 잡힙니다.");

            var saved = victim.position;
            bool red = false;
            try
            {
                victim.position = saved - Vector3.up * 1.0f;
                try { AssertFootOnGround(); }
                catch (InvalidOperationException) { red = true; }
            }
            finally
            {
                victim.position = saved;
            }
            if (!red)
                throw new InvalidOperationException("발 높이 게이트 네거티브 컨트롤 실패 — " + victim.name + "를 1m 내렸는데도 통과했습니다.");
            Debug.Log("[Ulon] 발 높이 게이트 네거티브 컨트롤 통과 — " + victim.name + " 1m 하강 시 FAIL");
        }
    }
}
