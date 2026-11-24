using UnityEngine;

namespace RadiationShowcase
{
    /// <summary>Marks a collider as a valid place to teleport onto (e.g. the floor).</summary>
    [RequireComponent(typeof(Collider))]
    public class TeleportArea : MonoBehaviour
    {
    }
}
