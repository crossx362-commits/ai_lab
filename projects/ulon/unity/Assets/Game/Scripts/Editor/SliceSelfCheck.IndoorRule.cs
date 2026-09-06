using System;
using Ulon.Client;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **「실내」의 정의를 양쪽으로 검사한다**(검수 조건 2026-09-07).
    /// 시야 페이드 레이어가 마을·숲까지 넓어지면서 「머리 위에 차폐 레이어가 있으면 실내」는
    /// **나무 밑에 서기만 해도 실내**가 된다(카메라가 야외에서 실내 줌으로 당겨진다).
    /// 그래서 정의를 「머리 위 차폐 **그리고** 지표보다 아래」로 바꿨고, 이 게이트가 두 방향을 다 잰다:
    /// ① 나무·건물 밑 야외에서 실내로 안 넘어가는가 ② 방 안에서는 여전히 실내인가.
    /// 한쪽만 재면 「전부 야외」로 만들어도 통과한다.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        static void AssertIndoorRule()
        {
            // ① 야외 — 머리 위에 차폐물(나무·건물)이 있는 자리를 **실제로 찾아서** 잰다.
            int layer = LayerMask.NameToLayer(DungeonSightFade.BlockerLayer);
            if (layer < 0)
                throw new InvalidOperationException("레이어 " + DungeonSightFade.BlockerLayer + "가 없습니다.");
            Vector3 underCover = Vector3.zero;
            string coverName = "";
            for (float x = -20f; x <= 20f && coverName == ""; x += 1f)
                for (float z = -20f; z <= 20f; z += 1f)
                {
                    var p = new Vector3(x, GroundYAt(new Vector2(x, z)) + 1.0f, z);
                    if (!Physics.Raycast(p + Vector3.up * 0.2f, Vector3.up, out RaycastHit hit, 30f, 1 << layer,
                                         QueryTriggerInteraction.Ignore))
                        continue;
                    underCover = p;
                    coverName = hit.collider != null ? hit.collider.transform.root.name : "?";
                    break;
                }
            if (coverName == "")
                throw new InvalidOperationException("마을에서 머리 위가 막힌 야외 자리를 못 찾았습니다 — " +
                    "잰 것이 없습니다(0이면 실패). 이 게이트가 검사하려는 상황 자체가 성립하지 않습니다.");
            bool outdoorSaysIndoor = QuarterViewCamera.IsIndoor(underCover);
            Debug.Log("[Ulon] 실내 판정 ① 야외 — " + coverName + " 밑 (" + underCover.x.ToString("0") + "," +
                      underCover.z.ToString("0") + ")에서 IsIndoor=" + outdoorSaysIndoor + "(false여야 함)");
            if (outdoorSaysIndoor)
                throw new InvalidOperationException(coverName + " 밑에 섰는데 실내로 읽힙니다 — 야외에서 카메라가 실내 줌으로 당겨집니다.");

            // ② 방 안 — 여전히 실내여야 한다.
            float roomY = GroundYAt(new Vector2(Dungeon1.InteriorX, Dungeon1.InteriorZ))
                          - VisualSliceBuilder.DungeonDepth + VisualSliceBuilder.RoomFloorTop + 1.0f;
            var inRoom = new Vector3(Dungeon1.InteriorX, roomY, Dungeon1.InteriorZ);
            bool roomSaysIndoor = QuarterViewCamera.IsIndoor(inRoom);
            Debug.Log("[Ulon] 실내 판정 ② 방 안 — 던전 1 방에서 IsIndoor=" + roomSaysIndoor + "(true여야 함)");
            if (!roomSaysIndoor)
                throw new InvalidOperationException("던전 1 방 안인데 실내로 안 읽힙니다 — 실내 줌이 안 걸려 화면이 지표 위로 나갑니다.");

            // ③ 입구(지표 높이, 뚜껑 없음) — 여기서 실내로 읽히면 접근할 때마다 줌이 튄다.
            var atEntrance = new Vector3(Dungeon2.EntranceX, GroundYAt(new Vector2(Dungeon2.EntranceX, Dungeon2.EntranceZ)) + 1.0f,
                                         Dungeon2.EntranceZ);
            bool entranceIndoor = QuarterViewCamera.IsIndoor(atEntrance);
            Debug.Log("[Ulon] 실내 판정 ③ 던전 2 입구(지표) — IsIndoor=" + entranceIndoor + "(false여야 함, 줌이 튀면 안 된다)");
            if (entranceIndoor)
                throw new InvalidOperationException("던전 입구 앞이 실내로 읽힙니다 — 다가갈 때마다 카메라 줌이 튑니다.");
        }
    }
}
