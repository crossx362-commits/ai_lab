using System;
using System.Collections.Generic;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// 보스가 화면에서 보스로 읽히는지 강제한다(검수 2026-09-06 P0-3).
        /// 수치가 아니라 **실제 렌더러 바운드**를 본다 — 헥사크는 키 2.48을 들고도 챙 넓은 모자에
        /// 몸이 먹혀 "떠 있는 모자"로 보였다. 수치만 보는 판정은 그걸 통과시킨다.
        /// </summary>
        // 기획서 §10.2 「보스는 일반 모델을 1.3~1.5배 확대」 — 하한만 넣으면 게이트가 상한 초과를 통과시킨다(검수 반려 1).
        const float BossSilhouetteMin = 1.3f;
        const float BossSilhouetteMax = 1.5f;
        // 실측(2026-09-06): 모자 축소 후 hatW 1.52 / 축소 전 2.45, 몸 폭은 둘 다 2.84.
        // 0.70이면 고친 상태는 통과하고 결함 상태는 잡힌다 — 이 값으로 네거티브 컨트롤이 빨간불을 낸다.
        const float HeadgearWidthMax = 0.70f;

        /// <summary>
        /// **보스 표식이 프레임 안인가**(검수 지시 2026-09-09). 「투구를 걷었더니 왕관이 화면 위로
        /// 잘려 그냥 사람 얼굴이 됐다」를 자로 만든 것 — 그때 이 자가 없어서 화면이 유일한 자였다.
        /// 판정은 `QaShots.HeadgearFramed`(찍을 때와 **같은 프레이밍 함수**)가 한다.
        /// NC: 왕관을 2m 띄우면 프레임 밖으로 나가 빨간불이어야 한다(끝나면 되돌린다).
        /// </summary>
        /// <summary>
        /// **문구멍이 소품에 가려지지 않았는가**(검수 판정 2026-09-09).
        /// 입구 샷에서 배너가 문구멍을 43~71% 덮고 있었다 — 「입구가 소품에 가려진 입구」다.
        /// 원인은 `fwd`가 안쪽이라는 부호였고, 이 파일에서 **세 번째로 물린 같은 함정**이라
        /// 눈 대신 자로 못박는다. 셈과 같은 함수(`EntranceCensus.MouthBlockShare`)를 쓴다.
        /// NC: 배너를 문구멍 쪽으로 옮기면 빨간불이어야 한다(끝나면 되돌린다).
        /// </summary>
        static void AssertEntranceMouthClear()
        {
            var spots = new (string Tag, string Root, float X, float Z)[]
            {
                ("던전 1", Dungeon1.RootObject, Dungeon1.EntranceX, Dungeon1.EntranceZ),
                ("던전 2", Dungeon2.RootObject, Dungeon2.EntranceX, Dungeon2.EntranceZ),
                ("던전 3", Dungeon3.RootObject, Dungeon3.EntranceX, Dungeon3.EntranceZ),
            };
            const float Max = 0.20f;      // 표본 49개 중 열 개까지 — 그 이상이면 화면에서 문이 안 읽힌다
            string report = "";
            foreach (var s in spots)
            {
                float share = EntranceCensus.MouthBlockShare(s.Root, s.X, s.Z, out string who);
                report += " · " + s.Tag + " " + (share * 100f).ToString("0") + "%" + (who == "" ? "" : "(" + who.Trim() + ")");
                if (share > Max)
                    throw new InvalidOperationException(s.Tag + " 문구멍이 " + (share * 100f).ToString("0") +
                        "% 가려졌습니다 —" + who + ". 소품을 치우지 말고 **문설주 바깥으로 옮기십시오**(입구 표식이다).");
            }
            Debug.Log("[Ulon] 문구멍 가림 통과 —" + report);

            // NC — 배너 하나를 문구멍 쪽으로 밀어 자가 무는지 본다.
            var root = GameObject.Find(Dungeon1.RootObject);
            Transform banner = null;
            if (root != null)
                foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                    if (tr.name.StartsWith("banner-red", StringComparison.Ordinal)) { banner = tr; break; }
            if (banner == null)
            {
                Debug.LogWarning("[Ulon] 문구멍 네거티브 컨트롤 건너뜀 — 배너를 못 찾았다(자가 무력할 수 있다)");
                return;
            }
            var keep = banner.position;
            banner.position = new Vector3(Dungeon1.EntranceX, keep.y, Dungeon1.EntranceZ) - (keep - new Vector3(Dungeon1.EntranceX, keep.y, Dungeon1.EntranceZ)).normalized * 1.6f;
            Physics.SyncTransforms();
            bool caught = EntranceCensus.MouthBlockShare(Dungeon1.RootObject, Dungeon1.EntranceX, Dungeon1.EntranceZ, out _) > Max;
            banner.position = keep;
            Physics.SyncTransforms();
            if (!caught)
                throw new InvalidOperationException("문구멍 네거티브 컨트롤 실패 — 배너를 문 앞으로 옮겼는데도 통과했습니다.");
            Debug.Log("[Ulon] 문구멍 네거티브 컨트롤 통과 — 배너를 문 앞으로 옮기면 FAIL");
        }

        static void AssertBossHeadgearFramed()
        {
            if (!QaShots.HeadgearFramed(out string report))
                throw new InvalidOperationException("보스 샷에서 머리 표식이 프레임 밖입니다 —" + report +
                    " (표식이 화면 밖이면 그 샷은 보스의 샷이 아니다. 카메라를 물리십시오 — 왕관·투구를 건드리지 말고.)");
            Debug.Log("[Ulon] 보스 표식 프레임 통과 —" + report);

            // 네거티브 컨트롤 — 자가 살아 있는지 그 자리에서 증명한다.
            var boss = GameObject.Find(Dungeon3.BossObject);
            Transform crown = null;
            if (boss != null)
                foreach (var tr in boss.GetComponentsInChildren<Transform>(true))
                    if (tr.name == VisualSliceBuilder.BossCrownObject) { crown = tr; break; }
            if (crown == null)
            {
                Debug.LogWarning("[Ulon] 보스 표식 네거티브 컨트롤 건너뜀 — 왕관을 못 찾았다(자가 무력할 수 있다)");
                return;
            }
            var keep = crown.position;
            crown.position = keep + Vector3.up * 2f;
            bool caught = !QaShots.HeadgearFramed(out string ncReport);
            crown.position = keep;
            if (!caught)
                throw new InvalidOperationException("보스 표식 네거티브 컨트롤 실패 — 왕관을 2m 띄웠는데도 통과했습니다:" + ncReport);
            Debug.Log("[Ulon] 보스 표식 네거티브 컨트롤 통과 — 왕관을 2m 띄우면 FAIL");
        }

        static void AssertBossSilhouette()
        {
            AssertDungeon3Leftover();

            CheckSilhouette("던전 1", Dungeon1.BossObject, Dungeon1.MobObject);
            CheckSilhouette("던전 2", Dungeon2.BossObject, Dungeon2.MobObject);
            CheckSilhouette("던전 3", Dungeon3.BossObject, Dungeon3.MobObject);
            CheckSilhouette("필드 보스", FieldBoss.Object, "Raider");

            Debug.Log("[Ulon] 보스 실루엣 통과 — §10.2 잡몹 대비 " + BossSilhouetteMin + "~" + BossSilhouetteMax + "배, 모자 폭 몸 폭의 " + HeadgearWidthMax + "배 이하 (던전 1·2·3·필드)");
        }

        static void CheckSilhouette(string label, string bossObject, string mobObject)
        {
            // 키 비교는 CharacterController.height로 한다. SkinnedMeshRenderer.bounds는 에디터에서
            // 애니메이션이 안 돌아 부풀거나 낡은 값이 나온다(실측: 키 1.85 잡몹이 2.37로 잡혔다).
            // cc.height는 스폰 시 원장 키가 그대로 들어가고 이동·피격 판정이 쓰는 실제 몸 높이다.
            float bossH = BodyHeight(label + " 보스", bossObject, out GameObject bossGo);
            float mobH = BodyHeight(label + " 잡몹", mobObject, out _);

            float ratio = bossH / mobH;
            if (ratio < BossSilhouetteMin || ratio > BossSilhouetteMax)
                throw new InvalidOperationException(label + " 보스가 잡몹 대비 " + ratio.ToString("0.00") + "배입니다 — 기획서 §10.2는 " + BossSilhouetteMin + "~" + BossSilhouetteMax + "배(보스 " + bossH.ToString("0.00") + "m, 잡몹 " + mobH.ToString("0.00") + "m).");

            // 모자가 몸을 덮는가 — 헥사크 결함의 직접 판정(폭 비율은 스케일이 같이 먹으므로 bounds로 봐도 안정적이다).
            Bounds hat = new Bounds();
            Bounds body = new Bounds();
            bool hasHat = false;
            bool hasBody = false;
            // **무엇을 모자로 세었는지 적는다** — 이름을 안 적으면 「모자 폭 1.42m」가 왕관인지 투구인지
            // 꺼진 렌더러인지 알 수 없어, 고치는 쪽이 엉뚱한 것을 줄이게 된다(실제로 한 번 그랬다).
            string hatNames = "";
            var rends = bossGo.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                // **꺼진 렌더러는 화면에 없다 — 세지 않는다**(실측 2026-09-09).
                // 보스 투구를 걷었더니 이 자가 **꺼진 투구**를 여전히 「모자 1.42m」로 세어 빨간불을 냈다.
                // 화면에는 맨머리와 왕관만 있었다. 완화가 아니라 **잣대 교정**이다 — 이 자가 잡아야 할
                // 「모자가 몸을 덮는다」는 보이는 것들 사이의 관계다. NC(모자를 원래 크기로 되돌리기)는
                // 그대로 빨간불이어야 하고, 실제로 그렇다.
                if (!rends[i].enabled || !rends[i].gameObject.activeInHierarchy || rends[i] is ParticleSystemRenderer)
                    continue;
                if (IsHeadgearName(rends[i].transform))
                {
                    if (!hasHat) { hat = rends[i].bounds; hasHat = true; }
                    else hat.Encapsulate(rends[i].bounds);
                    hatNames += " " + rends[i].name + "(" + Mathf.Max(rends[i].bounds.size.x, rends[i].bounds.size.z).ToString("0.00") +
                                (rends[i].enabled ? "" : ",꺼짐") + ")";
                }
                else
                {
                    // **몸 폭에 장비를 섞지 않는다**(검수 승인 2026-09-07). 무기·망토를 넣었더니
                    // 필드 보스 몸 폭이 4.28m(CharacterController 지름은 0.8m)로 잡혀 상한이 3.0m가 됐고,
                    // 모자 폭 1.41은 **무슨 짓을 해도 통과**했다 — 비율 게이트는 분모가 부풀면 통째로 무력해진다.
                    if (GroundFit.IsGear(bossGo.transform, rends[i].transform))
                        continue;
                    if (!hasBody) { body = rends[i].bounds; hasBody = true; }
                    else body.Encapsulate(rends[i].bounds);
                }
            }
            if (hasHat && hasBody)
            {
                float hatW = Mathf.Max(hat.size.x, hat.size.z);
                float bodyW = Mathf.Max(body.size.x, body.size.z);
                var ccm = bossGo.GetComponent<CharacterController>();
                Debug.Log("[Ulon] 실루엣 계측 " + label + " hatW=" + hatW.ToString("0.00") + " bodyW=" + bodyW.ToString("0.00") + " ccR=" + (ccm != null ? ccm.radius : 0f).ToString("0.00") + " ccH=" + (ccm != null ? ccm.height : 0f).ToString("0.00") + " · 모자로 센 것:" + hatNames);
                if (bodyW > 0.01f && hatW > bodyW * HeadgearWidthMax)
                    throw new InvalidOperationException(label + " 모자 폭 " + hatW.ToString("0.00") + "m가 몸 폭 " + bodyW.ToString("0.00") + "m의 " + HeadgearWidthMax + "배를 넘습니다 — 45° 시점에서 몸이 모자에 가려집니다.");
            }
        }

        /// <summary>
        /// 네거티브 컨트롤 — **모자를 원래 크기로 되돌려** 빨간불을 본다.
        /// 이 NC가 빨간불이 안 나면 이 게이트는 아무것도 안 재고 있는 것이다(실제로 그랬다: 분모 오염).
        /// </summary>
        static void AssertBossSilhouetteHeadgearNegativeControl()
        {
            var boss = GameObject.Find(FieldBoss.Object);
            if (boss == null)
                throw new InvalidOperationException("실루엣 NC 대상(필드 보스)이 없습니다.");
            var hats = new List<Transform>();
            var all = boss.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (IsHeadgearName(all[i]) && all[i] != boss.transform)
                    hats.Add(all[i]);
            if (hats.Count == 0)
                throw new InvalidOperationException("실루엣 NC 대상 모자를 못 찾았습니다 — 결함을 만들 수 없습니다(NC 실패).");
            var saved = new List<Vector3>();
            for (int i = 0; i < hats.Count; i++) saved.Add(hats[i].localScale);
            bool red = false;
            try
            {
                // 축소 전 크기(0.62배로 줄인 것을 되돌린다) — 헥사크가 「떠 있는 모자」로 보이던 그 상태다.
                for (int i = 0; i < hats.Count; i++)
                    hats[i].localScale = hats[i].localScale / 0.62f;
                try { CheckSilhouette("필드 보스", FieldBoss.Object, "Raider"); }
                catch (InvalidOperationException) { red = true; }
            }
            finally
            {
                for (int i = 0; i < hats.Count; i++) hats[i].localScale = saved[i];
            }
            if (!red)
                throw new InvalidOperationException("실루엣 네거티브 컨트롤 실패 — 모자를 원래 크기로 되돌렸는데 통과했습니다. " +
                    "이 게이트는 아무것도 안 재고 있습니다(분모에 장비가 섞이지 않았는지 보라).");
            Debug.Log("[Ulon] 실루엣 네거티브 컨트롤 통과 — 모자를 원래 크기로 되돌리자 FAIL");
        }

        static float BodyHeight(string what, string objectName, out GameObject go)
        {
            go = GameObject.Find(objectName);
            if (go == null)
                throw new InvalidOperationException(what + " 오브젝트가 없습니다: " + objectName);
            var cc = go.GetComponent<CharacterController>();
            if (cc == null || cc.height < 0.01f)
                throw new InvalidOperationException(what + " CharacterController 높이가 없습니다: " + objectName);
            if (go.GetComponentsInChildren<Renderer>(true).Length == 0)
                throw new InvalidOperationException(what + " 렌더러가 없습니다(모델이 안 붙었습니다): " + objectName);
            return cc.height;
        }

        /// <summary>머리에 쓴 것인가 — 판정은 `GroundFit`에 한 벌만 둔다(이름 ∪ 자리·성질, 랩 ②).</summary>
        static bool IsHeadgearName(Transform t)
        {
            var actor = t;
            while (actor != null && actor.GetComponent<CharacterController>() == null)
                actor = actor.parent;
            return GroundFit.IsHeadgear(actor != null ? actor : t.root, t);
        }
    }
}
