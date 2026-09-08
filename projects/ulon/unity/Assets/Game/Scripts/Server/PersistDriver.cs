using UnityEngine;
using Ulon.Shared;

namespace Ulon.Server
{
    public sealed class PersistDriver : MonoBehaviour
    {
        public static bool Creating;
        public static bool Frozen;

        public static string AccountKey()
        {
            string cli = Cli.Get("-ulon-account", "");
            if (!string.IsNullOrEmpty(cli))
                return cli;
            string key = PlayerPrefs.GetString("ulon.account", "");
            if (!string.IsNullOrEmpty(key))
                return key;
            key = System.Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString("ulon.account", key);
            PlayerPrefs.Save();
            return key;
        }

        void Start()
        {
            CharacterStore.EnsureRunning();
            var world = OfflineWorld.Instance;
            if (world == null || world.Player == null)
                return;
            Frozen = OpLog.IsFrozen(AccountKey());
            if (Frozen)
            {
                OpLog.Write("gm", AccountKey(), "login", "frozen");
                return;
            }
            var snap = CharacterStore.Load(AccountKey());
            if (snap == null)
            {
                Creating = true;
                return;
            }
            CharacterBinder.Apply(world.Player, snap, world.SkillsOf(world.Player), world.StatsOf(world.Player));
            world.RestoreCorpse(AccountKey(), snap);
            world.LoadPlot(HousingPlot.Id);
        }

        public static void Commit(CharacterSnapshot snap)
        {
            var world = OfflineWorld.Instance;
            if (world == null || world.Player == null || snap == null)
                return;
            CharacterBinder.Apply(world.Player, snap, world.SkillsOf(world.Player), world.StatsOf(world.Player));
            CharacterStore.Save(CharacterBinder.Capture(AccountKey(), world.Player, world.SkillsOf(world.Player), world.StatsOf(world.Player)));
            Creating = false;
        }

        void OnDestroy()
        {
            SaveLocal();
        }

        public static void SaveLocal()
        {
            var world = OfflineWorld.Instance;
            if (world == null || world.Player == null)
            {
                // **왜 저장을 안 했는지도 남긴다** — 「안 일어났다」와 「일어났는데 조용했다」를
                // 로그로 가를 수 없으면 격리 실험이 성립하지 않는다(2026-09-08).
                Debug.Log("[Ulon] 종료 저장 건너뜀 — 월드 " + (world == null ? "없음" : "있음") +
                          " · 로컬 플레이어 없음(접속이 끊기면 아바타가 사라진다)");
                return;
            }
            // 나갈 때 무엇을 저장했는지 한 줄 남긴다 — 저장소가 언제 누구 값으로 덮였는지
            // 추적할 수 있어야 한다(2026-09-08 격리 실험에서 이 줄이 없어 한 판을 헛돌았다).
            Debug.Log("[Ulon] 종료 저장 — 계정 " + AccountKey() + " 골드 " + world.Player.Gold);
            CharacterStore.Save(CharacterBinder.Capture(AccountKey(), world.Player, world.SkillsOf(world.Player), world.StatsOf(world.Player)));
        }
    }
}
