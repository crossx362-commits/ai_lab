using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 무기는 직업 계열만 착용한다(§11). 방어구·장신구는 공용 — 로스터 돌려쓰기.
    /// 옛 TryEquip은 직업을 안 봤다. QA_NO면 옛 항상 허용.
    /// 송곳니 검은 물리(탱·딜). 마딜·힐·버퍼는 거부. 다른 무기 레시피는 이 칸 아님.
    /// </summary>
    public static class EquipJob
    {
        public const string EnvShow = "QA_EQUIP_JOB";
        public const string EnvNo = "QA_NO_EQUIP_JOB";
        public const string Physical = "물리";
        public const string FangSwordId = "fang_sword";

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

        public static string Line() =>
            Blocked ? "무기는 아무나 찬다(§11)" : "무기는 직업 계열만(§11)";

        /// <summary>기본직업 5종으로 접는다. 1차·2차 이름은 그 계열.</summary>
        public static string LineOf(CharacterRecord character) =>
            character == null ? "" : LineOfJob(character.Job);

        public static string LineOfJob(string job)
        {
            if (string.IsNullOrEmpty(job)) return "";
            if (job == "탱" || job == "딜" || job == "마딜" || job == "힐" || job == "버퍼")
                return job;
            switch (job)
            {
                case "수호기사":
                case "광전사": return "탱";
                case "검사":
                case "궁수": return "딜";
                case "마법사":
                case "소환사": return "마딜";
                case "사제":
                case "드루이드": return "힐";
                case "음유시인":
                case "주술사":
                case "정령사": return "버퍼";
                default: return job;
            }
        }

        public static string RequiredOf(GearItem gear)
        {
            if (gear == null || gear.Slot != EquipSlot.Weapon) return "";
            var rec = Equipment.RecipeOf(gear.RecipeId);
            return rec == null ? "" : rec.JobLine ?? "";
        }

        public static bool Matches(string required, string have)
        {
            if (string.IsNullOrEmpty(required)) return true;
            if (required == Physical) return have == "탱" || have == "딜";
            return required == have;
        }

        public static bool CanWear(CharacterRecord character, GearItem gear)
        {
            if (Blocked) return true;
            if (character == null || gear == null) return false;
            if (gear.Slot != EquipSlot.Weapon) return true;
            return Matches(RequiredOf(gear), LineOf(character));
        }

        public static string WhyNot(CharacterRecord character, GearItem gear)
        {
            if (CanWear(character, gear)) return "";
            string need = RequiredOf(gear);
            string name = gear == null ? "무기" : gear.Name;
            if (need == Physical)
                return $"{name}은 물리 계열(탱·딜)(§11)";
            if (!string.IsNullOrEmpty(need))
                return $"{name}은 {need} 계열(§11)";
            return $"{name}은 이 직업이 못 찬다(§11)";
        }

        public static string LabelOf(string line) => line switch
        {
            "탱" => "탱커",
            "딜" => "물리딜러",
            "마딜" => "마법딜러",
            "힐" => "힐러",
            "버퍼" => "서포터",
            Physical => "물리(탱·딜)",
            _ => line ?? "",
        };

        /// <summary>시각 QA. QA_EQUIP_JOB=1이면 힐러 + 가방 송곳니 검.</summary>
        public static void SeedQaIfRequested()
        {
            if (!ShowQa) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            var roster = LifeSystem.GetCharacters();
            CharacterRecord healer = null;
            for (int i = 0; i < roster.Count; i++)
            {
                if (roster[i].IsDeleted) continue;
                if (LineOf(roster[i]) == "힐") { healer = roster[i]; break; }
            }
            if (healer == null && roster.Count > 0) healer = roster[0];
            if (healer == null) return;
            Equipment.TryUnequip(healer, EquipSlot.Weapon);
            bool have = false;
            var bag = Equipment.Unequipped();
            for (int i = 0; i < bag.Count; i++)
            {
                if (bag[i].RecipeId == FangSwordId) { have = true; break; }
            }
            if (!have) Equipment.AddUnequippedForTest(FangSwordId);
        }

        public static int QaHealerIndex()
        {
            var roster = LifeSystem.GetCharacters();
            for (int i = 0; i < roster.Count; i++)
            {
                if (roster[i].IsDeleted) continue;
                if (LineOf(roster[i]) == "힐") return i;
            }
            return roster.Count > 0 ? 0 : -1;
        }

        public static void ResetForTest()
        {
            _qaSeeded = false;
        }
    }
}
