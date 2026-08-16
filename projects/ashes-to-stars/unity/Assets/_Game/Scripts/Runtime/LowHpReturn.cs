using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 필드 자동사냥의 저체력 자동 귀환(§4·§6·§18-14).
    /// HP 30% 이하면 3초 이탈 후 영지로 돌아간다. 사망 카운트는 안 오르고
    /// 이번 판 보상은 없다. 기본 켜짐. 보스·던전·탑·침략은 안 본다.
    /// </summary>
    public static class LowHpReturn
    {
        public const float Threshold = 0.30f;
        public const float LeaveSeconds = 3f;
        const string PrefsKey = "ats.low_hp_return";

        public enum Phase { Idle, Leaving, Left, Disabled, NotField }

        static bool _leaving;
        static float _elapsed;
        static bool _loaded;
        static bool _enabled = true;

        public static bool Leaving => _leaving;
        public static float Remaining =>
            _leaving ? Mathf.Max(0f, LeaveSeconds - _elapsed) : 0f;

        public static bool Enabled
        {
            get
            {
                if (QaForcedOff()) return false;
                EnsureLoaded();
                return _enabled;
            }
            set
            {
                EnsureLoaded();
                _enabled = value;
                PlayerPrefs.SetInt(PrefsKey, value ? 1 : 0);
                PlayerPrefs.Save();
                if (!value) ResetCast();
            }
        }

        public static bool ShouldWatch(GameFlow.BattleKind kind, string returnTo) =>
            kind == GameFlow.BattleKind.잡몹웨이브 && returnTo == GameFlow.Field;

        /// <summary>
        /// 필드 사냥만 본다. 발동 후 3초는 피격해도 취소되지 않는다(§18-14).
        /// 힐로 비율이 올라가도 이탈은 끝낸다 — 이미 결단한 귀환이다.
        /// </summary>
        public static Phase Tick(float dt, float lowestRatio, bool watch)
        {
            if (!watch)
            {
                ResetCast();
                return Phase.NotField;
            }
            if (!Enabled)
            {
                ResetCast();
                return Phase.Disabled;
            }
            if (!_leaving)
            {
                if (lowestRatio > Threshold) return Phase.Idle;
                _leaving = true;
                _elapsed = 0f;
            }
            _elapsed += Mathf.Max(0f, dt);
            if (_elapsed < LeaveSeconds) return Phase.Leaving;
            ResetCast();
            return Phase.Left;
        }

        public static void ResetForTest()
        {
            PlayerPrefs.DeleteKey(PrefsKey);
            _loaded = false;
            _enabled = true;
            ResetCast();
        }

        /// <summary>저장은 두고 메모리만 잊는다. 재기동 유지를 자가검사가 확인한다.</summary>
        public static void ForgetInMemoryForTest()
        {
            _loaded = false;
            ResetCast();
        }

        static bool QaForcedOff()
        {
            string raw = System.Environment.GetEnvironmentVariable("QA_NO_LOW_HP_RETURN");
            return raw == "1" || raw == "true";
        }

        static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            _enabled = !PlayerPrefs.HasKey(PrefsKey) || PlayerPrefs.GetInt(PrefsKey, 1) != 0;
        }

        static void ResetCast()
        {
            _leaving = false;
            _elapsed = 0f;
        }
    }
}
