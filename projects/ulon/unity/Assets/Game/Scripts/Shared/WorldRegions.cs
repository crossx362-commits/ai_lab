using UnityEngine;

namespace Ulon.Shared
{
    /// <summary>
    /// 기획서 §6.1 지역의 **좌표 원장**. 빌더와 Assert가 같은 값을 본다.
    /// 지역 이름만 문서에 있고 화면은 빈 초록이면 §8.2 위반이다(검수 2026-09-06 반려 4).
    /// 좌표는 평지 띠(체비셰프 88) 안이다. **「마을 반경 23·기존 필드 ±20과 안 겹친다」던 옛 주석은
    /// 모듈 좌표 기준이라 틀렸다** — 킷 배율(×2.1)을 태운 마을 장식·옛 필드·던전 입구는 세계 좌표로
    /// 두 배 넓게 퍼져 있어, 반경만 보면 지역 셋이 남의 것을 여럿 삼킨다(2026-09-09 실측 34건).
    /// 그래서 반경은 **도포·산포의 범위**로만 쓰고, 「남의 것이 끼어들었는가」는 반경이 아니라
    /// **그 지역이 놓은 물건까지의 거리**로 잰다(`AssertNoRegionIntrusion`) — 밭 한가운데 문게이트는
    /// 작물에서 1.6m, 숲 속 마을 울타리는 나무에서 1.4m였다. 던전 입구는 나무·이랑에서 멀어 결백하다.
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

        /// <summary>
        /// §6.1 「테스트 공간 — 개발자 전용 스킬/몬스터/장비 QA」. 던전 세 개가 쓰지 않는 네 번째 모서리에 둔다.
        /// 마을에서 이어지는 길이 없고 GM 패널(F1) 워프로만 간다 — 플레이어 동선에 끼어들지 않는다.
        /// </summary>
        public const string TestChamberObject = "Region_TestChamber";
        public static readonly Region TestChamber = new Region
        { Object = TestChamberObject, Name = "테스트 공간", X = -68f, Z = -68f, Radius = 14f, PropMin = 24 };

        /// <summary>평지 산포·소품 하한 판정 대상(테스트 공간은 개발자 전용이라 여기 넣지 않는다).</summary>
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
