using Ulon.Shared;

namespace Ulon.Server
{
    public sealed partial class OfflineWorld
    {
        /// <summary>기획 §3.1 — ↑/↓/Lock 은 서버가 정한다. 클라 HUD는 요청만.</summary>
        public AttackResult TryCycleSkillLock(WorldBody body, SkillId id)
        {
            if (SkillLockAuth.NcOpen)
                return new AttackResult { FailReason = "nc_open" };
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            if ((int)id < 0 || (int)id >= (int)SkillId.Count)
                return new AttackResult { FailReason = "bad_skill" };
            var sk = SkillsOf(body);
            var before = sk.GetLock(id);
            sk.CycleLock(id);
            var after = sk.GetLock(id);
            if (after == before)
                return new AttackResult { FailReason = "refused" };
            return new AttackResult { Applied = true };
        }

        /// <summary>기획 §18.2 — STR/DEX/INT 잠금도 서버가 정한다.</summary>
        public AttackResult TryCycleStatLock(WorldBody body, StatId id)
        {
            if (SkillLockAuth.NcOpen)
                return new AttackResult { FailReason = "nc_open" };
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            var st = StatsOf(body);
            var before = st.GetLock(id);
            st.CycleLock(id);
            var after = st.GetLock(id);
            if (after == before)
                return new AttackResult { FailReason = "refused" };
            return new AttackResult { Applied = true };
        }
    }
}
