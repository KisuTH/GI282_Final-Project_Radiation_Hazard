using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RadiationShowcase
{
    /// <summary>
    /// "Blacking out": the more dose the player's body has taken, the darker the edges of the view get,
    /// and colour drains away. Reaches near-black edges at the lethal dose.
    /// Works through the scene's global post-processing Volume (Vignette + Color Adjustments).
    /// </summary>
    public class ExposureEffects : MonoBehaviour
    {
        public Volume volume;
        public RadiationSource radiation;
        [Tooltip("Dose (Sv) at which the view is almost black")]
        public float lethalDose = 4f;
        [Range(0f, 1f)] public float baseVignette = 0.2f;

        Vignette vignette;
        ColorAdjustments colorAdjust;
        float shown;
        bool suppressed;

        /// <summary>Turns the darkening off completely (used while the end message is on screen).</summary>
        public void Suppress(bool on)
        {
            suppressed = on;
            if (on) Apply(0f, 0f);
        }

        void Start()
        {
            if (volume == null) return;
            var profile = volume.profile;   // runtime copy: never edits the saved asset
            profile.TryGet(out vignette);
            profile.TryGet(out colorAdjust);
            if (vignette != null)
            {
                vignette.color.Override(Color.black);
                vignette.smoothness.Override(1f);
                vignette.rounded.Override(true);
            }
        }

        void Update()
        {
            if (radiation == null || suppressed) return;

            // 0 → 1 as the body dose climbs toward lethal (square root so even ~0.1 Sv is just noticeable)
            float k = Mathf.Sqrt(Mathf.Clamp01(radiation.TotalDose / lethalDose));
            shown = Mathf.MoveTowards(shown, k, Time.deltaTime * 0.5f);

            // while a strong dose is coming in, the edges throb a little
            float throb = radiation.CurrentDoseRate > 10f ? 0.05f * Mathf.Sin(Time.time * 6f) : 0f;

            Apply(Mathf.Clamp01(Mathf.Lerp(baseVignette, 1f, shown) + throb * shown), shown);
        }

        void Apply(float vignetteIntensity, float k)
        {
            if (vignette != null) vignette.intensity.Override(vignetteIntensity);
            if (colorAdjust != null)
            {
                colorAdjust.saturation.Override(Mathf.Lerp(0f, -70f, k));
                colorAdjust.postExposure.Override(Mathf.Lerp(0f, -1.5f, k));
            }
        }

        public void ResetEffects()
        {
            shown = 0f;
            suppressed = false;
        }
    }
}
