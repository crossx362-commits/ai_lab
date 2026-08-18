using System;

namespace AshesToStars
{
    /// <summary>
    /// 소지품 줄. 부활초·두루마리는 n/상한, 환생석·재료는 개수만.
    /// 옛 줄은 무제한도 n/2147483647 이라 필드 지갑 카드가 잘렸다.
    /// QA_NO면 옛 상한 숫자. FieldScreen이 읽는다.
    /// </summary>
    public static class BagTextFmt
    {
        public const string EnvShow = "QA_BAG_TEXT";
        public const string EnvNo = "QA_NO_BAG_TEXT";
        public const int UnlimitedFloor = 1_000_000;

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
            int cap = Economy.ItemCapacity[it];
            if (Blocked || !Unlimited(cap))
                return $"{label} {n}/{cap}";
            return $"{label} {n}";
        }

        public static string Line() => Blocked
            ? "소지품이 상한 숫자를 붙인다"
            : "무제한 소지품은 개수만(§18-4)";

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
        }

        public static void ResetForTest()
        {
            _qaSeeded = false;
        }
    }
}
