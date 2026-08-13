using System.Collections.Generic;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 플레이어의 지속 상태 — 지갑·소지품·탑 진행도.
    ///
    /// 왜 필요한가:
    ///   Economy에 `Wallet`·`LifeItemInventory` **클래스**는 있었지만 그것을 **가진 주체**가 없어서,
    ///   전투 보상이 계산만 되고 어디에도 쌓이지 않았다(계산 ≠ 반영).
    ///   경제가 순환하려면 "번 돈이 남아 있고, 그 돈으로 다음 판에 들어간다"가 성립해야 한다(§2 코어 루프).
    ///
    /// 저장:
    ///   프로토타입이라 PlayerPrefs로 충분하다. 본 게임에서는 서버가 필요하다 —
    ///   §4의 회복 시간과 §12의 재화는 클라이언트가 들고 있으면 시간·수치 조작에 그대로 노출된다.
    /// </summary>
    public static class GameState
    {
        const string K_COPPER = "ats.copper";
        const string K_FLOOR = "ats.tower_floor";
        const string K_ITEM = "ats.item.";

        static Economy.Wallet _wallet;
        static Economy.LifeItemInventory _bag;
        static bool _loaded;

        /// <summary>지갑. §12의 쿠퍼–실버–골드 3단계는 Economy가 환산한다.</summary>
        public static Economy.Wallet Wallet { get { Load(); return _wallet; } }

        /// <summary>목숨 아이템 소지품. 상한(§18-4)은 Economy가 강제한다.</summary>
        public static Economy.LifeItemInventory Bag { get { Load(); return _bag; } }

        /// <summary>도달한 탑 최고 층(§8). 10층 돌파마다 필드·던전 티어가 오른다(§10-6).</summary>
        public static int TowerFloor
        {
            get { Load(); return _floor; }
            private set { _floor = Mathf.Max(1, value); Save(); }
        }
        static int _floor = 1;

        /// <summary>
        /// 현재 콘텐츠 티어. §10-6이 "탑 10층 돌파마다 한 단계"라고 정했다.
        /// 수익 곡선(§18-1)과 진입 비용(§18-2)이 이 값을 쓴다.
        /// </summary>
        public static int Tier => Mathf.Clamp((TowerFloor - 1) / 10, 0, 9);

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _wallet = new Economy.Wallet();
            _bag = new Economy.LifeItemInventory();

            long copper = 0;
            long.TryParse(PlayerPrefs.GetString(K_COPPER, "0"), out copper);
            if (copper > 0) _wallet.TryAdd(copper);

            _floor = Mathf.Max(1, PlayerPrefs.GetInt(K_FLOOR, 1));

            foreach (Economy.LifeItem it in System.Enum.GetValues(typeof(Economy.LifeItem)))
            {
                int n = PlayerPrefs.GetInt(K_ITEM + it, 0);
                if (n > 0) _bag.TryAdd(it, n);
            }
        }

        static void Save()
        {
            if (!_loaded) return;
            PlayerPrefs.SetString(K_COPPER, _wallet.Copper.ToString());
            PlayerPrefs.SetInt(K_FLOOR, _floor);
            foreach (Economy.LifeItem it in System.Enum.GetValues(typeof(Economy.LifeItem)))
                PlayerPrefs.SetInt(K_ITEM + it, _bag.GetCount(it));
            PlayerPrefs.Save();
        }

        /// <summary>보상 지급. 실제로 지갑에 들어가고 저장된다.</summary>
        public static void Earn(long copper)
        {
            Load();
            if (copper <= 0) return;
            _wallet.TryAdd(copper);
            Save();
        }

        /// <summary>
        /// 진입 비용 차감. 모자라면 **차감하지 않고 false**를 돌려준다 —
        /// 부분 차감은 "돈은 냈는데 못 들어갔다"가 되어 최악이다.
        /// </summary>
        public static bool Pay(long copper)
        {
            Load();
            if (copper <= 0) return true;
            if (!_wallet.TrySubtract(copper)) return false;
            Save();
            return true;
        }

        /// <summary>드랍 획득. 상한(§18-4)에 걸리면 false — **소실이 아니라 획득 거부**다.</summary>
        public static bool Gain(Economy.LifeItem item, int amount = 1)
        {
            Load();
            bool ok = _bag.TryAdd(item, amount);
            if (ok) Save();
            return ok;
        }

        /// <summary>아이템 사용(부활초 등). 없으면 false.</summary>
        public static bool Consume(Economy.LifeItem item, int amount = 1)
        {
            Load();
            bool ok = _bag.TryConsume(item, amount);
            if (ok) Save();
            return ok;
        }

        /// <summary>탑 층 돌파. 최고 기록만 올라간다(재도전으로 내려가지 않는다).</summary>
        public static void ClearFloor(int floor)
        {
            Load();
            if (floor >= _floor) TowerFloor = floor + 1;
        }

        /// <summary>지금 보유량을 화면에 쓸 문자열로. 예: "12골드 30실버".</summary>
        public static string WalletText => Economy.FormatCurrency(Wallet.Copper);

        /// <summary>소지품 요약. 0개인 것은 빼고 보여준다.</summary>
        public static string BagText()
        {
            Load();
            var parts = new List<string>();
            foreach (Economy.LifeItem it in System.Enum.GetValues(typeof(Economy.LifeItem)))
            {
                int n = _bag.GetCount(it);
                if (n > 0) parts.Add($"{Label(it)} {n}/{Economy.ItemCapacity[it]}");
            }
            return parts.Count == 0 ? "소지품 없음" : string.Join(" · ", parts);
        }

        /// <summary>아이템 표시 이름(§4·§18-4의 우리말 명칭).</summary>
        public static string Label(Economy.LifeItem it) => it switch
        {
            Economy.LifeItem.RevivalTea => "부활초",
            Economy.LifeItem.ScrollOfReturn => "귀환의 두루마리",
            Economy.LifeItem.RebornStone => "환생석",
            _ => "특수 직업 증표",
        };

        /// <summary>디버그·초기화용. 프로토타입 테스트에서 상태를 되돌릴 때 쓴다.</summary>
        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(K_COPPER);
            PlayerPrefs.DeleteKey(K_FLOOR);
            foreach (Economy.LifeItem it in System.Enum.GetValues(typeof(Economy.LifeItem)))
                PlayerPrefs.DeleteKey(K_ITEM + it);
            PlayerPrefs.Save();
            _loaded = false;
        }
    }
}
