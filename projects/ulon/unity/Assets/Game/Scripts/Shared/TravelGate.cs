namespace Ulon.Shared
{
    public static class TravelGate
    {
        public const string Object = "Moongate";
        public const string DisplayName = "문게이트";
        // 모듈 좌표 × 킷 배율(랩 B). **옛 값 (12.8, -22.8)은 배율을 타고 (26.9, -47.9)로 밀려나
        // 나중에 생긴 농경지(WorldRegions.Meadow, 중심 (48,-36)·반경 26) 작물 이랑 위에 섰다**
        // — 검수가 「밭 한가운데 소품 무더기」로 잡은 분홍 기둥이 이 게이트였다(2026-09-09).
        // 마을 동쪽 길가로 되돌린다: 지역 셋 어느 반경에도 안 들어가고 마을 안이다.
        public const float X = 13.5f * WorldScale.Kit;   // 마구간(모듈 8.8,-8.4)에서 15.0m — 간격 하한 12.6m
        public const float Z = -3.0f * WorldScale.Kit;
        public const float PlazaX = 0f;
        public const float PlazaZ = 0f;
        public const float InteractRange = 2.6f;
        public const int GoldCost = 5;
    }

    public static class TravelMark
    {
        public const int GoldCost = 5;
        public const float CombatSeconds = 8f;
    }
}
