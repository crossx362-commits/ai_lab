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
        const int EntrancePathMin = 3;

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

            AssertEntranceFrameNegativeControl();

            if (GameObject.Find(Dungeon3.SignObject) == null)
                throw new InvalidOperationException("던전 3 이정표가 없습니다: " + Dungeon3.SignObject + " (마을에서 찾을 단서가 없다)");

            Debug.Log("[Ulon] 던전 입구 단서 통과 — 등불 " + EntranceLightMin + "개↑·깃발·진입로 타일 " + EntrancePathMin + "장↑·바위 " + EntranceRockClearRadius + "m 안 없음·옆벽 정렬 (던전 1·2·3) + 던전 3 이정표");
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

            int path = 0;
            bool banner = false;
            var rends = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < rends.Length; i++)
            {
                var p = rends[i].transform.position;
                if (Vector2.Distance(new Vector2(p.x, p.z), pos) > EntranceRadius)
                    continue;
                string n = AncestorNames(rends[i].transform);
                if (n.IndexOf("path", StringComparison.OrdinalIgnoreCase) >= 0)
                    path++;
                if (n.IndexOf("banner", StringComparison.OrdinalIgnoreCase) >= 0)
                    banner = true;
            }
            if (path < EntrancePathMin)
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < rends.Length; i++)
                {
                    var p2 = rends[i].transform.position;
                    if (Vector2.Distance(new Vector2(p2.x, p2.z), pos) > EntranceRadius)
                        continue;
                    sb.Append(AncestorNames(rends[i].transform)).Append(" | ");
                }
                Debug.LogError("[Ulon] 입구 진단 " + label + ": " + sb);
            }
            if (path < EntrancePathMin)
                throw new InvalidOperationException(label + " 입구 진입로 타일이 " + path + "장입니다 — 최소 " + EntrancePathMin + "장. 어디로 들어가는지 안 보입니다.");
            if (!banner)
                throw new InvalidOperationException(label + " 입구에 깃발 표식이 없습니다.");

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
