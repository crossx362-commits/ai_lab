using System;
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
                if (Math.Abs(p.x - spots[i].X) > 0.05f || Math.Abs(p.z - spots[i].Z) > 0.05f)
                    throw new InvalidOperationException(label + "은(는) 사냥터 자리 x=" + spots[i].X + " z=" + spots[i].Z +
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
        const float FootErrorMax = 0.10f;      // 발끝과 지표의 차
        const float HuntDepthMin = 3.0f;       // z 산포(최대−최소)

        static void AssertHuntGround()
        {
            var spots = VisualSliceBuilder.HuntSpots;
            float zMin = float.MaxValue, zMax = float.MinValue;
            int n = 0;
            for (int i = 0; i < spots.Length; i++)
            {
                var go = GameObject.Find(spots[i].Name);
                if (go == null)
                    throw new InvalidOperationException("사냥터 몹 " + spots[i].Name + "이(가) 없습니다.");
                var p = go.transform.position;
                float ground = VisualSliceBuilder.GroundHeightAt(p.x, p.z);
                float err = p.y - ground;
                if (Mathf.Abs(err) > FootErrorMax)
                    throw new InvalidOperationException("사냥터 몹 " + spots[i].Name + "의 발이 지표에서 " + err.ToString("+0.00;-0.00") +
                        "m 어긋났습니다 — 최대 " + FootErrorMax + "m. " + (err < 0f ? "땅속에 묻혀 화면에 안 보입니다" : "공중에 떠 있습니다") + "(§8.1).");
                zMin = Mathf.Min(zMin, p.z);
                zMax = Mathf.Max(zMax, p.z);
                n++;
            }
            float depth = zMax - zMin;
            Debug.Log("[Ulon] 사냥터 계측 — 몹 " + n + "체·발 오차 " + FootErrorMax + "m 이내·z 산포 " + depth.ToString("0.0") + "m (하한 " + HuntDepthMin + "m)");
            if (depth < HuntDepthMin)
                throw new InvalidOperationException("사냥터 몹이 z " + depth.ToString("0.0") + "m 안에 모두 서 있습니다 — 최소 " + HuntDepthMin +
                    "m. 한 줄로 진열된 화면입니다(§8.1).");

            AssertMobDressing();
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
