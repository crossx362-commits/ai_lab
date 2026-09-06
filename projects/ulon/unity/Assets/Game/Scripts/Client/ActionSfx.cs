using UnityEngine;

namespace Ulon.Client
{
    /// <summary>
    /// §11.2 사운드 — 행동 결과가 **귀에도** 남게 한다(구멍 표 ③: 사운드 0).
    ///
    /// **클립 출처**: 저장소에 등록된 CC0 오디오가 하나도 없다(Kenney Particle Pack은 이미지 전용).
    /// 그래서 클립을 받아오지 않고 **코드로 합성한다** — VFX에서 코드 생성 텍스처를 허용한 것과 같은 선이며,
    /// 라이선스 대상이 되는 외부 파일이 생기지 않는다. 소리의 **질**은 자리를 채운 수준이고,
    /// 실제로 들어 보고 판단하는 것은 오너 몫이다(검수는 소리를 들을 수 없다).
    ///
    /// 종류마다 **길이와 음높이가 달라야** 한다 — 같은 삑 소리 세 개는 귀에서 한 가지로 뭉친다.
    /// </summary>
    public static class ActionSfx
    {
        public enum Kind { Hit, Heal, Craft }

        public struct Spec
        {
            public float Seconds;
            public float FromHz;     // 시작 음높이
            public float ToHz;       // 끝 음높이 — 타격은 떨어지고 회복은 오른다
            public float Noise;      // 잡음 비율 — 타격의 「퍽」
            public float Volume;
        }

        public static Spec SpecFor(Kind kind)
        {
            switch (kind)
            {
                case Kind.Heal:  return new Spec { Seconds = 0.45f, FromHz = 520f, ToHz = 780f, Noise = 0.00f, Volume = 0.55f };
                case Kind.Craft: return new Spec { Seconds = 0.30f, FromHz = 990f, ToHz = 990f, Noise = 0.05f, Volume = 0.60f };
                default:         return new Spec { Seconds = 0.16f, FromHz = 220f, ToHz = 90f,  Noise = 0.45f, Volume = 0.70f };
            }
        }

        const int SampleRate = 44100;
        static readonly AudioClip[] Cache = new AudioClip[3];

        public static AudioClip Clip(Kind kind)
        {
            int i = (int)kind;
            if (Cache[i] == null)
                Cache[i] = Synth(kind.ToString(), SpecFor(kind));
            return Cache[i];
        }

        /// <summary>사양대로 파형을 만든다 — 게이트도 같은 함수로 만든 클립을 잰다.</summary>
        public static AudioClip Synth(string name, Spec spec)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(spec.Seconds * SampleRate));
            var data = new float[count];
            var rng = new System.Random(name.GetHashCode());
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                float hz = Mathf.Lerp(spec.FromHz, spec.ToHz, t);
                phase += 2f * Mathf.PI * hz / SampleRate;
                float tone = Mathf.Sin(phase);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float env = Mathf.Pow(1f - t, 2f);          // 때리고 잦아든다
                data[i] = Mathf.Clamp(Mathf.Lerp(tone, noise, spec.Noise) * env * spec.Volume, -1f, 1f);
            }
            var clip = AudioClip.Create("Sfx" + name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>그 자리에서 한 번 울린다. 3D로 두어 거리에 따라 줄어든다.</summary>
        public static void Play(Kind kind, Vector3 position)
        {
            var clip = Clip(kind);
            if (clip == null)
                return;
            var go = new GameObject("SfxOneShot" + kind);
            go.transform.position = position;
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.spatialBlend = 1f;
            src.minDistance = 3f;
            src.maxDistance = 30f;
            src.Play();
            Object.Destroy(go, clip.length + 0.1f);
        }
    }
}
