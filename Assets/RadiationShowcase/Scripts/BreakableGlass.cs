using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RadiationShowcase
{
    /// <summary>
    /// Glass front of the emergency case. Swing the fire axe into it hard enough and it shatters,
    /// which unlocks the Geiger counter inside.
    /// VR: the axe head must hit the glass faster than breakSpeed (m/s).
    /// Desktop: hold the axe, stand close, aim at the glass and press Space to swing.
    /// </summary>
    public class BreakableGlass : MonoBehaviour
    {
        public Grabbable axe;
        public Transform axeHead;
        public GeigerCounter geiger;
        [Tooltip("The glass pane (renderer + collider are hidden when broken)")]
        public GameObject pane;
        public Material shardMaterial;
        public float breakSpeed = 2.2f;
        public float hitPadding = 0.06f;

        public bool Broken { get; private set; }

        readonly List<GameObject> shards = new List<GameObject>();
        Vector3 prevHeadPos;
        Collider paneCollider;
        AudioClip smashClip, knockClip;
        float knockCooldown;

        void Awake()
        {
            paneCollider = pane.GetComponent<Collider>();
            smashClip = ProceduralAudio.Shatter("GlassSmash");
            knockClip = ProceduralAudio.Notes("GlassKnock", new[] { 1800f, 1400f }, 0.03f, 0.4f);
            if (axeHead != null) prevHeadPos = axeHead.position;
        }

        void Update()
        {
            if (axeHead == null) return;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            Vector3 head = axeHead.position;
            float speed = (head - prevHeadPos).magnitude / dt;
            Vector3 from = prevHeadPos;
            prevHeadPos = head;
            knockCooldown -= Time.deltaTime;

            if (Broken || axe == null || !axe.IsHeld) return;

            var rig = ShowcaseRig.Instance;
            bool desktop = rig != null && !rig.VRActive;

            if (desktop)
            {
                var kb = Keyboard.current;
                if (kb != null && kb.spaceKey.wasPressedThisFrame &&
                    Physics.Raycast(rig.head.position, rig.head.forward, out RaycastHit hit, 1.6f, ~0, QueryTriggerInteraction.Collide) &&
                    hit.collider == paneCollider)
                    Break(hit.point);
                return;
            }

            // VR: did the axe head touch the glass this frame (sweep from last position)?
            Vector3 closest = paneCollider.ClosestPoint(head);
            bool touching = (closest - head).sqrMagnitude < hitPadding * hitPadding;
            if (!touching && Physics.Linecast(from, head, out RaycastHit sweep, ~0, QueryTriggerInteraction.Collide) && sweep.collider == paneCollider)
            {
                touching = true;
                closest = sweep.point;
            }
            if (!touching) return;

            if (speed >= breakSpeed) Break(closest);
            else if (knockCooldown <= 0f)
            {
                AudioSource.PlayClipAtPoint(knockClip, closest, 0.6f);   // too gentle: just a knock
                knockCooldown = 0.25f;
            }
        }

        void Break(Vector3 point)
        {
            Broken = true;
            pane.SetActive(false);
            AudioSource.PlayClipAtPoint(smashClip, point, 1f);
            SpawnShards(point);
            if (geiger != null) geiger.locked = false;
        }

        void SpawnShards(Vector3 point)
        {
            var b = pane.GetComponent<Renderer>().bounds;
            var rnd = new System.Random();
            for (int i = 0; i < 14; i++)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Cube);
                s.name = "GlassShard";
                s.transform.position = new Vector3(
                    Mathf.Lerp(b.min.x, b.max.x, (float)rnd.NextDouble()),
                    Mathf.Lerp(b.min.y, b.max.y, (float)rnd.NextDouble()),
                    Mathf.Lerp(b.min.z, b.max.z, (float)rnd.NextDouble()));
                s.transform.rotation = Random.rotation;
                float size = 0.03f + 0.07f * (float)rnd.NextDouble();
                s.transform.localScale = new Vector3(size, size * 0.8f, 0.004f);
                if (shardMaterial != null) s.GetComponent<Renderer>().sharedMaterial = shardMaterial;
                var rb = s.AddComponent<Rigidbody>();
                rb.mass = 0.05f;
                Vector3 away = (s.transform.position - point).normalized + pane.transform.forward * -0.5f;
                rb.linearVelocity = away * (0.5f + (float)rnd.NextDouble());
                if (ShowcaseRig.Instance != null)
                    Physics.IgnoreCollision(ShowcaseRig.Instance.GetComponent<CharacterController>(), s.GetComponent<Collider>());
                shards.Add(s);
            }
        }

        public void ResetGlass()
        {
            Broken = false;
            pane.SetActive(true);
            foreach (var s in shards) if (s != null) Destroy(s);
            shards.Clear();
            if (axeHead != null) prevHeadPos = axeHead.position;
        }
    }
}
