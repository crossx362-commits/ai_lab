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

        static void AssertDungeonEntrance()
        {
            AssertDungeon3Leftover();

            CheckEntrance("던전 1", new Vector2(Dungeon1.EntranceX, Dungeon1.EntranceZ));
            CheckEntrance("던전 2", new Vector2(Dungeon2.EntranceX, Dungeon2.EntranceZ));
            CheckEntrance("던전 3", new Vector2(Dungeon3.EntranceX, Dungeon3.EntranceZ));

            if (GameObject.Find(Dungeon3.SignObject) == null)
                throw new InvalidOperationException("던전 3 이정표가 없습니다: " + Dungeon3.SignObject + " (마을에서 찾을 단서가 없다)");

            Debug.Log("[Ulon] 던전 입구 단서 통과 — 등불 " + EntranceLightMin + "개↑·깃발·진입로 타일 " + EntrancePathMin + "장↑ (던전 1·2·3) + 던전 3 이정표");
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
