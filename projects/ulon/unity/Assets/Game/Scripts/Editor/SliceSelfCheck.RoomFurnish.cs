using System;
using System.Collections.Generic;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// §6.1 던전 콘텐츠 — 방을 넓혔으면 **채움도 따라와야 한다**(검수 2026-09-06 관찰:
        /// 반경 6→8m로 넓힌 뒤 `23_d1_corner_playcam`이 어두운 빈 바닥으로 화면 대부분을 채웠다).
        ///
        /// 판정은 소품 **개수**가 아니라 화면이다: 플레이 카메라에서 **맨바닥만 보이는 비율**의 상한.
        /// 개수를 세면 구석에 몰아넣어도 통과한다(대리 지표).
        /// </summary>
        // 실측(2026-09-06): 채운 상태 던전 1·2·3 = 0.29 / 0.30 / 0.27, 소품을 치운 결함 상태 = 0.65.
        // 0.45면 채운 상태는 넉넉히 통과하고 빈 방은 잡힌다.
        const float BareFloorShareMax = 0.45f;
        const int FurnishRaysPerAxis = 21;

        static void AssertRoomFurnished()
        {
            var cam = UnityEngine.Object.FindFirstObjectByType<Ulon.Client.QuarterViewCamera>(FindObjectsInactive.Include);
            if (cam == null)
                throw new InvalidOperationException("씬에 QuarterViewCamera가 없습니다 — 실내 채움을 화면으로 잴 수 없습니다.");

            CheckFurnish("던전 1", new Vector2(Dungeon1.InteriorX, Dungeon1.InteriorZ), Dungeon1.InteriorObject, cam);
            CheckFurnish("던전 2", new Vector2(Dungeon2.InteriorX, Dungeon2.InteriorZ), Dungeon2.InteriorObject, cam);
            CheckFurnish("던전 3", new Vector2(Dungeon3.InteriorX, Dungeon3.InteriorZ), Dungeon3.InteriorObject, cam);
            Debug.Log("[Ulon] 던전 실내 채움 통과 — 맨바닥 비율 상한 " + BareFloorShareMax);
        }

        static float BareFloorShare(Vector2 center, string interiorObject, Ulon.Client.QuarterViewCamera cam)
        {
            var interior = GameObject.Find(interiorObject);
            if (interior == null)
                throw new InvalidOperationException(interiorObject + "이(가) 없습니다.");
            float groundY = GroundYAt(center) - VisualSliceBuilder.DungeonDepth;
            var player = new Vector3(center.x, groundY + 1.0f, center.y);
            var rot = Quaternion.Euler(cam.Pitch, cam.Yaw, 0f);
            float dist = Mathf.Min(cam.Distance, cam.IndoorDistance);
            var eye = player - rot * Vector3.forward * dist;
            float halfV = 55f * 0.5f;
            float halfH = Mathf.Rad2Deg * Mathf.Atan(Mathf.Tan(halfV * Mathf.Deg2Rad) * 16f / 9f);

            var hidden = new List<Renderer>();
            Ulon.Client.DungeonSightFade.Hide(eye, player, Ulon.Client.DungeonSightFade.DefaultRadius, hidden);
            int total = 0, bare = 0;
            try
            {
                for (int iy = 0; iy < FurnishRaysPerAxis; iy++)
                {
                    float ty = (iy / (float)(FurnishRaysPerAxis - 1)) * 2f - 1f;
                    for (int ix = 0; ix < FurnishRaysPerAxis; ix++)
                    {
                        float tx = (ix / (float)(FurnishRaysPerAxis - 1)) * 2f - 1f;
                        var dir = rot * Quaternion.Euler(ty * halfV, tx * halfH, 0f) * Vector3.forward;
                        total++;
                        var hits = Physics.RaycastAll(eye, dir, 80f, ~0, QueryTriggerInteraction.Ignore);
                        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                        for (int h = 0; h < hits.Length; h++)
                        {
                            var col = hits[h].collider;
                            if (col == null)
                                continue;
                            var rend = col.GetComponent<Renderer>();
                            if (rend != null && !rend.enabled)
                                continue;                       // 페이드로 꺼진 것은 화면에 없다
                            if (col.name.StartsWith("DungeonFloor", StringComparison.Ordinal))
                                bare++;
                            break;                              // 첫 번째로 실제 보이는 것만 센다
                        }
                    }
                }
            }
            finally
            {
                Ulon.Client.DungeonSightFade.Restore(hidden);
            }
            return bare / (float)total;
        }

        static void CheckFurnish(string label, Vector2 center, string interiorObject, Ulon.Client.QuarterViewCamera cam)
        {
            float share = BareFloorShare(center, interiorObject, cam);
            Debug.Log("[Ulon] 실내 채움 계측 " + label + " 맨바닥 " + share.ToString("0.00") + " (상한 " + BareFloorShareMax + ")");
            if (share > BareFloorShareMax)
                throw new InvalidOperationException(label + " 화면의 " + (share * 100f).ToString("0") +
                    "%가 맨바닥입니다 — 상한 " + (BareFloorShareMax * 100f).ToString("0") +
                    "%. 방을 넓혔으면 기둥·궤짝·잔해로 채워야 합니다(§6.1 던전 콘텐츠).");
        }

        /// <summary>네거티브 컨트롤 — 채움 소품을 실제로 치우면 맨바닥 비율이 상한을 넘어야 한다.</summary>
        static void AssertRoomFurnishedNegativeControl()
        {
            var cam = UnityEngine.Object.FindFirstObjectByType<Ulon.Client.QuarterViewCamera>(FindObjectsInactive.Include);
            var interior = GameObject.Find(Dungeon1.InteriorObject);
            if (cam == null || interior == null)
                throw new InvalidOperationException("던전 1 실내 채움 네거티브 컨트롤을 할 수 없습니다.");

            var furn = new List<Transform>();
            for (int c = 0; c < interior.transform.childCount; c++)
                if (interior.transform.GetChild(c).name.StartsWith("DungeonFurn", StringComparison.Ordinal))
                    furn.Add(interior.transform.GetChild(c));
            if (furn.Count == 0)
                throw new InvalidOperationException("던전 1에 채움 소품(DungeonFurn*)이 없습니다.");

            var saved = new Vector3[furn.Count];
            for (int i = 0; i < furn.Count; i++)
                saved[i] = furn[i].position;
            float share;
            try
            {
                for (int i = 0; i < furn.Count; i++)
                    furn[i].position = saved[i] + Vector3.up * 60f;   // 소품을 실제로 치운다
                Physics.SyncTransforms();
                share = BareFloorShare(new Vector2(Dungeon1.InteriorX, Dungeon1.InteriorZ), Dungeon1.InteriorObject, cam);
            }
            finally
            {
                for (int i = 0; i < furn.Count; i++)
                    furn[i].position = saved[i];
                Physics.SyncTransforms();
            }
            if (share <= BareFloorShareMax)
                throw new InvalidOperationException("실내 채움 네거티브 컨트롤 실패 — 소품을 치웠는데도 맨바닥이 " +
                    share.ToString("0.00") + "입니다(상한 " + BareFloorShareMax + ").");
            Debug.Log("[Ulon] 실내 채움 네거티브 컨트롤 통과 — 소품 " + furn.Count + "개 제거 시 맨바닥 " + share.ToString("0.00") + " > 상한 " + BareFloorShareMax);
        }
    }
}
