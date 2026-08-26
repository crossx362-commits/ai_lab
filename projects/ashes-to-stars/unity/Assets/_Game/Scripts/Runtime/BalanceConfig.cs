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
        [Tooltip("PvE 사망 회복(시간). 소비처: PveRecover.Hours → LifeSystem.PveRecoverSeconds(종족표 차단·비인간 기본) · 속성 탭 줄")] public float PvE회복시간 = 24f;
        [Tooltip("PvP 사망 회복(시간). 소비처: PvpRecover.Hours → LifeSystem.PvpRecoverSeconds · 속성 탭 줄")] public float PvP회복시간 = 12f;
        public int 부활초소지상한 = 3;

        [Header("성능 예산 (§10-9)")]
        public int 잡몹상한 = 500;
        public int 소환수상한 = 50;
        public int 투사체상한 = 200; // 소비처: ProjCap.Limit → StressTest 풀 · 속성 탭 줄

        [Header("안전장치 (§18-14)")]
        [Tooltip("소환수 재소환 비용(실버). 원장 0.5G/h = T1 50실버 고정. 소비처: Resummon.CostSilver → 속성 탭 줄")] public float 재소환비용실버 = 50f;
        [Tooltip("소환수 재소환 쿨다운(초). 소비처: Resummon.CooldownSeconds → 속성 탭 줄")] public float 재소환쿨다운초 = 30f;

        [Header("정예 수호자 오라 (§10-2 — 원장은 「피해 감소 오라·고방어」만 확정, 수치는 미확정)")]
        [Tooltip("수호자 오라 반경(유닛). 원장 미확정. 소비처: EliteGuardian.AuraRadius → W3Party.DamageMob")] public float 수호자오라반경 = 3.5f;
        [Tooltip("오라 안 잡몹이 받는 피해 배율(0~1, 낮을수록 강한 보호). 원장 미확정. 소비처: EliteGuardian.NearbyTakenMul → W3Party.DamageMob")] public float 수호자주변피해배율 = 0.5f;
        [Tooltip("수호자 자신이 받는 피해 배율(0~1, 낮을수록 고방어). 원장 미확정. 소비처: EliteGuardian.SelfTakenMul → W3Party.DamageMob")] public float 수호자자체피해배율 = 0.4f;

        [Header("정예 군단장 오라 (§10-2 — 원장은 「공속·이속 증가」만 확정, 수치는 미확정)")]
        [Tooltip("군단장 오라 반경(유닛). 원장 미확정. 소비처: EliteLegion.AuraRadius → W3Party.TickMobs")] public float 군단장오라반경 = 4.0f;
        [Tooltip("오라 안 잡몹 공속 배율(≥1, 클수록 자주 때림). 원장 미확정. 소비처: EliteLegion.AtkSpdMul → W3Party.TickMobs(공격 쿨다운을 나눔)")] public float 군단장주변공속배율 = 1.5f;
        [Tooltip("오라 안 잡몹 이속 배율(≥1, 클수록 빠르게 접근). 원장 미확정. 소비처: EliteLegion.MoveMul → W3Party.TickMobs(이동 spd에 곱)")] public float 군단장주변이속배율 = 1.3f;

        [Header("정예 저주술사 오라 (§10-2 — 원장은 「플레이어 회복량·이속 감소」만 확정, 수치는 미확정)")]
        [Tooltip("저주술사 오라 반경(유닛). 원장 미확정. 소비처: EliteCurse.AuraRadius → W3Party.Heal/TickParty")] public float 저주술사오라반경 = 4.0f;
        [Tooltip("오라 안 파티가 받는 회복 배율(0~1, 낮을수록 강한 저주). 원장 미확정. 소비처: EliteCurse.HealMul → W3Party.Heal")] public float 저주술사회복배율 = 0.7f;
        [Tooltip("오라 안 파티 이속 배율(0~1, 낮을수록 느려짐). 원장 미확정. 소비처: EliteCurse.MoveMul → W3Party.TickParty(이동 step에 곱)")] public float 저주술사이속배율 = 0.85f;

        [Header("정예 처형자 (§10-2 — 원장은 「딜 거울·후열 돌진 폭딜」만 확정, 수치는 미확정)")]
        [Tooltip("처형자 후열 명중 폭딜 배율(≥1, 클수록 위협적). 원장 미확정. 소비처: EliteExecutioner.BurstMul → W3Party.TickMobs(근접 피해에 곱)")] public float 처형자폭딜배율 = 3.0f;
        [Tooltip("처형자 돌진 이동 배율(플레이어 대비, 정예 중 가장 빠름). 원장 미확정. 소비처: EliteExecutioner.RushMul → W3Party.TickMobs(이동 spd)")] public float 처형자돌진속도배율 = 0.95f;

        [Header("영지 건설 (§18-12)")]
        [Tooltip("동시 건설 슬롯 상한(본성 라인 1 + 그 외 1). 소비처: BuildSlots.Cap → EstateBuild.WhyCannotUpgrade")] public int 동시건설슬롯 = 2;

        [Header("쿼터뷰")]
        [Tooltip("월드 y를 화면에 그릴 때 곱하는 값 = sin(30°)")] public float ISO_Y = 0.5f;
    }
}
