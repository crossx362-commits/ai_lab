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
        const float WeaponHandDistMax = 1.50f;   // 앵커는 느슨한 상식선만(진짜 판정은 아래 포함 여부)
        const float WeaponHandInsideMax = 0.15f;
        const float CrownAxisOffsetMax = 0.15f;

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

            // 손 부착 — 무기 앵커가 손 본에서 얼마나 떨어져 있나.
            var hand = VisualSliceBuilder.FindHandBone(go);
            if (weaponT != null)
            {
                if (hand == null)
                    throw new InvalidOperationException(label + "에게 손 본이 없습니다 — 무기를 손에 매달 수 없습니다(§10.2).");
                float d = Vector3.Distance(weaponT.position, hand.position);
                Debug.Log("[Ulon] 보스 무기 손 거리 " + label + " " + d.ToString("0.00") + "m (한도 " + WeaponHandDistMax + ")");
                Bounds hb;
                if (RenderBounds(weaponT, out hb))
                {
                    float outside = Mathf.Sqrt(hb.SqrDistance(hand.position));
                    Debug.Log("[Ulon] 보스 무기-손 포함 " + label + " " + outside.ToString("0.00") + "m (한도 " + WeaponHandInsideMax + ")");
                    if (outside > WeaponHandInsideMax)
                        throw new InvalidOperationException(label + " 손이 무기 덩어리 밖 " + outside.ToString("0.00") + "m에 있습니다 — 최대 " + WeaponHandInsideMax +
                            "m. 앵커만 손에 있고 무기는 얼굴 옆에 떠 있는 상태입니다(§8.1).");
                }
                if (d > WeaponHandDistMax)
                    throw new InvalidOperationException(label + " 무기가 손에서 " + d.ToString("0.00") + "m 떨어져 있습니다 — 최대 " + WeaponHandDistMax +
                        "m. 무기가 아예 다른 곳에 붙어 있습니다(§8.1). 손 본에 매다세요.");
            }
            // 왕관 중심축 — 머리 위에 얹혀야 왕관으로 읽힌다.
            if (crownT != null)
            {
                Vector3 axis = go.transform.position + cc.center;
                float off = new Vector2(crownT.position.x - axis.x, crownT.position.z - axis.z).magnitude;
                Debug.Log("[Ulon] 보스 왕관 축 편차 " + label + " " + off.ToString("0.00") + "m (한도 " + CrownAxisOffsetMax + ")");
                if (off > CrownAxisOffsetMax)
                    throw new InvalidOperationException(label + " 왕관이 몸 축에서 수평으로 " + off.ToString("0.00") + "m 벗어났습니다 — 최대 " + CrownAxisOffsetMax + "m(§8.1).");
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
