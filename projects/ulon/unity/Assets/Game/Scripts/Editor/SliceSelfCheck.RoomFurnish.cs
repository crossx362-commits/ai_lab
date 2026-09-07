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
        // 실측(2026-09-06): 검수가 「바위 물량 = 채석장」이라며 반려해 소품을 44+16개에서 24개로 줄인 뒤
        // 채운 상태 0.50, 소품을 전부 치운 결함 상태 0.65. 상한은 그 중간인 0.57.
        // **이 지표의 폭은 원래 좁다** — 방을 아무리 채워도 내려다보는 화면의 절반은 바닥이다.
        // 그래서 이 게이트는 이제 「빈 방 경보」로만 쓰고, 화면 품질 판정은 소품 자격·분포·종류 편중
        // 게이트가 맡는다. 물량으로 이 수치를 낮추면 화면이 나빠진다(그게 이번 반려였다).
        const float BareFloorShareMax = 0.57f;
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

        /// <summary>중앙 전투 공간 반경 — 이 안에 서 있는 소품이 있으면 플레이어 동선을 막는다(검수 요구).</summary>
        const float CombatClearRadius = 2.5f;

        /// <summary>소품 발과 방 바닥 윗면의 허용 오차 — 뜨면 그림자가 어긋나고 묻히면 사라진다(실측 0.00).</summary>
        const float PropFootErrorMax = 0.05f;

        /// <summary>
        /// **분포**를 잰다(검수 2026-09-06: 개수 하한은 대리 지표다 — 한구석에 쌓아도 통과한다).
        /// 사분면마다 서 있는 소품 1개 이상 + 중앙 반경 2.5m 안은 비어 있을 것 + 발이 방 바닥에 붙어 있을 것.
        /// </summary>
        static void AssertPropDistribution()
        {
            CheckDistribution("던전 1", new Vector2(Dungeon1.InteriorX, Dungeon1.InteriorZ), Dungeon1.InteriorObject);
            CheckDistribution("던전 2", new Vector2(Dungeon2.InteriorX, Dungeon2.InteriorZ), Dungeon2.InteriorObject);
            CheckDistribution("던전 3", new Vector2(Dungeon3.InteriorX, Dungeon3.InteriorZ), Dungeon3.InteriorObject);
            Debug.Log("[Ulon] 실내 소품 분포 통과 — 사분면마다 1개 이상, 중앙 반경 " + CombatClearRadius + "m 비어 있음");
        }

        /// <summary>방 바닥 판의 **실측** 윗면 — 공식(지표−깊이+두께)이 아니라 실제 오브젝트 바운드를 읽는다.</summary>
        static bool RoomFloorTopMeasured(GameObject interior, out float y)
        {
            y = 0f;
            bool any = false;
            var rends = interior.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                if (!rends[i].gameObject.name.StartsWith("DungeonFloor", StringComparison.Ordinal))
                    continue;
                float top = rends[i].bounds.max.y;
                if (!any || top > y) { y = top; any = true; }
            }
            return any;
        }

        static void CheckDistribution(string label, Vector2 center, string interiorObject)
        {
            var interior = GameObject.Find(interiorObject);
            if (interior == null)
                throw new InvalidOperationException(interiorObject + "이(가) 없습니다.");
            var quad = new int[4];
            foreach (var t in PropNodes(interior))
            {
                float dx = t.position.x - center.x;
                float dz = t.position.z - center.y;
                float r = Mathf.Sqrt(dx * dx + dz * dz);
                // 전역 발 높이 게이트(`AssertFootOnGround`)는 지하 방 구조물을 건너뛴다(지표 기준이라 못 잰다).
                // 그러니 **방 안에서는 여기가 그 역할**을 한다 — 소품 발이 방 바닥 윗면에 붙어 있는가.
                // 벽에 거는 것(횃불·등불)은 바닥에 발이 없다 — 이름으로 빼되, 나머지는 전부 잰다.
                bool wallMounted = t.name.StartsWith("DungeonFurnTorch", StringComparison.Ordinal)
                    || t.name.StartsWith("DungeonFurnLantern", StringComparison.Ordinal);
                if (!wallMounted && GroundFit.WorldBounds(t, out Bounds wb))
                {
                    // **공식이 아니라 실물 바닥 판**을 잰다(검수 승인 2026-09-07) —
                    // 공식과 실물이 「지금은 일치」해도 자가 둘이면 언젠가 갈린다(방 안 몹 게이트에서 겪었다).
                    // 액터처럼 「발 밑 광선」을 쓰면 **겹쳐 놓인 소품**이 먼저 맞는다(첫 시도에서 잔해가
                    // 이웃 소품 아래로 들어가 −0.79m로 읽혔다) — 방 안에서는 바닥 판 자체를 대상으로 삼는다.
                    if (!RoomFloorTopMeasured(interior, out float floorTop))
                        throw new InvalidOperationException(label + "의 바닥 판(DungeonFloor)을 못 찾았습니다 — 못 잰 것을 통과로 적지 않는다.");
                    string under = "DungeonFloor";
                    float dy = wb.min.y - floorTop;
                    if (Mathf.Abs(dy) > PropFootErrorMax)
                        throw new InvalidOperationException(label + "의 소품 " + t.name + " 발이 방 바닥에서 " +
                            dy.ToString("0.00") + "m " + (dy > 0f ? "떠" : "묻혀") + " 있습니다 — 허용 " + PropFootErrorMax + "m(밑에 닿은 것: " + under + ").");
                }
                if (r < CombatClearRadius)
                    throw new InvalidOperationException(label + "의 소품 " + t.name + "이(가) 중앙에서 " +
                        r.ToString("0.0") + "m입니다 — 전투 공간 반경 " + CombatClearRadius + "m 안에는 소품을 두지 않는다(검수 요구).");
                quad[(dx >= 0f ? 0 : 2) + (dz >= 0f ? 0 : 1)]++;
            }
            for (int q = 0; q < 4; q++)
                if (quad[q] == 0)
                    throw new InvalidOperationException(label + "의 " + q + "번 사분면에 소품이 없습니다 — " +
                        "개수가 아니라 **분포**로 채워야 한다(한구석에 쌓으면 화면 절반이 빈 방이다).");
            Debug.Log("[Ulon] 실내 소품 분포 " + label + " 사분면 " + quad[0] + "/" + quad[1] + "/" + quad[2] + "/" + quad[3]);
        }

        /// <summary>네거티브 컨트롤 — 소품을 한 사분면으로 몰면 빨간불이어야 한다.</summary>
        static void AssertPropDistributionNegativeControl()
        {
            var interior = GameObject.Find(Dungeon1.InteriorObject);
            if (interior == null)
                throw new InvalidOperationException("던전 1 실내가 없어 분포 네거티브 컨트롤을 할 수 없습니다.");
            var center = new Vector2(Dungeon1.InteriorX, Dungeon1.InteriorZ);
            var moved = new List<Transform>();
            var saved = new List<Vector3>();
            bool red = false;
            try
            {
                foreach (var t in PropNodes(interior))
                {
                    moved.Add(t);
                    saved.Add(t.position);
                    // 실제로 한구석(북동 사분면)에 몰아 쌓는다
                    t.position = new Vector3(center.x + 5f, t.position.y, center.y + 5f);
                }
                Physics.SyncTransforms();
                try { AssertPropDistribution(); }
                catch (InvalidOperationException) { red = true; }
            }
            finally
            {
                for (int i = 0; i < moved.Count; i++)
                    moved[i].position = saved[i];
                Physics.SyncTransforms();
            }
            if (!red)
                throw new InvalidOperationException("소품 분포 네거티브 컨트롤 실패 — 한구석에 몰아 쌓았는데도 통과했습니다.");
            Debug.Log("[Ulon] 실내 소품 분포 네거티브 컨트롤 통과 — 한 사분면에 몰면 FAIL");

            // 발 붙음 네거티브 컨트롤 — 소품 하나를 실제로 1m 띄운다.
            Transform lift = null;
            foreach (var t in PropNodes(interior))
                if (t.name.StartsWith("DungeonFurnLoad", StringComparison.Ordinal)) { lift = t; break; }
            if (lift == null)
                throw new InvalidOperationException("띄울 소품이 없습니다.");
            var keep = lift.position;
            bool footRed = false;
            try
            {
                lift.position = keep + Vector3.up * 1f;
                Physics.SyncTransforms();
                try { AssertPropDistribution(); }
                catch (InvalidOperationException) { footRed = true; }
            }
            finally
            {
                lift.position = keep;
                Physics.SyncTransforms();
            }
            if (!footRed)
                throw new InvalidOperationException("소품 발 붙음 네거티브 컨트롤 실패 — 소품을 1m 띄웠는데도 통과했습니다.");
            Debug.Log("[Ulon] 실내 소품 발 붙음 네거티브 컨트롤 통과 — 1m 띄우면 FAIL");
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
            var tally = new Dictionary<string, int>(StringComparer.Ordinal);   // 무엇이 화면을 차지하는가(진단)
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
                            // 페이드는 끄지 않고 비치게 한다(검수 2026-09-07) — 둘 다 화면을 안 가린다.
                            if (rend != null && (!rend.enabled || Ulon.Client.DungeonSightFade.IsGhosted(rend)))
                                continue;                       // 페이드로 꺼진 것은 화면에 없다
                            if (col.name.StartsWith("DungeonFloor", StringComparison.Ordinal))
                                bare++;
                            string key = RootTag(col.transform);
                            tally[key] = (tally.TryGetValue(key, out int had) ? had : 0) + 1;
                            break;                              // 첫 번째로 실제 보이는 것만 센다
                        }
                    }
                }
            }
            finally
            {
                Ulon.Client.DungeonSightFade.Restore(hidden);
            }
            // 계측이 「무엇에 막혀 있는지」를 남긴다 — 수치만 남기면 왜 안 내려가는지 알 수 없다(2026-09-06 교훈).
            var top = new List<KeyValuePair<string, int>>(tally);
            top.Sort((a, b) => b.Value.CompareTo(a.Value));
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < top.Count && i < 5; i++)
                sb.Append(top[i].Key).Append('=').Append(top[i].Value).Append(' ');
            Debug.Log("[Ulon] 실내 채움 화면 분포 — " + sb + "(광선 " + total + ")");
            return bare / (float)total;
        }

        /// <summary>화면을 차지한 것의 정체 — 방 자식 이름(소품/바닥/벽)까지 거슬러 올라간다.</summary>
        static string RootTag(Transform t)
        {
            var cur = t;
            while (cur != null)
            {
                if (cur.name.StartsWith("Dungeon", StringComparison.Ordinal) || cur.name.StartsWith("Hostile", StringComparison.Ordinal))
                    return cur.name;
                cur = cur.parent;
            }
            return t.name;
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
