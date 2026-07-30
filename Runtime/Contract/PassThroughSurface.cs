using UnityEngine;

namespace CoverUp.Gameplay
{
    /// <summary>
    /// Marks geometry that is SOLID to feet but transparent to shots and to the
    /// third-person camera boom. Drop it on a GameObject and every collider in its
    /// subtree stops blocking both.
    ///
    /// The case it exists for: a map that stacks its two roles vertically. Diorama
    /// puts the seekers on a transparent floor above the hiders, who read as
    /// figures floating overhead — which only works if the seekers can shoot down
    /// through the glass they are standing on, and if a hider looking up doesn't
    /// have the boom slammed into their face by a pane they can see straight
    /// through.
    ///
    /// Deliberately NOT a layer: an AssetBundle serializes the layer INDEX, and a
    /// third-party map project's layer 12 is not necessarily the game's layer 12.
    /// A component rebinds by class name like every other Map SDK marker, and the
    /// MapSceneGuard allowlist trusts the whole SDK assembly, so this needs no
    /// security-policy edit to be legal in a downloaded map.
    ///
    /// What it does NOT change: movement (the surface is walkable — that is the
    /// whole point), and exposure scoring, which is measured by rendering hider
    /// proxies rather than by raycast, so a transparent floor already occludes
    /// exactly as much as it visibly occludes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PassThroughSurface : MonoBehaviour
    {
        /// <summary>True if this collider belongs to a pass-through surface.
        /// One policy, shared by the gun and the camera, so the two can never
        /// disagree about what a shot can cross.</summary>
        public static bool Is(Collider collider) =>
            collider != null && collider.GetComponentInParent<PassThroughSurface>() != null;
    }
}
