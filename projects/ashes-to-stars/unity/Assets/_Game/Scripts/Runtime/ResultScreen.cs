using UnityEngine;
using System.Collections.Generic;

namespace AshesToStars
{
    // Unity는 MonoBehaviour마다 **클래스명과 같은 이름의 .cs 파일**을 요구한다.
    // 한 파일(Screens.cs)에 화면 8종을 넣었더니 Unity가 대표 클래스를 못 찾아
    // 첫 클래스(BattleRewardInfo)로 해석했고, 그것이 MonoBehaviour가 아니라서
    // 씬의 컴포넌트를 통째로 떼어냈다("references runtime script in scene file. Fixing!").
    // 그래서 클래스마다 파일을 나눈다 — 다시 합치지 마라.

    /// <summary>결과 — 전투가 끝나고 들어온 곳으로 돌아간다.</summary>
    public class ResultScreen : GameScreen
    {
        protected override string Title => "결과";
        protected override string Subtitle => "보상 정산 후 원래 화면으로";
        protected override bool ShowBottomBar => false;

        int _rowIndex = 0;

        protected override void Body(Rect r)
        {
            _rowIndex = 0;

            // 전투 결과 요약 (§2 코어 루프)
            Info(r, _rowIndex++, string.IsNullOrEmpty(GameFlow.LastBattleSummary) ? "전투 기록 없음" : GameFlow.LastBattleSummary);

            // 보상 정보 표시 — 승리했을 때만 (§18-1·§10-8·§18-4)
            var reward = BattleScreen._GetLastReward();
            if (reward != null && reward.Survived)
            {
                Info(r, _rowIndex++, "");  // 빈 줄
                RewardInfo(r, _rowIndex++, "gold", $"획득 골드: {Economy.FormatCurrency(reward.GoldReward)}");

                // 드랍 아이템 표시 (§10-8)
                if (reward.DroppedItems.Count > 0)
                {
                    foreach (var item in reward.DroppedItems)
                    {
                        RewardInfo(r, _rowIndex++, ItemAtlas.KeyFor(item), $"획득: {FormatLifeItem(item)}");
                    }
                }

                // 소지 상한 초과로 거절된 아이템 (§18-4 "획득 거부로 처리")
                if (reward.RejectedItems.Count > 0)
                {
                    foreach (var item in reward.RejectedItems)
                    {
                        RewardInfo(r, _rowIndex++, ItemAtlas.KeyFor(item), $"획득 불가: {FormatLifeItem(item)} — 소지 상한에 도달했습니다");
                    }
                }

                // 경험치 분배 (§3 레벨 비례 · §18-6 성장) — 출전 파티가 나눠 갖는다
                if (reward.ExpGains != null && reward.ExpGains.Count > 0)
                {
                    Info(r, _rowIndex++, "");  // 빈 줄
                    Info(r, _rowIndex++, "📈 경험치 (출전 레벨 비례 분배, §3)");
                    foreach (var line in reward.ExpGains)
                    {
                        Info(r, _rowIndex++, $"  {line}");
                    }
                }
            }

            Info(r, _rowIndex++, "");  // 빈 줄

            if (Row(r, _rowIndex++, "계속", "들어온 화면으로 복귀")) GameFlow.Go(GameFlow.ReturnTo);
            if (Row(r, _rowIndex++, "영지로", "허브 복귀(§16)")) GameFlow.Go(GameFlow.Estate);
        }

        string FormatLifeItem(Economy.LifeItem item)
        {
            return item switch
            {
                Economy.LifeItem.RevivalTea => "부활초 — 사망 카운트 1 차감 (§4)",
                Economy.LifeItem.ScrollOfReturn => "귀환의 두루마리 — 긴급 탈출 아이템 (§4)",
                Economy.LifeItem.RebornStone => "환생석 — 삭제된 캐릭터 복구 (§4)",
                Economy.LifeItem.AdvancementMaterial => "전직 재료 — 1차 전직에 5개 필요 (§3)",
                Economy.LifeItem.SpecialJobToken => "특수 직업 전직 증표 — 50층 이상 보상 (§3)",
                _ => "알 수 없는 아이템"
            };
        }

        void RewardInfo(Rect r, int index, string iconKey, string text)
        {
            Info(r, index, "       " + text);
            if (!string.IsNullOrEmpty(iconKey))
            {
                const float h = 58f, gap = 14f;
                ItemAtlas.Draw(new Rect(r.x + 4, r.y + index * (h + gap) + 5, 48, 48), iconKey);
            }
        }
    }
}
