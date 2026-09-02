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
                return;
            CharacterStore.Save(CharacterBinder.Capture(AccountKey(), world.Player, world.SkillsOf(world.Player), world.StatsOf(world.Player)));
        }
    }
}
