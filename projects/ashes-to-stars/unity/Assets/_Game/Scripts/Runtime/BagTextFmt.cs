using System;

namespace AshesToStars
{
    /// <summary>
    /// 소지품 줄. 부활초·두루마리는 n/상한, 환생석·재료는 개수만.
    /// 도크 부제는 Caption — 목숨 2종만. 옛 줄은 BagText 전부라 두 줄로 잘렸다.
    /// QA_NO면 옛 상한 숫자·긴 부제. FieldScreen이 읽는다.
    /// </summary>
    public static class BagTextFmt
    {
        public const string EnvShow = "QA_BAG_TEXT";
        public const string EnvNo = "QA_NO_BAG_TEXT";
        public const int UnlimitedFloor = 1_000_000;
        /// <summary>필드 도크 한 칸. 「잠김 — 」을 붙여도 한 줄.</summary>
        public const int CaptionMaxRunes = 18;

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

        public static bool Unlimited(int cap) => cap >= UnlimitedFloor;

        public static string Format(Economy.LifeItem it, int n)
        {
            if (n <= 0) return "";
            string label = GameState.Label(it);
            int cap = Economy.Capacity(it);
            if (Blocked || !Unlimited(cap))
                return $"{label} {n}/{cap}";
            return $"{label} {n}";
        }

        public static string Line() => Blocked
            ? "소지품이 상한 숫자를 붙인다"
            : "지갑 부제는 한 줄이다(§16)";

        public static int RuneCount(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int n = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (!char.IsLowSurrogate(text[i])) n++;
            }
            return n;
        }

        public static bool CaptionFits(string text) =>
            RuneCount(text) <= CaptionMaxRunes;

        /// <summary>
        /// 필드 지갑 도크용 짧은 부제. 제목이 골드이므로 목숨 2종만.
        /// 옛 줄은 BagText 전부 + 「필드 사냥은 무료」라 잠김 접두와 함께 잘렸다.
        /// </summary>
        public static string Caption()
        {
            if (Blocked)
                return GameState.BagText() + " · 필드 사냥은 무료";
            int tea = GameState.Bag.GetCount(Economy.LifeItem.RevivalTea);
            int stone = GameState.Bag.GetCount(Economy.LifeItem.RebornStone);
            int kinds = 0;
            foreach (Economy.LifeItem it in Enum.GetValues(typeof(Economy.LifeItem)))
            {
                if (GameState.Bag.GetCount(it) > 0) kinds++;
            }
            string teaS = Format(Economy.LifeItem.RevivalTea, tea);
            string stoneS = Format(Economy.LifeItem.RebornStone, stone);
            if (teaS.Length > 0 && stoneS.Length > 0) return teaS + " · " + stoneS;
            if (teaS.Length > 0) return teaS + " · 사냥 무료";
            if (stoneS.Length > 0) return stoneS + " · 사냥 무료";
            if (kinds <= 0) return "소지품 없음 · 사냥 무료";
            return $"소지품 {kinds}종 · 사냥 무료";
        }

        /// <summary>시각 QA. QA_BAG_TEXT=1이면 환생석 1 + 부활초 1.</summary>
        public static void SeedQaIfRequested()
        {
            if (!ShowQa) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            if (GameState.Bag.GetCount(Economy.LifeItem.RebornStone) <= 0)
                GameState.Gain(Economy.LifeItem.RebornStone, 1);
            if (GameState.Bag.GetCount(Economy.LifeItem.RevivalTea) <= 0)
                GameState.Gain(Economy.LifeItem.RevivalTea, 1);
            // 레이드가 2번 칸을 먹으면 지갑 부제를 못 본다.
            if (RaidSpawn.Active) RaidSpawn.Consume();
        }

        public static void ResetForTest()
        {
            _qaSeeded = false;
        }
    }
}
