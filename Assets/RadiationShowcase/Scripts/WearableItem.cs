using UnityEngine;

namespace RadiationShowcase
{
    /// <summary>Radiation suit: grab it and pull it against your body (chest) to put it on.</summary>
    public class WearableItem : Grabbable
    {
        public string actionId = "suit";
        [Tooltip("How close (m) the suit must touch the chest")]
        public float wearDistance = 0.15f;

        public bool Worn { get; private set; }
        public override bool CanGrab => base.CanGrab && !Worn;

        Collider col;
        AudioClip zipClip;

        protected override void Awake()
        {
            base.Awake();
            col = GetComponent<Collider>();
            zipClip = ProceduralAudio.Notes("Zip", new[] { 300f, 420f, 560f, 700f, 900f }, 0.05f, 0.5f);
        }

        public override void HeldUpdate()
        {
            base.HeldUpdate();
            var rig = ShowcaseRig.Instance;
            if (Worn || rig == null || col == null) return;
            Vector3 chest = rig.ChestPoint;
            if (Vector3.Distance(col.ClosestPoint(chest), chest) < wearDistance) Wear();
        }

        void Wear()
        {
            Worn = true;
            PlaySound(zipClip);
            ShowcaseEvents.Raise(actionId);
            gameObject.SetActive(false);  // the hand notices and lets go
        }

        public override void ResetItem()
        {
            Worn = false;
            base.ResetItem();
        }
    }
}
