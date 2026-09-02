using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 성계 이동·랭킹·동맹 로컬 목업(§14·§15). 서버 없이 밝힌 별과 논다.
    /// QA_NO_LOCAL_NET이면 옛 잠김 카드.
    /// </summary>
    public static class LocalNet
    {
        public const string EnvNo = "QA_NO_LOCAL_NET";
        public const int AllyCap = 5;
        public const int RankRows = 6;

        const string K_ALLY = "ats.local.ally";
        const string K_VISIT = "ats.local.visit";

        static bool _loaded;
        static readonly List<string> _allies = new List<string>();
        static string _lastVisit = "";

        public static readonly string[] ExtraRivals = { "잿빛 별", "새벽 별" };

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static int AllyCount { get { Load(); return _allies.Count; } }
        public static string LastVisit { get { Load(); return _lastVisit; } }

        public static IReadOnlyList<string> Allies
        {
            get { Load(); return _allies; }
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _allies.Clear();
            string raw = PlayerPrefs.GetString(K_ALLY, "");
            if (!string.IsNullOrEmpty(raw))
                foreach (var part in raw.Split(','))
                    if (!string.IsNullOrEmpty(part) && !_allies.Contains(part))
                        _allies.Add(part);
            _lastVisit = PlayerPrefs.GetString(K_VISIT, "");
        }

        static void Save()
        {
            PlayerPrefs.SetString(K_ALLY, string.Join(",", _allies));
            PlayerPrefs.SetString(K_VISIT, _lastVisit ?? "");
            PlayerPrefs.Save();
        }

        public static bool IsAlly(string name)
        {
            Load();
            return !string.IsNullOrEmpty(name) && _allies.Contains(name);
        }

        public static string WhyCannotAlly(string name)
        {
            Load();
            if (string.IsNullOrEmpty(name)) return "대상이 없다";
            if (IsAlly(name)) return "이미 동맹이다";
            if (_allies.Count >= AllyCap) return $"동맹 상한 {AllyCap}명";
            return null;
        }

        public static bool TryAlly(string name)
        {
            if (WhyCannotAlly(name) != null) return false;
            _allies.Add(name);
            Save();
            return true;
        }

        public static bool TryUnally(string name)
        {
            Load();
            if (!IsAlly(name)) return false;
            _allies.Remove(name);
            Save();
            return true;
        }

        public static bool CanInvade(string name) => !IsAlly(name);

        public static void MarkVisit(string name)
        {
            Load();
            _lastVisit = name ?? "";
            Save();
        }

        public enum Board { Floor, Honor, Guard }

        public struct RankRow
        {
            public string Name;
            public int Score;
            public bool Mine;
        }

        public static RankRow[] BoardRows(Board board)
        {
            var names = new List<string>();
            var stars = WorldExplore.Neighbors();
            for (int i = 0; i < stars.Length; i++) names.Add(stars[i].Name);
            for (int i = 0; i < ExtraRivals.Length; i++) names.Add(ExtraRivals[i]);

            var rows = new RankRow[names.Count + 1];
            rows[0] = new RankRow
            {
                Name = "나",
                Score = MyScore(board),
                Mine = true,
            };
            for (int i = 0; i < names.Count; i++)
            {
                rows[i + 1] = new RankRow
                {
                    Name = names[i],
                    Score = RivalScore(names[i], board),
                    Mine = false,
                };
            }
            Array.Sort(rows, (a, b) =>
            {
                int c = b.Score.CompareTo(a.Score);
                if (c != 0) return c;
                return string.CompareOrdinal(a.Name, b.Name);
            });
            if (rows.Length > RankRows)
            {
                var cut = new RankRow[RankRows];
                Array.Copy(rows, cut, RankRows);
                return cut;
            }
            return rows;
        }

        public static int MyScore(Board board) => board switch
        {
            Board.Floor => GameState.TowerFloor,
            Board.Honor => Honor.Points,
            _ => Honor.GuardWins,
        };

        public static int RivalScore(string name, Board board)
        {
            uint h = 2166136261u;
            string key = name + "|" + (int)board;
            for (int i = 0; i < key.Length; i++) { h ^= key[i]; h *= 16777619u; }
            int my = MyScore(board);
            int spread = board == Board.Floor ? 18 : board == Board.Honor ? 40 : 4;
            int delta = (int)(h % (uint)(spread * 2 + 1)) - spread;
            int score = my + delta;
            if (score < 0) score = 0;
            if (board == Board.Floor && score > WorldStar.MaxFloor) score = WorldStar.MaxFloor;
            return score;
        }

        public static int MyPlace(Board board)
        {
            var rows = BoardRows(board);
            for (int i = 0; i < rows.Length; i++)
                if (rows[i].Mine) return i + 1;
            return rows.Length;
        }

        public static string RankCaption() =>
            Blocked ? WorldMapDockCap.RankCap : $"내 순위 {MyPlace(Board.Floor)}위";

        public static void ResetForTest()
        {
            PlayerPrefs.DeleteKey(K_ALLY);
            PlayerPrefs.DeleteKey(K_VISIT);
            PlayerPrefs.Save();
            _allies.Clear();
            _lastVisit = "";
            _loaded = false;
        }
    }
}
