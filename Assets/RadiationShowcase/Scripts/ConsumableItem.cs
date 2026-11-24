using UnityEngine;

namespace RadiationShowcase
{
    /// <summary>Radiation blocker pills: grab the bottle and bring it up to your mouth to take it.</summary>
    public class ConsumableItem : Grabbable
    {
        public string actionId = "pill";
        [Tooltip("How close (m) the bottle must be to the mouth")]
        public float mouthDistance = 0.1f;

        public bool Consumed { get; private set; }
        public override bool CanGrab => base.CanGrab && !Consumed;

        Collider col;
        AudioClip gulpClip;

        protected override void Awake()
        {
            base.Awake();
            col = GetComponent<Collider>();
            gulpClip = ProceduralAudio.Notes("Gulp", new[] { 220f, 160f, 120f }, 0.08f, 0.6f);
        }

        public override void HeldUpdate()
        {
            base.HeldUpdate();
            var rig = ShowcaseRig.Instance;
            if (Consumed || rig == null || col == null) return;
            Vector3 mouth = rig.MouthPoint;
            if (Vector3.Distance(col.ClosestPoint(mouth), mouth) < mouthDistance) Consume();
        }

        void Consume()
        {
            Consumed = true;
            PlaySound(gulpClip, 1f);
            ShowcaseEvents.Raise(actionId);
            gameObject.SetActive(false);
        }

        public override void ResetItem()
        {
            Consumed = false;
            base.ResetItem();
        }
    }
}
