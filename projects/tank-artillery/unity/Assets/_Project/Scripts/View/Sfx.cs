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

using UnityEngine;

namespace Tankfall.View
{
    public sealed class Sfx : MonoBehaviour
    {
        const int Rate = 44100;

        static Sfx _inst;
        AudioSource _src;
        AudioClip _fire, _boom, _hit, _move, _click, _confirm, _win, _lose;

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
            _src = gameObject.AddComponent<AudioSource>();
            _src.playOnAwake = false;
            _src.spatialBlend = 0f;          // 2D — 카메라가 포탄을 따라다녀서 3D 감쇠가 오히려 헷갈린다

            _fire    = Make("fire",    0.34f, FireWave);
            _boom    = Make("boom",    0.90f, BoomWave);
            _hit     = Make("hit",     0.22f, HitWave);
            _move    = Make("move",    0.18f, MoveWave);
            _click   = Make("click",   0.06f, (t, d) => Tone(t, 660f, d, 0.035f) * 0.35f);
            _confirm = Make("confirm", 0.18f, (t, d) => (Tone(t, 523f, d, 0.12f) + Tone(t, 784f, d, 0.16f)) * 0.30f);
            _win     = Make("win",     0.90f, WinWave);
            _lose    = Make("lose",    0.90f, LoseWave);
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

        void One(AudioClip c, float vol, float pitch)
        {
            if (Muted || c == null || _src == null) return;
            _src.pitch = pitch;
            _src.PlayOneShot(c, Mathf.Clamp01(vol));
        }

        public static void Fire(float power01) => I.One(I._fire, 0.85f, Mathf.Lerp(1.12f, 0.88f, Mathf.Clamp01(power01)));
        public static void Boom(float scale)   => I.One(I._boom, Mathf.Clamp(0.5f + scale * 0.35f, 0.4f, 1f), Mathf.Lerp(1.15f, 0.8f, Mathf.Clamp01(scale)));
        public static void Hit()               => I.One(I._hit, 0.8f, 1f);
        public static void Click()             => I.One(I._click, 0.7f, 1f);
        public static void Confirm()           => I.One(I._confirm, 0.8f, 1f);
        public static void Win()               => I.One(I._win, 0.8f, 1f);
        public static void Lose()              => I.One(I._lose, 0.8f, 1f);

        /// <summary>주행음 — 누르고 있는 동안만. 매 프레임 불러도 겹치지 않게 재생 중이면 그냥 둔다.</summary>
        public static void Engine(bool on)
        {
            var s = I;
            if (Muted) return;
            if (on)
            {
                if (s._src.isPlaying && s._src.clip == s._move) return;
                s._src.clip = s._move; s._src.loop = true; s._src.pitch = 1f; s._src.volume = 0.5f;
                s._src.Play();
            }
            else if (s._src.clip == s._move && s._src.isPlaying)
            {
                s._src.Stop(); s._src.clip = null; s._src.loop = false; s._src.volume = 1f;
            }
        }
    }
}
