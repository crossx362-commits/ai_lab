using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §18-11 잡몹 피해. <see cref="MobDef.피해비율"/>의 런타임 소비처.
    /// 에셋 기본 0.03(최대 HP의 2~4% = 25~50대에 사망)이 authored돼 있으면서도
    /// grep 소비처가 0곳이었다 — 형제 체력배율은 MobHp가 읽는데 피해만 죽어 있었다.
    /// QA_NO면 옛 0.03·줄 없음. 표시 줄 + 읽기. W3Party·전투는 안 건드린다.
    /// </summary>
    public static class MobDmg
    {
        public const string EnvShow = "QA_MOB_DMG";
        public const string EnvNo = "QA_NO_MOB_DMG";
        /// <summary>§18-11 표 한가운데(3%). MobDef 기본값과 같다.</summary>
        public const float Default = 0.03f;

        /// <summary>SelfCheck가 필드 소비를 증명할 때만.</summary>
        public static MobDef ForceDef;

        static float _cached = -1f;
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

        /// <summary>§18-11 앵커. 에셋 기본값 0.03. 차단이면 옛 하드코드 0.03.</summary>
        public static float Ratio()
        {
            if (Blocked) return Default;
            if (ForceDef != null)
                return ClampRaw(ForceDef.피해비율);
            if (_cached > 0f) return _cached;
            _cached = Read();
            return _cached;
        }

        static float Read()
        {
            float raw = Default;
            try
            {
                var d = ScriptableObject.CreateInstance<MobDef>();
                if (d != null && d.피해비율 > 0f)
                    raw = d.피해비율;
                if (d != null)
                    UnityEngine.Object.DestroyImmediate(d);
            }
            catch
            {
                raw = Default;
            }
            return ClampRaw(raw);
        }

        static float ClampRaw(float raw) => raw > 0f ? raw : Default;

        /// <summary>던전 부제·캐릭터 속성 탭. QA_NO면 빈 문자열(옛 화면 = 잡몹 피해 줄 없음).</summary>
        public static string Line()
        {
            if (Blocked) return "";
            return $"잡몹 피해 {Pct(Ratio())}%(§18-11)";
        }

        static string Pct(float v) =>
            (v * 100f).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

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
            _cached = -1f;
            _qaSeeded = false;
        }
    }
}
