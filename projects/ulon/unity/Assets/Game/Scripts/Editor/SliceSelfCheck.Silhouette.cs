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
            var rends = bossGo.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                if (IsHeadgearName(rends[i].transform))
                {
                    if (!hasHat) { hat = rends[i].bounds; hasHat = true; }
                    else hat.Encapsulate(rends[i].bounds);
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
                Debug.Log("[Ulon] 실루엣 계측 " + label + " hatW=" + hatW.ToString("0.00") + " bodyW=" + bodyW.ToString("0.00") + " ccR=" + (ccm != null ? ccm.radius : 0f).ToString("0.00") + " ccH=" + (ccm != null ? ccm.height : 0f).ToString("0.00"));
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

        static bool IsHeadgearName(Transform t)
        {
            for (var cur = t; cur != null; cur = cur.parent)
            {
                if (cur.name.IndexOf("Hat", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }
    }
}
