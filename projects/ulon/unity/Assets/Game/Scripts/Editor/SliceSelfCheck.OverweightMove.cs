using System;
using System.IO;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// 기획 §18.5. 과적이면 달리기 불가. NC: NcOpen 이면 과적 달리기가 살아 빨간불.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        static void AssertOverweightMove()
        {
            if (CarryMove.NcOpen)
                throw new InvalidOperationException("CarryMove.NcOpen 가 켜져 있으면 과적해도 달립니다.");
            if (!CarryMove.CanRun(false))
                throw new InvalidOperationException("과적이 아니면 달릴 수 있어야 합니다.");
            if (CarryMove.CanRun(true))
                throw new InvalidOperationException("과적이면 달리면 안 됩니다.");

            string motorPath = Path.Combine(Application.dataPath, "Game/Scripts/Client/ClickMotor.cs");
            string motor = File.ReadAllText(motorPath);
            if (motor.IndexOf("CarryMove.CanRun", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("ClickMotor가 과적 달리기 원장을 안 탑니다.");
            if (motor.IndexOf("0.35f", StringComparison.Ordinal) >= 0 ||
                motor.IndexOf("planar *= 0.35", StringComparison.Ordinal) >= 0)
                throw new InvalidOperationException("ClickMotor에 출처 없는 과적 0.35 감속이 남아 있습니다.");

            string movePath = Path.Combine(Application.dataPath, "Game/Scripts/Client/NetAvatar.Move.cs");
            string move = File.ReadAllText(movePath);
            if (move.IndexOf("CarryMove.CanRun", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("이동 RPC가 과적 달리기를 서버에서 안 막습니다.");

            string hud = HudSourceText();
            if (hud.IndexOf("과적", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("HUD에 과적 표시가 없습니다.");

            Debug.Log("[Ulon] 과적 이동 — 달리기 불가 · 모터/RPC CarryMove · HUD 과적");
        }

        static void AssertOverweightMoveNegativeControl()
        {
            bool was = CarryMove.NcOpen;
            bool red = false;
            try
            {
                CarryMove.NcOpen = true;
                try { AssertOverweightMove(); }
                catch (InvalidOperationException) { red = true; }
            }
            finally { CarryMove.NcOpen = was; }
            if (!red)
                throw new InvalidOperationException("과적 이동 네거티브 컨트롤 실패 — NcOpen 인데 통과했습니다.");
            Debug.Log("[Ulon] 과적 이동 네거티브 컨트롤 통과 — NcOpen 이면 FAIL");
        }
    }
}
