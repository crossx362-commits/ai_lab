namespace Ulon.Shared
{
    /// <summary>
    /// 스킬/스탯 잠금은 서버가 정한다(§3.1·§18.2). NC는 이 스위치를 켜 옛 클라 CycleLock을 되살린다.
    /// </summary>
    public static class SkillLockAuth
    {
        public static bool NcOpen;
    }
}
