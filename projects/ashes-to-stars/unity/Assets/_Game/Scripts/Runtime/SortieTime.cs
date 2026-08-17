using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 누적 출전 시간(§4 ✅). 영묘 기록에 칸이 있는데 쓰는 곳이 없었다.
    /// 전투는 한 판의 초, 일정 사냥은 흐른 초를 출전 명부에 더한다.
    /// QA_NO면 0. 시계는 BattleScreen._t · HuntSchedule.Tick이다.
    /// </summary>
    public static class SortieTime
    {
        public const string EnvShow = "QA_SORTIE_TIME";
        public const string EnvNo = "QA_NO_SORTIE_TIME";
        public const long QaSeconds = 3600;

        static bool _qaSeeded;

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static long Seconds(CharacterRecord ch) =>
            ch == null || Blocked ? 0 : Math.Max(0, ch.SortieSeconds);

        public static void AddTo(CharacterRecord ch, long seconds)
        {
            if (Blocked || ch == null || ch.IsDeleted || seconds <= 0) return;
            ch.SortieSeconds += seconds;
        }

        public static void AddToIndexes(IReadOnlyList<int> indexes, long seconds)
        {
            if (Blocked || indexes == null || seconds <= 0) return;
            var roster = LifeSystem.GetCharacters();
            bool any = false;
            for (int i = 0; i < indexes.Count; i++)
            {
                int idx = indexes[i];
                if (idx < 0 || idx >= roster.Count) continue;
                var ch = roster[idx];
                if (ch == null || ch.IsDeleted) continue;
                ch.SortieSeconds += seconds;
                any = true;
            }
            if (any) LifeSystem.PersistRoster();
        }

        /// <summary>전투가 끝난 뒤. 출전 편성에 이번 판 초를 더한다.</summary>
        public static void Apply(float seconds)
        {
            if (Blocked || seconds < 1f) return;
            AddToIndexes(PartyState.Slots, (long)seconds);
        }

        public static string Format(long seconds)
        {
            if (seconds <= 0) return "";
            if (seconds >= 3600)
                return $"{seconds / 3600}시간 {(seconds % 3600) / 60}분";
            if (seconds >= 60)
                return $"{seconds / 60}분 {seconds % 60}초";
            return $"{seconds}초";
        }

        public static string Line(CharacterRecord ch)
        {
            if (Blocked) return "";
            long n = Seconds(ch);
            if (n <= 0) return "";
            return "출전 " + Format(n) + "(§4)";
        }

        /// <summary>시각 QA. 삭제 전에 1시간을 심어 영묘가 읽게 한다.</summary>
        public static void SeedQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable(EnvShow) != "1"
                && Environment.GetEnvironmentVariable(Memorial.EnvShow) != "1")
                return;
            if (Blocked) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            var roster = LifeSystem.GetCharacters();
            if (roster.Count == 0) return;
            var ch = roster[0];
            if (ch.SortieSeconds < QaSeconds) ch.SortieSeconds = QaSeconds;
            LifeSystem.PersistRoster();
        }

        public static void ResetForTest()
        {
            _qaSeeded = false;
        }
    }
}
