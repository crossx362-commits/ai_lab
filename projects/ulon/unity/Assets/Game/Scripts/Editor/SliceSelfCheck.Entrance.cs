using System;
using System.Collections.Generic;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// 던전 입구가 "문"으로 읽히는지 강제한다(검수 2026-09-06 P0-2).
        /// 옛 입구는 얇은 회색 판때기 한 장이라 길가 돌무더기로 보였다 — 셀프체크는 게이트 컴포넌트만 봤다.
        /// </summary>
        const float EntranceRadius = 7f;
        const int EntranceLightMin = 2;

        // 검수 2026-09-06 반려: 흰 Kenney 바위가 던전 1 문구멍을 정면에서 가렸고,
        // 옆벽이 기둥과 높이가 어긋나 계단처럼 보였다. 존재가 아니라 **위치·정렬**을 잰다.
        const float EntranceRockClearRadius = 4f;
        const float WingTopGapMax = 0.5f;

        /// <summary>
        /// 문틀의 수평 최소 두께 하한(m). **실측 밴드에서 골랐다**(2026-09-07):
        /// 정상(돌기둥 2 + 낮은 돌기둥 옆벽 2 + 문구멍) 던전 1·2 = 1.38m, 던전 3 = 5.02m(45° 대각이라 AABB가 크다).
        /// 결함 쪽은 네거티브 컨트롤이 실제로 만든다 — 기둥·옆벽을 끄고 문구멍 판만 남기면 0.12m.
        /// 0.6m는 그 사이다(결함의 5배, 정상의 절반 아래). 여유를 양쪽에 남긴 값이다.
        /// </summary>
        const float EntranceFrameThickMin = 0.6f;

        /// <summary>
        /// 조각 하나의 높이 상한(m) — 하한만 두면 반대쪽으로 샌다(검수 「양쪽 한계」).
        /// 지금 조각 최대는 기둥 3.2m·옆벽 2.8m다. 5.0m를 넘으면 문이 아니라 탑이다.
        /// </summary>
        const float EntrancePieceHeightMax = 5.0f;

        static void CheckEntranceFinish(string label, float ex, float ez)
        {
            var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            float wingTop = float.MinValue;
            float pillarTop = float.MinValue;
            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t == null)
                    continue;
                var p = t.position;
                float d = new Vector2(p.x - ex, p.z - ez).magnitude;
                if (d > EntranceRockClearRadius)
                    continue;
                if (t.name.IndexOf("rock", StringComparison.OrdinalIgnoreCase) >= 0
                    && t.GetComponentInChildren<Renderer>(true) != null)
                    throw new InvalidOperationException(label + " 입구 " + d.ToString("0.0") + "m 앞에 바위(" + t.name + ")가 있습니다 — 문구멍을 가리고 흰 저폴리 바위는 돌벽과 재질이 붕 뜹니다(§8.1).");
                // 이름을 **접두사**로 본다 — 옛 게이트는 `== "EntranceWing"` 정확 일치라, 조각에 번호를
                // 붙이는 순간(EntranceWing1) 아무것도 못 재고 조용히 통과했다(2026-09-07 랩에서 실제로 겪음).
                var rend = t.GetComponentInChildren<Renderer>(false);
                if (rend == null)
                    continue;
                if (t.name.StartsWith("EntranceWing", StringComparison.Ordinal))
                    wingTop = Mathf.Max(wingTop, rend.bounds.max.y);
                if (t.name.StartsWith("EntrancePillar", StringComparison.Ordinal))
                    pillarTop = Mathf.Max(pillarTop, rend.bounds.max.y);
            }
            if (wingTop <= float.MinValue || pillarTop <= float.MinValue)
                throw new InvalidOperationException(label + " 입구에서 옆벽·기둥을 못 찾았습니다 — 잰 것이 없습니다(0이면 실패).");
            if (pillarTop - wingTop > WingTopGapMax)
                throw new InvalidOperationException(label + " 입구 옆벽 윗면이 기둥보다 " + (pillarTop - wingTop).ToString("0.00") + "m 낮습니다 — 최대 " + WingTopGapMax + "m. 「벽에 뚫린 문」이 아니라 계단처럼 보입니다(§8.1).");
        }

        static void AssertDungeonEntrance()
        {
            AssertDungeon3Leftover();

            CheckEntrance("던전 1", new Vector2(Dungeon1.EntranceX, Dungeon1.EntranceZ));
            CheckEntrance("던전 2", new Vector2(Dungeon2.EntranceX, Dungeon2.EntranceZ));
            CheckEntrance("던전 3", new Vector2(Dungeon3.EntranceX, Dungeon3.EntranceZ));

            CheckEntranceFinish("던전 1", Dungeon1.EntranceX, Dungeon1.EntranceZ);
            CheckEntranceFinish("던전 2", Dungeon2.EntranceX, Dungeon2.EntranceZ);
            CheckEntranceFinish("던전 3", Dungeon3.EntranceX, Dungeon3.EntranceZ);

            AssertEntrancePathPaint();

            AssertEntranceFrameNegativeControl();

            if (GameObject.Find(Dungeon3.SignObject) == null)
                throw new InvalidOperationException("던전 3 이정표가 없습니다: " + Dungeon3.SignObject + " (마을에서 찾을 단서가 없다)");

            Debug.Log("[Ulon] 던전 입구 단서 통과 — 등불 " + EntranceLightMin + "개↑·깃발·문 앞 돌포장 " + EntrancePaintMin + "↑(옆 12m " + EntrancePaintOffMax + "↓)·바위 " + EntranceRockClearRadius + "m 안 없음·옆벽 정렬 (던전 1·2·3) + 던전 3 이정표");
        }

        /// <summary>
        /// **입구 앞 길이 땅에 칠해져 있는가**(옛 「진입로 타일 3장」 게이트를 갈아 끼웠다, 2026-09-09).
        ///
        /// 옛 자는 **판때기 개수**를 셌다 — 그래서 잔디 위에 놓인 갈색 깔개 넷도 통과했다(검수 관찰).
        /// 길을 지형 도포로 옮겼으니 자도 **칠해진 결과**(알파맵)를 읽는다. 규칙(`EntrancePathAt`)이
        /// 아니라 구운 것을 보는 이유는 「저장됐다 ≠ 반영됐다」다 — 규칙을 고치고 굽지 않으면 화면은 그대로다.
        ///
        /// **NC는 옆에 둔다**: 문 앞 6m는 칠해져야 하고, 같은 거리에서 **옆으로 12m** 비킨 자리는
        /// 칠해지면 안 된다. 이게 없으면 「온 세상을 돌포장으로 칠하면 통과」가 된다.
        /// </summary>
        const float EntrancePaintMin = 0.50f;      // 문 앞 표본의 돌포장 평균 하한
        const float EntrancePaintOffMax = 0.15f;   // 옆으로 비킨 자리(NC)의 상한

        static void AssertEntrancePathPaint()
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null || terrain.terrainData == null)
                throw new InvalidOperationException("지형이 없습니다 — 입구 앞 길 도포를 검사할 수 없습니다.");
            var data = terrain.terrainData;
            int ar = data.alphamapResolution;
            var alpha = data.GetAlphamaps(0, 0, ar, ar);
            var doors = new[]
            {
                ("던전 1", Dungeon1.EntranceX, Dungeon1.EntranceZ, Dungeon1.EntranceYaw),
                ("던전 2", Dungeon2.EntranceX, Dungeon2.EntranceZ, Dungeon2.EntranceYaw),
                ("던전 3", Dungeon3.EntranceX, Dungeon3.EntranceZ, Dungeon3.EntranceYaw),
            };
            for (int i = 0; i < doors.Length; i++)
            {
                // 진입로 쪽은 **원장 하나**에서 온다(`EntranceGeom.Front`) — 옛 `-Heading(yaw)` 정의는
                // 이름만 「반대쪽」이라 세 입구 모두 문 뒤를 가리켰다(2026-09-09, 언덕 위 길 띠로 드러남).
                var approachDir = Ulon.Shared.EntranceGeom.Front(doors[i].Item2, doors[i].Item3, doors[i].Item4);
                float ax = approachDir.x, az = approachDir.y;
                float sx = az, sz = -ax;                            // 그 옆
                float on = 0f, off = 0f;
                int n = 0;
                for (float s = 1f; s <= 6f; s += 1f)
                    for (float t = -1f; t <= 1f; t += 1f)
                    {
                        float wx = doors[i].Item2 + ax * s + sx * t;
                        float wz = doors[i].Item3 + az * s + sz * t;
                        on += Sample(alpha, ar, wx, wz, WorldSplat.Cobble);
                        off += Sample(alpha, ar, wx + sx * 12f, wz + sz * 12f, WorldSplat.Cobble);
                        n++;
                    }
                on /= n; off /= n;
                Debug.Log("[Ulon] 입구 앞 길 도포 " + doors[i].Item1 + " — 문 앞 " + on.ToString("0.00") +
                          "(하한 " + EntrancePaintMin + ") · 옆 12m " + off.ToString("0.00") +
                          "(상한 " + EntrancePaintOffMax + ")");
                if (on < EntrancePaintMin)
                    throw new InvalidOperationException(doors[i].Item1 + " 입구 앞 돌포장이 " + on.ToString("0.00") +
                        "입니다 — 최소 " + EntrancePaintMin + ". 어디로 들어가는지 안 보입니다.");
                if (off > EntrancePaintOffMax)
                    throw new InvalidOperationException(doors[i].Item1 + " 입구에서 옆으로 12m 비킨 자리도 돌포장 " +
                        off.ToString("0.00") + "입니다 — 상한 " + EntrancePaintOffMax +
                        ". 길이 아니라 온 바닥이 칠해졌습니다(자가 아무것도 안 재는 상태).");
            }
        }

        /// <summary>
        /// **문구멍이 기둥 사이에 있는가**(검수 2026-09-07 반려: 조각은 등록 메시로 바뀌었는데 화면은
        /// 「돌기둥 넷이 흩어져 선 모습」이었다). 두께만 재면 조각이 흩어져 있어도 통과한다 —
        /// 또 한 축만 재는 것이다. 입구가 바라보는 방향의 **가로축(right)** 위에서:
        ///   ① 기둥 둘이 문구멍의 **반대쪽**에 하나씩 있어야 하고,
        ///   ② 문구멍이 두 기둥의 가운데에서 좌우로 크게 벗어나면 안 된다.
        /// </summary>
        const float PortalOffCenterMax = 0.35f;

        static void CheckPortalBetweenPillars(string label, Vector2 pos)
        {
            float yaw = 0f;
            bool found = false;
            for (int s = 0; s < VisualSliceBuilder.EntranceSpots.Length; s++)
            {
                var spot = VisualSliceBuilder.EntranceSpots[s];
                if (new Vector2(spot.X - pos.x, spot.Z - pos.y).magnitude < 1f)
                {
                    yaw = spot.Yaw;
                    found = true;
                }
            }
            if (!found)
                throw new InvalidOperationException(label + " 입구 좌표가 원장(EntranceSpots)에 없습니다 — 잰 것이 없습니다.");
            float rad = yaw * Mathf.Deg2Rad;
            var right = new Vector2(Mathf.Cos(rad), -Mathf.Sin(rad));   // fwd=(sin,cos)의 오른쪽

            float Lateral(Vector3 p) => Vector2.Dot(new Vector2(p.x, p.z) - pos, right);

            var pillars = new List<float>();
            float portalAt = float.NaN;
            var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t == null || t.parent == null || t.parent.name != VisualSliceBuilder.EntranceFrameObject)
                    continue;
                var rend = t.GetComponentInChildren<Renderer>(false);
                if (rend == null)
                    continue;
                if (Vector2.Distance(new Vector2(rend.bounds.center.x, rend.bounds.center.z), pos) > EntranceRadius)
                    continue;
                if (t.name.StartsWith("EntrancePillar", StringComparison.Ordinal))
                    pillars.Add(Lateral(rend.bounds.center));
                else if (t.name.StartsWith(VisualSliceBuilder.EntrancePortalObject, StringComparison.Ordinal))
                    portalAt = Lateral(rend.bounds.center);
            }
            if (pillars.Count < 2 || float.IsNaN(portalAt))
                throw new InvalidOperationException(label + " 입구에서 기둥 2개와 문구멍을 못 찾았습니다(기둥 " + pillars.Count +
                    ") — 잰 것이 없습니다(0이면 실패).");
            pillars.Sort();
            float leftMost = pillars[0];
            float rightMost = pillars[pillars.Count - 1];
            if (!(leftMost < portalAt && portalAt < rightMost))
                throw new InvalidOperationException(label + " 문구멍이 기둥 사이에 없습니다 — 기둥 " +
                    leftMost.ToString("0.00") + "m·" + rightMost.ToString("0.00") + "m, 문구멍 " + portalAt.ToString("0.00") +
                    "m(입구 가로축). 기둥이 문을 끼고 서야 「문」으로 읽힌다(§8.1).");
            float off = Mathf.Abs(portalAt - (leftMost + rightMost) * 0.5f);
            if (off > PortalOffCenterMax)
                throw new InvalidOperationException(label + " 문구멍이 기둥 가운데에서 " + off.ToString("0.00") +
                    "m 치우쳤습니다 — 상한 " + PortalOffCenterMax + "m. 문이 아니라 흩어진 돌기둥으로 보입니다(§8.1).");
            // 상인방 노릇을 하기로 한 아치(DungeonGate 오브젝트)가 **실제로 어디에 얼마만 하게** 서 있는지
            // 찍는다 — 검수가 「두 샷 어디에도 아치가 안 보인다」고 물었다. 판정이 아니라 진단이다.
            var gates = UnityEngine.Object.FindObjectsByType<Ulon.Server.DungeonGate>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < gates.Length; i++)
            {
                var r = gates[i].GetComponentInChildren<Renderer>(false);
                if (r == null || Vector2.Distance(new Vector2(r.bounds.center.x, r.bounds.center.z), pos) > EntranceRadius)
                    continue;
                Debug.Log("[Ulon] 입구 아치 진단 " + label + " — " + gates[i].name + " 크기 " + r.bounds.size.ToString("0.00") +
                          ", 가로축 " + Lateral(r.bounds.center).ToString("0.00") + "m, 윗면 " +
                          (r.bounds.max.y - r.bounds.min.y).ToString("0.00") + "m 높이");
            }
            Debug.Log("[Ulon] 입구 문구멍 위치 " + label + " — 기둥 " + leftMost.ToString("0.00") + "m·" +
                      rightMost.ToString("0.00") + "m 사이, 중앙에서 " + off.ToString("0.00") + "m(상한 " + PortalOffCenterMax + "m)");
        }

        /// <summary>
        /// 네거티브 컨트롤 — **결함을 실제로 만든다**. 기둥·옆벽을 꺼서 「아치 뒤 문구멍 판 한 장」으로
        /// 되돌리면(옛 입구가 정확히 그랬다) 두께 게이트가 빨간불이어야 한다. 끝나면 되살린다.
        /// </summary>
        static void AssertEntranceFrameNegativeControl()
        {
            // 결함을 만드는 문틀과 재는 입구가 **같은 던전이어야** 한다 — 아무 문틀이나 끄면
            // 다른 입구를 재게 되어 NC가 조용히 「통과」한다.
            var frames = EntranceFrames();
            Transform target = null;
            for (int i = 0; i < frames.Count; i++)
                if (frames[i].parent != null && frames[i].parent.name == Dungeon1.RootObject)
                    target = frames[i];
            if (target == null)
                throw new InvalidOperationException("던전 1 입구 문틀이 없어 두께 네거티브 컨트롤을 할 수 없습니다.");
            var off = new List<GameObject>();
            for (int c = 0; c < target.childCount; c++)
            {
                var child = target.GetChild(c);
                if (child.name.StartsWith(VisualSliceBuilder.EntrancePortalObject, StringComparison.Ordinal))
                    continue;
                if (!child.gameObject.activeSelf)
                    continue;
                child.gameObject.SetActive(false);
                off.Add(child.gameObject);
            }
            if (off.Count == 0)
                throw new InvalidOperationException("문틀에서 끌 조각이 없습니다 — 네거티브 컨트롤이 결함을 못 만듭니다.");
            bool red = false;
            try
            {
                try { CheckEntrance("던전 1", new Vector2(Dungeon1.EntranceX, Dungeon1.EntranceZ)); }
                catch (InvalidOperationException) { red = true; }
            }
            finally
            {
                for (int i = 0; i < off.Count; i++)
                    off[i].SetActive(true);
            }
            if (!red)
                throw new InvalidOperationException("입구 문틀 두께 네거티브 컨트롤 실패 — 기둥·옆벽을 다 꺼 판 한 장만 남겼는데도 통과했습니다.");
            Debug.Log("[Ulon] 입구 문틀 두께 네거티브 컨트롤 통과 — 조각 " + off.Count + "개를 끄면(판 한 장) FAIL");

            // 문구멍을 **실제로 1m 옆으로 옮겨** 「기둥 사이」 판정이 반응하는지 본다(검수 지시).
            Transform portal = null;
            for (int c = 0; c < target.childCount; c++)
                if (target.GetChild(c).name.StartsWith(VisualSliceBuilder.EntrancePortalObject, StringComparison.Ordinal))
                    portal = target.GetChild(c);
            if (portal == null)
                throw new InvalidOperationException("문구멍이 없어 위치 네거티브 컨트롤을 할 수 없습니다.");
            var was = portal.position;
            float radNc = 90f * Mathf.Deg2Rad;                                  // 던전 1 접근 방향
            var rightNc = new Vector3(Mathf.Cos(radNc), 0f, -Mathf.Sin(radNc));
            portal.position = was + rightNc * 1.5f;
            bool movedRed = false;
            try
            {
                try { CheckEntrance("던전 1", new Vector2(Dungeon1.EntranceX, Dungeon1.EntranceZ)); }
                catch (InvalidOperationException) { movedRed = true; }
            }
            finally { portal.position = was; }
            if (!movedRed)
                throw new InvalidOperationException("문구멍 위치 네거티브 컨트롤 실패 — 문구멍을 1.5m 옆으로 옮겼는데도 통과했습니다.");
            Debug.Log("[Ulon] 입구 문구멍 위치 네거티브 컨트롤 통과 — 문구멍을 1.5m 옆으로 옮기면 FAIL");
        }

        static void CheckEntrance(string label, Vector2 pos)
        {
            int lights = 0;
            var allLights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < allLights.Length; i++)
            {
                if (allLights[i].type != LightType.Point)
                    continue;
                var p = allLights[i].transform.position;
                if (Vector2.Distance(new Vector2(p.x, p.z), pos) <= EntranceRadius)
                    lights++;
            }
            if (lights < EntranceLightMin)
                throw new InvalidOperationException(label + " 입구 조명이 " + lights + "개입니다 — 최소 " + EntranceLightMin + "개(양옆 등불). 입구로 안 읽힙니다.");

            bool banner = false;
            var rends = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < rends.Length; i++)
            {
                var p = rends[i].transform.position;
                if (Vector2.Distance(new Vector2(p.x, p.z), pos) > EntranceRadius)
                    continue;
                string n = AncestorNames(rends[i].transform);
                if (n.IndexOf("banner", StringComparison.OrdinalIgnoreCase) >= 0)
                    banner = true;
            }
            // **깃발은 더 이상 요구하지 않는다**(검수 판정 2026-09-09, 배너를 걷었다).
            // `banner-red`는 원점이 장대 밑이고 천이 옆으로 뻗는 **벽걸이** 소품이라, 자유 자리로 세우면
            // 화면에서 「공중에 뜬 천」이고, 기둥 표면에 붙이면 **문구멍을 15% 가리면서도 기둥과의
            // 화면 겹침은 0%**였다(자리 81칸 + 매달기 한 판을 다 재고 내린 결론).
            // 표식은 아치·어두운 포털·등불·돌길이 대신한다 — 그 넷은 각자 게이트가 있다.
            if (banner)
                Debug.Log("[Ulon] " + label + " 입구에 배너가 남아 있습니다(걷는 패스가 안 돌았는지 보십시오)");

            // 「판때기가 아니다」는 **조각 개수가 아니라 두께**다(검수 2026-09-07 랩).
            // 옛 게이트는 「기둥 2 + 상인당 = 3개」를 셌다 — 조각 수는 대리 지표라, 검은 큐브 3개도
            // 만점이었고 진짜 입체 조각으로 바꾸면(상인방이 아치로 흡수) 멀쩡한데 빨간불이 났다.
            // 잰다: 문틀 전체의 **수평 최소 두께**(회전과 무관하다 — 얇은 판은 한 축이 반드시 얇다).
            bool portal = false;
            bool hasFrame = false;
            var box = new Bounds();
            for (int i = 0; i < rends.Length; i++)
            {
                var p = rends[i].transform.position;
                if (Vector2.Distance(new Vector2(p.x, p.z), pos) > EntranceRadius)
                    continue;
                string n = AncestorNames(rends[i].transform);
                if (n.IndexOf(VisualSliceBuilder.EntranceFrameObject, StringComparison.Ordinal) < 0)
                    continue;
                if (!hasFrame) { box = rends[i].bounds; hasFrame = true; }
                else box.Encapsulate(rends[i].bounds);
                float h = rends[i].bounds.size.y;
                if (h > EntrancePieceHeightMax)
                    throw new InvalidOperationException(label + " 입구 조각 " + rends[i].transform.parent.name + " 높이가 " +
                        h.ToString("0.00") + "m입니다 — 상한 " + EntrancePieceHeightMax + "m. 문이 아니라 탑으로 보입니다(§8.1).");
                if (n.IndexOf(VisualSliceBuilder.EntrancePortalObject, StringComparison.Ordinal) >= 0)
                    portal = true;
            }
            if (!hasFrame)
                throw new InvalidOperationException(label + " 입구에 문틀이 없습니다 — 잰 것이 없습니다(0이면 실패).");
            float thick = Mathf.Min(box.size.x, box.size.z);
            if (thick < EntranceFrameThickMin)
                throw new InvalidOperationException(label + " 입구 문틀 두께가 " + thick.ToString("0.00") + "m입니다 — 하한 " +
                    EntranceFrameThickMin + "m. 얇은 판때기 한 장은 길가 표지판으로 보입니다(§8.1).");
            if (!portal)
                throw new InvalidOperationException(label + " 입구에 어두운 문구멍이 없습니다 — 들어가는 곳으로 안 읽힙니다.");
            CheckPortalBetweenPillars(label, pos);
            Debug.Log("[Ulon] 입구 문틀 " + label + " — 수평 최소 두께 " + thick.ToString("0.00") + "m(하한 " + EntranceFrameThickMin + "m)");
        }
        // Kenney 프리팹은 메시 자식 이름이 전부 "Visual"이다 — 조상 이름까지 이어 붙여야 종류를 안다.
        static string AncestorNames(Transform t)
        {
            var sb = new System.Text.StringBuilder();
            for (var cur = t; cur != null; cur = cur.parent)
                sb.Append(cur.name).Append('/');
            return sb.ToString();
        }
    }
}
