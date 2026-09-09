namespace Ulon.Shared
{
    public static class Dungeon3
    {
        public const string Id = "dungeon3";
        public const string RootObject = "Dungeon3";
        public const string EntranceObject = "Dungeon3Entrance";
        public const string ExitObject = "Dungeon3Exit";
        public const string InteriorObject = "Dungeon3Interior";
        public const string MobObject = "DungeonRaider";
        public const string BossObject = "IronTyrant";
        // 남서. 가드존(반경 16) 밖이고 던전1 입구에서 15.9f, 던전2에서 33f,
        // 하우징 부지·문게이트·마구간 등 랜드마크에서 전부 6f 이상 떨어져 있다.
        public const float EntranceX = -14.0f * WorldScale.Kit;
        public const float EntranceZ = -18.0f * WorldScale.Kit;
        /// <summary>입구가 **어느 쪽을 보고 서 있나**(진입로 방위) — 문틀·등불·입구 앞 길이
        /// 전부 이 값을 읽는다. 예전엔 빌더 호출부에만 숫자로 있어 지표를 칠하는 쪽이 방향을
        /// 다시 짐작해야 했다(같은 값이 두 곳에 살면 어긋난다).</summary>
        public const float EntranceYaw = 45.0f;
        // 던전1이 (+80,+80), 던전2가 (-80,+80)을 쓴다. 남은 사분면.
        public const float InteriorX = 68f;
        public const float InteriorZ = -68f;
        public const float ExitX = 65.4f;
        public const float ExitZ = -65.6f;
        // **몹은 출구 워프 자리에서 비켜 선다**(검수 랩 2026-09-08 실측: 세 던전 모두 출구에서
        // 0.82m — 같은 수치가 복사돼 있었다). 나가는 사람이 몹 몸 안에서 튀어나오면 그건 배치 결함이다.
        // 자리는 **들어오는 착지 자리(Interior)와 나가는 게이트(Exit) 양쪽에서 2.5m** 떨어진 점이다
        // (출구에서만 물러나면 이번엔 들어오는 사람 위에 선다 — 자리는 둘 다 봐야 정해진다).
        public const float MobX = 65.5f;
        public const float MobZ = -68.1f;
        public const float BossX = 70.4f;
        public const float BossZ = -70.6f;
        public const string SignObject = "Dungeon3Signpost";
        public const float SignX = -7.2f;
        public const float SignZ = -12.4f;
        public const float RoomHalf = 8f;
        public const float RoomHeight = 3.2f;
        public const float LeaveX = -12.5f;
        public const float LeaveZ = -18.0f;
    }
}
