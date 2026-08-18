using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 탑 대보스 마릿수(§10-7·§18-11). 최대 3체, 확률 1=60 / 2=30 / 3=10.
    /// 옛 길은 BattleScreen이 탑을 항상 1로 넘겨 던전만 60/30/10이었다.
    /// 던전은 Plan.BossCount. 필드 배회는 💡라 1. QA_NO면 옛 1.
    /// W3Party는 안 읽는다.
    /// </summary>
    public static class BossCount
    {
        public const string EnvShow = "QA_BOSS_COUNT";
        public const string EnvNo = "QA_NO_BOSS_COUNT";
        public const int Cap = 3;
        public const int QaForce = 2;
        public const float OneUntil = 0.60f;
        public const float TwoUntil = 0.90f;

        /// <summary>SelfCheck가 마릿수를 고정할 때만. 0이면 굴린다.</summary>
        public static int Force;

        static int _fight = 1;
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

        /// <summary>이번 판. Begin이 쓰기 전에는 1.</summary>
        public static int Fight => _fight < 1 ? 1 : _fight;

        /// <summary>탑 대보스(10·20·…·100)만. 던전·배회·중간 레이드는 아니다.</summary>
        public static bool Applies(int floor)
        {
            if (Blocked) return false;
            if (DungeonRun.Active) return false;
            if (FieldBoss.Fighting) return false;
            return RaidCost.IsMega(floor);
        }

        /// <summary>0~1 굴림 → 1/2/3. 던전 생성기와 같은 문턱.</summary>
        public static int FromRoll(float roll)
        {
            if (roll < OneUntil) return 1;
            if (roll < TwoUntil) return 2;
            return Cap;
        }

        public static int Roll(uint seed)
        {
            if (Blocked) return 1;
            var rng = Rng.Stream(seed, 1, SeedChannel.Boss);
            return FromRoll(rng.Value01());
        }

        /// <summary>그 층의 마릿수. 대보스가 아니면 1. Force&gt;0이면 그 값.</summary>
        public static int Of(int floor)
        {
            if (!Applies(floor)) return 1;
            if (Force > 0) return Mathf.Clamp(Force, 1, Cap);
            return Roll((uint)floor);
        }

        /// <summary>전투 시작. Begin이 읽은 값을 Fight·드랍이 같이 본다.</summary>
        public static int Begin(int floor)
        {
            _fight = Of(floor);
            return _fight;
        }

        public static string FormatLine(int floor)
        {
            if (!RaidCost.IsMega(floor)) return "";
            if (Blocked) return "대보스는 1체(옛)";
            int n = Of(floor);
            return n <= 1 ? "대보스 1체(§10-7)" : $"대보스 {n}체(§10-7)";
        }

        public static string Line() => FormatLine(RaidCost.CurrentRaidFloor());

        public static void SeedQaIfRequested()
        {
            if (!ShowQa) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            Force = QaForce;
            GameState.SetTowerFloorForTest(10);
        }

        public static void ResetForTest()
        {
            Force = 0;
            _fight = 1;
            _qaSeeded = false;
        }
    }
}
