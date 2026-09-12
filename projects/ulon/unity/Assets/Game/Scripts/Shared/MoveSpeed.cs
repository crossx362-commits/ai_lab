namespace Ulon.Shared
{
    /// <summary>
    /// 기획 §7.2.1 Classic 2D 이동 리듬. 1타일 = 1m.
    /// 출처: http://wikiwiki.jp/uoemu/Tips9 , 보조 https://github.com/xrip/uo-client
    /// </summary>
    public static class MoveSpeed
    {
        public const float WalkStepSeconds = 0.40f;
        public const float RunStepSeconds = 0.20f;
        public const float MountWalkStepSeconds = 0.20f;
        public const float MountRunStepSeconds = 0.10f;

        public const float WalkMetersPerSecond = 1f / WalkStepSeconds;
        public const float RunMetersPerSecond = 1f / RunStepSeconds;
    }
}
