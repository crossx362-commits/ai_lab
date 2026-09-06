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

            float groundY = GroundYAt(center);
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

            Debug.Log("[Ulon] 플레이 카메라 시야 통과 — pitch " + cam.Pitch + "·yaw " + cam.Yaw + "에서 방 안 플레이어를 가리는 것은 전부 " + Ulon.Client.DungeonSightFade.BlockerLayer + " 레이어(런타임 렌더 오프)");
        }

        static void CheckSight(string label, Vector2 center, Ulon.Client.QuarterViewCamera cam, int blockerLayer)
        {
            float groundY = GroundYAt(center);
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

        static float GroundYAt(Vector2 flat)
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null)
                return 0f;
            return terrain.SampleHeight(new Vector3(flat.x, 0f, flat.y)) + terrain.transform.position.y;
        }
    }
}
