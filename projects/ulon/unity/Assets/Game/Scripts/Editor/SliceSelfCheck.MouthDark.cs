using System;
using System.Collections.Generic;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// **문구멍이 어둠으로 읽히나 — 화면 밝기로 잰다**(검수 판정 2026-09-09).
        ///
        /// 카메라 렌더가 필요해 `-nographics` 셀프체크에서는 못 잰다 — **VFX 실화면 자와 같은 자리**,
        /// 곧 `QaShots.Run` 끝에서 돈다(그 자가 이미 낸 길이다).
        ///
        /// 목표는 상수가 아니라 **화면에서 정의**한다: 문구멍 픽셀 밝기가 **발밑 지표의 20% 이하**.
        /// 재질을 옛 값(빛 받는 Standard)으로 되돌리면 이 자가 물어야 한다 — 그것이 NC다.
        /// </summary>
        const float MouthDarkMax = 0.20f;

        public static void AssertMouthDark()
        {
            var doors = new (string Tag, string Root, float X, float Z, float Yaw)[]
            {
                ("07/D1", Dungeon1.RootObject, Dungeon1.EntranceX, Dungeon1.EntranceZ, Dungeon1.EntranceYaw),
                ("09/D2", Dungeon2.RootObject, Dungeon2.EntranceX, Dungeon2.EntranceZ, Dungeon2.EntranceYaw),
                ("11/D3", Dungeon3.RootObject, Dungeon3.EntranceX, Dungeon3.EntranceZ, Dungeon3.EntranceYaw),
            };
            string report = "";
            foreach (var d in doors)
            {
                if (!EntranceCensus.ReadMouthDark(d.Root, d.X, d.Z, d.Yaw, out float ratio, out float m, out float g, out string what))
                    throw new InvalidOperationException("문구멍 밝기를 못 쟀습니다(" + d.Tag + "): " + what);
                report += " · " + d.Tag + " 발밑 대비 " + (ratio * 100f).ToString("0") + "%";
                if (ratio > MouthDarkMax)
                    throw new InvalidOperationException("문구멍이 발밑 지표의 " + (ratio * 100f).ToString("0") +
                        "% 밝기입니다(" + d.Tag + ", 상한 " + (MouthDarkMax * 100f).ToString("0") +
                        "%) — 구멍이 아니라 문짝으로 읽힙니다.");
            }
            Debug.Log("[Ulon] 문구멍 어둠(화면) 통과 —" + report);
            AssertMouthDarkNegativeControl();
        }

        /// <summary>
        /// **NC — 빛을 받는 재질로 되돌리면 물어야 한다.** 재질 에셋은 안 건드리고 렌더러에만
        /// 잠깐 끼웠다 뺀다(재는 자가 세계를 바꾸면 안 된다 — 랩 ③에서 값을 치른 교훈).
        /// </summary>
        static void AssertMouthDarkNegativeControl()
        {
            var root = GameObject.Find(Dungeon1.RootObject);
            var portal = root == null ? null : EntranceCensus.FindChild(root.transform, VisualSliceBuilder.EntrancePortalObject);
            var rends = new List<Renderer>();
            if (portal != null)
                rends.AddRange(portal.GetComponentsInChildren<Renderer>(true));
            if (rends.Count == 0)
                throw new InvalidOperationException("문구멍 밝기 NC를 세울 포털 렌더러가 없습니다 — " +
                    "**NC를 못 세우는 자는 게이트가 아니라 로그다.**");
            var keep = new List<Material[]>();
            var lit = new Material(Shader.Find("Standard"));
            lit.mainTexture = rends[0].sharedMaterial != null ? rends[0].sharedMaterial.mainTexture : null;
            foreach (var r in rends) { keep.Add(r.sharedMaterials); r.sharedMaterial = lit; }
            bool ok = EntranceCensus.ReadMouthDark(Dungeon1.RootObject, Dungeon1.EntranceX, Dungeon1.EntranceZ,
                                                  Dungeon1.EntranceYaw, out float ncRatio, out _, out _, out _);
            for (int i = 0; i < rends.Count; i++) rends[i].sharedMaterials = keep[i];
            UnityEngine.Object.DestroyImmediate(lit);
            if (!ok)
                throw new InvalidOperationException("문구멍 밝기 NC — 되돌린 판을 못 쟀습니다(자가 무력합니다).");
            if (ncRatio <= MouthDarkMax)
                throw new InvalidOperationException("문구멍 밝기 네거티브 컨트롤 실패 — 빛 받는 재질로 되돌렸는데도 " +
                    (ncRatio * 100f).ToString("0") + "%로 통과했습니다. 자가 무력합니다.");
            Debug.Log("[Ulon] 문구멍 어둠 NC 통과 — 빛 받는 재질로 되돌리면 " + (ncRatio * 100f).ToString("0") + "%로 걸린다");
        }
    }
}
