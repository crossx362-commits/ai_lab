using System;
using UnityEngine;

namespace AshesToStars
{
    // Unity는 ScriptableObject·MonoBehaviour마다 **클래스명과 같은 이름의 .cs 파일**을 요구한다.
    // 5종을 GameData.cs 한 파일에 넣었더니 Unity가 스크립트를 못 찾아
    // ("No script asset for MobDef …") Data/*.asset의 m_Script가 fileID: 0으로 끊겼다.
    // 그래서 타입마다 파일을 나눈다 — 다시 합치지 마라.

    // ────────────────────────────────────────────────────────
    [CreateAssetMenu(menuName = "재와별/몬스터", fileName = "Mob_")]
    public class MobDef : ScriptableObject
    {
        public string 이름;
        public MobAi AI;
        public MobFamily 계열;
        public EliteKind 정예유형 = EliteKind.없음;

        [Header("수치 (§18-11)")]
        [Tooltip("플레이어 이동 속도 대비 배율 — 추적0.90/포위0.85/원거리0.65")]
        public float 속도배율 = 0.90f;
        [Tooltip("캐릭터 1타 데미지 대비 HP 배율 (0.8~1.5 = 1~2타에 사망)")]
        public float 체력배율 = 1.2f;
        [Tooltip("캐릭터 최대 HP 대비 피해 비율 (0.02~0.04)")]
        public float 피해비율 = 0.03f;
        [Tooltip("근접 공격 간격(초)")] public float 공격간격 = 1.0f;

        [Header("원거리형 전용")]
        public float 유지거리 = 6.5f;
        public float 발사간격 = 2.4f;
        [Tooltip("탄속 — 느려야 회피 가능(§10-2)")] public float 탄속 = 5.5f;

        [Header("표시")]
        public Sprite 스프라이트;
        public Color 색조 = Color.white;
        public float 크기 = 2.2f;
    }
}
