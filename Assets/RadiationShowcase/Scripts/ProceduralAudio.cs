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
