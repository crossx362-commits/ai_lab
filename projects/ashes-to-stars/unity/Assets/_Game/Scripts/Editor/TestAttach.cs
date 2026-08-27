using System;
using System.Reflection;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 에디터(비플레이·배치)에선 AddComponent가 Awake를 부르지 않는다.
    /// 검증 픽스처가 AddComponent 뒤 수동으로 Awake를 띄우는 공용 경로.
    /// QA_NO_ATTACH_AWAKE=1이면 AddComponent만 하고 Invoke는 건너뛴다(옛 깨진 배치 경로).
    /// W3Party처럼 Awake 전에 필드를 넣어야 하면 beforeAwake 콜백을 쓴다.
    /// </summary>
    public static class TestAttach
    {
        public const string EnvNo = "QA_NO_ATTACH_AWAKE";

        const BindingFlags AwakeFlags =
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static T AttachWithAwake<T>(GameObject go) where T : Component
            => AttachWithAwake<T>(go, null);

        public static T AttachWithAwake<T>(GameObject go, Action<T> beforeAwake) where T : Component
        {
            var comp = go.AddComponent<T>();
            beforeAwake?.Invoke(comp);
            if (!Blocked)
            {
                var awake = typeof(T).GetMethod("Awake", AwakeFlags);
                if (awake != null) awake.Invoke(comp, null);
            }
            return comp;
        }
    }
}
