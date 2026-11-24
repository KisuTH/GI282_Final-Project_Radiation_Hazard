using UnityEngine;

namespace RadiationShowcase
{
    /// <summary>
    /// Wall lever: grab the knob with Grip and pull it DOWN. The lever turns around its pivot and follows
    /// your hand; past offAngle it clunks into the OFF position and stays there.
    /// If you let go before that, it springs back up.
    /// Put this on the knob (which has a collider); the knob is a child of the pivot.
    /// </summary>
    public class LeverHandle : Grabbable
    {
        public Transform pivot;
        public string actionId = "fan";
        public float maxAngle = 150f;
        public float offAngle = 125f;
        public float springBackSpeed = 300f;

        public bool IsOff { get; private set; }

        Quaternion restLocal;
        float angle;
        float prevHandAngle;
        AudioClip clunkClip, tickClip;

        protected override void Awake()
        {
            base.Awake();
            restLocal = pivot.localRotation;
            clunkClip = ProceduralAudio.Notes("Clunk", new[] { 140f, 90f }, 0.07f, 0.9f);
            tickClip = ProceduralAudio.Beep("Tick", 900f, 0.015f, 0.3f);
        }

        // angle of a world point around the pivot: 0 = straight up, 90 = toward the player, 180 = down
        float AngleOf(Vector3 worldPoint)
        {
            Quaternion baseRot = pivot.parent.rotation * restLocal;
            Vector3 axis = baseRot * Vector3.right;
            Vector3 up = baseRot * Vector3.up;
            Vector3 fwd = baseRot * Vector3.forward;
            Vector3 v = worldPoint - pivot.position;
            v -= axis * Vector3.Dot(v, axis);
            return Mathf.Atan2(Vector3.Dot(v, fwd), Vector3.Dot(v, up)) * Mathf.Rad2Deg;
        }

        public override void Grab(Transform hand, bool snapToHand)
        {
            holder = hand;
            prevHandAngle = AngleOf(hand.position);
            PlaySound(tickClip);
        }

        public override void HeldUpdate()
        {
            if (holder == null) return;
            float cur = AngleOf(holder.position);
            angle = Mathf.Clamp(angle + Mathf.DeltaAngle(prevHandAngle, cur), 0f, maxAngle);
            prevHandAngle = cur;
            Apply();

            if (!IsOff && angle >= offAngle)
            {
                IsOff = true;
                PlaySound(clunkClip, 1f);
                ShowcaseEvents.Raise(actionId);
            }
        }

        public override void Release(Vector3 velocity, Vector3 angularVelocity)
        {
            holder = null;
        }

        protected override void Update()
        {
            base.Update();
            if (holder == null && !IsOff && angle > 0f)
            {
                angle = Mathf.MoveTowards(angle, 0f, springBackSpeed * Time.deltaTime);
                Apply();
            }
            if (IsOff && holder == null && angle < offAngle)
            {
                angle = Mathf.MoveTowards(angle, maxAngle, springBackSpeed * Time.deltaTime);
                Apply();
            }
        }

        void Apply() => pivot.localRotation = restLocal * Quaternion.Euler(angle, 0f, 0f);

        public override void ResetItem()
        {
            holder = null;
            IsOff = false;
            angle = 0f;
            Apply();
        }
    }
}
