using UnityEngine;

namespace CoverUp.Gameplay
{
    /// <summary>
    /// Marks geometry the HIDER'S CAMERA CANNOT CROSS: a map's walls, floors and
    /// ceilings. Drop it on a GameObject and everything in its subtree is covered,
    /// so one component on the group that holds the shell does a whole map.
    ///
    /// A hider's third-person camera passes through scenery so the body is always
    /// in view (the game draws it through whatever covers it). Left alone, that
    /// camera also walks out through the outer wall of an enclosed map and shows
    /// the empty scene around it, or through an inner wall into the next room.
    /// Surfaces carrying this marker get the seeker's spring arm instead: the lens
    /// stops short of them and pulls in when the view is turned into one, from
    /// either side, whatever way the surface faces. The free-fly camera of a
    /// spectator is held by them too. Everything UNMARKED keeps the see-through:
    /// a hay bale, a rock, a crate, a table, another player.
    ///
    /// What it does NOT change: feet, shots, sight, the float, exposure scoring.
    /// It is a camera rule and nothing else. An unmarked map behaves exactly as it
    /// did before this existed, so marking is opt-in and Validate Map only warns.
    ///
    /// Beside the camera arm, the game keeps marked surfaces whole inside the
    /// porthole's peephole (the circle around the body that shows the room
    /// behind a blocker): a marked wall crossing that circle stays drawn where a
    /// prop would be cut away. That needs the surface's RENDERERS, not just its
    /// colliders, so a marker should sit above both; asset packs that keep the
    /// collider and the mesh on sibling objects want it on their common parent.
    ///
    /// Precedence: a <see cref="PassThroughSurface"/> wins. A glass deck inside
    /// a marked shell stays glass to the camera, which is the case that comes up
    /// (the specific marker under the general one); the other way round has no
    /// use. <see cref="Blocks"/> is the one policy that encodes it.
    ///
    /// A MARKER PLUS A STATIC POLICY, like the rest of the family, because an
    /// AssetBundle serializes a layer INDEX and a map project's layer 12 is not
    /// the game's layer 12. The rendering layer bit below is different: it is
    /// stamped at runtime by this component, so it never travels in a bundle.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraProofSurface : MonoBehaviour
    {
        /// <summary>The rendering layer bit the game's peephole reads to keep a
        /// marked surface whole. Set on every renderer under the marker while it
        /// is enabled; bits 8 to 11 belong to the body, the viewmodel and the
        /// two outlines, so this is the next one.</summary>
        public const uint RenderingLayer = 1u << 12;

        /// <summary>How many markers are enabled right now. Zero means the game
        /// can skip the peephole's mask pass outright; the hub and every
        /// unmarked map sit at zero.</summary>
        public static int LiveCount { get; private set; }

        private Renderer[] tagged;

        private void OnEnable()
        {
            LiveCount++;
            // Inactive children too: a size variant switched off now is switched
            // on later without this component hearing about it.
            tagged = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < tagged.Length; i++)
                if (tagged[i] != null) tagged[i].renderingLayerMask |= RenderingLayer;
        }

        private void OnDisable()
        {
            LiveCount--;
            if (tagged == null) return;
            for (int i = 0; i < tagged.Length; i++)
            {
                Renderer r = tagged[i];
                if (r == null) continue;
                // A marker nested under another: the renderer is still covered.
                if (!CoveredByAnotherLive(r.transform)) r.renderingLayerMask &= ~RenderingLayer;
            }
            tagged = null;
        }

        private bool CoveredByAnotherLive(Transform t)
        {
            foreach (CameraProofSurface s in t.GetComponentsInParent<CameraProofSurface>(false))
                if (s != this && s.isActiveAndEnabled) return true;
            return false;
        }

        /// <summary>True if this collider belongs to a marked surface, whatever
        /// else it carries. Inactive parents count, for the reason spelled out on
        /// <see cref="PassThroughSurface.Is"/>: Validate Map reads the size
        /// variants that are switched off.</summary>
        public static bool Is(Collider collider) =>
            collider != null && collider.GetComponentInParent<CameraProofSurface>(true) != null;

        /// <summary>True if the camera must stop at this collider: marked, and not
        /// under a <see cref="PassThroughSurface"/>, which wins. The one policy
        /// for the hider's arm and the free-fly camera, so the two can never
        /// disagree about which wall is a wall.</summary>
        public static bool Blocks(Collider collider) =>
            Is(collider) && !PassThroughSurface.Is(collider);
    }
}
