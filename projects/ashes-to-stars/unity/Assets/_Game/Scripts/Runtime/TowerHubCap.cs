using System;

namespace AshesToStars
{
    /// <summary>
    /// 탑 허브 제목판 부제. 옛 길은 스킬·마릿수·HP·비용·재입장·풀·스케일을
    /// 이어 붙여 한 줄 자막이 잘렸다(플레이모드 샷 polish_r70/tower.png).
    /// QA_NO면 그 옛 긴 줄. TowerScreen이 읽는다.
    /// </summary>
    public static class TowerHubCap
    {
        public const string EnvShow = "QA_TOWER_HUB_CAP";
        public const string EnvNo = "QA_NO_TOWER_HUB_CAP";
        /// <summary>제목판 슬림 부제(14px)가 1280폭에서 한 줄로 읽히는 상한.</summary>
        public const int CaptionMaxRunes = 80;

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

        public static string Line() => Blocked
            ? "부제가 잘린다"
            : "탑 부제는 한 줄이다(§16)";

        public static int RuneCount(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int n = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (!char.IsLowSurrogate(text[i])) n++;
            }
            return n;
        }

        public static bool CaptionFits(string text) =>
            RuneCount(text) <= CaptionMaxRunes;

        /// <summary>
        /// 기본은 훈련 안내+짧은 rest만. 막히면 옛처럼 스펙 줄을 앞에 붙인다.
        /// </summary>
        public static string Compose(
            string train, string scale, string pool, string reroll, string mega,
            string curve, string countMul, string count, string skills, string rest)
        {
            if (Blocked)
                return OldJoin(train, scale, pool, reroll, mega, curve, countMul, count, skills, rest);
            return string.IsNullOrEmpty(train) ? rest : train + " · " + rest;
        }

        /// <summary>옛 소비처 — 스펙 Line을 rest 앞에 전부 이었다.</summary>
        public static string OldJoin(
            string train, string scale, string pool, string reroll, string mega,
            string curve, string countMul, string count, string skills, string rest)
        {
            if (!string.IsNullOrEmpty(scale)) rest = scale + " · " + rest;
            if (!string.IsNullOrEmpty(pool)) rest = pool + " · " + rest;
            if (!string.IsNullOrEmpty(reroll)) rest = reroll + " · " + rest;
            if (!string.IsNullOrEmpty(mega)) rest = mega + " · " + rest;
            if (!string.IsNullOrEmpty(curve)) rest = curve + " · " + rest;
            if (!string.IsNullOrEmpty(countMul)) rest = countMul + " · " + rest;
            if (!string.IsNullOrEmpty(count)) rest = count + " · " + rest;
            if (!string.IsNullOrEmpty(skills)) rest = skills + " · " + rest;
            return string.IsNullOrEmpty(train) ? rest : train + " · " + rest;
        }
    }
}
