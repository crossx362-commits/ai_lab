using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §18-11 잡몹 이동 속도. <see cref="MobDef.속도배율"/>의 런타임 소비처.
    /// 에셋이 추적 0.90 / 포위 0.85 / 원거리 0.65로 authored돼 있으면서도
    /// grep 소비처가 0곳이었다 — W2Arena·W3Party가 같은 숫자를 하드코딩했다.
    /// QA_NO면 옛 표·줄 없음. 표시 줄 + 읽기. W3Party·W2 손맛은 안 건드린다.
    /// </summary>
    public static class MobSpeed
    {
        public const string EnvShow = "QA_MOB_SPEED";
        public const string EnvNo = "QA_NO_MOB_SPEED";

        /// <summary>§18-11 추적형. MobDef 기본값과 같다.</summary>
        public const float Chaser = 0.90f;
        /// <summary>§18-11 포위형.</summary>
        public const float Surround = 0.85f;
        /// <summary>§18-11 원거리형.</summary>
        public const float Ranged = 0.65f;
        /// <summary>돌진형은 표에 없고 추적과 같다(ProjectSetup).</summary>
        public const float Charge = 0.90f;

        /// <summary>SelfCheck가 필드 소비를 증명할 때만.</summary>
        public static MobDef ForceDef;

        static bool _qaSeeded;

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool ShowQa
        {
            get
            {
                if (Blocked) return false;
                string raw = Environment.GetEnvironmentVariable(EnvShow);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>§18-11 표. 차단이면 옛 하드코드(같은 숫자).</summary>
        public static float Of(MobAi ai)
        {
            if (Blocked) return DefaultOf(ai);
            if (ForceDef != null && ForceDef.AI == ai && ForceDef.속도배율 > 0f)
                return ForceDef.속도배율;
            if (ai == MobAi.추적)
                return ReadChaser();
            return DefaultOf(ai);
        }

        static float ReadChaser()
        {
            float raw = Chaser;
            try
            {
                var d = ScriptableObject.CreateInstance<MobDef>();
                if (d != null && d.속도배율 > 0f)
                    raw = d.속도배율;
                if (d != null)
                    UnityEngine.Object.DestroyImmediate(d);
            }
            catch
            {
                raw = Chaser;
            }
            return raw > 0f ? raw : Chaser;
        }

        public static float DefaultOf(MobAi ai) => ai switch
        {
            MobAi.포위 => Surround,
            MobAi.원거리 => Ranged,
            MobAi.돌진 => Charge,
            _ => Chaser,
        };

        /// <summary>던전 부제·캐릭터 속성 탭. QA_NO면 빈 문자열(옛 화면 = 잡몹 이속 줄 없음).</summary>
        public static string Line()
        {
            if (Blocked) return "";
            return $"잡몹 이속 추적×{Fmt(Of(MobAi.추적))} · 포위×{Fmt(Of(MobAi.포위))} · 원거리×{Fmt(Of(MobAi.원거리))}(§18-11)";
        }

        static string Fmt(float v) =>
            v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

        public static void SeedQaIfRequested()
        {
            if (!ShowQa) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            _ = Line();
        }

        public static void ResetForTest()
        {
            ForceDef = null;
            _qaSeeded = false;
        }
    }
}
