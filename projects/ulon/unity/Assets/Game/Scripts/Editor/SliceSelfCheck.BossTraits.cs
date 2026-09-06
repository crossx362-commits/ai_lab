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
