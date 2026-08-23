using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 전투 오디오 문법(§16-10). 영구 손실과 직결되는 소리는 다른 효과음에 묻히지 않는다.
    /// 위험 신호(장판 발동·보스 격노·마지막 목숨 진입·긴급 탈출 캐스팅·소멸)는 전용 고순위
    /// 채널로 즉시 재생해 일반 징글(레벨업·일반 사망)에 묻히지 않게 한다.
    /// 위험 경고음은 별도 음량(DangerVol). 클립은 art/gen_sfx_grammar.py가 합성한
    /// mono 22050Hz WAV 7종(Resources/sfx/). QA_NO_SFX면 옛 무음(네거티브 컨트롤).
    /// </summary>
    public static class Sfx
    {
        public const string EnvShow = "QA_SFX";
        public const string EnvNo = "QA_NO_SFX";
        public const int SampleRate = 22050;

        /// <summary>재생 신호. 배열 순서와 Clips·Danger 인덱스가 1:1로 맞는다.</summary>
        public enum Signal
        {
            DangerZone,
            BossEnrage,
            LastLifeEnter,
            EscapeCast,
            LastLifeGone,
            LevelUp,
            DeathLow,
        }

        static readonly string[] ClipNames =
        {
            "sfx/danger_zone",
            "sfx/boss_enrage",
            "sfx/last_life_enter",
            "sfx/escape_cast",
            "sfx/last_life_gone",
            "sfx/level_up",
            "sfx/death_low",
        };

        static readonly bool[] DangerFlag =
        {
            true,   // DangerZone      위험 장판 발동
            true,   // BossEnrage      보스 격노
            true,   // LastLifeEnter   마지막 목숨 진입
            true,   // EscapeCast      긴급 탈출 캐스팅
            true,   // LastLifeGone    소멸(영구 손실)
            false,  // LevelUp         레벨업 징글
            false,  // DeathLow        일반 사망 저음
        };

        static readonly string[] Labels =
        {
            "위험 장판", "보스 격노", "마지막 목숨 진입", "탈출 캐스팅",
            "소멸 신호", "레벨업", "일반 사망",
        };

        /// <summary>전체 음량(§16-10 필수 옵션 슬라이스).</summary>
        public static float MasterVol = 1f;
        /// <summary>위험 경고음 별도 음량(§16-10) — 일반 징글과 독립이다.</summary>
        public static float DangerVol = 1f;
        /// <summary>일반 효과음 음량.</summary>
        public static float NormalVol = 1f;

        static AudioSource _dangerSrc;
        static AudioSource _normalSrc;
        static Transform _root;

        static AudioClip[] _clips;

        // --- QA 관측점(SelfCheck·플레이모드 검증이 읽는다) ---
        public static Signal LastSignal { get; private set; } = (Signal)(-1);
        public static int DangerPlays { get; private set; }
        public static int NormalPlays { get; private set; }
        public static bool LastWasPlaying { get; private set; }

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
                return raw == "1" || raw == "true";
            }
        }

        public static bool IsDanger(Signal s) => DangerFlag[(int)s];
        public static string LabelOf(Signal s) => Labels[(int)s];
        public static string ClipName(Signal s) => ClipNames[(int)s];

        public static string Line()
        {
            if (Blocked) return "전투 오디오 차단(§16-10)";
            return $"위험 신호 4종+소멸 · 징글 2종 · 위험 볼륨 별도(§16-10)";
        }

        /// <summary>신호 재생. 위험 신호는 일반 채널을 밀어내고 전용 채널에서 즉시 낸다.</summary>
        public static void Play(Signal signal)
        {
            if (Blocked) return;
            int i = (int)signal;
            if (i < 0 || i >= ClipNames.Length) return;

            EnsureSources();
            var clip = LoadClip(i);
            LastSignal = signal;
            if (clip == null) { LastWasPlaying = false; return; }

            if (IsDanger(signal))
            {
                _dangerSrc.volume = Mathf.Clamp01(MasterVol * DangerVol);
                _dangerSrc.clip = clip;
                _dangerSrc.Play();          // 동일 프레임 최우선 — 재생 중인 위험 신호조차 덮는다
                DangerPlays++;
                LastWasPlaying = _dangerSrc.isPlaying;
            }
            else
            {
                // 일반 징글은 위험 신호와 다른 채널이라 묻히지 않고, 놓친 위험 신호도 안 가린다.
                if (_normalSrc.isPlaying) return;
                _normalSrc.volume = Mathf.Clamp01(MasterVol * NormalVol);
                _normalSrc.clip = clip;
                _normalSrc.Play();
                NormalPlays++;
                LastWasPlaying = _normalSrc.isPlaying;
            }
        }

        static AudioClip LoadClip(int i)
        {
            if (_clips == null) _clips = new AudioClip[ClipNames.Length];
            if (_clips[i] == null)
                _clips[i] = Resources.Load<AudioClip>(ClipNames[i]);
            return _clips[i];
        }

        /// <summary>SelfCheck가 캐시 없이 로드 가능을 볼 때 쓴다.</summary>
        public static AudioClip PeekClip(Signal signal) =>
            Resources.Load<AudioClip>(ClipNames[(int)signal]);

        static void EnsureSources()
        {
            if (_dangerSrc != null && _normalSrc != null && _root != null) return;

            if (_root == null)
            {
                var go = new GameObject("_SfxGrammar");
                _root = go.transform;
                // 씬에 리스너가 없으면 여기서 하나 둔다 — 기존 씬·카메라는 건드리지 않는다.
                if (UnityEngine.Object.FindObjectOfType<AudioListener>() == null)
                    go.AddComponent<AudioListener>();
            }
            if (_dangerSrc == null)
            {
                _dangerSrc = _root.gameObject.AddComponent<AudioSource>();
                _dangerSrc.playOnAwake = false;
                _dangerSrc.priority = 0;    // 최우선
            }
            if (_normalSrc == null)
            {
                _normalSrc = _root.gameObject.AddComponent<AudioSource>();
                _normalSrc.playOnAwake = false;
                _normalSrc.priority = 128;
            }
        }

        public static void ResetForTest()
        {
            LastSignal = (Signal)(-1);
            DangerPlays = 0;
            NormalPlays = 0;
            LastWasPlaying = false;
            _clips = null;
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root.gameObject);
                _root = null;
                _dangerSrc = null;
                _normalSrc = null;
            }
        }
    }
}
