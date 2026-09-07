using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **마을 5역할이 서로 다른 모습인가**(검수 랩 ③사람 완료 기준, 2026-09-07).
    ///
    /// 판정은 **존재가 아니라 차이**다(검수). 두 사람이 화면에서 갈리려면 적어도 하나가 달라야 한다 —
    ///   ① 실루엣(모델) ② 든 것(보이는 장비) ③ 몸 색.
    /// 셋이 모두 같으면 「같은 사람이 두 자리에 서 있다」로 읽힌다. 그 조합을 쌍마다 전수로 잰다.
    ///
    /// 임계값은 **짐작이 아니라 실측**으로 잡았다: 고친 뒤 같은 모델을 쓰는 짝(훈련사↔은행원,
    /// 상인↔마구간지기)의 색 거리는 0.5 이상이고, 고치기 전(모두 흰색)은 0.00이었다. 그 사이인
    /// 0.15를 하한으로 둔다 — 「색을 칠했다」가 아니라 「눈에 다른 색으로 보인다」를 요구한다.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        /// <summary>같은 모델·같은 장비인 두 사람이 「다른 색」으로 읽히려면 필요한 최소 색 거리.</summary>
        const float VillagerColorGapMin = 0.15f;

        static void AssertVillagerLooksDistinct()
        {
            var people = VillagerLook.Villagers();
            if (people.Count < 2)
                throw new InvalidOperationException("마을 역할 사람이 " + people.Count +
                    "명입니다 — 잰 것이 없습니다(0이면 실패).");

            var sig = new List<string>();
            var model = new List<string>();
            var gear = new List<string>();
            var color = new List<Color>();
            for (int i = 0; i < people.Count; i++)
            {
                string m = MobArt.ModelOf(people[i], out MobArt.Model mm, out _) ? mm.Prefix : "(원장 밖)";
                var g = VillagerLook.VisibleGear(people[i]);
                var c = VillagerLook.BodyColor(people[i], out string via);
                model.Add(m);
                gear.Add(string.Join("+", g));
                color.Add(c);
                sig.Add(VillagerLook.HostOf(people[i]) + " = " + m + " / " +
                        (g.Count == 0 ? "맨손" : string.Join("+", g)) + " / " +
                        PersonLookAudit.ColorText(c) + "(" + via + ")");
            }
            for (int i = 0; i < sig.Count; i++)
                Debug.Log("[Ulon] 마을 사람 외형 — " + sig[i]);

            var same = new List<string>();
            for (int i = 0; i < people.Count; i++)
                for (int j = i + 1; j < people.Count; j++)
                {
                    if (model[i] != model[j] || gear[i] != gear[j])
                        continue;                              // 실루엣이나 든 것이 다르면 갈린다
                    float gap = ColorGap(color[i], color[j]);
                    if (gap >= VillagerColorGapMin)
                        continue;
                    same.Add(VillagerLook.HostOf(people[i]) + "↔" + VillagerLook.HostOf(people[j]) +
                             "(둘 다 " + model[i] + "/" + (gear[i] == "" ? "맨손" : gear[i]) +
                             ", 색 거리 " + gap.ToString("0.00") + " < " + VillagerColorGapMin + ")");
                }
            if (same.Count > 0)
                throw new InvalidOperationException("마을 역할 " + same.Count + "쌍이 화면에서 같은 사람으로 읽힙니다: " +
                    string.Join(", ", same) + " — 역할은 존재가 아니라 **차이**로 읽힌다(§8.1). " +
                    "모델·든 것·몸 색 중 하나는 달라야 합니다(VisualSliceBuilder.EnsureVillagerLooks).");

            Debug.Log("[Ulon] 마을 사람 구분 — " + people.Count + "명 전수, 같은 조합 0쌍(색 하한 " +
                      VillagerColorGapMin + ")");
        }

        static float ColorGap(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);

        /// <summary>
        /// 네거티브 컨트롤 — **두 사람을 같은 모습으로 만들면 빨간불**이어야 한다.
        /// 결함을 실제로 만든다: 같은 모델을 쓰는 짝에서 한쪽의 몸 재질·장비를 다른 쪽 것으로 덮는다.
        /// (씬 상태를 안 건드리고 「빌더 호출을 뺀다」로는 빨간불이 안 난다 — 이미 저장된 것이 그대로다.)
        /// </summary>
        static void AssertVillagerLooksNegativeControl()
        {
            var people = VillagerLook.Villagers();
            int a = -1, b = -1;
            for (int i = 0; i < people.Count && a < 0; i++)
                for (int j = i + 1; j < people.Count && a < 0; j++)
                {
                    MobArt.ModelOf(people[i], out MobArt.Model mi, out _);
                    MobArt.ModelOf(people[j], out MobArt.Model mj, out _);
                    if (mi.Prefix == mj.Prefix) { a = i; b = j; }
                }
            if (a < 0)
                throw new InvalidOperationException("마을 사람 구분 NC 대상(같은 모델을 쓰는 짝)이 없습니다 — " +
                    "모델이 전부 달라졌다면 이 NC는 자기 전제를 잃은 것이니 다시 짜라.");

            var victim = people[b];
            var donorMat = FirstBodyMaterial(people[a]);
            var keep = new Dictionary<Renderer, Material>();
            var gearOn = new List<GameObject>();
            bool red = false;
            string message = "";
            try
            {
                foreach (var r in victim.GetComponentsInChildren<Renderer>(true))
                {
                    if (VisualSliceBuilder.IsGearName(r.gameObject.name))
                    {
                        if (r.enabled && r.gameObject.activeInHierarchy)
                        {
                            gearOn.Add(r.gameObject);
                            r.gameObject.SetActive(false);   // 든 것까지 같게 만들어야 결함이 완성된다
                        }
                        continue;
                    }
                    keep[r] = r.sharedMaterial;
                    r.sharedMaterial = donorMat;
                }
                // 결함이 **실제로 만들어졌는지** 먼저 확인한다 — 못 만들었으면 NC 자신의 실패다.
                float gap = ColorGap(VillagerLook.BodyColor(people[a], out _), VillagerLook.BodyColor(victim, out _));
                if (gap >= VillagerColorGapMin || VillagerLook.VisibleGear(victim).Count != VillagerLook.VisibleGear(people[a]).Count)
                    throw new InvalidOperationException("마을 사람 구분 NC가 결함을 못 만들었습니다 — " +
                        "색 거리 " + gap.ToString("0.00") + ", 든 것 " + string.Join("+", VillagerLook.VisibleGear(victim)));
                try { AssertVillagerLooksDistinct(); }
                catch (InvalidOperationException e) { red = true; message = e.Message; }
            }
            finally
            {
                foreach (var kv in keep)
                    kv.Key.sharedMaterial = kv.Value;
                for (int i = 0; i < gearOn.Count; i++)
                    gearOn[i].SetActive(true);
            }
            if (!red)
                throw new InvalidOperationException("마을 사람 구분 네거티브 컨트롤 실패 — " +
                    VillagerLook.HostOf(victim) + "를 " + VillagerLook.HostOf(people[a]) +
                    "와 같은 모습으로 만들었는데 통과했습니다.");
            Debug.Log("[Ulon] 마을 사람 구분 네거티브 컨트롤 통과 — 둘을 같은 모습으로 만들면 FAIL: " + message);
        }

        /// <summary>
        /// **모자가 사람을 덮지 않는가**(랩 ③ 실측 발견, 2026-09-07). 훈련사 모자가 몸 폭의 2.5배를
        /// 넘어 근접 샷에서 챙이 화면을 덮었다 — 실루엣은 「사람」으로 읽혀야 한다(§8.1).
        /// 상한은 빌더가 맞추는 목표(<see cref="VisualSliceBuilder.HatHeadWidthMax"/>)에 여유 0.2를 더한 값이다.
        /// </summary>
        static void AssertVillagerHatFits()
        {
            var people = VillagerLook.Villagers();
            var bad = new List<string>();
            int measured = 0;
            for (int i = 0; i < people.Count; i++)
            {
                if (!HatRatio(people[i], out float ratio))
                    continue;
                measured++;
                Debug.Log("[Ulon] 마을 사람 모자 — " + VillagerLook.HostOf(people[i]) + " 모자/몸 폭 " + ratio.ToString("0.0") + "배");
                if (ratio > VisualSliceBuilder.HatHeadWidthMax + 0.2f)
                    bad.Add(VillagerLook.HostOf(people[i]) + " " + ratio.ToString("0.0") + "배");
            }
            if (measured == 0)
                throw new InvalidOperationException("모자 쓴 마을 사람이 없습니다 — 잰 것이 없습니다(0이면 실패).");
            if (bad.Count > 0)
                throw new InvalidOperationException("모자가 머리보다 지나치게 큰 사람 " + bad.Count + "명: " +
                    string.Join(", ", bad) + " — 챙이 몸을 덮으면 화면에서 사람으로 안 읽힌다(§8.1). " +
                    "상한 " + (VisualSliceBuilder.HatHeadWidthMax + 0.2f) + "배.");
        }

        /// <summary>NC — 모자를 실제로 키우면 빨간불이어야 한다.</summary>
        static void AssertVillagerHatFitsNegativeControl()
        {
            var people = VillagerLook.Villagers();
            Transform hat = null;
            for (int i = 0; i < people.Count && hat == null; i++)
                hat = HatOf(people[i]);
            if (hat == null)
                throw new InvalidOperationException("모자 NC 대상이 없습니다 — 잰 것이 없습니다(0이면 실패).");
            var keep = hat.localScale;
            bool red = false;
            string message = "";
            try
            {
                hat.localScale = keep * 3f;                  // **결함을 실제로 만든다**
                try { AssertVillagerHatFits(); }
                catch (InvalidOperationException e) { red = true; message = e.Message; }
            }
            finally { hat.localScale = keep; }
            if (!red)
                throw new InvalidOperationException("모자 네거티브 컨트롤 실패 — 모자를 3배로 키웠는데 통과했습니다.");
            Debug.Log("[Ulon] 마을 사람 모자 네거티브 컨트롤 통과 — 모자를 3배로 키우면 FAIL: " + message);
        }

        static Transform HatOf(GameObject who)
        {
            foreach (var r in who.GetComponentsInChildren<Renderer>(true))
                if (r.enabled && r.gameObject.activeInHierarchy &&
                    r.gameObject.name.IndexOf("Hat", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return r.transform;
            return null;
        }

        static bool HatRatio(GameObject who, out float ratio)
        {
            ratio = 0f;
            Bounds hatBox = new Bounds(), headBox = new Bounds();
            bool hasHat = false, hasHead = false;
            foreach (var r in who.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy)
                    continue;
                if (r.gameObject.name.IndexOf("Hat", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (hasHat) hatBox.Encapsulate(r.bounds); else { hatBox = r.bounds; hasHat = true; }
                }
                else
                {
                    if (hasHead) headBox.Encapsulate(r.bounds); else { headBox = r.bounds; hasHead = true; }
                }
            }
            if (!hasHat || !hasHead)
                return false;
            float headW = Mathf.Max(headBox.size.x, headBox.size.z);
            if (headW < 0.01f)
                return false;
            ratio = Mathf.Max(hatBox.size.x, hatBox.size.z) / headW;
            return true;
        }

        static Material FirstBodyMaterial(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                if (!VisualSliceBuilder.IsGearName(r.gameObject.name) && r.sharedMaterial != null)
                    return r.sharedMaterial;
            return null;
        }
    }
}
