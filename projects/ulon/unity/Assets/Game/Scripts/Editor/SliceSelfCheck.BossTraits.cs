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
        // **「손에 가깝다」가 아니라 「어느 쪽이 손에 있나」를 잰다**(검수 판정 2026-09-08).
        // 옛 0.10m는 킷이 만들어 둔 손 슬롯(`handslot.r`) 자리를 **탈락**시켰다(실측 0.11m) — 그래서
        // 코드가 무기를 바운드 끝으로 끌어다 손에 「붙여」 놓았고, 화면에서는 자루가 손 밖으로
        // 밀려 날 밑동이 소매에 닿았다. 자가 맞추는 자를 만든 셈이다.
        // 새 자: **자루 끝은 손 반경 안**(보스 손은 1.3~1.5배로 커진다 — 0.25m), **날 끝은 손 바깥**(0.5m 밖).

        /// <summary>렌더가 켜진 자식들의 월드 바운드 — 무기 덩어리를 잰다.</summary>
        static bool BoundsOfEnabledWeapon(Transform t, out Bounds b)
        {
            b = new Bounds();
            bool any = false;
            var rends = t.GetComponentsInChildren<Renderer>(false);
            for (int i = 0; i < rends.Length; i++)
            {
                if (!rends[i].enabled || rends[i] is ParticleSystemRenderer)
                    continue;
                if (!any) { b = rends[i].bounds; any = true; }
                else b.Encapsulate(rends[i].bounds);
            }
            return any;
        }

        // **미터가 아니라 비율로 잰다**(검수 지시 2026-09-09). 절대 미터는 「사람 1.8m」를 조용히
        // 전제한다 — 몸이 커지면(보스 2.45~2.65m, 킷 배율 2.1) 같은 0.25m가 다른 뜻이 된다.
        // 유도: 손 반경 0.25m는 **키 1.8m 사람 기준**이었다 → 0.25/1.8 = 0.139 → 0.14.
        const float WeaponGripPadRatio = 0.14f;     // 손 반경 ÷ 몸 높이 (옛 0.25m @ 1.8m)
        // (옛 `WeaponTipDistMin = 0.50f`는 **한 번도 쓰이지 않았다** — 「어느 끝이 손에 있나」를 묻는
        //  자는 석궁·지팡이에서 틀려서 ㉠㉡㉢ 규칙으로 갈아치웠고, 상수만 남아 있었다. 지웠다.)
        const float WeaponForearmAngleMax = 60f;    // 무기 장축 ↔ 팔꿈치→손 방향 (각도는 이미 무차원)
        const float GripAboveNeckMax = 0.00f;       // 그립 y ≤ 목(머리 본) y (검수 2026-09-06 관찰)
        // 유도: 옛 0.15m를 실측 머리 폭(1.05~1.11m)으로 나누면 0.14 — 값은 그대로고 뜻만 몸에 붙는다.
        // 왕관이 머리 중심에서 벗어나도 되는 한도 — **머리 폭의 비**로 잰다(옛 0.15m 절대값).
        // 유도: 옛 값은 큰 보스 머리 폭 1.05m에서 잡힌 것이라 0.15/1.05 ≒ 0.14 → 0.15로 물려받는다.
        // 실측 여유(2026-09-09 네 보스): 본워든 0.00 · 섀도우캡틴 0.00 · 강철폭군 0.00 · 헥사크 0.01 —
        // 한도까지 10배 여유. 머리 폭 0.50m인 본워든에게도 같은 뜻이 되는 것이 절대값과 다른 점이다.
        const float CrownAxisOffsetHeadFrac = 0.15f;
        // 검수 2026-09-06 반려 1 — 수평만 재서 수직이 무검사로 남았다.
        const float CrownSitGapMax = 0.03f;         // 왕관 바닥이 정수리 위로 떠도 되는 한도
        const float CrownWidthRatioMax = 1.4f;      // 왕관 지름 ≤ 머리 폭 × 이 값

        static void AssertBossTraits()
        {
            AssertDungeon3Leftover();
            AssertBossTraitsNegativeControl();

            CheckBossTraits("본워든", Dungeon1.BossObject);
            CheckBossTraits("섀도우캡틴", Dungeon2.BossObject);
            CheckBossTraits("강철폭군", Dungeon3.BossObject);
            CheckBossTraits("헥사크", FieldBoss.Object);

            Debug.Log("[Ulon] 보스 차별화 통과 — §10.2 요소 " + BossTraitMin + "개↑·왕관 바닥 몸높이 " + CrownBottomRatio + "배↑·무기 " + WeaponLengthRatio + "배↑ (보스 4종)");
        }


        /// <summary>
        /// **새 자에 붙는 양방향 NC**(검수 조건 2026-09-08). 옛 자(그립 0.10m·팔뚝 60°)를 떼고 세운 자이므로,
        /// 그 자가 정말 무는지 **결함을 실제로 만들어** 확인한다 — 안 그러면 자를 바꾼 자리가 곧 빈 통과가 된다.
        ///   ㉠ 무기를 손 밖으로 옮긴다 → 「손이 무기 덩어리 안」이 깨져 빨간불이어야 한다.
        ///   ㉡ 무기를 몸통 기둥 안으로 당긴다 → 「먼 끝이 기둥 밖」이 깨져 빨간불이어야 한다.
        /// 둘 다 원위치로 되돌린다(계측이 세계를 바꾸면 안 된다).
        /// </summary>
        static void AssertBossTraitsNegativeControl()
        {
            var go = GameObject.Find(Dungeon1.BossObject);
            if (go == null)
                throw new InvalidOperationException("보스 물림 NC 대상이 없습니다: " + Dungeon1.BossObject + "(0이면 실패).");
            Transform weaponT = null;
            var all = go.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length && weaponT == null; i++)
                if (all[i].name.StartsWith(VisualSliceBuilder.BossWeaponPrefix, StringComparison.Ordinal))
                    weaponT = all[i];
            if (weaponT == null)
                throw new InvalidOperationException("보스 물림 NC — 본워든이 무기를 안 들고 있습니다(0이면 실패).");

            var savedPos = weaponT.position;
            var savedRot = weaponT.rotation;
            try
            {
                // ㉠ 손 밖으로 — 무기 길이만큼 옆으로 밀면 손 점이 덩어리 밖으로 나간다.
                weaponT.position = savedPos + Vector3.right * 3f;
                bool outHand = Red("본워든", Dungeon1.BossObject);
                weaponT.position = savedPos;
                if (!outHand)
                    throw new InvalidOperationException("보스 물림 NC ㉠ 실패 — 무기를 3m 옆으로 옮겼는데 통과했습니다. 「쥐었다」를 아무도 안 재고 있습니다.");

                // ㉡ 몸통 기둥 안으로 — 무기를 몸 중심에 겹쳐 놓으면 먼 끝이 기둥 안이다.
                var cc = go.GetComponent<CharacterController>();
                var axis = cc != null ? go.transform.TransformPoint(cc.center) : go.transform.position;
                weaponT.position = axis;
                weaponT.rotation = Quaternion.identity;
                bool inBody = Red("본워든", Dungeon1.BossObject);
                if (!inBody)
                    throw new InvalidOperationException("보스 물림 NC ㉡ 실패 — 무기를 몸통 한가운데로 당겼는데 통과했습니다. 「몸에 꽂힌 막대」를 아무도 안 재고 있습니다.");
            }
            finally
            {
                weaponT.position = savedPos;
                weaponT.rotation = savedRot;
            }
            Debug.Log("[Ulon] 보스 물림 양방향 NC 통과 — 손 밖으로 옮기면 FAIL · 몸통 안으로 당기면 FAIL");
        }

        /// <summary>결함을 만든 상태에서 게이트를 불러 빨간불인지 본다(같은 판정 코드를 쓴다 — 자는 하나다).</summary>
        static bool Red(string label, string bossObject)
        {
            try { CheckBossTraits(label, bossObject); }
            catch (InvalidOperationException) { return true; }
            return false;
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
                Debug.Log("[Ulon] 보스 무기 그립 " + label + " 손까지 " + gripD.ToString("0.00") + "m·팔뚝 정렬 " + angle.ToString("0") + "° (손 반경 여유 " +
                          (bodyH * WeaponGripPadRatio).ToString("0.00") + "m = 몸높이 " + bodyH.ToString("0.00") + "×" +
                          WeaponGripPadRatio + " · 각 한도 " + WeaponForearmAngleMax + "°)");
                // **「어느 끝이 손에 있나」를 물으면 석궁·지팡이에서 틀린다** — 실측: 섀도우캡틴 석궁의
                // 바운드 끝은 손에서 1.67m다(석궁은 가운데 개머리를 쥔다). 자를 모양에 안 기대게 바꾼다:
                //   ㉠ 무기가 **손 슬롯/손뼈 아래**에 달려 있는가(킷이 쥐라고 만든 자리)
                //   ㉡ **손 점이 무기 덩어리 안**에 있는가(가까이 있는 것과 물려 있는 것을 가른다)
                //   ㉢ 먼 끝이 **몸통 기둥 밖**인가(아래에서 따로 잰다)
                bool underHand = false;
                for (var t = weaponT; t != null && !underHand; t = t.parent)
                    underHand = t == hand;
                if (!underHand)
                    throw new InvalidOperationException(label + " 무기가 손뼈(" + hand.name +
                        ") 아래에 달려 있지 않습니다 — 옆에 세워 둔 것은 쥔 것이 아닙니다(§8.1).");
                if (BoundsOfEnabledWeapon(weaponT, out Bounds wBounds))
                {
                    var padded = wBounds;
                    padded.Expand(bodyH * WeaponGripPadRatio);  // 손 반경만큼 봐준다 — 몸에 비례한다(큰 보스는 손도 크다)
                    Debug.Log("[Ulon] 보스 무기 물림 " + label + " 손이 무기 덩어리 안? " + padded.Contains(hand.position) +
                              " (덩어리 " + wBounds.size.ToString("0.00") + ")");
                    if (!padded.Contains(hand.position))
                        throw new InvalidOperationException(label + " 손이 무기 덩어리 밖에 있습니다 — 무기는 " +
                            wBounds.center.ToString("0.0") + " 크기 " + wBounds.size.ToString("0.0") + ", 손은 " +
                            hand.position.ToString("0.0") + ". 가까이 둔 것과 **쥔 것**은 다릅니다(§8.1).");
                }
                // **팔뚝 정렬 각도는 이제 재기만 하고 막지 않는다**(검수 판정 2026-09-08).
                // 이 각도 한도(60°)는 무기를 손 슬롯에서 떼어 **팔뚝 방향으로 돌려 놓으라**는 요구였고,
                // 그 요구가 곧 「자루가 손 밖으로 밀려나는」 그림을 만들었다(실측: 슬롯 그대로면 90°인데
                // 화면에서는 손이 자루를 제대로 쥐고 있다 — 킷이 그렇게 만들어 둔 자리다).
                // 걱정했던 것은 각도가 아니라 **칼이 몸에 가로로 꽂혀 보이는 것**이므로, 자도 그것을 잰다:
                // 날 끝이 **몸통 기둥 밖**에 있어야 한다(안에 있으면 몸을 관통한 막대로 읽힌다).
                // 몸통 기둥은 **몸이 정의한 것**(CharacterController)으로 잰다 — 렌더러 바운드로 재면
                // 무기 자신이 그 바운드에 들어 있어서 기둥이 무기 길이만큼 부풀고, 석궁처럼 폭 넓은
                // 물건은 「제 몸에 꽂혔다」로 잘못 읽힌다(실측: 반경이 1.07m로 부풀었다).
                var ccBody = go.GetComponent<CharacterController>();
                if (ccBody != null)
                {
                    float scale = Mathf.Max(go.transform.lossyScale.x, go.transform.lossyScale.z);
                    float bodyR = ccBody.radius * scale;
                    Vector3 axis = go.transform.TransformPoint(ccBody.center);
                    var flatTip = new Vector2(tip.x - axis.x, tip.z - axis.z);
                    Debug.Log("[Ulon] 보스 무기 날끝 " + label + " 몸통 중심에서 " + flatTip.magnitude.ToString("0.00") +
                              "m (몸통 반경 " + bodyR.ToString("0.00") + "m)");
                    if (flatTip.magnitude < bodyR)
                        throw new InvalidOperationException(label + " 무기 날 끝이 몸통 기둥 안(중심에서 " +
                            flatTip.magnitude.ToString("0.00") + "m < 반경 " + bodyR.ToString("0.00") +
                            "m)에 있습니다 — 몸에 가로로 꽂힌 막대로 보입니다(§8.1).");
                }

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
                if (!BossFit.HeadMetrics(go, out float headTopY, out float headW, out Vector3 headC))
                    throw new InvalidOperationException(label + " 머리 정점을 못 읽었습니다 — 왕관 부착을 검사할 수 없습니다(§10.2).");
                // **재는 자와 맞추는 자를 하나로.** 옛 자는 관을 **몸 축**(CharacterController 중심)과 견줬는데
                // 빌더는 관을 **두개골 중심**(BossFit.HeadMetrics)에 얹는다. 본워든처럼 머리가 몸 축에서
                // 비켜난 골격에서는 관이 제자리에 얹혀 있어도 0.127m로 읽혔다 — 관이 아니라 **자세**를 잰 것이다.
                // 이제 빌더가 겨눈 그 점에서 잰다.
                CrownAxisNegativeControl(label, crownT, headC, headW);
                float off = new Vector2(crownT.position.x - headC.x, crownT.position.z - headC.z).magnitude;
                float offMax = headW * CrownAxisOffsetHeadFrac;   // 얼마나 비뚤어도 되는지는 **머리 폭**이 정한다
                Debug.Log("[Ulon] 보스 왕관 축 " + label + " 두개골 중심에서 " + off.ToString("0.000") + "m · 머리 폭 " +
                          headW.ToString("0.00") + "m · 비 " + (off / Mathf.Max(0.01f, headW)).ToString("0.00") +
                          " (한도 ×" + CrownAxisOffsetHeadFrac + ")");
                if (off > offMax)
                    throw new InvalidOperationException(label + " 왕관이 두개골 중심에서 수평으로 " + off.ToString("0.00") +
                        "m 벗어났습니다 — 최대 " + offMax.ToString("0.00") + "m(머리 폭 " + headW.ToString("0.00") +
                        "m × " + CrownAxisOffsetHeadFrac + ", §8.1).");
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

        /// <summary>
        /// 왕관 축 자의 양방향 NC — 관을 머리 폭만큼 옆으로 밀면 빨간불, 되돌리면 초록.
        /// 이 자는 빌더가 겨눈 점(두개골 중심)에서 재므로 **갓 구운 씬에서는 늘 0**이다.
        /// 그래서 「늘 초록인 자」가 아님을 매 판 증명한다 — 나중에 다른 패스가 관을 밀면 잡으라고 둔 자다.
        /// </summary>
        static void CrownAxisNegativeControl(string label, Transform crownT, Vector3 headC, float headW)
        {
            var keep = crownT.position;
            bool red;
            try
            {
                crownT.position = keep + new Vector3(headW, 0f, 0f);
                red = new Vector2(crownT.position.x - headC.x, crownT.position.z - headC.z).magnitude
                      > headW * CrownAxisOffsetHeadFrac;
            }
            finally
            {
                crownT.position = keep;
            }
            if (!red)
                throw new InvalidOperationException(label + " 왕관 축 네거티브 컨트롤 실패 — 관을 머리 폭 " +
                    headW.ToString("0.00") + "m만큼 옆으로 밀었는데 통과했습니다.");
        }

    }
}
