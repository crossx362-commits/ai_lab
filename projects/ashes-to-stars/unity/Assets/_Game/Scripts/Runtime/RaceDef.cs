using System;
using UnityEngine;

namespace AshesToStars
{
    // Unity는 ScriptableObject·MonoBehaviour마다 **클래스명과 같은 이름의 .cs 파일**을 요구한다.
    // 5종을 GameData.cs 한 파일에 넣었더니 Unity가 스크립트를 못 찾아
    // ("No script asset for MobDef …") Data/*.asset의 m_Script가 fileID: 0으로 끊겼다.
    // 그래서 타입마다 파일을 나눈다 — 다시 합치지 마라.

    // ────────────────────────────────────────────────────────
    [CreateAssetMenu(menuName = "재와별/종족", fileName = "Race_")]
    public class RaceDef : ScriptableObject
    {
        public RaceId Id;
        [TextArea] public string 정체성;

        [Header("기울기 (§18-9 — 10~20% 원칙)")]
        [Tooltip("경험치 획득 배율")] public float 경험치배율 = 1f;
        [Tooltip("전직 재료 획득 배율")] public float 전직재료배율 = 1f;
        [Tooltip("이동 속도 배율")] public float 이속배율 = 1f;
        [Tooltip("최대 HP 배율")] public float 체력배율 = 1f;
        [Tooltip("방어 배율")] public float 방어배율 = 1f;
        [Tooltip("PvE 사망 후 회복 시간(시간) — §18-8")] public float 회복시간 = 24f;
        [Tooltip("영지 생산 배율")] public float 영지생산배율 = 1f;
        [Tooltip("몬스터 드랍률 배율")] public float 드랍률배율 = 1f;
        [Tooltip("침략 약탈량 배율")] public float 약탈량배율 = 1f;
        [Tooltip("별 인식 범위 배율")] public float 인식범위배율 = 1f;
        [Tooltip("골드 소모 행위 비용 배율 — 드워프 0.80")] public float 골드소비배율 = 1f;
        [Tooltip("경매 수수료(%) — 기본 10")] public float 경매수수료 = 10f;

        [Header("고유 메커니즘")]
        [TextArea] public string 고유메커니즘;
        [Tooltip("드워프 불굴 / 수인 야성감각 발동 확률")] public float 고유발동확률 = 0f;
        [Tooltip("고유 메커니즘 발동 제한 — 전투당 횟수")] public int 전투당발동 = 1;
    }
}
