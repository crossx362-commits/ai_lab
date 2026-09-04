using System;
using UnityEngine;

namespace AshesToStars
{
    // Unity는 ScriptableObject·MonoBehaviour마다 **클래스명과 같은 이름의 .cs 파일**을 요구한다.
    // 5종을 GameData.cs 한 파일에 넣었더니 Unity가 스크립트를 못 찾아
    // ("No script asset for MobDef …") Data/*.asset의 m_Script가 fileID: 0으로 끊겼다.
    // 그래서 타입마다 파일을 나눈다 — 다시 합치지 마라.

    // ────────────────────────────────────────────────────────
    [CreateAssetMenu(menuName = "재와별/전투 스타일", fileName = "Style_")]
    public class CombatStyleDef : ScriptableObject
    {
        public StyleId Id;
        [Header("§3 전투 스타일 표")]
        [Tooltip("주는 피해 배율")] public float 딜배율 = 1f;
        [Tooltip("받는 피해 배율")] public float 피해배율 = 1f;
        [Tooltip("이 HP 비율 이하면 후퇴")] [Range(0f, 1f)] public float 후퇴체력 = 0.15f;
        [Tooltip("탱커로부터 유지할 거리")] public float 유지거리 = 1.4f;
        [TextArea] public string 행동설명;

        [Header("토글 기본값")]
        public bool 정예우선타겟 = false;
        [Tooltip("부활초·귀환의두루마리는 항상 false — 자동 사용 금지(§4)")]
        public bool 소모품자동사용 = false;
    }
}
