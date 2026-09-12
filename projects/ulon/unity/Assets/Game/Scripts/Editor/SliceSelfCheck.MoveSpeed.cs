using System;
using System.IO;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// 기획 §7.2.1. 걷기 2.5 / 달리기 5.0 m/s. NC: NcOpen 이면 옛 4.2 로 돌아가 빨간불.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        static void AssertMoveSpeed()
        {
            if (MoveSpeed.NcOpen)
                throw new InvalidOperationException("MoveSpeed.NcOpen 가 켜져 있으면 옛 4.2 m/s 입니다.");

            string path = DataLedger.PathOf(MoveSpeed.FileName);
            if (!File.Exists(path))
                throw new InvalidOperationException("이동 리듬 원장이 없습니다: " + path);

            MoveSpeed.Reload();
            if (Math.Abs(MoveSpeed.FileWalkMetersPerSecond - 2.5f) > 0.01f)
                throw new InvalidOperationException(
                    "걷기 원장이 2.5 m/s가 아닙니다: " + MoveSpeed.FileWalkMetersPerSecond);
            if (Math.Abs(MoveSpeed.FileRunMetersPerSecond - 5.0f) > 0.01f)
                throw new InvalidOperationException(
                    "달리기 원장이 5.0 m/s가 아닙니다: " + MoveSpeed.FileRunMetersPerSecond);
            if (Math.Abs(MoveSpeed.WalkMetersPerSecond - 2.5f) > 0.01f)
                throw new InvalidOperationException("걷기가 원장을 안 탑니다: " + MoveSpeed.WalkMetersPerSecond);
            if (Math.Abs(MoveSpeed.RunMetersPerSecond - 5.0f) > 0.01f)
                throw new InvalidOperationException("달리기가 원장을 안 탑니다: " + MoveSpeed.RunMetersPerSecond);
            if (Math.Abs(MoveSpeed.MetersPerSecond(false) - 2.5f) > 0.01f)
                throw new InvalidOperationException("MetersPerSecond(false)가 걷기가 아닙니다.");
            if (Math.Abs(MoveSpeed.MetersPerSecond(true) - 5.0f) > 0.01f)
                throw new InvalidOperationException("MetersPerSecond(true)가 달리기가 아닙니다.");

            string motorPath = Path.Combine(Application.dataPath, "Game/Scripts/Client/ClickMotor.cs");
            string motor = File.ReadAllText(motorPath);
            if (motor.IndexOf("MoveSpeed.MetersPerSecond", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("ClickMotor가 MoveSpeed 원장을 안 탑니다.");
            if (motor.IndexOf("4.2f", StringComparison.Ordinal) >= 0 ||
                motor.IndexOf("speed = 4.2", StringComparison.Ordinal) >= 0)
                throw new InvalidOperationException("ClickMotor에 옛 4.2 m/s가 남아 있습니다.");
            if (motor.IndexOf("RpcRequestMove", StringComparison.Ordinal) < 0 ||
                motor.IndexOf("Running", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("ClickMotor가 달리기 플래그를 서버에 안 보냅니다.");

            string movePath = Path.Combine(Application.dataPath, "Game/Scripts/Client/NetAvatar.Move.cs");
            string move = File.ReadAllText(movePath);
            if (move.IndexOf("RpcRequestMove(Vector3 dest, bool stop, bool running)", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("이동 RPC에 달리기 인자가 없습니다.");
            if (move.IndexOf("ApplyServerRunning", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("서버 모터가 달리기를 안 받습니다.");

            string avatarPath = Path.Combine(Application.dataPath, "Game/Scripts/Client/LocalAvatar.cs");
            string avatar = File.ReadAllText(avatarPath);
            if (avatar.IndexOf("SetRunning", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("소유 클라가 달리기 입력을 모터에 안 넣습니다.");

            Debug.Log("[Ulon] 이동 리듬 — 걷기 " + MoveSpeed.WalkMetersPerSecond.ToString("0.0") +
                      " / 달리기 " + MoveSpeed.RunMetersPerSecond.ToString("0.0") +
                      " m/s · 출처 " + MoveSpeed.Source);
        }

        static void AssertMoveSpeedNegativeControl()
        {
            bool was = MoveSpeed.NcOpen;
            bool red = false;
            try
            {
                MoveSpeed.NcOpen = true;
                try { AssertMoveSpeed(); }
                catch (InvalidOperationException) { red = true; }
            }
            finally { MoveSpeed.NcOpen = was; }
            if (!red)
                throw new InvalidOperationException("이동 리듬 네거티브 컨트롤 실패 — NcOpen 인데 통과했습니다.");
            Debug.Log("[Ulon] 이동 리듬 네거티브 컨트롤 통과 — NcOpen 이면 FAIL");
        }
    }
}
