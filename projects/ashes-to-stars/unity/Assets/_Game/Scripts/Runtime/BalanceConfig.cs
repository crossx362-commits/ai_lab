using System;
using UnityEngine;

namespace AshesToStars
{
    // Unity는 ScriptableObject·MonoBehaviour마다 **클래스명과 같은 이름의 .cs 파일**을 요구한다.
    // 5종을 GameData.cs 한 파일에 넣었더니 Unity가 스크립트를 못 찾아
    // ("No script asset for MobDef …") Data/*.asset의 m_Script가 fileID: 0으로 끊겼다.
    // 그래서 타입마다 파일을 나눈다 — 다시 합치지 마라.

    // ────────────────────────────────────────────────────────
    [CreateAssetMenu(menuName = "재와별/밸런스 설정", fileName = "GameBalance")]
    public class BalanceConfig : ScriptableObject
    {
        [Header("앵커 (§18 — 이것만 바꾸면 경제 전체가 스케일된다)")]
        [Tooltip("티어1 필드 자동사냥 1시간 수익 = 1골드. 소비처: GhAnchor.Hours → WaveHuntGold · 속성 탭 줄")] public float 티어1시간당골드 = 1f;
        [Tooltip("티어당 수익 배율. 소비처: TierMul.Factor → Economy.TierRevenueMultiplier · 속성 탭 줄")] public float 티어배율 = 1.6f;
        [Tooltip("하위 레이드 스케일링 계수")] public float 스케일링계수 = 0.65f;
        [Tooltip("총 유입 대비 소각 목표(%). 소비처: BurnTarget.Range → 속성 탭 줄")] public Vector2 소각목표 = new Vector2(45f, 55f);

        [Header("플레이어")]
        [Tooltip("초당 유닛. 소비처: MoveSpd.Units → 속성 탭 줄. 잡몹 속도 배율의 기준(§18-11)")] public float 플레이어이동속도 = 4.2f;

        [Header("사망 (§4·§18-8)")]
        public int 사망상한 = 3;
        public float PvE회복시간 = 24f;
        public float PvP회복시간 = 12f;
        public int 부활초소지상한 = 3;

        [Header("성능 예산 (§10-9)")]
        public int 잡몹상한 = 500;
        public int 소환수상한 = 50;
        public int 투사체상한 = 200; // 소비처: ProjCap.Limit → StressTest 풀 · 속성 탭 줄

        [Header("쿼터뷰")]
        [Tooltip("월드 y를 화면에 그릴 때 곱하는 값 = sin(30°)")] public float ISO_Y = 0.5f;
    }
}
