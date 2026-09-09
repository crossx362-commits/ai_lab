using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **죽은 장 훑기 — 눈이 아니라 자로**(검수 착수 승인, 2026-09-09).
    ///
    /// 「이 샷의 주인공이 프레임에 얼마나 담겼나」를 센다. 카메라는 **찍는 쪽과 같은 목록**
    /// (`QaShots.BuildShots`)에서 받고, 주인공은 원장(`ShotSubject`)에서 받는다 — 두 벌을 두면
    /// 재는 자와 찍는 자가 다른 세계를 본다.
    ///
    /// 재는 것: 주인공 실루엣이 화면(320×180, `qa_shots`와 같은 화각 55°)에서 차지하는 **픽셀 몫**.
    /// 하한은 이 랩에서 정하지 않는다 — **살아 있는 샷들을 먼저 재고** 그 분포를 보고 정한다
    /// (하한을 먼저 정하면 그 수가 곧 결론이 된다).
    ///
    /// **이 자가 못 보는 것**: ①지형이 주인공인 조망은 못 잰다(원장에 빈 배열, 「못 잼」으로 찍는다)
    /// ②밝기·구도는 안 본다 — 주인공이 담겼어도 어둡거나 뒤통수일 수 있다(그건 다른 자의 몫).
    /// </summary>
    public static class ShotCensus
    {
        public static void RunSubjectShare()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);

            int total = EntranceCensus.ScreenW * EntranceCensus.ScreenH;
            int measured = 0, unmeasurable = 0, noSubject = 0, missing = 0;
            var shots = QaShots.BuildShots();
            for (int i = 0; i < shots.Length; i++)
            {
                string name = QaShots.NameOf(shots[i]);
                if (!ShotSubject.Table.TryGetValue(name, out string[] objects))
                {
                    noSubject++;
                    continue;                       // 원장에 안 적힌 샷은 이번 대상이 아니다(검수가 고른 목록)
                }
                if (objects.Length == 0)
                {
                    unmeasurable++;
                    Debug.Log("[장] " + name + " — **못 잼**(지형이 주인공, 실루엣 자는 메시만 그린다)");
                    continue;
                }
                QaShots.EyeOf(shots[i], out Vector3 eye, out Vector3 look);
                int px = 0;
                string absent = "";
                for (int k = 0; k < objects.Length; k++)
                {
                    var go = GameObject.Find(objects[k]);
                    if (go == null) { absent += " " + objects[k]; continue; }
                    px += EntranceCensus.Draw(go.transform, eye, look).Pixels;
                }
                if (absent.Length > 0)
                {
                    missing++;
                    Debug.LogWarning("[장] " + name + " — 주인공을 씬에서 못 찾음:" + absent +
                                     " (원장이 낡았거나 이름이 바뀐 것이다)");
                }
                measured++;
                Debug.Log("[장] " + name + " — 주인공 점유 " + (100f * px / total).ToString("0.0") + "% (" + px + "px)");
            }
            Debug.Log("[장] 요약 — 잰 샷 " + measured + " · 못 잼(지형) " + unmeasurable +
                      " · 주인공 못 찾음 " + missing + " · 원장 밖 " + noSubject);
            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(0);
        }
    }
}
