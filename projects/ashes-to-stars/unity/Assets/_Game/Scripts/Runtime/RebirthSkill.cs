using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 환생 때 생전 스킬 중 1개만 가져간다(§4). 직업 스킬은 Job+단계 고정이라
    /// 선택 슬롯이 없어 오펀으로 막혀 있었다. 표시 전용 — W3Party 전투 스킬은
    /// 안 건드린다. QA_NO면 옛 전체 스킬 줄(KeptSkill 무시).
    /// </summary>
    public static class RebirthSkill
    {
        public const string EnvShow = "QA_REBORN_SKILL";
        public const string EnvNo = "QA_NO_REBORN_SKILL";
        public const string QaKeep = "도발의 함성";

        static bool _qaSeeded;
        static bool _seedPick;

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool SeedPick => _seedPick;

        public static void ConsumeSeedPick() { _seedPick = false; }

        public static string Pack(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            return raw.Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');
        }

        /// <summary>직업 에셋의 이름 있는 스킬(초필 포함). 에셋이 없으면 빈 배열.</summary>
        public static string[] NamesOf(string job)
        {
            var d = JobInfo.For(job);
            if (d == null || d.스킬 == null || d.스킬.Length == 0)
                return Array.Empty<string>();
            var list = new List<string>();
            foreach (var s in d.스킬)
            {
                if (s == null || string.IsNullOrEmpty(s.이름)) continue;
                if (list.Contains(s.이름)) continue;
                list.Add(s.이름);
            }
            return list.ToArray();
        }

        public static bool NeedsPick(CharacterRecord ch)
        {
            if (Blocked || ch == null || !ch.IsDeleted || ch.IsSpecialJob) return false;
            return NamesOf(ch.Job).Length >= 2;
        }

        public static bool Apply(CharacterRecord ch, string skillName)
        {
            if (ch == null) return false;
            if (Blocked)
            {
                ch.KeptSkill = "";
                return false;
            }
            string name = Pack(skillName);
            if (string.IsNullOrEmpty(name)) return false;
            var names = NamesOf(ch.Job);
            bool ok = false;
            for (int i = 0; i < names.Length; i++)
                if (names[i] == name) { ok = true; break; }
            if (!ok) return false;
            ch.KeptSkill = name;
            return true;
        }

        public static string Line(CharacterRecord ch)
        {
            if (Blocked || ch == null || string.IsNullOrEmpty(ch.KeptSkill)) return "";
            return $"계승 스킬 — {ch.KeptSkill} (나머지 소실 §4)";
        }

        public static string SkillLine(CharacterRecord ch)
        {
            if (ch == null) return "";
            if (Blocked || string.IsNullOrEmpty(ch.KeptSkill))
                return JobInfo.SkillLine(ch.Job);
            return "보유 스킬 — " + ch.KeptSkill + " (계승 1개 §4)";
        }

        public static string SkillDescLine(CharacterRecord ch)
        {
            if (ch == null) return "";
            if (Blocked || string.IsNullOrEmpty(ch.KeptSkill))
                return JobInfo.SkillDescLine(ch.Job);
            if (JobInfo.SkillDescBlocked) return "";
            var d = JobInfo.For(ch.Job);
            if (d == null || d.스킬 == null) return "";
            foreach (var s in d.스킬)
            {
                if (s == null || s.이름 != ch.KeptSkill) continue;
                if (string.IsNullOrEmpty(s.설명)) return "";
                return "스킬 설명 — " + s.이름 + ": " + s.설명;
            }
            return "";
        }

        public static string SkillUltLine(CharacterRecord ch)
        {
            if (ch == null) return "";
            if (Blocked || string.IsNullOrEmpty(ch.KeptSkill))
                return JobInfo.SkillUltLine(ch.Job);
            return "";
        }

        public static string MausoleumSubtitle()
        {
            if (Blocked) return Rebirth.MausoleumSubtitle();
            return "환생하면 Lv1 · 스킬 1개만 가져간다(§4)";
        }

        public static string PickTitle(CharacterRecord ch)
        {
            if (ch == null) return "가져갈 스킬 1개를 고른다(§4)";
            return $"{ch.Name} · 가져갈 스킬 1개(§4)";
        }

        /// <summary>시각 QA. 1=영묘 선택, 2=계승 직후 캐릭터.</summary>
        public static void SeedQaIfRequested()
        {
            string raw = Environment.GetEnvironmentVariable(EnvShow);
            if (raw != "1" && raw != "2") return;
            if (Blocked) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            var roster = LifeSystem.GetCharacters();
            if (roster.Count == 0) return;
            var ch = roster[0];
            ch.Name = "환생시험";
            ch.Job = "수호기사";
            ch.Advancement = AdvancementTier.First;
            ch.Level = Rebirth.QaFromLevel;
            ch.Exp = Rebirth.QaFromExp;
            ch.IsDeleted = true;
            ch.DeathCount = 3;
            ch.RecoveryEndTime = 0;
            ch.KeptSkill = "";
            ch.ClearEquipped();
            if (GameState.Bag.GetCount(Economy.LifeItem.RebornStone) < 1)
                GameState.Gain(Economy.LifeItem.RebornStone, 1);
            Memorial.Open();
            if (raw == "2")
            {
                Apply(ch, QaKeep);
                LifeSystem.UseRebornStone(ch);
            }
            else _seedPick = true;
        }

        public static void ResetForTest()
        {
            _qaSeeded = false;
            _seedPick = false;
        }
    }
}
