using UnityEngine;

namespace Ulon.Shared
{
    /// <summary>
    /// 기획 §7.2 — 클라는 「이동하려 한다」만 보내고 위치는 서버가 정한다.
    /// 클릭 목적지가 섬 밖·NaN이면 거절. 먼 클릭은 원작처럼 걸어가므로 거리 상한은 두지 않는다.
    /// 이동 속도 수치는 기획서에 없어 여기서 정하지 않는다.
    /// </summary>
    public static class MoveAuthority
    {
        /// <summary>네거티브 컨트롤 — true면 섬 밖도 받아 자가 빨간불이 나야 한다.</summary>
        public static bool NcOpen;

        const float EdgeSlack = 2f;

        public static bool Accept(Vector3 dest)
        {
            if (NcOpen)
                return true;
            if (!IsFinite(dest.x) || !IsFinite(dest.y) || !IsFinite(dest.z))
                return false;
            float half = WorldTerrain.Half + EdgeSlack;
            if (Mathf.Abs(dest.x) > half || Mathf.Abs(dest.z) > half)
                return false;
            float yMin = WorldTerrain.LandBase - WorldTerrain.DungeonDepth - 8f;
            float yMax = WorldTerrain.MaxHeight + 20f;
            if (dest.y < yMin || dest.y > yMax)
                return false;
            return true;
        }

        static bool IsFinite(float v)
        {
            return !float.IsNaN(v) && !float.IsInfinity(v);
        }
    }
}
