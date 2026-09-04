namespace Ulon.Shared
{
    public static class TameCritter
    {
        public const string Id = "hart";
        public const string Object = "TameCritter";
        public const string DisplayName = "야생하트";
        public const float X = -22.8f;
        public const float Z = 8.4f;
        public const int ControlSlots = 1;
        public const float FollowOffsetX = 1.2f;
        public const float FollowOffsetZ = -0.8f;
    }


    public static class TameBoar
    {
        public const string Id = "boar";
        public const string Object = "TameBoar";
        public const string DisplayName = "야생멧돼지";
        public const float X = -25.6f;
        public const float Z = 11.2f;
        public const int ControlSlots = 1;
    }

    public sealed class TameRequest
    {
        public float Distance;
        public float Range = TameResolve.Range;
        public bool Ghost;
        public bool Tameable;
        public bool AlreadyPet;
        public int UsedSlots;
        public int ControlSlots = 1;
        public int FollowerCap = TameResolve.FollowerCap;
        public SkillSet Skills;
        public StatSet Stats;
        public float Difficulty = TameResolve.Difficulty;
    }

    public sealed class PetCommandRequest
    {
        public bool Ghost;
        public bool HasPet;
        public bool IsOwner;
        public bool HasEnemy;
        public bool PetAlive = true;
        public bool PetStabled;
    }

    public static class TameResolve
    {
        public const float Range = 2.8f;
        public const float AttackRange = 8f;
        public const float Difficulty = 8f;
        public const int FollowerCap = 2;

        public static AttackResult Tame(TameRequest req)
        {
            if (req == null || req.Skills == null)
                return Fail("bad_request");
            if (req.Ghost)
                return Fail("ghost");
            if (!req.Tameable)
                return Fail("not_tameable");
            if (req.AlreadyPet)
                return Fail("already_pet");
            if (req.Distance > req.Range)
                return Fail("range");
            int slots = req.ControlSlots < 1 ? 1 : req.ControlSlots;
            if (req.UsedSlots + slots > req.FollowerCap)
                return Fail("no_slot");

            SkillGain.TryRaise(req.Skills, SkillId.AnimalTaming, req.Difficulty, out float before, out float after, req.Stats);
            return new AttackResult
            {
                Applied = true,
                Hit = true,
                SkillBefore = before,
                SkillAfter = after
            };
        }

        public static AttackResult Follow(PetCommandRequest req) => Command(req);

        public static AttackResult Stay(PetCommandRequest req) => Command(req);

        public static AttackResult Guard(PetCommandRequest req) => Command(req);

        public static AttackResult Release(PetCommandRequest req) => Command(req);

        public static AttackResult Attack(PetCommandRequest req)
        {
            AttackResult gate = Command(req);
            if (!gate.Applied)
                return gate;
            if (req.PetStabled)
                return Fail("stabled");
            if (!req.PetAlive)
                return Fail("dead");
            if (!req.HasEnemy)
                return Fail("no_enemy");
            return new AttackResult { Applied = true, Hit = true };
        }

        public static AttackResult Come(PetCommandRequest req)
        {
            AttackResult gate = Command(req);
            if (!gate.Applied)
                return gate;
            if (req.PetStabled)
                return Fail("stabled");
            if (!req.PetAlive)
                return Fail("dead");
            return new AttackResult { Applied = true, Hit = true };
        }

        static AttackResult Command(PetCommandRequest req)
        {
            if (req == null)
                return Fail("bad_request");
            if (req.Ghost)
                return Fail("ghost");
            if (!req.HasPet)
                return Fail("not_pet");
            if (!req.IsOwner)
                return Fail("not_owner");
            return new AttackResult { Applied = true, Hit = true };
        }

        static AttackResult Fail(string reason) => new AttackResult { FailReason = reason };
    }
}
