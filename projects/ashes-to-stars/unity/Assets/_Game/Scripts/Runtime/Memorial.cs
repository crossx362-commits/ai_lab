using System;
using System.Text;

namespace AshesToStars
{
    /// <summary>
    /// 영묘 추모 기록(§4). 삭제되면 최고 층·마지막 출전·사망 원인·장착 이름을 남긴다.
    /// 전투력은 안 돌려준다. QA_NO면 옛 이름·직업만.
    /// </summary>
    public static class Memorial
    {
        public const string QaName = "추모시험";
        public const int QaFloor = 30;
        public const string EnvShow = "QA_MEMORIAL";
        public const string EnvNo = "QA_NO_MEMORIAL";

        static bool _qaSeeded;

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool HasRecord(CharacterRecord ch) =>
            ch != null && (ch.MemorialFloor > 0
                           || !string.IsNullOrEmpty(ch.MemorialPlace)
                           || !string.IsNullOrEmpty(ch.MemorialCause));

        /// <summary>삭제 직전. 장착을 지우기 전에 불러야 이름이 남는다.</summary>
        public static void Stamp(CharacterRecord ch)
        {
            if (ch == null || Blocked) return;
            int floor = GameState.TowerFloor;
            if (floor > ch.MemorialFloor) ch.MemorialFloor = floor;
            ch.MemorialPlace = PlaceOf(GameFlow.ReturnTo, GameFlow.Kind);
            ch.MemorialCause = CauseOf(ch, GameFlow.Kind);
            ch.MemorialGear = FormatGear(ch);
        }

        public static void NoteRebirth(CharacterRecord ch)
        {
            if (ch == null || Blocked) return;
            ch.MemorialRebirths++;
        }

        public static string PlaceOf(string returnTo, GameFlow.BattleKind kind)
        {
            if (kind == GameFlow.BattleKind.던전) return "던전";
            if (kind == GameFlow.BattleKind.침략) return "월드맵";
            if (returnTo == GameFlow.Field) return "필드";
            if (returnTo == GameFlow.Tower) return "탑";
            if (returnTo == GameFlow.Dungeon) return "던전";
            if (returnTo == GameFlow.WorldMap) return "월드맵";
            return "영지";
        }

        public static string CauseOf(CharacterRecord ch, GameFlow.BattleKind kind)
        {
            if (ch != null && ch.IsSpecialJob) return "특수 직업 1회 사망";
            if (kind == GameFlow.BattleKind.보스) return "보스전 전멸";
            if (kind == GameFlow.BattleKind.던전) return "던전 전멸";
            if (kind == GameFlow.BattleKind.침략) return "침략 전멸";
            if (GameFlow.ReturnTo == GameFlow.Tower) return "탑 전멸";
            if (GameFlow.ReturnTo == GameFlow.Field) return "필드 전멸";
            return "PvE 전멸";
        }

        public static string Line(CharacterRecord ch)
        {
            if (ch == null) return "";
            if (Blocked || !HasRecord(ch)) return "기록 없음";
            var sb = new StringBuilder();
            if (ch.MemorialFloor > 0) sb.Append(ch.MemorialFloor).Append("층");
            if (!string.IsNullOrEmpty(ch.MemorialPlace))
            {
                if (sb.Length > 0) sb.Append(" · ");
                sb.Append(ch.MemorialPlace);
            }
            if (!string.IsNullOrEmpty(ch.MemorialCause))
            {
                if (sb.Length > 0) sb.Append(" · ");
                sb.Append(ch.MemorialCause);
            }
            sb.Append("(§4)");
            return sb.ToString();
        }

        public static string GearLine(CharacterRecord ch)
        {
            if (Blocked) return "";
            if (ch == null || string.IsNullOrEmpty(ch.MemorialGear)) return "장착 없음";
            return ch.MemorialGear;
        }

        public static string RebirthLine(CharacterRecord ch)
        {
            if (Blocked || ch == null || ch.MemorialRebirths <= 0) return "";
            return "환생 " + ch.MemorialRebirths + "회";
        }

        public static string HubLine()
        {
            if (Blocked) return "";
            return "이름·직업·최고 층·사망 원인을 남긴다(§4)";
        }

        public static string ResultLine(CharacterRecord ch)
        {
            if (ch == null) return "";
            string line = Line(ch);
            if (line == "기록 없음") return "";
            return ch.Name + " · " + line;
        }

        public static string FormatGear(CharacterRecord ch)
        {
            if (ch == null) return "장착 없음";
            var worn = Equipment.WornAll(ch);
            if (worn.Count == 0) return "장착 없음";
            var sb = new StringBuilder();
            for (int i = 0; i < Equipment.SlotCount; i++)
            {
                var g = Equipment.Worn(ch, (EquipSlot)i);
                if (g == null) continue;
                if (sb.Length > 0) sb.Append(" · ");
                sb.Append(Equipment.SlotName((EquipSlot)i)).Append(' ').Append(g.Name);
            }
            return sb.Length == 0 ? "장착 없음" : sb.ToString();
        }

        /// <summary>시각 QA. 삭제 + 30층 탑 보스전 + 장착 이름.</summary>
        public static void SeedQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable(EnvShow) != "1") return;
            if (Blocked) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            var roster = LifeSystem.GetCharacters();
            if (roster.Count == 0) return;
            var ch = roster[0];
            ch.Name = QaName;
            ch.Job = "수호기사";
            ch.Advancement = AdvancementTier.First;
            ch.Level = 50;
            ch.DeathCount = 2;
            ch.IsDeleted = false;
            ch.IsSpecialJob = false;
            ch.RecoveryEndTime = 0;
            LifeSystem.PersistRoster();
            Equipment.SeedCraftedLoadoutForQa(ch);
            GameState.SetTowerFloorForTest(QaFloor);
            GameFlow.SetReturnForTest(GameFlow.Tower, GameFlow.BattleKind.보스);
            LifeSystem.RegisterDeath(ch);
        }

        public static void ResetForTest()
        {
            _qaSeeded = false;
        }
    }
}
