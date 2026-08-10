using UnityEngine;

namespace CoverUp.Gameplay
{
    /// <summary>
    /// Marks geometry that SWALLOWS SHOTS: a shot that reaches it stops there and
    /// counts as a clean miss. No impact splash, no permanent mark, and nothing
    /// behind it can be hit either, because the shot never gets past.
    ///
    /// The case it exists for is the island hub's boundary ring: 32 invisible
    /// walls just outside the shallows, there so you can wade into the water but
    /// not swim off into the open sea. They are physics and nothing else, with no
    /// renderer at all, so a shot fired at the horizon stopped on a wall nobody
    /// can see and hung a permanent splot in mid-air over the water. Every map
    /// with an out-of-bounds wall has the same problem, which is why this lives in
    /// the SDK rather than in the hub.
    ///
    /// Deliberately NOT either of the two markers next to it, because each gets
    /// half of it wrong. <see cref="PassThroughSurface"/> lets the shot carry on,
    /// so the splot lands on the seabed behind the wall instead of on the wall.
    /// <see cref="PaintProofSurface"/> refuses the mark but keeps the impact, so
    /// paint still bursts against thin air. What an invisible wall wants is for
    /// the shot to read the way it looks like it should: sailing away over the
    /// water and landing nowhere.
    ///
    /// A MARKER PLUS A STATIC POLICY, the same shape as its two siblings and for
    /// the same reason: an AssetBundle serializes a layer INDEX, and a map
    /// project's layer 12 is not the game's layer 12. A component rebinds by
    /// class name, and the MapSceneGuard allowlist trusts the whole SDK assembly.
    ///
    /// Tested by COLLIDER rather than by position, unlike PaintProofSurface:
    /// everything that asks is holding the raycast hit itself, at the moment the
    /// shot resolves, before any of it goes over the wire.
    ///
    /// What it does NOT change: movement. The surface still blocks feet, which is
    /// the entire reason it is standing there.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShotVoidSurface : MonoBehaviour
    {
        /// <summary>True if this collider belongs to a shot-swallowing surface.
        /// One policy for the gun and for paint scatter, so the two can never
        /// disagree about where a shot ended.</summary>
        public static bool Is(Collider collider) =>
            collider != null && collider.GetComponentInParent<ShotVoidSurface>() != null;
    }
}
