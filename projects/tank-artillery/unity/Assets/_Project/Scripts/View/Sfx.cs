// 명세: docs/GAME_SPEC_TANK_ARTILLERY.md — 소리는 명세에 항목이 없다. 없어서 안 만든 게 아니라
// **한 번도 다룬 적이 없는 축**이라, 여기서는 규칙을 발명하지 않고 "무엇이 일어났는지 귀로 알린다"만 한다.
//
// 왜 코드로 만드나: 이 프로젝트는 지형(§7)·탱크(§9)·UI(Ui.cs)를 전부 코드로 그린다.
// 오디오 파일을 들이면 라이선스·용량·플랫폼이 거기서만 갈린다. `AudioClip.Create` 로 파형을 직접 굽는다.
//
// ⚠️ 소리는 **게임 규칙이 아니라 피드백**이다. 여기서 판정·타이밍을 만들지 마라 —
//    발사 여부는 Sim 이 정하고 이 파일은 그 결과를 재생만 한다(§4-1 SIM/VIEW 분리와 같은 이유).
//
// ⚠️ 볼륨을 키우지 마라. 폭발이 겹치면 클리핑으로 찢어진다(다탄두는 한 프레임에 여러 발이 터진다).
//    2026-09-17: 겹침은 이제 `Duck()`(1/√n)이 받아 준다 — 그래도 **개별 파형의 진폭**은 그대로 둬라.
//    Duck 은 동시 발수만 보정하지 한 발이 애초에 큰 것은 못 막는다.

using UnityEngine;

namespace Tankfall.View
{
    public sealed class Sfx : MonoBehaviour
    {
        const int Rate = 44100;

        static Sfx _inst;
        AudioSource[] _pool;      // 원샷 — 소리마다 제 피치를 갖는다
        int _next;
        AudioSource _loop;        // 주행음 전용
        AudioClip _fire, _hit, _move, _click, _confirm, _win, _lose;
        AudioClip[] _boom;        // 폭발 3변주 — 다탄두는 한 프레임에 9발이 터진다

        public static Sfx I
        {
            get
            {
                if (_inst == null)
                {
                    var go = new GameObject("[Tankfall Sfx]");
                    DontDestroyOnLoad(go);
                    _inst = go.AddComponent<Sfx>();
                    _inst.Build();
                }
                return _inst;
            }
        }

        void Build()
        {
            AudioListener.volume = Volume;   // 저장된 음량을 시작하자마자 적용 — 설정 화면을 안 열어도 반영돼야 한다

            _pool = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++) _pool[i] = NewSource();

            _fire    = Make("fire",    0.34f, FireWave);
            _boom    = new[] { Make("boom0", 0.90f, BoomWave), Make("boom1", 0.90f, BoomWave), Make("boom2", 0.90f, BoomWave) };
            _hit     = Make("hit",     0.22f, HitWave);
            _move    = Make("move",    0.18f, MoveWave);
            _click   = Make("click",   0.06f, (t, d) => Tone(t, 660f, d, 0.035f) * 0.35f);
            _confirm = Make("confirm", 0.18f, (t, d) => (Tone(t, 523f, d, 0.12f) + Tone(t, 784f, d, 0.16f)) * 0.30f);
            _win     = Make("win",     0.90f, WinWave);
            _lose    = Make("lose",    0.90f, LoseWave);

            _loop = NewSource();
            _loop.clip = _move; _loop.loop = true; _loop.volume = 0.5f;
        }

        AudioSource NewSource()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;             // 2D — 카메라가 포탄을 따라다녀서 3D 감쇠가 오히려 헷갈린다
            return s;
        }

        delegate float Wave(float t, float dur);

        static AudioClip Make(string name, float dur, Wave w)
        {
            int n = Mathf.RoundToInt(Rate * dur);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(w(i / (float)Rate, dur), -1f, 1f);
            var c = AudioClip.Create(name, n, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }

        // ── 파형 ────────────────────────────────────────────────
        // 난수는 `Rng` 가 아니라 여기 전용 LCG 를 쓴다 — 소리가 게임의 결정론(§4-1)에 끼어들면 안 된다.
        static uint _seed = 0x51F7A3u;
        static float Noise()
        {
            _seed = _seed * 1664525u + 1013904223u;
            return ((_seed >> 9) / (float)(1 << 23)) * 2f - 1f;
        }

        static float Tone(float t, float hz, float dur, float decay)
            => Mathf.Sin(t * hz * 2f * Mathf.PI) * Mathf.Exp(-t / Mathf.Max(decay, 1e-4f)) * (t < dur ? 1f : 0f);

        /// <summary>발사 — 짧은 파열 + 아래로 떨어지는 저음. "쿵" 하고 밀려나는 느낌.</summary>
        static float FireWave(float t, float dur)
        {
            float env = Mathf.Exp(-t * 14f);
            float body = Mathf.Sin(t * Mathf.Lerp(190f, 60f, Mathf.Clamp01(t * 6f)) * 2f * Mathf.PI);
            return (body * 0.55f + Noise() * 0.45f * Mathf.Exp(-t * 26f)) * env * 0.55f;
        }

        /// <summary>폭발 — 넓은 노이즈가 천천히 꺼지고 저음이 남는다. 발사보다 길어야 "터졌다"로 읽힌다.</summary>
        static float BoomWave(float t, float dur)
        {
            float env = Mathf.Exp(-t * 4.2f);
            float rumble = Mathf.Sin(t * Mathf.Lerp(90f, 32f, Mathf.Clamp01(t * 1.6f)) * 2f * Mathf.PI);
            return (Noise() * 0.62f + rumble * 0.52f) * env * 0.60f;
        }

        /// <summary>직격 — 금속을 때린 소리. 폭발과 구별돼야 "맞혔다"가 귀로 온다.</summary>
        static float HitWave(float t, float dur)
            => (Tone(t, 1180f, dur, 0.035f) * 0.5f + Tone(t, 1760f, dur, 0.022f) * 0.3f + Noise() * 0.25f * Mathf.Exp(-t * 40f)) * 0.5f;

        /// <summary>주행 — 낮게 웅웅거리는 엔진. 이동 중에만 반복 재생한다.</summary>
        static float MoveWave(float t, float dur)
            => (Mathf.Sin(t * 74f * 2f * Mathf.PI) * 0.5f + Noise() * 0.30f) * 0.22f
               * Mathf.Min(1f, t * 20f) * Mathf.Min(1f, (dur - t) * 20f);   // 양끝 페이드 — 반복 재생 시 딸깍 소리 방지

        static float WinWave(float t, float dur)
        {
            float[] hz = { 523f, 659f, 784f, 1047f };
            int i = Mathf.Clamp(Mathf.FloorToInt(t / 0.16f), 0, hz.Length - 1);
            return Mathf.Sin(t * hz[i] * 2f * Mathf.PI) * Mathf.Exp(-(t - i * 0.16f) * 6f) * 0.30f;
        }

        static float LoseWave(float t, float dur)
        {
            float[] hz = { 523f, 440f, 349f, 262f };
            int i = Mathf.Clamp(Mathf.FloorToInt(t / 0.18f), 0, hz.Length - 1);
            return Mathf.Sin(t * hz[i] * 2f * Mathf.PI) * Mathf.Exp(-(t - i * 0.18f) * 5f) * 0.30f;
        }

        // ── 재생 ────────────────────────────────────────────────
        /// <summary>자동 검증·배치 실행에서는 소리를 내지 않는다 — 하네스가 오디오 장치에 기대면 기계마다 결과가 갈린다.</summary>
        public static bool Muted;

        /// <summary>
        /// 플레이어가 설정 화면(§3 Settings)에서 조절하는 음량. `Muted` 와는 별개다 — `Muted` 는
        /// 하네스가 켜는 것이고, 이건 사람이 켠다. `AudioListener.volume` 하나로 걸어 두면 원샷·주행음
        /// 전부를 한 번에 스케일할 수 있어 재생 경로마다 곱해 줄 필요가 없다.
        /// </summary>
        const string VolumeKey = "tankfall_volume";
        static float? _volume;
        public static float Volume
        {
            get => _volume ??= PlayerPrefs.GetFloat(VolumeKey, 1f);
            set
            {
                _volume = Mathf.Clamp01(value);
                AudioListener.volume = _volume.Value;
                PlayerPrefs.SetFloat(VolumeKey, _volume.Value);
            }
        }

        // ⚠️ **전부를 AudioSource 하나로 재생하면 안 된다**(2026-09-17 수정). 유니티에서 `pitch` 와
        //    `Stop()` 은 **클립이 아니라 소스**에 걸리므로 한 채널에 섞으면 서로를 망가뜨린다:
        //      · `pitch` 를 바꾸면 **이미 울리고 있던 PlayOneShot 까지** 같이 조가 바뀐다.
        //        다탄두는 한 프레임에 9발이 터지므로 마지막 발의 피치가 앞의 8발을 통째로 끌고 갔다.
        //      · 주행음을 끄는 `Stop()` 은 그 소스의 **원샷까지 전부 끊는다** —
        //        걷다가 지뢰를 밟으면 폭발음이 중간에 잘렸다.
        //    그래서 원샷은 보이스를 나눠 각자 피치를 갖게 하고, 주행 루프는 전용 소스에 둔다.
        const int Voices = 8;

        /// <summary>안 울리는 보이스를 우선 쓴다 — 울리는 보이스를 재사용하면 그 소리의 피치가 바뀐다.</summary>
        AudioSource Take()
        {
            for (int i = 0; i < Voices; i++)
            {
                var s = _pool[(_next + i) % Voices];
                if (!s.isPlaying) { _next = (_next + i + 1) % Voices; return s; }
            }
            var oldest = _pool[_next]; _next = (_next + 1) % Voices; return oldest;   // 전부 울리는 중
        }

        /// <summary>
        /// 동시에 울리는 수가 늘수록 한 발씩 낮춘다(에너지 보존 1/√n).
        /// 이게 없으면 다탄두 9발이 그대로 합쳐져 클리핑으로 찢어진다 —
        /// 그동안은 **모든 소리의 볼륨을 낮게 유지해서** 피하고 있었다(이 파일 머리말의 경고).
        /// </summary>
        float Duck()
        {
            int on = 0;
            for (int i = 0; i < Voices; i++) if (_pool[i].isPlaying) on++;
            return 1f / Mathf.Sqrt(1f + on);      // 첫 발 100% · 네 발째 50% · 여덟 발째 33%
        }

        void One(AudioClip c, float vol, float pitch)
        {
            if (Muted || c == null || _pool == null) return;
            float duck = Duck();
            var s = Take();
            s.pitch = pitch;
            s.PlayOneShot(c, Mathf.Clamp01(vol) * duck);
        }

        /// <summary>같은 클립이 같은 피치로 겹치면 한 발처럼 뭉친다 — 발마다 살짝 흔든다.</summary>
        static float Jitter(float amount) => 1f + Noise() * amount;

        public static void Fire(float power01) => I.One(I._fire, 0.85f, Mathf.Lerp(1.12f, 0.88f, Mathf.Clamp01(power01)) * Jitter(0.03f));

        public static void Boom(float scale)
        {
            var s = I;
            var clip = s._boom[(int)((Noise() * 0.5f + 0.5f) * s._boom.Length) % s._boom.Length];
            s.One(clip, Mathf.Clamp(0.5f + scale * 0.35f, 0.4f, 1f), Mathf.Lerp(1.15f, 0.8f, Mathf.Clamp01(scale)) * Jitter(0.06f));
        }

        public static void Hit()               => I.One(I._hit, 0.8f, Jitter(0.05f));
        public static void Click()             => I.One(I._click, 0.7f, 1f);
        public static void Confirm()           => I.One(I._confirm, 0.8f, 1f);
        public static void Win()               => I.One(I._win, 0.8f, 1f);
        public static void Lose()              => I.One(I._lose, 0.8f, 1f);

        /// <summary>주행음 — 누르고 있는 동안만. 매 프레임 불러도 겹치지 않게 재생 중이면 그냥 둔다.</summary>
        public static void Engine(bool on)
        {
            var s = I;
            if (s._loop == null) return;
            // ⚠️ `Muted` 는 켜는 쪽에서만 본다. 예전엔 함수 첫 줄에서 걸러서, 주행 중에 음소거가 켜지면
            //    끄는 호출이 통째로 무시돼 루프가 영원히 남았다.
            if (on) { if (!Muted && !s._loop.isPlaying) s._loop.Play(); }
            else if (s._loop.isPlaying) s._loop.Stop();
        }
    }
}
