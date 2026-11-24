using System;
using UnityEngine;

namespace RadiationShowcase
{
    /// <summary>
    /// A 3D button the player points at (VR controller ray or desktop crosshair) and presses with the trigger.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class WorldButton : MonoBehaviour
    {
        /// <summary>Fired when any WorldButton is pressed.</summary>
        public static event Action<WorldButton> Pressed;

        [Tooltip("Used by GameFlow to know what this button does: mask, fan, reset")]
        public string id;
        public Renderer targetRenderer;
        public Color emissionColor = new Color(1f, 0.8f, 0.2f);
        [Tooltip("Pulses to show 'press me now'")]
        public bool highlighted;

        int lastHoverFrame = -10;
        MaterialPropertyBlock block;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        public bool IsHovered => Time.frameCount - lastHoverFrame <= 1;

        public void MarkHovered() => lastHoverFrame = Time.frameCount;

        public void Press() => Pressed?.Invoke(this);

        void Awake()
        {
            if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();
            block = new MaterialPropertyBlock();
        }

        void Update()
        {
            if (targetRenderer == null) return;
            float strength = 0.15f;
            if (highlighted) strength = 0.6f + 0.6f * Mathf.Sin(Time.time * 6f);
            if (IsHovered) strength = 2.5f;

            targetRenderer.GetPropertyBlock(block);
            block.SetColor(EmissionId, emissionColor * strength);
            targetRenderer.SetPropertyBlock(block);
        }
    }
}
