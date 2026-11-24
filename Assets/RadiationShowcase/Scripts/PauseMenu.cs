using UnityEngine;
using UnityEngine.InputSystem;

namespace RadiationShowcase
{
    /// <summary>
    /// Main menu (shown when the game starts) + pause menu.
    /// Main menu: title "How to survive: Radiation Leak" with Start / Quit; the game waits until Start.
    /// Pause menu: VR Menu button (☰) on the left controller, desktop Esc.
    /// Menus float ~1.4 m away; in VR both hands shoot a laser pointer — point at a button and pull Trigger.
    /// While paused, time stops (radiation, physics, sound) and a panel appears in front of the player with
    /// three 3D buttons — touch them with a hand in VR, click them with the mouse on desktop:
    ///   Resume / Restart / Quit.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        public ShowcaseRig rig;
        public GameFlow flow;
        public GameObject panel;
        [Tooltip("Title screen shown when Play starts")]
        public GameObject mainPanel;
        public bool showMainMenuOnStart = true;
        [Tooltip("How far in front of the eyes the menus appear (m); they move closer if a wall is in the way")]
        public float distance = 1.4f;

        public static bool Paused { get; private set; }
        public static PauseMenu Instance { get; private set; }

        /// <summary>The end-of-game message (with Restart / Quit buttons) is on screen.</summary>
        public bool EndScreenOpen => flow != null && flow.endPanel != null && flow.endPanel.activeInHierarchy;

        /// <summary>True for the buttons on the open menu (the VR laser pointer only clicks these).</summary>
        public bool IsMenuButton(WorldButton b)
        {
            if (b == null) return false;
            return (panel != null && panel.activeInHierarchy && b.transform.IsChildOf(panel.transform)) ||
                   (mainPanel != null && mainPanel.activeInHierarchy && b.transform.IsChildOf(mainPanel.transform)) ||
                   (EndScreenOpen && b.transform.IsChildOf(flow.endPanel.transform));
        }

        InputAction pauseAction;
        bool mainOpen;

        void Awake()
        {
            Instance = this;
            pauseAction = new InputAction("Pause", InputActionType.Button);
            pauseAction.AddBinding("<XRController>{LeftHand}/menu");
            pauseAction.AddBinding("<XRController>{LeftHand}/menuButton");
            pauseAction.AddBinding("<XRController>{LeftHand}/{MenuButton}");
            pauseAction.AddBinding("<Keyboard>/escape");
            if (panel != null) panel.SetActive(false);
            if (mainPanel != null) mainPanel.SetActive(false);
        }

        void Start()
        {
            if (showMainMenuOnStart && mainPanel != null) OpenMainMenu();
        }

        public void OpenMainMenu()
        {
            Paused = true;
            mainOpen = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            mainPanel.SetActive(true);
            PlaceInFront(mainPanel.transform, 1f);
        }

        void LateUpdate()
        {
            // the headset may only start tracking a moment after Play, so keep the title screen in front of the player
            if (mainOpen && mainPanel != null && rig != null && rig.VRActive)
                PlaceInFront(mainPanel.transform, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 4f));
        }

        void PlaceInFront(Transform t, float k)
        {
            if (rig == null) return;
            Vector3 fwd = rig.FlatForward;
            // keep the menu in front of any wall that is closer than 'distance'
            float d = distance;
            if (Physics.Raycast(rig.head.position, fwd, out RaycastHit wall, distance + 0.2f, ~0, QueryTriggerInteraction.Ignore) &&
                !IsPartOfMenu(wall.collider.transform))
                d = Mathf.Max(0.6f, wall.distance - 0.2f);
            Vector3 pos = rig.head.position + fwd * d - Vector3.up * 0.1f;
            t.SetPositionAndRotation(Vector3.Lerp(t.position, pos, k),
                Quaternion.Slerp(t.rotation, Quaternion.LookRotation(fwd, Vector3.up), k));
            Physics.SyncTransforms();   // physics is stopped, so keep the buttons' colliders current
        }

        void OnEnable()
        {
            pauseAction.Enable();
            WorldButton.Pressed += OnButtonPressed;
        }

        void OnDisable()
        {
            pauseAction.Disable();
            WorldButton.Pressed -= OnButtonPressed;
            if (Paused) Resume();
        }

        void OnDestroy()
        {
            pauseAction.Dispose();
            if (Instance == this) Instance = null;
        }

        bool IsPartOfMenu(Transform t) =>
            (panel != null && t.IsChildOf(panel.transform)) || (mainPanel != null && t.IsChildOf(mainPanel.transform));

        void Update()
        {
            if (pauseAction.WasPressedThisFrame() && !mainOpen)
            {
                if (Paused) Resume();
                else if (flow == null || !flow.IsFinished) Open();
            }

            if ((Paused || EndScreenOpen) && rig != null && !rig.VRActive) DesktopPointer();
        }

        public void Open()
        {
            Paused = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;

            if (panel != null)
            {
                panel.SetActive(true);
                PlaceInFront(panel.transform, 1f);
            }
        }

        public void Resume()
        {
            Paused = false;
            mainOpen = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (panel != null) panel.SetActive(false);
            if (mainPanel != null) mainPanel.SetActive(false);
            
        }

        void OnButtonPressed(WorldButton b)
        {
            if (!Paused && !EndScreenOpen) return;
            switch (b.id)
            {
                case "resume":
                case "start":
                    Resume();
                    break;
                case "restart":
                    Resume();
                    if (flow != null) flow.ResetGame();
                    break;
                case "quit":
                    Quit();
                    break;
            }
        }

        // desktop: aim with the centre crosshair (mouse look still works in menus) and left-click —
        // the same "point and click" as the VR laser, and independent of the Game view's scale
        void DesktopPointer()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                if (mouse.leftButton.wasPressedThisFrame) Cursor.lockState = CursorLockMode.Locked; // first click only locks
                return;
            }
            WorldButton best = null;
            float bestDist = 8f;
            foreach (var hit in Physics.RaycastAll(rig.head.position, rig.head.forward, 8f, ~0, QueryTriggerInteraction.Collide))
            {
                var b = hit.collider.GetComponentInParent<WorldButton>();
                if (b == null || !IsMenuButton(b) || hit.distance >= bestDist) continue;
                best = b;
                bestDist = hit.distance;
            }
            if (best == null) return;
            best.MarkHovered();
            if (mouse.leftButton.wasPressedThisFrame) best.Press();
        }

        void Quit()
        {
            Resume();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;   // in the Editor: leave Play mode
#else
            Application.Quit();
#endif
        }
    }
}
