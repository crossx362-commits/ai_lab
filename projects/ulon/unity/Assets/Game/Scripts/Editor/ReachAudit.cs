using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ulon.Editor
{
    /// <summary>
    /// **감사 도구(게이트 아님)** — 상호작용 사거리를 대상 **중심**에서 재는 곳에 큰 대상이 있는지 본다.
    /// 은행이 풍차라서 「옆에 붙어 서도 사거리 밖」이던 모순(검수 랩 B)이 다른 곳에도 잠들어 있는지
    /// 확인만 하기 위한 것이다. 판정하지 않고 표만 찍는다.
    ///
    /// 실행: Unity -batchmode -executeMethod Ulon.Editor.ReachAudit.Run
    /// </summary>
    public static class ReachAudit
    {
        public static void Run()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.rootCount == 0)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity");

            var all = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Debug.Log("[Ulon] 사거리 감사 — 대상 | 사거리 | 발자국 반경 | 중심 기준이 모순인가");
            for (int i = 0; i < all.Length; i++)
            {
                var f = all[i].GetType().GetField("InteractRange", BindingFlags.Instance | BindingFlags.Public);
                if (f == null || f.FieldType != typeof(float))
                    continue;
                float range = (float)f.GetValue(all[i]);
                float foot = Footprint(all[i].transform);
                bool contradiction = foot >= range;      // 표면에 붙어 서면 중심 거리가 사거리를 넘는다
                Debug.Log("[Ulon] 사거리 " + all[i].GetType().Name + " " + all[i].name +
                          " | " + range.ToString("0.00") + "m | 발자국 " + foot.ToString("0.00") + "m | " +
                          (contradiction ? "**모순 가능**" : "여유 " + (range - foot).ToString("0.00") + "m"));
            }
        }

        static float Footprint(Transform t)
        {
            var rends = t.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0)
                return 0f;
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++)
                b.Encapsulate(rends[i].bounds);
            return Mathf.Max(b.extents.x, b.extents.z);
        }
    }
}
