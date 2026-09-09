using System;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// **호수는 닫힌 물이고, 출구는 하나뿐이며, 강은 흐른다**(대장 판정 2026-09-10).
        ///
        /// 화면이 「바다 만과 부두」로 읽혔고, 세어 보니 호수 중심에서 이어지는 수면이
        /// **39,684㎡·226m로 해안 밖까지** 트여 있었다. 판정은 **국소 지형 수리**였다 —
        /// 개명(「만」으로 이름 바꾸기)은 오너가 명시한 넷(산·바다·강·호수)을 지우는 **라벨 수리**라 기각.
        ///
        /// 자는 셈과 **같은 함수**(`OutdoorCensus.Measure`)를 쓴다. 세 가지를 묻는다:
        ///   ① 목을 못 넘는 플러드필로 잰 **몸통이 원장 면적의 1.2배 이하**인가(봉합됐나).
        ///   ② 그 몸통이 **해안 밖에 안 닿는가**(만이 아닌가).
        ///   ③ 그냥 채운 플러드필은 **바다까지 닿는가**(강이 흐르나 — 봉합이 강을 막으면 안 된다).
        ///
        /// NC 둘. **출구를 막으면 ③이 울고, 봉합을 풀면 ①②가 운다** — 하나로는 못 가른다:
        /// 「호수를 막아 버리는」 수리도 ①②는 통과하기 때문이다.
        ///
        /// **이 자가 못 보는 것**: 화면이다. 폭·깊이가 원장과 맞아도 물로 안 읽힐 수 있다 —
        /// 그 판정은 `15_lake_river`와 강 근접 샷이 한다.
        /// </summary>
        const float LakeBodyMax = 1.2f;
        const float ShoreRadiusTol = 3f;
        const float OutletWidthMax = 8f;      // 지시는 6m — 격자·노이즈 여유 2m

        static void AssertLakeSealed()
        {
            var r = OutdoorCensus.Measure();
            if (r.BodyArea <= 0f)
                throw new InvalidOperationException("호수 봉합 — 원장 좌표(" + WorldTerrain.LakeX + ", " +
                    WorldTerrain.LakeZ + ")가 마른 땅입니다. **못 재는 자를 초록불로 남기지 않는다.**");
            Debug.Log("[Ulon] 호수 봉합 — 몸통 " + r.BodyArea.ToString("0") + "㎡(원장의 " +
                      r.Ratio.ToString("0.00") + "배, 상한 " + LakeBodyMax.ToString("0.0") + ") · 해안 밖 " +
                      (r.BodyReachesSea ? "닿음" : "안 닿음") + " · 강 바다까지 " +
                      (r.RiverReachesSea ? "흐름" : "끊김") + " · 출구 총폭 " +
                      (WorldTerrain.ChannelHalfWidth(WorldTerrain.LakeX - WorldTerrain.LakeRadius) * 2f).ToString("0.0") +
                      "m · 물가 개구부 " + r.OpenCount + "곳 최대 " + r.OpenWidest.ToString("0.0") + "m");

            // **개구부가 봉합의 본체다.** 플러드필의 목 차단 필터는 옛 강(반폭 7.75m)도 막아 버려
            // 옛 세계마저 「닫힌 호수」로 보인다 — 그래서 물가 원 위의 열린 구간을 직접 센다.
            if (r.OpenCount != 1 || r.OpenWidest > OutletWidthMax)
                throw new InvalidOperationException("호수 물가의 개구부가 " + r.OpenCount + "곳(최대 " +
                    r.OpenWidest.ToString("0.0") + "m)입니다 — 닫힌 물에 **출구 하나**(≤" +
                    OutletWidthMax.ToString("0") + "m)여야 합니다. 넓으면 화면에서 만으로 읽힙니다.");

            // **원장대로 팠나** — 판정의 방향이 「자를 실물에 맞추지 말고 `carve`를 원장에 맞춘다」였다.
            // 옛 규칙은 둑을 반경 **안쪽**에 둬서 물이 21m가 아니라 12m에서 끝났다.
            if (Mathf.Abs(r.ShoreMedian - WorldTerrain.LakeRadius) > ShoreRadiusTol)
                throw new InvalidOperationException("호수 물가 반경이 " + r.ShoreMedian.ToString("0.0") +
                    "m입니다(원장 " + WorldTerrain.LakeRadius + "m, 허용 ±" + ShoreRadiusTol.ToString("0.0") +
                    "m) — `carve`가 원장을 안 따릅니다.");

            if (r.BodyReachesSea || r.Ratio > LakeBodyMax)
                throw new InvalidOperationException("호수가 닫혀 있지 않습니다 — 몸통 " +
                    r.BodyArea.ToString("0") + "㎡(원장의 " + r.Ratio.ToString("0.00") + "배)" +
                    (r.BodyReachesSea ? ", 해안 밖까지 이어집니다" : "") + ". 화면에서 호수가 아니라 만으로 읽힙니다.");
            if (!r.RiverReachesSea)
                throw new InvalidOperationException("강이 바다까지 안 흐릅니다 — 호수를 봉합하면서 " +
                    "출구까지 막았습니다. 호수는 닫히되 **출구 하나**는 남아야 합니다.");

            // NC ① 출구를 막으면 강이 끊겨야 한다.
            WorldTerrain.OutletDisabled = true;
            var noOutlet = OutdoorCensus.Measure();
            WorldTerrain.OutletDisabled = false;
            if (noOutlet.RiverReachesSea || noOutlet.OpenCount != 0)
                throw new InvalidOperationException("네거티브 컨트롤 실패 — 출구를 막았는데 강이 " +
                    (noOutlet.RiverReachesSea ? "여전히 바다까지 흐릅니다" : "") +
                    "(개구부 " + noOutlet.OpenCount + "곳). 이 자는 출구를 안 보고 있습니다.");

            // NC ② 봉합을 풀면 호수가 바다와 합쳐져야 한다.
            WorldTerrain.LakeSealDisabled = true;
            var noSeal = OutdoorCensus.Measure();
            WorldTerrain.LakeSealDisabled = false;
            if (noSeal.OpenWidest <= OutletWidthMax &&
                Mathf.Abs(noSeal.ShoreMedian - WorldTerrain.LakeRadius) <= ShoreRadiusTol)
                throw new InvalidOperationException("네거티브 컨트롤 실패 — 옛 규칙(둑이 반경 안쪽)으로 " +
                    "되돌렸는데도 개구부 최대 " + noSeal.OpenWidest.ToString("0.0") + "m · 물가 " +
                    noSeal.ShoreMedian.ToString("0.0") + "m로 통과합니다. 이 자는 봉합을 안 보고 있습니다.");
            Debug.Log("[Ulon] 호수 봉합 네거티브 컨트롤 통과 — 출구 막으면 강 " +
                      (noOutlet.RiverReachesSea ? "흐름" : "끊김") + " · 봉합 풀면 몸통 " +
                      noSeal.Ratio.ToString("0.00") + "배·개구부 최대 " + noSeal.OpenWidest.ToString("0.0") +
                      "m·물가 " + noSeal.ShoreMedian.ToString("0.0") + "m");
        }
    }
}
