using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 허브 카드 글씨가 칸 안에 **실제로 들어가는지** 잰다(§16).
    ///
    /// 왜 칸 좌표만으로는 모자란가: 지금까지의 카드 점검은 `CardLayout`이 돌려준 title·sub
    /// 사각형이 카드 밖으로 안 나가는지만 봤다. 그런데 잘림은 사각형이 아니라 **글꼴 줄 높이**
    /// 때문에 난다 — 20px 글꼴의 실제 줄 높이는 ~26px이라 22px 칸에 넣으면 받침이 잘리는데
    /// 사각형은 멀쩡히 카드 안에 있다. `LabelClip`이 TextClipping.Clip이라 조용히 잘린다.
    /// 그래서 여기서는 GUIStyle.CalcHeight로 **글꼴이 요구하는 높이**를 재서 칸과 대조한다.
    ///
    /// 네거티브 컨트롤: `UiPages.SlimTitleH`를 22로 되돌리면 필드 도크 제목 줄이 FAIL이어야
    /// 한다. FAIL이 안 나면 이 점검이 아무것도 안 재고 있는 것이다.
    ///
    /// ⚠️ **이 점검이 못 재는 것 — 한글 줄바꿈.** builtin LegacyRuntime.ttf에는 한글 글리프가
    /// 없어 **폭**이 런타임(OS 폴백 렌더)과 다르다. 줄 높이(제목 잘림)는 정확하지만
    /// **부제가 몇 줄로 접히는지는 실제보다 적게 나온다** — 실측으로 확인했다: 여기서 2줄
    /// 38.0px로 통과한 문구가 화면에서는 3줄로 접혀 잘려 있었다. 부제는 이 점검을 믿지 말고
    /// 반드시 스크린샷으로 볼 것(`GAME_PROJ=…/unity_meas ./tools/qa_shot.sh "go:Field" 240`).
    /// 런타임 <see cref="UiPages.LabelFit"/>는 진짜 글꼴로 재므로 화면에서는 올바르게 줄어든다.
    /// </summary>
    public static class CardTextFitSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        /// <summary>
        /// ⚠️ `GUI.skin`은 OnGUI 밖에서 못 읽는다(배치 모드에서 ArgumentException). 그런데
        /// `GUI.skin.label`이 쓰는 글꼴이 바로 이 builtin `LegacyRuntime.ttf`라, 직접 물려주면
        /// **OnGUI 없이도 런타임과 같은 줄 높이**가 나온다(실측: font=null 기본값과 완전 동일,
        /// AppleGothic을 물리면 24.31로 달라진다 — 글꼴을 명시해야 하는 이유).
        /// </summary>
        static Font Base() => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        static GUIStyle TitleStyle(bool slim) => new GUIStyle
        {
            font = Base(),
            fontSize = slim ? UiPages.SlimTitleFont : UiPages.CardTitleFont,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
        };

        static GUIStyle SubStyle(bool slim) => new GUIStyle
        {
            font = Base(),
            fontSize = slim ? UiPages.SlimSubFont : UiPages.CardSubFont,
            wordWrap = true,
        };

        /// <summary>카드 하나에 제목·부제를 넣어 보고 글꼴이 칸을 넘는지 잰다.</summary>
        static void Fits(Rect card, string title, string sub, string what)
        {
            bool slim = UiPages.IsSlimCard(card);
            UiPages.CardLayout(card, true, out var icon, out var titleR, out var subR);

            var ts = TitleStyle(slim);
            var ss = SubStyle(slim);

            // LabelFit이 고르는 글꼴로 판정한다. minFont까지 줄여도 안 들어가면 그건 진짜 잘림이다.
            int fT = UiPages.FittedFont(titleR, title, ts);
            int fS = UiPages.FittedFont(subR, sub, ss);
            var tFit = new GUIStyle(ts) { fontSize = fT };
            var sFit = new GUIStyle(ss) { fontSize = fS };
            float needT = tFit.CalcHeight(new GUIContent(title), Mathf.Max(4f, titleR.width));
            float needS = sFit.CalcHeight(new GUIContent(sub), Mathf.Max(4f, subR.width));

            Check(needT <= titleR.height + 0.5f,
                $"{what} 제목 «{title}» {ts.fontSize}→{fT}px 필요 {needT:0.0} ≤ 칸 {titleR.height:0.0}");
            Check(needS <= subR.height + 0.5f,
                $"{what} 부제 «{sub}» {ss.fontSize}→{fS}px 필요 {needS:0.0} ≤ 칸 {subR.height:0.0}");
            // 글꼴을 얼마나 줄였는지도 본다 — 바닥(11px)까지 내려갔다면 문구가 너무 길다는 신호다.
            Check(fS > UiPages.LabelMinFont,
                $"{what} 부제가 최소 글꼴({UiPages.LabelMinFont})까지 안 내려간다 (실제 {fS})");
            Check(titleR.yMin >= card.yMin - 0.5f && subR.yMax <= card.yMax + 0.5f,
                $"{what} 글씨 칸이 카드 안 ({titleR.yMin:0}~{subR.yMax:0} ⊂ {card.yMin:0}~{card.yMax:0})");
            Check(icon.width < 1f || icon.xMax <= titleR.xMin + 0.5f,
                $"{what} 아이콘이 제목을 안 밀친다 (아이콘 {icon.xMax:0} ≤ 제목 {titleR.xMin:0})");
        }

        [MenuItem("Ashes to Stars/QA/Card Text Fit Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;

            string no = Environment.GetEnvironmentVariable(FieldHud.EnvNo);
            string dockNo = Environment.GetEnvironmentVariable(FieldDockCap.EnvNo);
            Environment.SetEnvironmentVariable(FieldHud.EnvNo, null);
            Environment.SetEnvironmentVariable(FieldDockCap.EnvNo, null);
            FieldHud.ResetForTest();

            // 실제 본문 칸 — GameScreen.Body(BodyPadX 36, BodyTop 56, NavReserve 80).
            var body = new Rect(36f, 56f, 1280f - 72f, 720f - 56f - 80f);
            var dock = FieldHud.Cards(body);
            Check(dock.Length == 6, $"필드 도크 6칸 (실제 {dock.Length})");
            Check(UiPages.IsSlimCard(dock[0]),
                $"도크 칸 {dock[0].width:0}×{dock[0].height:0} 은 슬림");

            // FieldScreen이 실제로 넣는 가장 긴 문구들. WORKLOG가 지목한 잘림 두 건이 여기 있다.
            // ⚠️ 잠긴 카드는 DrawCard가 런타임에 「잠김 — 」을 앞에 붙인다. 그걸 빼고 재면
            //    점검은 통과하는데 화면은 잘린다 — 실제로 처음에 그렇게 놓쳤다(오너 스크린샷).
            Fits(dock[0], "사냥 시작", "잡몹은 자동, 보스는 수동 지휘(§5)", "도크[0]");
            Fits(dock[1], "던전 입장", "랜덤 생성 + 종점 보스 · 1실버 99쿠퍼(§7)", "도크[1]");
            Fits(dock[2], "9150골드 7실버 50쿠퍼",
                "잠김 — 부활초 3/3 · 환생석 3", "도크[2]");
            HuntSchedule.ResetForTest();
            FieldDockCap.ResetForTest();
            Fits(dock[3], "저체력 귀환 켜짐", FieldDockCap.LowHp(), "도크[3]");
            Fits(dock[4], "일정 사냥", FieldDockCap.Schedule(), "도크[4]");
            Fits(dock[5], "사망 없음",
                "잠김 — " + FieldDockCap.Death(), "도크[5]");

            string towerNo = Environment.GetEnvironmentVariable(TowerDockCap.EnvNo);
            Environment.SetEnvironmentVariable(TowerDockCap.EnvNo, null);
            GameState.ResetAll();
            RaidReroll.ResetForTest();
            RaidScale.ResetForTest();
            RaidBossPool.ResetForTest();
            TowerDockCap.ResetForTest();
            RaidScale.ForceScalePercent = 65;
            GameState.SetTowerFloorForTest(51);
            GameState.TrySelectTier(4);
            RaidReroll.Record(RaidScale.LowerRaidFloor);
            var tower = TowerHud.Cards(body);
            Check(tower.Length == 4, $"탑 도크 4칸 (실제 {tower.Length})");
            Fits(tower[1], "레이드 (5층 단위)", TowerDockCap.Raid(50), "탑도크[1]");
            Fits(tower[2], "하위 레이드 5층", TowerDockCap.Lower(5), "탑도크[2]");
            RaidScale.ForceScalePercent = -1;
            RaidReroll.ResetForTest();
            RaidScale.ResetForTest();
            RaidBossPool.ResetForTest();
            TowerDockCap.ResetForTest();
            GameState.ResetAll();
            Environment.SetEnvironmentVariable(TowerDockCap.EnvNo, towerNo);

            string worldNo = Environment.GetEnvironmentVariable(WorldMapDockCap.EnvNo);
            Environment.SetEnvironmentVariable(WorldMapDockCap.EnvNo, null);
            InvasionApproach.ResetForTest();
            InvasionState.ResetForTest();
            WorldMapDockCap.ResetForTest();
            GameState.SetTowerFloorForTest(WorldMapScreen.InvasionUnlockFloor);
            var world = WorldMapHud.Cards(body);
            Check(world.Length == 4, $"월드맵 도크 4칸 (실제 {world.Length})");
            Fits(world[0], "성계 이동",
                "잠김 — " + WorldMapDockCap.Star(), "월드맵도크[0]");
            Fits(world[1], "침략", WorldMapDockCap.Caption(), "월드맵도크[1]");
            Fits(world[2], "랭킹",
                "잠김 — " + WorldMapDockCap.Rank(), "월드맵도크[2]");
            GameState.SetTowerFloorForTest(1);
            Fits(world[1], "침략",
                "잠김 — " + WorldMapDockCap.Caption(), "월드맵도크잠김");
            WorldMapDockCap.ResetForTest();
            InvasionApproach.ResetForTest();
            InvasionState.ResetForTest();
            GameState.ResetAll();
            Environment.SetEnvironmentVariable(WorldMapDockCap.EnvNo, worldNo);

            // 슬림이 아닌 큰 카드도 같은 규칙을 지켜야 한다(직업 선택·보너스 카드 계열).
            var wide = new Rect(0f, 0f, 328f, 152f);
            Check(!UiPages.IsSlimCard(wide), $"보너스 카드 {wide.width:0}×{wide.height:0} 는 슬림 아님");
            Fits(wide, "예리함", "공격력이 오른다. 나가면 사라진다(§7)", "보너스");

            Environment.SetEnvironmentVariable(FieldHud.EnvNo, no);
            Environment.SetEnvironmentVariable(FieldDockCap.EnvNo, dockNo);
            FieldHud.ResetForTest();
            FieldDockCap.ResetForTest();

            if (_fail == 0) Debug.Log("[CardTextFitSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[CardTextFitSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[CardTextFitSelfCheck] FAIL {_fail}건");
        }
    }
}
