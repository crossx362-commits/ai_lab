using System;
using System.Collections.Generic;
using Ulon.Client;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// 검수 2026-09-07: 카메라가 플레이어를 따라다니므로 **카메라와 플레이어 사이에 소품이 끼는 상황은
    /// 반드시 생긴다.** 판정은 존재가 아니라 **가려짐**이다 — 플레이 카메라에서 몸통으로 광선을 쏴서
    /// 몇 %가 막히는지 잰다. 차폐 페이드(`DungeonSightFade`)를 **런타임과 같은 함수로** 먼저 적용한 뒤
    /// 재므로, 「페이드가 실제로 걷어내는가」를 재는 것이기도 하다.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        /// <summary>몸통 표본 중 막혀도 되는 비율 — 실루엣이 읽히려면 대부분이 뚫려 있어야 한다.</summary>
        const float PlayerOccludedMax = 0.10f;

        /// <summary>
        /// **지표인가**(검수 승인 2026-09-07 ④). 예전엔 `transform.root.name == "Ground"`로 갈랐다 —
        /// 판정 실패가 아니라 **분류**라서, 지표 루트를 개명하는 순간 아무도 모르게 「지표가 화면을
        /// 가린다」로 뒤집힌다(빨간불도 안 난다). 이름 대신 **성질**로 잰다: TerrainCollider이거나
        /// 루트에 Terrain 컴포넌트가 달린 것. 네거티브 컨트롤은 `AssertTerrainClassNegativeControl`.
        /// </summary>
        public static bool IsTerrainCollider(Collider c)
        {
            if (c == null)
                return false;
            if (c is TerrainCollider)
                return true;
            return c.transform.root.GetComponentInChildren<Terrain>(true) != null;
        }

        /// <summary>
        /// 지표 분류의 네거티브 컨트롤 — **양쪽**을 로그에 남긴다(검수 조건).
        ///   ① 지표 루트를 개명해도 여전히 지표로 분류되는가(이름 의존이 남아 있으면 여기서 깨진다),
        ///   ② 다른 오브젝트를 `Ground`로 개명해도 지표로 안 세는가(이름만으로 면제받지 못한다).
        /// </summary>
        static void AssertTerrainClassNegativeControl()
        {
            var ground = GameObject.Find("Ground");
            if (ground == null || ground.GetComponent<Terrain>() == null)
                throw new InvalidOperationException("지표(Terrain 컴포넌트가 달린 Ground)를 못 찾았습니다 — 잰 것이 없습니다(0이면 실패).");
            var gc = ground.GetComponent<Collider>();
            if (gc == null)
                throw new InvalidOperationException("지표에 콜라이더가 없습니다 — 분류를 잴 수 없습니다.");

            string had = ground.name;
            ground.name = "지표_개명_NC";
            bool stillTerrain = IsTerrainCollider(gc);
            ground.name = had;

            var fake = new GameObject("Ground");
            fake.transform.position = new Vector3(0f, -500f, 0f);   // 다른 게이트의 광선에 걸리지 않게 멀리
            var fc = fake.AddComponent<BoxCollider>();
            bool fakeTerrain = IsTerrainCollider(fc);
            UnityEngine.Object.DestroyImmediate(fake);

            Debug.Log("[Ulon] 지표 분류 NC — 개명한 지표를 지표로 봄: " + (stillTerrain ? "예" : "아니오") +
                      " · 이름만 Ground인 상자를 지표로 봄: " + (fakeTerrain ? "예" : "아니오"));
            if (!stillTerrain)
                throw new InvalidOperationException("지표 분류 네거티브 컨트롤 실패 — 이름을 바꾸자 지표가 지표로 안 읽힙니다(이름 의존이 남아 있다).");
            if (fakeTerrain)
                throw new InvalidOperationException("지표 분류 네거티브 컨트롤 실패 — 이름만 Ground인 상자가 지표로 통과했습니다(가림이 조용히 면제된다).");
        }

        static void AssertPlayerNotOccluded()
        {
            CheckSightLine("던전 1", Dungeon1.InteriorX, Dungeon1.InteriorZ);
            CheckSightLine("던전 2", Dungeon2.InteriorX, Dungeon2.InteriorZ);
            CheckSightLine("던전 3", Dungeon3.InteriorX, Dungeon3.InteriorZ);
            Debug.Log("[Ulon] 플레이어 시선 통과 — 실내 몸통 가려짐 " + (PlayerOccludedMax * 100f) + "% 이하");
        }

        static void CheckSightLine(string label, float cx, float cz)
        {
            float blocked = OccludedShare(cx, cz, true, out int total, out string worst);
            Debug.Log("[Ulon] 시선 " + label + " — 표본 " + total + "개 중 막힘 " +
                      (blocked * 100f).ToString("0.0") + "%" + (worst != "" ? " (" + worst + ")" : ""));
            if (blocked > PlayerOccludedMax)
                throw new InvalidOperationException(label + "에서 플레이어 몸통의 " + (blocked * 100f).ToString("0.0") +
                    "%가 소품에 가립니다 — 허용 " + (PlayerOccludedMax * 100f) + "%. 가장 많이 가리는 것: " + worst);
        }

        /// <summary>플레이 카메라와 같은 각도·거리로 서서 몸통 표본에 광선을 쏜다.</summary>
        static float OccludedShare(float cx, float cz, bool applyFade, out int total, out string worst)
        {
            var qv = UnityEngine.Object.FindFirstObjectByType<QuarterViewCamera>(FindObjectsInactive.Include);
            float pitch = qv != null ? qv.Pitch : 35f;
            float yaw = qv != null ? qv.Yaw : 45f;
            float dist = qv != null ? Mathf.Min(qv.Distance, qv.IndoorDistance) : 5.5f;

            float floor = GroundYAt(new Vector2(cx, cz)) - VisualSliceBuilder.DungeonDepth + VisualSliceBuilder.RoomFloorTop;
            var feet = new Vector3(cx, floor, cz);
            return OccludedShareAt(feet, dist, applyFade, out total, out worst);
        }

        /// <summary>
        /// 발밑 좌표와 카메라 거리를 받아 같은 계측을 한다 — 실내(방 바닥)와 야외(지표)가 **같은 함수**를 쓴다.
        /// 야외 표본을 따로 구현하면 두 판정이 갈라진다(원장 패턴).
        /// </summary>
        static float OccludedShareAt(Vector3 feet, float dist, bool applyFade, out int total, out string worst,
                                     bool ignoreCreatures = false)
        {
            var qv = UnityEngine.Object.FindFirstObjectByType<QuarterViewCamera>(FindObjectsInactive.Include);
            float pitch = qv != null ? qv.Pitch : 35f;
            float yaw = qv != null ? qv.Yaw : 45f;
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            var eye = feet + Vector3.up * 1.0f - rot * Vector3.forward * dist;
            var right = rot * Vector3.right;

            // 런타임과 **같은 함수**로 차폐 페이드를 적용한 뒤 잰다.
            var faded = new List<Renderer>();
            if (applyFade)
                DungeonSightFade.Hide(eye, feet + Vector3.up * 1.0f, DungeonSightFade.DefaultRadius, faded);

            int hit = 0;
            total = 0;
            worst = "";
            var tally = new Dictionary<string, int>();
            for (int r = 0; r < 5; r++)          // 발끝~머리
                for (int c = 0; c < 3; c++)      // 몸 폭
                {
                    var p = feet + Vector3.up * (0.15f + r * 0.4f) + right * ((c - 1) * 0.3f);
                    total++;
                    var dir = p - eye;
                    float len = dir.magnitude;
                    if (len < 0.01f) continue;
                    // 몸통 표면 직전까지만 본다 — 0.05m 여유를 두어 플레이어 자신을 세지 않는다.
                    // **RaycastAll이어야 한다**: 첫 히트만 보면 지표(제외 대상)에 가려 그 뒤의 소품을 못 본다
                    // (실측 — 네거티브 컨트롤이 「앞을 막았는데 0%」로 나와 잡았다).
                    var hits = Physics.RaycastAll(eye, dir / len, len - 0.05f, ~0, QueryTriggerInteraction.Ignore);
                    Array.Sort(hits, (u, v) => u.distance.CompareTo(v.distance));   // 가장 가까운 가림부터

                    for (int h = 0; h < hits.Length; h++)
                    {
                        var info = hits[h];
                        var rend = info.collider != null ? info.collider.GetComponent<Renderer>() : null;
                        // 「걷혔다」의 뜻이 바뀌었다(검수 2026-09-07): 렌더러를 끄는 대신 **반투명**으로 만든다.
                        // 렌더러는 켜져 있지만 뒤가 비치므로 화면을 가리지 않는다 — 판정도 그 뜻으로 읽는다.
                        if (rend != null && (!rend.enabled || DungeonSightFade.IsGhosted(rend)))
                            continue;
                        // 지표(`Ground`)는 **화면을 가리지 않는다** — 카메라가 방 안(지하)에 있고 지표 메시는
                        // 한 면만 그려져 아래에서는 그대로 통과해 보인다(광선은 맞지만 화면은 뚫려 있다).
                        // 벽·뚜껑은 빼지 않는다 — 그건 런타임 페이드가 걷는지까지 이 게이트가 봐야 한다.
                        if (IsTerrainCollider(info.collider))
                            continue;
                        // 사람·짐승은 **움직인다** — 그 자리를 「구조적으로 안 보이는 자리」로 세면 안 된다.
                        // 정적 가림만 판정하고 싶을 때 켠다(야외 최악 축, 검수 지시 2026-09-07).
                        if (ignoreCreatures && info.collider != null &&
                            info.collider.transform.root.GetComponentInChildren<Ulon.Server.WorldBody>(true) != null)
                            continue;
                        hit++;
                        string name = info.collider != null ? info.collider.transform.root.name + "/" + info.collider.name : "?";
                        tally.TryGetValue(name, out int n);
                        tally[name] = n + 1;
                        break;                              // 한 표본은 한 번만 센다
                    }
                }
            DungeonSightFade.Restore(faded);

            int best = 0;
            foreach (var kv in tally)
                if (kv.Value > best) { best = kv.Value; worst = kv.Key + " " + kv.Value + "개"; }
            return total > 0 ? hit / (float)total : 0f;
        }

        /// <summary>
        /// 네거티브 컨트롤 — 소품 하나를 **실제로 카메라와 플레이어 사이에 옮겨** 세운 뒤,
        /// ① 페이드를 끄면 가려짐이 잡혀야 하고(계측이 살아 있다는 증거)
        /// ② 페이드를 켜면 다시 뚫려야 한다(런타임이 실제로 걷어낸다는 증거).
        /// 둘을 나란히 재지 않으면 「0%」가 잘 되는 건지 아무것도 못 재는 건지 알 수 없다.
        /// </summary>
        static void AssertPlayerNotOccludedNegativeControl()
        {
            var interior = GameObject.Find(Dungeon1.InteriorObject);
            if (interior == null)
                throw new InvalidOperationException("던전 1 실내가 없어 시선 네거티브 컨트롤을 할 수 없습니다.");
            Transform victim = null;
            foreach (var t in PropNodes(interior))
                if (!IsStructureProp(t)) { victim = t; break; }
            if (victim == null)
                throw new InvalidOperationException("가릴 소품을 찾지 못했습니다.");

            var qv = UnityEngine.Object.FindFirstObjectByType<QuarterViewCamera>(FindObjectsInactive.Include);
            float pitch = qv != null ? qv.Pitch : 35f;
            float yaw = qv != null ? qv.Yaw : 45f;
            float floor = GroundYAt(new Vector2(Dungeon1.InteriorX, Dungeon1.InteriorZ))
                          - VisualSliceBuilder.DungeonDepth + VisualSliceBuilder.RoomFloorTop;
            var feet = new Vector3(Dungeon1.InteriorX, floor, Dungeon1.InteriorZ);
            var rot = Quaternion.Euler(pitch, yaw, 0f);

            var savedPos = victim.position;
            var savedScale = victim.localScale;
            var kids = victim.GetComponentsInChildren<Transform>(true);
            var savedLayers = new int[kids.Length];
            for (int i = 0; i < kids.Length; i++) savedLayers[i] = kids[i].gameObject.layer;
            float unfaded, covered;
            try
            {
                // 카메라와 플레이어 사이 1.2m 지점에, 몸을 가릴 만큼 키워 세운다.
                victim.position = feet + Vector3.up * 0.9f - rot * Vector3.forward * 1.2f;
                victim.localScale = savedScale * 2.5f;
                // ① **페이드가 못 걷는 소품**(블로커 레이어 밖)으로 만들어 본다 — 게이트가 잡아야 한다.
                for (int i = 0; i < kids.Length; i++) kids[i].gameObject.layer = 0;
                Physics.SyncTransforms();
                unfaded = OccludedShare(Dungeon1.InteriorX, Dungeon1.InteriorZ, true, out _, out string w1);
                // ② 다시 블로커로 되돌리면 런타임 페이드가 걷어내야 한다.
                for (int i = 0; i < kids.Length; i++) kids[i].gameObject.layer = savedLayers[i];
                Physics.SyncTransforms();
                covered = OccludedShare(Dungeon1.InteriorX, Dungeon1.InteriorZ, true, out _, out _);
                Debug.Log("[Ulon] 시선 네거티브 컨트롤 — 앞을 막은 소품이 페이드 밖이면 막힘 " +
                          (unfaded * 100f).ToString("0.0") + "% (" + w1 + "), 블로커로 되돌리면 " +
                          (covered * 100f).ToString("0.0") + "%");
            }
            finally
            {
                for (int i = 0; i < kids.Length; i++) kids[i].gameObject.layer = savedLayers[i];
                victim.position = savedPos;
                victim.localScale = savedScale;
                Physics.SyncTransforms();
            }
            if (unfaded <= PlayerOccludedMax)
                throw new InvalidOperationException("시선 네거티브 컨트롤 실패 — 페이드 밖 소품으로 앞을 막았는데 가려짐이 " +
                    (unfaded * 100f).ToString("0.0") + "%였습니다(계측이 아무것도 못 잡습니다).");
            if (covered > PlayerOccludedMax)
                throw new InvalidOperationException("차폐 페이드가 소품을 걷어내지 못합니다 — 앞을 막은 채 " +
                    (covered * 100f).ToString("0.0") + "%가 가립니다(페이드 반경 밖이거나 렌더러-콜라이더가 어긋납니다).");
            AssertPlayerNotOccluded();      // 복구 확인
        }
    }
}
