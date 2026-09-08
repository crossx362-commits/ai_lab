namespace Ulon.Shared
{
    public static class FieldBoss
    {
        public const string Object = "Hexarch";
        // 마을 격자 × 킷 배율(랩 B) — 마을이 커지면 필드 보스도 같은 배로 물러나야
        // 가드존 밖에 남는다.
        public const float X = 22.6f * WorldScale.Kit;
        public const float Z = 8.4f * WorldScale.Kit;
    }
}
