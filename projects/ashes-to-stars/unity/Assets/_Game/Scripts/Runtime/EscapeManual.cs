using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 긴급 탈출은 수동 지휘 중에만(§4 ✅). 자동 전투에서는 발동 불가.
    /// TryBegin은 두루마리만 보고 잡몹·던전 노드에서도 캐스트가 열렸다.
    /// 지휘가 열린 보스·침략만 허용. 잡몹에서 리더를 직접 움직이는 경우는
    /// W3Party라 안 넣는다. QA_NO면 옛 항상 허용.
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

        /// <summary>보스·침략은 지휘가 열린다. 잡몹 웨이브·던전 노드는 자동.</summary>
        public static bool Allowed(GameFlow.BattleKind kind)
        {
            if (Blocked) return true;
            return kind == GameFlow.BattleKind.보스 || kind == GameFlow.BattleKind.침략;
        }

        public static bool Allowed() => Allowed(GameFlow.Kind);

        public static string WhyNot()
        {
            if (Allowed()) return "";
            return "자동 전투 중에는 쓸 수 없다 — 보스전 지휘 중에만(§4)";
        }

        public static string Line() => "두루마리는 보스전 지휘 중에만(§4)";

        public static string Body() =>
            "잡몹·던전 노드는 자동. 보고 지휘할 때만 빠져나간다(§4·§5)";

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
