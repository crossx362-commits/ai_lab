using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>§10-2 회피 가능한 원거리 잡몹 투사체. MobDef.탄속의 런타임 소비처.</summary>
    public static class MobProjectileSpeed
    {
        public const string EnvShow = "QA_MOB_PROJECTILE_SPEED";
        public const string EnvNo = "QA_NO_MOB_PROJECTILE_SPEED";
        public const float Default = 5.5f;
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
            if (ForceDef != null) return Valid(ForceDef.탄속);
            if (_cached > 0f) return _cached;
            var def = ScriptableObject.CreateInstance<MobDef>();
            _cached = Valid(def != null ? def.탄속 : Default);
            if (def != null) UnityEngine.Object.DestroyImmediate(def);
            return _cached;
        }

        static float Valid(float value) => value > 0f ? value : Default;

        public static string Line() => Blocked
            ? ""
            : $"원거리 탄속 {Units().ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}u/s(§10-2)";

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
