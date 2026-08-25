using System;

namespace AshesToStars
{
    /// <summary>던전 포기 카드의 플레이어용 문구. QA_NO면 내부 절 번호가 보이던 옛 문구.</summary>
    public static class DungeonAbandonCopy
    {
        public const string EnvNo = "QA_NO_DUNGEON_ABANDON_COPY";

        public static string Text()
        {
            return Environment.GetEnvironmentVariable(EnvNo) == "1"
                ? "여기서 나간다 — 강화는 사라진다(§7)"
                : "여기서 나간다 — 임시 강화는 모두 사라진다";
        }
    }
}
