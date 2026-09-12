using System;
using System.IO;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// 기획 §4.2. 홀드를 떼면 멈춘다. 짧은 클릭(1프레임)은 목적지 유지.
    /// NC: NcOpen 이면 떼도 안 멈춰 빨간불.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        static void AssertClickHold()
        {
            if (ClickHold.NcOpen)
                throw new InvalidOperationException("ClickHold.NcOpen 가 켜져 있으면 홀드를 떼도 안 멈춥니다.");
            if (ClickHold.StopOnRelease(1))
                throw new InvalidOperationException("1프레임 클릭은 목적지까지 걸어야 합니다.");
            if (!ClickHold.StopOnRelease(2))
                throw new InvalidOperationException("2프레임 이상 홀드를 떼면 멈춰야 합니다.");
            if (!ClickHold.StopOnRelease(8))
                throw new InvalidOperationException("긴 홀드를 떼면 멈춰야 합니다.");
            if (ClickHold.StopOnRelease(0))
                throw new InvalidOperationException("안 누른 프레임은 정지가 아닙니다.");

            string avatarPath = Path.Combine(Application.dataPath, "Game/Scripts/Client/LocalAvatar.cs");
            string avatar = File.ReadAllText(avatarPath);
            if (avatar.IndexOf("ClickHold.StopOnRelease", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("LocalAvatar가 홀드 해제를 안 탑니다.");
            if (avatar.IndexOf("motor.Stop()", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("LocalAvatar가 홀드 해제 시 Stop을 안 부릅니다.");

            string motorPath = Path.Combine(Application.dataPath, "Game/Scripts/Client/ClickMotor.cs");
            string motor = File.ReadAllText(motorPath);
            if (motor.IndexOf("public void Stop()", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("ClickMotor.Stop이 없습니다.");

            Debug.Log("[Ulon] 클릭 홀드 — 떼면 정지 · 짧은 클릭은 목적지 유지");
        }

        static void AssertClickHoldNegativeControl()
        {
            bool was = ClickHold.NcOpen;
            bool red = false;
            try
            {
                ClickHold.NcOpen = true;
                try { AssertClickHold(); }
                catch (InvalidOperationException) { red = true; }
            }
            finally { ClickHold.NcOpen = was; }
            if (!red)
                throw new InvalidOperationException("클릭 홀드 네거티브 컨트롤 실패 — NcOpen 인데 통과했습니다.");
            Debug.Log("[Ulon] 클릭 홀드 네거티브 컨트롤 통과 — NcOpen 이면 FAIL");
        }
    }
}
