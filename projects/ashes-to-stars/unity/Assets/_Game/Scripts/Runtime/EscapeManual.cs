using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 긴급 탈출은 플레이어가 수동으로 누를 때만(§4 ✅) 시작한다.
    /// 전투 화면의 입력은 보스·잡몹 웨이브 모두 같은 캐스트 경로를 쓴다.
    /// QA_NO면 옛 항상 허용.
    /// </summary>
    public static class EscapeManual
    {
        public const string EnvShow = "QA_ESCAPE_MANUAL";
        public const string EnvNo = "QA_NO_ESCAPE_MANUAL";

        static bool _qaSeeded;

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool ShowQa
        {
            get
            {
                if (Blocked) return false;
                string raw = Environment.GetEnvironmentVariable(EnvShow);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>전투 화면에서 수동 입력으로 시작하므로 모든 전투 종류를 허용한다.</summary>
        public static bool Allowed(GameFlow.BattleKind kind)
        {
            if (Blocked) return true;
            return true;
        }

        public static bool Allowed() => Allowed(GameFlow.Kind);

        public static string WhyNot()
        {
            if (Allowed()) return "";
            return "긴급 탈출을 사용할 수 없다(§4)";
        }

        public static string Line() => "두루마리는 수동 입력으로 즉시 캐스팅한다(§4)";

        public static string Body() =>
            "보스·잡몹 전투 중 수동으로 즉시 탈출을 시도한다(§4·§5)";

        /// <summary>시각 QA. QA_ESCAPE_MANUAL=1이면 필드 자막·두루마리.</summary>
        public static void SeedQaIfRequested()
        {
            if (!ShowQa) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            GameState.Gain(Economy.LifeItem.ScrollOfReturn, 1);
        }

        public static void ResetForTest()
        {
            _qaSeeded = false;
        }
    }
}
