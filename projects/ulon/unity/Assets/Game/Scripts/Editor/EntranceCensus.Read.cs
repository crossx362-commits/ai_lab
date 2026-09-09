using System.Collections.Generic;
using UnityEngine;
using Ulon.Shared;

namespace Ulon.Editor
{
    /// <summary>
    /// **입구 한 곳을 화면에서 읽는 쪽**(랩 ㉾ 분할 — 자는 `EntranceCensus.Screen.cs`, 고르는 루프는
    /// `EntranceCensus.Probe.cs`). 담는 것: 실루엣에서 **지표를 뽑는 규칙**과 그 보고.
    /// 안 담는 것: 실루엣을 어떻게 그리나(자), 후보를 어떻게 고르나(루프).
    /// </summary>
    public static partial class EntranceCensus
    {

        /// <summary>
        /// **배너가 화면에서 무엇을 가리고, 무엇에 걸린 것으로 보이나**를 센다(고치지 않는다 — 세기만).
        /// 배치로 `RunBannerScreen`을 부른다. 첫 줄에 **무엇을 셌는지 이름과 픽셀 수**를 찍는다 —
        /// 이름 없는 셈은 엉뚱한 것을 세고도 초록불을 낸다(왕관 사고 2026-09-09).
        /// </summary>
        public static void RunBannerScreen()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            Report("07/D1", Dungeon1.RootObject, Dungeon1.EntranceX, Dungeon1.EntranceZ);
            Report("09/D2", Dungeon2.RootObject, Dungeon2.EntranceX, Dungeon2.EntranceZ);
            Report("11/D3", Dungeon3.RootObject, Dungeon3.EntranceX, Dungeon3.EntranceZ);
            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(0);
        }

        /// <summary>입구 한 곳을 화면에서 읽은 결과 — 자가 늘어나 이름을 붙였다(인자 여섯은 못 읽는다).</summary>
        public struct Readout
        {
            public float BannerMouth;     // 배너가 보이는 문구멍을 덮은 몫(배너 한 장씩 재서 **최악**)
            public float BannerLantern;   // 배너와 등불의 상호 겹침
            public float BannerPillar;    // 배너가 기둥과 겹친 몫 — **0이면 아무 데도 안 걸렸다**(가장 낮은 배너)
            public float PillarShow;      // 기둥 픽셀 중 배너 밖으로 보이는 몫 — 낮으면 배너가 걸린 것을 삼켰다
            public float LanternMouth;    // 등불이 보이는 문구멍을 덮은 몫
            public float LanternShow;     // 등불 픽셀 중 **가려지지 않고 보이는** 몫 — 기둥 뒤로 물리면 머리만 남는다
            public string What;           // 무엇을 셌는지(이름과 픽셀 수)
        }

        /// <summary>
        /// **「붙어 보이나」는 거리가 아니라 두 조건이다**(검수 지시 2026-09-09):
        /// ⓐ배너와 기둥이 **겹칠 것** ⓑ그 기둥이 배너 **밖으로 삐져나올 것**.
        /// 거리 0px은 「맞닿았다」이지 「걸려 보인다」가 아니었다 — 화면에서 배너는 여전히 공중에 뜬 천이었다.
        /// ⓑ가 없으면 「배너 뒤에 완전히 숨은 기둥」이 통과한다.
        /// </summary>
        public static bool ReadEntrance(string rootName, float ex, float ez, out Readout r)
        {
            r = new Readout();
            var root = GameObject.Find(rootName);
            if (root == null) { r.What = "(입구 없음: " + rootName + ")"; return false; }
            ShotEye(ex, ez, out Vector3 eye, out Vector3 look);
            var pillars = FindChildren(root.transform, "EntrancePillar");
            var banners = NearPillars(FindChildren(root.transform, "banner"), pillars);
            var lanterns = NearPillars(FindChildren(root.transform, "lantern"), pillars);
            var portal = FindChild(root.transform, VisualSliceBuilder.EntrancePortalObject);
            var frame = FindChild(root.transform, VisualSliceBuilder.EntranceFrameObject);
            // **배너가 없는 것은 정상이다**(2026-09-09 걷었다). 없으면 배너 지표는 0으로 두고
            // 등불·문구멍만 읽는다 — 「없는 것을 못 쟀다」고 빨간불을 내면 그건 자가 아니라 고집이다.
            if (portal == null || pillars.Count == 0)
            {
                r.What = "(포털 " + (portal == null ? "없음" : "있음") + " · 기둥 " + pillars.Count + " — 셀 수 없다)";
                return false;
            }
            // **한 장씩 잰다 — 합치면 한 장만 걸려 있어도 통과한다**(2026-09-09 화면에서 걸렸다:
            // 왼쪽 배너는 기둥에 걸렸는데 오른쪽 배너는 허공에 떴고, 합친 자는 「38% 겹침」이라 했다).
            // 「최악」을 쓰는 이유: 화면을 망치는 것은 평균이 아니라 가장 나쁜 한 장이다.
            var lantern = Union(lanterns, eye, look);
            var pillar = Union(pillars, eye, look);
            var mouth = VisibleMouth(portal, frame, eye, look);
            var banner = Union(banners, eye, look);
            var blockers = Union(new List<Transform>(pillars) { }, eye, look);
            foreach (var b in banners)
            {
                var one = Draw(b, eye, look);
                for (int i = 0; i < blockers.Depth.Length; i++)
                    if (one.Depth[i] < blockers.Depth[i]) blockers.Depth[i] = one.Depth[i];
            }

            r.BannerPillar = 1f;
            r.PillarShow = 1f;
            r.LanternShow = 1f;
            int worstBannerPx = 0;
            foreach (var b in banners)
            {
                var one = Draw(b, eye, look);
                if (one.Pixels == 0) continue;
                r.BannerMouth = Mathf.Max(r.BannerMouth, CoverShare(one, mouth));
                r.BannerLantern = Mathf.Max(r.BannerLantern, OverlapShare(one, lantern));
                r.BannerPillar = Mathf.Min(r.BannerPillar, OverlapShare(one, pillar));
                worstBannerPx = Mathf.Max(worstBannerPx, one.Pixels);
            }
            foreach (var l in lanterns)
            {
                var one = Draw(l, eye, look);
                if (one.Pixels == 0) continue;
                r.LanternMouth = Mathf.Max(r.LanternMouth, CoverShare(one, mouth));
                // **등불이 스스로 보이나** — 기둥 뒤로 물리면 머리만 삐죽 남는다(화면에서 그렇게 됐다).
                // 기둥과 배너를 **합쳐서 한 번만** 뺀다. 따로 빼면 둘 다 가린 픽셀이 두 번 깎여
                // 「보임 −43%」 같은 값이 나온다(실제로 나왔다 — 음수가 나오면 자를 의심하라).
                r.LanternShow = Mathf.Min(r.LanternShow, 1f - CoverShare(blockers, one));
            }
            r.PillarShow = pillar.Pixels == 0 ? 0f : 1f - CoverShare(banner, pillar);
            r.What = "배너 " + banners.Count + "개(최대 " + worstBannerPx + "px) · 등불 " + lanterns.Count + "개 " +
                     lantern.Pixels + "px · 기둥 " + pillar.Pixels + "px · 보이는 구멍 " + mouth.Pixels + "px";
            return true;
        }

        static void Report(string tag, string rootName, float ex, float ez)
        {
            if (!ReadEntrance(rootName, ex, ez, out Readout r)) { Debug.Log("[입구/화면] " + tag + " " + r.What); return; }
            Debug.Log("[입구/화면] " + tag + " · " + r.What +
                      " · 문 " + (r.BannerMouth * 100f).ToString("0") + "% · 등불겹침 " +
                      (r.BannerLantern * 100f).ToString("0") + "% · 기둥겹침 " + (r.BannerPillar * 100f).ToString("0") +
                      "% · 기둥노출 " + (r.PillarShow * 100f).ToString("0") + "% · 등불이문 " +
                      (r.LanternMouth * 100f).ToString("0") + "% · 등불보임 " + (r.LanternShow * 100f).ToString("0") + "%");
        }

        /// <summary>
        /// **화면에서 실제로 보이는 문구멍**과 그 앞을 덮은 소품의 몫 — 문틀·포털을 뺀 **나머지 전부**가
        /// 후보다(배너만 세면 다른 소품이 새로 들어와도 조용하다).
        /// 광선으로 재던 자(`MouthBlockShare`)와 다투었고 화면이 이겼다: 그 자의 문틀 사각형 가장자리는
        /// 화면에서 **기둥 뒤**여서, 거기 선 배너를 「25% 가림」이라 불렀다(샷에는 가려진 것이 없었다).
        /// </summary>
        public static bool MouthScreenShare(string rootName, float ex, float ez,
                                            out float covered, out string who, out int mouthPixels)
        {
            covered = 0f; who = ""; mouthPixels = 0;
            var root = GameObject.Find(rootName);
            if (root == null) { who = "(입구 없음)"; return false; }
            ShotEye(ex, ez, out Vector3 eye, out Vector3 look);
            var portal = FindChild(root.transform, VisualSliceBuilder.EntrancePortalObject);
            var frame = FindChild(root.transform, VisualSliceBuilder.EntranceFrameObject);
            if (portal == null) { who = "(포털 없음)"; return false; }
            var mouth = VisibleMouth(portal, frame, eye, look);
            mouthPixels = mouth.Pixels;
            if (mouth.Pixels == 0) { who = "(보이는 문구멍이 없다 — 문틀이 다 가린 각이다)"; return false; }
            float worst = 0f;
            foreach (Transform child in root.transform)
            {
                if (child == frame || child == portal) continue;
                var sil = Draw(child, eye, look);
                if (sil.Pixels == 0) continue;
                float share = CoverShare(sil, mouth);
                if (share <= 0.005f) continue;
                covered += share;
                if (share > worst) { worst = share; who = child.name + " " + (share * 100f).ToString("0") + "%"; }
            }
            covered = Mathf.Clamp01(covered);
            if (who == "") who = "없음";
            return true;
        }
    }
}
