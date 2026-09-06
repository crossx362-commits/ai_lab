using UnityEngine;

namespace Ulon.Shared
{
    /// <summary>
    /// 기획서 §6.1 지역의 **좌표 원장**. 빌더와 Assert가 같은 값을 본다.
    /// 지역 이름만 문서에 있고 화면은 빈 초록이면 §8.2 위반이다(검수 2026-09-06 반려 4).
    /// 좌표는 평지 띠(체비셰프 88) 안, 마을(반경 23)·기존 필드(±20)·던전(±68)과 겹치지 않게 잡았다.
    /// </summary>
    public static class WorldRegions
    {
        public struct Region
        {
            public string Object;    // 씬 루트 오브젝트 이름
            public string Name;      // 표시 이름
            public float X;
            public float Z;
            public float Radius;
            public int PropMin;      // 이 반경 안에 있어야 하는 소품 수 하한
        }

        public const string MeadowObject = "Region_Meadow";
        public const string ForestObject = "Region_Forest";
        public const string MineObject = "Region_Mine";

        public static readonly Region Meadow = new Region
        { Object = MeadowObject, Name = "남동 농경지", X = 48f, Z = -36f, Radius = 26f, PropMin = 40 };

        public static readonly Region Forest = new Region
        { Object = ForestObject, Name = "북서 숲", X = -46f, Z = 50f, Radius = 30f, PropMin = 45 };

        public static readonly Region Mine = new Region
        { Object = MineObject, Name = "동쪽 광산", X = 74f, Z = 26f, Radius = 20f, PropMin = 20 };

        public static Region[] All => new[] { Meadow, Forest, Mine };

        /// <summary>지역 배치·소품 산포에서 같은 값을 뽑기 위한 결정적 난수(씬이 매번 흔들리면 검수가 못 한다).</summary>
        public static float Rand(int seed, int index, float min, float max)
        {
            uint h = (uint)(seed * 374761393 + index * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return Mathf.Lerp(min, max, (h & 0xFFFFFF) / (float)0xFFFFFF);
        }
    }
}
