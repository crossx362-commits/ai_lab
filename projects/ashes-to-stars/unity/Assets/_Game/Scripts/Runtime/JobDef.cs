using System;
using UnityEngine;

namespace AshesToStars
{
    // Unity는 ScriptableObject·MonoBehaviour마다 **클래스명과 같은 이름의 .cs 파일**을 요구한다.
    // 5종을 GameData.cs 한 파일에 넣었더니 Unity가 스크립트를 못 찾아
    // ("No script asset for MobDef …") Data/*.asset의 m_Script가 fileID: 0으로 끊겼다.
    // 그래서 타입마다 파일을 나눈다 — 다시 합치지 마라.

    [CreateAssetMenu(menuName = "재와별/직업", fileName = "Job_")]
    public class JobDef : ScriptableObject
    {
        public string 직업명;
        public RoleId 역할;
        [TextArea] public string 컨셉;
        [TextArea] public string 고유메커니즘;

        [Header("기본 스탯")]
        public float 최대체력 = 130f;
        public float 공격력 = 20f;
        [Tooltip("공격 사거리(유닛)")] public float 사거리 = 5f;
        [Tooltip("공격 간격(초)")] public float 공격간격 = 0.45f;

        [Header("이동기 (§5 — 전 캐릭터 보유)")]
        [Tooltip("대시 형태: 구르기/방패돌진/점멸/스텝/위치교체")] public string 이동기형태 = "구르기";
        [Tooltip("무적 프레임(초) — 기본 0.3")] public float 무적시간 = 0.3f;
        [Tooltip("쿨다운(초) — 기본 6")] public float 이동기쿨 = 6f;
        [Tooltip("이동 거리(기본 이동 몇 초분)")] public float 이동기거리 = 3f;

        [Header("스킬 (기본2 → 1차전직4 → 2차전직 +초필)")]
        public SkillDef[] 스킬 = new SkillDef[0];

        [Header("리스크 프로필 (§3)")]
        [Tooltip("최저/낮음/중/중상/높음")] public string 사망리스크 = "중";
        [Tooltip("필드 자동사냥 적합도 0~1")] [Range(0f, 1f)] public float 자동사냥적합도 = 0.5f;
    }
}
