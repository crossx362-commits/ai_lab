using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>MobDef.크기의 런타임 소비처. 던전 QA 부제에서 작성된 크기를 증명한다.</summary>
    public static class MobSize
    {
        public const string EnvShow = "QA_MOB_SIZE";
        public const string EnvNo = "QA_NO_MOB_SIZE";
        public const float Default = 2.2f;
        public static MobDef ForceDef;

        static float _cached = -1f;
        static bool _qaSeeded;

        public static bool Blocked => Flag(EnvNo);
        public static bool ShowQa => !Blocked && Flag(EnvShow);

        static bool Flag(string key)
        {
            string raw = Environment.GetEnvironmentVariable(key);
            return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
        }

        public static float Units()
        {
            if (Blocked) return Default;
            if (ForceDef != null) return Valid(ForceDef.크기);
            if (_cached > 0f) return _cached;
            var def = ScriptableObject.CreateInstance<MobDef>();
            _cached = Valid(def != null ? def.크기 : Default);
            if (def != null) UnityEngine.Object.DestroyImmediate(def);
            return _cached;
        }

        static float Valid(float value) => value > 0f ? value : Default;

        public static string Line() => Blocked
            ? ""
            : $"잡몹 크기 ×{Units().ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}";

        public static void SeedQaIfRequested()
        {
            if (!ShowQa || _qaSeeded) return;
            _qaSeeded = true;
            _ = Line();
        }

        public static void ResetForTest()
        {
            ForceDef = null;
            _cached = -1f;
            _qaSeeded = false;
        }
    }
}
