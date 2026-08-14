using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 지형 노이즈 — **바닥 색조와 프랍 배치가 같은 규칙을 본다.**
    ///
    /// 왜 공용으로 빼는가:
    ///   같은 개념을 두 곳에 따로 구현하면 한쪽만 고쳐져 어긋난다 — 이 저장소가
    ///   반복해서 겪은 패턴이다(백로그 owner 판정·회의 보류 사유·이중가동 가드…).
    ///   실제로 여기도 그랬다: `NoiseTerrain`이 노이즈 배치를 구현해 뒀는데
    ///   `FieldDecor`는 완전 랜덤으로 따로 뿌리고 있었고, 정작 `NoiseTerrain.ScatterProps`는
    ///   `장애물스프라이트`가 비어 있어 **한 번도 실행된 적이 없다**(2026-08-14 실측).
    ///
    /// 왜 배치가 노이즈여야 하는가(§10-2):
    ///   완전 랜덤이면 "저 바위 뒤로 돌아간다"가 성립하지 않는다. 노이즈 봉우리에
    ///   심으면 프랍이 **덩어리로 뭉치고 사이에 길이 생긴다** — 그때 비로소 위치 선정이
    ///   판단이 된다. 바닥 색조도 같은 노이즈를 보므로 바위 구역에 바위가 난다.
    /// </summary>
    public static class TerrainNoise
    {
        public const float 대형스케일 = 0.035f;
        public const float 소형스케일 = 0.16f;
        public const float 잔무늬비중 = 0.35f;

        /// <summary>시드에서 노이즈 원점을 뽑는다. 같은 시드 = 같은 지형(§3-2 재현성).</summary>
        public static Vector2 Origin(int seed)
        {
            var rng = new System.Random(seed);
            return new Vector2((float)rng.NextDouble() * 10000f, (float)rng.NextDouble() * 10000f);
        }

        /// <summary>노이즈 값 0~1 — 대형 지형 + 잔무늬를 섞는다.</summary>
        public static float Sample(Vector2 origin, float x, float y)
        {
            float big = Mathf.PerlinNoise(origin.x + x * 대형스케일, origin.y + y * 대형스케일);
            float small = Mathf.PerlinNoise(origin.x + 100f + x * 소형스케일,
                                            origin.y + 100f + y * 소형스케일);
            return Mathf.Clamp01(big * (1f - 잔무늬비중) + small * 잔무늬비중);
        }
    }
}
