using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 계정 종족 선택을 저장한다(§3·§18-9).
    ///
    /// 왜 따로 있나 (`CombatStylePrefs`와 같은 계열):
    ///   `RaceDef`(에셋 4종)는 **종족이 어떤 기울기인가**를 정한다. 그런데 그 표를
    ///   살려놓고도 **읽는 코드가 0곳**이라 종족은 전투에 아무 영향이 없었다
    ///   (이 저장소가 반복해서 겪은 「정의는 있고 소비처가 없다」 — CombatStyleDef·
    ///   보스 기믹·prop_scale.json이 전부 그랬다). 이 클래스가 그 선택을 담는 자리다.
    ///
    /// 종족은 계정 단위다(파티원별이 아니라 한 계정이 한 종족). 그래서 키가 하나뿐이다.
    /// 저장은 `CombatStylePrefs`·`LifeSystem`과 같은 PlayerPrefs를 쓴다 — 프로토타입
    /// 단계의 기존 방식과 맞춘 것이다. 저장소를 바꿀 땐 세 곳을 같이 옮길 것.
    /// </summary>
    public static class RacePrefs
    {
        const string K = "ats.race";

        /// <summary>기본값은 인간 — 모든 배율이 1인 범용형이라, 아무것도 안 고른
        /// 플레이어(그리고 W1~W3 기존 측정)가 종족 도입 전과 똑같이 돈다.</summary>
        public const RaceId Default = RaceId.인간;

        public static RaceId Get()
        {
            int v = PlayerPrefs.GetInt(K, (int)Default);
            // 저장값이 enum 범위를 벗어나면(에디터에서 enum을 고쳤거나 손으로 건드린 경우)
            // 조용히 이상한 종족으로 싸우게 두지 않고 기본값으로 되돌린다.
            return System.Enum.IsDefined(typeof(RaceId), v) ? (RaceId)v : Default;
        }

        public static void Set(RaceId id)
        {
            PlayerPrefs.SetInt(K, (int)id);
            PlayerPrefs.Save();
        }
    }
}
