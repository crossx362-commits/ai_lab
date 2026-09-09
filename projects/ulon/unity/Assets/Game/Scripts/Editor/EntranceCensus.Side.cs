using UnityEngine;
using Ulon.Shared;

namespace Ulon.Editor
{
    public static partial class EntranceCensus
    {
        /// <summary>
        /// **옆에서 보면 어둠이 문틀 밖으로 새는가**(`60_d3_entrance_45` 화면, 2026-09-09).
        ///
        /// 정면(`07`·`09`·`11`)에서는 문구멍이 아치 안에 얌전히 앉아 있는데, 45° 옆에서 보면
        /// 검은 판이 문틀 오른쪽으로 삐져나와 **아치 안의 어둠**이 아니라 **문 앞에 세운 검은 판**으로
        /// 읽힌다. 판은 문 안쪽 0.55m에 서고 폭은 기둥 **중심 간격**이라, 시선이 비스듬해지면
        /// 기둥이 판의 옆구리를 다 못 가린다.
        ///
        /// 재는 것: 45° 눈에서 **포털 픽셀 중 문틀 실루엣이 덮지 않는 비율**. 문틀을 그릴 때는
        /// 포털을 잠깐 끄고 그린다(같은 가지에 있어 함께 찍히면 「자기가 자기를 가렸다」가 된다).
        /// 0이면 완전히 아치 안, 1이면 통째로 밖이다.
        /// </summary>
        public static bool ReadPortalSideLeak(string rootName, float ex, float ez, float yaw, float sideDeg,
                                              out float leak, out string why)
        {
            leak = 0f;
            why = "";
            var root = GameObject.Find(rootName);
            if (root == null) { why = rootName + "을 못 찾았습니다"; return false; }
            var portal = FindChild(root.transform, VisualSliceBuilder.EntrancePortalObject);
            var frame = FindChild(root.transform, VisualSliceBuilder.EntranceFrameObject);
            if (portal == null || frame == null) { why = "문구멍 판 또는 문틀이 없습니다"; return false; }

            var front = EntranceGeom.Front(ex, ez, yaw);
            var dir = Quaternion.Euler(0f, sideDeg, 0f) * new Vector3(front.x, 0f, front.y);
            var hits = Physics.RaycastAll(new Vector3(ex, 500f, ez), Vector3.down, 1000f);
            float gy = float.MaxValue;
            for (int i = 0; i < hits.Length; i++) gy = Mathf.Min(gy, hits[i].point.y);
            if (gy == float.MaxValue) gy = 0f;
            var look = new Vector3(ex, gy + 1.4f, ez);
            var eye = look + dir * 7f + Vector3.up * 2.4f;

            bool wasOn = portal.gameObject.activeSelf;
            portal.gameObject.SetActive(false);
            Silhouette frameSil;
            try { frameSil = Draw(frame, eye, look); }
            finally { portal.gameObject.SetActive(wasOn); }
            var portalSil = Draw(portal, eye, look);
            if (portalSil.Pixels == 0) { why = "포털이 화면에 안 잡힙니다"; return false; }

            // **문틀 「안쪽」은 비어 있다** — 아치 가운데는 원래 뚫린 자리라, 픽셀 그대로 비교하면
            // 정면에서도 60%가 「밖」으로 나온다(첫 판 실측). 그래서 문틀의 **바깥 윤곽**을 만든다:
            // 줄마다 문틀이 찍힌 가장 왼쪽~가장 오른쪽을 채운 마스크. 그 밖에 있는 어둠만 「샌 것」이다.
            int outside = 0;
            for (int y = 0; y < ScreenH; y++)
            {
                int lo = int.MaxValue, hi = int.MinValue;
                for (int x = 0; x < ScreenW; x++)
                    if (frameSil.Depth[y * ScreenW + x] < float.MaxValue) { if (x < lo) lo = x; if (x > hi) hi = x; }
                for (int x = 0; x < ScreenW; x++)
                {
                    if (portalSil.Depth[y * ScreenW + x] == float.MaxValue)
                        continue;
                    if (lo == int.MaxValue || x < lo || x > hi)
                        outside++;
                }
            }
            leak = (float)outside / portalSil.Pixels;
            return true;
        }

        /// <summary>세 입구를 좌우 45°에서 나란히 센다(정면 0°도 같이 — 대조군).</summary>
        public static void RunPortalSide()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            Debug.Log("[Census] 문구멍이 문틀 밖으로 새는 비율 — 0°(대조군) / ±45°");
            foreach (var d in EntranceGeom.All)
            {
                string line = d.Root;
                foreach (float side in new[] { 0f, 45f, -45f })
                {
                    if (!ReadPortalSideLeak(d.Root, d.X, d.Z, d.Yaw, side, out float leak, out string why))
                    {
                        line += " · " + side + "° 못 쟀다(" + why + ")";
                        continue;
                    }
                    line += " · " + side.ToString("0") + "° " + (leak * 100f).ToString("0.0") + "%";
                }
                // 판별 테스트 — 판을 문 앞으로 1.2m 내밀면 값이 커져야 한다. 안 커지면 이 자는 아무것도 안 센다.
                var probe = FindChild(GameObject.Find(d.Root).transform, VisualSliceBuilder.EntrancePortalObject);
                if (probe != null)
                {
                    var keep = probe.position;
                    var f = EntranceGeom.Front(d.X, d.Z, d.Yaw);
                    probe.position = keep + new Vector3(f.x, 0f, f.y) * 1.2f;
                    ReadPortalSideLeak(d.Root, d.X, d.Z, d.Yaw, 45f, out float moved, out _);
                    probe.position = keep;
                    line += " · [판별] 앞으로 1.2m 내밀면 45°에서 " + (moved * 100f).ToString("0.0") + "%";
                }
                Debug.Log("[Census] " + line);
            }
            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(0);
        }
    }
}
