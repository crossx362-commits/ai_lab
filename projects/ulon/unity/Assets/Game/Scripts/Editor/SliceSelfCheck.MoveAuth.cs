using System;
using System.IO;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        static void AssertMoveAuthority()
        {
            string prefab = File.ReadAllText(
                Path.Combine(Application.dataPath, "Game/Prefabs/NetPlayer.prefab"));
            if (prefab.IndexOf("_clientAuthoritative: 1", StringComparison.Ordinal) >= 0)
                throw new InvalidOperationException(
                    "NetPlayer NetworkTransform이 클라 권위입니다 — 위치가 클라에서 결정됩니다.");
            if (prefab.IndexOf("_clientAuthoritative: 0", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException(
                    "NetPlayer에 서버 권위 NetworkTransform이 없습니다.");

            string movePath = Path.Combine(Application.dataPath, "Game/Scripts/Client/NetAvatar.Move.cs");
            if (!File.Exists(movePath))
                throw new InvalidOperationException("NetAvatar.Move.cs가 없습니다.");
            string move = File.ReadAllText(movePath);
            if (move.IndexOf("RpcRequestMove", StringComparison.Ordinal) < 0 ||
                move.IndexOf("MoveAuthority.Accept", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("이동 RPC가 서버 검증을 안 탑니다.");

            string motorPath = Path.Combine(Application.dataPath, "Game/Scripts/Client/ClickMotor.cs");
            string motor = File.ReadAllText(motorPath);
            if (motor.IndexOf("RpcRequestMove", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("ClickMotor가 목적지를 서버에 안 보냅니다.");

            string avatarPath = Path.Combine(Application.dataPath, "Game/Scripts/Client/NetAvatar.cs");
            string avatar = File.ReadAllText(avatarPath);
            if (avatar.IndexOf("motor.enabled = IsServerInitialized", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException(
                    "원격 클라가 로컬 모터로 위치를 밀 수 있습니다 — 서버만 걷게 해야 합니다.");

            Vector3 plaza = Vector3.zero;
            if (!MoveAuthority.Accept(plaza))
                throw new InvalidOperationException("광장 목적지가 거절됩니다.");
            Vector3 dungeon = new Vector3(0f, WorldTerrain.LandBase - WorldTerrain.DungeonDepth, 0f);
            if (!MoveAuthority.Accept(dungeon))
                throw new InvalidOperationException("던전 높이 목적지가 거절됩니다.");
            Vector3 outside = new Vector3(WorldTerrain.Half + 50f, WorldTerrain.LandBase, 0f);
            if (MoveAuthority.Accept(outside))
                throw new InvalidOperationException("섬 밖 목적지가 통과합니다.");
            Vector3 nan = new Vector3(float.NaN, 0f, 0f);
            if (MoveAuthority.Accept(nan))
                throw new InvalidOperationException("NaN 목적지가 통과합니다.");

            Debug.Log("[Ulon] 이동 서버 권위 — NT 서버 · RpcRequestMove · 섬 밖 거절");
        }

        static void AssertMoveAuthorityNegativeControl()
        {
            bool was = MoveAuthority.NcOpen;
            bool red = false;
            try
            {
                MoveAuthority.NcOpen = true;
                try { AssertMoveAuthority(); }
                catch (InvalidOperationException) { red = true; }
            }
            finally { MoveAuthority.NcOpen = was; }
            if (!red)
                throw new InvalidOperationException("이동 권위 네거티브 컨트롤 실패 — NcOpen 인데 통과했습니다.");
            Debug.Log("[Ulon] 이동 권위 네거티브 컨트롤 통과 — NcOpen 이면 FAIL");
        }
    }
}
