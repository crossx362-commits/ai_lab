using System;
using System.Text;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 영묘 추모 기록(§4). 삭제되면 최고 층·마지막 출전·사망 원인·장착·마지막 동료·누적 출전을 남긴다.
    /// 건물은 첫 캐릭터 삭제에 열린다(§13-2). 환생해도 다시 잠기지 않는다.
    /// 전투력은 안 돌려준다. QA_NO면 옛 이름·직업만. QA_NO_MAUSOLEUM_UNLOCK면 항상 열림.
    /// </summary>
    public static class Memorial
    {
        public const string QaName = "추모시험";
        public const int QaFloor = 30;
        public const string EnvShow = "QA_MEMORIAL";
        public const string EnvNo = "QA_NO_MEMORIAL";
        public const string EnvShowUnlock = "QA_MAUSOLEUM_UNLOCK";
        public const string EnvNoUnlock = "QA_NO_MAUSOLEUM_UNLOCK";
        const string K_UNLOCKED = "ats.mausoleum.unlocked";

        static bool _qaSeeded;
        static bool _unlockQaSeeded;
        static bool _unlockLoaded;
        static bool _everDeleted;

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool UnlockBlocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNoUnlock);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>첫 삭제에 연다. QA_NO면 옛 항상 열림. 지금 삭제 명부는 옛 저장 폴백.</summary>
        public static bool Unlocked
        {
            get
            {
                if (UnlockBlocked) return true;
                LoadUnlock();
                if (_everDeleted) return true;
                if (!HasDeletedNow()) return false;
                Open();
                return true;
            }
        }

        public static string LockReason()
        {
            if (Unlocked) return null;
            return "첫 캐릭터 삭제 시 해금 — 3회 사망한 캐릭터가 여기 잠든다(§13-2)";
        }

        public static string LockLine()
        {
            string why = LockReason();
            return string.IsNullOrEmpty(why) ? "영묘 해금(§13-2)" : why;
        }

        /// <summary>삭제가 확정된 뒤에만 부른다. 환생해도 플래그는 남는다.</summary>
        public static void Open()
        {
            LoadUnlock();
            if (_everDeleted) return;
            _everDeleted = true;
            PlayerPrefs.SetInt(K_UNLOCKED, 1);
            PlayerPrefs.Save();
        }

        static void LoadUnlock()
        {
            if (_unlockLoaded) return;
            _unlockLoaded = true;
            _everDeleted = PlayerPrefs.GetInt(K_UNLOCKED, 0) == 1;
        }

        static bool HasDeletedNow()
        {
            var dead = LifeSystem.GetDeletedCharacters();
            return dead != null && dead.Count > 0;
        }

        public static bool HasRecord(CharacterRecord ch) =>
            ch != null && (ch.MemorialFloor > 0
                           || !string.IsNullOrEmpty(ch.MemorialPlace)
                           || !string.IsNullOrEmpty(ch.MemorialCause)
                           || !string.IsNullOrEmpty(ch.MemorialParty));

        /// <summary>삭제 직전. 장착을 지우기 전에 불러야 이름이 남는다.</summary>
        public static void Stamp(CharacterRecord ch)
        {
            if (ch == null || Blocked) return;
            int floor = GameState.TowerFloor;
            if (floor > ch.MemorialFloor) ch.MemorialFloor = floor;
            ch.MemorialPlace = PlaceOf(GameFlow.ReturnTo, GameFlow.Kind);
            ch.MemorialCause = CauseOf(ch, GameFlow.Kind);
            ch.MemorialGear = FormatGear(ch);
            ch.MemorialParty = FormatParty(ch);
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

        public static string PartyLine(CharacterRecord ch)
        {
            if (Blocked || ch == null) return "";
            if (!string.IsNullOrEmpty(ch.MemorialParty))
                return ch.MemorialParty + "(§4)";
            return HasRecord(ch) ? "혼자 출전(§4)" : "";
        }

        public static string TimeLine(CharacterRecord ch) => SortieTime.Line(ch);

        public static string HubLine()
        {
            if (Blocked) return "";
            return "이름·직업·최고 층·사망 원인·마지막 동료·누적 출전을 남긴다(§4)";
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

        /// <summary>죽은 본인을 뺀 출전 이름. 편성이 비면 혼자.</summary>
        public static string FormatParty(CharacterRecord ch)
        {
            if (ch == null) return "혼자 출전";
            var sortie = PartyState.SortieRecords();
            var sb = new StringBuilder();
            for (int i = 0; i < sortie.Count; i++)
            {
                var mate = sortie[i];
                if (mate == null) continue;
                if (ReferenceEquals(mate, ch)) continue;
                if (!string.IsNullOrEmpty(ch.Id) && mate.Id == ch.Id) continue;
                if (string.IsNullOrEmpty(mate.Name)) continue;
                if (sb.Length > 0) sb.Append(" · ");
                sb.Append(mate.Name);
            }
            return sb.Length == 0 ? "혼자 출전" : "동료 " + sb;
        }

        /// <summary>시각 QA. 삭제 + 30층 탑 보스전 + 장착 이름 + 동료 힐러.</summary>
        public static void SeedQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable(EnvShow) != "1") return;
            if (Blocked) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            HuntSchedule.ResetForTest();
            DefenseState.ResetForTest();
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
            if (roster.Count < 2)
                LifeSystem.AddStarterCompanion("힐");
            roster = LifeSystem.GetCharacters();
            if (roster.Count < 2)
                LifeSystem.AddBasicRecruit("힐");
            roster = LifeSystem.GetCharacters();
            if (roster.Count > 1)
            {
                roster[1].Name = "힐러";
                roster[1].IsDeleted = false;
                roster[1].RecoveryEndTime = 0;
                roster[1].DeathCount = 0;
            }
            LifeSystem.PersistRoster();
            Equipment.SeedCraftedLoadoutForQa(ch);
            PartyState.SetSlotsForTest(0, roster.Count > 1 ? 1 : 0);
            SortieTime.SeedQaIfRequested();
            GameState.SetTowerFloorForTest(QaFloor);
            GameFlow.SetReturnForTest(GameFlow.Tower, GameFlow.BattleKind.보스);
            LifeSystem.RegisterDeath(ch);
        }

        /// <summary>시각 QA. QA_MAUSOLEUM_UNLOCK=1이면 삭제 없는 잠긴 영묘를 보여 준다.</summary>
        public static void SeedUnlockQaIfRequested()
        {
            string raw = Environment.GetEnvironmentVariable(EnvShowUnlock);
            if (raw != "1" && !string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase))
                return;
            if (UnlockBlocked) return;
            if (_unlockQaSeeded) return;
            _unlockQaSeeded = true;
            PlayerPrefs.DeleteKey(K_UNLOCKED);
            _unlockLoaded = true;
            _everDeleted = false;
            var roster = LifeSystem.GetCharacters();
            for (int i = 0; i < roster.Count; i++)
            {
                if (roster[i] == null || !roster[i].IsDeleted) continue;
                roster[i].IsDeleted = false;
            }
            LifeSystem.PersistRoster();
        }

        public static void ResetForTest()
        {
            _qaSeeded = false;
            _unlockQaSeeded = false;
            SortieTime.ResetForTest();
            _unlockLoaded = false;
            _everDeleted = false;
            PlayerPrefs.DeleteKey(K_UNLOCKED);
        }

        public static void ForgetInMemoryForTest()
        {
            _unlockLoaded = false;
        }
    }
}
