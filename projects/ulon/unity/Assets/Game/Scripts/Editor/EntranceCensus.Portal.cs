using System.Collections.Generic;
using UnityEngine;
using Ulon.Shared;

namespace Ulon.Editor
{
    /// <summary>
    /// **문이 닫혀 있나 — 「샘」 자**(검수 지시 2026-09-09).
    ///
    /// 지금까지의 입구 자는 전부 「문구멍 **앞을** 무엇이 가리나」를 물었다. 그래서 포털이 옆으로
    /// 서거나 문 밖으로 나가면 **가리는 것이 없어 0%**, 곧 초록불이었다(실물: `11`의 포털이 옆으로
    /// 서서 화면에서 검은 막대기 하나, 문구멍은 활짝 열려 들판이 그대로 보였다).
    ///
    /// 그래서 **문구멍 띠를 포털이 아니라 문틀에서 유도**한다 — 포털이 어디로 가든 띠는 제자리다.
    /// 띠는 두 기둥 사이·지표에서 기둥 꼭대기까지의 사각형이고, 서 있는 방향은 `Dungeon*.EntranceYaw`
    /// 원장에서 온다(붙이는 쪽과 재는 쪽이 같은 값을 읽는다).
    ///
    /// 그 띠 안에서 둘을 센다:
    /// - **채움**(ⓐ): 포털이 띠의 몇 %를 채우나 — 문짝이 문에 서 있으면 높다.
    /// - **샘**(ⓑ): 던전의 어떤 것도 없어 **바깥 세계가 그대로 보이는** 픽셀의 몫 — 0이어야 한다.
    ///
    /// **이 자가 못 보는 것**: 색이다. 검은 판이 서 있는지 흰 판이 서 있는지는 안 묻는다(화면이 답한다).
    /// 그리고 띠는 **문틀이 서 있어야** 유도된다 — 기둥을 못 찾으면 숫자를 내지 말고 사유를 적는다.
    /// </summary>
    public static partial class EntranceCensus
    {
        public struct PortalRead
        {
            public float Fill;        // 띠에서 포털이 채운 몫
            public float Leak;        // 띠에서 바깥 세계가 그대로 보이는 몫(샘)
            public int BandPixels;    // 띠 픽셀 수 — 0이면 잰 것이 없다
            public string What;       // 무엇을 셌는지(이름과 픽셀 수) 또는 못 잰 사유
        }

        /// <summary>
        /// 「문 자리에 있다」고 볼 깊이 여유 — **문틀 두께에서 유도한다**(기둥 반폭 1.12m).
        /// 처음엔 0.6m로 뒀다가 던전 1이 **채움 0%**로 나왔다: 판은 제자리에 있는데(빌더가 안쪽으로
        /// 0.55m 물린다) 띠와의 깊이 차가 **0.68m**여서 문턱 하나 차이로 「없다」가 됐다.
        /// 문틀 안에 있으면 문에 선 것이다 — 여유는 취향이 아니라 **문틀에서** 나온다.
        /// </summary>
        public static float PortalNearBand => VisualSliceBuilder.EntrancePillarHalf;

        public static bool ReadPortal(string rootName, float ex, float ez, float yaw, out PortalRead r)
        {
            r = new PortalRead();
            var root = GameObject.Find(rootName);
            if (root == null) { r.What = "(던전 루트 없음: " + rootName + ")"; return false; }
            ShotEye(ex, ez, out Vector3 eye, out Vector3 look);

            var pillars = FindChildren(root.transform, "EntrancePillar");
            if (pillars.Count < 2) { r.What = "(문설주가 " + pillars.Count + "개 — 띠를 유도할 수 없다)"; return false; }
            // 기둥 꼭대기 = 문구멍의 윗변(상인방은 그 위에 얹힌다). 지표는 문 자리에서 쏴서 읽는다.
            float top = float.MinValue;
            for (int i = 0; i < pillars.Count; i++)
                foreach (var rend in pillars[i].GetComponentsInChildren<Renderer>(true))
                    if (rend.enabled)
                        top = Mathf.Max(top, rend.bounds.max.y);
            if (top <= float.MinValue) { r.What = "(문설주에 켜진 렌더러가 없다)"; return false; }
            float ground = GroundAt(new Vector3(ex, top + 50f, ez));

            // 문이 선 방향 — 원장에서 온다(빌더 `EntranceSide`와 같은 정의).
            float rad = yaw * Mathf.Deg2Rad;
            var inward = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
            var side = new Vector3(inward.z, 0f, -inward.x);
            float half = VisualSliceBuilder.EntranceDoorHalf;
            var band = QuadSilhouette(
                new Vector3(ex, ground, ez) - side * half,
                new Vector3(ex, ground, ez) + side * half,
                new Vector3(ex, top, ez) + side * half,
                new Vector3(ex, top, ez) - side * half, eye, look);
            r.BandPixels = band.Pixels;
            if (band.Pixels == 0) { r.What = "(문구멍 띠가 화면 밖이다)"; return false; }

            var portal = FindChild(root.transform, VisualSliceBuilder.EntrancePortalObject);
            var portalSil = portal == null ? new Silhouette() : Draw(portal, eye, look);
            var all = Draw(root.transform, eye, look);

            int fill = 0, leak = 0;
            for (int i = 0; i < band.Depth.Length; i++)
            {
                if (band.Depth[i] == float.MaxValue)
                    continue;
                // **띠보다 뒤에 있는 포털 픽셀은 문을 막은 것이 아니다** — 앞뒤를 가른다.
                if (portalSil.Depth[i] < band.Depth[i] + PortalNearBand)
                    fill++;
                // 던전의 어떤 것도 띠 자리에 없으면 그 픽셀로 **바깥 세계가 보인다**.
                if (all.Depth[i] > band.Depth[i] + PortalNearBand)
                    leak++;
            }
            r.Fill = fill / (float)band.Pixels;
            r.Leak = leak / (float)band.Pixels;
            r.What = "띠 " + band.Pixels + "px(문설주 " + pillars.Count + "개에서 유도, 높이 " +
                     (top - ground).ToString("0.0") + "m) · 포털 " + portalSil.Pixels + "px" +
                     (portal == null ? " · 포털 없음" : " · 회전 " + portal.rotation.eulerAngles.y.ToString("0") + "°");
            return true;
        }

        /// <summary>세계의 사각형 하나를 화면에 찍는다 — 문구멍 띠처럼 **물건이 아닌 자리**를 재려면 필요하다.</summary>
        static Silhouette QuadSilhouette(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 eye, Vector3 look)
        {
            var sil = new Silhouette();
            var cam = MakeCam(eye, look);
            var pts = new[] { a, b, c, d };
            var sx = new float[4]; var sy = new float[4]; var dp = new float[4]; var ok = new bool[4];
            for (int i = 0; i < 4; i++)
                ok[i] = Project(in cam, pts[i], out sx[i], out sy[i], out dp[i]);
            if (ok[0] && ok[1] && ok[2])
                Raster(sil, sx[0], sy[0], dp[0], sx[1], sy[1], dp[1], sx[2], sy[2], dp[2]);
            if (ok[0] && ok[2] && ok[3])
                Raster(sil, sx[0], sy[0], dp[0], sx[2], sy[2], dp[2], sx[3], sy[3], dp[3]);
            for (int i = 0; i < sil.Depth.Length; i++) if (sil.Depth[i] < float.MaxValue) sil.Pixels++;
            return sil;
        }

        public static void RunPortalCensus()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            PortalReport("07/D1", Dungeon1.RootObject, Dungeon1.EntranceX, Dungeon1.EntranceZ, Dungeon1.EntranceYaw);
            PortalReport("09/D2", Dungeon2.RootObject, Dungeon2.EntranceX, Dungeon2.EntranceZ, Dungeon2.EntranceYaw);
            PortalReport("11/D3", Dungeon3.RootObject, Dungeon3.EntranceX, Dungeon3.EntranceZ, Dungeon3.EntranceYaw);
            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(0);
        }

        static void PortalReport(string tag, string rootName, float ex, float ez, float yaw)
        {
            if (!ReadPortal(rootName, ex, ez, yaw, out PortalRead r))
            {
                Debug.Log("[문/화면] " + tag + " — 못 쟀다: " + r.What);
                return;
            }
            Debug.Log("[문/화면] " + tag + " — **채움 " + (r.Fill * 100f).ToString("0") + "%** · **샘 " +
                      (r.Leak * 100f).ToString("0") + "%** · " + r.What);
        }
    }
}
