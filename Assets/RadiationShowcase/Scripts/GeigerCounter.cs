using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace RadiationShowcase
{
    /// <summary>
    /// Hand-held Geiger counter — the ONLY way to read radiation in the game.
    /// Starts locked inside the glass emergency case; once the glass is broken, grab it (Grip) and hold it.
    /// Display: ambient dose rate at the probe (real units), the player's accumulated body dose, a log-scale
    /// needle and a click LED. Sound: random Poisson clicks generated sample-by-sample, denser near the leak.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class GeigerCounter : Grabbable
    {
        [Header("Geiger")]
        public RadiationSource source;
        [Tooltip("Where the radiation is measured (tip of the probe)")]
        public Transform probeTip;
        [Tooltip("Locked inside the emergency case until the glass breaks")]
        public bool locked = true;
        public string actionId = "geiger";

        [Header("Clicks")]
        [Tooltip("Natural background: a few random clicks even far from the leak")]
        public float backgroundCps = 0.6f;
        [Tooltip("Clicks per second right at the vent (the tube is saturated there)")]
        public float maxCps = 700f;
        [Tooltip("Lower = the click rate climbs earlier as you approach")]
        public float cpsCurve = 0.35f;
        [Range(0f, 1f)] public float volume = 0.8f;

        [Header("Display")]
        public RectTransform needle;
        public Text rateText;
        public Text doseText;
        public Text alarmText;
        public Image led;
        public Color ledOn = new Color(1f, 0.15f, 0.1f);
        public Color ledOff = new Color(0.25f, 0.05f, 0.05f);

        public override bool CanGrab => base.CanGrab && !locked;
        public float MeasuredRate { get; private set; }   // Sv/h shown on the display (smoothed)

        bool pickedUp;

        // shared with the audio thread
        volatile float audioCps;
        int clickCounter;
        // audio-thread state
        System.Random rng;
        int sampleRate;
        float decay, clickEnv, ringPhase, ringStep, clickSign = 1f, vol;

        int lastClicks;
        float ledTimer;

        protected override void Awake()
        {
            base.Awake();
            rng = new System.Random();
            sampleRate = AudioSettings.outputSampleRate;
            decay = Mathf.Exp(-1f / (0.0012f * sampleRate)); // ~1.2 ms click
            vol = volume;

            var src = GetComponent<AudioSource>();
            src.clip = AudioClip.Create("Silence", sampleRate, 1, sampleRate, false);
            src.loop = true;
            src.playOnAwake = true;
            src.spatialBlend = 0.7f;
            src.minDistance = 0.6f;
            src.Play();
        }

        public override void Grab(Transform hand, bool snapToHand)
        {
            base.Grab(hand, snapToHand);
            if (!pickedUp)
            {
                pickedUp = true;
                ShowcaseEvents.Raise(actionId);
            }
        }

        public override void HeldUpdate()
        {
            var rig = ShowcaseRig.Instance;
            if (holder != null && rig != null && !rig.VRActive)
            {
                // desktop: show it at the bottom-right of the view, display tilted toward the camera
                Transform h = rig.head;
                transform.SetPositionAndRotation(h.position + h.forward * 0.42f + h.right * 0.17f - h.up * 0.2f,
                    h.rotation * Quaternion.Euler(-65f, 0f, 0f));
                return;
            }
            base.HeldUpdate();
        }

        public override void ResetItem()
        {
            locked = true;
            pickedUp = false;
            base.ResetItem();
        }

        protected override void Update()
        {
            base.Update();
            // the game has ended (dose frozen for the report): fade the clicks out; back to full on restart
            bool gameOver = source != null && !source.accumulate;
            vol = gameOver ? Mathf.MoveTowards(vol, 0f, volume * Time.deltaTime) : volume;

            float ambient = 0f;
            if (source != null)
                ambient = source.AmbientRateAt(probeTip != null ? probeTip.position : transform.position);

            // click rate: background + a curve that keeps a usable range from "tick... tick" to a saturated buzz
            float k = source != null && source.maxRateSvPerHour > 0f ? Mathf.Clamp01(ambient / source.maxRateSvPerHour) : 0f;
            audioCps = backgroundCps + maxCps * Mathf.Pow(k, cpsCurve);

            // display reading smoothed like a real meter
            MeasuredRate = Mathf.Lerp(MeasuredRate, ambient, 1f - Mathf.Exp(-Time.deltaTime / 0.4f));

            int clicks = Interlocked.CompareExchange(ref clickCounter, 0, 0);
            if (clicks != lastClicks) ledTimer = 0.035f;
            lastClicks = clicks;
            ledTimer -= Time.deltaTime;

            UpdateDisplay();
        }

        void UpdateDisplay()
        {
            float uSvh = MeasuredRate * 1e6f + 0.1f; // background floor ~0.1 µSv/h
            if (needle != null)
            {
                // log scale: 0.1 µSv/h (-60°) ... 10 kSv/h (+60°)
                float n = Mathf.Clamp01((Mathf.Log10(uSvh) + 1f) / 11f);
                needle.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Lerp(-60f, 60f, n));
            }
            if (rateText != null)
            {
                string c = MeasuredRate < 0.0001f ? "#7CFF9A" : MeasuredRate < 0.1f ? "#FFC040" : "#FF4A3A";
                rateText.text = $"<color={c}>{RadiationEffects.Rate(MeasuredRate)}</color>";
            }
            if (doseText != null && source != null)
            {
                Color dc = RadiationEffects.LevelColor(source.TotalDose);
                doseText.text = $"DOSE  <color=#{ColorUtility.ToHtmlStringRGB(dc)}>{RadiationEffects.Dose(source.TotalDose)}</color>";
            }
            if (alarmText != null)
                alarmText.enabled = MeasuredRate > 1f && Mathf.Repeat(Time.time, 0.5f) < 0.3f;
            if (led != null) led.color = ledTimer > 0f ? ledOn : ledOff;
        }

        // ---------------------------------------------------------------- audio thread

        void OnAudioFilterRead(float[] data, int channels)
        {
            double pClick = audioCps / sampleRate;
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
