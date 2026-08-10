using UnityEngine;

namespace CoverUp.Gameplay
{
    /// <summary>
    /// Marks geometry the FLOAT GLOBE CANNOT SEE: being next to it is being next
    /// to nothing. Gravity keeps applying, SPACE does not turn into a climb, and
    /// the lean cue does not tip the body toward it. Drop it on a GameObject and
    /// everything in its subtree is covered.
    ///
    /// The case it exists for is the island hub's boundary ring, the invisible
    /// wall that keeps people out of the open sea. The float is the only way up a
    /// vertical face in the game, it holds you as long as anything solid is in
    /// range, and it has no height ceiling, so an invisible wall was a ladder: you
    /// floated up the thing you could not see and dropped over the far side into
    /// the water it was there to keep you out of. Any map with an out-of-bounds
    /// wall can be left the same way, which is why this lives in the SDK.
    ///
    /// A MARKER PLUS A STATIC POLICY, the same shape as the rest of the family
    /// and for the same reason: an AssetBundle serializes a layer INDEX, and a map
    /// project's layer 12 is not the game's layer 12. It is also why the float
    /// cannot simply mask the boundary out: its overlap runs unmasked on purpose,
    /// because a map's geometry can be on any layer at all.
    ///
    /// What it does NOT change: walking, jumping, or collision. The surface is
    /// still solid, still blocks feet, and still stops a capsule dead. It only
    /// stops being something to hold on to. A wall that is float-proof but short
    /// enough to jump is still a wall you can jump, so height is the author's
    /// problem, not this marker's.
    ///
    /// Deliberately NOT a "no climb" flag on the collider or a slope trick: the
    /// float never touches slope or friction, it asks one question about what is
    /// in range, and this answers exactly that question.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FloatProofSurface : MonoBehaviour
    {
        /// <summary>True if this collider belongs to a surface the float ignores.
        /// One policy for the sense and for the lean cue, so the body can never
        /// tilt toward a wall that is not holding it.</summary>
        public static bool Is(Collider collider) =>
            collider != null && collider.GetComponentInParent<FloatProofSurface>() != null;
    }
}
