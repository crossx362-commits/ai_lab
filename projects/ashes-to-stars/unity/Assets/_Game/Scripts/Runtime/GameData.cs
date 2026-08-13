using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 기획서의 수치를 **에디터에서 보고 고칠 수 있게** ScriptableObject로 옮긴 것.
    ///
    /// 왜 코드가 아니라 에셋인가:
    ///   기획서 §18 수치 밸런싱 기준표는 플레이테스트에서 계속 조정된다.
    ///   수치가 C# 상수로 박혀 있으면 값 하나 바꿀 때마다 컴파일·빌드가 필요하고,
    ///   기획자가 직접 만질 수 없다. 인스펙터에서 만지고 바로 플레이할 수 있어야 한다.
    ///
    /// 문서 대응: 각 필드의 툴팁에 기획서 절 번호를 적어 둔다.
    /// </summary>

    public enum RaceId { 인간, 엘프, 드워프, 수인 }
    public enum RoleId { 탱, 딜, 힐, 버퍼 }
    public enum MobAi { 추적, 포위, 돌진, 원거리 }
    public enum MobFamily { 야수, 언데드, 마족, 기계, 정령 }
    public enum EliteKind { 없음, 수호자, 처형자, 주술사, 군단장, 저주술사, 소환술사 }
    public enum StyleId { 공격형, 균형형, 방어형, 생존형 }

    // ────────────────────────────────────────────────────────
    [Serializable]
    public class SkillDef
    {
        public string 이름;
        [TextArea] public string 설명;
        [Tooltip("쿨다운(초) — 마나 없음, 쿨다운 단일 체계(§11-5)")] public float 쿨다운 = 5f;
        [Tooltip("기본 공격력 대비 배율")] public float 위력배율 = 1f;
        [Tooltip("효과 반경(0이면 단일)")] public float 반경 = 0f;
        [Tooltip("고유 자원 소모량(0이면 미사용)")] public float 자원소모 = 0f;
        [Tooltip("초필살기 여부 — 2차 전직에서 해금(§3)")] public bool 초필살기 = false;
    }

}
