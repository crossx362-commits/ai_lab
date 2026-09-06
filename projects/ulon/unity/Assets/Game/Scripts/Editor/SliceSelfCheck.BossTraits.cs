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

        static void AssertBossTraits()
        {
            AssertDungeon3Leftover();

            CheckBossTraits("본워든", Dungeon1.BossObject);
            CheckBossTraits("섀도우캡틴", Dungeon2.BossObject);
            CheckBossTraits("강철폭군", Dungeon3.BossObject);
            CheckBossTraits("헥사크", FieldBoss.Object);

            Debug.Log("[Ulon] 보스 차별화 통과 — §10.2 요소(왕관·큰 무기·오라) " + BossTraitMin + "개↑ (보스 4종)");
        }

        static void CheckBossTraits(string label, string bossObject)
        {
            var go = GameObject.Find(bossObject);
            if (go == null)
                throw new InvalidOperationException(label + " 오브젝트가 없습니다: " + bossObject);

            bool crown = false;
            bool aura = false;
            bool bigWeapon = false;
            var all = go.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                string n = all[i].name;
                if (n == VisualSliceBuilder.BossCrownObject)
                    crown = all[i].GetComponentInChildren<Renderer>(true) != null;
                if (n == VisualSliceBuilder.BossAuraObject)
                    aura = all[i].GetComponent<Light>() != null;
                if (n.StartsWith(VisualSliceBuilder.BossWeaponPrefix, StringComparison.Ordinal))
                    bigWeapon = true;
            }

            int traits = (crown ? 1 : 0) + (aura ? 1 : 0) + (bigWeapon ? 1 : 0);
            if (traits < BossTraitMin)
                throw new InvalidOperationException(label + "의 §10.2 차별화 요소가 " + traits + "개입니다 — 최소 " + BossTraitMin + "개(머리장식 " + crown + " · 큰 무기 " + bigWeapon + " · VFX 오라 " + aura + "). 크기만으로는 같은 모델의 잡몹과 구분이 안 됩니다.");
        }
    }
}
