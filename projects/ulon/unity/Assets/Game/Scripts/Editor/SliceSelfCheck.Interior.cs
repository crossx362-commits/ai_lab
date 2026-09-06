using System;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// 던전 내부가 "실내"인지 화면 기준으로 판정한다(검수 2026-09-06 P0-1).
        /// 옛 던전 Assert는 입장/퇴장 플래그만 봐서 빈 잔디밭도 통과시켰다 — 그게 "완료"가 거짓이 된 원인이다.
        /// 여기서는 벽 수·천장(상방 차단)·바닥·몹 간격을 오브젝트로 강제한다.
        /// </summary>
        const int InteriorWallMin = 8;
        const float InteriorWallHeightMin = 1.5f;
        const float InteriorRoomRadius = 9f;
        const float InteriorMobGapMin = 4f;

        static void AssertDungeonInterior()
        {
            AssertDungeon3Leftover();
            Physics.SyncTransforms();

            CheckOneInterior("던전 1", Dungeon1.InteriorObject,
                new Vector2(Dungeon1.InteriorX, Dungeon1.InteriorZ),
                new Vector2(Dungeon1.MobX, Dungeon1.MobZ),
                new Vector2(Dungeon1.BossX, Dungeon1.BossZ));
            CheckOneInterior("던전 2", Dungeon2.InteriorObject,
                new Vector2(Dungeon2.InteriorX, Dungeon2.InteriorZ),
                new Vector2(Dungeon2.MobX, Dungeon2.MobZ),
                new Vector2(Dungeon2.BossX, Dungeon2.BossZ));
            CheckOneInterior("던전 3", Dungeon3.InteriorObject,
                new Vector2(Dungeon3.InteriorX, Dungeon3.InteriorZ),
                new Vector2(Dungeon3.MobX, Dungeon3.MobZ),
                new Vector2(Dungeon3.BossX, Dungeon3.BossZ));

            Debug.Log("[Ulon] 던전 내부 실내 판정 통과 — 벽 " + InteriorWallMin + "개↑·천장 차단·바닥·몹 간격 " + InteriorMobGapMin + "m↑ (던전 1·2·3)");
        }

        static void CheckOneInterior(string label, string interiorObject, Vector2 center, Vector2 mob, Vector2 boss)
        {
            var interior = GameObject.Find(interiorObject);
            if (interior == null)
                throw new InvalidOperationException(label + " 내부 오브젝트가 없습니다: " + interiorObject);

            var renderers = interior.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                throw new InvalidOperationException(label + " 내부가 비어 있습니다(렌더러 0).");

            float groundY = GroundYAt(center) - VisualSliceBuilder.DungeonDepth;   // 방 바닥(지하화)
            int walls = 0;
            bool hasFloor = false;
            float ceilingY = 0f;
            for (int i = 0; i < renderers.Length; i++)
            {
                var b = renderers[i].bounds;
                var flat = new Vector2(b.center.x, b.center.z);
                float dist = Vector2.Distance(flat, center);
                if (dist > InteriorRoomRadius)
                    continue;
                if (b.size.y >= InteriorWallHeightMin && dist >= 1.5f)
                    walls++;
                if (b.size.y < 0.8f && b.size.x >= 4f && b.size.z >= 4f && b.center.y < groundY + 1f)
                    hasFloor = true;
                if (b.center.y > groundY + 2f && b.size.x >= 4f && b.size.z >= 4f)
                    ceilingY = Mathf.Max(ceilingY, b.center.y);
            }

            if (walls < InteriorWallMin)
                throw new InvalidOperationException(label + " 내부 벽이 " + walls + "개입니다 — 최소 " + InteriorWallMin + "개(높이 " + InteriorWallHeightMin + "m↑, 방 반경 " + InteriorRoomRadius + "m 안). 실내가 아니라 들판입니다.");
            if (!hasFloor)
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < renderers.Length && i < 24; i++)
                {
                    var b = renderers[i].bounds;
                    sb.Append(renderers[i].gameObject.name).Append(" c=").Append(b.center.ToString("0.0")).Append(" s=").Append(b.size.ToString("0.0")).Append(" | ");
                }
                throw new InvalidOperationException(label + " 내부에 바닥 오브젝트가 없습니다(가로·세로 4m↑의 낮은 판). 잔디가 그대로 보입니다. 렌더러: " + sb);
            }

            // 하늘 미노출 — 방 중앙에서 위로 쏜 레이가 이 던전 내부 물체에 막혀야 한다.
            var from = new Vector3(center.x, groundY + 1.2f, center.y);
            bool blocked = false;
            var hits = Physics.RaycastAll(from, Vector3.up, 40f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider != null && hits[i].collider.transform.IsChildOf(interior.transform))
                {
                    blocked = true;
                    break;
                }
            }
            if (!blocked)
                throw new InvalidOperationException(label + " 내부 중앙에서 하늘이 그대로 보입니다 — 천장(또는 상방 차단)이 없습니다. 천장 렌더러 최고점 y=" + ceilingY.ToString("0.00"));

            float gap = Vector2.Distance(mob, boss);
            if (gap < InteriorMobGapMin)
                throw new InvalidOperationException(label + " 잡몹과 보스 간격이 " + gap.ToString("0.00") + "m입니다 — 최소 " + InteriorMobGapMin + "m(45° 시점에서 포개집니다).");
        }


        /// <summary>
        /// **플레이 카메라 기준** 시야 판정(검수 2026-09-06 P0). 기획서 §4.2 고정 3/4 쿼터뷰라
        /// 카메라 피치·요를 실내에서 바꿀 수 없다 — 대신 카메라와 플레이어 사이의 던전 벽·천장을
        /// DungeonBlocker 레이어로 빼고 런타임에 렌더만 끈다(DungeonSightFade).
        /// 검증 카메라가 플레이 카메라와 다르면 증거가 아니다 — 그래서 씬의 QuarterViewCamera 값을 그대로 읽는다.
        /// </summary>
        static void AssertPlayCameraSight()
        {
            AssertDungeon3Leftover();
            Physics.SyncTransforms();

            var cam = UnityEngine.Object.FindFirstObjectByType<Ulon.Client.QuarterViewCamera>(FindObjectsInactive.Include);
            if (cam == null)
                throw new InvalidOperationException("씬에 QuarterViewCamera가 없습니다 — 플레이 카메라 기준 검증을 할 수 없습니다.");
            if (cam.GetComponent<Ulon.Client.DungeonSightFade>() == null)
                throw new InvalidOperationException("플레이 카메라에 DungeonSightFade가 없습니다 — 실내에서 천장이 화면을 막습니다.");

            int blocker = LayerMask.NameToLayer(Ulon.Client.DungeonSightFade.BlockerLayer);
            if (blocker < 0)
                throw new InvalidOperationException("레이어 " + Ulon.Client.DungeonSightFade.BlockerLayer + "가 없습니다(ProjectSettings/TagManager).");

            CheckSight("던전 1", new Vector2(Dungeon1.InteriorX, Dungeon1.InteriorZ), cam, blocker);
            CheckSight("던전 2", new Vector2(Dungeon2.InteriorX, Dungeon2.InteriorZ), cam, blocker);
            CheckSight("던전 3", new Vector2(Dungeon3.InteriorX, Dungeon3.InteriorZ), cam, blocker);

            CheckScreenFill("던전 1", new Vector2(Dungeon1.InteriorX, Dungeon1.InteriorZ), cam, blocker);
            CheckScreenFill("던전 2", new Vector2(Dungeon2.InteriorX, Dungeon2.InteriorZ), cam, blocker);
            CheckScreenFill("던전 3", new Vector2(Dungeon3.InteriorX, Dungeon3.InteriorZ), cam, blocker);

            Debug.Log("[Ulon] 플레이 카메라 시야 통과 — pitch " + cam.Pitch + "·yaw " + cam.Yaw + "에서 방 안 플레이어를 가리는 것은 전부 " + Ulon.Client.DungeonSightFade.BlockerLayer + " 레이어(런타임 렌더 오프)");
        }

        static void CheckSight(string label, Vector2 center, Ulon.Client.QuarterViewCamera cam, int blockerLayer)
        {
            float groundY = GroundYAt(center) - VisualSliceBuilder.DungeonDepth;   // 방 바닥(지하화)
            var player = new Vector3(center.x, groundY + 1.0f, center.y);
            float[] distances = { cam.Distance, cam.MinDistance };
            for (int d = 0; d < distances.Length; d++)
            {
                var rot = Quaternion.Euler(cam.Pitch, cam.Yaw, 0f);
                var eye = player - rot * Vector3.forward * distances[d];
                Vector3 dir = player - eye;
                float dist = dir.magnitude;
                dir /= dist;
                var hits = Physics.RaycastAll(eye, dir, dist, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < hits.Length; i++)
                {
                    var col = hits[i].collider;
                    if (col == null)
                        continue;
                    var rend = col.GetComponent<Renderer>();
                    if (rend == null || !rend.enabled)
                        continue;
                    if (col.gameObject.layer == blockerLayer)
                        continue;   // 런타임에 렌더가 꺼진다
                    if (col.GetComponent<Terrain>() != null)
                        continue;
                    throw new InvalidOperationException(label + " 실내 플레이어가 플레이 카메라(거리 " + distances[d] + ")에서 안 보입니다 — " + col.gameObject.name + "(레이어 " + LayerMask.LayerToName(col.gameObject.layer) + ")가 시야를 막습니다. 페이드 레이어로 빼거나 치워라.");
                }
            }
        }

        /// <summary>
        /// 기획서 §8.2 「단조로운 초록 화면 금지」 — 플레이 카메라 시야에 잔디(Terrain)와 하늘이
        /// 얼마나 들어오는지 샘플 레이 다발로 잰다. 방을 지상에 두면 화면 대부분이 잔디라
        /// "던전"으로 안 읽힌다. 차폐 페이드로 꺼지는 렌더러는 없는 셈 치고(런타임과 동일) 판정한다.
        /// 하늘은 3/4 시점에서 화면 위쪽에 늘 조금 들어오므로 계측만 하고, 게이트는 **잔디 비율**로 건다.
        /// 실측(2026-09-06): 지하화+암반 뚜껑 0.23·0.27·0.36 / 네거티브 컨트롤(뚜껑을 방 크기로 줄여
        /// 주변 잔디가 화면을 채우게 한 상태) 0.60 → FAIL. 0.45면 고친 상태는 통과하고 결함은 잡힌다.
        /// </summary>
        const float OutdoorFillMax = 0.45f;
        const int FillRaysPerAxis = 21;

        static void CheckScreenFill(string label, Vector2 center, Ulon.Client.QuarterViewCamera cam, int blockerLayer)
        {
            float groundY = GroundYAt(center) - VisualSliceBuilder.DungeonDepth;
            var player = new Vector3(center.x, groundY + 1.0f, center.y);
            var rot = Quaternion.Euler(cam.Pitch, cam.Yaw, 0f);
            var eye = player - rot * Vector3.forward * cam.Distance;

            // 55° FOV·16:9 — QaShots의 카메라와 같은 화각으로 격자 샘플을 쏜다.
            float halfV = 55f * 0.5f;
            float halfH = Mathf.Rad2Deg * Mathf.Atan(Mathf.Tan(halfV * Mathf.Deg2Rad) * 16f / 9f);
            // 런타임과 같은 페이드를 실제로 적용한다 — "DungeonBlocker면 안 보인다"고 가정하면
            // 카메라와 플레이어 사이에 없는 바닥·벽까지 투명 취급해 화면이 전부 잔디로 잡힌다(실측 1.00).
            var hidden = new System.Collections.Generic.List<Renderer>();
            Ulon.Client.DungeonSightFade.Hide(eye, player, Ulon.Client.DungeonSightFade.DefaultRadius, hidden);
            int total = 0;
            int outdoor = 0;
            int skyCount = 0;
            try
            {
            for (int iy = 0; iy < FillRaysPerAxis; iy++)
            {
                float ty = FillRaysPerAxis == 1 ? 0f : (iy / (float)(FillRaysPerAxis - 1)) * 2f - 1f;
                for (int ix = 0; ix < FillRaysPerAxis; ix++)
                {
                    float tx = FillRaysPerAxis == 1 ? 0f : (ix / (float)(FillRaysPerAxis - 1)) * 2f - 1f;
                    var dir = rot * Quaternion.Euler(ty * halfV, tx * halfH, 0f) * Vector3.forward;
                    total++;
                    if (IsOutdoorPixel(eye, dir, out bool sky))
                    {
                        outdoor++;
                        if (sky) skyCount++;
                    }
                }
            }

            }
            finally
            {
                Ulon.Client.DungeonSightFade.Restore(hidden);
            }

            float ratio = outdoor / (float)total;
            float grass = (outdoor - skyCount) / (float)total;
            Debug.Log("[Ulon] 화면 채움 계측 " + label + " 잔디 " + grass.ToString("0.00") + " 하늘 " + (skyCount / (float)total).ToString("0.00") + " (잔디+하늘 " + outdoor + "/" + total + ")");
            if (grass > OutdoorFillMax)
                throw new InvalidOperationException(label + " 플레이 카메라 화면의 " + (grass * 100f).ToString("0") + "%가 잔디입니다 — 기획서 §8.2 상한 " + (OutdoorFillMax * 100f).ToString("0") + "%. 방이 지하로 안 내려갔거나 암반 뚜껑이 좁아 던전으로 안 읽힙니다.");
        }

        /// <summary>이 방향의 화면 픽셀이 잔디(Terrain)나 하늘로 보이는가 — 페이드로 꺼지는 렌더러는 없는 셈.</summary>
        static bool IsOutdoorPixel(Vector3 eye, Vector3 dir, out bool sky)
        {
            var hits = Physics.RaycastAll(eye, dir, 120f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool bestOutdoor = true;   // 아무것도 안 맞으면 하늘
            bool bestSky = true;
            for (int i = 0; i < hits.Length; i++)
            {
                var col = hits[i].collider;
                if (col == null)
                    continue;
                bool isTerrain = col.GetComponent<Terrain>() != null;
                if (!isTerrain)
                {
                    var rend = col.GetComponent<Renderer>();
                    if (rend == null || !rend.enabled)
                        continue;   // 페이드로 꺼진 것 포함 — 화면에 안 보인다
                }
                if (hits[i].distance < best)
                {
                    best = hits[i].distance;
                    bestOutdoor = isTerrain;
                    bestSky = false;
                }
            }
            sky = bestSky;
            return bestOutdoor;
        }

        /// <summary>
        /// 실내 조명이 던전 값인지 강제한다(검수 2026-09-06 P0-2). 기획서 §8.2 — 실내가 낮처럼 밝으면
        /// 던전으로 안 읽힌다. 판정은 둘: ① 태양광이 방 바닥에 직접 닿지 못한다(암반 뚜껑 그림자),
        /// ② 방 안 광원은 등불 계열 점광 3개↑(횃불 2 + 받침 1)이고 전부 지면 아래에 있다.
        /// </summary>
        const int RoomPointLightMin = 3;

        static void AssertDungeonLighting()
        {
            AssertDungeon3Leftover();
            Physics.SyncTransforms();

            Light sun = null;
            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].type == LightType.Directional)
                {
                    sun = lights[i];
                    break;
                }
            }
            if (sun == null)
                throw new InvalidOperationException("씬에 주광(Directional Light)이 없습니다 — 실내 조명 판정을 할 수 없습니다.");
            if (sun.shadows == LightShadows.None)
                throw new InvalidOperationException("주광 그림자가 꺼져 있습니다 — 암반 뚜껑이 그림자를 못 만들어 던전 내부가 낮처럼 밝습니다.");

            CheckRoomLighting("던전 1", Dungeon1.InteriorObject, new Vector2(Dungeon1.InteriorX, Dungeon1.InteriorZ), sun);
            CheckRoomLighting("던전 2", Dungeon2.InteriorObject, new Vector2(Dungeon2.InteriorX, Dungeon2.InteriorZ), sun);
            CheckRoomLighting("던전 3", Dungeon3.InteriorObject, new Vector2(Dungeon3.InteriorX, Dungeon3.InteriorZ), sun);

            Debug.Log("[Ulon] 던전 실내 조명 통과 — 주광 차단(암반 뚜껑 그림자)·등불 점광 " + RoomPointLightMin + "개↑ 지하 배치 (던전 1·2·3)");
        }

        static void CheckRoomLighting(string label, string interiorObject, Vector2 center, Light sun)
        {
            var interior = GameObject.Find(interiorObject);
            if (interior == null)
                throw new InvalidOperationException(label + " 내부 오브젝트가 없습니다: " + interiorObject);

            float groundY = GroundYAt(center);
            float floorY = groundY - VisualSliceBuilder.DungeonDepth;

            // ① 바닥에서 태양 쪽으로 쏜 레이가 이 던전 물체에 막혀야 한다.
            var from = new Vector3(center.x, floorY + 1.0f, center.y);
            var toSun = -sun.transform.forward;
            bool blocked = false;
            var hits = Physics.RaycastAll(from, toSun, 60f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider != null && hits[i].collider.transform.IsChildOf(interior.transform))
                {
                    blocked = true;
                    break;
                }
            }
            if (!blocked)
                throw new InvalidOperationException(label + " 방 바닥에 태양광이 직접 들어옵니다 — 암반 뚜껑이 태양 방향을 못 막습니다. 실내가 낮처럼 밝아집니다(§8.2).");

            // ② 등불 계열 점광이 방 안(지하)에 충분히 있는가.
            int points = 0;
            var lights = interior.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].type != LightType.Point)
                    continue;
                var p = lights[i].transform.position;
                if (Vector2.Distance(new Vector2(p.x, p.z), center) > InteriorRoomRadius)
                    continue;
                if (p.y > groundY)
                    continue;   // 지상에 뜬 광원은 실내 조명이 아니다
                points++;
            }
            if (points < RoomPointLightMin)
                throw new InvalidOperationException(label + " 방 안 등불 점광이 " + points + "개입니다 — 최소 " + RoomPointLightMin + "개(지면 아래·방 반경 " + InteriorRoomRadius + "m 안). 주광이 막힌 실내가 캄캄해집니다.");
        }

        static float GroundYAt(Vector2 flat)
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null)
                return 0f;
            return terrain.SampleHeight(new Vector3(flat.x, 0f, flat.y)) + terrain.transform.position.y;
        }
    }
}
