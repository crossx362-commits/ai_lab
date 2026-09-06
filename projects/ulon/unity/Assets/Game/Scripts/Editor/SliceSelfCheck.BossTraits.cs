using System;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// 기획서 §10.2 — 보스는 확대 **그리고** 머리장식·큰 무기·VFX·전용 기술로 차별화한다.
        /// 크기 게이트만 두면 같은 FBX를 쓰는 보스가 그대로 통과한다(검수 2026-09-06 반려 2).
        /// 여기서는 "차별화 요소가 실제로 붙었는지"를 오브젝트로 센다 — 최소 2개.
        /// </summary>
        const int BossTraitMin = 2;
        // 검수 2026-09-06: 「게이트를 존재 여부로 만들지 마라」 — 왕관은 붙었는데 어깨에 수평으로 떠 있었고,
        // 무기는 개명만 되고 손에 아무것도 없었다. 그래서 **위치와 크기**로 잰다.
        const float CrownBottomRatio = 0.80f;   // 왕관 최저점 ≥ 몸 높이의 0.8배
        const float WeaponLengthRatio = 0.40f;  // 무기 최장변 ≥ 몸 높이의 0.4배
        // 검수 2026-09-06 관찰: 무기가 얼굴 옆에 떠 있고, 왕관은 머리카락 위 노란 뿔 두 개로만 보였다.
        // 「크다」만으로는 부족하다 — **손에 들려 있는지**와 **머리 축 위에 있는지**를 잰다.
        // 앵커 거리만 재면 메시 원점이 칼끝인 장비를 통과시킨다(실제로 통과했다: 앵커 0.03m인데 화면에선 얼굴 옆).
        // 그래서 **손이 무기 덩어리 안에 있는지**를 잰다.
        // 앵커(무기 트랜스폼 원점) 거리는 판정에서 뺐다 — 메시 원점이 칼끝인 장비는 그립이 손에 있어도
        // 앵커가 1.8m 떨어진다(섀도우캡틴 실측). 화면 진실은 그립 끝점과 팔뚝 정렬이다.
        // 검수 2026-09-06 반려 2 — bounds 포함은 대리 지표였다(긴 칼의 AABB가 몸을 삼킨다).
        const float WeaponGripDistMax = 0.10f;      // 그립 끝점 ↔ 손 본
        const float WeaponForearmAngleMax = 60f;    // 무기 장축 ↔ 팔꿈치→손 방향
        const float GripAboveNeckMax = 0.00f;       // 그립 y ≤ 목(머리 본) y (검수 2026-09-06 관찰)
        const float CrownAxisOffsetMax = 0.15f;
        // 검수 2026-09-06 반려 1 — 수평만 재서 수직이 무검사로 남았다.
        const float CrownSitGapMax = 0.03f;         // 왕관 바닥이 정수리 위로 떠도 되는 한도
        const float CrownWidthRatioMax = 1.4f;      // 왕관 지름 ≤ 머리 폭 × 이 값

        static void AssertBossTraits()
        {
            AssertDungeon3Leftover();

            CheckBossTraits("본워든", Dungeon1.BossObject);
            CheckBossTraits("섀도우캡틴", Dungeon2.BossObject);
            CheckBossTraits("강철폭군", Dungeon3.BossObject);
            CheckBossTraits("헥사크", FieldBoss.Object);

            Debug.Log("[Ulon] 보스 차별화 통과 — §10.2 요소 " + BossTraitMin + "개↑·왕관 바닥 몸높이 " + CrownBottomRatio + "배↑·무기 " + WeaponLengthRatio + "배↑ (보스 4종)");
        }

        static void CheckBossTraits(string label, string bossObject)
        {
            var go = GameObject.Find(bossObject);
            if (go == null)
                throw new InvalidOperationException(label + " 오브젝트가 없습니다: " + bossObject);

            var cc = go.GetComponent<CharacterController>();
            if (cc == null || cc.height < 0.01f)
                throw new InvalidOperationException(label + " CharacterController 높이가 없습니다.");
            float bodyH = cc.height;
            float footY = go.transform.position.y;

            Transform crownT = null;
            Transform weaponT = null;
            bool crown = false;
            bool aura = false;
            bool bigWeapon = false;
            float crownBottom = 0f;
            float weaponLen = 0f;
            var all = go.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                string n = all[i].name;
                if (n == VisualSliceBuilder.BossCrownObject)
                {
                    Bounds cb;
                    if (RenderBounds(all[i], out cb))
                    {
                        crown = true;
                        crownT = all[i];
                        crownBottom = cb.min.y - footY;
                    }
                }
                if (n == VisualSliceBuilder.BossAuraObject)
                    aura = all[i].GetComponent<Light>() != null;
                if (n.StartsWith(VisualSliceBuilder.BossWeaponPrefix, StringComparison.Ordinal))
                {
                    Bounds wb;
                    if (RenderBounds(all[i], out wb))
                    {
                        bigWeapon = true;
                        weaponT = all[i];
                        weaponLen = Mathf.Max(wb.size.x, Mathf.Max(wb.size.y, wb.size.z));
                    }
                }
            }
            Debug.Log("[Ulon] 보스 차별화 계측 " + label + " 몸높이 " + bodyH.ToString("0.00") + " 왕관바닥 " + crownBottom.ToString("0.00") + " 무기길이 " + weaponLen.ToString("0.00"));

            if (crown && crownBottom < bodyH * CrownBottomRatio)
                throw new InvalidOperationException(label + " 왕관 최저점이 발끝에서 " + crownBottom.ToString("0.00") + "m입니다 — 몸 높이 " + bodyH.ToString("0.00") + "m의 " + CrownBottomRatio + "배 이상이어야 머리 위로 읽힙니다(§8.1). 지금은 어깨·목 높이에 떠 있습니다.");
            if (bigWeapon && weaponLen < bodyH * WeaponLengthRatio)
                throw new InvalidOperationException(label + " 무기 최장변이 " + weaponLen.ToString("0.00") + "m입니다 — 몸 높이의 " + WeaponLengthRatio + "배 이상이어야 「큰 무기」로 읽힙니다(§10.2).");
            if (!bigWeapon)
                throw new InvalidOperationException(label + "가 무기를 들고 있지 않습니다 — 렌더러가 있는 " + VisualSliceBuilder.BossWeaponPrefix + "* 오브젝트가 없습니다(§10.2 큰 무기). 이름만 바꾸는 것으로는 화면에 안 보입니다.");

            // 손 부착 — **그립 끝점**이 손에 있고, 칼날이 팔뚝 방향으로 뻗고, 무기가 얼굴 옆이 아니어야 한다.
            // bounds 포함 판정은 긴 칼의 AABB가 몸을 삼켜서 늘 통과했다(검수 2026-09-06 반려 2 — 대리 지표).
            var hand = VisualSliceBuilder.FindHandBone(go);
            if (weaponT != null)
            {
                if (hand == null)
                    throw new InvalidOperationException(label + "에게 손 본이 없습니다 — 무기를 손에 매달 수 없습니다(§10.2).");
                if (!BossFit.WeaponAxis(weaponT, out Vector3 grip, out Vector3 tip))
                    throw new InvalidOperationException(label + " 무기의 장축을 못 읽었습니다 — 렌더가 켜진 메시가 없습니다(§10.2).");

                float gripD = Vector3.Distance(grip, hand.position);
                var along = (tip - grip).normalized;
                var fore = BossFit.ForearmDir(hand, go.transform);
                float angle = Vector3.Angle(along, fore);
                Debug.Log("[Ulon] 보스 무기 그립 " + label + " 손까지 " + gripD.ToString("0.00") + "m·팔뚝 정렬 " + angle.ToString("0") + "° (한도 " + WeaponGripDistMax + "m/" + WeaponForearmAngleMax + "°)");
                if (gripD > WeaponGripDistMax)
                    throw new InvalidOperationException(label + " 무기 그립 끝이 손에서 " + gripD.ToString("0.00") + "m 떨어져 있습니다 — 최대 " + WeaponGripDistMax +
                        "m. 무기 덩어리는 근처에 있어도 **쥔 것으로 안 읽힙니다**(§8.1). 손 본 위치로 그립 끝을 옮기세요.");
                if (angle > WeaponForearmAngleMax)
                    throw new InvalidOperationException(label + " 무기 장축이 팔뚝 방향과 " + angle.ToString("0") + "° 어긋났습니다 — 최대 " + WeaponForearmAngleMax +
                        "°. 칼이 몸에 가로로 꽂힌 막대로 보입니다(§8.1).");

                // 자세 — 그립이 어깨보다 높으면 칼이 얼굴을 가로지른다(검수 2026-09-06 관찰).
                // 기준을 「어깨 본」으로 잡으려다 두 번 헛짚었다: 이름 검색은 모델 루트의 메시("Knight_ArmRight",
                // 발밑 좌표)를 물었고, 손에서 두 마디 위는 리그에 따라 손목(wrist.r)이었다. 리그 이름에 기대지 않고
                // **목 관절(머리 본)** 을 기준으로 잰다 — 그립이 목보다 높으면 칼이 얼굴을 가로지른다.
                var neck = BossFit.FindBone(go, "head");
                if (neck != null)
                {
                    float rise = grip.y - neck.position.y;
                    Debug.Log("[Ulon] 보스 그립 높이 " + label + " 목(" + neck.name + ") 대비 " + rise.ToString("+0.00;-0.00") + "m (한도 " + GripAboveNeckMax + ")");
                    if (rise > GripAboveNeckMax)
                        throw new InvalidOperationException(label + " 무기 그립이 목보다 " + rise.ToString("0.00") + "m 높습니다 — 최대 " + GripAboveNeckMax +
                            "m. 칼이 얼굴 높이를 가로지릅니다(§8.1).");
                }

                // 1몹 1무기(P1 #7) — 보스가 큰 무기 말고 다른 무기를 같이 들고 있으면 실루엣이 안 읽힌다.
                int weapons = 0;
                var gearAll = go.GetComponentsInChildren<Transform>(true);
                for (int g = 0; g < gearAll.Length; g++)
                {
                    var t = gearAll[g];
                    if (!VisualSliceBuilder.IsWeaponName(t.name) || !t.gameObject.activeInHierarchy)
                        continue;
                    if (t.parent != null && VisualSliceBuilder.IsWeaponName(t.parent.name))
                        continue;   // 무기 안의 부품은 따로 세지 않는다
                    var wr = t.GetComponentsInChildren<Renderer>(true);
                    bool visible = false;
                    for (int r = 0; r < wr.Length; r++)
                        if (wr[r].enabled && wr[r].gameObject.activeInHierarchy) visible = true;
                    if (visible)
                        weapons++;
                }
                if (weapons != 1)
                    throw new InvalidOperationException(label + "가 무기를 " + weapons + "개 들고 있습니다 — 1몹 1무기(P1 #7). 보스 무기를 새로 붙일 때 원래 무기를 꺼야 합니다.");

                if (BossFit.HeadBounds(go, out Bounds headB))
                {
                    var wCenter = (grip + tip) * 0.5f;
                    if (headB.Contains(wCenter))
                        throw new InvalidOperationException(label + " 무기 중심이 머리 덩어리 안에 있습니다 — 칼이 얼굴 옆 눈높이에 떠 있습니다(§8.1).");
                }

            }
            // 왕관 — 수평 축만 재면 결함이 **수직으로** 빠져나간다(검수 반려 1). 정수리 위 부착과 크기를 함께 잰다.
            if (crownT != null)
            {
                Vector3 axis = go.transform.position + cc.center;
                float off = new Vector2(crownT.position.x - axis.x, crownT.position.z - axis.z).magnitude;
                if (off > CrownAxisOffsetMax)
                    throw new InvalidOperationException(label + " 왕관이 몸 축에서 수평으로 " + off.ToString("0.00") + "m 벗어났습니다 — 최대 " + CrownAxisOffsetMax + "m(§8.1).");

                if (!BossFit.HeadMetrics(go, out float headTopY, out float headW, out Vector3 _))
                    throw new InvalidOperationException(label + " 머리 정점을 못 읽었습니다 — 왕관 부착을 검사할 수 없습니다(§10.2).");
                if (!RenderBounds(crownT, out Bounds cb2))
                    throw new InvalidOperationException(label + " 왕관 렌더러가 없습니다(§10.2).");
                float gap = cb2.min.y - headTopY;          // +면 공중에 떠 있다
                float diameter = Mathf.Max(cb2.size.x, cb2.size.z);
                Debug.Log("[Ulon] 보스 왕관 " + label + " 정수리 대비 바닥 " + gap.ToString("+0.00;-0.00") + "m·지름 " + diameter.ToString("0.00") + "m/머리폭 " + headW.ToString("0.00") + "m (한도 " + CrownSitGapMax + "m/×" + CrownWidthRatioMax + ")");
                if (gap > CrownSitGapMax)
                    throw new InvalidOperationException(label + " 왕관 바닥이 정수리보다 " + gap.ToString("0.00") + "m 위에 있습니다 — 최대 " + CrownSitGapMax +
                        "m. 관 전체가 머리 위 공중에 떠 있습니다(§8.1). CharacterController 캡슐 꼭대기가 아니라 **실제 메시 정수리**에 얹으세요.");
                if (diameter > headW * CrownWidthRatioMax)
                    throw new InvalidOperationException(label + " 왕관 지름이 " + diameter.ToString("0.00") + "m로 머리 폭 " + headW.ToString("0.00") + "m의 " +
                        (diameter / Mathf.Max(0.01f, headW)).ToString("0.0") + "배입니다 — 최대 " + CrownWidthRatioMax + "배. 머리보다 큰 관은 얹힌 것으로 안 읽힙니다(§8.1).");
            }

            int traits = (crown ? 1 : 0) + (aura ? 1 : 0) + (bigWeapon ? 1 : 0);
            if (traits < BossTraitMin)
                throw new InvalidOperationException(label + "의 §10.2 차별화 요소가 " + traits + "개입니다 — 최소 " + BossTraitMin + "개(머리장식 " + crown + " · 큰 무기 " + bigWeapon + " · VFX 오라 " + aura + "). 크기만으로는 같은 모델의 잡몹과 구분이 안 됩니다.");
        }

        /// <summary>이 트랜스폼 아래 **렌더가 켜진** 렌더러들의 합 바운드.</summary>
        static bool RenderBounds(Transform t, out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;
            var rends = t.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                if (!rends[i].enabled || !rends[i].gameObject.activeInHierarchy)
                    continue;
                if (!any) { bounds = rends[i].bounds; any = true; }
                else bounds.Encapsulate(rends[i].bounds);
            }
            return any;
        }
    }
}
