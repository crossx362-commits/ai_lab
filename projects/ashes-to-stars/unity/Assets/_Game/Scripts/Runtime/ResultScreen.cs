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
        protected override string BackgroundArt => "bg_result";
        protected override bool ShowBottomBar => false;

        int _rowIndex = 0;

        protected override void Body(Rect r)
        {
            _rowIndex = 0;
            GameFlow.SeedV4WipeQaIfRequested();
            TowerEnding.SeedQaIfRequested();
            SoloRaidClear.SeedQaIfRequested();
            FloorRecruit.SeedQaIfRequested();
            BattleScreen.SeedHuntExpRewardQaIfRequested();

            // 층 보상 선택이 남아 있으면 에필로그보다 먼저 4종을 한 화면에 보여 준다.
            // 예전엔 줄 목록이라 100층 자막이 있으면 힐·버퍼가 잘렸다.
            if (FloorRecruit.PendingJob)
            {
                Hint(new Rect(r.x, r.y, r.width, 24f),
                    $"{FloorRecruit.PendingFloor}층 돌파 — 기본 직업 1종을 고른다");
                if (FloorRecruit.PendingSpecialBanner)
                    Hint(new Rect(r.x, r.y + 26f, r.width, 22f),
                        $"특수 직업 증표 {FloorRecruit.LastSpecialTokensGot}장 — 레이드 확률(§3)");
                float top = FloorRecruit.PendingSpecialBanner ? 52f : 28f;
                var picks = UiPages.Grid(new Rect(r.x, r.y + top, r.width, r.height - top - 80f), 2, 2, 12f);
                for (int i = 0; i < LifeSystem.BasicJobs.Length && i < picks.Length; i++)
                {
                    string job = LifeSystem.BasicJobs[i];
                    if (DrawCard(picks[i], LifeSystem.BasicJobLabel(job),
                            "Lv1 기본직업 · 명부에 들어온다"))
                        FloorRecruit.TryClaim(job);
                }
                return;
            }

            if (TowerEnding.PendingEpilogue)
            {
                Info(r, _rowIndex++, $"{TowerEnding.TitleName} — 100층 최초 클리어(§8)");
                Info(r, _rowIndex++, TowerEnding.EpilogueBody);
                Info(r, _rowIndex++, $"{TowerEnding.LookName} · 전투력 변화 없음 · 100층 재도전 가능");
                if (SoloRaidClear.PendingBanner)
                    Info(r, _rowIndex++, $"{SoloRaidClear.BannerText} · {SoloRaidClear.LookName}");
                if (FloorRecruit.PendingSpecialBanner)
                    Info(r, _rowIndex++,
                        $"특수 직업 증표 {FloorRecruit.LastSpecialTokensGot}장 — 레이드 확률(§3)");
                if (Row(r, _rowIndex++, "건너뛰기", "에필로그를 닫고 저장을 유지한다"))
                {
                    TowerEnding.SkipEpilogue();
                    SoloRaidClear.AckBanner();
                    FloorRecruit.AckSpecialBanner();
                    return;
                }
                if (Row(r, _rowIndex++, "계속", "영지로 돌아간다"))
                {
                    TowerEnding.SkipEpilogue();
                    SoloRaidClear.AckBanner();
                    FloorRecruit.AckSpecialBanner();
                    GameFlow.Go(GameFlow.Estate);
                    return;
                }
                return;
            }

            if (SoloRaidClear.PendingBanner)
            {
                Info(r, _rowIndex++, SoloRaidClear.BannerText);
                Info(r, _rowIndex++, $"{SoloRaidClear.LookName} · 전투력 변화 없음 · 같은 보스는 다시 안 준다");
            }

            if (FloorRecruit.PendingJob)
            {
                Info(r, _rowIndex++, $"{FloorRecruit.PendingFloor}층 돌파 — 기본 직업 1종을 고른다");
                for (int i = 0; i < LifeSystem.BasicJobs.Length; i++)
                {
                    string job = LifeSystem.BasicJobs[i];
                    if (Row(r, _rowIndex++, LifeSystem.BasicJobLabel(job),
                            "Lv1 기본직업 · 명부에 들어온다"))
                        FloorRecruit.TryClaim(job);
                }
            }
            else if (!string.IsNullOrEmpty(FloorRecruit.LastGrantedName))
            {
                Info(r, _rowIndex++,
                    $"영입 {FloorRecruit.LastGrantedName} ({LifeSystem.BasicJobLabel(FloorRecruit.LastGrantedJob)})");
            }
            if (FloorRecruit.PendingSpecialBanner)
                Info(r, _rowIndex++,
                    $"특수 직업 증표 {FloorRecruit.LastSpecialTokensGot}장 — 레이드 확률(§3)");

            // 전투 결과 요약 (§2 코어 루프). 영구 손실은 골드보다 먼저 읽힌다(§16-7).
            Info(r, _rowIndex++, string.IsNullOrEmpty(GameFlow.LastBattleSummary) ? "전투 기록 없음" : GameFlow.LastBattleSummary);
            var defeat = GameFlow.LastDefeatReport;
            if (defeat != null && defeat.FallenNames.Count > 0)
                Info(r, _rowIndex++, $"[사망] {string.Join(", ", defeat.FallenNames)}");
            if (defeat != null && defeat.DeletedNames.Count > 0)
                Info(r, _rowIndex++, $"[삭제] {string.Join(", ", defeat.DeletedNames)} — 재가 되어 영묘에 기록됩니다(§16-6)");
            if (defeat != null && defeat.RescueGranted)
                Info(r, _rowIndex++, $"계속 플레이: {defeat.RescueName} (딜 Lv1)이 명부에 있습니다");

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

            if (Row(r, _rowIndex++, "계속", "들어온 화면으로 복귀"))
            {
                SoloRaidClear.AckBanner();
                FloorRecruit.AckSpecialBanner();
                GameFlow.Go(GameFlow.ReturnTo);
            }
            if (Row(r, _rowIndex++, "영지로", "허브 복귀(§16)"))
            {
                SoloRaidClear.AckBanner();
                FloorRecruit.AckSpecialBanner();
                GameFlow.Go(GameFlow.Estate);
            }
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
                Economy.LifeItem.CraftHide => "사냥 가죽 — 대장간 갑옷 재료 (§11)",
                Economy.LifeItem.CraftFang => "송곳니 — 대장간 무기 재료 (§11)",
                Economy.LifeItem.CraftBone => "유골 — 대장간 투구 재료 (§11)",
                Economy.LifeItem.CraftPart => "부품 — 대장간 장갑 재료 (§11)",
                Economy.LifeItem.CraftCrystal => "원소결정 — 대장간 신발 재료 (§11)",
                Economy.LifeItem.CraftDemonite => "마정석 — 대장간 장신구 재료 (§11)",
                Economy.LifeItem.EnhanceStone => "강화석 — 장비 강화. 실패해도 파괴 없음 (§11)",
                _ => "알 수 없는 아이템"
            };
        }

        void RewardInfo(Rect r, int index, string iconKey, string text)
        {
            Info(r, index, "       " + text);
            if (!string.IsNullOrEmpty(iconKey))
            {
                const float h = 58f, gap = 14f;
                ItemAtlas.DrawHud(new Rect(r.x + 4, r.y + index * (h + gap) + 5, 48, 48), iconKey);
            }
        }
    }
}
