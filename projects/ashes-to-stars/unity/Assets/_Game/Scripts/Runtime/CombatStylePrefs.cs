using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 파티원별 전투 스타일 선택을 저장한다(§3).
    ///
    /// 왜 따로 있나:
    ///   `CombatStyleDef`(에셋)는 **스타일이 어떤 수치인가**를 정한다. 그런데 그 표를
    ///   살려놓고도 **플레이어가 고르는 화면이 없어서** 전투는 계속 균형형 하나로만
    ///   돌았다(W3Party `_style` 필드가 파티 전체 공용에 기본값 고정). 즉 데이터는
    ///   있는데 **선택이 없었다** — 이 클래스가 그 선택을 담는 자리다.
    ///
    /// 키는 직업명이다. 파티원은 직업으로 유일하게 식별되고(수호기사·검사·마법사·
    /// 사제·음유시인 각 1명), 이름은 로스터에서 바뀔 수 있어 키로 쓰기 불안정하다.
    ///
    /// 저장은 `LifeSystem`과 같은 PlayerPrefs를 쓴다 — 프로토타입 단계의 기존 방식과
    /// 어긋나지 않게 맞춘 것이다. 저장소를 바꿀 땐 두 곳을 같이 옮길 것.
    /// </summary>
    public static class CombatStylePrefs
    {
        const string K_PREFIX = "ats.style.";

        /// <summary>기본값은 균형형 — 아무것도 안 고른 플레이어가 손해 보지 않는 값이다.</summary>
        public const StyleId Default = StyleId.균형형;

        public static StyleId Get(string job)
        {
            if (string.IsNullOrEmpty(job)) return Default;
            int v = PlayerPrefs.GetInt(K_PREFIX + job, (int)Default);
            // 저장값이 enum 범위를 벗어나면(에디터에서 enum을 고쳤거나 손으로 건드린 경우)
            // 조용히 이상한 스타일로 싸우게 두지 않고 기본값으로 되돌린다.
            return System.Enum.IsDefined(typeof(StyleId), v) ? (StyleId)v : Default;
        }

        public static void Set(string job, StyleId id)
        {
            if (string.IsNullOrEmpty(job)) return;
            PlayerPrefs.SetInt(K_PREFIX + job, (int)id);
            PlayerPrefs.Save();
        }

        /// <summary>스타일 한 줄 설명 — 화면과 로그가 같은 문구를 쓰게 한곳에 둔다.</summary>
        public static string Describe(StyleId id) => id switch
        {
            StyleId.공격형 => "피해를 더 주고 더 받는다. 후퇴하지 않는다",
            StyleId.방어형 => "피해를 덜 주고 덜 받는다. 탱커 뒤에 붙는다",
            StyleId.생존형 => "체력 절반에서 물러난다. 오래 버티는 대신 화력이 낮다",
            _ => "표준. 어느 쪽으로도 치우치지 않는다",
        };
    }
}
