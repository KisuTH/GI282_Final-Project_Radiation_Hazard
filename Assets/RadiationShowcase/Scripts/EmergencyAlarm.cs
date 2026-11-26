using UnityEngine;

namespace RadiationShowcase
{
    /// <summary>
    /// Emergency alert: rotating red beacons + a wailing siren, switched on 'alarmDelay' seconds after the game
    /// starts (after the explosion). Turning the fan off (leak contained) silences the siren; the beacons keep
    /// turning until the game ends or restarts.
    /// </summary>
    public class EmergencyAlarm : MonoBehaviour
    {
        public RadiationSource radiation;
        public GameFlow flow;
        [Tooltip("Seconds after the game starts when the siren and beacons switch on")]
        public float alarmDelay = 4f;
        [Tooltip("Rotating parts of the beacons (each holds the spot lights)")]
        public Transform[] rotors;
        public Light[] beamLights;
        [Tooltip("Red domes that glow while the alarm is on")]
        public Renderer[] domes;
        public float rotateSpeed = 320f;
        [Range(0f, 1f)] public float sirenVolume = 0.35f;
        public Color domeGlow = new Color(4f, 0.25f, 0.15f);

        AudioSource siren;
        bool lightsOn;
        MaterialPropertyBlock block;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        void Awake()
        {
            block = new MaterialPropertyBlock();
            siren = gameObject.AddComponent<AudioSource>();
            siren.clip = ProceduralAudio.Siren("Siren");
            siren.loop = true;
            siren.playOnAwake = false;
            siren.spatialBlend = 0.3f;
            siren.volume = 0f;
            SetLights(false);
        }

        void Update()
        {
            if (radiation == null || flow == null) return;
            bool alarm = flow.Elapsed >= alarmDelay && !flow.IsFinished;
            bool sirenOn = alarm && radiation.fanOn;

            if (alarm != lightsOn) SetLights(alarm);
            if (alarm)
                foreach (var r in rotors)
                    if (r != null) r.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.Self);

            // siren fades in/out smoothly
            float target = sirenOn ? sirenVolume : 0f;
            siren.volume = Mathf.MoveTowards(siren.volume, target, Time.unscaledDeltaTime * 0.8f);
            if (siren.volume > 0f && !siren.isPlaying) siren.Play();
            if (siren.volume <= 0f && siren.isPlaying) siren.Stop();
        }

        void SetLights(bool on)
        {
            lightsOn = on;
            if (beamLights != null)
                foreach (var l in beamLights) if (l != null) l.enabled = on;
            if (domes != null)
                foreach (var d in domes)
                {
                    if (d == null) continue;
                    d.GetPropertyBlock(block);
                    block.SetColor(EmissionId, on ? domeGlow : Color.black);
                    d.SetPropertyBlock(block);
                }
        }
    }
}
