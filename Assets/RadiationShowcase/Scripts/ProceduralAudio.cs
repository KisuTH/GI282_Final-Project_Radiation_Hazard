using UnityEngine;

namespace RadiationShowcase
{
    /// <summary>
    /// Makes simple beep / chime sounds in code, so the showcase needs no audio files.
    /// </summary>
    public static class ProceduralAudio
    {
        const int SampleRate = 44100;

        public static AudioClip Beep(string name, float frequency, float duration, float volume = 0.6f)
        {
            return Notes(name, new[] { frequency }, duration, volume);
        }

        /// <summary>Deep explosion: sharp crack + low boom + long rumbling tail.</summary>
        public static AudioClip Explosion(string name, float duration = 3f, float volume = 0.9f)
        {
            int n = Mathf.CeilToInt(SampleRate * duration);
            var data = new float[n];
            var rng = new System.Random(77);
            float low = 0f, low2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                low += (white - low) * 0.02f;           // rumble (heavily low-passed noise)
                low2 += (low - low2) * 0.05f;
                float crack = white * Mathf.Exp(-t * 40f);                               // first 50 ms
                float boom = Mathf.Sin(2f * Mathf.PI * (55f - 25f * t) * t) * Mathf.Exp(-t * 2.5f);
                float rumble = low2 * 18f * Mathf.Exp(-t * 1.1f);
                float attack = Mathf.Clamp01(t / 0.004f);
                data[i] = Mathf.Clamp((crack * 0.6f + boom * 0.9f + rumble) * attack * volume, -1f, 1f);
            }
            var clip = AudioClip.Create(name, n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>One cycle of a wailing siren (rising and falling pitch); loop it for a continuous alarm.</summary>
        public static AudioClip Siren(string name, float cycleSeconds = 2.4f, float lowHz = 600f, float highHz = 1150f, float volume = 0.6f)
        {
            int n = Mathf.CeilToInt(SampleRate * cycleSeconds);
            var data = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;                                    // 0..1 over one cycle (loops seamlessly)
                float f = Mathf.Lerp(lowHz, highHz, 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * t));
                phase += 2.0 * System.Math.PI * f / SampleRate;
                float sine = (float)System.Math.Sin(phase);
                float square = sine >= 0f ? 1f : -1f;
                data[i] = (sine * 0.7f + square * 0.15f) * volume;        // a bit of edge, like a real horn
            }
            var clip = AudioClip.Create(name, n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Bright noise burst with tinkles — a glass-smash sound.</summary>
        public static AudioClip Shatter(string name, float duration = 0.7f, float volume = 0.8f)
        {
            int n = Mathf.CeilToInt(SampleRate * duration);
            var data = new float[n];
            var rng = new System.Random(1234);
            float prev = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                float bright = white - prev;               // crude high-pass = crunchy glass
                prev = white;
                float env = Mathf.Exp(-t * 9f);
                float tinkle = Mathf.Sin(2f * Mathf.PI * (3200f + 900f * Mathf.Sin(t * 37f)) * t) * Mathf.Exp(-t * 4f) * 0.25f;
                data[i] = (bright * env * 0.8f + tinkle * (rng.NextDouble() < 0.02 ? 1f : 0.3f)) * volume;
            }
            var clip = AudioClip.Create(name, n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Plays the given frequencies one after another, each lasting noteDuration.</summary>
        public static AudioClip Notes(string name, float[] frequencies, float noteDuration, float volume = 0.6f)
        {
            int perNote = Mathf.CeilToInt(SampleRate * noteDuration);
            var data = new float[perNote * frequencies.Length];
            int attack = Mathf.Max(1, (int)(SampleRate * 0.004f));
            int release = Mathf.Max(1, (int)(SampleRate * 0.03f));

            for (int n = 0; n < frequencies.Length; n++)
            {
                float phase = 0f;
                float step = 2f * Mathf.PI * frequencies[n] / SampleRate;
                for (int i = 0; i < perNote; i++)
                {
                    float env = Mathf.Min(1f, (float)i / attack) * Mathf.Clamp01((float)(perNote - i) / release);
                    // sine + a little square-ish harmonic for a "device" sound
                    float s = Mathf.Sin(phase) * 0.8f + Mathf.Sin(phase * 3f) * 0.2f;
                    data[n * perNote + i] = s * env * volume;
                    phase += step;
                }
            }

            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
