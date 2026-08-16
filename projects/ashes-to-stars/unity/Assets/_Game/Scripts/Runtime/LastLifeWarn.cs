using System;
using System.Collections.Generic;
using System.Text;

namespace AshesToStars
{
    /// <summary>
    /// 마지막 목숨 출전 경고에 삭제될 장착 6부위를 보여 준다(§4·§11).
    /// 가방·창고는 안 지운다. QA_NO면 옛 일반 문구만.
    /// </summary>
    public static class LastLifeWarn
    {
        public const int LastDeaths = 2;
        public const string QaName = "마지막시험";
        public const string EnvShow = "QA_LAST_LIFE_GEAR";
        public const string EnvNo = "QA_NO_LAST_LIFE_GEAR";

        static bool _qaSeeded;
        static bool _showPrompt;

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool QaPrompt => _showPrompt;
        public static void AckQaPrompt() => _showPrompt = false;

        public static bool IsLastLife(CharacterRecord ch) =>
            ch != null && !ch.IsDeleted && !ch.IsSpecialJob && ch.DeathCount == LastDeaths;

        public static bool HasAny()
        {
            var roster = LifeSystem.GetCharacters();
            for (int i = 0; i < roster.Count; i++)
                if (IsLastLife(roster[i])) return true;
            return false;
        }

        public static List<CharacterRecord> Subjects()
        {
            var list = new List<CharacterRecord>();
            foreach (var ch in PartyState.SortieRecords())
                if (IsLastLife(ch)) list.Add(ch);
            if (list.Count > 0) return list;
            var roster = LifeSystem.GetCharacters();
            for (int i = 0; i < roster.Count; i++)
                if (IsLastLife(roster[i])) list.Add(roster[i]);
            return list;
        }

        public static string Title() =>
            "[주의] 마지막 목숨 캐릭터가 파티에 있습니다";

        public static string Body()
        {
            if (Blocked)
                return "사망 시 캐릭터가 영구 삭제되며\n장착 장비도 함께 사라집니다(§4)";
            return "사망 시 캐릭터와 아래 장착 6부위가 사라진다. 가방·창고는 남는다(§4·§11)";
        }

        public static string GearLine() => GearRange(0, 3);

        /// <summary>장갑·신발·장신구. 한 줄에 6칸을 쓰면 qa_shot에서 잘린다.</summary>
        public static string GearRest() => GearRange(3, 3);

        static string GearRange(int from, int count)
        {
            if (Blocked) return from == 0 ? "장착 장비도 함께 사라집니다(§4)" : "";
            var subjects = Subjects();
            if (subjects.Count == 0)
                return from == 0 ? "장착 없음 — 가방·창고는 남는다(§11)" : "";
            var sb = new StringBuilder();
            for (int i = 0; i < subjects.Count; i++)
            {
                string part = FormatRange(subjects[i], from, count);
                if (string.IsNullOrEmpty(part)) continue;
                if (sb.Length > 0) sb.Append(" / ");
                sb.Append(part);
            }
            return sb.ToString();
        }

        public static string FormatOne(CharacterRecord ch) => FormatRange(ch, 0, Equipment.SlotCount);

        static string FormatRange(CharacterRecord ch, int from, int count)
        {
            if (ch == null) return "";
            string name = string.IsNullOrEmpty(ch.Name) ? ch.Job : ch.Name;
            var worn = Equipment.WornAll(ch);
            if (from == 0 && worn.Count == 0) return name + " · 장착 없음";
            if (from > 0 && worn.Count == 0) return "";
            var sb = new StringBuilder();
            if (from == 0) sb.Append(name);
            int end = Math.Min(Equipment.SlotCount, from + count);
            for (int i = from; i < end; i++)
            {
                var slot = (EquipSlot)i;
                var g = Equipment.Worn(ch, slot);
                if (sb.Length > 0) sb.Append(" · ");
                sb.Append(Equipment.SlotName(slot)).Append(' ');
                sb.Append(g != null ? g.Name : "빈칸");
            }
            return sb.ToString();
        }

        /// <summary>시각 QA. 마지막 목숨 + 장착 6칸 + 경고 화면.</summary>
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
            ch.DeathCount = LastDeaths;
            ch.IsDeleted = false;
            ch.RecoveryEndTime = 0;
            LifeSystem.PersistRoster();
            Equipment.SeedCraftedLoadoutForQa(ch);
            PartyState.SetSlotsForTest(0);
            _showPrompt = true;
        }

        public static void ResetForTest()
        {
            _qaSeeded = false;
            _showPrompt = false;
        }
    }
}
