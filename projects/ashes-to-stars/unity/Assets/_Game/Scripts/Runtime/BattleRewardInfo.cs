using UnityEngine;
using System.Collections.Generic;

namespace AshesToStars
{
    // Unity는 MonoBehaviour마다 **클래스명과 같은 이름의 .cs 파일**을 요구한다.
    // 한 파일(Screens.cs)에 화면 8종을 넣었더니 Unity가 대표 클래스를 못 찾아
    // 첫 클래스(BattleRewardInfo)로 해석했고, 그것이 MonoBehaviour가 아니라서
    // 씬의 컴포넌트를 통째로 떼어냈다("references runtime script in scene file. Fixing!").
    // 그래서 클래스마다 파일을 나눈다 — 다시 합치지 마라.

    /// <summary>
    /// 전투 보상 정보 저장소 (§2 코어 루프: 재화 획득 → 캐릭터 성장)
    /// BattleScreen에서 계산하고 ResultScreen에서 표시한다.
    /// GameFlow.LastBattleSummary와 함께 결과를 전달한다.
    /// 출처: §18-1(티어별 수익), §10-8(드랍 설계), §18-4(소지 상한)
    /// </summary>
    public class BattleRewardInfo
    {
        /// <summary>획득 골드 (§18-1 티어별 수익)</summary>
        public long GoldReward { get; set; }

        /// <summary>경험치 분배 결과 표시 줄 (§3·§18-6). BattleScreen이 채우고 ResultScreen이 표시.</summary>
        public List<string> ExpGains { get; set; } = new List<string>();

        /// <summary>획득한 드랍 아이템 (부활초·귀환의 두루마리 등, §10-8)</summary>
        public List<Economy.LifeItem> DroppedItems { get; set; } = new List<Economy.LifeItem>();

        /// <summary>소지 상한에 걸려 획득 거부된 아이템 (§18-4)</summary>
        public List<Economy.LifeItem> RejectedItems { get; set; } = new List<Economy.LifeItem>();

        /// <summary>이 전투에서 삭제된 캐릭터 목록 (사망 카운트 3회 도달, §4)</summary>
        public List<string> DeletedCharacters { get; set; } = new List<string>();

        /// <summary>이 전투에서 회복 중이 된 캐릭터 (사망 카운트 증가, §4)</summary>
        public List<(string name, int deathCount)> RecoveredCharacters { get; set; } = new List<(string, int)>();

        /// <summary>전투 소요 시간 (초 단위)</summary>
        public float BattleDurationSeconds { get; set; }

        /// <summary>전투 승리 여부</summary>
        public bool Survived { get; set; }

        public void Clear()
        {
            GoldReward = 0;
            ExpGains.Clear();
            DroppedItems.Clear();
            RejectedItems.Clear();
            DeletedCharacters.Clear();
            RecoveredCharacters.Clear();
            BattleDurationSeconds = 0;
            Survived = false;
        }
    }
}
