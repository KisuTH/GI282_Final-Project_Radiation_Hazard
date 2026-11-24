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
