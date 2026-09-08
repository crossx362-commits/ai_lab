using System;
using System.Collections.Generic;
using System.IO;
using Ulon.Client;
using Ulon.Server;
using Ulon.Shared;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// **VisualSliceBuilder 분할**(오너 상시 지시 2026-09-08, 파일 비대화 정리).
// 이 파일이 담는 것: 서비스 NPC·주민 외형·동료·장비 착용·몹 외형 원장·보스 치장 — 사람과 몹의 겉모습.
// 동작 변경 0 — 구간을 순서 그대로 옮기기만 했다(순서를 바꾸면 주석과 몸통의 짝이 깨진다).
namespace Ulon.Editor
{
    public static partial class VisualSliceBuilder
    {
        /// <summary>
        /// **마을 서비스 NPC**(검수 랩 ②, §18.19). 표시명이 「치유사」·「마구간지기」인데 화면엔 분수와
        /// 좌판만 있었다 — 말을 거는 상대가 없다는 뜻이다. 저장소의 사람 모델로 지금 세울 수 있다.
        /// 새 모델을 받지 않으므로 훈련사(Mage)와 겹치지 않는 것만 고른다.
        /// 멱등 — 있으면 헐고 다시 세운다(옛 씬도 이 패스가 고친다).
        /// </summary>
        public static void EnsureServiceNpcs()
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (ctrl == null)
            {
                Debug.LogWarning("[Ulon] 애니메이터 컨트롤러가 없어 서비스 NPC를 세우지 못했습니다 — T포즈로 세우지 않는다.");
                return;
            }
            int made = 0;
            // 치유사는 **후드 로브**다 — Knight(판금)일 때 「경비로 읽힌다」로 §8.1 불합격이었고,
            // 식별 축이 청록 틴트 하나뿐이었다(가슴 꽃 문양은 Knight 기본 텍스처라 우리 표식이 아니다).
            made += ServiceNpc("Healer", RogueHoodedFbx, "치유사", new Vector3(1.5f, 0f, 0.9f), ctrl) ? 1 : 0;
            made += ServiceNpc("Stable", RogueFbx, "마구간지기", new Vector3(1.3f, 0f, -0.9f), ctrl) ? 1 : 0;
            // 표시명이 「은행」·「잡화」라 접미사 규칙엔 안 걸리지만, §18.19가 말하는 마을 서비스는
            // **말을 거는 상대**다(검수: 「은행원·상인이 좌판인 것이 위반의 본체」).
            // 은행원은 문 앞에 세운다 — 건물 안에 넣으면 밖에서 안 보이고 워프 착지 자리를 먹는다.
            made += ServiceNpc("Banker", MageFbx, "은행원", new Vector3(0f, 0f, -3.4f), ctrl) ? 1 : 0;
            made += ServiceNpc("Vendor", RogueFbx, "상인", new Vector3(0f, 0f, -1.6f), ctrl) ? 1 : 0;
            Physics.SyncTransforms();
            Debug.Log("[Ulon] 마을 서비스 NPC — " + made + "명(치유사·마구간지기·은행원·상인). 표시명만 사람이던 자리에 사람을 세운다(§18.19). " +
                      "사람 모델 4종(치유사=RogueHooded, 훈련사·은행원=Mage, 마구간지기·상인=Rogue) — 겹치는 짝은 색·든 것으로 가른다.");
        }

        /// <summary>
        /// **마을 사람 5역할을 서로 다르게 보이게 한다**(검수 랩 ③사람, 2026-09-07).
        ///
        /// 실측(`PersonLookAudit`)이 말한 것: 다섯이 모델 3종을 돌려써서 **상인과 마구간지기가 같고**,
        /// 치유사는 칼·방패를 들어 경비로 읽히고, 훈련사는 완드와 지팡이를 **둘 다** 들고 있었다.
        /// 새 모델을 받지 않고 두 축으로 가른다 —
        ///   ① **든 것**: KayKit 모델은 검·방패·완드·석궁을 FBX 안에 품고 있다(꺼져 있을 뿐이다),
        ///   ② **몸 색**: 같은 모델을 쓰는 짝은 옷 색으로 가른다(질감은 그대로, 색만 곱한다).
        /// 색만으로 가르지 않는 이유는 색맹·야간 화면에서 무너지기 때문이고, 장비만으로 가르지 않는
        /// 이유는 손이 빈 역할이 둘 있기 때문이다. **두 축을 같이 본다**(게이트도 같은 두 축을 잰다).
        /// 멱등 — 매 실행 같은 결과가 되게 「켤 것」과 「끌 것」을 둘 다 명시한다.
        /// </summary>
        /// <summary>
        /// **마을 사람 외형 원장 — 세우는 자와 재는 자가 같이 읽는다.** 예전엔 이 표가
        /// `EnsureVillagerLooks` 안에만 있어서 게이트가 「맨손이어야 할 사람」이 누구인지 몰랐다
        /// (원장: 재는 자와 맞추는 자는 하나여야 한다).
        /// </summary>
        public static readonly (string Host, string Gear, Color Tint, string Why)[] VillagerSpecs =
        {
            ("Healer",  "",             new Color(0.62f, 0.92f, 0.86f), "치유사 — 칼·방패를 내려놓아야 경비로 안 읽힌다"),
            ("Trainer", "2H_Staff",     new Color(0.86f, 0.34f, 0.28f), "훈련사 — 가르치는 도구 하나만(완드까지 둘은 무기 둘이다)"),
            ("Banker",  "",             new Color(0.30f, 0.36f, 0.62f), "은행원 — 훈련사와 같은 Mage 몸이라 색·장비로 가른다"),
            ("Vendor",  "",             new Color(0.92f, 0.78f, 0.30f), "상인 — 마구간지기와 같은 Rogue 몸이라 색으로 가른다"),
            ("Stable",  "1H_Crossbow",  new Color(0.48f, 0.34f, 0.20f), "마구간지기 — 짐승을 다루는 손에 무언가 들려야 상인과 갈린다"),
        };

        public static void EnsureVillagerLooks()
        {
            var specs = VillagerSpecs;
            var people = VillagerLook.Villagers();
            int done = 0;
            var lines = new List<string>();
            for (int s = 0; s < specs.Length; s++)
            {
                GameObject who = null;
                for (int i = 0; i < people.Count; i++)
                    if (VillagerLook.HostOf(people[i]) == specs[s].Host)
                        who = people[i];
                if (who == null)
                    continue;
                // ① 든 것 — 원장이 적은 하나만 켜고 나머지 장비는 끈다(모델이 품은 것이 제멋대로 켜져 있었다).
                foreach (var t in who.GetComponentsInChildren<Transform>(true))
                {
                    if (!IsGearName(t.name))
                        continue;
                    t.gameObject.SetActive(t.name == specs[s].Gear);
                }
                // ①-b **자리로 한 번 더** — 이름 목록에 없는 것이 손에 남아 있었다(치유사 `Throwable`,
                //      은행원 `Spellbook`). 선언한 장비가 아니면서 손뼈 근처에 켜져 있는 것은 끈다.
                Physics.SyncTransforms();
                foreach (var r in HeldNearHands(who))
                    if (r.gameObject.name != specs[s].Gear)
                        r.gameObject.SetActive(false);
                // ② 몸 색 — 장비를 뺀 몸 렌더러에만 칠한다(칼날 색이 그 사람의 색이 되면 안 된다).
                var mat = TintMaterial(specs[s].Host, specs[s].Tint, who);
                if (mat != null)
                    foreach (var r in who.GetComponentsInChildren<Renderer>(true))
                        if (!IsGearName(r.gameObject.name))
                            r.sharedMaterial = mat;
                FitVillagerHat(who);
                done++;
                lines.Add(specs[s].Host + "=" + (specs[s].Gear == "" ? "맨손" : specs[s].Gear) + "/" +
                          ColorUtility.ToHtmlStringRGB(specs[s].Tint) + " (" + specs[s].Why + ")");
            }
            Debug.Log("[Ulon] 마을 사람 외형 — " + done + "명을 든 것·몸 색 두 축으로 갈랐다: " + string.Join(" · ", lines));
        }

        /// <summary>
        /// **모자가 머리보다 크면 줄인다**(랩 ③에서 실측으로 드러난 결함, 2026-09-07).
        /// 훈련사 모자가 2.09 × 1.34 × 2.04m — 몸통(0.85 × 0.95)의 두 배가 넘어 근접 샷에서
        /// 사람 대신 챙이 화면을 덮었다. 옛 씬에 남은 잔재다(`EnsureTrainerNpc`는 있으면 일찍 반환한다).
        ///
        /// `SlimHeadgear`처럼 **곱하기**로 줄이면 실행할수록 작아진다(멱등이 아니다) —
        /// 그래서 **목표 비율에 맞춘다**: 모자 폭 ≤ 몸(모자 뺀 것) 폭 × <see cref="HatHeadWidthMax"/>.
        /// 처음엔 「머리 폭」과 견줬는데, 스킨드 렌더러의 머리 바운드가 1.27m로 부풀어 있어
        /// **모자가 2.09m여도 1.7배로 읽혀 통과**했다(보스 실루엣 게이트가 몸 폭을 쓰는 것과 같은 이유다).
        /// 이미 그 안이면 아무것도 안 한다(그래서 몇 번을 돌려도 같은 결과다).
        /// </summary>
        public const float HatHeadWidthMax = 1.0f;

        static void FitVillagerHat(GameObject who)
        {
            Transform hat = null;
            Bounds hatBox = new Bounds(), headBox = new Bounds();
            bool hasHat = false, hasHead = false;
            foreach (var r in who.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy)
                    continue;
                if (GroundFit.IsHeadgear(who.transform, r.transform))   // 이름이 아니라 자리·성질(랩 ②)
                {
                    hat = r.transform;
                    hatBox = hasHat ? Encapsulated(hatBox, r.bounds) : r.bounds;
                    hasHat = true;
                }
                else
                {
                    headBox = hasHead ? Encapsulated(headBox, r.bounds) : r.bounds;   // 모자를 뺀 몸 전체
                    hasHead = true;
                }
            }
            if (!hasHat || !hasHead)
                return;
            float hatW = Mathf.Max(hatBox.size.x, hatBox.size.z);
            float headW = Mathf.Max(headBox.size.x, headBox.size.z);
            if (headW < 0.01f || hatW <= headW * HatHeadWidthMax)
                return;
            float k = headW * HatHeadWidthMax / hatW;
            hat.localScale = hat.localScale * k;
            Debug.Log("[Ulon] 모자 비례 " + who.name + " — 모자 폭 " + hatW.ToString("0.00") + "m가 몸 폭 " +
                      headW.ToString("0.00") + "m의 " + (hatW / headW).ToString("0.0") + "배라 " +
                      k.ToString("0.00") + "배로 줄였다(목표 " + HatHeadWidthMax + "배 이하).");
        }

        static Bounds Encapsulated(Bounds a, Bounds b) { a.Encapsulate(b); return a; }


        /// <summary>
        /// **동료를 다시 세운다**(검수 랩 ③ 남은 항목, 2026-09-07).
        ///
        /// 실측으로 드러난 것: 씬에 `Companion`이 **아예 없다**. 예전에 `EnsureMobArtQualified`가
        /// 자격 미달 모델(맨몸 야만인)을 지웠는데 **다시 세우는 패스가 없었다** — 그래서 HUD의
        /// 「동료 초대」·결투 상대 폴백이 오프라인에서 아무 일도 안 했다. 지우는 패스를 만들면
        /// **다시 세우는 패스도 같이** 있어야 한다.
        ///
        /// 플레이어와 갈리는 축은 마을 5역할과 같다 — **몸 색(초록)과 든 것(검만, 방패 없음)**.
        /// 새 모델은 받지 않는다(§11 승인 대기와 무관하게 지금 세울 수 있어야 한다).
        /// </summary>
        public const string CompanionObject = "Companion";

        /// <summary>
        /// 동료가 서는 **한 자리**(원장). 스폰과 보수 패스가 각각 좌표를 들고 있어 한쪽을 고치면
        /// 다른 쪽이 되돌리고 있었다 — 자리 원장이 둘이면 갈린다(검수 계열 문장).
        ///
        /// 카메라는 플레이어 기준 (−x,−z) 쪽이다(고정 쿼터뷰 요 45°). 이 자리는 그 축에서 **77°**
        /// 비껴 있어 상시 가림이 아니다(`AssertCompanionOffSightAxis`, 하한 35°). 고정 카메라라
        /// 프레임마다 계산할 필요가 없다 — 월드 고정 좌표로 충분하다.
        /// </summary>
        public static readonly Vector3 CompanionSpot = new Vector3(-2.2f, 0f, 1.4f);
        public static readonly Color CompanionTint = new Color(0.36f, 0.70f, 0.42f);

        public static void EnsureCompanion()
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (ctrl == null)
            {
                Debug.LogWarning("[Ulon] 애니메이터 컨트롤러가 없어 동료를 세우지 못했습니다 — T포즈로 세우지 않는다.");
                return;
            }
            var go = GameObject.Find(CompanionObject);
            if (go == null)
            {
                ConfigureHumanoid(KnightFbx, true);
                go = SpawnActor(CompanionObject, KnightFbx, CompanionSpot, 1.85f, ctrl,
                                false, false, "동료", 50f);
                if (go == null)
                    return;
                Debug.Log("[Ulon] 동료를 다시 세웠다 — 씬에 없어서 「동료 초대」가 오프라인에서 죽어 있었다.");
            }
            // 든 것: 검 하나만(플레이어는 검+방패다 — 실루엣이 갈린다).
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                if (IsGearName(t.name))
                    t.gameObject.SetActive(t.name == "1H_Sword");
            var mat = TintMaterial(CompanionObject, CompanionTint, go);
            if (mat != null)
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    if (!IsGearName(r.gameObject.name))
                        r.sharedMaterial = mat;
            Physics.SyncTransforms();
        }

        /// <summary>
        /// **한 사람이 무기 하나·방패 하나만 든다**(랩 ③ 실측 발견, 2026-09-07).
        ///
        /// 플레이어가 검 3자루와 방패 4개를 동시에 달고 있었다(잡몹 Knight도 방패 4개). `HideExtraGear`는
        /// 스폰 때만 도는데 **이미 저장된 씬에는 안 돌았다**. 잡몹 드레싱 게이트가 못 본 이유는
        /// 그 게이트가 **무기 개수만** 세고 방패는 무기로 안 치기 때문이다 — 세는 것만 보인다.
        /// 보스 무기(`BossWeapon*`)는 §10.2 표식이라 그것이 있으면 다른 무기는 전부 끈다.
        /// 멱등 — 무엇을 켜고 무엇을 끌지 이름 우선순위로 정하므로 몇 번 돌려도 같은 결과다.
        /// </summary>
        static readonly string[] GearKeepOrder =
        {
            "BossWeapon", "1H_Sword", "2H_Sword", "2H_Staff", "1H_Wand", "1H_Crossbow", "2H_Crossbow", "Dagger",
        };

        public static void EnsureGearDressed()
        {
            var actors = UnityEngine.Object.FindObjectsByType<CharacterController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            int fixedUp = 0;
            for (int a = 0; a < actors.Length; a++)
            {
                var root = actors[a].transform;
                // **꺼 둔 것까지 후보로 본다.** 보이는 것 중에서만 고르면 한 번 잘못 남은 선택이
                // 그대로 굳는다 — 플레이어가 「1H_Sword_Offhand」(왼손 검)만 든 채 굳었다(실측).
                // 다만 **원래 무장한 사람만** 손댄다(맨손 스켈레톤에게 무기를 쥐여 주지 않는다).
                var weapons = new List<Transform>();
                var shields = new List<Transform>();
                bool armed = false, shielded = false;
                // 장비를 **이름 원장 또는 손자리**로 찾는다(랩 ①) — 이름만 보면 새 팩의 새 이름이 샌다.
                foreach (var t in GearOnActor(root.gameObject))
                {
                    var r = t.GetComponent<Renderer>();
                    if (r == null)
                        continue;
                    bool visible = r.enabled && t.gameObject.activeInHierarchy;
                    if (t.name.IndexOf("Shield", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        shields.Add(t);
                        shielded |= visible;
                    }
                    else if (IsWeaponName(t.name) || UnderHandBone(t))
                    {
                        // 이름 목록에 없어도 **손에 쥐고 있으면 무기다** — 방패·화살통은 위에서 갈렸다.
                        weapons.Add(t);
                        armed |= visible;
                    }
                }
                // **이미 하나만 들고 있으면 그것을 존중한다.** 여기서 다시 고르면 앞선 패스(`DressMob`은
                // 「가장 큰 무기」를 고른다)의 결정을 덮어 **다른 몹과 같은 모습으로 합쳐진다** —
                // 실제로 기사(2H_Sword)를 1H_Sword로 바꿔 약탈자와 완전히 같아졌고 겹침 게이트가 잡았다.
                var visibleWeapons = new List<Transform>();
                for (int i = 0; i < weapons.Count; i++)
                    if (weapons[i].gameObject.activeInHierarchy)
                        visibleWeapons.Add(weapons[i]);
                var keepW = !armed ? null
                          : visibleWeapons.Count == 1 ? visibleWeapons[0]
                          : PickGear(weapons);
                var keepS = shielded ? PickShield(shields) : null;
                int off = 0;
                for (int i = 0; i < weapons.Count; i++)
                {
                    bool want = weapons[i] == keepW;
                    if (weapons[i].gameObject.activeSelf != want) { weapons[i].gameObject.SetActive(want); off++; }
                }
                for (int i = 0; i < shields.Count; i++)
                {
                    bool want = shields[i] == keepS;
                    if (shields[i].gameObject.activeSelf != want) { shields[i].gameObject.SetActive(want); off++; }
                }
                if (off > 0)
                {
                    fixedUp++;
                    Debug.Log("[Ulon] 장비 정리 " + root.name + " — 무기 " + weapons.Count + "·방패 " + shields.Count +
                              " 중 " + off + "개를 고쳤다(남긴 것: " + (keepW != null ? keepW.name : "없음") +
                              (keepS != null ? " + " + keepS.name : "") + ")");
                }
            }
            Debug.Log("[Ulon] 장비 정리 — 사람형 " + actors.Length + "체 중 " + fixedUp + "체를 「무기 1 + 방패 1」로 정리했다");
        }

        static Transform PickGear(List<Transform> gear)
        {
            // **보스 무기가 최우선이다**(§10.2 표식) — 정확한 이름 우선 규칙을 먼저 돌렸더니
            // 섀도우캡틴의 `BossWeapon_2H_Crossbow`를 끄고 모델 석궁을 남겨 보스 무기 게이트가 빨간불이었다.
            for (int i = 0; i < gear.Count; i++)
                if (gear[i].name.StartsWith(BossWeaponPrefix, StringComparison.Ordinal))
                    return gear[i];
            // **정확히 같은 이름이 먼저다** — 접두사만 보면 「1H_Sword」를 찾다가 「1H_Sword_Offhand」를
            // 집어 플레이어가 왼손 검만 든 채로 남았다(실측).
            for (int k = 0; k < GearKeepOrder.Length; k++)
                for (int i = 0; i < gear.Count; i++)
                    if (gear[i].name == GearKeepOrder[k])
                        return gear[i];
            for (int k = 0; k < GearKeepOrder.Length; k++)
                for (int i = 0; i < gear.Count; i++)
                    if (gear[i].name.StartsWith(GearKeepOrder[k], StringComparison.Ordinal))
                        return gear[i];
            return gear.Count > 0 ? gear[0] : null;
        }

        /// <summary>
        /// 방패 여럿 중 **하나를 고른다** — 이름(`Round_Shield`)이 아니라 **자리와 크기**로(랩 ②, 2026-09-09).
        ///
        /// 옛 자는 정확히 `Round_Shield`인 것을 먼저 집었다. 새 팩이 `Kite_Shield`·`Buckler`로 오면
        /// 그 자는 조용히 첫 번째를 집는다 — **이름 원장은 언제나 샌다.**
        /// 성질로 고친다: ①**손에 쥔 것**(손뼈 아래)이 먼저다 — 쥐지 않은 것은 등에 멘 장식이다
        /// ②그래도 여럿이면 **큰 것**을 남긴다(작은 것은 버클·장식 조각이다).
        /// </summary>
        /// <summary>게이트가 **같은 자**를 부르게 여는 창(자가 둘이면 갈린다).</summary>
        public static Transform PickShieldForCheck(List<Transform> shields) => PickShield(shields);

        static Transform PickShield(List<Transform> shields)
        {
            Transform best = null;
            float bestScore = -1f;
            for (int i = 0; i < shields.Count; i++)
            {
                var rends = shields[i].GetComponentsInChildren<Renderer>(true);
                if (rends.Length == 0)
                    continue;
                Bounds b = rends[0].bounds;
                for (int k = 1; k < rends.Length; k++)
                    b.Encapsulate(rends[k].bounds);
                float size = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                float score = (UnderHandBone(shields[i]) ? 100f : 0f) + size;   // 자리가 먼저, 크기가 다음
                if (score > bestScore)
                {
                    bestScore = score;
                    best = shields[i];
                }
            }
            return best != null ? best : (shields.Count > 0 ? shields[0] : null);
        }


        /// <summary>
        /// **몹 외형 원장**(검수 승인 2026-09-07 — 도적 교체·겹침 이동 대책).
        ///
        /// 모델만 바꾸면 **겹침이 옮겨간다**: 도적을 Rogue로 바꾸면 자객(Rogue/석궁)과 완전히 같아진다
        /// (Rogue FBX가 품은 무기는 석궁 2종뿐이다). 그래서 마을 5역할과 같은 두 축 —
        /// **든 것과 몸 색** — 을 여기 한 곳에 적고 적용한다.
        ///
        /// 기사↔약탈자도 같은 이유로 여기 있다: 방패 넷을 정리하고 나니 **둘이 완전히 같아졌다**
        /// (원래 방패 개수만 달랐다 — 그건 구분이 아니다). 겹침 게이트가 그 자리에서 잡았다.
        /// 「기대 모델」이 다르면 **지운다** — 다시 세우는 것은 `EnsureHuntMobs`·`EnsureDungeon2`다
        /// (지우기만 하는 패스를 만들지 않는다, 동료가 그렇게 사라졌다).
        /// </summary>
        public static readonly (string Object, string Fbx, string Gear, Color Tint, string Why)[] MobLooks =
        {
            // 마구간지기(갈 7A5733)도 같은 Rogue 몸에 한손 석궁을 든다 — 밝은 적갈로는 색 거리가
            // 0.12밖에 안 나 화면에서 안 갈렸다. 어두운 적갈로 내려 0.44를 벌었다(§8.1).
            ("Bandit",             null, "1H_Crossbow",  new Color(0.34f, 0.13f, 0.11f), "도적 — 어두운 적갈, 한손 석궁"),
            (Dungeon2.MobObject,   null, "1H_Crossbow",  new Color(0.34f, 0.13f, 0.11f), "던전 도적 — 마을판과 같은 종류(선언된 쌍)"),
            ("Rogue",              null, "2H_Crossbow",  new Color(0.28f, 0.32f, 0.40f), "자객 — 청회색, 두손 석궁(도적과 갈린다)"),
            ("Knight",             null, "1H_Sword",     new Color(0.62f, 0.66f, 0.72f), "기사 — 밝은 강철빛, 한손검+방패"),
            ("Raider",             null, "2H_Sword",     new Color(0.44f, 0.30f, 0.22f), "약탈자 — 흙빛, 두손검(기사와 갈린다)"),
            (Dungeon3.MobObject,   null, "2H_Sword",     new Color(0.44f, 0.30f, 0.22f), "던전 약탈자 — 마을판과 같은 종류(선언된 쌍)"),
        };

        /// <summary>기대 모델이 다른 몹을 지운다 — `EnsureHuntMobs`·`EnsureDungeon*`보다 **먼저** 돌아야 한다.</summary>
        public static void EnsureMobModelLedger()
        {
            var expect = new (string Object, string Prefix)[]
            {
                ("Bandit", "Rogue"),
                (Dungeon2.MobObject, "Rogue"),
            };
            int removed = 0;
            for (int i = 0; i < expect.Length; i++)
            {
                var go = GameObject.Find(expect[i].Object);
                if (go == null)
                    continue;
                if (MobArt.ModelOf(go, out MobArt.Model m, out _) && m.Prefix == expect[i].Prefix)
                    continue;
                Debug.Log("[Ulon] 몹 모델 교체 — " + expect[i].Object + "가 기대 모델(" + expect[i].Prefix +
                          ")이 아니라 헐고 다시 세운다");
                UnityEngine.Object.DestroyImmediate(go);
                removed++;
            }
            if (removed > 0)
                Debug.Log("[Ulon] 몹 모델 원장 — " + removed + "체를 교체 대상으로 지웠다(재생성은 EnsureHuntMobs·EnsureDungeon2)");
        }

        /// <summary>원장대로 몹의 든 것·몸 색을 맞춘다(멱등). `DressMob`·`EnsureGearDressed` 뒤에 돈다.</summary>
        public static void EnsureMobLooks()
        {
            int done = 0;
            for (int i = 0; i < MobLooks.Length; i++)
            {
                var go = GameObject.Find(MobLooks[i].Object);
                if (go == null)
                    continue;
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                {
                    if (!IsGearName(t.name) || t.name.StartsWith(BossWeaponPrefix, StringComparison.Ordinal))
                        continue;
                    bool shield = t.name.IndexOf("Shield", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (shield)
                        continue;                        // 방패는 `EnsureGearDressed`가 하나로 정리한다
                    t.gameObject.SetActive(t.name == MobLooks[i].Gear);
                }
                var mat = TintMaterial("Mob" + MobLooks[i].Object, MobLooks[i].Tint, go);
                if (mat != null)
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                        if (!IsGearName(r.gameObject.name))
                            r.sharedMaterial = mat;
                done++;
            }
            Debug.Log("[Ulon] 몹 외형 원장 — " + done + "체에 든 것·몸 색을 적용했다(모델만 바꾸면 겹침이 옮겨간다)");
        }

        /// <summary>역할별 몸 재질 — 모델 텍스처는 그대로 두고 색만 곱한다(질감을 잃으면 §8.2 위반이다).</summary>
        static Material TintMaterial(string host, Color tint, GameObject who)
        {
            Texture texture = null;
            foreach (var r in who.GetComponentsInChildren<Renderer>(true))
            {
                if (IsGearName(r.gameObject.name) || r.sharedMaterial == null)
                    continue;
                if (r.sharedMaterial.mainTexture != null)
                {
                    texture = r.sharedMaterial.mainTexture;
                    break;
                }
            }
            if (texture == null)
                return null;                        // 텍스처를 못 찾으면 칠하지 않는다(민무늬로 만들지 않는다)
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Game/Art/People"));
            string path = "Assets/Game/Art/People/" + host + "Tint.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.mainTexture = texture;
            mat.color = tint;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static bool ServiceNpc(string host, string fbx, string display, Vector3 offset, AnimatorController ctrl)
        {
            var go = GameObject.Find(host);
            if (go == null || AssetDatabase.LoadAssetAtPath<GameObject>(fbx) == null)
                return false;
            string name = host + "Npc";
            var old = GameObject.Find(name);
            if (old != null)
                UnityEngine.Object.DestroyImmediate(old);
            ConfigureHumanoid(fbx, true);
            var baseAt = HostAnchor(go.transform);   // 차양·굴뚝 같은 부속을 뺀 본체 중심(검수 판정 ③)
            var npc = SpawnActor(name, fbx, new Vector3(baseAt.x + offset.x, 0f, baseAt.z + offset.z), 1.75f, ctrl,
                false, false, display, 50f);
            if (npc == null)
                return false;
            HideExtraGear(npc);
            // **망토는 보스 표식이다**(§10.2, b2905b55) — 마을 사람이 걸치면 그 대비가 무너진다.
            // 치유사(Knight)가 붉은 망토를 걸치고 나와 화면에서 「경비」로 읽혔다(검수 판정 2026-09-07).
            foreach (var t in npc.GetComponentsInChildren<Transform>(true))
                if (IsCapeName(t.name))
                    t.gameObject.SetActive(false);
            npc.transform.SetParent(go.transform, true);
            return true;
        }

        /// <summary>로그인 스폰(0,0) 둘레에 부속을 놓지 않는 반경 — 스폰이 소품 위에 뜨는 것을 막는다.</summary>
        const float SpawnClearRadius = 2.6f * KitScale;   // 비울 자리도 킷과 같은 배로(랩 B)

        /// <summary>
        /// 부속을 붙일 **기준점** — 시설 본체(부속·사람·불꽃을 뺀 것)의 보이는 중심.
        ///
        /// 예전엔 시설 전체 바운드의 중심을 썼다. 그러면 **부속이 이미 붙은 상태에서 다시 돌 때마다
        /// 기준이 부속 쪽으로 밀려** 실행할수록 조각이 흩어진다(실측: 화덕 부속이 5.4m로 벌어졌다 —
        /// 근접 샷이 마을 전경이 돼서야 드러났다). 멱등 패스는 **자기 출력물을 입력으로 먹으면 안 된다.**
        /// </summary>
        /// <summary>
        /// 시설의 **본체 시각을 치운다** — 역할과 다른 물건이 몸통일 때 쓴다(낚시터=물레방아, 화덕=등불).
        /// 부속(FacPart*)·사람(*Npc)·불꽃은 남긴다. 콜라이더·컴포넌트는 그대로 두고 **보이는 것만** 없앤다.
        /// </summary>
        static void HideOwnVisual(string hostName)
        {
            var host = GameObject.Find(hostName);
            if (host == null)
                return;
            // **본체 오브젝트 자신에게 달린 렌더러도 있다** — 화덕은 등불 메시가 자식이 아니라 루트에
            // 붙어 있어, 자식만 치웠더니 불 한가운데 등불 기둥이 그대로 서 있었다(검수 확인 요청).
            foreach (var r in host.GetComponents<Renderer>())
                r.enabled = false;
            for (int c = host.transform.childCount - 1; c >= 0; c--)
            {
                var ch = host.transform.GetChild(c);
                if (ch.name.StartsWith("FacPart", StringComparison.Ordinal) ||
                    ch.name.StartsWith("YardFence", StringComparison.Ordinal) ||
                    ch.name == CampfireFlameObject || ch.name.EndsWith("Npc", StringComparison.Ordinal))
                    continue;
                UnityEngine.Object.DestroyImmediate(ch.gameObject);
            }
        }

        /// <summary>
        /// **네거티브 컨트롤 전용 스위치** — 켜면 `HostAnchor`가 옛 방식(시설 **전체** 바운드)으로 돌아간다.
        /// 멱등 실측 게이트가 「자기참조를 되살리면 좌표가 실제로 밀리는가」를 증명하는 데만 쓴다.
        /// 평시엔 false이고, 게이트가 켠 뒤 반드시 되돌린다.
        /// </summary>
        public static bool AnchorSelfReferenceForNc;

        static Vector3 HostAnchor(Transform host)
        {
            if (AnchorSelfReferenceForNc)
            {
                if (BoundsOf(host, true, out Bounds nc) && nc.size.sqrMagnitude > 0.0001f)
                    return new Vector3(nc.center.x, host.position.y, nc.center.z);
                return host.position;
            }
            bool any = false;
            Bounds box = new Bounds();
            var rends = host.GetComponentsInChildren<Renderer>(false);
            for (int i = 0; i < rends.Length; i++)
            {
                if (rends[i] is ParticleSystemRenderer)
                    continue;
                bool mine = false;
                for (var p = rends[i].transform; p != null && p != host; p = p.parent)
                    if (p.name.StartsWith("FacPart", StringComparison.Ordinal) ||
                        p.name.StartsWith("YardFence", StringComparison.Ordinal) ||
                        p.name == CampfireFlameObject || p.name.EndsWith("Npc", StringComparison.Ordinal))
                    { mine = true; break; }
                if (mine)
                    continue;
                if (!any) { box = rends[i].bounds; any = true; }
                else box.Encapsulate(rends[i].bounds);
            }
            return any ? new Vector3(box.center.x, host.position.y, box.center.z) : host.position;
        }

        /// <summary>시설에 부속 조각 하나를 붙인다(멱등 — 이름이 같으면 헐고 다시 붙인다).</summary>
        static bool FacilityPart(string host, string part, string fbx, Vector3 offset, float size, bool byHeight)
        {
            var go = GameObject.Find(host);
            if (go == null)
                return false;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(fbx) == null && fbx.EndsWith(".fbx", StringComparison.Ordinal))
                ConfigureProp(fbx);
            string name = "FacPart" + part;
            for (int c = go.transform.childCount - 1; c >= 0; c--)
                if (go.transform.GetChild(c).name == name)
                    UnityEngine.Object.DestroyImmediate(go.transform.GetChild(c).gameObject);
            // 기준은 **보이는 것의 중심**이다 — 오브젝트 원점이 (0,0,0)에 남아 있는 시설이 있어(실측)
            // transform.position에 붙였더니 조각이 마을 광장 스폰 자리에 떨어져 스폰이 1.35m 떴다.
            Vector3 baseAt = HostAnchor(go.transform);
            // 오프셋은 **미터 그대로** 둔다. 배율에 태워 봤더니(`Module(offset)`, 2026-09-09) 부두
            // 낚싯대가 2.1배로 밀려 물 위로 나가 「발이 지표에서 0.27m 벗어남」 빨간불이 났다 —
            // 조각 **크기**도, 조각끼리의 배치(모닥불 돌 ±0.55m, 부두 위 낚싯대)도 전부 절대 미터라
            // 자리만 배율에 태우면 한 덩이로 있어야 할 것들이 흩어진다.
            // 대신 진짜 성질을 지킨다: **부속은 몸통 밖에 선다**(아래 `PushClearOfHost`) —
            // 배율이 얼마가 되든 몸통이 커진 만큼 부속이 비켜난다.
            var at = new Vector3(baseAt.x, go.transform.position.y, baseAt.z) + offset;
            // **스폰 자리를 막지 않는다** — 목공소가 마을 광장 한복판(0,0)에 서 있어 널빤지가 스폰 위에
            // 떨어졌고, 로그인 직후 플레이어가 1.35m 떠서 착지했다(2026-09-07 실측).
            var flat = new Vector2(at.x, at.z);
            if (flat.magnitude < SpawnClearRadius)
            {
                var dir = flat.sqrMagnitude > 0.0001f ? flat.normalized : new Vector2(1f, 0f);
                at = new Vector3(dir.x * SpawnClearRadius, at.y, dir.y * SpawnClearRadius);
            }
            at = OnGround(new Vector3(at.x, 0f, at.z));
            var made = RoomPropObject(go.transform, name, fbx, at, 0f, size, byHeight);
            // **납작한 조각을 높이 기준으로 키우면 바닥이 폭발한다** — 널빤지를 높이 1.3m로 맞췄더니
            // 21.7×21.7m가 돼 마을 광장과 스폰을 덮었다(2026-09-07 실측). 조용히 넘어가지 않게 막는다.
            if (made != null && BoundsOf(made.transform, true, out Bounds gb) && Mathf.Max(gb.size.x, gb.size.z) > 6f)
                throw new InvalidOperationException("시설 부속 " + host + "/" + part + "이 " +
                    gb.size.ToString("0.0") + "로 커졌습니다 — 납작한 조각은 높이가 아니라 폭으로 맞춰라.");
            // 조각 원점이 모서리인 프리팹이 있다 — 바운드 중심으로 다시 맞춘다(안 맞추면 스폰 자리로 되돌아온다).
            if (made != null && BoundsOf(made.transform, true, out Bounds mb))
                made.transform.position += new Vector3(at.x - mb.center.x, 0f, at.z - mb.center.z);
            if (made != null)
                PushClearOfHost(go.transform, made.transform, offset);
            return made != null;
        }

        /// <summary>지금 세운 은행 벽의 실제 높이(조각 비율이 바뀌어도 지붕이 따라 올라가게).</summary>
        static float BankWallHeight(GameObject bank)
        {
            float top = 0f;
            var rends = bank.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
                // 프리팹 메시 노드 이름은 전부 "Visual"이다 — **부모 이름**으로 지붕을 가려야 한다
                // (renderer.name으로 보면 지붕이 안 걸러져 굴뚝이 허공에 뜬다, 2026-09-07 실측).
                if (rends[i].transform.parent != null
                    && rends[i].transform.parent.name.IndexOf("Roof", StringComparison.Ordinal) < 0)
                    top = Mathf.Max(top, rends[i].bounds.max.y - bank.transform.position.y);
            return top;
        }

        /// <summary>입구 세 곳의 뿌리·좌표·접근 방향 원장 — 빌더와 유지보수 패스가 같은 목록을 본다.</summary>
        public static readonly (string Root, float X, float Z, float Yaw)[] EntranceSpots =
        {
            (Dungeon1.RootObject, Dungeon1.EntranceX, Dungeon1.EntranceZ, 90f),
            (Dungeon2.RootObject, Dungeon2.EntranceX, Dungeon2.EntranceZ, -90f),
            (Dungeon3.RootObject, Dungeon3.EntranceX, Dungeon3.EntranceZ, 45f),
        };

        /// <summary>
        /// 이미 씬에 저장된 입구 문틀도 등록 조각으로 다시 세운다(멱등).
        /// `EnsureDungeon*`는 루트가 있으면 통째로 다시 짓지만 셀프체크 경로에서는 호출되지 않는다 —
        /// 빌더만 고치면 **옛 씬의 검은 큐브는 영영 그대로 남는다**(보스 무기·마을 페이드에서 겪은 함정).
        /// </summary>
        public static void EnsureEntranceFramesQualified()
        {
            int rebuilt = 0;
            for (int s = 0; s < EntranceSpots.Length; s++)
            {
                var spot = EntranceSpots[s];
                var root = GameObject.Find(spot.Root);
                if (root == null)
                    continue;                                   // 아직 안 지어진 던전 — 게이트가 따로 실패시킨다
                for (int c = root.transform.childCount - 1; c >= 0; c--)
                {
                    var child = root.transform.GetChild(c);
                    if (child.name == EntranceFrameObject)
                        UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
                BuildEntranceFrame(root.transform, new Vector3(spot.X, 0f, spot.Z), spot.Yaw);
                rebuilt++;
            }
            Debug.Log("[Ulon] 입구 문틀 재건 " + rebuilt + "곳 — 등록 CC0 조각(돌기둥·벽 블록)으로");
        }

        /// <summary>마을에서 던전 3이 보이도록 세우는 이정표(검수 P0-2 「우연히라도 찾을 단서가 없다」).</summary>
        public static void EnsureDungeon3Signpost(Transform parent)
        {
            const string Poles = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/poles.fbx";
            const string Banner = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/banner-red.fbx";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Poles) == null)
                ConfigureProp(Poles);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Banner) == null)
                ConfigureProp(Banner);
            var pos = new Vector3(Dungeon3.SignX, 0f, Dungeon3.SignZ);
            var go = Place(Poles, pos, new Vector3(0f, 225f, 0f));
            if (go == null)
                return;
            go.name = Dungeon3.SignObject;
            go.transform.SetParent(parent, true);
            Decor(parent, Banner, pos + new Vector3(0.6f, 0f, 0.6f), new Vector3(0f, 225f, 0f));
        }

        /// <summary>
        /// 기획서 §10.2 「보스는 일반 모델을 1.3~1.5배 확대하고 **머리장식·큰 무기·VFX·전용 기술**로 차별화한다」.
        /// 크기만으로는 같은 FBX를 쓰는 잡몹과 구분이 안 된다(검수 2026-09-06 반려 2).
        /// 새 에셋 없이 기존 파츠·스케일·조명으로 세 가지를 붙인다: 왕관(머리장식)·무기 확대·오라(VFX).
        /// </summary>
        public const string BossCrownObject = "BossCrown";
        public const string BossAuraObject = "BossAura";
        public const string BossWeaponPrefix = "BossWeapon_";

        /// <summary>
        /// 이미 씬에 있는 보스 4종에도 §10.2 차별화를 다시 적용한다(멱등).
        /// 스폰 Ensure*는 오브젝트가 있으면 일찍 반환하므로, 그 경로로만 두면 옛 보스는 영영 안 고쳐진다
        /// (헥사크가 무기 없이 남아 있던 이유다 — 검수 2026-09-06 반려).
        /// </summary>
        /// <summary>
        /// 이미 씬에 저장된 **잡몹**도 다시 꾸민다 — 스폰 Ensure*는 오브젝트가 있으면 일찍 반환하므로
        /// 드레싱을 스폰 경로에만 두면 옛 몹은 영영 술잔을 든 채 남는다(보스에서 이미 겪은 함정).
        /// </summary>
        /// <summary>사냥터 잡몹의 자리 — 좌표 원장(빌더·게이트 공용). z를 다 같이 두면 「일직선 진열」이 된다.</summary>
        public static readonly (string Name, float X, float Z, float Yaw)[] HuntSpots =
        {
            // z를 마을에서 20m 북으로 밀었다(검수 지시 2026-09-07) — 56 샷에서 몹이 담장에 붙어
            // 「마을에 몹이 산다」로 읽혔다. 흩어진 모양(x·z 산포)은 그대로 두고 통째로 옮긴다.
            // 자리를 **보는 눈에서 벌렸다**(검수 경증 2026-09-09: 왼쪽 둘이 한 덩이로 읽힘).
            // 월드 거리로는 4m 떨어져 있어도 시선 방향으로 늘어서면 화면에서는 겹친다 —
            // 그래서 `HuntViewEye` 기준 **방위각**을 −30°~+33°에 6.9° 이상 간격으로 흩고,
            // 이웃한 방위각끼리는 **거리를 번갈아**(11~17m) 둬 활 모양으로 안 보이게 했다.
            ("Skeleton", -0.4f, 36.2f, 168f),
            ("Bandit", -1.5f, 31.7f, 196f),
            ("Raider", 2.2f, 32.2f, 152f),
            ("Rogue", -5.5f, 35.3f, 208f),
            ("Knight", 4.4f, 35.9f, 174f),
            ("Acolyte", 5.9f, 32.5f, 160f),
            ("Minion", 8.8f, 35.8f, 186f),
            ("SkelRogue", 9.5f, 31.4f, 150f),
        };

        /// <summary>이름으로 사냥터 자리를 찾는다(y는 지표에서 유도) — 원장에 없으면 **세우지 않는다**
        /// (조용히 0,0에 세우면 마을 한복판에 몹이 선다).</summary>
        public static Vector3 HuntSpotOf(string name)
        {
            for (int i = 0; i < HuntSpots.Length; i++)
                if (HuntSpots[i].Name == name)
                    return HuntSpotWorld(i);
            throw new System.InvalidOperationException("사냥터 자리 원장에 " + name + "이(가) 없습니다.");
        }

        /// <summary>
        /// 사냥터 자리의 **월드 좌표** — 원장은 마을과 같은 모듈 격자로 적혀 있다(랩 B).
        /// 마을이 킷 배율만큼 넓어지면 사냥터도 같은 배로 물러나야 「마을에 몹이 산다」가 안 된다
        /// (실측: 배율 뒤 몹이 마을 담장에서 7.3m까지 다가와 게이트가 울었다).
        /// </summary>
        public static Vector3 HuntSpotWorld(int i)
        {
            return Module(new Vector3(HuntSpots[i].X, 0f, HuntSpots[i].Z));
        }

        /// <summary>사냥터를 **보는 눈**(마을 쪽 남에서 북을 본다) — QA 샷 카메라(`03_hunt_mobs`)와
        /// 겹침 게이트가 이 한 자리를 같이 쓴다. 재는 자와 찍는 자가 다르면 초록인데 화면은 겹친다.</summary>
        public static readonly Vector2 HuntViewEye = new Vector2(2.8f, 21f) * KitScale;
        public const float HuntViewEyeHeight = 6.0f;
        /// 도포 원장(`WorldSplat.HuntGround`)과 **같은 자리**를 본다 — 바닥이 닳은 곳과 몹이 도는 곳이
        /// 어긋나면 「밟힌 자리」가 아니라 「얼룩 옆에 선 몹」이 된다.
        public static readonly Vector2 HuntViewTarget = Ulon.Shared.WorldSplat.HuntGround;
        public const float HuntViewTargetHeight = 1.0f;

        /// <summary>
        /// 사냥터 잡몹을 흩고 **지표에 세운다**. 두 가지가 겹쳐 있었다:
        ///   1) 전부 z=13.2 한 줄(옛 「7종 일직선 진열」 반려의 잔존),
        ///   2) 지형을 LandBase 10m로 올린 뒤에도 y=0에 남아 **땅속에 묻혀** 있었다 —
        ///      03_hunt_mobs 샷에 몹이 2마리만 보인 진짜 이유다(검수 2026-09-06 관찰).
        /// 배치 Ensure*는 오브젝트가 있으면 일찍 반환하므로 이 보수 패스가 따로 필요하다.
        /// </summary>
        public static void EnsureHuntMobPlacement()
        {
            int moved = 0;
            for (int i = 0; i < HuntSpots.Length; i++)
            {
                var spot = HuntSpots[i];
                var go = GameObject.Find(spot.Name);
                if (go == null)
                    continue;
                Vector3 world = HuntSpotWorld(i);
                float y = GroundHeightAt(world.x, world.z);
                var want = new Vector3(world.x, y, world.z);
                if ((go.transform.position - want).sqrMagnitude > 0.0001f)
                    moved++;
                go.transform.position = want;
                go.transform.rotation = Quaternion.Euler(0f, spot.Yaw, 0f);
            }
            Debug.Log("[Ulon] 사냥터 배치 — " + HuntSpots.Length + "체 흩기·지표 세우기(옮긴 것 " + moved + "체)");
        }

        /// <summary>이 좌표의 지형 표면 높이(월드).</summary>
        public static float GroundHeightAt(float x, float z)
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null)
                return 0f;
            return terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
        }

        public static void EnsureMobDressing()
        {
            int n = 0;
            var bodies = UnityEngine.Object.FindObjectsByType<WorldBody>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < bodies.Length; i++)
            {
                var b = bodies[i];
                if (b == null || !b.IsEnemy)
                    continue;
                if (b.name == Dungeon1.BossObject || b.name == Dungeon2.BossObject || b.name == Dungeon3.BossObject || b.name == FieldBoss.Object)
                    continue;                                  // 보스는 DressBoss가 맡는다
                DressMob(b.gameObject);
                n++;
            }
            Debug.Log("[Ulon] 잡몹 드레싱 — " + n + "체(손 소품 제거·무기 1개·의상 켜기)");
        }

        public static void EnsureBossDressing()
        {
            DressBossNamed(Dungeon1.BossObject, new Color(0.55f, 0.85f, 1f));
            DressBossNamed(Dungeon2.BossObject, new Color(0.65f, 0.35f, 1f));
            DressBossNamed(Dungeon3.BossObject, new Color(1f, 0.45f, 0.2f));
            DressBossNamed(FieldBoss.Object, new Color(0.35f, 1f, 0.6f));
        }

        static void DressBossNamed(string objectName, Color tint)
        {
            var go = GameObject.Find(objectName);
            if (go != null)
                DressBoss(go, tint);
        }

        /// <summary>손에 드는 소품(무기 아님) — 이게 켜져 있고 무기가 꺼져 있으면 몹이 술잔을 들고 서 있다.</summary>
        public static bool IsHandPropNamePublic(string n) => IsHandPropName(n);

        static bool IsHandPropName(string n)
        {
            return n.IndexOf("Mug", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Bottle", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Cup", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Bread", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Torch", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// 잡몹 드레싱 — 보스만 꾸미고 잡몹은 FBX 기본 상태로 뒀더니 던전 3 야만인이 **술잔을 든 맨몸**으로
        /// 서 있었다(검수 2026-09-06 반려: 「왕관 쓴 보스 옆에 벗은 마네킹」). 하는 일:
        ///   1) 손 소품(잔·병 등)을 끄고 **무기 하나**를 켠다(1몹 1무기).
        ///   2) 모델이 가진 의상 메시(망토·모자·갑옷·후드)를 켠다 — 맨몸으로 두면 §8.1 실루엣이 안 읽힌다.
        /// </summary>
        public static void DressMob(GameObject actor)
        {
            if (actor == null)
                return;
            var all = actor.GetComponentsInChildren<Transform>(true);

            // 1) 무기 — 가장 큰 것 하나만 켜고 나머지 무기·손 소품은 끈다.
            Transform pick = null;
            float bestLen = -1f;
            for (int i = 0; i < all.Length; i++)
            {
                if (!IsWeaponName(all[i].name))
                    continue;
                var mf = all[i].GetComponentsInChildren<MeshFilter>(true);
                float len = 0f;
                for (int m = 0; m < mf.Length; m++)
                    if (mf[m].sharedMesh != null)
                        len = Mathf.Max(len, mf[m].sharedMesh.bounds.size.magnitude);
                if (len > bestLen)
                {
                    bestLen = len;
                    pick = all[i];
                }
            }
            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (IsHandPropName(t.name))
                {
                    t.gameObject.SetActive(false);
                    continue;
                }
                if (!IsWeaponName(t.name))
                    continue;
                bool keep = pick != null && (t == pick || t.IsChildOf(pick) || pick.IsChildOf(t));
                t.gameObject.SetActive(keep);
                if (keep)
                {
                    var rs = t.GetComponentsInChildren<Renderer>(true);
                    for (int r = 0; r < rs.Length; r++)
                        rs[r].enabled = true;
                }
            }
            if (pick != null)
                for (var t = pick; t != null && t != actor.transform; t = t.parent)
                    t.gameObject.SetActive(true);

            // 2) 의상 — 모델이 가진 것을 켠다. 단 **망토는 보스 전용**이다(검수 2026-09-06 반려):
            // 잡몹과 보스가 같은 모델일 때 망토까지 같으면 플레이 거리에서 「작은 보스」로 읽힌다(§8.1).
            for (int i = 0; i < all.Length; i++)
            {
                if (!IsClothingName(all[i].name))
                    continue;
                bool cape = IsCapeName(all[i].name);
                all[i].gameObject.SetActive(!cape);
                var rs = all[i].GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < rs.Length; r++)
                    rs[r].enabled = !cape;
            }

            // 3) 색 — 잡몹은 어두운 흙색 계열로 낮춘다. 보스는 원래 색을 유지하므로 같은 모델이라도 갈라진다.
            TintCharacter(actor, MobDrabName, MobDrabTint);
        }

        /// <summary>보스 전용 표식인 망토·클로크인가(잡몹은 끈다).</summary>
        public static bool IsCapeName(string n)
        {
            return n.IndexOf("Cape", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Cloak", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public const string MobDrabName = "MobDrab";
        public static readonly Color MobDrabTint = new Color(0.46f, 0.42f, 0.36f);

        /// <summary>
        /// 캐릭터 메시(무기 제외)에 색을 입힌다 — 원본 텍스처는 그대로 두고 **색만** 곱한 재질 에셋을 만든다.
        /// 다운로드 없이 실루엣·색으로 잡몹과 보스를 가르기 위한 최소 수단(검수 2026-09-06 지시 순서 2).
        /// </summary>
        public static void TintCharacter(GameObject actor, string matName, Color tint)
        {
            if (actor == null)
                return;
            var rends = actor.GetComponentsInChildren<Renderer>(true);
            Texture source = null;
            Shader shader = null;
            for (int i = 0; i < rends.Length && source == null; i++)
            {
                var m = rends[i].sharedMaterial;
                if (m != null && m.mainTexture != null) { source = m.mainTexture; shader = m.shader; }
            }
            if (source == null)
                return;
            string matPath = "Assets/Game/Art/Env/" + matName + "_" + source.name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader != null ? shader : Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.shader = shader != null ? shader : Shader.Find("Standard");
            mat.mainTexture = source;
            mat.color = tint;
            EditorUtility.SetDirty(mat);

            for (int i = 0; i < rends.Length; i++)
            {
                if (IsWeaponName(rends[i].name) || (rends[i].transform.parent != null && IsWeaponName(rends[i].transform.parent.name)))
                    continue;                                   // 무기는 원래 색 그대로
                var slots = rends[i].sharedMaterials;
                for (int sIdx = 0; sIdx < slots.Length; sIdx++)
                    if (slots[sIdx] != null && slots[sIdx].mainTexture == source)
                        slots[sIdx] = mat;
                rends[i].sharedMaterials = slots;
            }
        }

        /// <summary>의상 메시 이름인가(게이트도 같은 판정을 쓴다).</summary>
        public static bool IsClothingName(string n)
        {
            return n.IndexOf("Cape", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Cloak", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Hood", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Hat", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Helmet", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Armor", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Tunic", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// 무기 최저점이 발밑 바닥보다 아래면, **그립 점을 축으로** 칼끝을 들어 여유 0.10m를 만든다.
        /// 게이트(`AssertGearAboveFloor`)와 **같은 자**로 바닥을 잡는다 — 재는 자와 맞추는 자가 둘이면
        /// 한쪽만 통과하는 일이 생긴다. 도는 방향은 짐작하지 않고 **양쪽을 재서** 올라가는 쪽을 고른다.
        /// </summary>

        /// <summary>자루 끝·날 끝이 손뼈에서 각각 얼마나 떨어져 있는지 — 「쥐었다」는 자루가 손 안일 때다.</summary>
        static void LogGripFit(GameObject actor, Transform weapon, Transform handBone, string when)
        {
            if (actor == null || weapon == null || handBone == null)
                return;
            if (!BossFit.WeaponAxis(weapon, out Vector3 grip, out Vector3 tip))
                return;
            var sb = new System.Text.StringBuilder();
            for (var t = weapon; t != null && t != actor.transform; t = t.parent)
                sb.Insert(0, "/" + t.name);
            Debug.Log("[Ulon] 무기 쥠새(" + when + ") " + actor.name + " — 무기 " + sb +
                      " 손뼈 " + handBone.name +
                      " 크기 " + (BoundsOfEnabled(weapon, out Bounds wbb) ? wbb.size.ToString("0.00") : "?") +
                      " — 자루↔손 " + Vector3.Distance(grip, handBone.position).ToString("0.00") +
                      "m · 날끝↔손 " + Vector3.Distance(tip, handBone.position).ToString("0.00") + "m");
            if (false) Debug.Log("[Ulon] 무기 쥠새(" + when + ") " + actor.name +
                      " — 자루↔손 " + Vector3.Distance(grip, handBone.position).ToString("0.00") +
                      "m · 날끝↔손 " + Vector3.Distance(tip, handBone.position).ToString("0.00") + "m");
        }

        static void LiftWeaponAboveFloor(GameObject actor, Transform weapon, Transform handBone)
        {
            // **「손에 가깝다」와 「쥐었다」는 다르다**(검수 판정 2026-09-08). 회전 때문에 자루가 손을
            // 빠져나간 것인지 원래 그랬는지는 **짐작하지 말고 회전 전후를 재서** 남긴다.
            LogGripFit(actor, weapon, handBone, "회전 전");
            const float Want = 0.10f;
            if (actor == null || weapon == null)
                return;
            if (!GroundFit.BodyBounds(actor.transform, out Bounds body))
                return;
            if (!GroundFit.SurfaceUnder(actor.transform, body, out float floorY, out _))
                return;
            if (!BoundsOfEnabled(weapon, out Bounds start) || start.min.y >= floorY + Want)
                return;

            float sign = 0f;
            for (int step = 0; step < 36; step++)
            {
                if (!BoundsOfEnabled(weapon, out Bounds cur) || cur.min.y >= floorY + Want)
                    break;
                if (!BossFit.WeaponAxis(weapon, out Vector3 grip, out Vector3 tip))
                    break;
                var axis = Vector3.Cross((tip - grip).normalized, Vector3.up);
                if (axis.sqrMagnitude < 1e-6f)
                    break;
                axis = axis.normalized;
                if (sign == 0f)
                {
                    // 어느 쪽으로 돌아야 칼끝이 올라가는지 **재서** 고른다.
                    weapon.RotateAround(grip, axis, 5f);
                    bool up = BoundsOfEnabled(weapon, out Bounds probe) && probe.min.y > cur.min.y;
                    weapon.RotateAround(grip, axis, -5f);
                    sign = up ? 1f : -1f;
                }
                weapon.RotateAround(grip, axis, 5f * sign);
            }
            LogGripFit(actor, weapon, handBone, "회전 후");
            if (BoundsOfEnabled(weapon, out Bounds end))
                Debug.Log("[Ulon] 보스 무기 들어올림 — " + actor.name + " 최저점 " +
                          (start.min.y - floorY).ToString("0.00") + "m → " + (end.min.y - floorY).ToString("0.00") + "m (바닥 기준)");
        }

        public static void DressBoss(GameObject boss, Color tint)
        {
            if (boss == null)
                return;

            // 멱등하게 — 이전에 붙은 왕관·오라는 지우고 다시 만든다(위치 규칙이 바뀌면 옛 것이 남는다).
            var old = boss.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < old.Length; i++)
            {
                if (old[i] == null)
                    continue;
                if (old[i].name == BossCrownObject || old[i].name == BossAuraObject)
                    UnityEngine.Object.DestroyImmediate(old[i].gameObject);
            }

            // 1) 큰 무기 — 손에 든 장비를 키운다. 없으면 손 소켓에 검을 새로 붙인다.
            //    개명만 하고 손에 아무것도 없는 보스가 게이트를 통과했다(검수 2026-09-06 반려).
            var weapon = FindGearTransform(boss);
            if (weapon == null)
            {
                AttachGear(boss, SwordFbx, null);
                weapon = FindGearTransform(boss);
            }
            if (weapon == null)
                weapon = AttachWeaponToHand(boss);   // EquipmentSockets가 없는 보스(헥사크)용 폴백
            if (weapon != null)
            {
                // 화면에 보이는 것이 목적이다 — 개명만 되고 꺼져 있거나 부모가 꺼진 무기가 있었다(헥사크의 완드).
                for (var t = weapon; t != null && t != boss.transform; t = t.parent)
                    t.gameObject.SetActive(true);
                var wrends = weapon.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < wrends.Length; i++)
                {
                    wrends[i].enabled = true;
                    wrends[i].gameObject.SetActive(true);
                }
                if (!weapon.name.StartsWith(BossWeaponPrefix, StringComparison.Ordinal))
                {
                    weapon.localScale = weapon.localScale * 1.55f;
                    weapon.name = BossWeaponPrefix + weapon.name;
                }
                // **손에 매단다.** 장비 소켓에 붙어 있던 무기는 키우고 나면 얼굴 옆에 떠 있는 판때기로 보였다
                // (검수 2026-09-06 관찰). 게이트도 「존재」가 아니라 손과의 거리로 잰다.
                var handBone = FindHandBone(boss);
                if (handBone != null && weapon.parent != handBone)
                {
                    var keepScale = weapon.localScale;
                    weapon.SetParent(handBone, false);
                    weapon.localPosition = Vector3.zero;
                    weapon.localRotation = Quaternion.identity;
                    weapon.localScale = keepScale;
                }

                // 완드처럼 작은 장비는 1.55배로도 「큰 무기」로 안 읽힌다 — 몸 높이의 0.45배까지 키운다.
                var ccw = boss.GetComponent<CharacterController>();
                float wantLen = (ccw != null ? ccw.height : 2f) * 0.45f;
                Bounds wb;
                if (BoundsOfEnabled(weapon, out wb))
                {
                    float len = Mathf.Max(wb.size.x, Mathf.Max(wb.size.y, wb.size.z));
                    if (len > 0.01f && len < wantLen)
                        weapon.localScale = weapon.localScale * Mathf.Min(3.5f, wantLen / len);
                }
                // 무기 덩어리 한가운데를 손에 맞추면 **칼이 얼굴 옆에 가로로 뜬다**(검수 2026-09-06 반려 2):
                // 손 안에 들어와야 하는 건 무기 전체가 아니라 **그립 끝**이고, 칼날은 팔뚝 방향으로 뻗어야 한다.
                // **손 슬롯이 있으면 슬롯을 믿는다**(검수 판정 2026-09-08 「손에 가깝다 ≠ 쥐었다」).
                // 실측으로 안 것: 무기는 `…/hand.r/handslot.r/` 밑에 달려 있다 — 킷이 **쥐라고 만들어 둔 자리**다.
                // 그런데 아래 맞춤은 무기 **바운드 끝**을 손뼈 위치로 끌어다 놓는다. 칼자루는 메시의
                // 바운드 끝이 아니므로, 손은 물건의 맨 끝을 잡고 칼몸은 팔뚝 옆으로 밀려난다
                // (사진에서 「자루가 손 안에 없고 날 밑동이 소매에 닿은」 그 모습).
                // 회전 전후를 재 보니 자루↔손 0.00m로 **같았다** — 회전이 만든 것이 아니라
                // 처음부터 이 맞춤이 만든 것이다(그리고 그 자를 그대로 게이트로 쓰면 언제나 통과다).
                bool inHandSlot = handBone != null && handBone.name.StartsWith("handslot", StringComparison.OrdinalIgnoreCase);
                if (!inHandSlot && handBone != null && BossFit.WeaponAxis(weapon, out Vector3 grip, out Vector3 tip))
                {
                    var along = tip - grip;
                    var fore = BossFit.ForearmDir(handBone, boss.transform);
                    if (along.sqrMagnitude > 1e-6f)
                        weapon.rotation = Quaternion.FromToRotation(along.normalized, fore) * weapon.rotation;
                    if (BossFit.WeaponAxis(weapon, out grip, out tip))
                        weapon.position += handBone.position - grip;
                }
                // **칼끝을 바닥에서 든다**(검수 판정 2026-09-08: `BoneWarden/Visual 0.28m` 매몰).
                // 보스는 키가 1.3~1.5배, 무기는 그 위에 또 최대 3.5배까지 커진다 — 손에 제대로
                // 들려 있어도 팔뚝 방향으로 뻗은 칼끝이 바닥 밑으로 내려간다.
                // 고치는 자리는 **그립이 아니라 각도**다: 그립 점을 축으로 돌리면 손과의 관계는 그대로고
                // (같은 함수가 바로 위에서 맞춘 것을 안 깨뜨린다) 칼끝만 올라온다.
                LiftWeaponAboveFloor(boss, weapon, handBone);
            }

            // 1-b) **1몹 1무기**(P1 #7) — 보스 드레싱이 큰 무기를 새로 붙이면 원래 들고 있던 칼이
            //      같은 손에 그대로 남는다(기사 보스: 1H_Sword + BossWeapon_2H_Sword가 둘 다 켜져 있었다).
            //      HideExtraGear는 "1H_Sword"를 keep 목록에 두므로 이 경로를 못 잡는다.
            if (weapon != null)
            {
                var gears = boss.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < gears.Length; i++)
                {
                    var g = gears[i];
                    if (g == null || !IsWeaponName(g.name))
                        continue;
                    if (g == weapon || g.IsChildOf(weapon) || weapon.IsChildOf(g))
                        continue;
                    g.gameObject.SetActive(false);
                }
            }

            // 2) 머리장식 — 머리 **위**에 얹는다.
            //    렌더러 바운드(b.max.y)로 얹었더니 어깨 높이에 수평으로 떠서 접시처럼 보였다
            //    (에디터에서 스킨드 바운드는 못 믿는다 — 실루엣 게이트에서 이미 겪은 함정이다).
            //    CharacterController가 스폰 시 원장 키를 그대로 받으므로 그걸 머리 기준으로 쓴다.
            var crownMat = MakeNoiseMat("BossCrown", new Color(0.62f, 0.48f, 0.12f), new Color(0.86f, 0.72f, 0.26f));
            Bounds b;
            bool hasBounds = BoundsOf(boss.transform, true, out b);
            var cc = boss.GetComponent<CharacterController>();
            float headY;
            float headR;
            Vector3 axis;
            if (cc != null && cc.height > 0.01f)
            {
                axis = boss.transform.position + cc.center;
                headY = boss.transform.position.y + cc.center.y + cc.height * 0.5f;
                headR = Mathf.Max(0.16f, cc.radius * 0.62f);
            }
            else
            {
                if (!hasBounds)
                    return;
                axis = b.center;
                headY = b.max.y;
                headR = Mathf.Max(0.16f, b.size.x * 0.22f);
            }
            // CharacterController 캡슐 꼭대기는 **머리카락보다 한 뼘 위**다 — 그 위에 얹으면 관이 공중에 뜬다
            // (검수 2026-09-06 반려 1). 실제 정점(스킨드 베이크)의 정수리·머리 폭으로 다시 잡는다.
            if (BossFit.HeadMetrics(boss, out float meshTopY, out float headW, out Vector3 headC))
            {
                headY = meshTopY;
                axis = new Vector3(headC.x, axis.y, headC.z);
                headR = headW * 0.46f;      // 테 조각·뿔 기울기까지 더한 실측 지름이 머리 폭 1.26배(게이트 상한 1.4배)
            }
            var crown = new GameObject(BossCrownObject);
            crown.transform.SetParent(boss.transform, true);
            // 테 아랫면(pos.y + 0.04 − 0.05)이 정수리에 살짝 파묻히게 — 떠 있으면 얹힌 것으로 안 읽힌다.
            crown.transform.position = new Vector3(axis.x, headY - 0.02f, axis.z);
            // 테(밴드)가 없으면 머리카락 위로 뿔 두 개만 삐죽 나와 「왕관」으로 안 읽힌다(검수 관찰).
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f * Mathf.Deg2Rad;
                var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                seg.name = "CrownBand" + i;
                seg.transform.SetParent(crown.transform, true);
                seg.transform.position = crown.transform.position + new Vector3(Mathf.Sin(a) * headR, 0.04f, Mathf.Cos(a) * headR);
                seg.transform.rotation = Quaternion.Euler(0f, i * 30f, 0f);
                seg.transform.localScale = new Vector3(headR * 0.62f, 0.10f, 0.07f);
                var segRend = seg.GetComponent<Renderer>();
                if (segRend != null)
                    segRend.sharedMaterial = crownMat;
                var segCol = seg.GetComponent<Collider>();
                if (segCol != null)
                    UnityEngine.Object.DestroyImmediate(segCol);
            }
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f * Mathf.Deg2Rad;
                var spike = GameObject.CreatePrimitive(PrimitiveType.Cube);
                spike.name = "CrownSpike" + i;
                spike.transform.SetParent(crown.transform, true);
                spike.transform.position = crown.transform.position + new Vector3(Mathf.Sin(a) * headR, 0.20f, Mathf.Cos(a) * headR);
                spike.transform.localScale = new Vector3(0.11f, 0.40f, 0.11f);
                spike.transform.rotation = Quaternion.Euler(Mathf.Cos(a) * 14f, 0f, -Mathf.Sin(a) * 14f);
                var rend = spike.GetComponent<Renderer>();
                if (rend != null)
                    rend.sharedMaterial = crownMat;
                var col = spike.GetComponent<Collider>();
                if (col != null)
                    UnityEngine.Object.DestroyImmediate(col);
            }

            // 3) VFX — 보스 색 오라(점광). 멀리서도 색으로 읽힌다(§8.1 실루엣/가독성).
            var auraGo = new GameObject(BossAuraObject);
            auraGo.transform.SetParent(boss.transform, true);
            auraGo.transform.position = new Vector3(axis.x, boss.transform.position.y + (headY - boss.transform.position.y) * 0.55f, axis.z);
            var aura = auraGo.AddComponent<Light>();
            aura.type = LightType.Point;
            aura.color = tint;
            aura.intensity = 3.4f;
            aura.range = 6.5f;
            aura.shadows = LightShadows.None;
        }

        /// <summary>시설 몸통의 보이는 바운드(부속·NPC는 뺀다 — `HostAnchor`와 같은 잣대).</summary>
        static bool HostBodyBounds(Transform host, out Bounds box)
        {
            bool any = false;
            box = new Bounds();
            var rends = host.GetComponentsInChildren<Renderer>(false);
            for (int i = 0; i < rends.Length; i++)
            {
                if (rends[i] is ParticleSystemRenderer)
                    continue;
                bool mine = false;
                for (var p = rends[i].transform; p != null && p != host; p = p.parent)
                    if (p.name.StartsWith("FacPart", StringComparison.Ordinal) ||
                        p.name.StartsWith("YardFence", StringComparison.Ordinal) ||
                        p.name == CampfireFlameObject || p.name.EndsWith("Npc", StringComparison.Ordinal))
                    { mine = true; break; }
                if (mine)
                    continue;
                if (!any) { box = rends[i].bounds; any = true; }
                else box.Encapsulate(rends[i].bounds);
            }
            return any;
        }

        /// <summary>
        /// **부속은 몸통 밖에 선다** — 킷 배율이 바뀌면 시설 몸통만 커져 굴뚝·통이 그 안으로 파고든다
        /// (오프셋은 절대 미터라 안 따라간다). 자리를 배율에 태우는 대신 **파고든 만큼 비켜세운다**:
        /// 배율이 얼마가 되든 성립하고, 조각끼리의 배치(모닥불 돌·부두 낚싯대)는 그대로 남는다.
        /// 무는 기준은 마을 소품↔건물과 **같은 자**(`SliceSelfCheck.PropOverlapFrac`)를 쓴다.
        /// </summary>
        static void PushClearOfHost(Transform host, Transform part, Vector3 offset)
        {
            for (int pass = 0; pass < 4; pass++)
            {
                if (!HostBodyBounds(host, out Bounds body) || !BoundsOf(part, true, out Bounds pb))
                    return;
                float pen = SliceSelfCheck.Penetration(pb, body);
                if (pen <= 0f)
                    return;
                float thin = Mathf.Min(SliceSelfCheck.Thickness(pb), SliceSelfCheck.Thickness(body));
                if (thin <= 0.01f || pen / thin <= SliceSelfCheck.PropOverlapFrac)
                    return;
                // 미는 방향은 **원래 오프셋이 가리키던 쪽** — 설계자가 「이쪽에 붙이려 했다」는 뜻이다.
                // 오프셋이 0이면(모닥불 장작처럼 한가운데가 자리인 것) 건드리지 않는다.
                var dir = new Vector2(offset.x, offset.z);
                if (dir.sqrMagnitude < 0.0001f)
                    return;
                dir.Normalize();
                float need = Mathf.Max(SliceSelfCheck.Overlap2D(pb, body, dir), 0.05f) + 0.05f;
                var moved = part.position + new Vector3(dir.x * need, 0f, dir.y * need);
                part.position = new Vector3(moved.x, OnGround(new Vector3(moved.x, 0f, moved.z)).y +
                                            (part.position.y - OnGround(new Vector3(part.position.x, 0f, part.position.z)).y),
                                            moved.z);
                Debug.Log("[Ulon] 시설 부속 " + host.name + "/" + part.name + " 몸통 밖으로 " +
                          need.ToString("0.00") + "m 비켜세움(배율 " + KitScale + "에서 몸통이 커졌다).");
            }
        }

    }
}
