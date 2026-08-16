using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 하위 레이드 보스 랜덤 출현(§9 ✅).
    /// 이미 깬 레이드 층(5·10·…·직전) 풀에서 하나를 고른다.
    /// 첫 클리어·던전은 입장 층 고정. QA_NO면 항상 입장 층.
    /// 골드·경험은 입장 층 RaidScale, 고유 드랍·페이즈는 출현 층.
    /// </summary>
    public static class RaidBossPool
    {
        public const string EnvShow = "QA_RAID_BOSS_POOL";
        public const string EnvNo = "QA_NO_RAID_BOSS_POOL";
        public const int QaPickedFloor = 30;

        /// <summary>SelfCheck가 출현 층을 고정할 때만. 0이면 시드로 뽑는다.</summary>
        public static int ForcePickedFloor;

        /// <summary>SelfCheck가 추첨을 고정할 때만. 0이면 0번(가장 낮은 층).</summary>
        public static uint ForceSeed;

        static bool _qaSeeded;
        static int _picked;
        static int _entry;

        static readonly string[] Roster =
        {
            "문지기 골렘", "재의 군주", "사슬에 묶인 야수", "강철 파수꾼",
            "흑요석 마녀", "심연의 눈", "서리심장", "폭풍의 정령왕",
            "백골 군단장", "죽음의 문지기", "타락한 성전사", "빛을 잃은 성좌",
            "태엽 심판자", "시간의 태엽", "용의 알 수호자", "잿빛 용",
            "별의 파편", "별의 사도", "탑의 그림자", "탑의 주인",
        };

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static int PickedFloor => _picked;
        public static int EntryFloor => _entry;

        /// <summary>전투·드랍이 읽는 출현 층. 안 뽑혔으면 입장 층.</summary>
        public static int FightFloor => _picked > 0 ? _picked : (_entry > 0 ? _entry : GameFlow.BossFloor);

        /// <summary>이미 깬 탑 레이드만. 스케일 차단과 무관하다.</summary>
        public static bool Applies(int floor) =>
            !Blocked && !FieldBoss.Fighting && RaidScale.IsRaidFloor(floor)
            && floor < GameState.TowerFloor
            && !(DungeonRun.Active && GameFlow.ReturnTo == GameFlow.Dungeon);

        /// <summary>이미 깬 레이드 층. TowerFloor 51이면 5…50, 10종.</summary>
        public static int[] ClearedFloors()
        {
            int top = GameState.TowerFloor;
            int n = 0;
            for (int f = 5; f < top && f <= 100; f += 5) n++;
            var list = new int[n];
            int i = 0;
            for (int f = 5; f < top && f <= 100; f += 5) list[i++] = f;
            return list;
        }

        public static int PoolCount => ClearedFloors().Length;

        public static string Name(int floor)
        {
            if (!RaidScale.IsRaidFloor(floor)) return floor + "층 보스";
            int i = floor / 5 - 1;
            if (i < 0 || i >= Roster.Length) return floor + "층 보스";
            return Roster[i];
        }

        public static string Name() => Name(FightFloor);

        public static Economy.DropSource DropSourceFor(int floor) =>
            floor > 0 && floor % 10 == 0
                ? Economy.DropSource.Tower10Boss
                : Economy.DropSource.Tower5Boss;

        /// <summary>하위면 풀에서 뽑고, 첫 클리어·차단이면 입장 층.</summary>
        public static int Pick(int entryFloor)
        {
            _entry = entryFloor;
            if (!Applies(entryFloor) || Blocked)
            {
                _picked = entryFloor;
                return _picked;
            }
            if (ForcePickedFloor > 0)
            {
                _picked = ForcePickedFloor;
                return _picked;
            }
            int[] pool = ClearedFloors();
            if (pool.Length == 0)
            {
                _picked = entryFloor;
                return _picked;
            }
            uint seed = ForceSeed;
            _picked = pool[(int)(seed % (uint)pool.Length)];
            return _picked;
        }

        public static string Line()
        {
            if (Blocked) return "하위 레이드 보스 고정";
            int n = PoolCount;
            if (n <= 0) return "";
            return $"하위 레이드 보스 {n}종(§9)";
        }

        public static string PickedLine()
        {
            if (_picked <= 0) return "";
            if (Blocked) return "하위 레이드 보스 고정 · " + Name(_picked);
            return $"출현 {Name(_picked)}({_picked}층)";
        }

        public static string BattleTitle()
        {
            if (_picked <= 0) return $"보스전 · {GameFlow.BossFloor}층";
            if (Applies(_entry > 0 ? _entry : GameFlow.BossFloor))
                return $"하위 레이드 · {Name(_picked)}({_picked}층)";
            return $"보스전 · {Name(_picked)}({_picked}층)";
        }

        public static string BattleHint()
        {
            if (_picked <= 0) return $"{GameFlow.BossFloor}층 보스";
            return $"{Name(_picked)}({_picked}층)";
        }

        /// <summary>시각 QA. 51층·T5·출현 심연의 눈(30층). 풀은 10종.</summary>
        public static void SeedQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable(EnvShow) != "1") return;
            if (Blocked) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            GameState.SetTowerFloorForTest(51);
            GameState.TrySelectTier(4);
            ForcePickedFloor = QaPickedFloor;
            Pick(RaidScale.LowerRaidFloor);
        }

        public static void ResetForTest()
        {
            ForcePickedFloor = 0;
            ForceSeed = 0;
            _qaSeeded = false;
            _picked = 0;
            _entry = 0;
        }
    }
}
