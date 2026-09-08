using System;
using System.Collections.Generic;
using System.IO;
using Ulon.Client;
using Ulon.Server;
using Ulon.Shared;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// **VisualSliceBuilder 분할**(오너 상시 지시 2026-09-08, 파일 비대화 정리).
// 이 파일이 담는 것: 시야 페이드·지형 구멍·뚜껑·방 가구·방 크기·던전 방 건설·바닥 위 정렬 — 실내.
// 동작 변경 0 — 구간을 순서 그대로 옮기기만 했다(순서를 바꾸면 주석과 몸통의 짝이 깨진다).
namespace Ulon.Editor
{
    public static partial class VisualSliceBuilder
    {

        public const string DungeonBlockerLayer = "DungeonBlocker";

        /// <summary>던전 방 깊이 — 원장은 `WorldTerrain.DungeonDepth`다(실내 줌 상한이 여기서 유도된다).</summary>
        public const float DungeonDepth = WorldTerrain.DungeonDepth;
        public const string WaterObject = "SeaWater";

        /// <summary>씬의 플레이 카메라에 실내 차폐 페이드를 보장한다(검수 2026-09-06 P0).</summary>
        public static void EnsureCameraSightFade()
        {
            var cams = UnityEngine.Object.FindObjectsByType<QuarterViewCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < cams.Length; i++)
            {
                if (cams[i].GetComponent<DungeonSightFade>() == null)
                    cams[i].gameObject.AddComponent<DungeonSightFade>();
            }
        }

        /// <summary>
        /// 방 자리의 지형에 구멍을 뚫는다. 지하 방을 만들어도 Terrain 표면이 그대로 남아 있으면
        /// 페이드로 뚜껑이 걷힐 때 방이 아니라 **잔디가** 보인다(실측으로 확인, 검수 P0 마무리).
        /// </summary>
        static void PunchTerrainHole(Vector3 center, float half)
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null || terrain.terrainData == null)
                return;
            var data = terrain.terrainData;
            int res = data.holesResolution;
            Vector3 size = data.size;
            Vector3 origin = terrain.transform.position;
            float pad = 2.5f;   // 벽 바깥면·해상도 반올림까지 덮어야 방 안에 잔디 조각이 안 남는다
            int x0 = Mathf.Clamp(Mathf.FloorToInt((center.x - half - pad - origin.x) / size.x * res), 0, res - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((center.x + half + pad - origin.x) / size.x * res), 0, res - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((center.z - half - pad - origin.z) / size.z * res), 0, res - 1);
            int z1 = Mathf.Clamp(Mathf.CeilToInt((center.z + half + pad - origin.z) / size.z * res), 0, res - 1);
            int w = x1 - x0 + 1;
            int h = z1 - z0 + 1;
            if (w <= 0 || h <= 0)
                return;
            var holes = new bool[h, w];   // false = 구멍
            data.SetHoles(x0, z0, holes);
            EditorUtility.SetDirty(data);
        }

        // 뚜껑 윗면은 지표 아래로, 아랫면(=방 천장)은 예전 높이 그대로 — 두께로 맞춘다.
        // 천장이 내려오면 보스가 천장에 닿고 실내 화면 판정이 흔들린다.
        // 뚜껑 윗면은 지표 아래로, 아랫면(=방 천장)은 예전 높이 그대로 — 두께로 맞춘다.
        // 천장이 내려오면 보스가 천장에 닿고 실내 화면 판정이 흔들린다.
        public const float CapTopBelowGround = 0.60f;
        public const float CapBottomBelowGround = 2.10f;

        /// <summary>
        /// 암반 뚜껑 6×6 타일. 한 장이면 통째로 사라져 다시 잔디가 보이므로 격자로 깐다.
        /// **윗면 높이는 타일마다 그 자리의 지면에서 잰다** — 방 중심 한 곳만 재면 경사에서 뚜껑이 지표로 솟아
        /// 조망에 회색 판으로 찍힌다(검수 반려 B의 잔재가 그랬다). 아랫면은 방 천장이라 고정한다.
        /// </summary>
        static void BuildCapTiles(Transform room, Vector3 center, float span, Material ceilMat)
        {
            float centerGround = OnGround(new Vector3(center.x, 0f, center.z)).y;
            float bottom = centerGround - CapBottomBelowGround;
            int capTiles = 6;
            float capSpan = span + 16f;
            float tile = capSpan / capTiles;
            for (int cx = 0; cx < capTiles; cx++)
            {
                for (int cz = 0; cz < capTiles; cz++)
                {
                    float px = center.x - capSpan * 0.5f + tile * (cx + 0.5f);
                    float pz = center.z - capSpan * 0.5f + tile * (cz + 0.5f);
                    float top = OnGround(new Vector3(px, 0f, pz)).y - CapTopBelowGround;
                    float thick = Mathf.Max(0.8f, top - bottom);
                    RoomSlab(room, "DungeonCap", new Vector3(px, top - thick * 0.5f, pz),
                        new Vector3(tile * 1.02f, thick, tile * 1.02f), ceilMat);
                }
            }
        }

        /// <summary>
        /// 방 크기(`Dungeon*.RoomHalf`)가 바뀌면 **이미 지어진 방은 안 따라온다** — `EnsureDungeon*`이
        /// 실내 오브젝트가 있으면 일찍 반환하기 때문이다. 벽 실측 반경이 원장 값과 다르면 방 구조물만
        /// 지우고 다시 짓는다(몹·보스·소품은 이름이 달라 남는다).
        /// </summary>
        /// <summary>
        /// 씬에 이미 놓인 배치물을 **그 자리 지표**로 끌어올린다(검수 2026-09-06 A).
        /// 지형 높이를 올린 랩 이후 마을 건물·상인·장식이 지표 10m 아래에 묻혀 있었다 — 화면에서 마을이 사라졌다.
        /// 대상·바운드·기대 높이는 게이트와 같은 `GroundFit`을 쓴다.
        /// </summary>
        /// <summary>
        /// 자격 없는 모델을 쓰는 몹은 **지우고 다시 짓게** 한다 — `Ensure*`는 오브젝트가 있으면 일찍 반환하므로
        /// 모델 상수를 바꿔도 이미 저장된 씬은 옛 모델 그대로다(검수 2026-09-06 야만인 교체에서 실측).
        /// 이 패스는 `EnsureHuntMobs`·`EnsureDungeon*`보다 **먼저** 돌아야 한다.
        /// </summary>
        public static void EnsureMobArtQualified()
        {
            var names = new List<string>();
            for (int i = 0; i < HuntSpots.Length; i++)
                names.Add(HuntSpots[i].Name);
            names.Add(Dungeon1.MobObject); names.Add(Dungeon2.MobObject); names.Add(Dungeon3.MobObject);
            names.Add("Companion");
            int removed = 0;
            for (int i = 0; i < names.Count; i++)
            {
                var go = GameObject.Find(names[i]);
                if (go == null)
                    continue;
                if (MobArt.ModelOf(go, out MobArt.Model model, out string _) && model.BodyReadsClothed)
                    continue;
                UnityEngine.Object.DestroyImmediate(go);
                removed++;
            }
            if (removed > 0)
                Debug.Log("[Ulon] 자격 없는 몹 모델 " + removed + "체 제거 — 다음 Ensure에서 등록 모델로 다시 짓는다");
        }

        /// <summary>
        /// **역할 없는 물레방아를 씬에서 지운다**(검수 절차 판정 2026-09-07).
        ///
        /// 절차대로 역할 원장(`RoleLook.Facilities`)을 먼저 봤고 **물레방아에 대응하는 역할이 없다**
        /// (제분소 같은 기능이 게임에 없다). 남은 것은 장식이고, 마을 광장 포장 위의 물레방아는
        /// §8.1 기능↔외형 어긋남 그 자체다. 「낚시터에서 치웠다」던 그 물건이 실은 지표 아래로
        /// 내려가 화면에서만 사라져 있었고(지면 스냅이 되올렸다), 이번엔 **정말로 지운다**.
        ///
        /// 낚시터는 건드리지 않는다 — `EnsureFishSpot`이 먼저 돌아 이름을 `FishingSpot`으로 바꾸므로
        /// 여기서는 「아직 물레방아인 것」만 남는다(순서가 곧 안전장치다).
        /// </summary>
        /// <summary>
        /// **장식이 사람 몸에 박혀 있으면 장식을 비킨다**(검수 판정 2026-09-08).
        ///
        /// 훈련사 머리에 지붕이, 상인 몸에 다른 지붕이 겹쳐 있었다. 사람을 옮기는 것은 금지고
        /// (그 자리가 §18.19대로 맞다), 겹친 쪽은 **역할 없는 장식**이니 장식이 물러난다.
        /// 개별 좌표를 손보지 않는다 — 겹치면 밀어내는 **규칙**이라 다음에 장식을 더 놔도 같은 일이 안 난다.
        /// 멱등: 안 겹치면 아무것도 안 움직인다.
        /// </summary>
        public static void EnsureDecorClearOfPeople()
        {
            var people = UnityEngine.Object.FindObjectsByType<CharacterController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var rends = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            int moved = 0;
            for (int i = 0; i < people.Length; i++)
            {
                if (!GroundFit.BodyBounds(people[i].transform, out Bounds body))
                    continue;
                for (int r = 0; r < rends.Length; r++)
                {
                    if (!StuckInBody(rends[r], people[i].transform, body))
                        continue;
                    var group = PieceGroup(rends[r].transform);
                    if (group == null || group.GetComponentInChildren<CharacterController>(true) != null)
                        continue;                              // 사람 자신은 옮기지 않는다
                    var away = group.position - people[i].transform.position;
                    away.y = 0f;
                    if (away.sqrMagnitude < 0.0001f)
                        away = Vector3.right;
                    away = away.normalized;
                    int step = 0;
                    while (step < 20 && StuckInBody(rends[r], people[i].transform, body))
                    {
                        group.position += away * 0.25f;
                        step++;
                    }
                    if (step > 0)
                    {
                        // 밀어낸 뒤에도 발은 땅에 붙어 있어야 한다(공중 장식 사고와 같은 원인).
                        group.position = new Vector3(group.position.x, GroundHeightAt(group.position.x, group.position.z), group.position.z);
                        Debug.Log("[Ulon] 조각이 사람 몸에 박혀 비켜섰다 — " + GroundFit.NodePath(group) + " ← " + people[i].name +
                                  " " + (step * 0.25f).ToString("0.00") + "m");
                        moved++;
                    }
                }
            }
            Debug.Log("[Ulon] 조각-사람 겹침 정리 — 사람 " + people.Length + "명 대상, 비켜선 조각 " + moved + "개");
        }

        /// <summary>
        /// **맞추는 자는 재는 자와 같아야 한다** — 게이트(`PiecesInBody`)와 **똑같은 기준**으로 묻는다:
        /// 몸 부피의 5% 이상 겹치고, **삼각형이 실제로 몸에 닿는가**. 예전엔 이 패스가 자기만의
        /// 기준(VillageDecor 그룹 바운드)으로 밀어서, 게이트가 부르는 조각은 손도 못 대고
        /// 평평한 길 타일만 밀어냈다.
        /// </summary>
        static bool StuckInBody(Renderer rend, Transform person, Bounds body)
        {
            if (rend == null || !rend.enabled || rend is ParticleSystemRenderer)
                return false;
            var t = rend.transform;
            if (t.IsChildOf(person) || t.GetComponent<Terrain>() != null)
                return false;
            if (t.GetComponentInParent<Ulon.Server.WorldBody>() != null)
                return false;                                  // 사람·짐승 겹침은 다른 축
            var b = rend.bounds;
            if (b.size.x > 20f || b.size.z > 20f || b.size.y < 0.3f)
                return false;                                  // 월드 규모 판·바닥 타일
            if (!b.Intersects(body))
                return false;
            var lo = Vector3.Max(b.min, body.min);
            var hi = Vector3.Min(b.max, body.max);
            var ov = Vector3.Max(hi - lo, Vector3.zero);
            if (ov.x * ov.y * ov.z < body.size.x * body.size.y * body.size.z * 0.05f)
                return false;
            return SliceSelfCheck.MeshHitsBox(rend, body);
        }

        /// <summary>조각의 **이동 단위** — 뿌리(시설·건물)가 아니라 그 직계 자식(붙인 조각)을 옮긴다.</summary>
        static Transform PieceGroup(Transform t)
        {
            var cur = t;
            while (cur.parent != null && cur.parent.parent != null)
                cur = cur.parent;
            return cur;
        }

        /// <summary>두 바운드의 겹침 부피가 몸의 `share` 이상인가 — 「스쳤다」와 「박혔다」를 가른다.</summary>
        static bool Overlaps(Bounds a, Bounds body, float share)
        {
            var lo = Vector3.Max(a.min, body.min);
            var hi = Vector3.Min(a.max, body.max);
            var ov = Vector3.Max(hi - lo, Vector3.zero);
            return ov.x * ov.y * ov.z >= body.size.x * body.size.y * body.size.z * share;
        }

        public static void EnsureNoRolelessWatermill()
        {
            var gone = new List<string>();
            var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || !IsWatermillName(all[i].name))
                    continue;
                gone.Add(GroundFit.NodePath(all[i]) + all[i].position.ToString("F1"));
                UnityEngine.Object.DestroyImmediate(all[i].gameObject);
            }
            if (gone.Count > 0)
                Debug.Log("[Ulon] 역할 없는 물레방아 " + gone.Count + "채 제거: " + string.Join(", ", gone));
        }

        /// <summary>모델 이름으로 고른다 — 이건 「이름으로 거르기」가 아니라 **어떤 자산인지**를 보는 것이다.</summary>
        public static bool IsWatermillName(string n)
        {
            return string.Equals(n, "watermill", StringComparison.OrdinalIgnoreCase) ||
                   n.StartsWith("watermill (", StringComparison.OrdinalIgnoreCase);
        }

        public static void EnsureFootOnGround()
        {
            var items = GroundFit.Candidates();
            int moved = 0;
            float worst = 0f;
            string worstName = "";
            for (int i = 0; i < items.Count; i++)
            {
                if (!GroundFit.BodyBounds(items[i], out Bounds b))
                    continue;
                float dy = GroundFit.ExpectedGroundY(items[i], b) - b.min.y;
                if (Mathf.Abs(dy) < 0.02f)
                    continue;
                items[i].position += new Vector3(0f, dy, 0f);
                moved++;
                if (Mathf.Abs(dy) > Mathf.Abs(worst)) { worst = dy; worstName = items[i].name; }
            }
            if (moved > 0)
                Debug.Log("[Ulon] 배치물 지표 스냅 — " + moved + "개 이동(최대 " + worstName + " " + worst.ToString("0.00") + "m)");
        }

        /// <summary>
        /// 넓힌 방을 **채운다**(검수 2026-09-06 관찰: 반경 6→8m로 늘린 만큼 실내가 빈 바닥이 됐다).
        /// §6.1 던전 콘텐츠 — 지지 기둥·짐·잔해·벽 등불. 멱등: `DungeonFurn*`을 지우고 다시 짓는다.
        ///
        /// 소품은 **등록 CC0 모델**에서만 온다(검수 반려: 처음 판은 전부 무텍스처 색칠 큐브였다 —
        /// §8.2 프리미티브 금지). 자격 원장은 `Editor/PropArt.cs`, 게이트는 `AssertRoomPropsQualified`.
        /// 방 한가운데는 전투 공간으로 비우고, 소품은 벽 쪽·모서리에 둔다(검수 요구).
        /// 방 구조물과 같은 블로커 레이어라 카메라 앞에 오면 벽처럼 페이드된다.
        /// </summary>
        /// <summary>짐이 돌기둥을 비켜서는 최소 각(축 기준). 24°는 실측으로 고른 값이다 — 18°에서도 파고들었다.</summary>
        const float PillarClearDeg = 24f;

        public static void EnsureRoomFurnishing()
        {
            const string Dg = "Assets/_ThirdParty/KayKit/Dungeon/RAW/Models/";
            // 방을 채우는 주력: 궤짝·통·나무상자(§6.1 던전). 마을 가구는 뺐다 — 검수가 「가구 창고」라고 반려했다.
            string[] loadFbx =
            {
                Dg + "box_large.obj", Dg + "barrel_large.obj", Dg + "crates_stacked.obj",
                Dg + "box_stacked.obj", Dg + "barrel_small_stack.obj", Dg + "chest.obj",
                Dg + "box_small.obj", Dg + "table_medium_broken.obj",
            };
            string[] rubbleFbx = { Dg + "rubble_large.obj", Dg + "rubble_half.obj" };

            var rooms = new[]
            {
                new { Obj = Dungeon1.InteriorObject, X = Dungeon1.InteriorX, Z = Dungeon1.InteriorZ, Half = Dungeon1.RoomHalf, Seed = 11 },
                new { Obj = Dungeon2.InteriorObject, X = Dungeon2.InteriorX, Z = Dungeon2.InteriorZ, Half = Dungeon2.RoomHalf, Seed = 23 },
                new { Obj = Dungeon3.InteriorObject, X = Dungeon3.InteriorX, Z = Dungeon3.InteriorZ, Half = Dungeon3.RoomHalf, Seed = 37 },
            };

            int placed = 0;
            for (int i = 0; i < rooms.Length; i++)
            {
                var go = GameObject.Find(rooms[i].Obj);
                if (go == null)
                    continue;
                var room = go.transform;
                for (int c = room.childCount - 1; c >= 0; c--)
                    if (room.GetChild(c).name.StartsWith("DungeonFurn", StringComparison.Ordinal))
                        UnityEngine.Object.DestroyImmediate(room.GetChild(c).gameObject);

                var center = new Vector3(rooms[i].X, 0f, rooms[i].Z);
                // 바닥 슬래브 **윗면**은 방 원점보다 0.2m 위다 — 여기에 놓지 않으면 소품이 바닥 밑에 묻힌다(실측).
                float y = OnGround(center).y - DungeonDepth + RoomFloorTop;
                float half = rooms[i].Half;
                var rng = new System.Random(rooms[i].Seed);
                float Jitter(float span) => (float)(rng.NextDouble() - 0.5) * span;

                // 네 변 가운데 — **천장까지 닿는 돌기둥** + 벽 횃불. 짧은 막대는 허공의 막대기로 읽힌다(검수).
                for (int sIdx = 0; sIdx < 4; sIdx++)
                {
                    float px = sIdx == 2 ? half - 0.7f : sIdx == 3 ? -half + 0.7f : 0f;
                    float pz = sIdx == 0 ? half - 0.7f : sIdx == 1 ? -half + 0.7f : 0f;
                    placed += RoomProp(room, "DungeonFurnPillar" + sIdx, Dg + (sIdx % 2 == 0 ? "pillar.obj" : "pillar_decorated.obj"),
                        new Vector3(center.x + px, y, center.z + pz), sIdx * 90f, RoomHeightOfWall - RoomFloorTop) ? 1 : 0;
                    placed += RoomProp(room, "DungeonFurnTorch" + sIdx, Dg + "torch_mounted.obj",
                        new Vector3(center.x + px * 0.94f, y + 2.0f, center.z + pz * 0.94f), sIdx * 90f + 180f, 1.1f) ? 1 : 0;
                    RoomTorch(room, new Vector3(center.x + px * 0.85f, y + 2.4f, center.z + pz * 0.85f), half * 0.7f);
                }

                // 벽 쪽 짐 — 궤짝·통·상자. 수로 채우지 않는다(검수: 물량이 곧 반려 사유였다).
                // **자리는 12칸이 아니라 8칸**이다(2026-09-08 실측 수리). 예전 12칸(30°마다, 위상 20°)은
                // 축 방향에 선 돌기둥과 10°(=1.1m)밖에 안 떨어져 `Load5`가 기둥에 **100% 파고들어** 있었다.
                // 재 본 다른 판 둘은 각각 이렇게 깨졌다: ㉠고리를 안쪽으로(half−3.0m) → 소품이 몰려
                // **허공 게이트 NC가 죽었다**(자 하나 맞추다 다른 자를 부순다) ㉡겹치는 슬롯만 옆으로 밀기
                // → 밀린 슬롯이 **이웃 짐과** 겹쳤다. 그래서 자리 자체를 기둥에서 30° 떨어진 8칸으로 바꿨다
                // (사분면마다 둘 — 분포 게이트 그대로, 종류는 8종 1개씩이라 편중도 없다).
                for (int k = 0; k < 8; k++)
                {
                    float deg = (k / 2) * 90f + (k % 2 == 0 ? 30f : 60f);
                    float a = deg * Mathf.Deg2Rad;
                    float r = half - 1.7f - Mathf.Abs(Jitter(1.2f));
                    var p = new Vector3(center.x + Mathf.Sin(a) * r, y, center.z + Mathf.Cos(a) * r);
                    string fbx = loadFbx[k % loadFbx.Length];
                    // **크기는 플레이어 키 비율로** 잡는다 — 바닥 폭으로 맞췄더니 통이 사람보다 컸다(검수).
                    // 통·상자·궤짝은 허리~가슴(0.4~0.7배), 부서진 탁자는 그 사이.
                    float h = PlayerHeight * LoadHeightFrac(fbx);
                    // 벽을 바라보게 세운다 — 무작위 yaw는 소품을 누운 것처럼 보이게 했다(검수).
                    placed += RoomProp(room, "DungeonFurnLoad" + k, fbx, p, a * Mathf.Rad2Deg + 180f, h, true) ? 1 : 0;
                }

                // **중간 고리 — 시선을 끊는 자리 넷**(검수 2026-09-08: 「소품이 전부 벽에 몰려 방 한가운데가
                // 텅 비어 창고 벽면처럼 읽힌다」). 전투 공간(중앙 반경 `CombatClearRadius`)은 그대로 비우고,
                // 그 바깥 반경 4.6m·축 방향 네 자리에 부러진 기둥과 잔해를 둔다 — 벽 짐(반경 6.3m·30°/60°)과는
                // 각·반경 둘 다 벌어지고, 벽 기둥(같은 축·반경 7.3m)과는 2.7m 떨어진다.
                for (int k = 0; k < 4; k++)
                {
                    float a = k * 90f * Mathf.Deg2Rad;
                    float r = 4.6f;
                    var p = new Vector3(center.x + Mathf.Sin(a) * r, y, center.z + Mathf.Cos(a) * r);
                    bool stump = k % 2 == 0;
                    placed += RoomProp(room, "DungeonFurnBreak" + k,
                        stump ? Dg + "pillar.obj" : Dg + "rubble_large.obj",
                        p, (float)rng.NextDouble() * 360f,
                        PlayerHeight * (stump ? 0.78f : 0.40f), true) ? 1 : 0;
                }

                // 잔해 — 모서리에 넷만. 물량으로 쓰면 방이 채석장이 된다(검수 반려).
                // **네 모서리로 밀어 넣는다**: 예전엔 짐과 같은 반경(half−1.5)에 45°로 놓아서
                // 30°마다 도는 짐(50°·140°…)과 5°밖에 안 떨어졌고, 실측에서 잔해가 궤짝 안에
                // **100% 파고들어** 있었다(`AssertPropsNotOverlapping` 첫 판정). 방은 정사각형이라
                // 45° 방향은 벽까지 half×1.41m다 — 그 사이로 들어가면 짐 고리와 3m 벌어진다.
                for (int k = 0; k < 4; k++)
                {
                    float a = (k * 90f + 45f) * Mathf.Deg2Rad;
                    float r = half * 1.20f;
                    var p = new Vector3(center.x + Mathf.Sin(a) * r, y, center.z + Mathf.Cos(a) * r);
                    placed += RoomProp(room, "DungeonFurnRubble" + k, rubbleFbx[k % rubbleFbx.Length],
                        p, (float)rng.NextDouble() * 360f,
                        PlayerHeight * (0.28f + (float)rng.NextDouble() * 0.12f), true) ? 1 : 0;
                }
            }
            Debug.Log("[Ulon] 던전 실내 채우기 — 등록 CC0 소품 " + placed + "개(돌기둥·벽 횃불·궤짝·통·잔해), 프리미티브 0개");
        }

        /// <summary>
        /// 방 짐 소품의 높이를 **플레이어 키에 대한 비율**로 준다(검수 요구: 절대 수치 금지).
        /// 상한 0.8배·하한 0.15배는 게이트 `AssertPropScaleRatio`가 지킨다.
        /// </summary>
        static float LoadHeightFrac(string fbx)
        {
            if (fbx.EndsWith("box_small.obj", StringComparison.Ordinal)) return 0.34f;
            if (fbx.EndsWith("chest.obj", StringComparison.Ordinal)) return 0.42f;
            if (fbx.EndsWith("barrel_small_stack.obj", StringComparison.Ordinal)) return 0.52f;
            if (fbx.EndsWith("table_medium_broken.obj", StringComparison.Ordinal)) return 0.55f;
            if (fbx.EndsWith("box_large.obj", StringComparison.Ordinal)) return 0.60f;
            if (fbx.EndsWith("barrel_large.obj", StringComparison.Ordinal)) return 0.66f;
            if (fbx.EndsWith("box_stacked.obj", StringComparison.Ordinal)) return 0.70f;
            if (fbx.EndsWith("crates_stacked.obj", StringComparison.Ordinal)) return 0.74f;
            return 0.60f;
        }

        /// <summary>
        /// 방 안에 등록 CC0 모델을 놓는다. `size`(월드 m)에 맞춰 **균등 배율**로 맞추되,
        /// `byHeight`면 높이 기준, 아니면 **바닥 폭 기준**이다(납작한 널빤지를 높이로 맞추면 10m 판이 된다 — 실측).
        /// 발을 `pos.y`(방 바닥 윗면)에 붙인다. 지형 스냅(`Place`)은 지하 방에서 쓸 수 없다 — 지표로 끌어올린다.
        /// 콜라이더는 렌더러와 **같은 오브젝트**에 붙인다(`DungeonSightFade`가 콜라이더에서 렌더러를 찾는다).
        /// </summary>
        static bool RoomProp(Transform room, string name, string fbx, Vector3 pos, float yaw, float size)
        {
            return RoomProp(room, name, fbx, pos, yaw, size, true);
        }

        static bool RoomProp(Transform room, string name, string fbx, Vector3 pos, float yaw, float size, bool byHeight)
        {
            return RoomPropObject(room, name, fbx, pos, yaw, size, byHeight) != null;
        }

        static GameObject RoomPropObject(Transform room, string name, string fbx, Vector3 pos, float yaw, float size, bool byHeight)
        {
            if (!PropArt.IsRegistered(fbx))
                throw new InvalidOperationException("소품 " + fbx + "은(는) PropArt 원장에 없습니다 — 등록 없이 쓰지 마라(§8.2·§11).");
            string prefabPath = EnsureEnvPrefab(fbx);
            var prefab = string.IsNullOrEmpty(prefabPath) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[Ulon] 소품 프리팹 없음: " + fbx);
                return null;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = name;
            go.transform.SetParent(room, true);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one;

            Bounds b;
            if (BoundsOf(go.transform, true, out b))
            {
                float src = byHeight ? b.size.y : Mathf.Max(b.size.x, b.size.z);
                if (src > 0.001f)
                    go.transform.localScale = Vector3.one * (size / src);
            }
            if (BoundsOf(go.transform, true, out b))
                go.transform.position += Vector3.up * (pos.y - b.min.y);

            int blocker = LayerMask.NameToLayer(DungeonBlockerLayer);
            var mfs = go.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < mfs.Length; i++)
            {
                if (mfs[i].sharedMesh == null)
                    continue;
                if (blocker >= 0)
                    mfs[i].gameObject.layer = blocker;
                if (mfs[i].GetComponent<Collider>() == null)
                {
                    var mc = mfs[i].gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mfs[i].sharedMesh;
                }
            }
            if (blocker >= 0)
                go.layer = blocker;
            return go;
        }

        /// <summary>방 원점에서 바닥 슬래브 윗면까지.</summary>
        public const float RoomFloorTop = 0.2f;

        /// <summary>플레이어 키(m) — 소품 크기의 **유일한 기준**이다(검수: 절대 수치로 박으면
        /// 캐릭터 스케일을 바꾸는 순간 또 어긋난다). `SpawnActor("Player", …)`도 이 값을 쓴다.</summary>
        public const float PlayerHeight = 1.8f;

        /// <summary>킷(Kenney FantasyTown, 1m 모듈) 배율 — **유일한 원장**이다.
        /// 눈대중이 아니라 **잰 값에서 유도했다**: 문 개구부는 원래 크기에서 0.74m,
        /// 사람이 머리를 안 부딪는 최소는 `PlayerHeight × 0.85 = 1.53m`이므로
        /// 1.53 / 0.74 = 2.07 → 올려서 2.1(게이트 `AssertDoorFitsPerson`이 이 비를 상시 잰다).
        /// 지형(실 미터)·KayKit 던전 소품은 이 배율의 대상이 아니다.</summary>
        public const float KitScale = 2.1f;

        /// <summary>방 벽 높이(바닥에서 지면까지) — 채움 기둥이 벽과 같은 높이여야 한다.</summary>
        /// <summary>방 벽 높이 — 게이트도 「방 천장 위」를 이 값으로 판별한다(단일 원장).</summary>
        public const float RoomHeightOfWall = DungeonDepth + 0.15f;

        public static void EnsureRoomSize()
        {
            var rooms = new[]
            {
                new { Obj = Dungeon1.InteriorObject, X = Dungeon1.InteriorX, Z = Dungeon1.InteriorZ, Half = Dungeon1.RoomHalf, H = Dungeon1.RoomHeight, Door = "West" },
                new { Obj = Dungeon2.InteriorObject, X = Dungeon2.InteriorX, Z = Dungeon2.InteriorZ, Half = Dungeon2.RoomHalf, H = Dungeon2.RoomHeight, Door = "East" },
                new { Obj = Dungeon3.InteriorObject, X = Dungeon3.InteriorX, Z = Dungeon3.InteriorZ, Half = Dungeon3.RoomHalf, H = Dungeon3.RoomHeight, Door = "West" },
            };
            for (int i = 0; i < rooms.Length; i++)
            {
                var go = GameObject.Find(rooms[i].Obj);
                if (go == null)
                    continue;
                var room = go.transform;
                var center = new Vector3(rooms[i].X, 0f, rooms[i].Z);

                // 실측 반경 = 벽 슬래브 중심의 최대 수평 편차. 문 쪽이 비어도 나머지 세 면이 있으므로 잰다.
                float measured = -1f;
                bool hasDoorBack = false;
                bool hasRockFill = false;
                bool ringSunk = true;
                float floorSpan = -1f;
                for (int c = 0; c < room.childCount; c++)
                {
                    var child = room.GetChild(c);
                    if (child.name == "DungeonWallDoorBack")
                        hasDoorBack = true;
                    if (child.name.StartsWith("DungeonRockFill", StringComparison.Ordinal))
                    {
                        hasRockFill = true;
                        // **있다/없다만 보면 옛 판이 그대로 남는다** — 링을 지표 아래로 내린 뒤에도
                        // `Ensure*`가 일찍 반환해 씬은 예전 높이였다(2026-09-08). 자리까지 본다.
                        var fr = child.GetComponent<Renderer>();
                        if (fr != null && fr.bounds.max.y > GroundFit.TerrainY(fr.bounds.center.x, fr.bounds.center.z))
                            ringSunk = false;
                    }
                    if (child.name == "DungeonFloor")
                        floorSpan = child.localScale.x;
                    if (!child.name.StartsWith("DungeonWall", StringComparison.Ordinal) || child.name.StartsWith("DungeonWallDoor", StringComparison.Ordinal))
                        continue;
                    var p = child.position;
                    measured = Mathf.Max(measured, Mathf.Max(Mathf.Abs(p.x - center.x), Mathf.Abs(p.z - center.z)));
                }
                bool sizeOk = measured >= 0f && Mathf.Abs(measured - rooms[i].Half) < 0.05f;
                bool floorOk = Mathf.Abs(floorSpan - (rooms[i].Half * 2f + RockRingGap * 2f + 4f)) < 0.05f;
                if (sizeOk && hasDoorBack && hasRockFill && ringSunk && floorOk)
                    continue;

                int removed = 0;
                for (int c = room.childCount - 1; c >= 0; c--)
                {
                    var child = room.GetChild(c);
                    string n = child.name;
                    bool structural = n.StartsWith("Dungeon", StringComparison.Ordinal)
                        || n.StartsWith("CapDress", StringComparison.Ordinal)
                        || n.IndexOf("lantern", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!structural)
                        continue;
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                    removed++;
                }
                BuildDungeonRoom(room, center, rooms[i].Half, rooms[i].H, rooms[i].Door);
                Debug.Log("[Ulon] 던전 방 보수 — " + rooms[i].Obj + " 반경 " + measured.ToString("0.0") + "m → " +
                    rooms[i].Half.ToString("0.0") + "m, 문 밖 통로 " + (hasDoorBack ? "있음" : "없음→신설") +
                    "·바깥 암반 " + (hasRockFill ? "있음" : "없음→신설") + ", 구조물 " + removed + "개 재건");
            }
        }

        /// <summary>
        /// 이미 만들어진 씬을 고치는 멱등 보수 패스(검수 반려 B). `Ensure*`는 오브젝트가 있으면 일찍 반환하므로
        /// 코드 수정만으로는 디스크의 씬이 안 고쳐진다. 옛 뚜껑·뚜껑 장식을 지우고 새 높이로 다시 깐다.
        /// </summary>
        public static void EnsureCapBuried()
        {
            RepairTerrainHoles();
            var ceilMat = MakeNoiseMat("DungeonCeiling", new Color(0.11f, 0.11f, 0.13f), new Color(0.18f, 0.17f, 0.20f));
            var rooms = new[]
            {
                new { Obj = Dungeon1.InteriorObject, X = Dungeon1.InteriorX, Z = Dungeon1.InteriorZ, Half = Dungeon1.RoomHalf },
                new { Obj = Dungeon2.InteriorObject, X = Dungeon2.InteriorX, Z = Dungeon2.InteriorZ, Half = Dungeon2.RoomHalf },
                new { Obj = Dungeon3.InteriorObject, X = Dungeon3.InteriorX, Z = Dungeon3.InteriorZ, Half = Dungeon3.RoomHalf },
            };
            int removed = 0;
            for (int i = 0; i < rooms.Length; i++)
            {
                var go = GameObject.Find(rooms[i].Obj);
                if (go == null)
                    continue;
                var room = go.transform;
                for (int c = room.childCount - 1; c >= 0; c--)
                {
                    var child = room.GetChild(c);
                    if (child.name == "DungeonCap" || child.name.StartsWith("CapDress", StringComparison.Ordinal))
                    {
                        UnityEngine.Object.DestroyImmediate(child.gameObject);
                        removed++;
                    }
                }
                BuildCapTiles(room, new Vector3(rooms[i].X, 0f, rooms[i].Z), rooms[i].Half * 2f, ceilMat);
            }
            Debug.Log("[Ulon] 던전 뚜껑 매설 — 옛 타일 " + removed + "장 제거 후 타일마다 그 자리 지표 " + CapTopBelowGround + "m 아래로 재배치, Terrain 홀 복구");
        }

        /// <summary>뚫어 둔 Terrain 홀을 전부 메운다 — 홀은 에셋에 남으므로 코드에서 안 뚫는 것만으로는 안 사라진다.</summary>
        static void RepairTerrainHoles()
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null || terrain.terrainData == null)
                return;
            var data = terrain.terrainData;
            int res = data.holesResolution;
            var solid = new bool[res, res];
            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                    solid[z, x] = true;
            data.SetHoles(0, 0, solid);
            EditorUtility.SetDirty(data);
        }

        /// <summary>방 벽에서 바깥 암반 링까지의 거리 — 카메라 눈의 수평 오프셋(실내 줌 8.11m × cos35° = 6.6m)보다 넉넉히.</summary>
        public const float RockRingGap = 14f;

        public static void BuildDungeonRoom(Transform room, Vector3 center, float half, float wallH, string doorSide)
        {
            // 앰비언트가 야외 값(0.55)이라 알베도를 낮춰야 실내가 낮처럼 안 보인다(§8.2).
            var floorMat = MakeNoiseMat("DungeonFloor", new Color(0.13f, 0.12f, 0.14f), new Color(0.21f, 0.20f, 0.21f));
            var wallMat = MakeNoiseMat("DungeonWall", new Color(0.11f, 0.10f, 0.12f), new Color(0.19f, 0.18f, 0.20f));
            var ceilMat = MakeNoiseMat("DungeonCeiling", new Color(0.11f, 0.11f, 0.13f), new Color(0.18f, 0.17f, 0.20f));

            // 지하화(검수 2026-09-06 P0 마무리) — 지상에 상자를 얹으면 페이드가 걷힐 때 화면 절반이 잔디밭이 된다(§8.2).
            float ground = OnGround(new Vector3(center.x, 0f, center.z)).y;
            float y = ground - DungeonDepth;
            float span = half * 2f;
            float seg = span / 3f;
            float t = 0.5f;
            wallH = DungeonDepth + 0.15f;   // 바닥에서 지면까지 — 벽 너머로 잔디가 보이지 않게

            // Terrain 홀은 더 이상 뚫지 않는다(검수 2026-09-06 반려 B) — 구멍은 조망에서 **짙은 회색 직사각형**으로 읽혔다.
            // 실내 줌(§4.2) 이후 카메라가 방 안에 있어 뚜껑이 페이드로 걷히지 않으므로, 지표는 잔디 그대로 두고
            // 뚜껑을 지표 **아래로** 묻는다(하늘·주광 차단은 뚜껑이 계속 한다).
            RepairTerrainHoles();

            // 바닥은 Terrain 홀보다 넓어야 한다 — 좁으면 방 가장자리로 하늘이 비친다(플레이캠 실측).
            // 바닥은 방보다 훨씬 넓어야 한다 — 플레이어가 귀퉁이에 서면 카메라 눈이 벽 밖에 놓이고,
            // 화면 아래쪽 시선이 바닥 판 **바깥으로 떨어져** 검은 띠가 생긴다(검수 2026-09-06 B 실측: 아래 18%).
            float floorSpan = span + RockRingGap * 2f + 4f;
            RoomSlab(room, "DungeonFloor", new Vector3(center.x, y + 0.1f, center.z), new Vector3(floorSpan, 0.2f, floorSpan), floorMat);

            for (int side = 0; side < 4; side++)
            {
                for (int i = -1; i <= 1; i++)
                {
                    string name = side == 0 ? "North" : side == 1 ? "South" : side == 2 ? "East" : "West";
                    if (i == 0 && name == doorSide)
                        continue;
                    Vector3 pos;
                    Vector3 size;
                    float off = i * seg;
                    if (side == 0) { pos = new Vector3(center.x + off, y + wallH * 0.5f, center.z + half); size = new Vector3(seg, wallH, t); }
                    else if (side == 1) { pos = new Vector3(center.x + off, y + wallH * 0.5f, center.z - half); size = new Vector3(seg, wallH, t); }
                    else if (side == 2) { pos = new Vector3(center.x + half, y + wallH * 0.5f, center.z + off); size = new Vector3(t, wallH, seg); }
                    else { pos = new Vector3(center.x - half, y + wallH * 0.5f, center.z + off); size = new Vector3(t, wallH, seg); }
                    RoomSlab(room, "DungeonWall" + name + (i + 1), pos, size, wallMat);
                }
            }

            // 문 쪽 한 칸은 벽이 없다 — 그 틈으로 쏜 시선은 **아무것도 안 맞고** 화면에 검은 삼각형으로 남는다
            // (검수 2026-09-06: 8.11m 줌 오른쪽 아래 모서리). 문 밖에 짧은 통로를 이어 막는다.
            {
                Vector3 dir = doorSide == "North" ? Vector3.forward : doorSide == "South" ? Vector3.back
                    : doorSide == "East" ? Vector3.right : Vector3.left;
                Vector3 per = new Vector3(dir.z, 0f, dir.x);
                float len = 5f;
                Vector3 mouth = center + dir * half;
                Vector3 axis(float along, float side) => new Vector3(
                    mouth.x + dir.x * along + per.x * side, 0f, mouth.z + dir.z * along + per.z * side);
                Vector3 SizeOf(float along, float across) =>
                    new Vector3(Mathf.Abs(dir.x) * along + Mathf.Abs(per.x) * across, 0f, Mathf.Abs(dir.z) * along + Mathf.Abs(per.z) * across);

                var back = axis(len, 0f);
                var bs = SizeOf(t, seg + t * 2f); bs.y = wallH;
                RoomSlab(room, "DungeonWallDoorBack", new Vector3(back.x, y + wallH * 0.5f, back.z), bs, wallMat);
                for (int s = -1; s <= 1; s += 2)
                {
                    var sideP = axis(len * 0.5f, s * (seg * 0.5f + t * 0.5f));
                    var ss = SizeOf(len, t); ss.y = wallH;
                    RoomSlab(room, "DungeonWallDoorSide" + (s + 1), new Vector3(sideP.x, y + wallH * 0.5f, sideP.z), ss, wallMat);
                }
                var mid = axis(len * 0.5f, 0f);
                var fs = SizeOf(len, seg + t * 2f);
                RoomSlab(room, "DungeonFloorDoor", new Vector3(mid.x, y + 0.1f, mid.z), new Vector3(fs.x, 0.2f, fs.z), floorMat);
                RoomSlab(room, "DungeonCeilDoor", new Vector3(mid.x, y + wallH, mid.z), new Vector3(fs.x, 0.4f, fs.z), ceilMat);

                // **방 바깥도 실내여야 한다.** 플레이어가 귀퉁이에 서면 카메라 눈이 벽 밖(지하)에 놓인다 —
                // 거기에 아무것도 없으면 화면 아래가 통째로 검다(검수 2026-09-06 B, 실측 허공 0.18).
                // 방을 한 겹 더 둘러싸는 **바깥 암반 링**을 두른다. 눈이 어디에 서든 시선 끝에 돌이 있다.
                // (속을 채운 덩어리는 답이 아니다 — 덩어리 **안**에서 쏜 레이는 아무것도 못 맞는다.)
                // 링 꼭대기를 **지표 아래로 내린다**(2026-09-08 실측): 예전엔 방 벽과 같은 높이라
                // 경사면에서 꼭대기가 지표를 0.1~0.7m 뚫고 나왔고, 어두운 던전 텍스처 그대로라
                // 들판에 검은 턱이 생겼다(성질 기반 밝기 게이트가 이걸 잡았다 — 이름 기반일 땐
                // 뿌리 이름이 Dungeon이라 통째로 제외돼 **보이지 않던 결함**이다).
                const float RingSink = 1.4f;
                float ringR = half + RockRingGap;
                float ringLen = ringR * 2f + t * 2f;
                RoomSlab(room, "DungeonRockFillNorth", new Vector3(center.x, y + wallH * 0.5f - RingSink, center.z + ringR), new Vector3(ringLen, wallH, t), wallMat);
                RoomSlab(room, "DungeonRockFillSouth", new Vector3(center.x, y + wallH * 0.5f - RingSink, center.z - ringR), new Vector3(ringLen, wallH, t), wallMat);
                RoomSlab(room, "DungeonRockFillEast", new Vector3(center.x + ringR, y + wallH * 0.5f - RingSink, center.z), new Vector3(t, wallH, ringLen), wallMat);
                RoomSlab(room, "DungeonRockFillWest", new Vector3(center.x - ringR, y + wallH * 0.5f - RingSink, center.z), new Vector3(t, wallH, ringLen), wallMat);
            }

            for (int c = 0; c < 4; c++)
            {
                float sx = (c == 0 || c == 3) ? 1f : -1f;
                float sz = (c == 0 || c == 1) ? 1f : -1f;
                RoomSlab(room, "DungeonPillar" + c,
                    new Vector3(center.x + half * sx, y + wallH * 0.5f + 0.15f, center.z + half * sz),
                    new Vector3(0.9f, wallH + 0.3f, 0.9f), wallMat);
            }

            // 천장 = 지면 높이의 암반 뚜껑. 한 장이면 페이드 때 통째로 사라져 다시 잔디가 보이므로
            // 6×6 타일 격자로 깔아 시선에 걸린 몇 장만 걷히게 한다(단면으로 읽힌다).
            BuildCapTiles(room, center, span, ceilMat);

            const string Lantern = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/lantern.fbx";
            var lanternA = Place(Lantern, new Vector3(center.x - half + 1.2f, 0f, center.z + half - 1.2f), Vector3.zero);
            var lanternB = Place(Lantern, new Vector3(center.x + half - 1.2f, 0f, center.z - half + 1.2f), Vector3.zero);
            if (lanternA != null) { lanternA.transform.SetParent(room, true); SinkIntoDungeon(lanternA); }
            if (lanternB != null) { lanternB.transform.SetParent(room, true); SinkIntoDungeon(lanternB); }
            RoomTorch(room, new Vector3(center.x - half + 1.2f, y + 2.1f, center.z + half - 1.2f), half);
            RoomTorch(room, new Vector3(center.x + half - 1.2f, y + 2.1f, center.z - half + 1.2f), half);
            // 방 전체를 등불 색으로 아주 약하게 받쳐준다 — 주광은 암반 뚜껑 그림자에 막히므로(AssertDungeonLighting)
            // 이 점광들이 실내의 유일한 광원이다. 세면 낮처럼 보이니 약하게.
            RoomFill(room, new Vector3(center.x, y + 2.6f, center.z), half);
        }

        /// <summary>지상 배치 로직(Place/SnapRootToGround)을 거친 오브젝트를 방 바닥 높이로 내린다.</summary>
        public static void SinkIntoDungeon(GameObject go)
        {
            if (go == null)
                return;
            go.transform.position -= new Vector3(0f, DungeonDepth, 0f);
        }

        public static void SinkIntoDungeon(Transform parent, params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                var go = GameObject.Find(names[i]);
                if (go != null)
                    SinkIntoDungeon(go);
            }
        }

        /// <summary>
        /// 방 안 오브젝트를 **방 바닥 윗면**에 세운다. 각자 자기 자리 지면에서 깊이만큼 내리면
        /// 지면 기복만큼 어긋나 바닥에 파묻히거나 뜬다(지형 작업 후 보스가 0.31m 파묻혔다).
        /// </summary>
        /// <summary>
        /// **키운 뒤 바닥에 다시 세운다**(검수 판정 2026-09-07 ①).
        /// 보스는 잡몹의 1.3~1.5배로 **키운 뒤** 자리를 다시 안 잡아서 발이 방 바닥에 묻혔다
        /// (실측 −0.25 / −0.82 / −1.10m). `StandOnRoomFloor`는 **transform y**를 맞출 뿐이라
        /// 스케일이 바뀌면 발(메시 밑면)이 따라 내려간다.
        ///
        /// 그래서 이 패스는 **스케일이 확정된 뒤**(드레싱·크기 조정 전부 끝난 뒤) 돌아야 한다 —
        /// 낚시터에서 배운 「이동은 스냅 뒤에」와 같은 순서 문제다. 게이트와 **같은 자**
        /// (`GroundFit.SurfaceUnder`)를 쓴다.
        /// </summary>
        /// <summary>
        /// **무기가 바닥을 뚫지 않게 한다**(검수 판정 2026-09-07 2).
        /// 실측: 보스 무기 4개가 바닥 아래 0.28~1.10m — 「보스가 묻혔다」던 값과 **같은 숫자**다.
        /// 몸을 바닥에 세우자 그 깊이가 고스란히 무기로 옮겨간 것이니, 두 사건은 원인이 하나다:
        /// **키운 뒤 바닥과의 관계를 아무도 안 봤다.**
        ///
        /// 각도는 건드리지 않는다(칼날 각도 게이트는 안 걸기로 한 결정이 있다). **그립을 축으로 줄인다** —
        /// 축이 손이면 손에 들린 관계가 유지되고, 모자라면 남은 만큼만 위로 민다(무기-손 포함 게이트가 감시).
        /// </summary>
        public static int EnsureWeaponsAboveFloor()
        {
            var actors = UnityEngine.Object.FindObjectsByType<CharacterController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            int fixedCount = 0;
            for (int i = 0; i < actors.Length; i++)
            {
                var actor = actors[i].transform;
                if (!actor.gameObject.activeInHierarchy || !GroundFit.BodyBounds(actor, out Bounds body))
                    continue;
                if (!GroundFit.SurfaceUnder(actor, body, out float floorY, out _))
                    continue;
                var rends = actor.GetComponentsInChildren<Renderer>(false);
                for (int r = 0; r < rends.Length; r++)
                {
                    if (rends[r] is ParticleSystemRenderer || !rends[r].enabled)
                        continue;
                    if (!GroundFit.IsGear(actor, rends[r].transform))
                        continue;
                    if (IsCapeName(rends[r].gameObject.name))
                        continue;
                    // 무기 루트 — 스케일을 여기서 만진다(렌더러가 자식 「Visual」인 경우가 있다).
                    var root = rends[r].transform;
                    while (root.parent != null && root.parent != actor && !IsWeaponName(root.name))
                        root = root.parent;
                    float below = floorY + 0.05f - rends[r].bounds.min.y;
                    if (below <= 0f)
                        continue;
                    // **축은 트랜스폼 원점이 아니라 그립**이다 — 원점으로 줄였더니 석궁 그립이 손에서
                    // 0.78m 떨어져 나갔다(무기-손 게이트가 잡았다). 그립을 제자리에 두고 길이만 줄인다.
                    if (!BossFit.WeaponAxis(root, out Vector3 grip, out _))
                    {
                        Debug.LogWarning("[Ulon] 무기 접지 보류 — " + actor.name + "/" + root.name + " 축을 못 읽었다(" +
                                         below.ToString("0.00") + "m 매몰). 억지로 옮기지 않는다.");
                        continue;
                    }
                    float drop = grip.y - rends[r].bounds.min.y;            // 그립에서 최저점까지
                    float want = grip.y - (floorY + 0.05f);
                    if (drop <= 0.01f || want <= 0f)
                    {
                        Debug.LogWarning("[Ulon] 무기 접지 보류 — " + actor.name + "/" + root.name +
                                         " 그립 자체가 바닥 높이다(축소로 못 푼다).");
                        continue;
                    }
                    float s = want / drop;
                    if (s < 0.5f)
                    {
                        Debug.LogWarning("[Ulon] 무기 접지 보류 — " + actor.name + "/" + root.name + "를 " +
                                         s.ToString("0.00") + "배로 줄여야 한다(§10.2 보스 무기가 너무 작아진다). 검수 판단 필요.");
                        continue;
                    }
                    root.localScale = root.localScale * s;
                    if (BossFit.WeaponAxis(root, out Vector3 grip2, out _))
                        root.position += grip - grip2;                      // 그립을 손에 되돌린다
                    fixedCount++;
                }
            }
            if (fixedCount > 0)
                Debug.Log("[Ulon] 무기 접지 — " + fixedCount + "개를 바닥 위로(그립 축 축소 후 남은 만큼만 이동)");
            return fixedCount;
        }

        public static int EnsureActorsOnSurface()
        {
            var actors = UnityEngine.Object.FindObjectsByType<CharacterController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            int moved = 0;
            float worst = 0f;
            string worstName = "";
            for (int i = 0; i < actors.Length; i++)
            {
                var t = actors[i].transform;
                // 재착지도 **몸 기준**이다 — 칼끝을 바닥에 대면 발이 뜬다.
                if (!t.gameObject.activeInHierarchy || !GroundFit.BodyBounds(t, out Bounds b))
                    continue;
                if (!GroundFit.SurfaceUnder(t, b, out float sy, out _))
                    continue;
                float dy = sy - b.min.y;
                if (Mathf.Abs(dy) < 0.02f)
                    continue;
                t.position += new Vector3(0f, dy, 0f);
                moved++;
                if (Mathf.Abs(dy) > Mathf.Abs(worst)) { worst = dy; worstName = t.name; }
            }
            if (moved > 0)
            {
                Physics.SyncTransforms();
                Debug.Log("[Ulon] 액터 재착지 — " + moved + "명(최대 " + worstName + " " + worst.ToString("0.00") + "m). " +
                          "크기를 바꾼 뒤에는 자리를 다시 잡아야 한다.");
            }
            return moved;
        }

        public static void StandOnRoomFloor(Vector3 roomCenter, params string[] names)
        {
            float floorTop = OnGround(roomCenter).y - DungeonDepth + 0.2f;
            for (int i = 0; i < names.Length; i++)
            {
                var go = GameObject.Find(names[i]);
                if (go == null)
                    continue;
                var p = go.transform.position;
                go.transform.position = new Vector3(p.x, floorTop, p.z);
            }
        }

        static void RoomSlab(Transform parent, string name, Vector3 center, Vector3 size, Material mat)
        {
            RoomSlab(parent, name, center, size, mat, 0f);
        }

        static void RoomSlab(Transform parent, string name, Vector3 center, Vector3 size, Material mat, float yaw)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            // 카메라와 플레이어 사이에 오면 렌더를 끄는 레이어(콜라이더는 남는다 — 하늘 차단·이동 막기는 유지).
            int blocker = LayerMask.NameToLayer(DungeonBlockerLayer);
            if (blocker >= 0)
                go.layer = blocker;
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = size;
            var rend = go.GetComponent<Renderer>();
            if (rend != null && mat != null)
                rend.sharedMaterial = mat;
        }

        /// <summary>던전 톤 잔해 — Kenney 흰 저폴리 바위는 회색 돌벽과 재질이 붕 뜬다(검수 P1).</summary>
        static void RoomRubble(Transform parent, Vector3 center, float scale)
        {
            var mat = MakeNoiseMat("DungeonWall", new Color(0.16f, 0.15f, 0.17f), new Color(0.27f, 0.26f, 0.28f));
            float y = OnGround(new Vector3(center.x, 0f, center.z)).y - DungeonDepth;
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f * Mathf.Deg2Rad;
                var pos = new Vector3(center.x + Mathf.Sin(a) * scale * 0.5f, y + 0.25f * scale, center.z + Mathf.Cos(a) * scale * 0.5f);
                RoomSlab(parent, "DungeonRubble", pos, new Vector3(0.7f * scale, 0.5f * scale, 0.7f * scale), mat, i * 37f);
            }
        }

        static void RoomTorch(Transform parent, Vector3 pos, float half)
        {
            var go = new GameObject("DungeonTorch");
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.78f, 0.48f);
            light.intensity = 2.4f;
            light.range = half * 2.6f;
            light.shadows = LightShadows.None;
        }

        /// <summary>실내 받침 광원 — 던전 분위기(따뜻한 저강도)로 방 전체를 약하게 채운다.</summary>
        static void RoomFill(Transform parent, Vector3 pos, float half)
        {
            var go = new GameObject("DungeonFill");
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.95f, 0.72f, 0.45f);
            light.intensity = 0.7f;
            light.range = half * 3.2f;
            light.shadows = LightShadows.None;
        }
    }
}
