using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 환생 시 레벨 1·경험 0(§4·§3). 직업·전직 단계는 그대로다.
    /// 스킬 1개 선택은 이 슬라이스에 안 넣는다. QA_NO면 레벨을 안 내린다.
    /// </summary>
    public static class Rebirth
    {
        public const int StartLevel = 1;
        public const int QaFromLevel = 50;
        public const long QaFromExp = 12_345;
        public const string EnvShow = "QA_REBORN_LV1";
        public const string EnvNo = "QA_NO_REBORN_LV1";

        static bool _qaSeeded;
        static int _fromLevel;
        static string _name = "";

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static int FromLevel => _fromLevel;
        public static string LastName => _name ?? "";

        /// <summary>레벨만 1로. 직업·전직·목숨은 호출부가 처리한다.</summary>
        public static bool Apply(CharacterRecord ch)
        {
            if (ch == null) return false;
            _fromLevel = ch.Level;
            _name = ch.Name ?? "";
            if (Blocked) return false;
            ch.Level = StartLevel;
            ch.Exp = 0;
            return true;
        }

        public static string Line()
        {
            if (Blocked) return "환생 레벨 유지";
            return "환생하면 Lv1부터 재육성(§4)";
        }

        public static string DoneLine()
        {
            if (Blocked) return "환생 레벨 유지";
            if (string.IsNullOrEmpty(_name)) return Line();
            return $"{_name} · Lv{_fromLevel}→Lv1(§4)";
        }

        public static string MausoleumSubtitle()
        {
            if (Blocked)
                return "환생석으로 삭제된 캐릭터를 되돌린다. 장비는 함께 돌아오지 않는다(§4)";
            return "환생하면 Lv1부터 재육성(§4) · 장비는 돌아오지 않는다";
        }

        public static string RowDesc(CharacterRecord ch, int stones)
        {
            if (ch == null) return "";
            if (stones <= 0) return "환생석이 없다 — 10층 보스가 떨어뜨린다";
            if (Blocked) return "환생석 1개를 써서 되돌린다 — 사망 0에서 다시 시작한다";
            return $"환생석 1개 · 지금 Lv{ch.Level} → Lv1(§4)";
        }

        /// <summary>시각 QA. 1=영묘에 Lv50 삭제, 2=환생 직후 캐릭터 Lv1.</summary>
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
            ch.Level = QaFromLevel;
            ch.Exp = QaFromExp;
            ch.IsDeleted = true;
            ch.DeathCount = 3;
            ch.RecoveryEndTime = 0;
            ch.ClearEquipped();
            if (GameState.Bag.GetCount(Economy.LifeItem.RebornStone) < 1)
                GameState.Gain(Economy.LifeItem.RebornStone, 1);
            if (raw == "2")
                LifeSystem.UseRebornStone(ch);
        }

        public static void ResetForTest()
        {
            _qaSeeded = false;
            _fromLevel = 0;
            _name = "";
        }
    }
}
