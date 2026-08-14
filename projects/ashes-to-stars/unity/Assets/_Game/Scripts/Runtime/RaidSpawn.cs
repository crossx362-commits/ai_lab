using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 레이드급 던전의 필드 출현 (명세 S11 · 기획서 §7).
    ///
    /// ✅ §7: "레이드급으로 강력한 던전이 필드에 **랜덤하게 생성**된다.
    ///        별도의 주간 반복 레이드 시스템은 없다 — 반복 파밍 수요를 이것이 담당한다."
    /// 💡 §7: "출현이 랜덤이라 **떴을 때 잡아야 한다**는 긴장감 — 발견 시 맵에 표시, 일정 시간 후 소멸"
    ///
    /// ⚠️ **소멸 시간은 기획서 미정이다**(던전 명세 §9-9 미결). 여기서는 20분으로 두고
    ///    상수 하나(`LifetimeSec`)로 뽑아 뒀다 — 오너가 값을 정하면 이 줄만 바꾼다.
    ///    그 값이 왜 20분인가: §18-2가 "일반 던전 1판 = 10분"이라 했으므로,
    ///    **한 판을 마치고 돌아와 준비할 시간까지 두 판 분량**이 "떴을 때 잡아야 한다"와
    ///    "준비 없이 들어가 목숨을 잃는다"(§7) 사이의 최소 여유다.
    ///
    /// 저장은 PlayerPrefs다. 껐다 켜도 남은 시간이 이어져야 "한정 이벤트"가 성립한다 —
    /// 재시작으로 리셋되면 그냥 아무 때나 들어가는 던전이 된다(§19 악용 대응과 같은 계열).
    /// </summary>
    public static class RaidSpawn
    {
        /// <summary>출현 후 소멸까지(초). ⚠️ 기획서 미정 — 오너 결정 시 이 값만 바꾼다.</summary>
        public const int LifetimeSec = 20 * 60;

        /// <summary>다음 출현 판정까지의 최소 간격(초). 필드에 들를 때마다 뜨면 긴장감이 없다.</summary>
        const int RollIntervalSec = 15 * 60;

        /// <summary>출현 확률. 한 번 굴릴 때 이만큼.</summary>
        const float SpawnChance = 0.5f;

        const string K_UNTIL = "ats.raid.until";
        const string K_ROLL = "ats.raid.nextroll";
        const string K_SEED = "ats.raid.seed";

        static long Now => System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        /// <summary>지금 필드에 레이드급 던전이 떠 있는가.</summary>
        public static bool Active => RemainingSec > 0;

        public static int RemainingSec
        {
            get
            {
                long until = long.TryParse(PlayerPrefs.GetString(K_UNTIL, "0"), out long v) ? v : 0;
                return (int)Mathf.Max(0, until - Now);
            }
        }

        /// <summary>이 던전의 시드. 떠 있는 동안 **바뀌지 않는다** — 들락거리며 리롤할 수 없어야 한다.</summary>
        public static uint Seed
        {
            get
            {
                uint s = (uint)PlayerPrefs.GetInt(K_SEED, 0);
                return s == 0u ? 1u : s;
            }
        }

        /// <summary>
        /// 필드 화면을 열 때 부른다. 출현 판정은 **간격을 두고** 굴린다.
        /// 이미 떠 있으면 아무것도 하지 않는다(재판정으로 시드가 바뀌면 리롤이 된다).
        /// </summary>
        public static void Tick()
        {
            if (Active) return;

            long nextRoll = long.TryParse(PlayerPrefs.GetString(K_ROLL, "0"), out long v) ? v : 0;
            if (Now < nextRoll) return;

            PlayerPrefs.SetString(K_ROLL, (Now + RollIntervalSec).ToString());

            // 판정 난수는 전역 Random을 쓰지 않는다 — 던전 생성의 결정성을 밖에서 깨지 않기 위해서다(§3-2)
            var rng = new Rng((uint)(Now & 0x7FFFFFFF));
            if (!rng.Chance(SpawnChance)) { PlayerPrefs.Save(); return; }

            PlayerPrefs.SetString(K_UNTIL, (Now + LifetimeSec).ToString());
            PlayerPrefs.SetInt(K_SEED, (int)(rng.NextUInt() & 0x7FFFFFFF));
            PlayerPrefs.Save();
            Debug.Log($"[레이드급] 필드에 출현 — {LifetimeSec / 60}분 후 소멸 (시드 {Seed})");
        }

        /// <summary>들어갔으면 사라진다. 한 번 뜬 것을 반복해서 돌 수 있으면 "한정"이 아니다.</summary>
        public static void Consume()
        {
            PlayerPrefs.DeleteKey(K_UNTIL);
            PlayerPrefs.DeleteKey(K_SEED);
            PlayerPrefs.Save();
        }

        public static string RemainingText()
        {
            int s = RemainingSec;
            return $"{s / 60:D2}:{s % 60:D2}";
        }

        /// <summary>테스트·디버그용 — 즉시 띄운다.</summary>
        public static void ForceSpawnForTest(uint seed)
        {
            PlayerPrefs.SetString(K_UNTIL, (Now + LifetimeSec).ToString());
            PlayerPrefs.SetInt(K_SEED, (int)(seed & 0x7FFFFFFF));
            PlayerPrefs.Save();
        }
    }
}
