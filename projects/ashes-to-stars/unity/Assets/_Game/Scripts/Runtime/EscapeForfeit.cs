using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 긴급 탈출 보상 포기(§3·§4 ✅). 두루마리는 EmergencyEscape가 소모한다.
    /// 결과는 사망 없이 생존이지만 그 판의 골드·경험·드랍은 없다.
    /// BattleScreen이 Escaped에서 읽고 Result가 줄을 보여 준다.
    /// QA_NO면 옛 Estate 직행(줄 없음).
    /// </summary>
    public static class EscapeForfeit
    {
        public const string EnvShow = "QA_ESCAPE_FORFEIT";
        public const string EnvNo = "QA_NO_ESCAPE_FORFEIT";

        static bool _active;
        static bool _qaSeeded;

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool Active => !Blocked && _active;

        public static string Line()
        {
            if (!Active) return "";
            return "긴급 탈출 — 이번 판 보상 포기(§4)";
        }

        public static string Body() =>
            "목숨은 그대로. 전리품·경험치는 없다(§3·§4)";

        /// <summary>
        /// 정산 없이 런을 끝낸다. 골드 Earn·경험·목숨은 안 건드린다.
        /// 던전은 End. 침략 대기는 패배 추가 소모 없이 취소.
        /// </summary>
        public static void Apply(BattleRewardInfo reward)
        {
            if (Blocked)
            {
                _active = false;
                return;
            }
            if (reward != null) reward.Clear();
            if (DungeonRun.Active) DungeonRun.End();
            InvasionState.AbortPending();
            GameFlow.LastBattleSummary = LineForApply();
            GameFlow.LastDefeatReport = null;
            _active = true;
        }

        static string LineForApply() => "긴급 탈출 — 이번 판 보상 포기(§4)";

        /// <summary>시각 QA. QA_ESCAPE_FORFEIT=1이면 결과 화면에 포기 줄을 심는다.</summary>
        public static void SeedQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable(EnvShow) != "1") return;
            if (Blocked) return;
            if (_qaSeeded && Active) return;
            _qaSeeded = true;
            TowerEnding.ResetForTest();
            SoloRaidClear.ResetForTest();
            FloorRecruit.ResetForTest();
            var reward = BattleScreen._GetLastReward();
            Apply(reward);
        }

        public static void ResetForTest()
        {
            _active = false;
            _qaSeeded = false;
        }
    }
}
