using UnityEngine;
using Ulon.Shared;

namespace Ulon.Editor
{
    public static partial class EntranceCensus
    {
        /// <summary>
        /// **등불이 문설주 그림자에 묻히나 — 센다**(검수 다음 순서 3, 2026-09-09).
        ///
        /// `07_d1_entrance`에서 문 양옆 등불이 어둡게 보인다는 **인상**이 있다. 인상은 가설이므로
        /// 고치기 전에 두 축으로 잰다:
        ///   ① **햇빛이 닿나** — 등불 몸 표본점에서 **태양 쪽으로** 광선을 쏴 막히는지. 막은 것의
        ///      이름을 적는다(문설주인지 문틀인지 언덕인지 — 이름이 없으면 무엇을 옮길지 모른다).
        ///   ② **제 불빛은 켜져 있나** — 등불 옆에 세운 점광(`DungeonEntranceLight`)의 세기·거리와
        ///      등불 몸까지의 거리. 그림자 안이어도 제 불이 세면 화면에서는 읽힌다.
        /// 세 입구를 같이 재서 **D1만 그런지**를 가른다.
        ///
        /// **판별 테스트**: 지금 **안 막힌** 등불 앞에 판을 세우고 다시 쏜다 — 막혀야 한다.
        /// 안 막히면 이 자는 차폐를 못 재고 있는 것이다(0을 보고하는 자에는 판별을 붙인다).
        ///
        /// **이 자가 못 보는 것**: 화면 밝기는 안 잰다(그림자는 기하, 밝기는 렌더다). 화면은 `qa_shots`가 판정한다.
        ///
        /// **결과(2026-09-09) — 의심은 기각**: `07/D1`은 등불 **둘 다 햇빛 막힘 0%**다. 문설주에 가리는
        /// 것은 D2 등불 2(67%)·D3 등불 1(73%)뿐이고, 그것도 등불이 기둥 옆에 서니 태양 각도에 따라
        /// 한쪽이 그늘지는 **자연스러운** 결과다. 게다가 등불마다 제 점광(3.2세기/9m)이 0.6m 옆에 있어
        /// `09`·`11` 화면에서 둘 다 밝게 읽힌다. 그래서 **고치지 않는다** — 자만 남긴다.
        /// (판별 3/3 「예」로 이 자가 차폐를 실제로 문다는 것은 확인했다.)
        /// </summary>
        public static void RunLampShade()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);

            Light sun = null;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (l.type == LightType.Directional && l.enabled && (sun == null || l.intensity > sun.intensity))
                    sun = l;
            if (sun == null)
            {
                Debug.LogError("[등불] 태양(Directional Light)이 없습니다 — 그림자를 잴 수 없습니다.");
                if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(1);
                return;
            }
            var toSun = -sun.transform.forward;
            Debug.Log("[등불] 태양 방향 " + toSun.ToString("0.00") + " · 세기 " + sun.intensity.ToString("0.00") +
                      " · 그림자 " + sun.shadows);

            LampShadeOne("07/D1", Dungeon1.RootObject, toSun);
            LampShadeOne("09/D2", Dungeon2.RootObject, toSun);
            LampShadeOne("11/D3", Dungeon3.RootObject, toSun);
            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(0);
        }

        static void LampShadeOne(string tag, string rootName, Vector3 toSun)
        {
            var root = GameObject.Find(rootName);
            // **입구 등불만 센다** — 던전 실내 등불은 지하라 100% 막히는 것이 당연하고, 섞으면
            // 「막힘 100%」가 안건을 삼킨다(첫 판에서 실제로 그랬다). 기둥 옆의 것만 고른다.
            var pillars = FindChildren(root != null ? root.transform : null, "EntrancePillar");
            var lanterns = NearPillars(FindChildren(root != null ? root.transform : null, "lantern"), pillars);
            if (lanterns.Count == 0)
            {
                Debug.Log("[등불] " + tag + " — 등불이 없습니다.");
                return;
            }
            Transform clearOne = null;      // 판별은 **지금 안 막힌 등불**로 해야 판별이다
            var lights = new System.Collections.Generic.List<Light>();
            if (root != null)
                foreach (var l in root.GetComponentsInChildren<Light>(true))
                    if (l.type == LightType.Point)
                        lights.Add(l);

            for (int i = 0; i < lanterns.Count; i++)
            {
                var t = lanterns[i];
                var r = t.GetComponentInChildren<Renderer>();
                if (r == null)
                    continue;
                var b = r.bounds;
                int blocked = 0, n = 0;
                string who = "";
                // 등불 몸의 위·중간·아래 세 높이 × 앞뒤좌우 다섯 자리 — 한 점만 쏘면 우연이 판정을 먹는다.
                for (int hy = 0; hy < 3; hy++)
                for (int k = 0; k < 5; k++)
                {
                    var p = new Vector3(
                        b.center.x + (k == 1 ? b.extents.x * 0.6f : k == 2 ? -b.extents.x * 0.6f : 0f),
                        Mathf.Lerp(b.min.y + 0.1f, b.max.y - 0.05f, hy * 0.5f),
                        b.center.z + (k == 3 ? b.extents.z * 0.6f : k == 4 ? -b.extents.z * 0.6f : 0f));
                    n++;
                    if (!Physics.Raycast(p + toSun * 0.05f, toSun, out RaycastHit hit, 60f))
                        continue;
                    if (hit.transform.IsChildOf(t))
                        continue;                       // 제 몸에 맞은 것은 차폐가 아니다
                    blocked++;
                    if (who.Length == 0)
                        who = PathOf(hit.transform) + " " + hit.distance.ToString("0.0") + "m";
                }
                float near = float.MaxValue;
                Light own = null;
                for (int j = 0; j < lights.Count; j++)
                {
                    float d = Vector3.Distance(lights[j].transform.position, b.center);
                    if (d < near) { near = d; own = lights[j]; }
                }
                if (blocked == 0 && clearOne == null)
                    clearOne = t;
                Debug.Log("[등불] " + tag + " 등불 " + (i + 1) + " — 햇빛 막힘 " + blocked + "/" + n +
                          " (" + (100f * blocked / n).ToString("0") + "%)" + (who.Length > 0 ? " 막은 것 " + who : "") +
                          " · 제 점광 " + (own == null ? "없음" : own.intensity.ToString("0.0") + "세기/" +
                          own.range.ToString("0") + "m, 등불까지 " + near.ToString("0.0") + "m"));
            }

            // **판별 테스트** — 등불 앞(태양 쪽 2m)에 임시 판을 세우고 다시 쏜다. 막혀야 한다.
            // 첫 판에는 등불을 땅속으로 내려 봤는데 「안 막힘」이 나왔다 — 광선이 지형을 **뒤에서**
            // 때려 통과한 것이지 자가 죽은 것이 아니었다. 판별은 **확실히 막는 것**으로 해야 판별이다.
            var victim = clearOne;
            var vr = victim != null ? victim.GetComponentInChildren<Renderer>() : null;
            if (vr != null)
            {
                var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blocker.name = "LampShadeProbeBlocker";
                // 판은 **광선 시작점 밖**에 세운다 — 첫 판에는 중심 2m·한 변 4m라 시작점이 판 **안**에
                // 있었고, 콜라이더 내부에서 출발한 광선은 맞지 않아 「아니오」가 나왔다(자가 아니라 판별이 틀렸다).
                blocker.transform.position = vr.bounds.center + toSun * 1.2f;
                blocker.transform.localScale = new Vector3(1f, 1f, 1f);
                Physics.SyncTransforms();
                bool hit2 = Physics.Raycast(vr.bounds.center + toSun * 0.05f, toSun, out RaycastHit h2, 60f);
                string what = hit2 ? PathOf(h2.transform) : "(없음)";
                Object.DestroyImmediate(blocker);
                Debug.Log("[등불] " + tag + " [판별] 태양 쪽 1.2m에 판을 세우면 막힘 " +
                          (hit2 && what.Contains("LampShadeProbeBlocker") ? "예" : "아니오(" + what + ")"));
            }
        }
    }
}
