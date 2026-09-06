using System;
using System.Collections.Generic;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// 소품 크기를 **플레이어 키에 대한 비율**로 잰다(검수 2026-09-06: 통이 플레이어보다 커서
    /// 방이 「거인의 창고」로 읽혔다). 절대 수치로 박으면 캐릭터 스케일을 바꾸는 순간 또 어긋난다.
    /// 양쪽 한계를 둔다 — 너무 크면 시야를 막고, 너무 작으면 바닥 먼지처럼 안 보인다.
    /// 구조물(기둥)과 벽걸이(횃불·등불)는 예외다 — 천장까지 닿는 것이 정상인 물건을 이 자로 재면 게이트가 거짓말을 한다.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        const float PropHeightFracMax = 0.80f;
        const float PropHeightFracMin = 0.15f;

        static void AssertPropScaleRatio()
        {
            CheckPropScale("던전 1", Dungeon1.InteriorObject);
            CheckPropScale("던전 2", Dungeon2.InteriorObject);
            CheckPropScale("던전 3", Dungeon3.InteriorObject);
            Debug.Log("[Ulon] 실내 소품 크기 통과 — 플레이어 키(" + VisualSliceBuilder.PlayerHeight + "m)의 " +
                      PropHeightFracMin + "~" + PropHeightFracMax + "배");
        }

        static void CheckPropScale(string label, string interiorObject)
        {
            var interior = GameObject.Find(interiorObject);
            if (interior == null)
                throw new InvalidOperationException(interiorObject + "이(가) 없습니다.");
            int measured = 0;
            float tallest = 0f;
            string tallestName = "";
            foreach (var t in PropNodes(interior))
            {
                if (IsStructureProp(t)) continue;
                if (!GroundFit.WorldBounds(t, out Bounds wb)) continue;
                measured++;
                float frac = wb.size.y / VisualSliceBuilder.PlayerHeight;
                if (frac > tallest) { tallest = frac; tallestName = t.name; }
                if (frac > PropHeightFracMax)
                    throw new InvalidOperationException(label + "의 소품 " + t.name + " 높이가 플레이어 키의 " +
                        frac.ToString("0.00") + "배입니다 — 상한 " + PropHeightFracMax + "배(방이 거인의 창고로 읽힌다).");
                if (frac < PropHeightFracMin)
                    throw new InvalidOperationException(label + "의 소품 " + t.name + " 높이가 플레이어 키의 " +
                        frac.ToString("0.00") + "배입니다 — 하한 " + PropHeightFracMin + "배(바닥 먼지처럼 안 보인다).");
            }
            if (measured == 0)
                throw new InvalidOperationException(label + "에서 크기를 잰 소품이 0개입니다 — 게이트가 아무것도 안 재고 통과했습니다.");
            Debug.Log("[Ulon] 실내 소품 크기 " + label + " — " + measured + "개, 최대 " +
                      tallest.ToString("0.00") + "배(" + tallestName + ")");
        }

        /// <summary>기둥은 천장까지, 횃불은 벽 2m에 있는 것이 정상이다 — 비율 자에서 뺀다.</summary>
        static bool IsStructureProp(Transform t)
        {
            return t.name.StartsWith("DungeonFurnPillar", StringComparison.Ordinal)
                || t.name.StartsWith("DungeonFurnTorch", StringComparison.Ordinal)
                || t.name.StartsWith("DungeonFurnLantern", StringComparison.Ordinal);
        }

        /// <summary>네거티브 컨트롤 — 통 하나를 실제로 2배로 키우면 빨간불이어야 한다.</summary>
        static void AssertPropScaleNegativeControl()
        {
            var interior = GameObject.Find(Dungeon1.InteriorObject);
            if (interior == null)
                throw new InvalidOperationException("던전 1 실내가 없어 크기 네거티브 컨트롤을 할 수 없습니다.");
            Transform victim = null;
            foreach (var t in PropNodes(interior))
                if (!IsStructureProp(t)) { victim = t; break; }
            if (victim == null)
                throw new InvalidOperationException("크기를 키울 소품을 찾지 못했습니다.");

            var saved = victim.localScale;
            bool red = false;
            try
            {
                victim.localScale = saved * 2f;
                try { AssertPropScaleRatio(); }
                catch (Exception e) { red = true; Debug.Log("[Ulon] 소품 크기 네거티브 컨트롤 빨간불 — " + e.Message); }
            }
            finally
            {
                victim.localScale = saved;
                Physics.SyncTransforms();
            }
            if (!red)
                throw new InvalidOperationException("소품 크기 네거티브 컨트롤 실패 — 소품을 2배로 키웠는데 게이트가 통과했습니다.");
            AssertPropScaleRatio();     // 복구 확인
        }
    }
}
