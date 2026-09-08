using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// 사냥터 몹의 자리 판정. 좌표를 Assert에 박아 두면 그 좌표가 곧 「일직선 진열」을 강제한다
        /// (실제로 그랬다 — 검수 반려를 고치려는데 옛 게이트가 막았다). 원장은 `VisualSliceBuilder.HuntSpots`다.
        /// </summary>
        static void AssertHuntSpot(string label, GameObject go, string spotName)
        {
            if (go == null)
                throw new InvalidOperationException(label + "이(가) 씬에 없습니다.");
            var spots = VisualSliceBuilder.HuntSpots;
            for (int i = 0; i < spots.Length; i++)
            {
                if (spots[i].Name != spotName)
                    continue;
                var p = go.transform.position;
                Vector3 want = VisualSliceBuilder.HuntSpotWorld(i);   // 원장은 모듈, 씬은 월드(랩 B)
                if (Math.Abs(p.x - want.x) > 0.05f || Math.Abs(p.z - want.z) > 0.05f)
                    throw new InvalidOperationException(label + "은(는) 사냥터 자리 x=" + want.x + " z=" + want.z +
                        "에 있어야 합니다(지금 " + p.x.ToString("0.0") + ", " + p.z.ToString("0.0") + ").");
                return;
            }
            throw new InvalidOperationException("사냥터 자리 원장에 " + spotName + "이(가) 없습니다.");
        }

        /// <summary>
        /// §8.1·§8.2 — 사냥터가 **화면에 보이는가**. 두 가지를 잰다:
        ///   1) 발이 지표에 있는가(지형을 올린 뒤 y=0에 남아 **땅속에 묻힌** 몹이 6체 있었다),
        ///   2) 한 줄로 서 있지 않은가(옛 「7종 일직선 진열」 반려).
        /// </summary>
        // **이 자는 절대값으로 둔다**(검수 판정 2026-09-09) — 발이 땅에 닿았는가는 몸 크기와 무관한
        // 접지 판정이고, 비율로 바꾸면 큰 몹일수록 더 떠도 통과한다. 대신 **매 판 실측 여유를 남긴다**
        // — 한도만 찍는 로그는 「지금 얼마나 아슬아슬한가」를 안 말해 준다.
        const float FootErrorMax = 0.10f;      // 발끝과 지표의 차
        const float HuntDepthMin = 3.0f;       // z 산포(최대−최소)

        static void AssertHuntGround()
        {
            var spots = VisualSliceBuilder.HuntSpots;
            float zMin = float.MaxValue, zMax = float.MinValue;
            float worstFootErr = 0f;
            string worstFoot = "없음";
            int n = 0;
            for (int i = 0; i < spots.Length; i++)
            {
                var go = GameObject.Find(spots[i].Name);
                if (go == null)
                    throw new InvalidOperationException("사냥터 몹 " + spots[i].Name + "이(가) 없습니다.");
                var p = go.transform.position;
                float ground = VisualSliceBuilder.GroundHeightAt(p.x, p.z);
                float err = p.y - ground;
                if (Mathf.Abs(err) > worstFootErr)
                {
                    worstFootErr = Mathf.Abs(err);
                    worstFoot = spots[i].Name + " " + err.ToString("+0.00;-0.00") + "m";
                }
                if (Mathf.Abs(err) > FootErrorMax)
                    throw new InvalidOperationException("사냥터 몹 " + spots[i].Name + "의 발이 지표에서 " + err.ToString("+0.00;-0.00") +
                        "m 어긋났습니다 — 최대 " + FootErrorMax + "m. " + (err < 0f ? "땅속에 묻혀 화면에 안 보입니다" : "공중에 떠 있습니다") + "(§8.1).");
                zMin = Mathf.Min(zMin, p.z);
                zMax = Mathf.Max(zMax, p.z);
                n++;
            }
            float depth = zMax - zMin;
            Debug.Log("[Ulon] 사냥터 계측 — 몹 " + n + "체·발 오차 최대 " + worstFoot + "(한도 " + FootErrorMax + "m, 여유 " +
                      (FootErrorMax - worstFootErr).ToString("0.00") + "m)·z 산포 " + depth.ToString("0.0") + "m (하한 " + HuntDepthMin + "m)");
            if (depth < HuntDepthMin)
                throw new InvalidOperationException("사냥터 몹이 z " + depth.ToString("0.0") + "m 안에 모두 서 있습니다 — 최소 " + HuntDepthMin +
                    "m. 한 줄로 진열된 화면입니다(§8.1).");

            AssertMobDressing();
            AssertHuntSpotsApart();
        }

        /// <summary>
        /// **몹끼리 겹쳐 서 있지 않은가**(검수 경증 2026-09-09: `03_hunt_mobs.png` 왼쪽 둘이 거의 한 덩이).
        ///
        /// z 산포(`HuntDepthMin`)는 「한 줄 진열」만 잡는다 — 흩어져 있어도 **두 자리가 붙어 있으면**
        /// 화면에서는 한 마리가 다른 마리를 먹는다.
        ///
        /// **월드 거리만 재면 놓친다** — 첫 판이 그랬다: 두 몹이 4.03m 떨어져 있는데도 화면에서는
        /// 한 덩이였다(`03_hunt_mobs.png`). 카메라 시선 방향으로 늘어서 있었기 때문이다.
        /// 증상이 화면에 있으면 **화면 쪽에서 재야 한다**: `VisualSliceBuilder.HuntViewEye`
        /// (QA 샷 카메라와 **같은 눈**)에서 본 **방위각 차**가 몸이 차지하는 각보다 커야 한다.
        /// 월드 거리는 그대로 바닥으로 함께 잰다(눈을 옮겨도 서로 겹쳐 서 있으면 안 된다).
        /// </summary>
        static float HuntSpotGapMin => VisualSliceBuilder.PlayerHeight * 2f;

        /// <summary>몸 폭 — 사람 키에서 유도한다(사람 실루엣은 키의 약 0.4배 폭).</summary>
        static float BodyWidth => VisualSliceBuilder.PlayerHeight * 0.4f;

        static void AssertHuntSpotsApart()
        {
            string reason = HuntSpotGapReason(true);
            if (!string.IsNullOrEmpty(reason))
                throw new InvalidOperationException(reason);
        }

        /// <summary>빨간불 사유(없으면 빈 문자열) — 게이트와 NC가 **같은 자**를 쓴다.</summary>
        static string HuntSpotGapReason(bool log)
        {
            var spots = VisualSliceBuilder.HuntSpots;
            var pos = new System.Collections.Generic.List<KeyValuePair<string, Vector3>>();
            for (int i = 0; i < spots.Length; i++)
            {
                var go = GameObject.Find(spots[i].Name);
                if (go != null)
                    pos.Add(new KeyValuePair<string, Vector3>(spots[i].Name, go.transform.position));
            }
            if (pos.Count < 2)
                return "사냥터 몹을 " + pos.Count + "체밖에 못 쟀습니다 — 잰 것이 없습니다(0이면 실패).";

            Vector2 eye = VisualSliceBuilder.HuntViewEye;
            float worst = float.MaxValue, worstSlack = float.MaxValue;
            string worstPair = "", slackPair = "", slackDetail = "";
            for (int i = 0; i < pos.Count; i++)
                for (int k = i + 1; k < pos.Count; k++)
                {
                    Vector3 a = pos[i].Value, b = pos[k].Value;
                    float d = new Vector2(a.x - b.x, a.z - b.z).magnitude;
                    if (d < worst)
                    {
                        worst = d;
                        worstPair = pos[i].Key + "×" + pos[k].Key;
                    }

                    // 보는 눈에서의 방위각 차 vs 몸이 차지하는 각(가까운 쪽 기준).
                    Vector2 va = new Vector2(a.x, a.z) - eye, vb = new Vector2(b.x, b.z) - eye;
                    float apart = Vector2.Angle(va, vb);
                    float need = Mathf.Atan2(BodyWidth, Mathf.Min(va.magnitude, vb.magnitude)) * Mathf.Rad2Deg;
                    float slack = apart - need;
                    if (slack < worstSlack)
                    {
                        worstSlack = slack;
                        slackPair = pos[i].Key + "×" + pos[k].Key;
                        slackDetail = apart.ToString("0.0") + "° 벌어짐 · 몸이 " + need.ToString("0.0") + "° 차지";
                    }
                }
            if (log)
                Debug.Log("[Ulon] 사냥터 간격 — 몹 " + pos.Count + "체 · 월드 최소 " + worstPair + " " +
                          worst.ToString("0.00") + "m(하한 " + HuntSpotGapMin.ToString("0.00") + "m) · 화면 최소 " +
                          slackPair + " " + slackDetail + "(여유 " + worstSlack.ToString("+0.0;-0.0") + "°)");
            if (worst < HuntSpotGapMin)
                return "사냥터 몹 " + worstPair + "이(가) " + worst.ToString("0.00") + "m 거리에 붙어 서 있습니다(하한 " +
                       HuntSpotGapMin.ToString("0.00") + "m) — 화면에서 한 덩이로 읽힙니다. " +
                       "`VisualSliceBuilder.HuntSpots`의 자리를 벌리십시오.";
            if (worstSlack < 0f)
                return "사냥터 몹 " + slackPair + "이(가) 보는 눈에서 겹쳐 보입니다(" + slackDetail +
                       ") — 월드 거리는 떨어져 있어도 시선 방향으로 늘어서면 화면에서는 한 덩이입니다. " +
                       "`VisualSliceBuilder.HuntSpots`의 **방위각**을 벌리십시오.";
            return "";
        }

        /// <summary>양방향 NC — 몹 하나를 옆 몹 자리로 옮기면 빨간불, 되돌리면 다시 초록.</summary>
        static void AssertHuntSpotsApartNegativeControl()
        {
            var spots = VisualSliceBuilder.HuntSpots;
            var a = GameObject.Find(spots[0].Name);
            var b = GameObject.Find(spots[1].Name);
            if (a == null || b == null)
                throw new InvalidOperationException("사냥터 몹 둘을 못 찾았습니다 — 잰 것이 없습니다(0이면 실패).");

            string before = HuntSpotGapReason(true);
            if (!string.IsNullOrEmpty(before))
                throw new InvalidOperationException("사냥터 간격 NC 실패 — 손대기 전부터 빨간불입니다: " + before);

            Vector3 kept = a.transform.position;
            bool red;
            try
            {
                a.transform.position = b.transform.position;   // 옆 몹 자리로 포갠다
                red = !string.IsNullOrEmpty(HuntSpotGapReason(false));
            }
            finally { a.transform.position = kept; }

            if (!red)
                throw new InvalidOperationException("사냥터 간격 NC 실패 — 몹 둘을 겹쳐 놨는데 통과했습니다. 빈 통과입니다.");
            if (!string.IsNullOrEmpty(HuntSpotGapReason(false)))
                throw new InvalidOperationException("사냥터 간격 NC 실패 — 되돌렸는데 빨간불이 남았습니다(계측이 세계를 바꿨습니다).");
            if ((a.transform.position - kept).sqrMagnitude > 1e-6f)
                throw new InvalidOperationException("잰 뒤 몹 자리가 달라졌습니다 — 계측이 세계를 바꿨습니다.");
            Debug.Log("[Ulon] 사냥터 간격 양방향 NC 통과 — 겹쳐 놓으면 FAIL · 되돌리면 다시 통과");
        }

        /// <summary>
        /// §8.1·§8.2 — 잡몹이 **꾸며져 있는가**. 던전 3 야만인이 술잔을 들고 맨몸으로 서 있었다
        /// (검수 2026-09-06 반려: 「왕관 쓴 보스 옆에 벗은 마네킹」).
        /// 재는 것: 손에 소품(잔·병) 금지 · 무기 정확히 1개 · 모델이 가진 의상 메시가 켜져 있을 것.
        /// 한계는 정직하게 적는다 — 의상은 **모델이 의상 메시를 가진 경우에만** 요구한다(해골에 옷을 요구할 수는 없다).
        /// </summary>
        static void AssertMobDressing()
        {
            var names = new System.Collections.Generic.List<string>();
            var spots = VisualSliceBuilder.HuntSpots;
            for (int i = 0; i < spots.Length; i++)
                names.Add(spots[i].Name);
            names.Add(Ulon.Shared.Dungeon1.MobObject);
            names.Add(Ulon.Shared.Dungeon2.MobObject);
            names.Add(Ulon.Shared.Dungeon3.MobObject);

            for (int i = 0; i < names.Count; i++)
            {
                var go = GameObject.Find(names[i]);
                if (go == null)
                    continue;
                int weapons = 0, weaponNodes = 0, clothOn = 0, clothTotal = 0;
                var all = go.GetComponentsInChildren<Transform>(true);
                for (int t = 0; t < all.Length; t++)
                {
                    var tr = all[t];
                    bool visible = false;
                    var rs = tr.GetComponents<Renderer>();
                    for (int r = 0; r < rs.Length; r++)
                        if (rs[r].enabled && rs[r].gameObject.activeInHierarchy) visible = true;

                    if (VisualSliceBuilder.IsHandPropNamePublic(tr.name) && visible)
                        throw new InvalidOperationException(names[i] + "이(가) 손에 " + tr.name + "을(를) 들고 있습니다 — 잡몹은 술잔이 아니라 무기를 들어야 합니다(§8.1).");
                    if (VisualSliceBuilder.IsClothingName(tr.name))
                    {
                        // 망토는 **보스 전용 표식**이라 잡몹 의상으로 세지 않는다(검수 2026-09-06 실루엣 반려).
                        if (VisualSliceBuilder.IsCapeName(tr.name))
                        {
                            if (visible)
                                throw new InvalidOperationException(names[i] + "이(가) 망토를 걸치고 있습니다 — 망토는 보스 전용 표식입니다(§8.1·§10.2).");
                            continue;
                        }
                        clothTotal++;
                        if (visible) clothOn++;
                    }
                    // 본 이름도 키워드에 걸린다("elbowIK"에 bow가 들어 있다) — **렌더러가 있는 것만** 무기로 센다.
                    if (VisualSliceBuilder.IsWeaponName(tr.name) &&
                        !(tr.parent != null && VisualSliceBuilder.IsWeaponName(tr.parent.name)) &&
                        tr.GetComponentsInChildren<Renderer>(true).Length > 0)
                    {
                        weaponNodes++;
                        var wr = tr.GetComponentsInChildren<Renderer>(true);
                        for (int r = 0; r < wr.Length; r++)
                            if (wr[r].enabled && wr[r].gameObject.activeInHierarchy) { weapons++; break; }
                    }
                }
                if (weapons > 1)
                    throw new InvalidOperationException(names[i] + "이(가) 무기를 " + weapons + "개 들고 있습니다 — 1몹 1무기(P1 #7).");
                // 무기 메시를 가진 모델인데 하나도 안 켜져 있으면 맨손이다(해골처럼 원래 무기가 없는 모델은 제외).
                if (weaponNodes > 0 && weapons == 0)
                    throw new InvalidOperationException(names[i] + "이(가) 무기 메시 " + weaponNodes + "개를 가지고도 맨손입니다(§8.1).");
                if (clothTotal > 0 && clothOn == 0)
                    throw new InvalidOperationException(names[i] + "의 의상 메시 " + clothTotal + "개가 전부 꺼져 있습니다 — 맨몸으로 서 있습니다(§8.2).");
            }
            Debug.Log("[Ulon] 잡몹 드레싱 통과 — " + names.Count + "체(손 소품 없음·무기 1개·의상 켜짐·망토 없음)");
        }
    }
}
