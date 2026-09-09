using System.Collections.Generic;
using UnityEngine;
using Ulon.Shared;

namespace Ulon.Editor
{
    /// <summary>
    /// **후보 자리를 고르는 루프**(랩 ㉾ 분할). 담는 것: 씬의 물건을 잠깐 옮겨 재고 **제자리로 돌리는**
    /// 탐색. 안 담는 것: 지표(읽는 쪽)와 실루엣(자). 고른 값은 상수로 박지 말고 **규칙**으로 옮겨 적어라.
    /// </summary>
    public static partial class EntranceCensus
    {

        /// <summary>
        /// **자리를 고르기 전에 후보를 전부 재 본다**(고르는 루프 — 고른 결과를 상수로 박지 말고
        /// **규칙**으로 옮겨 적어라: 거리·면·좌우·등불은 **같이 골라진 한 칸**이다).
        /// 씬의 물건을 잠깐 옮겨 재고 **반드시 제자리로 돌린다** — 이 셈은 아무것도 안 고친다.
        /// 놓는 식은 빌더와 **같은 함수**(`VisualSliceBuilder.BannerPose`·`LanternPose`)를 쓴다.
        /// </summary>
        public static void RunBannerProbe()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            Probe("07/D1", Dungeon1.RootObject, Dungeon1.EntranceX, Dungeon1.EntranceZ);
            Probe("09/D2", Dungeon2.RootObject, Dungeon2.EntranceX, Dungeon2.EntranceZ);
            Probe("11/D3", Dungeon3.RootObject, Dungeon3.EntranceX, Dungeon3.EntranceZ);
            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(0);
        }

        static void Probe(string tag, string rootName, float ex, float ez)
        {
            var root = GameObject.Find(rootName);
            if (root == null) { Debug.Log("[배너탐색] " + tag + " 입구 없음"); return; }
            var pillars = FindChildren(root.transform, "EntrancePillar");
            var banners = NearPillars(FindChildren(root.transform, "banner"), pillars);
            var lanterns = NearPillars(FindChildren(root.transform, "lantern"), pillars);
            var portal = FindChild(root.transform, VisualSliceBuilder.EntrancePortalObject);
            if (pillars.Count < 2 || banners.Count == 0 || portal == null)
            {
                Debug.Log("[배너탐색] " + tag + " 못 잼(기둥 " + pillars.Count + " 배너 " + banners.Count + ")");
                return;
            }
            var p1 = pillars[0].position; var p2 = pillars[1].position;
            var side = (p2 - p1); side.y = 0f; side = side.normalized;
            var center = (p1 + p2) * 0.5f;
            var toPortal = portal.position - center; toPortal.y = 0f;
            var inward = toPortal.sqrMagnitude > 0.01f ? toPortal.normalized : Vector3.forward;
            float approachYaw = Mathf.Atan2(inward.x, inward.z) * Mathf.Rad2Deg;

            var home = new List<(Transform t, Vector3 pos, Quaternion rot)>();
            foreach (var b in banners) home.Add((b, b.position, b.rotation));
            foreach (var l in lanterns) home.Add((l, l.position, l.rotation));

            // **셋을 함께 고른다** — 배너만 움직이면 등불이 사이에 끼고, 등불만 움직이면 배너가 허공에 뜬다.
            float[] pushes = { 0f };                     // 배너는 매달려 있어 고정
            float[] faces = { 0f };
            float[] lamps = { 0.6f, 0f, -0.6f };         // 등불 앞뒤
            float[] laterals = { 0f, 0.5f, 1.0f };       // 등불을 문 바깥 옆으로
            foreach (float push in pushes)
                foreach (float face in faces)
                    foreach (float lamp in lamps)
                        foreach (float lat in laterals)
                        {
                            // **배너는 이제 기둥에 매달려 있으므로 건드리지 않는다**(검수 판정 2026-09-09).
                            // 남은 자유도는 등불뿐이라 이 루프는 등불만 훑는다.
                            for (int li = 0; li < lanterns.Count; li++)
                            {
                                int sideSign = li % 2 == 0 ? -1 : 1;
                                var pil = center + VisualSliceBuilder.EntranceSide(approachYaw) *
                                          (VisualSliceBuilder.EntranceDoorHalf * sideSign);
                                lanterns[li].position = VisualSliceBuilder.LanternPose(pil, approachYaw, sideSign, lamp, lat);
                            }
                            Physics.SyncTransforms();
                            if (!ReadEntrance(rootName, ex, ez, out Readout r)) continue;
                            Debug.Log("[배너탐색] " + tag + " 배너 " + push.ToString("0.0") + "m/" + face.ToString("0") +
                                      "°/옆" + lat.ToString("0.0") + "m · 등불 " + lamp.ToString("0.0") +
                                      "m → 문 " + (r.BannerMouth * 100f).ToString("0") +
                                      "% · 등불겹침 " + (r.BannerLantern * 100f).ToString("0") +
                                      "% · 기둥겹침 " + (r.BannerPillar * 100f).ToString("0") +
                                      "% · 기둥노출 " + (r.PillarShow * 100f).ToString("0") +
                                      "% · 등불이문 " + (r.LanternMouth * 100f).ToString("0") +
                                      "% · 등불보임 " + (r.LanternShow * 100f).ToString("0") + "%");
                        }

            foreach (var h in home) { h.t.position = h.pos; h.t.rotation = h.rot; }
            Physics.SyncTransforms();
            Debug.Log("[배너탐색] " + tag + " 제자리로 되돌렸습니다 · approachYaw " + approachYaw.ToString("0.0") + "°");
        }
    }
}
