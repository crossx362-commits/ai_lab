using UnityEngine;

namespace Ulon.Shared
{
    /// <summary>
    /// 사냥터 8종·보스·조련과 별도인 **추가 잡몹 자리 원장**(loop#11, 기획 §6 몬스터 20종 내외).
    /// 사냥터 `HuntRoster`는 검수 간격·무리 게이트에 묶여 있어 여기로 6종을 더하지 않는다.
    /// 좌표는 마을과 같은 모듈 격자. 가드존(16m×킷) 밖, 사냥 라인(z≈32~35)과 겹치지 않게 셋으로 묶는다.
    /// </summary>
    public static class ExtraMobRoster
    {
        public static readonly (string Name, string MobId, float X, float Z, float Yaw)[] Spots =
        {
            // 무리 서 — 마을 북서, 사냥터보다 남
            ("SkelMage", MobCatalog.SkelMage, -12.00f, 18.00f, 200f),
            ("Squire", MobCatalog.Squire, -9.40f, 20.20f, 175f),
            // 무리 동 — 동쪽 들판 참나무 근처
            ("Cutthroat", MobCatalog.Cutthroat, 20.00f, 6.00f, 210f),
            ("Seer", MobCatalog.Seer, 22.40f, 4.20f, 160f),
            // 무리 남 — 아마밭 남쪽
            ("Bonekin", MobCatalog.Bonekin, 6.00f, -24.00f, 10f),
            ("Runt", MobCatalog.Runt, 8.20f, -22.40f, 40f),
        };

        public static Vector2 World(int i) =>
            new Vector2(Spots[i].X * WorldScale.Kit, Spots[i].Z * WorldScale.Kit);
    }
}
