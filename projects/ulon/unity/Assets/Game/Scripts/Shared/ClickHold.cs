namespace Ulon.Shared
{
    /// <summary>
    /// 기획 §4.2 클릭/홀드 이동. 원작은 버튼을 떼면 멈춘다.
    /// 한 프레임만 눌린 짧은 클릭은 목적지까지 걷는다(PC 클릭 이동).
    /// 출처: https://www.uoguide.com/New_Player_Guide:_Introduction_to_the_Interface
    /// 「until you come to an obstruction or release the mouse button」
    /// 보조: https://uo.com/wiki/ultima-online-wiki/beginning-the-adventure/movement-and-travel/
    /// </summary>
    public static class ClickHold
    {
        /// <summary>네거티브 컨트롤 — true면 홀드를 떼도 안 멈춘다(옛 동작).</summary>
        public static bool NcOpen;

        public static bool StopOnRelease(int heldFrames)
        {
            if (NcOpen)
                return false;
            return heldFrames >= 2;
        }
    }
}
