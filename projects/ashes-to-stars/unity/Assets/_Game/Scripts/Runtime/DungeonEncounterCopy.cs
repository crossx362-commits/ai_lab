using System;

namespace AshesToStars
{
    /// <summary>일반 던전 전투 카드의 플레이어용 설명. QA_NO면 구현 용어가 섞인 옛 문구.</summary>
    public static class DungeonEncounterCopy
    {
        public const string EnvNo = "QA_NO_DUNGEON_ENCOUNTER_COPY";

        public static bool Blocked => Environment.GetEnvironmentVariable(EnvNo) == "1";

        public static string Text(DungeonNode node)
        {
            int count = node?.Wave?.TargetCount ?? 0;
            float ranged = node?.Wave?.RangedPercent ?? 0f;
            if (Blocked)
                return $"동시 {count}체 · 원거리 {ranged:F0}% · {OldTemplate(node)}";

            return $"최대 {count}마리 · 원거리 적 {ranged:F0}% · {PlayerTemplate(node)}";
        }

        static string OldTemplate(DungeonNode node)
        {
            switch (node?.Template ?? ArenaTemplate.open_ring)
            {
                case ArenaTemplate.pillars:    return "기둥";
                case ArenaTemplate.choke:      return "병목";
                case ArenaTemplate.pockets:    return "엄폐";
                case ArenaTemplate.arena_wide: return "넓은 무대";
                default:                       return "열린 고리";
            }
        }

        static string PlayerTemplate(DungeonNode node)
        {
            switch (node?.Template ?? ArenaTemplate.open_ring)
            {
                case ArenaTemplate.pillars:    return "기둥 지형";
                case ArenaTemplate.choke:      return "좁은 길";
                case ArenaTemplate.pockets:    return "엄폐 지형";
                case ArenaTemplate.arena_wide: return "넓은 전장";
                default:                       return "원형 전장";
            }
        }
    }
}
