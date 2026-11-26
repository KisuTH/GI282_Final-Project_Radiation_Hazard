using UnityEngine;

namespace RadiationShowcase
{
    /// <summary>
    /// The leaking air vent: glow, particles, fan, and a dose model in REAL units (sievert, real time).
    /// Ambient dose rate = maxRate × (1 − distance/range)^falloffPower × leak   [Sv/h]
    ///   leak = 1 with the fan on, 0.3 with the fan off.
    /// Body dose rate = ambient × protection (suit 0.4, KI pill 0.5).
    /// The vent sits behind the spawn point and the zone grows down the room toward the exit (slow at first,
    /// then faster). A player who keeps moving stays ahead of it; standing still near the spawn is lethal in ~45 s.
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

        [Header("Spreading over time")]
        [Tooltip("Seconds of play before the contaminated zone starts to grow")]
        public float spreadDelay = 5f;
        [Tooltip("Seconds it takes to grow from the starting size to full size")]
        public float spreadDuration = 60f;
        [Tooltip("Starting size of the zone (fraction of range / dangerRadius)")]
        [Range(0.1f, 1f)] public float startSize = 0.4f;

        [Header("Visuals")]
        public ParticleSystem particles;
        public Light glowLight;
        [Tooltip("The green glowing panel inside the vent (dark until the leak starts)")]
        public Renderer ventGlow;
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
        public bool InDanger => Distance < CurrentDangerRadius;

        /// <summary>0..1 how far the zone has spread (startSize .. full). Stops growing once the fan is off.</summary>
        public float SpreadSize { get; private set; } = 1f;
        public bool IsSpreading => fanOn && spreadTime > spreadDelay && SpreadSize < 1f;
        /// <summary>The contamination has started to spread (used to trigger the emergency alarm).</summary>
        public bool SpreadStarted => spreadTime >= spreadDelay;
        public float CurrentRange => range * SpreadSize;
        public float CurrentDangerRadius => dangerRadius * SpreadSize;

        float spreadTime;
        float leakTime;
        bool leakShown;
        Color ventGlowColor;
        Vector3 dangerFloorBaseScale;

        /// <summary>The leak has begun (green glow, particles and radiation all start together, at spreadDelay).</summary>
        public bool LeakActive => leakTime >= spreadDelay;

        float fanSpeed = 720f;
        float baseLightIntensity;
        MaterialPropertyBlock block;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        void Awake()
        {
            baseLightIntensity = glowLight != null ? glowLight.intensity : 1f;
            if (dangerFloor != null) dangerFloorBaseScale = dangerFloor.transform.localScale;
            if (ventGlow != null) ventGlowColor = ventGlow.sharedMaterial.GetColor(EmissionId);
            ShowLeak(false);
            SpreadSize = startSize;
            block = new MaterialPropertyBlock();
            if (listener == null && Camera.main != null) listener = Camera.main.transform;
        }

        /// <summary>Ambient dose rate (Sv/h) at any point in the room — what a Geiger counter there would read.</summary>
        public float AmbientRateAt(Vector3 worldPoint)
        {
            if (!LeakActive) return 0f;   // nothing has leaked yet
            Vector3 a = worldPoint; a.y = 0f;
            Vector3 b = transform.position; b.y = 0f;
            float t = Mathf.Clamp01(1f - Vector3.Distance(a, b) / Mathf.Max(0.01f, CurrentRange));
            return maxRateSvPerHour * Mathf.Pow(t, falloffPower) * (fanOn ? 1f : 0.3f);
        }

        public void ResetState()
        {
            fanOn = true;
            protection = 1f;
            TotalDose = 0f;
            accumulate = true;
            spreadTime = 0f;
            leakTime = 0f;
            SpreadSize = startSize;
            ApplySpreadVisual();
            ShowLeak(false);
            fanSpeed = 720f;
        }

        void Update()
        {
            if (listener == null) return;

            // the leak itself starts at spreadDelay: green glow + particles + radiation together
            if (accumulate) leakTime += Time.deltaTime;
            if (LeakActive != leakShown) ShowLeak(LeakActive);

            // contamination spreads while the fan keeps blowing (game time: frozen in menus / pause)
            if (accumulate && fanOn)
            {
                spreadTime += Time.deltaTime;
                float k = spreadDuration > 0f ? Mathf.Clamp01((spreadTime - spreadDelay) / spreadDuration) : 1f;
                SpreadSize = Mathf.Lerp(startSize, 1f, k * k);   // slow start, then speeds up and chases the player
                ApplySpreadVisual();
            }

            Vector3 a = listener.position; a.y = 0f;
            Vector3 b = transform.position; b.y = 0f;
            Distance = Vector3.Distance(a, b);

            CurrentDoseRate = AmbientRateAt(listener.position) * protection;
            if (accumulate) TotalDose += CurrentDoseRate * Time.deltaTime / 3600f; // real time

            UpdateVisuals();
        }

        void ShowLeak(bool on)
        {
            leakShown = on;
            if (particles != null)
            {
                if (on) particles.Play();
                else particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (glowLight != null) glowLight.enabled = on;
            if (ventGlow != null)
            {
                ventGlow.GetPropertyBlock(block ??= new MaterialPropertyBlock());
                block.SetColor(EmissionId, on ? ventGlowColor : Color.black);
                ventGlow.SetPropertyBlock(block);
            }
        }

        void ApplySpreadVisual()
        {
            if (dangerFloor == null) return;
            var sc = dangerFloorBaseScale;
            dangerFloor.transform.localScale = new Vector3(sc.x * SpreadSize, sc.y, sc.z * SpreadSize);
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
