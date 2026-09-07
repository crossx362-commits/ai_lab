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

        /// <summary>
        /// **사람 근접 샷은 앞에서 찍혔는가**(검수 반려 2026-09-07: 48 치유사가 뒷모습이었다).
        /// 증거 샷은 **판정할 성질이 화면에 들어오는 각도**로 찍어야 한다 — 역할이 서로 다른지는
        /// 얼굴·앞섶이 보여야 판정된다. 값은 촬영 때 고른 방위에서 실제로 계산한 코사인이다.
        /// </summary>
        public static void AssertPersonShotsFront()
        {
            var table = QaShots.PersonShotFront;
            if (table.Count == 0)
                throw new InvalidOperationException("사람 근접 샷이 하나도 없습니다 — 잰 것이 없습니다(0이면 실패).");
            var bad = new List<string>();
            foreach (var kv in table)
            {
                Debug.Log("[Ulon] 사람 샷 정면 판정 — " + kv.Key + " " + kv.Value.ToString("0.00"));
                if (kv.Value < QaShots.PersonFrontMin)
                    bad.Add(kv.Key + " " + kv.Value.ToString("0.00"));
            }
            if (bad.Count > 0)
                throw new InvalidOperationException("등을 보이고 찍힌 사람 샷 " + bad.Count + "장: " +
                    string.Join(", ", bad) + " — 얼굴이 없으면 「누구인지」가 화면에 없다(하한 " +
                    QaShots.PersonFrontMin + ").");
            Debug.Log("[Ulon] 사람 샷 정면 — " + table.Count + "장 전수 통과(하한 " + QaShots.PersonFrontMin + ")");
        }

        /// <summary>
        /// **사람 근접 샷이 실제로 보이는 방위에서 찍혔는가**(검수 반려 2026-09-08).
        ///
        /// 앞선 규칙은 「반투명이 끼는 방위를 뺀다」였는데 그건 **대리 지표**였다 —
        /// 불투명한 지붕이 통째로 가린 방위는 그 규칙을 통과했고, 게이트는 EXIT=0인데
        /// `50_villagers` 한 타일은 붉은 지붕만 찍히고 마법사는 지붕 위로 모자만 나왔다.
        /// 이제 촬영이 **머리·몸통 두 점이 다 뚫린 방위**만 후보로 쓰고, 그런 방위가 없는 사람은
        /// 차선으로 찍지 않고 이 게이트가 **이름과 함께 실패**로 올린다 — 못 찍는 사람이 있다는
        /// 사실 자체가 배치 보고다.
        /// </summary>
        public static void AssertPersonShotsUnblocked()
        {
            var table = QaShots.PersonShotClear;
            if (table.Count == 0)
                throw new InvalidOperationException("사람 근접 샷이 하나도 없습니다 — 잰 것이 없습니다(0이면 실패).");
            var bad = new List<string>();
            foreach (var kv in table)
                if (!kv.Value)
                    bad.Add(kv.Key);
            if (bad.Count > 0)
                throw new InvalidOperationException("머리·몸통이 다 보이는 방위가 없는 사람 " + bad.Count + "명: " +
                    string.Join(", ", bad) + " — 카메라를 더 비틀어 가릴 것이 아니라 **그 사람이 가려지지 않는 자리에 서야** 합니다. " +
                    "샷 프레이밍이 아니라 배치 문제입니다.");
            Debug.Log("[Ulon] 사람 샷 가림 — " + table.Count + "명 전수가 머리·몸통 다 뚫린 방위에서 찍혔다");
        }

        /// <summary>
        /// NC — 한 사람을 **판으로 둘러싸 모든 방위를 막으면** 그 이름이 실패 목록에 떠야 한다.
        /// 판 하나로는 한 방위만 막혀 24조합 중 다른 데가 뚫린다 — 결함을 **실제로** 만들려면
        /// 둘러싸야 한다(「NC가 결함을 못 만들면 게이트를 증명하지 못한다」).
        /// </summary>
        public static void AssertPersonShotsUnblockedNegativeControl()
        {
            var people = VillagerLook.Villagers();
            if (people.Count == 0)
                throw new InvalidOperationException("사람 샷 가림 NC 대상이 없습니다 — 잰 것이 없습니다(0이면 실패).");
            // **이미 실패 중인 사람을 NC 대상으로 잡으면 가짜 통과다** — 판을 세우지 않아도 빨간불이니
            // 「결함을 만들어서 빨개졌다」를 증명하지 못한다(2026-09-08 첫 판이 정확히 그랬다).
            // 그래서 **지금 통과 중인 사람**만 대상으로 삼고, 그 사람 하나가 뒤집히는지를 본다.
            GameObject victim = null;
            string want = null;
            for (int i = 0; i < people.Count; i++)
            {
                string shot = (45 + i).ToString("00") + "_person_" + VillagerLook.HostOf(people[i]);
                if (QaShots.PersonShotClear.TryGetValue(shot, out bool clear) && clear)
                {
                    victim = people[i];
                    want = shot;
                    break;
                }
            }
            if (victim == null)
                throw new InvalidOperationException("사람 샷 가림 NC를 돌릴 수 없습니다 — 지금 뚫린 방위로 찍히는 사람이 " +
                    "한 명도 없어, 판을 세워도 「이 NC가 만든 결함」인지 구분되지 않습니다. 가림 실패부터 해소하십시오.");
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bool red = false;
            string message = "";
            try
            {
                wall.name = "PersonShotBlockNC";
                wall.transform.position = victim.transform.position + Vector3.up * 1.0f;
                wall.transform.localScale = new Vector3(3f, 3f, 3f);
                UnityEngine.Object.DestroyImmediate(wall.GetComponent<Collider>());   // 콜라이더가 아니라 **보이는 것**으로 재는 게이트다
                QaShots.RecomputePersonFront();
                // **판정은 그 한 사람이 뒤집혔는가**로 한다 — 목록에 다른 실패가 섞여 있어도
                // 「내가 만든 결함이 잡혔다」는 이 사람으로만 증명된다.
                if (QaShots.PersonShotClear.TryGetValue(want, out bool nowClear) && !nowClear)
                {
                    red = true;
                    message = want + "이 통과 → 실패로 뒤집혔다";
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(wall);
                QaShots.RecomputePersonFront();          // 표를 실제 촬영 값으로 되돌린다
            }
            if (!red)
                throw new InvalidOperationException("사람 샷 가림 네거티브 컨트롤 실패 — " + victim.name +
                    "을 판으로 둘러쌌는데도 통과했습니다. 그렇다면 이 게이트는 아무것도 막고 있지 않습니다.");
            Debug.Log("[Ulon] 사람 샷 가림 네거티브 컨트롤 통과 — 한 사람을 둘러싸면 FAIL: " + message);
        }

        /// <summary>
        /// NC — **정면 규칙을 끄면 빨간불**이어야 한다. 규칙을 넣은 뒤에는 사람을 돌려세워도
        /// 프레이밍이 따라 돌아 결함이 안 만들어진다(그래서 씬이 아니라 규칙을 끈다).
        /// </summary>
        public static void AssertPersonShotsFrontNegativeControl()
        {
            bool red = false;
            string message = "";
            try
            {
                QaShots.IgnoreFrontRuleForNc = true;
                QaShots.RecomputePersonFront();
                try { AssertPersonShotsFront(); }
                catch (InvalidOperationException e) { red = true; message = e.Message; }
            }
            finally
            {
                QaShots.IgnoreFrontRuleForNc = false;
                QaShots.RecomputePersonFront();          // 표를 실제 촬영 값으로 되돌린다
            }
            if (!red)
                throw new InvalidOperationException("사람 샷 정면 네거티브 컨트롤 실패 — 규칙을 껐는데도 전부 앞에서 찍혔습니다. " +
                    "그렇다면 이 게이트는 아무것도 막고 있지 않은 것이니 다시 짜라.");
            Debug.Log("[Ulon] 사람 샷 정면 네거티브 컨트롤 통과 — 규칙을 끄면 FAIL: " + message);
        }

        /// <summary>
        /// **동료가 플레이어와 갈리는가**(검수 랩 ③ 남은 항목). 마을 5역할과 같은 자로 잰다 —
        /// 모델·든 것·몸 색 셋이 다 같으면 「내 편인지」가 화면에 없다(§8.1).
        /// 동료가 **씬에 있는지**부터 본다 — 지우는 패스만 있고 세우는 패스가 없어 사라져 있었다.
        /// </summary>
        static void AssertCompanionDistinct()
        {
            var pal = GameObject.Find(VisualSliceBuilder.CompanionObject);
            var player = GameObject.Find("Player");
            if (pal == null)
                throw new InvalidOperationException("동료(Companion)가 씬에 없습니다 — HUD의 「동료 초대」·결투 폴백이 " +
                    "가리킬 상대가 없습니다(VisualSliceBuilder.EnsureCompanion).");
            if (player == null)
                throw new InvalidOperationException("플레이어가 씬에 없습니다 — 잰 것이 없습니다(0이면 실패).");

            string mp = MobArt.ModelOf(player, out MobArt.Model pm, out _) ? pm.Prefix : "(원장 밖)";
            string mc = MobArt.ModelOf(pal, out MobArt.Model cm, out _) ? cm.Prefix : "(원장 밖)";
            var gp = string.Join("+", VillagerLook.VisibleGear(player));
            var gc = string.Join("+", VillagerLook.VisibleGear(pal));
            var cp = VillagerLook.BodyColor(player, out _);
            var cc = VillagerLook.BodyColor(pal, out _);
            float gap = ColorGap(cp, cc);
            Debug.Log("[Ulon] 동료 대비 — 플레이어 " + mp + "/" + (gp == "" ? "맨손" : gp) + "/" + PersonLookAudit.ColorText(cp) +
                      " vs 동료 " + mc + "/" + (gc == "" ? "맨손" : gc) + "/" + PersonLookAudit.ColorText(cc) +
                      " (색 거리 " + gap.ToString("0.00") + ")");
            if (mp == mc && gp == gc && gap < VillagerColorGapMin)
                throw new InvalidOperationException("동료가 플레이어와 같은 모습입니다(" + mp + "/" +
                    (gp == "" ? "맨손" : gp) + ", 색 거리 " + gap.ToString("0.00") + ") — 내 편인지가 화면에서 안 갈린다(§8.1).");
        }

        /// <summary>NC — 동료를 플레이어와 같은 모습으로 만들면 빨간불이어야 한다.</summary>
        static void AssertCompanionDistinctNegativeControl()
        {
            var pal = GameObject.Find(VisualSliceBuilder.CompanionObject);
            var player = GameObject.Find("Player");
            if (pal == null || player == null)
                throw new InvalidOperationException("동료 NC 대상이 없습니다 — 잰 것이 없습니다(0이면 실패).");
            var donor = FirstBodyMaterial(player);
            var keep = new Dictionary<Renderer, Material>();
            var gearWas = new List<(GameObject Go, bool On)>();
            var want = new HashSet<string>(VillagerLook.VisibleGear(player));
            bool red = false;
            string message = "";
            try
            {
                foreach (var r in pal.GetComponentsInChildren<Renderer>(true))
                {
                    if (VisualSliceBuilder.IsGearName(r.gameObject.name))
                    {
                        gearWas.Add((r.gameObject, r.gameObject.activeSelf));
                        r.gameObject.SetActive(want.Contains(r.gameObject.name));   // 든 것까지 같게
                        continue;
                    }
                    keep[r] = r.sharedMaterial;
                    r.sharedMaterial = donor;
                }
                float gap = ColorGap(VillagerLook.BodyColor(player, out _), VillagerLook.BodyColor(pal, out _));
                if (gap >= VillagerColorGapMin)
                    throw new InvalidOperationException("동료 NC가 결함을 못 만들었습니다 — 색 거리 " + gap.ToString("0.00"));
                try { AssertCompanionDistinct(); }
                catch (InvalidOperationException e) { red = true; message = e.Message; }
            }
            finally
            {
                foreach (var kv in keep)
                    kv.Key.sharedMaterial = kv.Value;
                for (int i = 0; i < gearWas.Count; i++)
                    gearWas[i].Go.SetActive(gearWas[i].On);
            }
            if (!red)
                throw new InvalidOperationException("동료 네거티브 컨트롤 실패 — 플레이어와 같은 모습으로 만들었는데 통과했습니다.");
            Debug.Log("[Ulon] 동료 네거티브 컨트롤 통과 — 플레이어와 같게 만들면 FAIL: " + message);
        }

        /// <summary>
        /// **장비를 슬롯별로 전수로 센다**(검수 지시 2026-09-07 4).
        ///
        /// 처음엔 「무기 1 + 방패 1」만 셌다. 그런데 이 구멍이 생긴 원인 자체가
        /// **「무기 개수만 세고 방패는 무기로 안 쳤다」**였다 — 같은 방식으로 세면 다음엔 투구나
        /// 망토가 겹쳐도 못 본다. **세는 것만 보인다.** 그래서 종류(슬롯)와 상한을 원장에 적고
        /// 켜져 있는 장비 렌더러를 종류별로 전수로 센다. 새 종류가 생기면 여기 한 줄 추가하면 된다.
        /// </summary>
        static readonly (string Slot, int Max, Func<string, bool> Is)[] GearSlots =
        {
            ("무기", 1, n => VisualSliceBuilder.IsWeaponName(n)),
            ("방패", 1, n => n.IndexOf("Shield", StringComparison.OrdinalIgnoreCase) >= 0),
            ("머리", 1, n => n.IndexOf("Hat", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             n.IndexOf("Helmet", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             n.StartsWith("BossCrown", StringComparison.Ordinal)),
            ("망토", 1, n => VisualSliceBuilder.IsCapeName(n)),
            ("화살통", 1, n => n.IndexOf("Quiver", StringComparison.OrdinalIgnoreCase) >= 0),
        };

        static void AssertGearDressed()
        {
            var actors = UnityEngine.Object.FindObjectsByType<CharacterController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (actors.Length == 0)
                throw new InvalidOperationException("사람형이 없습니다 — 잰 것이 없습니다(0이면 실패).");
            var bad = new List<string>();
            int worn = 0;
            for (int a = 0; a < actors.Length; a++)
            {
                var count = new int[GearSlots.Length];
                var names = new List<string>[GearSlots.Length];
                foreach (var t in actors[a].GetComponentsInChildren<Transform>(true))
                {
                    var r = t.GetComponent<Renderer>();
                    if (r == null || !r.enabled || !t.gameObject.activeInHierarchy)
                        continue;
                    for (int g = 0; g < GearSlots.Length; g++)
                    {
                        if (!GearSlots[g].Is(t.name))
                            continue;
                        count[g]++;
                        (names[g] ??= new List<string>()).Add(t.name);
                        worn++;
                        break;                               // 한 조각은 한 슬롯에만 센다
                    }
                }
                for (int g = 0; g < GearSlots.Length; g++)
                    if (count[g] > GearSlots[g].Max)
                        bad.Add(actors[a].name + " " + GearSlots[g].Slot + " " + count[g] + "개(" +
                                string.Join("+", names[g]) + ")");
            }
            if (worn == 0)
                throw new InvalidOperationException("장비를 걸친 사람형이 하나도 없습니다 — 잰 것이 없습니다(0이면 실패).");
            if (bad.Count > 0)
                throw new InvalidOperationException("한 슬롯에 여러 개를 걸친 사람형 " + bad.Count + "건: " +
                    string.Join(", ", bad) + " — 방패 넷을 짊어진 실루엣은 사람으로 안 읽힌다(§8.1). " +
                    "상한은 SliceSelfCheck.GearSlots 원장에 있습니다.");
            Debug.Log("[Ulon] 장비 슬롯 — 사람형 " + actors.Length + "체·걸친 조각 " + worn +
                      "개 전수, 슬롯 " + GearSlots.Length + "종 모두 상한 안");
        }

        /// <summary>NC — 꺼 둔 장비를 실제로 켜면 빨간불이어야 한다.</summary>
        static void AssertGearDressedNegativeControl()
        {
            var player = GameObject.Find("Player");
            if (player == null)
                throw new InvalidOperationException("장비 개수 NC 대상(플레이어)이 없습니다 — 잰 것이 없습니다(0이면 실패).");
            var turned = new List<GameObject>();
            bool red = false;
            string message = "";
            try
            {
                foreach (var t in player.GetComponentsInChildren<Transform>(true))
                    if (VisualSliceBuilder.IsGearName(t.name) && !t.gameObject.activeSelf)
                    {
                        t.gameObject.SetActive(true);      // **결함을 실제로 만든다**
                        turned.Add(t.gameObject);
                    }
                if (turned.Count == 0)
                    throw new InvalidOperationException("장비 개수 NC가 결함을 못 만들었습니다 — 꺼 둔 장비가 하나도 없습니다.");
                try { AssertGearDressed(); }
                catch (InvalidOperationException e) { red = true; message = e.Message; }
            }
            finally
            {
                for (int i = 0; i < turned.Count; i++)
                    turned[i].SetActive(false);
            }
            if (!red)
                throw new InvalidOperationException("장비 개수 네거티브 컨트롤 실패 — 장비를 " + turned.Count +
                    "개 더 켰는데 통과했습니다.");
            Debug.Log("[Ulon] 장비 개수 네거티브 컨트롤 통과 — 꺼 둔 장비를 켜면 FAIL: " + message);
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
