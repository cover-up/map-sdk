using CoverUp.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CoverUp.Gameplay
{
    /// <summary>
    /// Optional per-map component, sibling to <see cref="MapConfig"/> on the
    /// <c>_CoverUpMap</c> root. Holds the three size-variant roots; the loader
    /// activates exactly one for the round (the host-resolved size, clamped to
    /// what this map built) and deactivates the others. A map WITHOUT this
    /// component is a one-size map and behaves exactly as before.
    ///
    /// Authoring / Workshop contract:
    ///   • <see cref="MapBoundsVolume"/>s live ONLY inside these roots, never in
    ///     Base — clamping is the union of ACTIVE volumes, so a Base volume would
    ///     leak the largest extent into every size.
    ///   • The spawn disc stays in Base (Small always contains spawn).
    ///   • Smaller sizes ADD doors/blockers; nothing is removed.
    /// The Validate Map editor menu enforces these.
    /// </summary>
    public sealed class MapSizeVariants : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Root for the Small variant (bounds + doors). Leave empty if this map has no Small build.")]
        private GameObject small;
        [SerializeField]
        [Tooltip("Root for the Medium variant. Leave empty if this map has no Medium build.")]
        private GameObject medium;
        [SerializeField]
        [Tooltip("Root for the Large variant. Leave empty if this map has no Large build.")]
        private GameObject large;

        [SerializeField]
        [Tooltip("Editor/solo-play only: the size to show when this scene is played on its own " +
                 "(Walk Through, solo Play), where no host resolves one. Ignored in multiplayer — " +
                 "the loader applies the host-resolved size at door-open. Preview Size keeps this in sync.")]
        private MapSize soloWalkSize = MapSize.Medium;

        [SerializeField]
        [Tooltip("AUTO size only: the most players this map still wants to run Small for. " +
                 "Above this it goes Medium. Ignored when the host forces a size in the lobby.")]
        private int smallMaxPlayers = MapSizeRules.DefaultSmallMaxPlayers;

        [SerializeField]
        [Tooltip("AUTO size only: the most players this map still wants to run Medium for. " +
                 "Above this it goes Large. Must be greater than Small Max Players.")]
        private int mediumMaxPlayers = MapSizeRules.DefaultMediumMaxPlayers;

        /// <summary>The map's own auto-size brackets, sanitized. Read by the
        /// host before it resolves the round's size; a map that leaves these
        /// alone gets the shipped defaults and behaves exactly as before.</summary>
        public (int SmallMax, int MediumMax) AutoThresholds
        {
            get
            {
                int s = smallMaxPlayers, m = mediumMaxPlayers;
                MapSizeRules.Sanitize(ref s, ref m);
                return (s, m);
            }
        }

        // Solo/editor path (Walk Through, solo Play → scene loaded single/active):
        // pick a single size so authored-active roots don't overlap. Mirrors
        // MapConfig.Awake — the additive multiplayer path is NOT active at Awake,
        // so this no-ops there and MapLoader.ApplySize commits the host-resolved
        // size at door-open instead.
        private void Awake()
        {
            if (gameObject.scene == SceneManager.GetActiveScene()) Apply(soloWalkSize);
        }

        public GameObject Root(MapSize size) => size switch
        {
            MapSize.Small => small,
            MapSize.Medium => medium,
            _ => large,
        };

        /// <summary>Bit i set = MapSize i is built (its root is assigned).</summary>
        public int BuiltMask
        {
            get
            {
                int mask = 0;
                if (small != null) mask |= 1 << (int)MapSize.Small;
                if (medium != null) mask |= 1 << (int)MapSize.Medium;
                if (large != null) mask |= 1 << (int)MapSize.Large;
                return mask;
            }
        }

        /// <summary>
        /// Activate the clamped desired size and deactivate the rest. Every
        /// client runs this against the same scene, so the clamp lands
        /// identically without the host needing scene knowledge. Returns the
        /// size actually applied (may differ from <paramref name="desired"/>
        /// after clamping); a variant-less map returns desired and touches
        /// nothing.
        /// </summary>
        public MapSize Apply(MapSize desired)
        {
            int built = BuiltMask;
            if (built == 0) return desired;
            MapSize chosen = MapSizeRules.Clamp(desired, built);
            SetActiveSafe(small, chosen == MapSize.Small);
            SetActiveSafe(medium, chosen == MapSize.Medium);
            SetActiveSafe(large, chosen == MapSize.Large);
            return chosen;
        }

        private static void SetActiveSafe(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active) go.SetActive(active);
        }

        /// <summary>The variants component belonging to one specific scene
        /// (inactive roots included — the whole point is toggling them).</summary>
        public static MapSizeVariants FindInScene(Scene scene)
        {
            foreach (MapSizeVariants v in FindObjectsByType<MapSizeVariants>(FindObjectsInactive.Include))
            {
                if (v.gameObject.scene == scene) return v;
            }
            return null;
        }
    }
}
