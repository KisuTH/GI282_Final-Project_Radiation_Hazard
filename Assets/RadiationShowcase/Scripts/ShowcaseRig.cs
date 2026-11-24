using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace RadiationShowcase
{
    /// <summary>
    /// Player rig with H3VR-style "Twinstick" locomotion and physical grabbing.
    ///
    /// VR (Quest Touch):
    ///   Left stick           = walk (direction follows where you look)
    ///   Left stick click     = toggle sprint (turns off when you stop moving)
    ///   Right stick L/R      = snap turn 45°
    ///   Right stick UP       = aim teleport arc from the right hand, let go of the stick = teleport
    ///   Grip (hold)          = grab / hold an object, release Grip = let go (throw by swinging)
    ///   Poke with fingertip  = press 3D buttons
    ///   Y (left hand)        = show / hide the handbook
    ///   B (right hand)       = reset
    /// Desktop (testing): click Game view to lock mouse, mouse = look, WASD = walk, Shift = toggle sprint,
    ///   hold Right Mouse = aim teleport, release = teleport,
    ///   hold Left Mouse = grab / press what the crosshair points at, hold F = bring held item to your body/mouth,
    ///   Q/E = snap turn, H = handbook, R = reset, Esc = free the mouse.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class ShowcaseRig : MonoBehaviour
    {
        public static ShowcaseRig Instance { get; private set; }

        [Header("Hierarchy")]
        public Transform cameraOffset;
        public Transform head;
        public Transform leftHand;
        public Transform rightHand;
        public Transform crosshair;   // desktop only
        public Transform hud;
        [Tooltip("Floating handbook panel, toggled with Y")]
        public GameObject handbook;

        [Header("Teleport (right hand)")]
        public LineRenderer line;
        public Transform reticle;
        public float arcSpeed = 7f;

        [Header("Locomotion (H3VR Twinstick style)")]
        public float walkSpeed = 1.8f;
        public float sprintSpeed = 3.5f;
        public float snapTurnAngle = 45f;
        [Tooltip("true = stick moves where you LOOK, false = where the left hand points")]
        public bool headRelative = true;

        [Header("Interaction")]
        public float grabRadius = 0.09f;
        public float desktopReach = 2.5f;
        public Color gloveColor = new Color(0.55f, 0.75f, 0.95f);  // H3VR-style hand orbs
        public Color suitGloveColor = new Color(0.95f, 0.75f, 0.1f);
        [Tooltip("Items whose colliders must not push the player body")]
        public Grabbable[] items;

        [Header("Desktop")]
        public float desktopEyeHeight = 1.6f;
        public float mouseSensitivity = 0.1f;

        public event Action ResetRequested;
        public bool VRActive { get; private set; }

        public Vector3 FlatForward
        {
            get
            {
                Vector3 f = head.forward; f.y = 0f;
                return f.sqrMagnitude < 1e-4f ? transform.forward : f.normalized;
            }
        }
        public Vector3 MouthPoint => head.position - Vector3.up * 0.07f + FlatForward * 0.07f;
        public Vector3 ChestPoint => head.position - Vector3.up * 0.35f;

        class Hand
        {
            public Transform t;
            public InputAction grip;
            public Grabbable held;
            public Vector3 prevPos, velocity, angularVelocity;
            public Quaternion prevRot = Quaternion.identity;
            public bool poking;
            public Renderer[] renderers;
        }

        CharacterController body;
        Hand left, right;
        InputAction headPos, headRot, lPos, lRot, rPos, rRot;
        InputAction moveAction, sprintAction, turnAction, resetAction;
        InputAction rAimPos, rAimRot, handbookAction;
        readonly List<InputAction> allActions = new List<InputAction>();
        readonly Collider[] overlap = new Collider[16];

        bool originSet, turnReady = true;
        bool sprintOn, aiming;
        float floorOffset, yaw, pitch, verticalSpeed, idleTime;
        readonly List<Vector3> arcPoints = new List<Vector3>();
        Vector3 teleportTarget;
        bool teleportValid;
        static readonly Color ValidColor = new Color(0.2f, 1f, 0.4f);
        static readonly Color InvalidColor = new Color(1f, 0.25f, 0.2f);

        // ---------------------------------------------------------------- setup

        void Awake()
        {
            Instance = this;
            body = GetComponent<CharacterController>();

            headPos = Make("HeadPos", "Vector3", "<XRHMD>/centerEyePosition", "<XRHMD>/devicePosition");
            headRot = Make("HeadRot", "Quaternion", "<XRHMD>/centerEyeRotation", "<XRHMD>/deviceRotation");
            lPos = Make("LPos", "Vector3", "<XRController>{LeftHand}/devicePosition");
            lRot = Make("LRot", "Quaternion", "<XRController>{LeftHand}/deviceRotation");
            rPos = Make("RPos", "Vector3", "<XRController>{RightHand}/devicePosition");
            rRot = Make("RRot", "Quaternion", "<XRController>{RightHand}/deviceRotation");
            rAimPos = Make("RAimPos", "Vector3", "<XRController>{RightHand}/pointerPosition");
            rAimRot = Make("RAimRot", "Quaternion", "<XRController>{RightHand}/pointerRotation");

            moveAction = Make("Move", "Vector2", "<XRController>{LeftHand}/thumbstick", "<XRController>{LeftHand}/{Primary2DAxis}");
            turnAction = Make("Turn", "Vector2", "<XRController>{RightHand}/thumbstick", "<XRController>{RightHand}/{Primary2DAxis}");
            sprintAction = MakeButton("Sprint", "<XRController>{LeftHand}/thumbstickClicked", "<XRController>{LeftHand}/{Primary2DAxisClick}");
            resetAction = MakeButton("Reset", "<XRController>{RightHand}/secondaryButton", "<XRController>{RightHand}/{SecondaryButton}", "<Keyboard>/r");
            handbookAction = MakeButton("Handbook", "<XRController>{LeftHand}/secondaryButton", "<XRController>{LeftHand}/{SecondaryButton}", "<Keyboard>/h");

            left = new Hand
            {
                t = leftHand,
                grip = MakeButton("LGrip", "<XRController>{LeftHand}/gripPressed", "<XRController>{LeftHand}/{GripButton}"),
                renderers = leftHand.GetComponentsInChildren<Renderer>(true)
            };
            right = new Hand
            {
                t = rightHand,
                grip = MakeButton("RGrip", "<XRController>{RightHand}/gripPressed", "<XRController>{RightHand}/{GripButton}"),
                renderers = rightHand.GetComponentsInChildren<Renderer>(true)
            };
        }

        InputAction Make(string name, string controlType, params string[] bindings)
        {
            var a = new InputAction(name, InputActionType.Value, expectedControlType: controlType);
            foreach (var b in bindings) a.AddBinding(b);
            allActions.Add(a);
            return a;
        }

        InputAction MakeButton(string name, params string[] bindings)
        {
            var a = new InputAction(name, InputActionType.Button);
            foreach (var b in bindings) a.AddBinding(b);
            allActions.Add(a);
            return a;
        }

        void Start()
        {
            // held / hanging items must not shove the player's body around
            if (items != null)
                foreach (var item in items)
                    if (item != null)
                        foreach (var c in item.GetComponentsInChildren<Collider>(true))
                            Physics.IgnoreCollision(body, c, true);
            SetGloves(false);
        }

        void OnEnable()
        {
            foreach (var a in allActions) a.Enable();
            Application.onBeforeRender += OnBeforeRender;
        }

        void OnDisable()
        {
            foreach (var a in allActions) a.Disable();
            Application.onBeforeRender -= OnBeforeRender;
            Cursor.lockState = CursorLockMode.None;
        }

        void OnDestroy()
        {
            foreach (var a in allActions) a.Dispose();
            if (Instance == this) Instance = null;
        }

        // ---------------------------------------------------------------- main loop

        void Update()
        {
            VRActive = XRSettings.isDeviceActive;

            if (VRActive) { SetupTrackingOrigin(); UpdateTrackedPoses(); }
            else UpdateDesktopLookAndHand();

            foreach (var r in left.renderers) r.enabled = VRActive;
            foreach (var r in right.renderers) r.enabled = VRActive;

            if (resetAction.WasPressedThisFrame()) ResetRequested?.Invoke();
            if (handbookAction.WasPressedThisFrame()) ToggleHandbook();

            UpdateBodyCollider();
            UpdateLocomotion();
            UpdateSnapTurn();
            UpdateTeleport();

            if (VRActive)
            {
                UpdateHandVR(left);
                UpdateHandVR(right);
            }
            else UpdateHandDesktop(right);

            if (crosshair != null)
            {
                crosshair.gameObject.SetActive(!VRActive);
                crosshair.SetPositionAndRotation(head.position + head.forward * 0.5f, head.rotation);
            }
        }

        void OnBeforeRender()
        {
            if (!VRActive) return;
            UpdateTrackedPoses();
            if (left.held != null && left.held.gameObject.activeInHierarchy) left.held.HeldUpdate();
            if (right.held != null && right.held.gameObject.activeInHierarchy) right.held.HeldUpdate();
        }

        void LateUpdate()
        {
            UpdateHud();
            UpdateHandbook(false);
        }

        // ---------------------------------------------------------------- handbook (Y)

        public void ToggleHandbook()
        {
            if (handbook == null) return;
            bool show = !handbook.activeSelf;
            handbook.SetActive(show);
            if (show) UpdateHandbook(true);
        }

        void UpdateHandbook(bool snap)
        {
            if (handbook == null || !handbook.activeSelf) return;
            Vector3 fwd = FlatForward;
            Vector3 targetPos = head.position + fwd * 0.9f - Vector3.up * 0.1f;
            Quaternion targetRot = Quaternion.LookRotation(fwd, Vector3.up);
            float k = snap ? 1f : 1f - Mathf.Exp(-Time.deltaTime * 3f); // lazy follow, easy on the eyes
            var t = handbook.transform;
            t.SetPositionAndRotation(Vector3.Lerp(t.position, targetPos, k), Quaternion.Slerp(t.rotation, targetRot, k));
        }

        // ---------------------------------------------------------------- tracking

        void SetupTrackingOrigin()
        {
            if (originSet) return;
            originSet = true;
            bool floorOk = false;
            var subsystems = new List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            foreach (var s in subsystems)
                if (s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor)) floorOk = true;
            floorOffset = floorOk ? 0f : desktopEyeHeight;
        }

        void UpdateTrackedPoses()
        {
            cameraOffset.localPosition = new Vector3(0f, floorOffset, 0f);
            if (headPos.controls.Count > 0) head.localPosition = headPos.ReadValue<Vector3>();
            if (headRot.controls.Count > 0) head.localRotation = headRot.ReadValue<Quaternion>();
            if (lPos.controls.Count > 0) { leftHand.localPosition = lPos.ReadValue<Vector3>(); leftHand.localRotation = lRot.ReadValue<Quaternion>(); }
            if (rPos.controls.Count > 0) { rightHand.localPosition = rPos.ReadValue<Vector3>(); rightHand.localRotation = rRot.ReadValue<Quaternion>(); }
        }

        void UpdateDesktopLookAndHand()
        {
            cameraOffset.localPosition = new Vector3(0f, desktopEyeHeight, 0f);
            head.localPosition = Vector3.zero;

            var mouse = Mouse.current;
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Cursor.lockState = CursorLockMode.None;
            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 d = mouse.delta.ReadValue() * mouseSensitivity;
                yaw += d.x;
                pitch = Mathf.Clamp(pitch - d.y, -85f, 85f);
            }
            head.localRotation = Quaternion.Euler(pitch, yaw, 0f);

            // virtual right hand in front of the camera; hold F to bring it to your body / mouth
            bool bringIn = kb != null && kb.fKey.isPressed;
            Vector3 target = bringIn
                ? head.position - Vector3.up * 0.15f + FlatForward * 0.1f
                : head.position + head.forward * 0.45f - head.up * 0.12f + head.right * 0.08f;
            rightHand.position = Vector3.Lerp(rightHand.position, target, 1f - Mathf.Exp(-Time.deltaTime * 15f));
            rightHand.rotation = head.rotation;
        }

        void UpdateBodyCollider()
        {
            // keep the capsule under the head (supports walking around in your real room too)
            Vector3 local = transform.InverseTransformPoint(head.position);
            float h = Mathf.Clamp(local.y + 0.1f, 1.0f, 2.2f);
            body.height = h;
            body.center = new Vector3(local.x, h * 0.5f + body.skinWidth, local.z);
        }

        // ---------------------------------------------------------------- locomotion

        void UpdateLocomotion()
        {
            Vector2 input = Vector2.zero;
            bool sprint = false;

            if (VRActive)
            {
                if (moveAction.controls.Count > 0) input = moveAction.ReadValue<Vector2>();
                if (sprintAction.WasPressedThisFrame()) sprintOn = !sprintOn;   // click left stick = toggle sprint
            }
            else if (Keyboard.current != null)
            {
                var kb = Keyboard.current;
                if (kb.wKey.isPressed) input.y += 1f;
                if (kb.sKey.isPressed) input.y -= 1f;
                if (kb.aKey.isPressed) input.x -= 1f;
                if (kb.dKey.isPressed) input.x += 1f;
                if (kb.leftShiftKey.wasPressedThisFrame) sprintOn = !sprintOn;
            }

            if (input.magnitude < 0.15f) input = Vector2.zero; // dead zone
            input = Vector2.ClampMagnitude(input, 1f);

            // sprint switches itself off once you stop moving
            if (input == Vector2.zero) { idleTime += Time.deltaTime; if (idleTime > 0.3f) sprintOn = false; }
            else idleTime = 0f;
            sprint = sprintOn;

            Transform reference = headRelative || !VRActive ? head : leftHand;
            Vector3 fwd = reference.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = transform.forward;
            fwd.Normalize();
            Vector3 rightDir = Vector3.Cross(Vector3.up, fwd);
            Vector3 move = (fwd * input.y + rightDir * input.x) * (sprint ? sprintSpeed : walkSpeed);

            if (body.isGrounded && verticalSpeed < 0f) verticalSpeed = -1f;
            else verticalSpeed += Physics.gravity.y * Time.deltaTime;
            move.y = verticalSpeed;

            body.Move(move * Time.deltaTime);
        }

        void UpdateSnapTurn()
        {
            float x = 0f;
            if (VRActive && turnAction.controls.Count > 0)
            {
                Vector2 stick = turnAction.ReadValue<Vector2>();
                if (Mathf.Abs(stick.y) < 0.5f && !aiming) x = stick.x;
            }
            var kb = Keyboard.current;
            if (!VRActive && kb != null)
            {
                if (kb.qKey.wasPressedThisFrame) { x = -1f; turnReady = true; }
                if (kb.eKey.wasPressedThisFrame) { x = 1f; turnReady = true; }
            }

            if (turnReady && Mathf.Abs(x) > 0.7f)
            {
                Vector3 pivot = head.position;
                body.enabled = false;
                transform.RotateAround(pivot, Vector3.up, Mathf.Sign(x) * snapTurnAngle);
                body.enabled = true;
                turnReady = false;
            }
            else if (Mathf.Abs(x) < 0.4f) turnReady = true;
        }

        // ---------------------------------------------------------------- teleport (right hand)

        void UpdateTeleport()
        {
            bool wantAim;
            Vector3 origin, dir, lineStart;

            if (VRActive)
            {
                Vector2 stick = turnAction.controls.Count > 0 ? turnAction.ReadValue<Vector2>() : Vector2.zero;
                wantAim = aiming ? stick.y > 0.3f : stick.y > 0.6f && stick.y > Mathf.Abs(stick.x);
                if (rAimPos.controls.Count > 0 && rAimRot.controls.Count > 0)
                {
                    origin = cameraOffset.TransformPoint(rAimPos.ReadValue<Vector3>());
                    dir = cameraOffset.rotation * rAimRot.ReadValue<Quaternion>() * Vector3.forward;
                }
                else { origin = rightHand.position; dir = rightHand.forward; }
                lineStart = origin;
            }
            else
            {
                var mouse = Mouse.current;
                wantAim = mouse != null && Cursor.lockState == CursorLockMode.Locked && mouse.rightButton.isPressed;
                origin = head.position;
                dir = head.forward;
                lineStart = head.position - head.up * 0.25f + head.right * 0.15f + head.forward * 0.3f;
            }

            if (wantAim)
            {
                aiming = true;
                teleportValid = ComputeArc(origin, lineStart, dir, out teleportTarget, out Vector3 normal);
                Color c = teleportValid ? ValidColor : InvalidColor;
                if (line != null)
                {
                    line.enabled = true;
                    line.positionCount = arcPoints.Count;
                    for (int i = 0; i < arcPoints.Count; i++) line.SetPosition(i, arcPoints[i]);
                    line.startColor = c; line.endColor = c;
                }
                if (reticle != null)
                {
                    reticle.gameObject.SetActive(true);
                    reticle.position = teleportTarget + normal * 0.01f;
                    reticle.rotation = Quaternion.FromToRotation(Vector3.up, normal);
                    var r = reticle.GetComponentInChildren<Renderer>();
                    if (r != null) r.material.color = c;
                }
            }
            else if (aiming)
            {
                aiming = false;                         // stick / button let go = teleport
                if (teleportValid) TeleportTo(teleportTarget);
                HideTeleport();
            }
        }

        bool ComputeArc(Vector3 origin, Vector3 lineStart, Vector3 dir, out Vector3 target, out Vector3 normal)
        {
            arcPoints.Clear();
            arcPoints.Add(lineStart);
            target = origin; normal = Vector3.up;
            Vector3 p = origin;
            Vector3 v = dir.normalized * arcSpeed;
            const float dt = 0.04f;
            for (int i = 0; i < 80; i++)
            {
                Vector3 next = p + v * dt;
                v += Physics.gravity * dt;
                if (RaycastIgnoringSelf(p, next, out RaycastHit hit))
                {
                    arcPoints.Add(hit.point);
                    target = hit.point; normal = hit.normal;
                    return hit.collider.GetComponentInParent<TeleportArea>() != null && hit.normal.y > 0.7f;
                }
                arcPoints.Add(next);
                p = next;
            }
            target = p;
            return false;
        }

        bool RaycastIgnoringSelf(Vector3 from, Vector3 to, out RaycastHit hit)
        {
            Vector3 d = to - from;
            float len = d.magnitude;
            var hits = Physics.RaycastAll(from, d / len, len, ~0, QueryTriggerInteraction.Ignore);
            hit = default;
            float best = float.MaxValue;
            foreach (var h in hits)
            {
                if (h.collider == body || h.collider.GetComponentInParent<Grabbable>() != null) continue;
                if (h.distance < best) { best = h.distance; hit = h; }
            }
            return best < float.MaxValue;
        }

        void HideTeleport()
        {
            if (line != null) line.enabled = false;
            if (reticle != null) reticle.gameObject.SetActive(false);
        }

        /// <summary>Moves the rig so the player's head ends up above the target point.</summary>
        public void TeleportTo(Vector3 floorPoint)
        {
            Vector3 headOffset = head.position - transform.position; headOffset.y = 0f;
            body.enabled = false;
            transform.position = floorPoint - headOffset;
            body.enabled = true;
            verticalSpeed = 0f;
        }

        // ---------------------------------------------------------------- hands

        void TrackVelocity(Hand h)
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            Vector3 v = (h.t.position - h.prevPos) / dt;
            h.velocity = Vector3.Lerp(h.velocity, v, 0.5f);
            Quaternion dq = h.t.rotation * Quaternion.Inverse(h.prevRot);
            dq.ToAngleAxis(out float ang, out Vector3 axis);
            if (ang > 180f) ang -= 360f;
            if (!float.IsNaN(axis.x) && !float.IsInfinity(axis.x)) h.angularVelocity = axis * (ang * Mathf.Deg2Rad / dt);
            h.prevPos = h.t.position;
            h.prevRot = h.t.rotation;
        }

        void UpdateHandVR(Hand h)
        {
            TrackVelocity(h);

            // poke 3D buttons with the fingertip
            Vector3 tip = h.t.TransformPoint(0f, 0f, 0.02f); // front of the round hand
            bool touching = false;
            int n = Physics.OverlapSphereNonAlloc(tip, 0.035f, overlap, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var b = overlap[i].GetComponentInParent<WorldButton>();
                if (b == null) continue;
                touching = true;
                b.MarkHovered();
                if (!h.poking) b.Press();
                break;
            }
            h.poking = touching;

            if (h.held == null)
            {
                Grabbable nearest = FindNearestGrabbable(h.t.position);
                if (nearest != null) nearest.MarkHovered();
                if (h.grip.WasPressedThisFrame() && nearest != null) DoGrab(h, nearest, false);
            }
            else
            {
                if (!h.grip.IsPressed()) DoRelease(h);
                else h.held.HeldUpdate();
                if (h.held != null && !h.held.gameObject.activeInHierarchy) h.held = null; // worn / eaten
            }
        }

        void UpdateHandDesktop(Hand h)
        {
            TrackVelocity(h);
            var mouse = Mouse.current;
            if (mouse == null) return;

            if (Cursor.lockState != CursorLockMode.Locked)
            {
                if (mouse.leftButton.wasPressedThisFrame) Cursor.lockState = CursorLockMode.Locked; // first click only locks the mouse
                if (h.held != null) DoRelease(h);
                return;
            }

            if (h.held == null)
            {
                if (Physics.Raycast(head.position, head.forward, out RaycastHit hit, desktopReach, ~0, QueryTriggerInteraction.Collide))
                {
                    var b = hit.collider.GetComponentInParent<WorldButton>();
                    var g = hit.collider.GetComponentInParent<Grabbable>();
                    if (b != null)
                    {
                        b.MarkHovered();
                        if (mouse.leftButton.wasPressedThisFrame) b.Press();
                    }
                    else if (g != null && g.CanGrab)
                    {
                        g.MarkHovered();
                        if (mouse.leftButton.wasPressedThisFrame)
                        {
                            bool isLever = g is LeverHandle;
                            if (!isLever) h.t.position = hit.point;
                            DoGrab(h, g, !isLever);
                        }
                    }
                }
            }
            else
            {
                if (!mouse.leftButton.isPressed) DoRelease(h);
                else h.held.HeldUpdate();
                if (h.held != null && !h.held.gameObject.activeInHierarchy) h.held = null;
            }
        }

        Grabbable FindNearestGrabbable(Vector3 p)
        {
            Grabbable best = null;
            float bestDist = float.MaxValue;
            int n = Physics.OverlapSphereNonAlloc(p, grabRadius, overlap, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var g = overlap[i].GetComponentInParent<Grabbable>();
                if (g == null || !g.CanGrab) continue;
                float d = Vector3.Distance(p, overlap[i].ClosestPoint(p));
                if (d < bestDist) { bestDist = d; best = g; }
            }
            return best;
        }

        void DoGrab(Hand h, Grabbable g, bool snap)
        {
            Hand other = h == left ? right : left;
            if (other.held == g) other.held = null; // hand-over from the other hand
            h.held = g;
            g.Grab(h.t, snap);
        }

        void DoRelease(Hand h)
        {
            if (h.held == null) return;
            h.held.Release(h.velocity, h.angularVelocity);
            h.held = null;
        }

        public void ReleaseAll()
        {
            if (left.held != null) { left.held.Release(Vector3.zero, Vector3.zero); left.held = null; }
            if (right.held != null) { right.held.Release(Vector3.zero, Vector3.zero); right.held = null; }
        }

        /// <summary>Hands turn into yellow suit gloves once the suit is on.</summary>
        public void SetGloves(bool suit)
        {
            Color c = suit ? suitGloveColor : gloveColor;
            foreach (var h in new[] { left, right })
                foreach (var r in h.renderers)
                    if (r != null) r.material.color = c;
        }

        // ---------------------------------------------------------------- reset / HUD

        public void ResetTo(Transform start)
        {
            ReleaseAll();
            aiming = false; sprintOn = false;
            HideTeleport();
            float headYaw = VRActive ? head.localEulerAngles.y : 0f;
            if (!VRActive) { yaw = 0f; pitch = 0f; head.localRotation = Quaternion.identity; }
            body.enabled = false;
            transform.rotation = Quaternion.Euler(0f, start.eulerAngles.y - headYaw, 0f);
            transform.position = start.position;
            Vector3 headOffset = head.position - transform.position; headOffset.y = 0f;
            transform.position = start.position - headOffset;
            body.enabled = true;
            verticalSpeed = 0f;
        }

        void UpdateHud()
        {
            if (hud == null) return;
            if (VRActive)
            {
                Vector3 pos = leftHand.position + Vector3.up * 0.17f;
                hud.position = pos;
                hud.rotation = Quaternion.LookRotation(pos - head.position, Vector3.up);
            }
            else
            {
                hud.position = head.position + head.forward * 0.6f - head.right * 0.24f - head.up * 0.16f;
                hud.rotation = head.rotation;
            }
        }
    }
}
