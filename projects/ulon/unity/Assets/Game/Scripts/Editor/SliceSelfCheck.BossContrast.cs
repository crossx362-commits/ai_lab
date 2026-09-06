using System;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// §8.1·§10.2 — **같은 방의 잡몹과 보스는 화면에서 갈라져야 한다**(검수 2026-09-06 반려:
        /// 던전 3에서 둘 다 같은 기사 모델·같은 붉은 망토라 잡몹이 「작은 보스」로 읽혔다).
        ///
        /// 판정은 존재가 아니라 **차이**로 한다: (모델, 망토 유무, 주 색상) 세 축 중 **2개 이상**이 달라야 한다.
        /// 크기·왕관만으로는 부족하다는 것이 화면에서 확인됐으므로 크기는 축에 넣지 않는다.
        /// </summary>
        const int ContrastAxesMin = 2;
        const float ContrastColorMin = 0.15f;

        static void AssertBossMobContrast()
        {
            CheckContrast("던전 1", Dungeon1.MobObject, Dungeon1.BossObject);
            CheckContrast("던전 2", Dungeon2.MobObject, Dungeon2.BossObject);
            CheckContrast("던전 3", Dungeon3.MobObject, Dungeon3.BossObject);
            Debug.Log("[Ulon] 보스·잡몹 대비 통과 — 방마다 (모델·망토·주 색상) 중 " + ContrastAxesMin + "축 이상 차이");
        }

        static void CheckContrast(string label, string mobObject, string bossObject)
        {
            var mob = GameObject.Find(mobObject);
            var boss = GameObject.Find(bossObject);
            if (mob == null || boss == null)
                throw new InvalidOperationException(label + "의 잡몹 또는 보스가 없습니다(" + mobObject + "/" + bossObject + ").");

            int axes = 0;
            var reason = new System.Text.StringBuilder();

            MobArt.ModelOf(mob, out MobArt.Model mobModel, out string _);
            MobArt.ModelOf(boss, out MobArt.Model bossModel, out string _);
            if (mobModel.Prefix != bossModel.Prefix)
            {
                axes++;
                reason.Append("모델(").Append(mobModel.Prefix).Append("≠").Append(bossModel.Prefix).Append(") ");
            }

            bool mobCape = HasVisibleCape(mob);
            bool bossCape = HasVisibleCape(boss);
            if (mobCape != bossCape)
            {
                axes++;
                reason.Append("망토(잡몹 ").Append(mobCape ? "있음" : "없음").Append("/보스 ").Append(bossCape ? "있음" : "없음").Append(") ");
            }

            var mobColor = MainColor(mob);
            var bossColor = MainColor(boss);
            float d = Mathf.Abs(mobColor.r - bossColor.r) + Mathf.Abs(mobColor.g - bossColor.g) + Mathf.Abs(mobColor.b - bossColor.b);
            if (d > ContrastColorMin)
            {
                axes++;
                reason.Append("색(차 ").Append(d.ToString("0.00")).Append(") ");
            }

            Debug.Log("[Ulon] 보스·잡몹 대비 " + label + " " + axes + "축 — " + (reason.Length == 0 ? "차이 없음" : reason.ToString()));
            if (axes < ContrastAxesMin)
                throw new InvalidOperationException(label + "의 잡몹과 보스가 (모델·망토·주 색상) 중 " + axes +
                    "축만 다릅니다 — 최소 " + ContrastAxesMin + "축(§8.1 실루엣·§10.2 보스 차별화). " +
                    "잡몹 망토를 끄거나 색 계열을 갈라야 플레이 거리에서 「작은 보스」로 안 읽힙니다.");
        }

        static bool HasVisibleCape(GameObject go)
        {
            var all = go.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (!VisualSliceBuilder.IsCapeName(all[i].name))
                    continue;
                var rs = all[i].GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < rs.Length; r++)
                    if (rs[r].enabled && rs[r].gameObject.activeInHierarchy)
                        return true;
            }
            return false;
        }

        /// <summary>몸통 계열 메시가 실제로 쓰는 재질 색(텍스처에 곱해지는 색).</summary>
        static Color MainColor(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                if (!rends[i].enabled || !rends[i].gameObject.activeInHierarchy)
                    continue;
                if (rends[i].name.IndexOf("Body", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                var m = rends[i].sharedMaterial;
                if (m != null)
                    return m.color;
            }
            return Color.white;
        }

        /// <summary>네거티브 컨트롤 — 잡몹에게 보스와 같은 옷·색을 입혀 게이트가 빨간불이 되는지 본다.</summary>
        static void AssertBossMobContrastNegativeControl()
        {
            var mob = GameObject.Find(Dungeon3.MobObject);
            var boss = GameObject.Find(Dungeon3.BossObject);
            if (mob == null || boss == null)
                throw new InvalidOperationException("던전 3 잡몹·보스가 없어 대비 네거티브 컨트롤을 할 수 없습니다.");

            var saved = new System.Collections.Generic.List<(Renderer R, Material M, bool Enabled, bool Active)>();
            var bossMat = BodyMaterial(boss);
            bool red = false;
            try
            {
                var all = mob.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    var rs = all[i].GetComponents<Renderer>();
                    for (int r = 0; r < rs.Length; r++)
                    {
                        saved.Add((rs[r], rs[r].sharedMaterial, rs[r].enabled, rs[r].gameObject.activeSelf));
                        if (bossMat != null && rs[r].sharedMaterial != null && rs[r].sharedMaterial.mainTexture == bossMat.mainTexture)
                            rs[r].sharedMaterial = bossMat;              // 보스와 같은 색
                        if (VisualSliceBuilder.IsCapeName(all[i].name))
                        {
                            rs[r].gameObject.SetActive(true);            // 보스와 같은 망토
                            rs[r].enabled = true;
                        }
                    }
                }
                try { AssertBossMobContrast(); }
                catch (InvalidOperationException) { red = true; }
            }
            finally
            {
                for (int i = 0; i < saved.Count; i++)
                {
                    if (saved[i].R == null)
                        continue;
                    saved[i].R.sharedMaterial = saved[i].M;
                    saved[i].R.enabled = saved[i].Enabled;
                    saved[i].R.gameObject.SetActive(saved[i].Active);
                }
            }
            if (!red)
                throw new InvalidOperationException("보스·잡몹 대비 네거티브 컨트롤 실패 — 잡몹에게 보스와 같은 망토·색을 입혔는데도 통과했습니다.");
            Debug.Log("[Ulon] 보스·잡몹 대비 네거티브 컨트롤 통과 — 같은 망토·색을 입히면 FAIL");
        }

        static Material BodyMaterial(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
                if (rends[i].name.IndexOf("Body", StringComparison.OrdinalIgnoreCase) >= 0 && rends[i].sharedMaterial != null)
                    return rends[i].sharedMaterial;
            return null;
        }
    }
}
