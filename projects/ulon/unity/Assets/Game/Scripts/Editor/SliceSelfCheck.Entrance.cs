using System;
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
                var rend = t.GetComponent<Renderer>();
                if (rend == null)
                    continue;
                if (t.name == "EntranceWing")
                    wingTop = Mathf.Max(wingTop, rend.bounds.max.y);
                if (t.name == "EntrancePillar")
                    pillarTop = Mathf.Max(pillarTop, rend.bounds.max.y);
            }
            if (wingTop > float.MinValue && pillarTop > float.MinValue && pillarTop - wingTop > WingTopGapMax)
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

            if (GameObject.Find(Dungeon3.SignObject) == null)
                throw new InvalidOperationException("던전 3 이정표가 없습니다: " + Dungeon3.SignObject + " (마을에서 찾을 단서가 없다)");

            Debug.Log("[Ulon] 던전 입구 단서 통과 — 등불 " + EntranceLightMin + "개↑·깃발·진입로 타일 " + EntrancePathMin + "장↑·바위 " + EntranceRockClearRadius + "m 안 없음·옆벽 정렬 (던전 1·2·3) + 던전 3 이정표");
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

            int frame = 0;
            bool portal = false;
            for (int i = 0; i < rends.Length; i++)
            {
                var p = rends[i].transform.position;
                if (Vector2.Distance(new Vector2(p.x, p.z), pos) > EntranceRadius)
                    continue;
                string n = AncestorNames(rends[i].transform);
                if (n.IndexOf("EntrancePillar", StringComparison.Ordinal) >= 0 || n.IndexOf("EntranceLintel", StringComparison.Ordinal) >= 0)
                    frame++;
                if (n.IndexOf("EntrancePortal", StringComparison.Ordinal) >= 0)
                    portal = true;
            }
            if (frame < 3)
                throw new InvalidOperationException(label + " 입구 문틀(기둥 2 + 상인방)이 " + frame + "개입니다 — 아치 한 장은 얇은 판때기로 보입니다.");
            if (!portal)
                throw new InvalidOperationException(label + " 입구에 어두운 문구멍이 없습니다 — 들어가는 곳으로 안 읽힙니다.");
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
