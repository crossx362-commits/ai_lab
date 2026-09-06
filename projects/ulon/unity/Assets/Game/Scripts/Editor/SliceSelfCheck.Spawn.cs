using System;
using Ulon.Server;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **로그인 직후 서 있는 자리**를 잰다. 2026-09-07 HUD 샷에서 새 캐릭터가 y=0(스냅샷 기본값)에 놓여
    /// 마을 지표(y≈10) 아래 10m에서 떨어져 바다로 빠지는 것이 드러났다(P0).
    /// 「저장된 좌표를 그대로 믿는다」가 원인이라 이 게이트는 **좌표가 아니라 지표와의 관계**를 본다.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        /// <summary>발이 지표에서 이만큼 넘게 떨어져 있으면 떨어지거나 묻힌 것이다.</summary>
        const float SpawnGroundErrorMax = 0.35f;

        static void AssertSpawnOnGround()
        {
            var player = GameObject.Find("Player");
            if (player == null)
                throw new InvalidOperationException("Player가 없어 스폰 위치를 잴 수 없습니다.");

            // 새 캐릭터 스냅샷이 주는 좌표 그대로(0,0,0)를 넣어 본다 — 실제 로그인 경로와 같은 입력이다.
            var landed = CharacterBinder.GroundedSpawn(player.transform, Vector3.zero);
            float ground = GroundYAt(new Vector2(0f, 0f));
            float dy = landed.y - ground;
            Debug.Log("[Ulon] 스폰 — 스냅샷 (0,0,0) → 착지 y " + landed.y.ToString("0.00") +
                      ", 지표 " + ground.ToString("0.00") + ", 차 " + dy.ToString("0.00") + "m");
            if (Mathf.Abs(dy) > SpawnGroundErrorMax)
                throw new InvalidOperationException("로그인 직후 스폰이 지표에서 " + dy.ToString("0.00") +
                    "m " + (dy > 0f ? "떠" : "묻혀") + " 있습니다 — 허용 " + SpawnGroundErrorMax +
                    "m. 저장된 좌표를 그대로 믿지 말고 지표에 맞춰라(2026-09-07 P0).");

            // 지하 방 바닥에 서 있는 좌표는 **끌어올리면 안 된다** — 던전에서 로그아웃한 사람이 지표로 튀어나온다.
            float floor = GroundYAt(new Vector2(Dungeon1.InteriorX, Dungeon1.InteriorZ))
                          - VisualSliceBuilder.DungeonDepth + VisualSliceBuilder.RoomFloorTop;
            var inRoom = new Vector3(Dungeon1.InteriorX, floor + 0.1f, Dungeon1.InteriorZ);
            var keep = CharacterBinder.GroundedSpawn(player.transform, inRoom);
            if (Mathf.Abs(keep.y - inRoom.y) > 0.5f)
                throw new InvalidOperationException("던전 방 안 좌표가 " + (keep.y - inRoom.y).ToString("0.00") +
                    "m 옮겨졌습니다 — 방 바닥에 딛고 선 좌표는 그대로 둬야 합니다(지표로 튀어나오면 안 된다).");
            Debug.Log("[Ulon] 스폰 — 던전 방 안 좌표는 그대로 유지(y " + keep.y.ToString("0.00") + ")");
        }

        /// <summary>네거티브 컨트롤 — 땅속 깊이 넣은 좌표는 반드시 지표로 올라와야 한다.</summary>
        static void AssertSpawnOnGroundNegativeControl()
        {
            var player = GameObject.Find("Player");
            if (player == null)
                throw new InvalidOperationException("Player가 없어 스폰 네거티브 컨트롤을 할 수 없습니다.");
            var buried = new Vector3(3f, GroundYAt(new Vector2(3f, 3f)) - 40f, 3f);
            var fixedUp = CharacterBinder.GroundedSpawn(player.transform, buried);
            Debug.Log("[Ulon] 스폰 네거티브 컨트롤 — 지표 40m 아래에 넣은 좌표가 y " +
                      fixedUp.y.ToString("0.00") + "로 올라왔습니다(원래 " + buried.y.ToString("0.00") + ")");
            if (fixedUp.y - buried.y < 30f)
                throw new InvalidOperationException("스폰 네거티브 컨트롤 실패 — 땅속 40m 좌표가 올라오지 않았습니다(" +
                    (fixedUp.y - buried.y).ToString("0.00") + "m). 바로잡는 코드가 작동하지 않습니다.");
        }
    }
}
