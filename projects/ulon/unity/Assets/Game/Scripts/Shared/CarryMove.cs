namespace Ulon.Shared
{
    /// <summary>
    /// 기획 §18.5 과적 시 이동 제한. 원작 UO는 과적일 때 달릴 수 없다.
    /// 출처: https://www.uoguide.com/Weight — "If you are overweight, you will not be able to run".
    /// 걷기 감속 배율은 기획서에 없어 적용하지 않는다(옛 ClickMotor 0.35는 출처 없음).
    /// </summary>
    public static class CarryMove
    {
        /// <summary>네거티브 컨트롤 — true면 과적해도 달린다(옛 동작).</summary>
        public static bool NcOpen;

        public static bool CanRun(bool overweight)
        {
            if (NcOpen)
                return true;
            return !overweight;
        }
    }
}
