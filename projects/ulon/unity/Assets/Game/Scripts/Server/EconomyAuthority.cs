using UnityEngine;
using Ulon.Shared;

namespace Ulon.Server
{
    /// <summary>
    /// **골드·가방을 누가 정하는가**(축 ③, 오너 결정 (C) 단계 2026-09-08).
    ///
    /// 값을 SyncVar로 내려보내는 것만으로는 §417이 안 지켜진다 — **소비·획득 판정을 클라가 하면**
    /// 화면만 서버를 따라 하는 것이고, 그때 골드는 클라가 정한다(검수 조건 1).
    /// 실측한 구멍: `SliceHud`·`LocalAvatar`의 오프라인 폴백은 「NetAvatar가 아직 준비 안 됨」이면
    /// 그대로 `OfflineWorld.TryBuy/TryPick/TryGate…`를 **클라 프로세스에서** 돌린다 — 30곳이 넘는다.
    ///
    /// 그래서 호출부 30곳을 고치는 대신 **길목 둘**(골드 필드·가방 원본)에 문을 단다.
    /// 같은 로직이 여러 곳에 살면 재발한다(원장) — 문은 하나여야 한다.
    /// 동기화가 받아 적는 자리는 이 문을 지나지 않는다(`ApplyNetwork*`가 필드에 직접 쓴다).
    /// </summary>
    public static class EconomyAuthority
    {
        /// <summary>서버 없이 붙어 있는 클라 프로세스인가 — 그렇다면 경제는 내 몫이 아니다.</summary>
        public static bool ClientOnly
        {
            get
            {
                // NC 스위치: 문을 떼어 놓고 검사가 빨간불인지 본다(오늘의 실제 동작을 되살린다).
                if (Cli.Has("-ulon-nc-localeconomy"))
                    return false;
                var nm = FishNet.InstanceFinder.NetworkManager;
                if (nm == null)
                    return false;                        // 싱글 플레이(오프라인)는 그대로 제가 정한다
                return nm.ClientManager.Started && !nm.ServerManager.Started;
            }
        }

        /// <summary>바닥 어셈블리(`Ulon.Shared`)에 문고리를 꽂는다 — 판정은 여기 하나뿐이다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            WriteAuthority.Refuse = Refuse;
        }

        static float nextLogAt;

        /// <summary>거절을 조용히 하지 마라 — 왜 안 바뀌었는지 로그가 없으면 다음 사람이 못 찾는다.</summary>
        public static bool Refuse(string what)
        {
            if (!ClientOnly)
                return false;
            if (Time.realtimeSinceStartup >= nextLogAt)
            {
                nextLogAt = Time.realtimeSinceStartup + 1f;
                Debug.Log("[Ulon] 클라가 정할 수 없는 것 — " + what + " (서버가 정하고 클라는 받아 적는다, §417)");
            }
            return true;
        }
    }
}
