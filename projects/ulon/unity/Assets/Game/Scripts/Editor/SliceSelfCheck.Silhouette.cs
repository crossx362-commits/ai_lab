using System;
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
        const float BossSilhouetteRatio = 1.4f;
        const float HeadgearWidthMax = 1.35f;

        static void AssertBossSilhouette()
        {
            AssertDungeon3Leftover();

            CheckSilhouette("던전 1", Dungeon1.BossObject, Dungeon1.MobObject);
            CheckSilhouette("던전 2", Dungeon2.BossObject, Dungeon2.MobObject);
            CheckSilhouette("던전 3", Dungeon3.BossObject, Dungeon3.MobObject);
            CheckSilhouette("필드 보스", FieldBoss.Object, "Raider");

            Debug.Log("[Ulon] 보스 실루엣 통과 — 잡몹 대비 " + BossSilhouetteRatio + "배↑, 모자 폭 몸 폭의 " + HeadgearWidthMax + "배 이하 (던전 1·2·3·필드)");
        }

        static void CheckSilhouette(string label, string bossObject, string mobObject)
        {
            // 키 비교는 CharacterController.height로 한다. SkinnedMeshRenderer.bounds는 에디터에서
            // 애니메이션이 안 돌아 부풀거나 낡은 값이 나온다(실측: 키 1.85 잡몹이 2.37로 잡혔다).
            // cc.height는 스폰 시 원장 키가 그대로 들어가고 이동·피격 판정이 쓰는 실제 몸 높이다.
            float bossH = BodyHeight(label + " 보스", bossObject, out GameObject bossGo);
            float mobH = BodyHeight(label + " 잡몹", mobObject, out _);

            float ratio = bossH / mobH;
            if (ratio < BossSilhouetteRatio)
                throw new InvalidOperationException(label + " 보스가 잡몹 대비 " + ratio.ToString("0.00") + "배입니다 — 최소 " + BossSilhouetteRatio + "배(보스 " + bossH.ToString("0.00") + "m, 잡몹 " + mobH.ToString("0.00") + "m). 보스로 안 읽힙니다.");

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
                    if (!hasBody) { body = rends[i].bounds; hasBody = true; }
                    else body.Encapsulate(rends[i].bounds);
                }
            }
            if (hasHat && hasBody)
            {
                float hatW = Mathf.Max(hat.size.x, hat.size.z);
                float bodyW = Mathf.Max(body.size.x, body.size.z);
                if (bodyW > 0.01f && hatW > bodyW * HeadgearWidthMax)
                    throw new InvalidOperationException(label + " 모자 폭 " + hatW.ToString("0.00") + "m가 몸 폭 " + bodyW.ToString("0.00") + "m의 " + HeadgearWidthMax + "배를 넘습니다 — 45° 시점에서 몸이 모자에 가려집니다.");
            }
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
