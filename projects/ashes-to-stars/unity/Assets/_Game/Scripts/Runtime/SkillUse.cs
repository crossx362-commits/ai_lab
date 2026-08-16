using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 스킬 자동/수동 사용(오너 2026-08-16 「자동사용중에도 누르면 사용되게」).
    ///
    /// 자동이 기본이다 — 잡몹 구간은 완전 자동(§5). 수동은 누를 때만 나간다.
    /// 자동이 켜져 있어도 누른 슬롯이 이긴다. 쿨다운만 비우는 방식은
    /// 조건이 안 맞으면 아무 반응이 없어서, 슬롯 번호를 직접 세운다.
    /// </summary>
    public static class SkillUse
    {
        public const float DefaultCd = 4f;
        const string PrefsKey = "ats.skill_auto";

        static bool _loaded;
        static bool _auto = true;

        public static bool IsAuto
        {
            get
            {
                if (QaForcedOff()) return false;
                if (QaForcedOn()) return true;
                EnsureLoaded();
                return _auto;
            }
            set
            {
                EnsureLoaded();
                _auto = value;
                PlayerPrefs.SetInt(PrefsKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static string HudLabel => IsAuto ? "스킬 자동" : "스킬 수동";
        public static string HudHint => IsAuto
            ? "쿨이 되면 스스로 쓴다 · 눌러도 나간다"
            : "누를 때만 쓴다";

        /// <summary>
        /// 누른 슬롯이 있으면 그걸 그대로 둔다. 자동이고 쿨이 비었으면 다음 슬롯을 넣는다.
        /// 쿨은 넣자마자 세지 않는다 — 공격 쿨 때문에 이번 프레임에 안 나가면
        /// 빈 쿨만 태우게 된다. 발동된 뒤에 <see cref="SettleAuto"/>를 부른다.
        /// </summary>
        public static int Apply(ref int forceSkill, ref float skillCd, ref int nextSlot)
        {
            int slot = Resolve(IsAuto, forceSkill, skillCd, nextSlot);
            if (slot != 0) forceSkill = slot;
            return slot;
        }

        /// <summary>자동으로 넣은 슬롯이 실제로 나갔을 때만 기본 쿨·다음 슬롯을 확정한다.</summary>
        public static void SettleAuto(ref float skillCd, ref int nextSlot, int armed)
        {
            if (armed != 1 && armed != 2) return;
            if (skillCd <= 0f) skillCd = DefaultCd;
            nextSlot = Flip(armed);
        }

        public static int Resolve(bool auto, int pressed, float skillCd, int nextSlot)
        {
            if (pressed == 1 || pressed == 2) return pressed;
            if (!auto || skillCd > 0f) return 0;
            return nextSlot == 2 ? 2 : 1;
        }

        public static int Flip(int slot) => slot == 1 ? 2 : 1;

        public static void ResetForTest()
        {
            PlayerPrefs.DeleteKey(PrefsKey);
            _loaded = false;
            _auto = true;
        }

        public static void ForgetInMemoryForTest()
        {
            _loaded = false;
        }

        static bool QaForcedOff()
        {
            string raw = System.Environment.GetEnvironmentVariable("QA_NO_SKILL_AUTO");
            return raw == "1" || raw == "true";
        }

        static bool QaForcedOn()
        {
            string raw = System.Environment.GetEnvironmentVariable("QA_SKILL_AUTO");
            return raw == "1" || raw == "true";
        }

        static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            _auto = !PlayerPrefs.HasKey(PrefsKey) || PlayerPrefs.GetInt(PrefsKey, 1) != 0;
        }
    }
}
