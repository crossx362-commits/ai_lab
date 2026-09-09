using System;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// **샷의 주인공이 프레임에 담겼나**(검수 「죽은 장」 훑기 조건 ①~③, 2026-09-09).
        ///
        /// 판정하는 것은 그림의 좋고 나쁨이 아니라 **그 장이 무엇이라도 보고 있는가**다.
        /// `13_d1_room_cutaway`는 이름만 절단면이고 화면은 지표 잔디였는데 여러 판 통과했고,
        /// `06_field_boss`는 보스가 **0.1%(51px)**인 채 오래 찍혀 왔다 — 눈으로만 보면 놓친다.
        ///
        /// 하한은 **부류마다** 다르다(검수 반려 2026-09-09 — 전역 3%로 묶으면 **실내 절단면이
        /// 60%에서 20%로 죽어도 통과한다**. `13`이 죽어 있던 그 상태를 못 잡는 자가 된다).
        /// 실측(18장) 최저의 절반을 부류별 하한으로 삼는다: 근접 5.8→3 · 마을 6.3→3 · 지역 7.4→3 ·
        /// 입구 14.7→7 · 실내 60.5→30. 여유를 주는 이유는 그대로고(우는 자는 곧 꺼진다),
        /// **여유도 부류마다** 준다. 표는 원장 `ShotSubject.MinShare`에 있다 — 자는 하나다.
        ///
        /// NC 둘: ①주인공을 숨긴다 ②카메라를 90° 돌린다 — 둘 다 하한 아래로 떨어져야 한다.
        /// </summary>
        static void AssertShotSubjectFramed()
        {
            var shots = QaShots.BuildShots();
            int measured = 0, unmeasurable = 0;
            string report = "";
            float worst = float.MaxValue;   // 「하한 대비 여유」의 최소 — 몫이 아니라 여유로 고른다
            string worstName = "";
            for (int i = 0; i < shots.Length; i++)
            {
                string name = QaShots.NameOf(shots[i]);
                if (!ShotSubject.Table.TryGetValue(name, out ShotSubject.Entry e))
                    continue;
                if (e.Objects.Length == 0)
                {
                    unmeasurable++;              // 지형이 주인공 — 실루엣 자로는 못 잰다(원장에 적혀 있다)
                    continue;
                }
                float min = ShotSubject.MinShare(e.Class);
                float share = SubjectShare(shots[i], e.Objects, out string absent);
                if (absent.Length > 0)
                    throw new InvalidOperationException("샷 " + name + "의 주인공을 씬에서 못 찾았습니다:" + absent +
                        " — **못 재는 자를 초록불로 남기지 않는다.** 원장(ShotSubject)이 낡았거나 대상이 사라진 것입니다.");
                measured++;
                // 「얼마나 여유가 남았나」로 최악을 고른다 — 부류마다 하한이 다르니 몫만 보면
                // 실내 30%짜리 위기가 근접 6%짜리 여유에 묻힌다.
                float slack = share / Mathf.Max(0.0001f, min);
                if (slack < worst) { worst = slack; worstName = name + " " + (share * 100f).ToString("0.0") +
                    "%(부류 " + e.Class + " 하한 " + (min * 100f).ToString("0") + "%)"; }
                if (share < min)
                    throw new InvalidOperationException("샷 " + name + "의 주인공이 화면의 " +
                        (share * 100f).ToString("0.0") + "%뿐입니다(부류 " + e.Class + " 하한 " +
                        (min * 100f).ToString("0") + "%) — 이 장은 아무것도 안 보고 있습니다. " +
                        "카메라 자리를 **대상에서 유도**하십시오.");
            }
            if (measured == 0)
                throw new InvalidOperationException("주인공을 잰 샷이 0장입니다 — 원장이 비었거나 샷 이름이 바뀐 것입니다(자가 무력합니다).");
            report = " · 잰 샷 " + measured + " · 하한에 가장 가까운 것 " + worstName +
                     " · 못 잼(지형) " + unmeasurable;
            Debug.Log("[Ulon] 샷 주인공 프레임 통과 — 부류별 하한(근접·마을·지역 3 / 입구 7 / 실내 30)" + report);

            AssertShotSubjectNegativeControl(shots);
        }

        /// <summary>이 샷의 카메라에서 주인공이 화면의 몇 할인가(0~1).</summary>
        static float SubjectShare(QaShots.Shot shot, string[] objects, out string absent)
        {
            QaShots.EyeOf(shot, out Vector3 eye, out Vector3 look);
            absent = "";
            int px = 0;
            for (int k = 0; k < objects.Length; k++)
            {
                var go = GameObject.Find(objects[k]);
                if (go == null) { absent += " " + objects[k]; continue; }
                px += EntranceCensus.Draw(go.transform, eye, look).Pixels;
            }
            return (float)px / (EntranceCensus.ScreenW * EntranceCensus.ScreenH);
        }

        /// <summary>
        /// NC 둘 — **살아 있는 샷 하나**에 결함을 만들어 자가 무는지 본다.
        /// ①주인공 렌더러를 전부 끈다 ②카메라를 대상 둘레로 90° 돌린다.
        /// 대상은 **몸 하나짜리 근접 샷**이다. 첫 판에는 실내 방(`10_d2_interior`)을 골랐다가 NC ②가
        /// 「등을 돌려도 95%」로 실패했는데, 그건 자가 무력한 것이 아니라 **방이 카메라를 둘러싸고 있어
        /// 어디를 봐도 보이는 것**이었다 — NC는 결함이 실제로 만들어지는 자리에서 걸어야 한다.
        /// </summary>
        static void AssertShotSubjectNegativeControl(QaShots.Shot[] shots)
        {
            const string target = "22_mob_closeup";
            int idx = -1;
            for (int i = 0; i < shots.Length; i++)
                if (QaShots.NameOf(shots[i]) == target) { idx = i; break; }
            if (idx < 0 || !ShotSubject.Table.TryGetValue(target, out ShotSubject.Entry e) || e.Objects.Length == 0)
                throw new InvalidOperationException("샷 주인공 네거티브 컨트롤을 세울 샷(" + target +
                    ")이 목록이나 원장에 없습니다 — **NC를 못 세우는 자는 게이트가 아니라 로그다.**");

            var objects = e.Objects;
            float min = ShotSubject.MinShare(e.Class);
            var go = GameObject.Find(objects[0]);
            if (go == null)
                throw new InvalidOperationException("샷 주인공 네거티브 컨트롤 — 대상 " + objects[0] + "이 씬에 없습니다.");

            // ① 숨긴다.
            var rends = go.GetComponentsInChildren<Renderer>(true);
            var keep = new bool[rends.Length];
            for (int i = 0; i < rends.Length; i++) { keep[i] = rends[i].enabled; rends[i].enabled = false; }
            float hidden = SubjectShare(shots[idx], objects, out string _);
            for (int i = 0; i < rends.Length; i++) rends[i].enabled = keep[i];
            if (hidden >= min)
                throw new InvalidOperationException("샷 주인공 네거티브 컨트롤 ① 실패 — 주인공을 숨겼는데도 " +
                    (hidden * 100f).ToString("0.0") + "%로 통과합니다. 자가 무력합니다.");

            // ② 카메라를 대상 둘레로 90° 돌린다(눈만 돌리면 방은 여전히 프레임 안이므로 **자리**를 돈다).
            QaShots.EyeOf(shots[idx], out Vector3 eye, out Vector3 look);
            var turned = look + Quaternion.Euler(0f, 90f, 0f) * (eye - look);
            var away = look + (turned - look).normalized * Vector3.Distance(eye, look);
            // 90°만으로는 큰 방이 여전히 담긴다 — **등을 돌린다**(같은 자리에서 반대쪽을 본다).
            var back = eye + (eye - look).normalized * Vector3.Distance(eye, look);
            float turnedShare = SubjectShareAt(objects, away, look);
            float backShare = SubjectShareAt(objects, eye, back);
            if (backShare >= min)
                throw new InvalidOperationException("샷 주인공 네거티브 컨트롤 ② 실패 — 카메라가 등을 돌렸는데도 " +
                    (backShare * 100f).ToString("0.0") + "%로 통과합니다. 자가 무력합니다.");
            Debug.Log("[Ulon] 샷 주인공 네거티브 컨트롤 통과 — 숨기면 " + (hidden * 100f).ToString("0.0") +
                      "% · 등 돌리면 " + (backShare * 100f).ToString("0.0") + "% (참고: 옆으로 90° " +
                      (turnedShare * 100f).ToString("0.0") + "%)");
        }

        static float SubjectShareAt(string[] objects, Vector3 eye, Vector3 look)
        {
            int px = 0;
            for (int k = 0; k < objects.Length; k++)
            {
                var go = GameObject.Find(objects[k]);
                if (go == null) continue;
                px += EntranceCensus.Draw(go.transform, eye, look).Pixels;
            }
            return (float)px / (EntranceCensus.ScreenW * EntranceCensus.ScreenH);
        }
    }
}
