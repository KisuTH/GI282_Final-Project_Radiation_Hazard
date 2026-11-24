using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace RadiationShowcase
{
    /// <summary>
    /// Hand-held Geiger counter.
    /// Sound: real Geiger tubes click at RANDOM times (a Poisson process), not in a steady rhythm.
    /// The click rate (CPS = counts per second) rises sharply as the probe gets closer to the source:
    /// far away = a lone "tick... tick", close = a dense crackling buzz.
    /// Clicks are generated sample-by-sample in OnAudioFilterRead, so even hundreds per second sound right.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class GeigerCounter : MonoBehaviour
    {
        [Header("References")]
        public RadiationSource source;
        public ShowcaseRig rig;
        [Tooltip("Where the radiation is measured (tip of the probe)")]
        public Transform probeTip;

        [Header("Counting")]
        [Tooltip("Natural background radiation: a few random clicks even far away")]
        public float backgroundCps = 0.7f;
        [Tooltip("Clicks per second right at the vent")]
        public float maxCps = 450f;
        [Tooltip("Higher = rate stays low longer, then shoots up near the source")]
        public float falloffPower = 3f;
        [Range(0f, 1f)] public float volume = 0.8f;

        [Header("Display")]
        public RectTransform needle;
        public Text readout;
        public Image led;
        public Color ledOn = new Color(1f, 0.15f, 0.1f);
        public Color ledOff = new Color(0.25f, 0.05f, 0.05f);

        [Header("Holding pose")]
        public Vector3 handOffset = new Vector3(0f, 0.035f, -0.11f); // strapped to the back of the left wrist
        public Vector3 handEuler = new Vector3(0f, 0f, 0f);

        public float TrueCps { get; private set; }
        public float MeasuredCps { get; private set; }

        // shared with the audio thread
        volatile float audioCps;
        int clickCounter;

        // audio-thread state
        System.Random rng;
        int sampleRate;
        float decay;
        float clickEnv, ringPhase, ringStep, clickSign = 1f;
        float vol;

        int lastClicks;
        float ledTimer;

        void Awake()
        {
            rng = new System.Random();
            sampleRate = AudioSettings.outputSampleRate;
            decay = Mathf.Exp(-1f / (0.0012f * sampleRate)); // ~1.2 ms click
            vol = volume;

            // a silent looping clip keeps the AudioSource running so OnAudioFilterRead can add the clicks
            var src = GetComponent<AudioSource>();
            src.clip = AudioClip.Create("Silence", sampleRate, 1, sampleRate, false);
            src.loop = true;
            src.playOnAwake = true;
            src.spatialBlend = 0.5f;
            src.minDistance = 0.5f;
            src.Play();
        }

        void OnEnable() => Application.onBeforeRender += UpdatePose;
        void OnDisable() => Application.onBeforeRender -= UpdatePose;

        void Update()
        {
            vol = volume;

            // --- true count rate at the probe ---
            Vector3 probe = probeTip != null ? probeTip.position : transform.position;
            float cps = backgroundCps;
            if (source != null)
            {
                Vector3 a = probe; a.y = 0f;
                Vector3 b = source.transform.position; b.y = 0f;
                float t = Mathf.Clamp01(1f - Vector3.Distance(a, b) / source.beepRange);
                float leak = source.fanOn ? 1f : 0.3f;
                cps += maxCps * Mathf.Pow(t, falloffPower) * leak;
            }
            TrueCps = cps;
            audioCps = cps;

            // --- measured rate from the clicks actually heard (so the needle jitters like a real meter) ---
            int clicks = Interlocked.CompareExchange(ref clickCounter, 0, 0);
            int newClicks = clicks - lastClicks;
            lastClicks = clicks;
            if (Time.deltaTime > 0f)
            {
                float instant = newClicks / Time.deltaTime;
                MeasuredCps = Mathf.Lerp(MeasuredCps, instant, 1f - Mathf.Exp(-Time.deltaTime / 0.5f));
            }
            if (newClicks > 0) ledTimer = 0.035f;
            ledTimer -= Time.deltaTime;

            UpdateDisplay();
        }

        void LateUpdate() => UpdatePose();

        void UpdateDisplay()
        {
            if (needle != null)
            {
                // log scale: 0 -> 1000 CPS mapped to -60°..+60°
                float k = Mathf.Clamp01(Mathf.Log10(MeasuredCps + 1f) / 3f);
                float angle = Mathf.Lerp(-60f, 60f, k);
                needle.localRotation = Quaternion.Euler(0f, 0f, -angle);
            }
            if (readout != null)
            {
                string color = MeasuredCps > 100f ? "#FF4A3A" : MeasuredCps > 20f ? "#FFC040" : "#7CFF9A";
                readout.text = $"<color={color}>{MeasuredCps:0}</color> <size=60>CPS</size>";
            }
            if (led != null) led.color = ledTimer > 0f ? ledOn : ledOff;
        }

        void UpdatePose()
        {
            if (rig == null) return;
            if (rig.VRActive)
            {
                // strapped to the left wrist (hands stay free for grabbing)
                transform.SetPositionAndRotation(rig.leftHand.TransformPoint(handOffset),
                    rig.leftHand.rotation * Quaternion.Euler(handEuler));
            }
            else
            {
                // desktop: bottom-right of the view, display tilted toward the camera
                Transform h = rig.head;
                transform.SetPositionAndRotation(h.position + h.forward * 0.42f + h.right * 0.17f - h.up * 0.2f,
                    h.rotation * Quaternion.Euler(-65f, 0f, 0f));
            }
        }

        // ---------------------------------------------------------------- audio thread

        void OnAudioFilterRead(float[] data, int channels)
        {
            double pClick = audioCps / sampleRate; // chance of a click in each sample
            for (int i = 0; i < data.Length; i += channels)
            {
                if (rng.NextDouble() < pClick)
                {
                    clickEnv = 0.6f + 0.4f * (float)rng.NextDouble();
                    clickSign = rng.Next(2) == 0 ? 1f : -1f;
                    ringPhase = 0f;
                    ringStep = 2f * Mathf.PI * (2500f + rng.Next(2500)) / sampleRate;
                    Interlocked.Increment(ref clickCounter);
                }

                float s = 0f;
                if (clickEnv > 0.0005f)
                {
                    s = clickEnv * (clickSign * 0.55f + 0.45f * Mathf.Sin(ringPhase));
                    ringPhase += ringStep;
                    clickEnv *= decay;
                }

                s *= vol;
                for (int c = 0; c < channels; c++) data[i + c] += s;
            }
        }
    }
}
