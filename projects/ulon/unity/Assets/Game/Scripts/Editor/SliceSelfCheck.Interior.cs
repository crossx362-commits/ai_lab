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
        // 방 반경은 **원장(RoomHalf)에서 유도한다** — 상수로 박으면 방을 넓힌 순간 모서리 벽·기둥이
        // 판정 반경 밖으로 나가 「벽이 6개」로 오판한다(검은 허공 랩에서 실제로 그랬다). 대각선 + 여유.
        static float InteriorRoomRadius =>
            Mathf.Max(Dungeon1.RoomHalf, Mathf.Max(Dungeon2.RoomHalf, Dungeon3.RoomHalf)) * Mathf.Sqrt(2f) + 1.5f;
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

            // 방 바닥이 수면 아래면 던전이 물에 잠긴다(지형 작업 후 실제로 잠겼다).
            Debug.Log("[Ulon] 방 바닥 높이 " + label + " " + groundY.ToString("0.00") + "m (수면 " + WorldTerrain.SeaLevel + "m + 여유 " + (groundY - WorldTerrain.SeaLevel - 0.5f).ToString("0.00") + "m, 깊이 " + WorldTerrain.DungeonDepth + "m·LandBase " + WorldTerrain.LandBase + ")");
            if (groundY < WorldTerrain.SeaLevel + 0.5f)
                throw new InvalidOperationException(label + " 방 바닥이 " + groundY.ToString("0.0") + "m로 수면(" + WorldTerrain.SeaLevel + "m) 아래입니다 — 던전이 물에 잠깁니다. 평지 높이(WorldTerrain.LandBase)가 던전 깊이 " + VisualSliceBuilder.DungeonDepth + "m보다 충분히 높아야 합니다.");

            // 발이 바닥에 닿는가 — 지형·깊이를 만지면 떠 있거나 파묻힌다(§8.1 화면 가독성).
            CheckFeetOnFloor(label + " 잡몹", MobObjectAt(label, false), groundY);
            CheckFeetOnFloor(label + " 보스", MobObjectAt(label, true), groundY);

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

            CheckScreenFill("던전 1", new Vector2(Dungeon1.InteriorX, Dungeon1.InteriorZ), cam, blocker, Dungeon1.InteriorObject, Dungeon1.MobObject, Dungeon1.BossObject);
            CheckScreenFill("던전 2", new Vector2(Dungeon2.InteriorX, Dungeon2.InteriorZ), cam, blocker, Dungeon2.InteriorObject, Dungeon2.MobObject, Dungeon2.BossObject);
            CheckScreenFill("던전 3", new Vector2(Dungeon3.InteriorX, Dungeon3.InteriorZ), cam, blocker, Dungeon3.InteriorObject, Dungeon3.MobObject, Dungeon3.BossObject);
            AssertVoidNegativeControl();

            Debug.Log("[Ulon] 플레이 카메라 시야 통과 — pitch " + cam.Pitch + "·yaw " + cam.Yaw + "에서 방 안 플레이어를 가리는 것은 전부 " + Ulon.Client.DungeonSightFade.BlockerLayer + " 레이어(런타임 렌더 오프)");
        }

        static void CheckSight(string label, Vector2 center, Ulon.Client.QuarterViewCamera cam, int blockerLayer)
        {
            float groundY = GroundYAt(center) - VisualSliceBuilder.DungeonDepth;   // 방 바닥(지하화)
            var player = new Vector3(center.x, groundY + 1.0f, center.y);
            float[] distances = { Mathf.Min(cam.Distance, cam.IndoorDistance), cam.MinDistance };
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
        // 검수 2026-09-06: 「막을 것을 세지 말고 보여야 할 것을 세라」 — 잔디를 막았더니 회색 뚜껑이 화면을 덮었다.
        // 그래서 **실내(바닥·벽·방 안 몹)가 화면에서 차지하는 비율의 하한**을 건다.
        // 실측(2026-09-06): 실내 줌 5.5m에서 0.98~1.00, 실외 거리 18m(결함 상태)에서 0.67~0.78.
        // 0.90이면 고친 상태는 통과하고 결함은 잡힌다 — 0.55로 뒀더니 결함이 그대로 통과했다.
        const float InteriorShareMin = 0.90f;
        /// <summary>검수 2026-09-06 요구 — 이 거리까지 줌 아웃해도 실내가 유지돼야 한다(전투 시야).</summary>
        const float IndoorDistanceRequired = 8.0f;
        /// <summary>
        /// 화면에서 허용하는 「아무것도 안 맞는 화면」 비율(벽 바깥 허공).
        /// 실측(2026-09-06): 고친 상태 던전 1·2·3 전부 0.00 / 결함 상태(문 밖 통로를 치움) 0.02.
        /// 상한을 0.02로 두면 결함이 경계에 걸터앉는다 — 0.005면 고친 상태는 통과하고 결함은 4배로 넘긴다.
        /// </summary>
        const float VoidShareMax = 0.005f;
        const int FillRaysPerAxis = 21;


        /// <summary>같은 화각·같은 페이드로 한 거리에서의 잔디/실내 비율을 잰다(거리 실측 스윕과 본판정이 같은 자를 쓰게).</summary>
        static string s_voidSample = "";

        /// <summary>
        /// 허공 게이트의 네거티브 컨트롤 — 문 밖 통로의 **막음벽을 실제로 치워** 빨간불을 확인한다.
        /// 빌더 한 줄을 주석 처리하는 방식은 씬에 이미 벽이 있어 아무것도 증명하지 못한다(검수 지적).
        /// </summary>
        static void AssertVoidNegativeControl()
        {
            var cam = UnityEngine.Object.FindFirstObjectByType<Ulon.Client.QuarterViewCamera>(FindObjectsInactive.Include);
            if (cam == null)
                throw new InvalidOperationException("씬에 QuarterViewCamera가 없습니다 — 허공 네거티브 컨트롤을 할 수 없습니다.");
            int blocker = LayerMask.NameToLayer(Ulon.Client.DungeonSightFade.BlockerLayer);
            var interior = GameObject.Find(Dungeon2.InteriorObject);
            if (interior == null)
                throw new InvalidOperationException("던전 2 내부 오브젝트가 없습니다 — 허공 네거티브 컨트롤을 할 수 없습니다.");
            var corridor = new System.Collections.Generic.List<Transform>();
            var all = interior.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i].name.StartsWith("DungeonWallDoor", StringComparison.Ordinal) || all[i].name == "DungeonCeilDoor"
                    || all[i].name.StartsWith("DungeonRockFill", StringComparison.Ordinal))
                    corridor.Add(all[i]);
            if (corridor.Count < 8)
                throw new InvalidOperationException("던전 2 방 바깥 차폐물이 " + corridor.Count + "개입니다 — 문 밖 통로 4 + 바깥 암반 링 4가 있어야 벽 밖으로 허공이 안 보입니다.");

            // 바닥도 결함 시절 크기(방 span + 8m)로 되돌린다 — 지금은 넓은 바닥이 문틈 시선을 받아내고 있다.
            Transform floor = null;
            for (int i = 0; i < all.Length; i++)
                if (all[i].name == "DungeonFloor") { floor = all[i]; break; }
            if (floor == null)
                throw new InvalidOperationException("던전 2 방 바닥(DungeonFloor)이 없습니다.");
            var floorScale = floor.localScale;
            float oldSpan = Dungeon2.RoomHalf * 2f + 8f;

            var center = new Vector2(Dungeon2.InteriorX, Dungeon2.InteriorZ);
            var saved = new Vector3[corridor.Count];
            for (int i = 0; i < corridor.Count; i++)
                saved[i] = corridor[i].position;
            float voidShare;
            try
            {
                for (int i = 0; i < corridor.Count; i++)
                    corridor[i].position = saved[i] + Vector3.up * 60f;   // 통로·바깥 암반을 통째로 치운다(결함 상태 재현)
                floor.localScale = new Vector3(oldSpan, floorScale.y, oldSpan);
                Physics.SyncTransforms();
                MeasureFill(center, cam, Mathf.Min(cam.Distance, cam.IndoorDistance), blocker, interior,
                    GameObject.Find(Dungeon2.MobObject), GameObject.Find(Dungeon2.BossObject),
                    out float _, out float _, out voidShare);
            }
            finally
            {
                for (int i = 0; i < corridor.Count; i++)
                    corridor[i].position = saved[i];
                floor.localScale = floorScale;
                Physics.SyncTransforms();
            }
            if (voidShare <= VoidShareMax)
                throw new InvalidOperationException("허공 게이트 네거티브 컨트롤 실패 — 방 바깥 차폐(통로·암반 링·넓은 바닥)를 되돌렸는데도 허공이 " +
                    voidShare.ToString("0.00") + "입니다(상한 " + VoidShareMax + "). 게이트가 결함을 못 잡습니다.");
            Debug.Log("[Ulon] 허공 게이트 네거티브 컨트롤 통과 — 방 바깥 차폐 제거 시 허공 " + voidShare.ToString("0.00") + " > 상한 " + VoidShareMax);
        }

        static void MeasureFill(Vector2 center, Ulon.Client.QuarterViewCamera cam, float dist, int blockerLayer,
            GameObject interior, GameObject mobGo, GameObject bossGo, out float grass, out float interiorShare, out float voidShare)
        {
            float groundY = GroundYAt(center) - VisualSliceBuilder.DungeonDepth;
            var player = new Vector3(center.x, groundY + 1.0f, center.y);
            var rot = Quaternion.Euler(cam.Pitch, cam.Yaw, 0f);
            var eye = player - rot * Vector3.forward * dist;
            float halfV = 55f * 0.5f;
            float halfH = Mathf.Rad2Deg * Mathf.Atan(Mathf.Tan(halfV * Mathf.Deg2Rad) * 16f / 9f);
            var hidden = new System.Collections.Generic.List<Renderer>();
            Ulon.Client.DungeonSightFade.Hide(eye, player, Ulon.Client.DungeonSightFade.DefaultRadius, hidden);
            int total = 0, outdoor = 0, skyCount = 0, inside = 0;
            // 허공이 **화면 어디로** 새는지 남긴다 — 「어딘가 검다」만으론 방을 넓힐지 문틈을 막을지 못 고른다.
            float vxMin = 1f, vxMax = -1f, vyMin = 1f, vyMax = -1f;
            var vDirSum = Vector3.zero;
            try
            {
                for (int iy = 0; iy < FillRaysPerAxis; iy++)
                {
                    float ty = (iy / (float)(FillRaysPerAxis - 1)) * 2f - 1f;
                    for (int ix = 0; ix < FillRaysPerAxis; ix++)
                    {
                        float tx = (ix / (float)(FillRaysPerAxis - 1)) * 2f - 1f;
                        var dir = rot * Quaternion.Euler(ty * halfV, tx * halfH, 0f) * Vector3.forward;
                        total++;
                        if (IsOutdoorPixel(eye, dir, out bool sky, out Collider firstHit))
                        {
                            outdoor++;
                            if (sky)
                            {
                                skyCount++;
                                vxMin = Mathf.Min(vxMin, tx); vxMax = Mathf.Max(vxMax, tx);
                                vyMin = Mathf.Min(vyMin, ty); vyMax = Mathf.Max(vyMax, ty);
                                vDirSum += dir;
                            }
                        }
                        if (IsInteriorHit(firstHit, interior, mobGo, bossGo))
                            inside++;
                    }
                }
            }
            finally
            {
                Ulon.Client.DungeonSightFade.Restore(hidden);
            }
            grass = (outdoor - skyCount) / (float)total;
            interiorShare = inside / (float)total;
            voidShare = skyCount / (float)total;   // 아무것도 안 맞은 화면 = 벽 바깥 허공(검은 삼각형)
            s_voidSample = skyCount == 0 ? "" :
                " 허공 위치 화면 x[" + vxMin.ToString("0.00") + "," + vxMax.ToString("0.00") + "] y[" +
                vyMin.ToString("0.00") + "," + vyMax.ToString("0.00") + "] 평균방향 " + vDirSum.normalized.ToString("0.00");
        }

        static void CheckScreenFill(string label, Vector2 center, Ulon.Client.QuarterViewCamera cam, int blockerLayer,
            string interiorObject, string mobObject, string bossObject)
        {
            var interior = GameObject.Find(interiorObject);
            if (interior == null)
                throw new InvalidOperationException(label + " 내부 오브젝트가 없습니다: " + interiorObject);
            var mobGo = GameObject.Find(mobObject);
            var bossGo = GameObject.Find(bossObject);
            float groundY = GroundYAt(center) - VisualSliceBuilder.DungeonDepth;
            var player = new Vector3(center.x, groundY + 1.0f, center.y);
            var rot = Quaternion.Euler(cam.Pitch, cam.Yaw, 0f);
            // 검수 2026-09-06 요구 — 「실내 줌 8.0m에서 실내 비율 0.90↑」. 줌 상한은 방 깊이에서 유도되므로
            // 이 판정은 사실상 **깊이 하한**을 건다: 깊이 ≥ 눈높이 + 8.0×sin(pitch) + 여유.
            float allowed = WorldTerrain.IndoorDistanceFor(cam.Pitch);
            Debug.Log("[Ulon] 실내 줌 상한 " + label + " " + allowed.ToString("0.00") + "m (요구 " + IndoorDistanceRequired + "m, 방 깊이 " + WorldTerrain.DungeonDepth + "m·피치 " + cam.Pitch.ToString("0") + "°)");
            if (allowed < IndoorDistanceRequired)
                throw new InvalidOperationException(label + " 실내 줌 상한이 " + allowed.ToString("0.00") + "m입니다 — 요구 " + IndoorDistanceRequired +
                    "m. 방 깊이 " + WorldTerrain.DungeonDepth + "m가 모자랍니다(필요 최소 " +
                    (WorldTerrain.PlayerEyeHeight + IndoorDistanceRequired * Mathf.Sin(cam.Pitch * Mathf.Deg2Rad) + WorldTerrain.DungeonEyeMargin).ToString("0.00") +
                    "m). 카메라 숫자가 아니라 **깊이**를 고치세요(§4.2).");

            float useDist = Mathf.Min(cam.Distance, cam.IndoorDistance);

            // 실내 줌의 한계는 방 크기가 아니라 **지표**다: 눈높이가 지면을 넘는 순간 화면이 통째로 잔디가 된다.
            // 실측 절벽 — 던전 3은 6.1m, 던전 1은 6.3m에서 실내 1.00 → 0.00. 그래서 이유를 코드가 못 박는다.
            var eyeProbe = player - rot * Vector3.forward * useDist;
            float surfaceY = GroundYAt(new Vector2(eyeProbe.x, eyeProbe.z));
            if (eyeProbe.y > surfaceY - 0.05f)
                throw new InvalidOperationException(label + " 실내 카메라 눈높이가 지표 위 " + (eyeProbe.y - surfaceY).ToString("0.00") +
                    "m입니다 — 실내 줌 거리(" + useDist.ToString("0.0") + "m)가 방 깊이 " + VisualSliceBuilder.DungeonDepth +
                    "m에 비해 멉니다. 더 넓은 전투 시야가 필요하면 거리가 아니라 **방 깊이**를 늘려야 합니다(§4.2).");

            float grass, interiorShare;
            MeasureFill(center, cam, useDist, blockerLayer, interior, mobGo, bossGo, out grass, out interiorShare, out float voidShare);

            Debug.Log("[Ulon] 화면 채움 계측 " + label + " 잔디 " + grass.ToString("0.00") + " (눈높이 지표 아래 " + (surfaceY - eyeProbe.y).ToString("0.00") + "m)");
            Debug.Log("[Ulon] 실내 비율 계측 " + label + " " + interiorShare.ToString("0.00") + " (거리 " + useDist.ToString("0.0") + "m·허공 " + voidShare.ToString("0.00") + ")" + s_voidSample);
            // 「잔디 비율 상한」은 대리 지표였다 — 벽 바깥 **허공**(아무것도 안 맞는 검은 화면)은 잔디가 아니라서
            // 통과했다(검수 2026-09-06: 8.11m 줌에서 화면 아래 두 모서리가 검다). 허공에 따로 상한을 건다.
            // 버린 시도(2026-09-06): 플레이어를 방 네 귀퉁이로 옮겨 최악 허공/실내 비율을 재는 스윕 게이트.
            // 눈이 방 밖으로 나가는 지점에서 레이 모델이 실제 렌더와 어긋나(실내 0.00인데 화면은 멀쩡) 판정으로 못 쓴다.
            // 귀퉁이 화면 근거는 QA 샷 `23_d1_corner_playcam`으로 남긴다 — 스윕 게이트를 다시 만들지 마라.
            if (voidShare > VoidShareMax)
                throw new InvalidOperationException(label + " 화면의 " + (voidShare * 100f).ToString("0") + "%가 벽 바깥 허공(검은 화면)입니다 — 최대 " +
                    (VoidShareMax * 100f).ToString("0") + "%. 방이 카메라 화각을 못 채웁니다(§8.2). 줌 거리에 맞게 **방을 넓히세요**.");
            if (interiorShare < InteriorShareMin)
                throw new InvalidOperationException(label + " 플레이 카메라 화면에서 던전 실내(바닥·벽·몹)가 " + (interiorShare * 100f).ToString("0") + "%뿐입니다 — 최소 " + (InteriorShareMin * 100f).ToString("0") + "%. 잔디를 막아도 뚜껑 윗면이 화면을 덮으면 실내로 안 읽힙니다(§8.2·§4.2 줌).");
            if (grass > OutdoorFillMax)
                throw new InvalidOperationException(label + " 플레이 카메라 화면의 " + (grass * 100f).ToString("0") + "%가 잔디입니다 — 기획서 §8.2 상한 " + (OutdoorFillMax * 100f).ToString("0") + "%. 방이 지하로 안 내려갔거나 암반 뚜껑이 좁아 던전으로 안 읽힙니다.");
        }

        /// <summary>이 방향의 화면 픽셀이 잔디(Terrain)나 하늘로 보이는가 — 페이드로 꺼지는 렌더러는 없는 셈.</summary>
        /// <summary>이 화면 픽셀이 던전 실내(방 구조물 또는 방 안 몹)인가.</summary>
        static bool IsInteriorHit(Collider col, GameObject interior, GameObject mob, GameObject boss)
        {
            if (col == null)
                return false;
            var t = col.transform;
            if (interior != null && t.IsChildOf(interior.transform))
                return true;
            if (mob != null && (t == mob.transform || t.IsChildOf(mob.transform)))
                return true;
            if (boss != null && (t == boss.transform || t.IsChildOf(boss.transform)))
                return true;
            return false;
        }

        static bool IsOutdoorPixel(Vector3 eye, Vector3 dir, out bool sky, out Collider firstHit)
        {
            firstHit = null;
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
                    // 페이드는 이제 **끄지 않고 비치게** 한다(검수 2026-09-07) — 둘 다 「화면을 안 가림」이다.
                    if (rend == null || !rend.enabled || Ulon.Client.DungeonSightFade.IsGhosted(rend))
                        continue;
                }
                if (hits[i].distance < best)
                {
                    best = hits[i].distance;
                    bestOutdoor = isTerrain;
                    bestSky = false;
                    firstHit = col;
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

        /// <summary>
        /// **해는 하나다.** 방향광은 자리와 무관하게 세계 전체를 비추므로 등불 하나가 방향광이 되면
        /// 마을 조도가 그만큼 곱절이 된다 — 2026-09-09 실측: 해 셋(1.18×3)이 광장 화면의 24~39%를
        /// 순백(255)으로 태웠고, 포화한 픽셀 위에서는 VFX 픽셀 차가 0이라 「효과가 안 읽힘」이 됐다.
        /// 원인은 빌더가 `FindAnyObjectByType&lt;Light&gt;()`로 **아무 등불이나** 집어 해로 만든 것
        /// (「고르는 자는 틀려도 빨간불이 안 난다」). 고르는 자를 고쳤으니, 다시 둘이 되면 여기서 운다.
        /// </summary>
        static Light SingleSun()
        {
            Light sun = null;
            var strays = new System.Collections.Generic.List<string>();
            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].type != LightType.Directional)
                    continue;
                if (lights[i].gameObject.name == VisualSliceBuilder.SunObject && sun == null)
                    sun = lights[i];
                else
                    strays.Add(lights[i].name + "(I=" + lights[i].intensity.ToString("F2") + ")");
            }
            if (strays.Count > 0)
                throw new InvalidOperationException("방향광이 " + (strays.Count + (sun != null ? 1 : 0)) +
                    "개입니다 — 해는 " + VisualSliceBuilder.SunObject + " 하나뿐이어야 합니다. 나머지: " +
                    string.Join(", ", strays) + ". 방향광은 자리와 무관하게 세계 전체를 비춰 지표를 포화시킵니다.");
            return sun;
        }

        static void AssertDungeonLighting()
        {
            AssertDungeon3Leftover();
            Physics.SyncTransforms();

            Light sun = SingleSun();
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

        const float FeetGapMax = 0.25f;

        static string MobObjectAt(string label, bool boss)
        {
            if (label == "던전 1")
                return boss ? Dungeon1.BossObject : Dungeon1.MobObject;
            if (label == "던전 2")
                return boss ? Dungeon2.BossObject : Dungeon2.MobObject;
            return boss ? Dungeon3.BossObject : Dungeon3.MobObject;
        }

        /// <summary>
        /// 방 안 몹의 발과 **바로 아래 표면**의 차이.
        ///
        /// 2026-09-07 정정: 예전엔 `transform.position.y`(원점)를 「발끝」으로, `roomFloorY + 0.2`(공식)를
        /// 「바닥 윗면」으로 삼았다 — **둘 다 대리 지표**다. 보스를 1.3~1.5배로 키우면 원점과 메시 밑면이
        /// 벌어지고, 공식 바닥은 실제 바닥 판 콜라이더와 어긋날 수 있다. 그래서 보수 패스가 실제 표면에
        /// 재착지시켜 **화면상 바닥에 선** 뒤에도 이 게이트만 계속 빨간불이었다(자가 둘이면 한쪽이 틀린다).
        /// 이제 액터 발 게이트와 **같은 자**(메시 밑면 vs 발밑 광선)를 쓴다.
        /// </summary>
        static void CheckFeetOnFloor(string what, string objectName, float roomFloorY)
        {
            var go = GameObject.Find(objectName);
            if (go == null)
                throw new InvalidOperationException(what + " 오브젝트가 없습니다: " + objectName);
            // 발은 **몸**으로 잰다 — 무기를 넣으면 칼끝이 발이 된다(장비-바닥은 `AssertGearAboveFloor`가 따로 본다).
            if (!GroundFit.BodyBounds(go.transform, out Bounds b))
                throw new InvalidOperationException(what + "의 보이는 메시가 없습니다: " + objectName + " — 못 잰 것을 통과로 적지 않는다.");
            if (!GroundFit.SurfaceUnder(go.transform, b, out float sy, out string surface))
                throw new InvalidOperationException(what + "의 발 밑에 바닥이 없습니다(" + objectName + ").");
            float d = b.min.y - sy;
            Debug.Log("[Ulon] 발 높이 계측 " + what + " 발끝 " + b.min.y.ToString("0.00") + " 표면 " + sy.ToString("0.00") +
                      " 차 " + d.ToString("0.00") + " (" + surface + ")");
            if (Mathf.Abs(d) > FeetGapMax)
                throw new InvalidOperationException(what + "의 발이 방 바닥과 " + d.ToString("0.00") + "m 어긋납니다 — 허용 " + FeetGapMax + "m(양수는 공중부양, 음수는 바닥에 파묻힘).");
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
