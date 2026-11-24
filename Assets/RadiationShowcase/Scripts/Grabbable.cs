using UnityEngine;

namespace RadiationShowcase
{
    /// <summary>
    /// Anything the player can pick up with the Grip button (H3VR style: hold Grip = hold the object).
    /// While held the object follows the hand; on release it keeps the hand's velocity (you can throw it).
    /// </summary>
    public class Grabbable : MonoBehaviour
    {
        [Tooltip("Kinematic (frozen in place) until first grabbed, e.g. a suit hanging on a rack")]
        public bool startKinematic;
        public Renderer[] glowRenderers;
        public Color glowColor = new Color(1f, 0.8f, 0.2f);
        [Tooltip("Pulses to show 'use me now'")]
        public bool highlighted;
        [Tooltip("Optional: where and how the hand holds this item. On grab the item snaps so this point sits in " +
                 "the hand, its blue axis (Z) along the hand's pointing direction and its green axis (Y) up.")]
        public Transform gripPoint;

        protected Rigidbody rb;
        protected Transform holder;
        Vector3 posOffset;
        Quaternion rotOffset;

        Vector3 startPos;
        Quaternion startRot;
        int hoverFrame = -10;
        MaterialPropertyBlock block;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        public bool IsHeld => holder != null;
        public virtual bool CanGrab => isActiveAndEnabled;

        protected virtual void Awake()
        {
            rb = GetComponent<Rigidbody>();
            startPos = transform.position;
            startRot = transform.rotation;
            if (glowRenderers == null || glowRenderers.Length == 0) glowRenderers = GetComponentsInChildren<Renderer>();
            block = new MaterialPropertyBlock();
            if (rb != null) rb.isKinematic = startKinematic;
        }

        public void MarkHovered() => hoverFrame = Time.frameCount;

        /// <param name="snapToHand">true = object jumps into the hand (desktop); false = keep where it was grabbed (VR)</param>
        public virtual void Grab(Transform hand, bool snapToHand)
        {
            holder = hand;
            if (rb != null) rb.isKinematic = true;

            if (gripPoint != null)
            {
                // always held the right way round: the grip point lines up with the hand
                Quaternion gripLocalRot = Quaternion.Inverse(transform.rotation) * gripPoint.rotation;
                Vector3 gripLocalPos = Quaternion.Inverse(transform.rotation) * (gripPoint.position - transform.position);
                rotOffset = Quaternion.Inverse(gripLocalRot);
                posOffset = -(rotOffset * gripLocalPos);
                return;
            }

            posOffset = snapToHand ? Vector3.zero : hand.InverseTransformPoint(transform.position);
            rotOffset = Quaternion.Inverse(hand.rotation) * transform.rotation;
        }

        /// <summary>Called every frame while held.</summary>
        public virtual void HeldUpdate()
        {
            if (holder == null) return;
            transform.SetPositionAndRotation(holder.TransformPoint(posOffset), holder.rotation * rotOffset);
        }

        public virtual void Release(Vector3 velocity, Vector3 angularVelocity)
        {
            holder = null;
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.linearVelocity = velocity;
                rb.angularVelocity = angularVelocity;
            }
        }

        /// <summary>Can this be pulled to the hand from a distance (Trigger + Grip)? Fixed things like levers can't.</summary>
        public virtual bool CanForcePull => CanGrab && !IsHeld;

        /// <summary>Freeze physics while the item flies to the hand.</summary>
        public void BeginPull()
        {
            if (rb == null) return;
            if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            rb.isKinematic = true;
        }

        /// <summary>Pull cancelled mid-air: let it fall again.</summary>
        public void CancelPull()
        {
            if (rb != null && holder == null) rb.isKinematic = false;
        }

        public virtual void ResetItem()
        {
            holder = null;
            gameObject.SetActive(true);
            if (rb != null)
            {
                // stop it, then move the PHYSICS body too (an interpolated Rigidbody would drag the
                // transform back to its old spot if only the transform were moved)
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                rb.isKinematic = true;
                rb.position = startPos;
                rb.rotation = startRot;
            }
            transform.SetPositionAndRotation(startPos, startRot);
            if (rb != null) rb.isKinematic = startKinematic;
        }

        protected virtual void Update()
        {
            if (glowRenderers == null) return;
            float strength = 0f;
            if (highlighted && !IsHeld) strength = 0.25f + 0.25f * Mathf.Sin(Time.time * 5f);
            if (Time.frameCount - hoverFrame <= 1 && !IsHeld) strength = 0.9f;

            foreach (var r in glowRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(EmissionId, glowColor * strength);
                r.SetPropertyBlock(block);
            }
        }

        protected void PlaySound(AudioClip clip, float volume = 0.8f)
        {
            if (clip != null) AudioSource.PlayClipAtPoint(clip, transform.position, volume);
        }
    }
}
