using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class QaShots
    {
        /// <summary>
        /// **사람 근접 다섯 장이 왜 못 찍히나 — 세기만 한다**(검수 랩 ㉨).
        ///
        /// 로그에는 다섯 자리가 「못 찍는 자리」로 찍혀 있는데 **화면을 보면 셋(46·47·49)은 멀쩡하다.**
        /// 그러니 「다섯 장이 못 찍힌다」는 말부터 틀렸다 — 못 찍는 것과 세계가 틀린 것을 가르기 전에
        /// **방위마다 무엇이 참인지**를 먼저 적는다. 여기서는 아무것도 안 고치고 아무것도 판정하지 않는다.
        ///
        /// 방위 하나마다 넷을 잰다:
        ///   보임 — 머리·몸통이 렌더러에 안 막힌 표본 비율(고르는 쪽이 쓰는 그 자)
        ///   렌즈 — 눈앞이 비었나(EyeCrowded)
        ///   유령 — 눈과 사람 사이에 **페이드될 것**이 있나(반투명이 얼굴을 덮는 그 원인)
        ///   볕   — 카메라 쪽 면이 해를 보는 정도(-1~1)
        /// 그리고 방위와 무관한 것 하나: **사람이 볕에 서 있나 그늘에 서 있나**(머리에서 해로 광선).
        /// 이것이 갈려야 「눈을 옮기면 되는 것」과 「어느 방위에서도 그늘인 것」이 갈린다.
        ///
        /// **이 자가 못 보는 것**(원장 규칙 `270da455` — 자를 세울 땐 그 자의 구멍을 같이 적는다):
        ///   ① **첫 판은 거리를 하나만 쟀다가 세계를 잘못 그렸다.** 고르는 쪽은 껍데기 안까지 0.3m씩
        ///      당겨 보는데 이 자는 원래 거리에서만 재서, 화면에서 멀쩡한 상인이 「전 방위 막힘」으로
        ///      읽혔다. 지금은 같은 당김을 돈다 — **고르는 쪽과 다른 자리를 재면 그 숫자는 세계가 아니다.**
        ///   ② **그늘 판정은 사람이 서 있는 한 점만 본다.** 몸이 볕이어도 챙·처마가 얼굴만 덮으면
        ///      「볕」으로 나온다(은행원 모자 챙이 실제로 그렇다). 얼굴 밝기는 여전히 화면으로 본다.
        ///   ③ **플레이어·짐승처럼 움직이는 것은 이 순간의 자리로만 잰다** — 같은 방위가 다음 판에
        ///      다르게 나올 수 있다.
        /// </summary>
        [MenuItem("Ulon/Count Person Bearings")]
        public static void CountPersonBearingsMenu() { CountPersonBearings(); }

        public static void RunPersonBearings()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity");
            CountPersonBearings();
            if (Application.isBatchMode)
                EditorApplication.Exit(0);
        }

        public static void CountPersonBearings()
        {
            var people = VillagerLook.Villagers();
            for (int i = 0; i < people.Count; i++)
                CountOne((45 + i).ToString("00") + "_person_" + VillagerLook.HostOf(people[i]), people[i]);
        }

        static void CountOne(string name, GameObject go)
        {
            if (!GroundFit.WorldBounds(go.transform, out Bounds box,
                    t => GroundFit.IsGear(go.transform, t) || GroundFit.IsFacilityPart(go.transform, t)))
            {
                Debug.Log("[Census] 사람 방위 " + name + " — 몸을 못 쟀다");
                return;
            }
            var target = box.center;
            float radius = Mathf.Max(box.extents.magnitude, 0.6f);
            float dist = radius / Mathf.Tan(55f * 0.5f * Mathf.Deg2Rad) * PersonFramingSlack;

            // **그늘인가** — 머리에서 해 쪽으로 광선을 쏜다. 막히면 그 사람은 어느 방위에서 찍어도 그늘이다.
            var head = box.center + Vector3.up * box.extents.y * 0.8f;
            var sunFrom = SunFromDirection();
            bool shaded = BlockedByRenderer(head + sunFrom * 40f, head, go.transform, out string shader);

            var qv = Object.FindFirstObjectByType<Ulon.Client.QuarterViewCamera>(FindObjectsInactive.Include);
            float baseYaw = qv != null ? qv.Yaw : 45f;
            float[] pitches = { 10f, 18f, 26f };
            var rows = new System.Collections.Generic.List<string>();
            int okSeen = 0, okAll = 0;
            for (int p = 0; p < pitches.Length; p++)
                for (int k = 0; k < 8; k++)
                {
                    float y = baseYaw + k * 45f, pit = pitches[p];
                    // **고르는 쪽과 같은 거리를 잰다.** 첫 판은 원래 거리 하나만 쟀는데, 실제 고르는
                    // 루프는 껍데기 안까지 0.3m씩 당겨 본다 — 그래서 「전 방위 막힘」으로 읽힌 상인이
                    // 화면에서는 멀쩡했다. **자가 고르는 쪽과 다른 자리를 재면 그 숫자는 세계가 아니다.**
                    float stop = Mathf.Min(dist, 1.9f), used = dist;
                    for (float d = dist; d >= stop - 0.01f; d -= 0.3f)
                    {
                        if (d - 0.3f < stop && d > stop)
                            d = stop;
                        used = d;
                        if (!PersonBlocked(target - Quaternion.Euler(pit, y, 0f) * Vector3.forward * d, box, go.transform, out _))
                            break;
                    }
                    var eye = target - Quaternion.Euler(pit, y, 0f) * Vector3.forward * used;
                    int seen = 0, total = 0;
                    for (int sx = -1; sx <= 1; sx++)
                        for (int sy = -1; sy <= 1; sy++)
                            for (int sz = -1; sz <= 1; sz++)
                            {
                                var q = box.center + new Vector3(sx * box.extents.x * 0.6f, sy * box.extents.y * 0.6f, sz * box.extents.z * 0.6f);
                                total++;
                                if (!BlockedByRenderer(eye, q, go.transform))
                                    seen++;
                            }
                    float share = seen / (float)total;
                    bool clear = !PersonBlocked(eye, box, go.transform, out _);
                    bool crowded = EyeCrowded(eye, target, go.transform);
                    bool ghost = FadeBlocked(eye, target, go.transform);
                    float front = FrontDot(go.transform, target, pit, y, dist);
                    float lit = SunFacing(pit, y);
                    if (clear) okSeen++;
                    if (clear && !crowded && !ghost && front >= PersonFrontMin) okAll++;
                    rows.Add("요" + y.ToString("0") + "/내려" + pit.ToString("0") +
                             " " + used.ToString("0.0") + "m 보임" + (share * 100f).ToString("0") + "%" +
                             (clear ? "·머리몸통OK" : "·막힘") +
                             (crowded ? "·렌즈막힘" : "") +
                             (ghost ? "·유령" : "") +
                             " 정면" + front.ToString("0.00") + " 볕" + lit.ToString("0.00"));
                }
            // **사람이 어디를 보고 서 있나 · 해는 어디서 오나** — 정면 방위와 볕이 구조적으로 어긋나는지는
            // 이 둘의 각도 차이가 말한다(방위를 아무리 돌려도 안 되는 것은 여기서 갈린다).
            float faceYaw = Quaternion.LookRotation(new Vector3(go.transform.forward.x, 0f, go.transform.forward.z).normalized, Vector3.up).eulerAngles.y;
            float sunYaw = Quaternion.LookRotation(new Vector3(sunFrom.x, 0f, sunFrom.z).normalized, Vector3.up).eulerAngles.y;
            Debug.Log("[Census] 사람 방향 " + name + " — 얼굴이 보는 쪽 " + faceYaw.ToString("0") + "° · 해가 오는 쪽 " +
                      sunYaw.ToString("0") + "° · 얼굴과 해의 각 " + Mathf.Abs(Mathf.DeltaAngle(faceYaw, sunYaw)).ToString("0") +
                      "°(0=얼굴이 볕, 180=얼굴이 그늘)");
            Debug.Log("[Census] 사람 방위 " + name + " — 거리 " + dist.ToString("0.0") + "m · 24조합 중 머리·몸통 보이는 방위 " +
                      okSeen + "개 · 그중 렌즈·유령·정면까지 다 되는 방위 " + okAll + "개 · 사람이 " +
                      (shaded ? "그늘(" + shader + ")" : "볕") + "에 서 있다\n  " + string.Join("\n  ", rows));
        }

        /// <summary>햇빛이 **오는** 쪽(정규화). `SunFacing`이 쓰는 것과 같은 해를 찾는다.</summary>
        static Vector3 SunFromDirection()
        {
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (l.type == LightType.Directional)
                    return -l.transform.forward;
            return Vector3.up;
        }
    }
}
