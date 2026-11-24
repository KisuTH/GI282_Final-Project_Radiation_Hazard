using UnityEngine;

namespace RadiationShowcase
{
    /// <summary>
    /// The leaking air vent: glow, particles, fan, and a dose model in REAL units (sievert, real time).
    /// Ambient dose rate = maxRate × (1 − distance/range)^falloffPower × leak   [Sv/h]
    ///   leak = 1 with the fan on, 0.3 with the fan off.
    /// Body dose rate = ambient × protection (suit 0.4, KI pill 0.5).
    /// With the defaults an unprotected player standing at the exit door gets ~720 Sv/h (~0.2 Sv every second),
    /// enough for a lethal 4 Sv in about 20 s; a player who follows the guide ends around 0.05–0.15 Sv.
    /// Place this object on the floor directly under the vent (distance is measured on the floor plane).
    /// </summary>
    public class RadiationSource : MonoBehaviour
    {
        [Header("Player")]
        public Transform listener;              // the player's head (camera)

        [Header("Leak strength (real units)")]
        [Tooltip("Ambient dose rate right at the vent, in Sv/h")]
        public float maxRateSvPerHour = 7660f;
        [Tooltip("Radiation reaches this far (m)")]
        public float range = 7f;
        [Tooltip("Higher = drops off faster with distance")]
        public float falloffPower = 6f;
        [Tooltip("Inside this radius = red danger zone (visual)")]
        public float dangerRadius = 2.6f;

        [Header("Visuals")]
        public ParticleSystem particles;
        public Light glowLight;
        public Transform fanBlades;
        public Renderer dangerFloor;
        public Color dangerColor = new Color(1f, 0.1f, 0.05f);

        [Header("State (runtime)")]
        public bool fanOn = true;
        [Tooltip("1 = no protection; suit and pill multiply this down")]
        public float protection = 1f;
        [Tooltip("Stops dose counting (e.g. once the game has ended)")]
        public bool accumulate = true;

        public float Distance { get; private set; }
        /// <summary>Dose rate the player's body receives now (Sv/h, after protection).</summary>
        public float CurrentDoseRate { get; private set; }
        /// <summary>Total dose the player's body has received (Sv).</summary>
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

        /// <summary>Ambient dose rate (Sv/h) at any point in the room — what a Geiger counter there would read.</summary>
        public float AmbientRateAt(Vector3 worldPoint)
        {
            Vector3 a = worldPoint; a.y = 0f;
            Vector3 b = transform.position; b.y = 0f;
            float t = Mathf.Clamp01(1f - Vector3.Distance(a, b) / range);
            return maxRateSvPerHour * Mathf.Pow(t, falloffPower) * (fanOn ? 1f : 0.3f);
        }

        public void ResetState()
        {
            fanOn = true;
            protection = 1f;
            TotalDose = 0f;
            accumulate = true;
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

            CurrentDoseRate = AmbientRateAt(listener.position) * protection;
            if (accumulate) TotalDose += CurrentDoseRate * Time.deltaTime / 3600f; // real time

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
