using System;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// **강둑이 바둑판으로 읽히지 않는가**(검수 판정 2026-09-10 — 텍셀 랩을 닫고도 화면에 남은 것).
        ///
        /// 셈이 범인을 갈랐다(`OutdoorCensus.RunBankBlob`): 그 둑은 **MineGravel 72.6%**이고,
        /// 그 겹의 무늬만 지우면 잔결이 6.88 → 3.58로 **반이 날아갔다**. 해를 꺼도 상대 대비는
        /// 그대로였고(음영 아님) 도포 1등이 뒤집히는 칸은 2.8%뿐이었다(섞임 아님).
        /// 자갈이 **산 암벽 무늬(사선 층리)**를 쓰고 있던 것이 원인이라 제 무늬로 갈랐다.
        ///
        /// 자는 **잔결의 이웃 상관 r(1)** — 한 픽셀이 옆 픽셀과 얼마나 같은가. 덩어리로 뭉치면
        /// 높고, 낟알이면 낮다. 실측 무늬 1 **0.64** · 무늬 6 0.73 · **무늬 5 0.29**.
        /// 상한 **0.45**는 그 사이다. NC는 **옛 무늬로 실제로 다시 구워** 확인한다.
        ///
        /// **대상을 좁혔다**(2026-09-11, 물 재작업 1단계): 이 자의 상자는 화면 고정 사각형이라
        /// **강물이 그 안에 들어 있었다**. 물이 매끈해지자 둑과 무관하게 0.52로 울었고, 판별로
        /// 갈랐다(`OutdoorCensus.RunBankWaterProbe`) — 같은 판에서 물 켬 0.52 · 물 끔 0.16.
        /// 그래서 `BankLumpiness`가 **수면 렌더러를 끄고** 잰다. 상한은 다시 재서 그대로 뒀다:
        /// 새 눈금에서 지금 **0.16** · NC(옛 무늬 1로 실제 재굽기) **0.63** — 0.45가 그 사이다.
        ///
        /// 렌더가 필요하므로 `QaShots.Run` 끝에서 돈다.
        /// NC 굽기는 텍셀 NC와 **한 판으로 묶여** `SliceSelfCheck.OutdoorGrainNc`에서 돈다.
        /// </summary>
        const float BankLumpMax = 0.45f;

        public static void AssertBankNotBlocky()
        {
            float r1 = OutdoorCensus.BankLumpiness();
            Debug.Log("[Ulon] 둑 잔결 — 64_river_bend 이웃 상관 r(1) " + r1.ToString("0.00") +
                      " (상한 " + BankLumpMax.ToString("0.00") + ")");
            if (r1 > BankLumpMax)
                throw new InvalidOperationException("강둑 잔결의 이웃 상관이 " + r1.ToString("0.00") +
                    "입니다(상한 " + BankLumpMax.ToString("0.00") +
                    ") — 바닥이 낟알이 아니라 덩어리(바둑판)로 읽힙니다. 자갈 겹 무늬를 보십시오.");

        }
    }
}
