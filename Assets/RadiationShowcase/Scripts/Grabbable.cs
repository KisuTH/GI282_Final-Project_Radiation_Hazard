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

        public virtual void ResetItem()
        {
            holder = null;
            gameObject.SetActive(true);
            transform.SetPositionAndRotation(startPos, startRot);
            if (rb != null)
            {
                rb.isKinematic = true; // stop velocity first
                rb.isKinematic = startKinematic;
            }
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
