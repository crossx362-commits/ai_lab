using System;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// **문 뒤에 산이 있나 — 시선으로 잰다**(대장 조건 ③, 2026-09-09).
        ///
        /// 「지하로 내려간다」는 높이 숫자가 아니라 **화면에서 문 위가 막혀 보이는가**로 정해진다.
        /// 그래서 자도 높이를 직접 묻지 않고 **QA 눈에서 두 줄기 시선**을 쏜다:
        /// - ⓐ 문구멍으로 가는 시선은 **뚫려 있어야** 한다(언덕이 입구를 삼키면 안 된다).
        /// - ⓑ 문 위 3m를 지나는 시선은 문 뒤 30m 안에서 **지형에 막혀야** 한다(뒤가 하늘이면 들판 기념문이다).
        ///
        /// 눈은 QA 샷과 같은 자리(진입로 쪽 8m·20°)다. 높이는 **구워진 지형**을 읽는다 — 화면이 보는 것.
        /// NC는 `EntranceGeom.MoundDisabled`로 언덕만 0으로 만들고 **높이 함수**로 다시 잰다:
        /// 언덕이 없으면 ⓑ가 뚫려야 한다(안 뚫리면 이 자는 언덕이 아니라 딴것을 재고 있다).
        /// </summary>
        const float MoundSightBlockWithin = 30f;   // 문 뒤 이만큼 안에서 막혀야 한다
        const float MoundSightLintelUp = 3f;       // 문 위 이만큼을 지나는 시선
        const float MoundEyeDist = 8f, MoundEyeElevDeg = 20f;
        const float MoundSightGainMin = 3f;    // 언덕이 시선을 이만큼은 앞당겨야 「언덕이 일했다」

        static void AssertMoundBehind()
        {
            string report = "";
            foreach (var d in EntranceGeom.All)
            {
                var front = EntranceGeom.Front(d.X, d.Z, d.Yaw);
                float doorY = GroundFit.TerrainY(d.X, d.Z);
                var eye = new Vector3(d.X + front.x * MoundEyeDist, 0f, d.Z + front.y * MoundEyeDist);
                eye.y = GroundFit.TerrainY(eye.x, eye.z) + MoundEyeDist * Mathf.Tan(MoundEyeElevDeg * Mathf.Deg2Rad) + 1.6f;

                var mouth = new Vector3(d.X, doorY + 1.2f, d.Z);
                if (SightBlocked(eye, mouth, 0.5f, MoundEyeDist - 0.5f, false, out float hitA))
                    throw new InvalidOperationException("문구멍으로 가는 시선이 지형에 막힙니다(" + d.Root + ", " +
                        hitA.ToString("0.0") + "m 앞) — 언덕이 입구를 삼켰습니다.");

                var lintel = new Vector3(d.X, doorY + MoundSightLintelUp, d.Z);
                if (!SightBlocked(eye, lintel, MoundEyeDist + 1f, MoundEyeDist + MoundSightBlockWithin, false, out float hitB))
                    throw new InvalidOperationException("문 위 " + MoundSightLintelUp + "m를 지나는 시선이 문 뒤 " +
                        MoundSightBlockWithin + "m 안에서 지형에 막히지 않습니다(" + d.Root +
                        ") — 문 뒤가 하늘이면 들판의 기념문으로 읽힙니다.");
                report += " · " + d.Root + " 뒤 " + (hitB - MoundEyeDist).ToString("0.0") + "m에서 막힘";

                // NC — 언덕만 0으로 두고 **같은 시선을 다시 잰다**. 언덕이 일한 만큼 막히는 자리가 멀어져야 한다.
                // (D1처럼 뒤가 원래 산비탈인 곳도 있다 — 그래서 「막히나」가 아니라 「얼마나 앞당겼나」로 잰다.)
                if (!SightBlocked(eye, lintel, MoundEyeDist + 1f, MoundEyeDist + MoundSightBlockWithin, true, out float hitFn))
                    throw new InvalidOperationException("높이 함수로는 언덕이 있어도 문 뒤가 안 막힙니다(" + d.Root +
                        ") — 구워진 지형과 높이 함수가 어긋났습니다.");
                float flatHit;
                bool flatBlocked;
                EntranceGeom.MoundDisabled = true;
                try
                {
                    flatBlocked = SightBlocked(eye, lintel, MoundEyeDist + 1f, MoundEyeDist + MoundSightBlockWithin, true, out flatHit);
                }
                finally { EntranceGeom.MoundDisabled = false; }
                float gained = (flatBlocked ? flatHit : MoundEyeDist + MoundSightBlockWithin) - hitFn;
                report += "(언덕이 " + gained.ToString("0.0") + "m 앞당김)";
                if (gained < MoundSightGainMin)
                    throw new InvalidOperationException("NC 실패 — 언덕 높이를 0으로 둬도 문 뒤가 " +
                        (flatBlocked ? flatHit.ToString("0.0") + "m에서 막힙니다" : "같습니다") + "(" + d.Root +
                        ", 언덕이 앞당긴 거리 " + gained.ToString("0.0") + "m < " + MoundSightGainMin +
                        "m). 이 입구의 지하감은 언덕이 만든 것이 아닙니다.");
            }
            Debug.Log("[Ulon] 문 뒤 언덕(시선) 통과 — 문구멍은 뚫리고 문 위는 막힌다" + report);
        }

        /// <summary>눈에서 목표로 가는 직선이 `from`~`to`(눈으로부터의 거리) 구간에서 지형에 잠기는가.</summary>
        static bool SightBlocked(Vector3 eye, Vector3 target, float from, float to, bool byHeightFunc, out float atDist)
        {
            var dir = (target - eye).normalized;
            for (float s = from; s <= to; s += 0.5f)
            {
                var p = eye + dir * s;
                float g = byHeightFunc ? WorldTerrain.HeightAt(p.x, p.z) : GroundFit.TerrainY(p.x, p.z);
                if (g > p.y)
                {
                    atDist = s;
                    return true;
                }
            }
            atDist = 0f;
            return false;
        }
    }
}
