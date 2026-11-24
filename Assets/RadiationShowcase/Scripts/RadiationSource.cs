using UnityEngine;

namespace RadiationShowcase
{
    /// <summary>
    /// The leaking air vent: glow, particles, fan and a simple dose model.
    /// (The clicking sound comes from the GeigerCounter the player holds.)
    /// Place this object on the floor directly under the vent (distance is measured on the floor plane).
    /// </summary>
    public class RadiationSource : MonoBehaviour
    {
        [Header("Player")]
        public Transform listener;              // the player's head (camera)

        [Header("Zones")]
        [Tooltip("Inside this radius = red danger zone")]
        public float dangerRadius = 2.6f;
        [Tooltip("Radiation reaches this far (Geiger counter starts reacting inside this radius)")]
        public float beepRange = 7f;
        [Tooltip("Dose rate at the vent (µSv/h, for display only)")]
        public float maxDoseRate = 250f;

        [Header("Visuals")]
        public ParticleSystem particles;
        public Light glowLight;
        public Transform fanBlades;
        public Renderer dangerFloor;
        public Color dangerColor = new Color(1f, 0.1f, 0.05f);

        [Header("State (runtime)")]
        public bool fanOn = true;
        [Tooltip("1 = no protection, 0.3 = wearing mask")]
        public float protection = 1f;

        public float Distance { get; private set; }
        public float CurrentDoseRate { get; private set; }
        public float TotalDose { get; private set; }
        public bool InDanger => Distance < dangerRadius;

        float fanSpeed = 720f;
        float baseLightIntensity;
        MaterialPropertyBlock block;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        void Awake()
        {
            baseLightIntensity = glowLight != null ? glowLight.intensity : 1f;
            block = new MaterialPropertyBlock();
            if (listener == null && Camera.main != null) listener = Camera.main.transform;
        }

        public void ResetState()
        {
            fanOn = true;
            protection = 1f;
            TotalDose = 0f;
            fanSpeed = 720f;
            if (particles != null)
            {
                particles.Clear();
                particles.Play();
            }
        }

        void Update()
        {
            if (listener == null) return;

            Vector3 a = listener.position; a.y = 0f;
            Vector3 b = transform.position; b.y = 0f;
            Distance = Vector3.Distance(a, b);

            // --- dose model (simple, for the showcase) ---
            float t = Mathf.Clamp01(1f - Distance / beepRange);
            float leak = fanOn ? 1f : 0.3f;
            CurrentDoseRate = maxDoseRate * t * t * leak * protection;
            TotalDose += CurrentDoseRate * Time.deltaTime / 3600f * 60f; // sped up x60 so numbers move in a 1-2 min demo

            UpdateVisuals();
        }

        void UpdateVisuals()
        {
            float target = fanOn ? 720f : 0f;
            fanSpeed = Mathf.MoveTowards(fanSpeed, target, 400f * Time.deltaTime);
            if (fanBlades != null) fanBlades.Rotate(Vector3.right, fanSpeed * Time.deltaTime, Space.Self);

            if (particles != null)
            {
                var emission = particles.emission;
                emission.rateOverTime = fanOn ? 45f : 6f;
                var main = particles.main;
                main.startSpeed = fanOn ? 1.0f : 0.25f;
            }

            if (glowLight != null)
            {
                float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 4f);
                glowLight.intensity = baseLightIntensity * pulse * (fanOn ? 1f : 0.4f);
            }

            if (dangerFloor != null)
            {
                float pulse = 0.6f + 0.4f * Mathf.Sin(Time.time * (InDanger ? 10f : 3f));
                dangerFloor.GetPropertyBlock(block);
                block.SetColor(EmissionId, dangerColor * pulse * (fanOn ? 1.5f : 0.6f));
                dangerFloor.SetPropertyBlock(block);
            }
        }
    }
}
