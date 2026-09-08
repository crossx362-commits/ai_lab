using UnityEngine;

namespace Ulon.Shared
{
    /// <summary>
    /// **사냥터 잡몹 자리 원장**(검수 랩 ④). 전에는 에디터(`VisualSliceBuilder.HuntSpots`)에만 있어서
    /// 도포(`WorldSplat`)가 **몹이 어디 서는지 모른 채** 얼룩을 깔았다 — 그래서 「몹은 잔디에, 흙은
    /// 그 옆에」가 됐다. 자리를 여기로 옮겨 **놓는 쪽·깔는 쪽·재는 쪽이 한 원장을 읽는다.**
    ///
    /// 좌표는 마을과 같은 **모듈 격자**다(랩 B) — 마을이 킷 배율만큼 넓어지면 사냥터도 같이 물러난다.
    /// </summary>
    public static class HuntRoster
    {
        /// <summary>
        /// **무리로 선다**(검수 지시 2026-09-09 — 「여덟이 고르게 흩어져 섰다, 둘셋씩 뭉치는 것이 무리다」).
        /// 셋·셋·둘로 묶고, 무리 안은 4m 안팎(월드 하한 3.6m 위), 무리끼리는 12~14m 띄운다.
        /// 무리 안에서도 **보는 눈 기준으로 좌우를 벌려** 한 덩이로 안 읽히게 한다 — 겹침은
        /// `SliceSelfCheck.HuntSpotGapReason`이 화면 쪽에서 판정한다.
        /// </summary>
        public static readonly (string Name, float X, float Z, float Yaw)[] Spots =
        {
            // 무리 1 — 서쪽
            // 무리 안이라도 **보는 눈에서 앞뒤로 겹치면 안 된다** — 처음 잡은 자리는 Skeleton과
            // Bandit이 시선 방향으로 정확히 늘어서 방위각 차가 0.0°였다(게이트가 잡았다).
            ("Skeleton", -4.29f, 34.29f, 168f),
            ("Bandit", -2.48f, 32.48f, 196f),
            ("Rogue", -1.62f, 34.00f, 208f),
            // 무리 2 — 가운데
            ("Raider", 2.14f, 35.00f, 152f),
            ("Knight", 3.43f, 33.71f, 174f),
            ("Acolyte", 4.86f, 35.00f, 160f),
            // 무리 3 — 동쪽 둘
            ("Minion", 8.33f, 33.10f, 186f),
            ("SkelRogue", 9.81f, 31.90f, 150f),
        };

        /// <summary>자리의 월드 좌표(y는 부르는 쪽이 지표에서 유도한다).</summary>
        public static Vector2 World(int i) =>
            new Vector2(Spots[i].X * WorldScale.Kit, Spots[i].Z * WorldScale.Kit);
    }
}
