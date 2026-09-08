namespace Ulon.Shared
{
    /// <summary>
    /// **킷 배율의 단일 원장**(랩 B, 2026-09-09).
    ///
    /// Kenney FantasyTown 킷은 **1m 모듈**로 만들어졌고 사람은 1.8m다 — 그대로 세우면 사람이 집보다
    /// 커서 문으로 못 들어간다. 배율은 눈대중이 아니라 **잰 값에서 유도했다**:
    /// 문 개구부 실측 **0.74m**(임시 콜라이더로 벽을 수직으로 훑어 잰 뚫린 구간),
    /// 사람이 머리를 안 부딪는 최소는 `사람 키 1.8 × 0.85 = 1.53m`이므로 `1.53 / 0.74 = 2.07` → **2.1**.
    /// 게이트 `SliceSelfCheck.AssertDoorFitsPerson`이 이 비를 매 판 다시 잰다.
    ///
    /// **여기 있는 이유**: 마을 시설 좌표(`HousingPlot`·`StableYard`·`TravelGate`·`TameResolve`)는
    /// 런타임 코드도 읽는다. 배율을 에디터에만 두면 마을은 커지는데 런타임이 아는 자리는 안 커져
    /// 서로 갈린다 — **재는 자와 맞추는 자는 하나여야 한다.**
    ///
    /// 대상이 아닌 것: 지형(실 미터), KayKit 던전 소품(`RoomPropObject`가 목표 크기를 강제),
    /// 사냥터 자리(`HuntSpots`, 마을 밖 들판), 배우 크기(`SpawnActor`가 키로 맞춘다).
    /// </summary>
    public static class WorldScale
    {
        public const float Kit = 2.1f;
    }
}
