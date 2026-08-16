using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 긴급 탈출(§4). 두루마리는 캐스트가 끝난 뒤에만 소모한다.
    /// 6초 동안 피격되면 취소되고 두루마리는 남는다.
    /// </summary>
    public static class EmergencyEscape
    {
        public const float CastSeconds = 6f;

        public enum Phase { Idle, Casting, Escaped, Cancelled, Rejected }

        static bool _casting;
        static float _elapsed;

        public static bool Casting => _casting;
        public static float Elapsed => _elapsed;
        public static float Progress => _casting ? Mathf.Clamp01(_elapsed / CastSeconds) : 0f;

        public static bool HasScroll() =>
            GameState.Bag.GetCount(Economy.LifeItem.ScrollOfReturn) > 0;

        public static bool TryBegin()
        {
            if (_casting || !HasScroll()) return false;
            _casting = true;
            _elapsed = 0f;
            return true;
        }

        public static Phase Tick(float dt, bool hit)
        {
            if (!_casting) return Phase.Idle;
            if (hit)
            {
                _casting = false;
                _elapsed = 0f;
                return Phase.Cancelled;
            }
            _elapsed += Mathf.Max(0f, dt);
            if (_elapsed < CastSeconds) return Phase.Casting;
            _casting = false;
            _elapsed = 0f;
            return GameState.Consume(Economy.LifeItem.ScrollOfReturn)
                ? Phase.Escaped
                : Phase.Rejected;
        }

        public static void ResetForTest()
        {
            _casting = false;
            _elapsed = 0f;
        }
    }
}
