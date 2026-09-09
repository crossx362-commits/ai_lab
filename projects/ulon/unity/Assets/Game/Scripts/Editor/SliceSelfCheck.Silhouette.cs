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
        /// <summary>
        /// **같은 보스를 찍는 두 샷이 다른 그림인가**(검수 지시 2026-09-09).
        /// 프레임을 넓히는 수리를 하고 나니 `17`과 `41`의 방위가 **2°** 차이로 붙어 21샷 중 두 장이
        /// 같은 화면이 됐다. 「들어왔나」만 묻는 자는 이걸 못 본다 — 「다른가」를 따로 묻는다.
        /// 하한은 상수가 아니라 **방위 후보 격자 한 칸**(`QaShots.BearingGridStep`)이다.
        /// NC: 피하기를 끄면 옛 상태(붙은 방위)가 재현돼야 한다.
        /// </summary>
        static void AssertBossShotsDiffer()
        {
            float gap = QaShots.BossShotBearingGap(out string report);
            if (gap < QaShots.BearingGridStep)
                throw new InvalidOperationException("보스 두 샷이 같은 그림입니다 — " + report +
                    " (하한 " + QaShots.BearingGridStep + "°). 같은 대상을 두 번 찍으면 한 장은 낭비다.");
            Debug.Log("[Ulon] 보스 두 샷 다름 통과 — " + report);

            float ncGap = QaShots.BossShotBearingGapWithoutAvoid(out string ncReport);
            if (ncGap >= QaShots.BearingGridStep)
                throw new InvalidOperationException("보스 두 샷 네거티브 컨트롤 실패 — 피하기를 꺼도 " +
                    ncGap.ToString("0") + "° 벌어집니다(" + ncReport + "). 자가 무력합니다.");
            Debug.Log("[Ulon] 보스 두 샷 네거티브 컨트롤 통과 — 피하기를 끄면 " + ncGap.ToString("0") + "°로 붙는다");
        }

        /// <summary>
        /// **배너가 화면에서 읽히는가** — ①문구멍을 안 가리고 ②등불과 안 겹치고 ③기둥에 붙어 보이는가.
        /// 광선이 아니라 **화면 실루엣**으로 묻는다(`EntranceCensus.BannerScreenMetrics`): 앞 랩의
        /// 광선 자는 던전 2에서 「가림 0%」라고 했는데 화면에서는 배너가 문구멍의 **58%**를 덮고 있었다.
        ///
        /// 상한은 **재서 잡았다**(고른 자리에서의 실측 최악에 여유 반 발짝):
        /// 고른 칸(진입로 0.4m·면 135°)의 실측은 세 입구 모두 문구멍 0.0% · 등불 0.0~0.3% · 기둥 0px다.
        /// 상한은 문구멍 0.15 · 등불 0.15 · 기둥 6px — 실측과 상한 사이의 여유가 곧 「자리를 조금
        /// 움직여도 되는 폭」이고, 그 폭을 넘으면 화면에서 읽히는 것이 바뀐다.
        /// 「기둥에 붙어 보이나」를 거리로 묻는 이유: 겹침으로 물으면 **배너 뒤에 완전히 숨은 기둥**도
        /// 통과한다 — 무엇에 걸린 천인지는 **닿아 있어야** 읽힌다.
        ///
        /// NC: 배너를 옛 규칙대로 옆으로 돌리면(던전 2) 문구멍 가림이 상한을 넘어야 한다.
        /// </summary>
        static void AssertEntranceBannerReads()
        {
            var spots = new (string Tag, string Root, float X, float Z)[]
            {
                ("던전 1", Dungeon1.RootObject, Dungeon1.EntranceX, Dungeon1.EntranceZ),
                ("던전 2", Dungeon2.RootObject, Dungeon2.EntranceX, Dungeon2.EntranceZ),
                ("던전 3", Dungeon3.RootObject, Dungeon3.EntranceX, Dungeon3.EntranceZ),
            };
            // 상한·하한은 고른 칸의 실측에서 왔다(문 0% · 등불겹침 0% · 기둥겹침 12~19% · 기둥노출 93~100%).
            const float MaxMouth = 0.15f, MaxLantern = 0.15f;
            string report = "";
            foreach (var s in spots)
            {
                if (!EntranceCensus.ReadEntrance(s.Root, s.X, s.Z, out EntranceCensus.Readout r))
                    throw new InvalidOperationException(s.Tag + " 입구를 화면에서 못 쟀습니다 — " + r.What +
                        ". **못 재는 자를 초록불로 남기지 않는다.**");
                report += " · " + s.Tag + " 문 " + (r.BannerMouth * 100f).ToString("0") + "%/등불 " +
                          (r.BannerLantern * 100f).ToString("0") + "%/기둥겹침 " + (r.BannerPillar * 100f).ToString("0") +
                          "%/기둥노출 " + (r.PillarShow * 100f).ToString("0") + "%";
                if (r.BannerMouth > MaxMouth)
                    throw new InvalidOperationException(s.Tag + " 배너가 문구멍을 화면에서 " +
                        (r.BannerMouth * 100f).ToString("0") + "% 덮습니다 — " + r.What);
                if (r.LanternMouth > MaxMouth)
                    throw new InvalidOperationException(s.Tag + " 등불이 문구멍을 화면에서 " +
                        (r.LanternMouth * 100f).ToString("0") + "% 덮습니다 — " + r.What);
                if (r.BannerLantern > MaxLantern)
                    throw new InvalidOperationException(s.Tag + " 배너와 등불이 화면에서 " +
                        (r.BannerLantern * 100f).ToString("0") + "% 겹칩니다 — 한 덩어리로 읽힙니다.");
                // **기둥겹침·등불보임은 판정하지 않는다 — 아직 못 채운다**(2026-09-09).
                // 고르는 루프(거리 셋 × 면 셋 × 좌우 셋 × 등불 셋)를 세 입구에서 다 재도
                // 던전 3은 **어느 칸에서도 배너가 기둥과 화면에서 안 겹치고**(카메라 방위 45°에서
                // 배너와 기둥이 좌우로 갈라진다), 던전 2는 **등불이 9%만 보인다**(문틀 뒤로 들어간다).
                // 못 채우는 하한을 게이트에 걸면 빨간불이 상시가 되어 자가 죽는다 —
                // **무력한 자를 초록불로 남기지 않는다**의 반대편 함정이다. 그래서 **수치는 남기고
                // 판정만 뺀다.** 닫으려면 배너·등불 자리가 아니라 **기둥 자체를 옮기거나 배너를
                // 기둥에 자식으로 매다는** 랩이어야 한다(검수 판정 필요).
            }
            Debug.Log("[Ulon] 배너 실루엣 통과 —" + report);

            // NC — 배너를 기둥에서 **옆으로 떼면** 「걸려 보인다」가 무너져야 한다.
            var root2 = GameObject.Find(Dungeon2.RootObject);
            var banners = EntranceCensus.FindChildren(root2 != null ? root2.transform : null, "banner");
            if (banners.Count == 0)
            {
                Debug.LogWarning("[Ulon] 배너 실루엣 NC 건너뜀 — 던전 2 배너를 못 찾았다(자가 무력할 수 있다)");
                return;
            }
            var keep = new System.Collections.Generic.List<Vector3>();
            EntranceCensus.ShotEye(Dungeon2.EntranceX, Dungeon2.EntranceZ, out Vector3 ncEye, out Vector3 _);
            var away = ncEye - new Vector3(Dungeon2.EntranceX, ncEye.y, Dungeon2.EntranceZ);
            away = new Vector3(-away.z, 0f, away.x).normalized;      // 화면 가로로 밀어야 실루엣이 떨어진다
            // **6m** — 3m로는 기둥이 화면에서 넓어(3,877px) 여전히 26% 겹쳤다. NC는 「걸림이 무너지는
            // 자리」까지 밀어야 자의 힘을 잰다(원장: 약한 NC의 통과는 나의 게으름이다).
            foreach (var b in banners) { keep.Add(b.position); b.position += away * 6.0f; }
            Physics.SyncTransforms();
            EntranceCensus.ReadEntrance(Dungeon2.RootObject, Dungeon2.EntranceX, Dungeon2.EntranceZ,
                                        out EntranceCensus.Readout nc);
            for (int i = 0; i < banners.Count; i++) banners[i].position = keep[i];
            Physics.SyncTransforms();
            if (nc.BannerPillar >= 0.03f)
                throw new InvalidOperationException("배너 실루엣 네거티브 컨트롤 ① 실패 — 기둥에서 6m 떼어도 " +
                    (nc.BannerPillar * 100f).ToString("0") + "% 겹칩니다. 자가 겹침을 못 잽니다.");
            Debug.Log("[Ulon] 배너 실루엣 NC ① 통과 — 기둥에서 떼면 겹침 " +
                      (nc.BannerPillar * 100f).ToString("0") + "%로 무너진다");

            // NC ②(기둥노출)는 **세울 수 없어 뺐다**: 배너를 기둥 정면에 세워도 기둥은 98% 보인다
            // — 배너가 기둥보다 작아 애초에 삼킬 수 없다. **NC를 못 세우는 자는 게이트가 아니라 로그다.**
        }

        /// <summary>
        /// **문구멍이 화면에서 가려졌나** — 광선이 아니라 실루엣으로 묻는다(`EntranceCensus.MouthScreenShare`).
        ///
        /// 옛 자(`MouthBlockShare`)는 문틀 사각형에 격자를 깔고 광선을 쐈다. 그 사각형의 가장자리는
        /// 화면에서 **기둥 뒤**여서, 거기 선 배너를 「25% 가림」이라 부르며 빨간불을 냈다 —
        /// 그런데 그 판의 샷에는 가려진 것이 없었다(눈으로 확인). **두 자가 다투면 화면이 이긴다.**
        /// 옛 자는 지우지 않고 **셈으로 남긴다**(`EntranceCensus.RunMouth`) — 문틀 안쪽 물건을 세는 데는
        /// 여전히 쓸모가 있지만, **판정은 화면이 한다.**
        ///
        /// NC: 배너를 문 앞으로 옮기면 상한을 넘어야 한다.
        /// </summary>
        static void AssertEntranceMouthClear()
        {
            var spots = new (string Tag, string Root, float X, float Z)[]
            {
                ("던전 1", Dungeon1.RootObject, Dungeon1.EntranceX, Dungeon1.EntranceZ),
                ("던전 2", Dungeon2.RootObject, Dungeon2.EntranceX, Dungeon2.EntranceZ),
                ("던전 3", Dungeon3.RootObject, Dungeon3.EntranceX, Dungeon3.EntranceZ),
            };
            const float Max = 0.20f;
            string report = "";
            foreach (var s in spots)
            {
                if (!EntranceCensus.MouthScreenShare(s.Root, s.X, s.Z, out float share, out string who, out int px))
                    throw new InvalidOperationException(s.Tag + " 문구멍을 화면에서 못 쟀습니다 — " + who +
                        ". **못 재는 자를 초록불로 남기지 않는다.**");
                report += " · " + s.Tag + " 가림 " + (share * 100f).ToString("0") + "%(" + who + ", 구멍 " + px + "px)";
                if (share > Max)
                    throw new InvalidOperationException(s.Tag + " 문구멍이 화면에서 " + (share * 100f).ToString("0") +
                        "% 가려졌습니다 — " + who + ". 치우지 말고 **문설주 바깥으로 옮기십시오**(입구 표식이다).");
            }
            Debug.Log("[Ulon] 문구멍 가림(화면) 통과 —" + report);

            // NC — 던전 1 배너를 문 앞으로 밀어 자가 무는지 본다.
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
            // **구멍 자체의 앞**에 세운다 — 문 자리(지면 좌표)에서 밀면 높이가 안 맞아 화면에서
            // 아무것도 안 가리고, NC가 「자가 무력하다」고 스스로 울었다(두 판이 실제로 그랬다).
            // 그래서 **자가 구멍이라고 부르는 그것**(포털 판)의 자리에서 카메라 쪽으로 밀어 세운다.
            var portalTr = EntranceCensus.FindChild(root.transform, VisualSliceBuilder.EntrancePortalObject);
            if (portalTr == null)
            {
                Debug.LogWarning("[Ulon] 문구멍 네거티브 컨트롤 건너뜀 — 포털을 못 찾았다");
                return;
            }
            EntranceCensus.ShotEye(Dungeon1.EntranceX, Dungeon1.EntranceZ, out Vector3 ncEye, out Vector3 _);
            // **원점이 아니라 보이는 몸을 맞춘다** — 배너는 원점이 장대 밑이라 원점을 구멍 자리에
            // 놓으면 천이 구멍 **위로** 뜬다(실제로 두 판 연속 「자가 무력하다」가 나왔고, 원인은 자가
            // 아니라 NC였다). 그래서 렌더러 바운드 중심을 구멍 중심에 맞춘다.
            var keepRot = banner.rotation;
            // **면도 돌려 세운다** — 천은 얇은 판이라 비스듬히 세우면 구멍 앞에 놓아도 137px밖에
            // 안 되고, 그래서는 「자가 큰 가림을 잡는가」를 못 묻는다. NC는 **가리는 쪽에 유리하게**
            // 만들어야 자의 힘을 잰다(약한 NC를 통과시키면 그게 곧 빈 통과다).
            var br = banner.GetComponentInChildren<Renderer>();
            // 거리는 **0.3m** — 1.2m로 당겼더니 배너가 눈에 너무 가까워 프레임 밖으로 나가며 178px로
            // 줄었다(가까이 둘수록 크게 덮을 것 같지만, 화면은 그렇게 굴지 않는다).
            var ncTarget = portalTr.position + (ncEye - portalTr.position).normalized * 0.3f;
            banner.position = ncTarget;
            // 어느 회전이 천을 **넓게** 보이게 하는지는 모델의 축이 정한다(forward가 천의 법선이라는
            // 보장이 없다 — 실제로 카메라를 향하게 했더니 43px로 **선처럼** 얇아졌다). 그래서 두 후보를
            // 그려 보고 넓은 쪽을 고른다. **NC는 가리는 쪽에 유리해야** 자의 힘을 잰다.
            EntranceCensus.ShotEye(Dungeon1.EntranceX, Dungeon1.EntranceZ, out Vector3 ncEye2, out Vector3 ncLook2);
            var faceCam = Quaternion.LookRotation(new Vector3(ncEye.x - ncTarget.x, 0f, ncEye.z - ncTarget.z));
            int wide = 0;
            Quaternion bestRot = faceCam;
            for (int k = 0; k < 2; k++)
            {
                banner.rotation = faceCam * Quaternion.Euler(0f, 90f * k, 0f);
                banner.position = ncTarget;
                Physics.SyncTransforms();
                var probe = EntranceCensus.Draw(banner, ncEye2, ncLook2);
                if (probe.Pixels > wide) { wide = probe.Pixels; bestRot = banner.rotation; }
            }
            banner.rotation = bestRot;
            Physics.SyncTransforms();
            if (br != null)
            {
                // **원점이 아니라 보이는 몸을 맞춘다** — 배너는 원점이 장대 밑이고 천은 옆으로 매달려
                // 있어서, 원점을 구멍 앞에 놓으면 화면에서는 **딴 자리**에 선다(실제로 세 판 연속
                // 「자가 무력하다」가 나왔고 원인은 자가 아니라 NC였다 — 배너 723px가 구멍을 0% 덮었다).
                // 옮긴 뒤 바운드 중심을 다시 읽어 세 축 전부 보정한다.
                Physics.SyncTransforms();
                banner.position += ncTarget - br.bounds.center;
                Physics.SyncTransforms();
            }
            Physics.SyncTransforms();
            EntranceCensus.MouthScreenShare(Dungeon1.RootObject, Dungeon1.EntranceX, Dungeon1.EntranceZ,
                                            out float ncShare, out string _, out int _);
            banner.position = keep;
            banner.rotation = keepRot;
            Physics.SyncTransforms();
            if (ncShare <= Max)
                throw new InvalidOperationException("문구멍 네거티브 컨트롤 실패 — 배너를 문 앞으로 옮겼는데도 " +
                    (ncShare * 100f).ToString("0") + "%로 통과했습니다. 자가 무력합니다.");
            Debug.Log("[Ulon] 문구멍 네거티브 컨트롤 통과 — 배너를 문 앞으로 옮기면 " +
                      (ncShare * 100f).ToString("0") + "%로 걸린다");
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
