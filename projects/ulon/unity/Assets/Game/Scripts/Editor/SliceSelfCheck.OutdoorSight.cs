using System;
using System.Collections.Generic;
using Ulon.Client;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **야외에서도 플레이어가 보이는가**(검수 랩 D, 사각지대 표 ④).
    ///
    /// ㉞의 시선 차폐 게이트는 **던전 방 중앙 한 점**만 잰다. 그런데 카메라 각도는 고정(요 45°)이고
    /// 마을에는 집·좌판·나무가 카메라 쪽에 서 있다 — **집 뒤에 서면 화면에서 사라진다.**
    /// 실내는 `DungeonSightFade`가 걷어 주지만 그건 던전 차폐물 레이어만 본다.
    ///
    /// 그래서 마을·숲을 **격자로 걸어 다니며** 설 수 있는 자리마다 몸통 가려짐을 잰다.
    /// 판정은 실내와 **같은 함수**(`OccludedShareAt`)로 하고, 런타임 페이드도 같이 적용한다.
    /// 축은 **둘**이다 — 평균(가려지는 자리 비율)과 최악(한 자리 최대 가림). 평균만 보면 한 자리가
    /// 20%여도 통과한다.
    ///
    /// **한계 두 가지**(다음 사람이 「야외 차폐는 검증된다」고 읽지 않게):
    /// ① 콜라이더가 있는 것만 잡는다 — 콜라이더 없는 장식이 화면을 가리면 이 게이트는 못 본다(샷으로 볼 몫이다).
    /// ② 남은 평균값의 정체는 **몹**이다(실측 Bandit). 건물 문제가 아니다 — 사람·짐승은 페이드하지 않는다.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        /// <summary>한 자리에서 몸통이 이만큼 넘게 막히면 「그 자리에선 안 보인다」로 센다(실내 기준과 같다).</summary>
        const float OutdoorOccludedMax = 0.10f;
        /// <summary>안 보이는 자리 **비율**의 상한(평균 축) — 결함 2.5% / 고친 값 1.4% 사이가 아니라,
        /// 소품 하나 추가로 빨간불이 나지 않게 고친 값 위에 둔다(검수 지시: 0%를 상한으로 박지 마라).</summary>
        const float OutdoorBlindSpotMax = 0.02f;
        /// <summary>**최악 자리** 상한 — 평균만 보면 한 자리가 20%여도 통과한다(검수 지시).
        /// 다만 **몹은 빼고** 잰다: 사람·짐승은 움직이므로 「구조적으로 안 보이는 자리」가 아니다
        /// (실측 남은 최악 20%의 정체가 Bandit이다). 정적 물체가 이만큼 가리면 그건 배치 결함이다.</summary>
        const float OutdoorWorstStaticMax = 0.10f;

        struct SightArea
        {
            public string Label;
            public float X, Z;
            public float Half;     // 격자 반경(m)
            public float Step;     // 격자 간격(m)
        }

        static SightArea[] SightAreas()
        {
            return new[]
            {
                new SightArea { Label = "마을", X = 0f, Z = 0f, Half = 16f, Step = 2f },
                new SightArea { Label = "숲", X = WorldRegions.Forest.X, Z = WorldRegions.Forest.Z, Half = 14f, Step = 2f },
                new SightArea { Label = "농경지", X = WorldRegions.Meadow.X, Z = WorldRegions.Meadow.Z, Half = 14f, Step = 2f },
            };
        }

        /// <summary>사람이 실제로 설 수 있는 자리인가 — 구조물 안은 표본이 아니다.</summary>
        static bool CanStand(Vector3 feet)
        {
            var hits = Physics.OverlapCapsule(feet + Vector3.up * 0.4f, feet + Vector3.up * 1.6f, 0.35f,
                ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                string root = hits[i].transform.root.name;
                if (root == "Ground" || root.StartsWith("Terrain", StringComparison.Ordinal))
                    continue;
                return false;
            }
            return true;
        }

        static float BlindSpotShare(SightArea area, out int stands, out string worst, out string worstBlocker,
                                    out float worstStatic, out string worstStaticWhere)
        {
            var qv = UnityEngine.Object.FindFirstObjectByType<QuarterViewCamera>(FindObjectsInactive.Include);
            float dist = qv != null ? qv.Distance : 12f;
            int blind = 0;
            stands = 0;
            worst = "";
            worstBlocker = "";
            worstStatic = 0f;
            worstStaticWhere = "";
            float worstShare = 0f;
            for (float dx = -area.Half; dx <= area.Half + 0.001f; dx += area.Step)
                for (float dz = -area.Half; dz <= area.Half + 0.001f; dz += area.Step)
                {
                    float x = area.X + dx, z = area.Z + dz;
                    var feet = new Vector3(x, GroundYAt(new Vector2(x, z)), z);
                    if (!CanStand(feet))
                        continue;
                    stands++;
                    float share = OccludedShareAt(feet, dist, true, out _, out string blocker);
                    if (share > worstShare)
                    {
                        worstShare = share;
                        worst = "(" + x.ToString("0") + "," + z.ToString("0") + ") " + (share * 100f).ToString("0") + "%";
                        worstBlocker = blocker;
                    }
                    if (share > OutdoorOccludedMax)
                        blind++;
                    // 최악 축은 **몹을 빼고** — 움직이는 것은 구조적 결함이 아니다.
                    float stat = OccludedShareAt(feet, dist, true, out _, out string statBlocker, true);
                    if (stat > worstStatic)
                    {
                        worstStatic = stat;
                        worstStaticWhere = "(" + x.ToString("0") + "," + z.ToString("0") + ") " +
                            (stat * 100f).ToString("0") + "%" + (statBlocker != "" ? " ← " + statBlocker : "");
                    }
                }
            return stands > 0 ? blind / (float)stands : 0f;
        }

        static void AssertOutdoorSightLine()
        {
            var areas = SightAreas();
            var failures = new List<string>();
            int totalStands = 0;
            for (int i = 0; i < areas.Length; i++)
            {
                float blind = BlindSpotShare(areas[i], out int stands, out string worst, out string blocker,
                                             out float worstStatic, out string worstStaticWhere);
                totalStands += stands;
                Debug.Log("[Ulon] 야외 시선 " + areas[i].Label + " — 설 수 있는 자리 " + stands + "곳 중 가려지는 자리 " +
                          (blind * 100f).ToString("0.0") + "%, 최악 " + worst + (blocker != "" ? " ← " + blocker : "") +
                          " | 몹 제외 최악 " + (worstStatic * 100f).ToString("0.0") + "% " + worstStaticWhere);
                if (stands == 0)
                    failures.Add(areas[i].Label + ": 설 수 있는 자리를 한 곳도 못 찾음(잰 것이 없다)");
                if (blind > OutdoorBlindSpotMax)
                    failures.Add(areas[i].Label + ": 가려지는 자리 " + (blind * 100f).ToString("0.0") + "%(상한 " +
                        (OutdoorBlindSpotMax * 100f).ToString("0.0") + "%), 최악 " + worst + " ← " + blocker);
                if (worstStatic > OutdoorWorstStaticMax)
                    failures.Add(areas[i].Label + ": **한 자리**가 정적 물체에 " + (worstStatic * 100f).ToString("0.0") +
                        "% 가림(상한 " + (OutdoorWorstStaticMax * 100f).ToString("0.0") + "%) " + worstStaticWhere +
                        " — 평균이 통과해도 그 자리에 선 사람은 안 보인다");
            }
            if (totalStands == 0)
                throw new InvalidOperationException("야외 표본이 0개입니다 — 잰 것이 없습니다(0이면 실패).");
            if (failures.Count > 0)
                throw new InvalidOperationException("야외에서 플레이어가 가려지는 자리가 있습니다:\n  " +
                    string.Join("\n  ", failures) +
                    "\n카메라 각도는 고정이라 그 자리에 서면 화면에서 사라집니다.");
            Debug.Log("[Ulon] 야외 시선 — 지역 " + areas.Length + "곳 표본 " + totalStands + "자리, 평균 축 상한 " +
                      (OutdoorBlindSpotMax * 100f).ToString("0.0") + "% · 최악(몹 제외) 축 상한 " +
                      (OutdoorWorstStaticMax * 100f).ToString("0.0") + "% 이내");
        }

        /// <summary>마을 격자에서 아무것도 안 가리는 자리를 찾는다 — NC의 기준선이다.</summary>
        static bool FindClearStand(out Vector3 feet, out float share)
        {
            var qv = UnityEngine.Object.FindFirstObjectByType<QuarterViewCamera>(FindObjectsInactive.Include);
            float dist = qv != null ? qv.Distance : 12f;
            var area = SightAreas()[0];
            feet = Vector3.zero;
            share = 1f;
            for (float dx = -area.Half; dx <= area.Half + 0.001f; dx += area.Step)
                for (float dz = -area.Half; dz <= area.Half + 0.001f; dz += area.Step)
                {
                    float x = area.X + dx, z = area.Z + dz;
                    var p = new Vector3(x, GroundYAt(new Vector2(x, z)), z);
                    if (!CanStand(p))
                        continue;
                    float s = OccludedShareAt(p, dist, true, out _, out _);
                    if (s > 0.001f)
                        continue;
                    feet = p;
                    share = s;
                    return true;
                }
            return false;
        }

        /// <summary>
        /// 네거티브 컨트롤 — 마을에서 **시야가 트인 자리**를 찾아 카메라 쪽에 **큰 가림막을 실제로 세우고** 재면 빨간불이어야 한다.
        /// 계측이 야외에서도 살아 있다는 증거다(실내에서만 되는 계측을 야외에 재사용하면 조용히 0%가 나온다).
        /// </summary>
        static void AssertOutdoorSightLineNegativeControl()
        {
            var qv = UnityEngine.Object.FindFirstObjectByType<QuarterViewCamera>(FindObjectsInactive.Include);
            float pitch = qv != null ? qv.Pitch : 35f;
            float yaw = qv != null ? qv.Yaw : 45f;
            float dist = qv != null ? qv.Distance : 12f;
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            // **깨끗한 자리를 찾아서** 세운다 — 광장 한복판(0,0)은 이미 26.7%가 막혀 있었다(몹·장식).
            // 이미 막힌 자리에서 재면 「가림막 때문에 막혔다」를 증명할 수 없다.
            if (!FindClearStand(out Vector3 feet, out float clean))
                throw new InvalidOperationException("마을에서 시야가 트인 자리를 못 찾았습니다 — " +
                    "네거티브 컨트롤의 기준선을 세울 수 없습니다.");

            float before = clean;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "OutdoorSightNegativeControl";
            wall.transform.position = feet + Vector3.up * 1.0f - rot * Vector3.forward * 2.0f;
            wall.transform.localScale = new Vector3(4f, 4f, 0.3f);
            wall.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Physics.SyncTransforms();
            float after;
            try { after = OccludedShareAt(feet, dist, true, out _, out _); }
            finally { UnityEngine.Object.DestroyImmediate(wall); Physics.SyncTransforms(); }
            Physics.SyncTransforms();

            Debug.Log("[Ulon] 야외 시선 네거티브 컨트롤 — 트인 자리(" + feet.x.ToString("0") + "," + feet.z.ToString("0") + ") 가림막 없이 " + (before * 100f).ToString("0.0") +
                      "%, 앞을 막으면 " + (after * 100f).ToString("0.0") + "%");
            if (after <= OutdoorOccludedMax)
                throw new InvalidOperationException("야외 시선 네거티브 컨트롤 실패 — 마을 한복판 앞을 벽으로 막았는데 " +
                    "가려짐이 " + (after * 100f).ToString("0.0") + "%였습니다(야외에서는 아무것도 못 재고 있습니다).");
            if (before > OutdoorOccludedMax)
                throw new InvalidOperationException("야외 시선 네거티브 컨트롤 — 가림막을 세우기도 전에 " +
                    (before * 100f).ToString("0.0") + "%가 막혀 있습니다(빨간불과 복구를 나란히 잴 수 없습니다).");
        }
    }
}
