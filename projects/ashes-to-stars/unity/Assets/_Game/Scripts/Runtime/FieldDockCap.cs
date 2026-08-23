using System;

namespace AshesToStars
{
    /// <summary>
    /// 필드 도크 사냥 시작·일정·저체력·사망없음·배회 보스·레이드급·던전 입장 부제. 옛 줄은 한 칸에 두 줄로 잘렸다.
    /// QA_NO면 옛 긴 줄. FieldScreen이 읽는다.
    /// </summary>
    public static class FieldDockCap
    {
        public const string EnvShow = "QA_FIELD_DOCK";
        public const string EnvBoss = "QA_FIELD_BOSS_CAP";
        public const string EnvRaid = "QA_FIELD_RAID_CAP";
        public const string EnvDungeon = "QA_FIELD_DUNGEON_CAP";
        public const string EnvHunt = "QA_FIELD_HUNT_CAP";
        public const string EnvNo = "QA_NO_FIELD_DOCK";
        /// <summary>필드 도크 한 칸. 「잠김 — 」을 붙여도 한 줄.</summary>
        public const int CaptionMaxRunes = 18;
        public const string RaidShort = "5인 · 환생석 없음";
        public const string DungeonShort = "랜덤 · 종점 보스";
        public const string HuntShort = "잡몹 자동 · 보스 수동";
        public const string OldHunt = "잡몹은 자동, 보스는 수동 지휘(§5)";

        public const string OldLowHp = "HP 30%면 3초 뒤 영지. 이번 판 보상 없음(§4·§6)";
        public const string OldSchedule = "편성을 보내 두면 영지에서도 돈다. 사망 없음 · 상한 12시간(§6)";
        public const string OldDeath = "일정 사냥은 카운트를 안 올린다. 상한 12시간(§6)";
        public const string BossTail = " · 환생석 없음";
        public const string BossPrefix = "배회하는 ";

        static bool _qaSeeded;
        static bool _bossSeeded;
        static bool _raidSeeded;
        static bool _dungeonSeeded;
        static bool _huntSeeded;

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

        public static bool ShowRaidQa
        {
            get
            {
                if (Blocked) return false;
                string raw = Environment.GetEnvironmentVariable(EnvRaid);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool ShowDungeonQa
        {
            get
            {
                if (Blocked) return false;
                string raw = Environment.GetEnvironmentVariable(EnvDungeon);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool ShowHuntQa
        {
            get
            {
                if (Blocked) return false;
                string raw = Environment.GetEnvironmentVariable(EnvHunt);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static string Line() => Blocked
            ? "부제가 두 줄이다"
            : "일정·저체력 부제는 한 줄이다(§16)";

        public static string BossLine() => Blocked
            ? "부제가 두 줄이다"
            : "배회 보스 부제는 한 줄이다(§16)";

        public static string RaidLine() => Blocked
            ? "부제가 두 줄이다"
            : "레이드급 부제는 한 줄이다(§16)";

        public static string DungeonLine() => Blocked
            ? "부제가 두 줄이다"
            : "던전 입장 부제는 한 줄이다(§16)";

        public static string HuntLine() => Blocked
            ? "부제가 두 줄이다"
            : "사냥 시작 부제는 한 줄이다(§16)";

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

        /// <summary>옛 줄은 인원·비용·드랍금지를 이어 붙여 슬림 칸에서 잘렸다.</summary>
        public static string OldRaid()
        {
            long cost = Economy.GetActionCost("RaidDungeon", GameState.Tier);
            return $"5인 전제 · {Economy.FormatCurrency(cost)} · 환생석·증표 없음(§10-8)";
        }

        public static string Raid() => Blocked ? OldRaid() : RaidShort;

        /// <summary>옛 줄은 생성·종점·비용을 이어 붙여 슬림 칸에서 잘렸다.</summary>
        public static string OldDungeon()
        {
            long cost = Economy.GetActionCost("DungeonEntry", GameState.Tier);
            return $"랜덤 생성 + 종점 보스 · {Economy.FormatCurrency(cost)}(§7)";
        }

        public static string Dungeon() => Blocked ? OldDungeon() : DungeonShort;

        /// <summary>옛 줄은 잡몹·보스 지휘를 이어 붙여 슬림 칸에서 잘렸다.</summary>
        public static string OldHuntStatus() => OldHunt;

        public static string Hunt() => Blocked ? OldHunt : HuntShort;

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

        /// <summary>시각 QA. 레이드급을 띄워 짧은 부제를 보여 준다.</summary>
        public static void SeedRaidQaIfRequested()
        {
            if (!ShowRaidQa) return;
            if (_raidSeeded) return;
            _raidSeeded = true;
            GameState.TrySelectTier(0);
            RaidSpawn.ForceSpawnForTest(1);
        }

        /// <summary>시각 QA. 던전 입장 칸의 짧은 부제를 보여 준다.</summary>
        public static void SeedDungeonQaIfRequested()
        {
            if (!ShowDungeonQa) return;
            if (_dungeonSeeded) return;
            _dungeonSeeded = true;
            GameState.TrySelectTier(0);
            if (RaidSpawn.Active) RaidSpawn.Consume();
            if (FieldBoss.Active) FieldBoss.Consume();
        }

        /// <summary>시각 QA. 사냥 시작 칸의 짧은 부제를 보여 준다.</summary>
        public static void SeedHuntQaIfRequested()
        {
            if (!ShowHuntQa) return;
            if (_huntSeeded) return;
            _huntSeeded = true;
            GameState.TrySelectTier(0);
            if (RaidSpawn.Active) RaidSpawn.Consume();
            if (FieldBoss.Active) FieldBoss.Consume();
        }

        public static void ResetForTest()
        {
            _qaSeeded = false;
            _bossSeeded = false;
            _raidSeeded = false;
            _dungeonSeeded = false;
            _huntSeeded = false;
        }
    }
}
