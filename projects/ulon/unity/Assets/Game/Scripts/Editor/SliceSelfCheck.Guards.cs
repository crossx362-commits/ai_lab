using System;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// 던전3가 온전한지 확인한다. 어떤 슬라이스도 던전3를 지우거나 겹쳐 만들지
        /// 않았는지 보는 자리다.
        ///
        /// 원래는 "던전3가 있으면 안 된다"는 네거티브 가드였고, 같은 if/throw 두 줄이
        /// 9개 파일 51곳에 복사돼 있었다. 판정을 여기 하나로 모아둔 덕에 던전3가
        /// 들어온 지금 이 함수만 뒤집으면 됐다 — 호출부 51곳은 한 줄도 안 고쳤다.
        /// </summary>
        /// <param name="context">어느 슬라이스 뒤에 확인하는지. 실패 메시지에 붙는다.</param>
        /// <summary>
        /// 마을이 통째로 날아가지 않았는지 보는 **공용 전제**(던전3 잔재·DressVillage 부재·랜드마크·장식).
        /// 스킬·주문·펫 게이트 25곳에 같은 전문이 복붙돼 있었다 — 이름은 「봉합 주문」인데 몸통은 마을을
        /// 검사하고 있었다. 검수 지적: 「게이트 이름이 거짓이면 PASS 로그 전체가 거짓 증거」(2026-09-06).
        /// 전제는 전제라고 부르고 한 곳에 둔다.
        /// </summary>
        static void AssertVillageIntact()
        {
            AssertDungeon3Leftover();
            if (GameObject.Find("DressVillage") != null)
                throw new InvalidOperationException("DressVillage 오브젝트가 있으면 안 됩니다.");
            string[] keep = { "Forge", "Vendor", "Healer", HousingPlot.VendorObject, StableYard.Object };
            for (int i = 0; i < keep.Length; i++)
            {
                if (GameObject.Find(keep[i]) == null)
                    throw new InvalidOperationException("마을 랜드마크가 있어야 합니다: " + keep[i]);
            }
            AssertVillageDecorIntact();
        }

        /// <summary>
        /// 마을 장식이 통째로 사라지지 않았는가. **한 곳에만 둔다** — 같은 판정이 `Pets`에도 복사돼
        /// 있어서 바닥을 옮겼을 때 두 곳이 같이 울었다(2026-09-09, 「같은 로직이 여러 곳에 살면 재발한다」).
        /// </summary>
        internal static void AssertVillageDecorIntact()
        {
            var decor = GameObject.Find("VillageDecor");
            if (decor == null)
                throw new InvalidOperationException("VillageDecor 울타리/집을 지우면 안 됩니다.");
            // **바닥 판때기는 빼고 센다** — 이 자가 지키려는 것은 울타리·집이지 바닥이 아니다.
            // 광장 바닥이 킷 판때기(68칸)에서 지형 도포로 옮겨 가자 childCount가 200 밑으로 떨어져
            // 「울타리를 지웠다」고 울었다(2026-09-09). 세는 대상을 뜻에 맞추고, 수도 그만큼만 낮춘다
            // (200 − 68 = 132 → 130). 낮춘 것이 아니라 **바닥을 빼고 같은 것을 센다**.
            int solid = 0;
            foreach (Transform c in decor.transform)
                if (!c.name.StartsWith("road", StringComparison.Ordinal))
                    solid++;
            Debug.Log("[Ulon] 마을 장식 — 바닥 뺀 조각 " + solid + "개(하한 130)");
            if (solid < 130)
                throw new InvalidOperationException("VillageDecor 울타리/집을 지우면 안 됩니다(바닥 뺀 조각 " +
                    solid + "개 < 130).");
        }

        static void AssertDungeon3Leftover(string context = null)
        {
            var root = GameObject.Find(Dungeon3Root);
            var entrance = GameObject.Find(Dungeon3Entrance);
            if (root != null && entrance != null)
                return;
            string what = root == null && entrance == null ? "던전3가 통째로 사라졌습니다"
                        : root == null ? "던전3 루트가 사라졌습니다"
                        : "던전3 입구가 사라졌습니다";
            throw new InvalidOperationException(
                string.IsNullOrEmpty(context) ? what + "." : context + " " + what + ".");
        }

        const string Dungeon3Root = "Dungeon3";
        const string Dungeon3Entrance = "Dungeon3Entrance";
    }
}
