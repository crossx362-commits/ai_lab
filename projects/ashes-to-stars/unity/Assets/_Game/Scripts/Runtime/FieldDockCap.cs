using System;

namespace AshesToStars
{
    /// <summary>
    /// 필드 도크 일정·저체력·사망없음·배회 보스 부제. 옛 줄은 한 칸에 두 줄로 잘렸다.
    /// QA_NO면 옛 긴 줄. FieldScreen이 읽는다.
    /// </summary>
    public static class FieldDockCap
    {
        public const string EnvShow = "QA_FIELD_DOCK";
        public const string EnvBoss = "QA_FIELD_BOSS_CAP";
        public const string EnvNo = "QA_NO_FIELD_DOCK";
        /// <summary>필드 도크 한 칸. 「잠김 — 」을 붙여도 한 줄.</summary>
        public const int CaptionMaxRunes = 18;

        public const string OldLowHp = "HP 30%면 3초 뒤 영지. 이번 판 보상 없음(§4·§6)";
        public const string OldSchedule = "편성을 보내 두면 영지에서도 돈다. 사망 없음 · 상한 12시간(§6)";
        public const string OldDeath = "일정 사냥은 카운트를 안 올린다. 상한 12시간(§6)";
        public const string BossTail = " · 환생석 없음";
        public const string BossPrefix = "배회하는 ";

        static bool _qaSeeded;
        static bool _bossSeeded;

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

        public static bool ShowBossQa
        {
            get
            {
                if (Blocked) return false;
                string raw = Environment.GetEnvironmentVariable(EnvBoss);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static string Line() => Blocked
            ? "부제가 두 줄이다"
            : "일정·저체력 부제는 한 줄이다(§16)";

        public static string BossLine() => Blocked
            ? "부제가 두 줄이다"
            : "배회 보스 부제는 한 줄이다(§16)";

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

        public static string LowHp() => Blocked ? OldLowHp : "30%면 3초 이탈";

        public static string Schedule()
        {
            if (Blocked) return HuntSchedule.Running
                ? HuntSchedule.CardBody()
                : OldSchedule;
            if (HuntSchedule.Running)
                return HuntSchedule.Count + "명 · 정산 · 사망 없음";
            return "허브에서도 돈다 · 12h";
        }

        public static string Death() => Blocked ? OldDeath : "카운트 없음 · 12h";

        /// <summary>옛 CardBody는 이름+위험+환생석을 이어 붙여 슬림 칸에서 잘렸다.</summary>
        public static string OldBoss() => FieldBoss.CardBody();

        public static string ShortBossName()
        {
            string n = FieldBoss.Name();
            if (!string.IsNullOrEmpty(n) && n.StartsWith(BossPrefix, StringComparison.Ordinal))
                return n.Substring(BossPrefix.Length);
            return string.IsNullOrEmpty(n) ? "재의 야수" : n;
        }

        public static string Boss() => Blocked
            ? OldBoss()
            : ShortBossName() + BossTail;

        /// <summary>시각 QA. 레이드·배회 보스를 걷어 일정·저체력 칸을 연다.</summary>
        public static void SeedQaIfRequested()
        {
            if (!ShowQa) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            if (RaidSpawn.Active) RaidSpawn.Consume();
            if (FieldBoss.Active) FieldBoss.Consume();
            if (HuntSchedule.Running) HuntSchedule.Stop();
        }

        /// <summary>시각 QA. 배회 보스를 띄워 짧은 부제를 보여 준다.</summary>
        public static void SeedBossQaIfRequested()
        {
            if (!ShowBossQa) return;
            if (_bossSeeded) return;
            _bossSeeded = true;
            if (RaidSpawn.Active) RaidSpawn.Consume();
            GameState.TrySelectTier(0);
            FieldBoss.ForceSpawnForTest(0);
        }

        public static void ResetForTest()
        {
            _qaSeeded = false;
            _bossSeeded = false;
        }
    }
}
