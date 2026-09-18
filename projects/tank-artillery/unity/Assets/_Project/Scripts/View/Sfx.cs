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
        /// 사람이 끈 것. `Muted` 와 따로 둔다 — `Muted` 는 하네스가 켜는 것이라 사람 설정과 섞이면
        /// "설정에서 껐는데 저장이 안 된다"(하네스 값이 덮어씀)·"하네스가 사람 설정을 읽는다" 둘 다 생긴다.
        /// 저장·복원은 Prefs.cs 가 한다. 이 파일은 값을 볼 뿐 쓰지 않는다.
        /// </summary>
        public static bool SfxOff, MusicOff;

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
            if (Muted || SfxOff || c == null || _pool == null) return;
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
            if (on) { if (!Muted && !SfxOff && !s._loop.isPlaying) s._loop.Play(); }
            else if (s._loop.isPlaying) s._loop.Stop();
        }
        // ══════════════════════════════════════════════════════
        //  배경음악 — 효과음처럼 파형을 코드로 굽는다(파일 없음). 두 트랙뿐이다:
        //    Title  — 느린 패드 + 아르페지오(72bpm, Am-F-C-G). 메뉴에서 "살아 있다"만 알리면 된다.
        //    Battle — 킥 + 베이스 오스티나토 + 드론(104bpm, Em-Em-C-D). 턴 시간이 흐른다는 압박.
        //  ⚠️ 루프 경계에서 딸깍 소리가 나지 않게 **모든 음이 마디 안에서 꺼지도록** 만들었다(감쇠 포락선).
        //     길이를 바꾸면 마디 수의 정수배로만 바꿔라.
        //  ⚠️ 음악은 게임 상태를 **보기만** 한다. `Music()` 은 매 프레임 불러도 같은 트랙이면 아무것도 안 한다.
        // ══════════════════════════════════════════════════════
        public enum Track { None, Title, Battle }

        AudioSource _music;
        AudioClip _titleBgm, _battleBgm;
        Track _track = Track.None;
        const float MusicVolume = 0.30f;     // 효과음 아래에 깔리는 정도. 폭발보다 크면 피드백(효과음)이 묻힌다.

        /// <summary>원하는 트랙을 말한다. 같은 트랙이 이미 울리면 그대로 둔다. None 이면 끈다.</summary>
        public static void Music(Track t)
        {
            var s = I;
            if (s._music == null) { s._music = s.NewSource(); s._music.loop = true; s._music.volume = MusicVolume; }
            bool want = t != Track.None && !Muted && !MusicOff;
            if (!want)
            {
                if (s._music.isPlaying) s._music.Stop();
                s._track = Track.None;
                return;
            }
            if (s._track == t && s._music.isPlaying) return;
            if (t == Track.Title) { if (s._titleBgm == null) s._titleBgm = BuildTitleBgm(); s._music.clip = s._titleBgm; }
            else { if (s._battleBgm == null) s._battleBgm = BuildBattleBgm(); s._music.clip = s._battleBgm; }
            s._track = t;
            s._music.Play();
        }

        /// <summary>마지막으로 **요청이 받아들여진** 트랙. 오디오 장치가 없는 배치 실행에서도 값이 선다(자체검사용).</summary>
        public static Track Current => _inst == null ? Track.None : _inst._track;

        /// <summary>트랙의 파형을 굽는다(자체검사가 길이·진폭·루프 이음새를 잰다). 게임은 Music() 을 쓴다.</summary>
        public static AudioClip BuildClip(Track t) => t == Track.Title ? BuildTitleBgm() : BuildBattleBgm();

        static float Env(float t, float attack, float decay)   // 짧은 어택 뒤 지수 감쇠
            => Mathf.Min(1f, t / Mathf.Max(attack, 1e-4f)) * Mathf.Exp(-t / Mathf.Max(decay, 1e-4f));

        static AudioClip BuildTitleBgm()
        {
            const float bpm = 72f, beat = 60f / bpm, bar = beat * 4f;
            float[][] chords =
            {
                new[] { 220.0f, 261.6f, 329.6f },   // Am
                new[] { 174.6f, 220.0f, 261.6f },   // F
                new[] { 196.0f, 261.6f, 329.6f },   // C (2전위 — 성부가 크게 안 뛰게)
                new[] { 196.0f, 246.9f, 293.7f },   // G
            };
            int n = Mathf.RoundToInt(Rate * bar * chords.Length);
            var d = new float[n];
            float eighth = beat * 0.5f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                int b = Mathf.Min(chords.Length - 1, (int)(t / bar));
                float tb = t - b * bar;
                var ch = chords[b];
                // 패드 — 마디 안에서 올라왔다 내려간다(마디 경계에서 0 → 코드 바뀔 때 딸깍이 없다)
                float padEnv = Mathf.Min(1f, tb / 0.25f) * Mathf.Min(1f, (bar - tb) / 0.35f);
                float pad = 0f;
                for (int k = 0; k < ch.Length; k++)
                    pad += Mathf.Sin(t * ch[k] * 2f * Mathf.PI) + 0.25f * Mathf.Sin(t * ch[k] * 4f * Mathf.PI);
                pad *= padEnv * 0.09f;
                // 아르페지오 — 8분음표마다 코드 톤 한 옥타브 위, 마지막 8분은 쉰다(호흡)
                int e = (int)(tb / eighth);
                float te = tb - e * eighth;
                float arp = 0f;
                if (e < 7)
                {
                    float f = ch[e % 3] * 2f;
                    arp = Mathf.Sin(te * f * 2f * Mathf.PI) * Env(te, 0.004f, 0.22f) * 0.16f;
                }
                d[i] = Mathf.Clamp(pad + arp, -1f, 1f);
            }
            var c = AudioClip.Create("bgm_title", n, 1, Rate, false);
            c.SetData(d, 0);
            return c;
        }

        static AudioClip BuildBattleBgm()
        {
            const float bpm = 104f, beat = 60f / bpm, bar = beat * 4f;
            float[] roots = { 82.4f, 82.4f, 65.4f, 73.4f };            // Em Em C D (E2 · C2 · D2)
            float[][] drone =
            {
                new[] { 164.8f, 196.0f, 246.9f },
                new[] { 164.8f, 196.0f, 246.9f },
                new[] { 130.8f, 164.8f, 196.0f },
                new[] { 146.8f, 185.0f, 220.0f },
            };
            int[] pattern = { 0, 0, 7, 0, 0, 12, 0, 7, 0, 0, 7, 0, 5, 0, 7, 12 };   // 16분음 베이스, 반음 오프셋
            int n = Mathf.RoundToInt(Rate * bar * roots.Length);
            var d = new float[n];
            float sixteenth = beat * 0.25f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                int b = Mathf.Min(roots.Length - 1, (int)(t / bar));
                float tb = t - b * bar;
                // 킥 — 매 박, 120→45Hz 로 떨어지는 사인
                float tk = tb % beat;
                float kick = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(120f, 45f, Mathf.Clamp01(tk * 9f)) * tk) * Env(tk, 0.002f, 0.09f) * 0.42f;
                // 하이햇 — 뒷박(8분 오프비트), 아주 짧은 노이즈
                float th = (tb + beat * 0.5f) % beat;
                float hat = th < 0.04f ? Noise() * Env(th, 0.001f, 0.012f) * 0.10f : 0f;
                // 베이스 — 16분음 오스티나토(마디 마지막 16분은 쉬어 루프 경계를 비운다)
                int s16 = (int)(tb / sixteenth);
                float ts = tb - s16 * sixteenth;
                float bass = 0f;
                if (s16 < 15)
                {
                    float f = roots[b] * Mathf.Pow(2f, pattern[s16] / 12f);
                    bass = (Mathf.Sin(ts * f * 2f * Mathf.PI) + 0.35f * Mathf.Sin(ts * f * 4f * Mathf.PI))
                           * Env(ts, 0.003f, 0.10f) * 0.22f;
                }
                // 드론 — 낮게 깔리는 코드, 마디 경계에서 0
                float droneEnv = Mathf.Min(1f, tb / 0.20f) * Mathf.Min(1f, (bar - tb) / 0.25f);
                float dr = 0f;
                foreach (var f in drone[b]) dr += Mathf.Sin(t * f * 2f * Mathf.PI);
                dr *= droneEnv * 0.05f;
                d[i] = Mathf.Clamp(kick + hat + bass + dr, -1f, 1f);
            }
            var c = AudioClip.Create("bgm_battle", n, 1, Rate, false);
            c.SetData(d, 0);
            return c;
        }
    }
}
