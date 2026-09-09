namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// **씬을 규칙으로 수렴시키는 패스들**(랩 ㉪ 2/N, `Run`에서 갈라 나왔다).
        /// 담는 것: 「씬이 원장과 다르면 고쳐 놓는」 멱등 패스의 **순서**.
        /// 안 담는 것: 판정(자는 `SliceSelfCheck.Gates.cs`), 패스의 내용(빌더 partial들).
        ///
        /// **순서가 곧 뜻이다** — 지형이 먼저고(던전 방이 거기 구멍을 뚫는다), 크기가 드레싱보다 앞이다
        /// (왕관·무기는 정수리·손에 맞춰 붙으므로 뒤에서 몸을 키우면 부착이 어긋난다). 줄을 옮기지 마라.
        /// </summary>
        static void RunBuild()
        {
            VisualSliceBuilder.EnsureVillageTerrain();   // 지형 먼저 — 던전 방이 여기에 구멍을 뚫는다
            VisualSliceBuilder.EnsureNoNestedActors();   // **자리를 정하는 패스들보다 먼저** 사본을 푼다 — 이름으로 찾는 코드가 껍데기를 집기 전에
            VisualSliceBuilder.EnsureMobArtQualified();    // 맨몸 모델 몹은 지우고 다시 짓는다(검수 자격 규칙)
            VisualSliceBuilder.EnsureMobModelLedger();     // 기대 모델과 다른 몹도 헐고 다시(도적=Rogue, 검수 승인)
            VisualSliceBuilder.EnsureHuntMobs();
            VisualSliceBuilder.EnsureHuntMobPlacement();
            // 크기를 **드레싱보다 먼저** 맞춘다 — 왕관·무기는 정수리·손 위치에 맞춰 붙는데,
            // 뒤에서 몸 크기를 바꾸면 그 부착이 통째로 어긋난다(실제로 섀도우캡틴 관이 1.00m 떴다).
            VisualSliceBuilder.EnsureMobSizes();           // 원장 키로(랩 ⑥ — 재는 자와 맞추는 자가 갈려 있었다)
            VisualSliceBuilder.EnsureMobDressing();
            VisualSliceBuilder.EnsureBossDressing();
            VisualSliceBuilder.EnsureEntranceClearance();
            VisualSliceBuilder.EnsureWorldRegions();
            VisualSliceBuilder.EnsureHouseRoofs();   // 커밋된 씬의 민가 지붕도 규칙으로 수렴시킨다(판때기 수리)
            VisualSliceBuilder.EnsureFishSpot();
            VisualSliceBuilder.EnsureNoRolelessWatermill();  // 역할 원장에 없는 장식 물레방아는 지운다(검수 판정)
            VisualSliceBuilder.EnsureCampfire();
            VisualSliceBuilder.EnsureMortar();
            VisualSliceBuilder.EnsureLockedCrate();
            VisualSliceBuilder.EnsureHousingPlot();
            VisualSliceBuilder.EnsureHouseVendor();
            VisualSliceBuilder.EnsureTameCritter();
            VisualSliceBuilder.EnsureTameBoar();
            VisualSliceBuilder.EnsureMoongate();
            VisualSliceBuilder.EnsureStable();
            VisualSliceBuilder.EnsureEastField();
            VisualSliceBuilder.EnsureSouthField();
            VisualSliceBuilder.EnsureNorthField();
            VisualSliceBuilder.EnsureDungeon1();
            VisualSliceBuilder.EnsureDungeon2();
            VisualSliceBuilder.EnsureDungeon3();
            VisualSliceBuilder.EnsureNoEntranceBanner();   // 입구 배너는 걷었다(매달 수 없어서 — Entrance.cs 기록)
            VisualSliceBuilder.EnsureNoEntrancePathTiles();  // 입구 돌길 판때기도 걷었다(길은 지형 도포)
            VisualSliceBuilder.EnsureFieldBoss();
            VisualSliceBuilder.EnsureFootOnGround();       // 지형이 올라가면 배치물도 따라 올린다(검수 A)
        }
    }
}
