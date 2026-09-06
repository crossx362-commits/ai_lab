using System;
using System.IO;
using UnityEngine;
using Ulon.Client;

namespace Ulon.Editor
{
    /// <summary>
    /// §11.2 사운드 게이트 — 검수는 소리를 들을 수 없다. 그래서 「AudioSource가 있다」가 아니라
    /// **파형을 실제로 재서** ① 소리가 무음이 아닌지(RMS) ② 3종이 서로 **다른 길이·다른 음높이**인지를 본다.
    /// 배선은 세 결과 지점(타격·회복·제작)에서 실제로 호출되는지 소스로 확인하고,
    /// **듣는 귀**(AudioListener)가 씬에 있는지도 본다 — 리스너가 없으면 울려도 안 들린다.
    /// **실제 청취 판단은 오너 몫이다**(클립은 코드 합성 자리채움이다).
    /// </summary>
    public static partial class SliceSelfCheck
    {
        const float SfxRmsMin = 0.02f;              // 이보다 작으면 사실상 무음
        const float SfxPitchRatioMin = 1.35f;       // 음높이 비(높은 쪽/낮은 쪽)
        const float SfxLengthRatioMin = 1.30f;      // 길이 비
        // 음색 축 — 실측 3종의 최소 차이 1.33배(Hit·Heal)와 결함 상태(같은 클립 두 자리) 1.00배 사이.
        const float SfxCentroidRatioMin = 1.15f;
        // 음색의 **양쪽 한계** — 아래는 웅웅거리는 저음, 위는 쉿 소리만 남는 소리.
        const float SfxCentroidHzMin = 200f;
        const float SfxCentroidHzMax = 8000f;
        /// <summary>세 축(길이·거칠기·음색) 중 몇 개가 달라야 「구분된다」로 볼 것인가.
        /// 한 축만 재면 결함이 나머지로 빠져나간다 — 이 프로젝트가 반복해서 당한 실패다(검수).</summary>
        const int SfxAxesRequired = 2;

        struct SfxMeasure
        {
            public float Seconds;
            public float Rms;
            public float Hz;        // 영교차 — 녹음에서는 음높이가 아니라 「거칠기」에 가깝다
            public float Centroid;  // 스펙트럴 센트로이드(Hz) — 음색 축. 길이 축 하나로 버티지 않게 한다
        }

        static void AssertActionSfxDistinct()
        {
            var kinds = new[] { ActionSfx.Kind.Hit, ActionSfx.Kind.Heal, ActionSfx.Kind.Craft };
            var m = new SfxMeasure[kinds.Length];
            for (int i = 0; i < kinds.Length; i++)
            {
                // **실제로 울리는 클립**을 잰다 — 사양만 재면 배선이 폴백으로 내려가도 통과한다.
                if (ActionSfx.PackClip(kinds[i]) == null)
                    throw new InvalidOperationException("등록 CC0 클립이 씬에 없습니다: " + ActionSfx.ObjectFor(kinds[i]) +
                        " — 합성 폴백으로 내려간 채 통과시키지 않는다.");
                m[i] = MeasureClip(ActionSfx.Clip(kinds[i]));
                Debug.Log("[Ulon] SFX " + kinds[i] + " — " + m[i].Seconds.ToString("0.00") + "초, RMS " +
                          m[i].Rms.ToString("0.000") + ", 영교차 " + m[i].Hz.ToString("F0") + "Hz, 음색 " +
                          m[i].Centroid.ToString("F0") + "Hz");
                if (m[i].Centroid < SfxCentroidHzMin || m[i].Centroid > SfxCentroidHzMax)
                    throw new InvalidOperationException("SFX " + kinds[i] + "의 음색이 " + m[i].Centroid.ToString("F0") +
                        "Hz입니다 — 허용 " + SfxCentroidHzMin + "~" + SfxCentroidHzMax + "Hz(아래는 웅웅거림, 위는 쉿 소리).");
                if (m[i].Rms < SfxRmsMin)
                    throw new InvalidOperationException("SFX " + kinds[i] + "가 사실상 무음입니다(RMS " +
                        m[i].Rms.ToString("0.000") + " < " + SfxRmsMin + ").");
            }
            for (int a = 0; a < kinds.Length; a++)
                for (int b = a + 1; b < kinds.Length; b++)
                    AssertSfxPairDiffers(kinds[a].ToString(), kinds[b].ToString(), m[a], m[b]);

            AssertSfxWiring();
            ExportSfxWavs();
            Debug.Log("[Ulon] 행동 SFX 3종 — 길이·음높이가 서로 다르고 세 결과 지점에 배선됨. " +
                      "**실제 청취는 오너 확인 필요**(검수는 소리를 들을 수 없다).");
        }

        /// <summary>
        /// 오너가 **실제로 들어 볼 수 있게** 세 소리를 `builds/qa/sfx/*.wav`로 내보낸다 —
        /// 검수도 나도 소리를 판단할 수 없으니, 판단할 수 있는 사람에게 파일을 남기는 것이 유일한 정직한 증거다.
        /// </summary>
        static void ExportSfxWavs()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../builds/qa/sfx"));
            Directory.CreateDirectory(dir);
            foreach (ActionSfx.Kind kind in Enum.GetValues(typeof(ActionSfx.Kind)))
            {
                var clip = ActionSfx.Clip(kind);
                var data = new float[clip.samples * clip.channels];
                clip.GetData(data, 0);
                File.WriteAllBytes(Path.Combine(dir, "sfx_" + kind.ToString().ToLowerInvariant() + ".wav"),
                    Wav(data, clip.frequency * clip.channels));
            }
            Debug.Log("[Ulon] SFX wav 3개 내보냄(오너 청취용) — " + dir);
        }

        static byte[] Wav(float[] data, int rate)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                int bytes = data.Length * 2;
                w.Write(new char[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + bytes);
                w.Write(new char[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                w.Write(16); w.Write((short)1); w.Write((short)1);
                w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
                w.Write(new char[] { 'd', 'a', 't', 'a' });
                w.Write(bytes);
                for (int i = 0; i < data.Length; i++)
                    w.Write((short)(Mathf.Clamp(data[i], -1f, 1f) * short.MaxValue));
                w.Flush();
                return ms.ToArray();
            }
        }

        static void AssertSfxPairDiffers(string a, string b, SfxMeasure x, SfxMeasure y)
        {
            float pitch = Ratio(x.Hz, y.Hz);
            float length = Ratio(x.Seconds, y.Seconds);
            float timbre = Ratio(x.Centroid, y.Centroid);
            int axes = (pitch >= SfxPitchRatioMin ? 1 : 0)
                     + (length >= SfxLengthRatioMin ? 1 : 0)
                     + (timbre >= SfxCentroidRatioMin ? 1 : 0);
            if (axes < SfxAxesRequired)
                throw new InvalidOperationException("SFX " + a + "·" + b + "가 귀에서 구분되지 않습니다 — 다른 축 " +
                    axes + "개(하한 " + SfxAxesRequired + "): 길이 비 " + length.ToString("0.00") +
                    "/" + SfxLengthRatioMin + ", 거칠기 비 " + pitch.ToString("0.00") +
                    "/" + SfxPitchRatioMin + ", 음색 비 " + timbre.ToString("0.00") + "/" + SfxCentroidRatioMin);
        }

        static float Ratio(float a, float b)
        {
            float lo = Mathf.Min(a, b), hi = Mathf.Max(a, b);
            return lo < 1e-6f ? float.MaxValue : hi / lo;
        }

        /// <summary>세 결과 지점에서 실제로 울리는가 — 소리는 화면에 안 남으므로 배선을 소스로 확인한다.</summary>
        static void AssertSfxWiring()
        {
            RequireSfxCall("Client/LocalAvatar.cs", "ActionSfx.Kind.Hit");
            RequireSfxCall("Client/LocalAvatar.cs", "ActionSfx.Kind.Craft");
            RequireSfxCall("Client/SliceHud.cs", "ActionSfx.Kind.Heal");
            RequireSfxCall("Client/SliceHud.cs", "ActionSfx.Kind.Craft");
            if (UnityEngine.Object.FindAnyObjectByType<AudioListener>() == null)
                throw new InvalidOperationException("씬에 AudioListener가 없습니다 — SFX를 울려도 아무도 못 듣습니다.");
        }

        static void RequireSfxCall(string relPath, string kind)
        {
            string path = Path.Combine(Application.dataPath, "Game/Scripts", relPath);
            if (!File.Exists(path))
                throw new InvalidOperationException("소스를 찾을 수 없습니다: " + relPath);
            string src = File.ReadAllText(path);
            if (src.IndexOf("ActionSfx.Play(" + kind, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException(relPath + "에 " + kind + " 소리 배선이 없습니다 — " +
                    "효과가 있어도 행동이 울리지 않으면 없는 것이다.");
        }

        static SfxMeasure MeasureSfx(string name, ActionSfx.Spec spec)
        {
            var clip = ActionSfx.Synth(name, spec);
            var m = MeasureClip(clip);
            UnityEngine.Object.DestroyImmediate(clip);
            return m;
        }

        static SfxMeasure MeasureClip(AudioClip clip)
        {
            var data = new float[clip.samples * clip.channels];
            if (!clip.GetData(data, 0))
                throw new InvalidOperationException("클립 샘플을 읽지 못했습니다: " + clip.name +
                    " (임포트 설정이 Decompress On Load인지 확인)");
            double sum = 0.0;
            int crossings = 0;
            for (int i = 0; i < data.Length; i++)
            {
                sum += data[i] * data[i];
                if (i > 0 && ((data[i - 1] < 0f && data[i] >= 0f) || (data[i - 1] >= 0f && data[i] < 0f)))
                    crossings++;
            }
            return new SfxMeasure
            {
                Seconds = clip.length,
                Rms = (float)Math.Sqrt(sum / Math.Max(1, data.Length)),
                Hz = clip.length > 0f ? crossings * 0.5f / (clip.length * Mathf.Max(1, clip.channels)) : 0f,
                Centroid = Centroid(data, clip.frequency * Mathf.Max(1, clip.channels)),
            };
        }

        /// <summary>
        /// 스펙트럴 센트로이드 — 소리의 「밝기」다. 로그 간격 대역의 에너지를 괴르첼로 재서 무게중심을 낸다
        /// (FFT 없이 몇 개 대역만 재면 충분하다). 길이 축 하나로 버티던 판정에 **음색 축**을 세우기 위한 것.
        /// </summary>
        static float Centroid(float[] data, int rate)
        {
            const int Bands = 24;
            double num = 0.0, den = 0.0;
            for (int b = 0; b < Bands; b++)
            {
                float hz = 100f * Mathf.Pow(10f, 2f * b / (float)(Bands - 1));      // 100Hz~10kHz 로그 간격
                double w = 2.0 * Math.PI * hz / rate;
                double coeff = 2.0 * Math.Cos(w), s1 = 0.0, s2 = 0.0;
                for (int i = 0; i < data.Length; i++)
                {
                    double s0 = data[i] + coeff * s1 - s2;
                    s2 = s1; s1 = s0;
                }
                double power = s1 * s1 + s2 * s2 - coeff * s1 * s2;
                if (power < 0.0) power = 0.0;
                double mag = Math.Sqrt(power);
                num += mag * hz;
                den += mag;
            }
            return den > 1e-9 ? (float)(num / den) : 0f;
        }

        /// <summary>
        /// 네거티브 컨트롤 — 두 소리를 **실제로 같은 사양**으로 만들면 빨간불이어야 하고,
        /// 볼륨을 0으로 만들면 무음으로 걸려야 한다.
        /// </summary>
        static void AssertActionSfxNegativeControl()
        {
            var hit = ActionSfx.SpecFor(ActionSfx.Kind.Hit);
            var same = MeasureSfx("Hit", hit);
            bool red = false;
            try { AssertSfxPairDiffers("Hit", "Heal(위조)", same, same); }
            catch (Exception e) { red = true; Debug.Log("[Ulon] SFX 네거티브 컨트롤(같은 소리) 빨간불 — " + e.Message); }
            if (!red)
                throw new InvalidOperationException("SFX 네거티브 컨트롤 실패 — 같은 사양 두 개가 「다르다」로 통과했습니다.");

            var mute = hit;
            mute.Volume = 0f;
            var silent = MeasureSfx("Hit", mute);
            if (silent.Rms >= SfxRmsMin)
                throw new InvalidOperationException("SFX 네거티브 컨트롤 실패 — 볼륨 0인 소리의 RMS가 " +
                    silent.Rms.ToString("0.000") + "입니다(무음 검출이 작동하지 않습니다).");
            Debug.Log("[Ulon] SFX 네거티브 컨트롤(무음) 빨간불 — RMS " + silent.Rms.ToString("0.000"));
        }
    }
}
