using System;
using CoverUp.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CoverUp.Gameplay
{
    /// <summary>
    /// Optional per-map component, sibling to <see cref="MapConfig"/> on the
    /// <c>_CoverUpMap</c> root. Holds up to five size-variant roots (Tiny to
    /// Huge), each with the most players it runs for; the loader activates
    /// exactly one for the round (the host's pick for the player count, clamped
    /// to what this map built) and deactivates the others. A map WITHOUT this
    /// component is a one-size map and behaves exactly as before.
    ///
    /// Authoring / Workshop contract:
    ///   • <see cref="MapBoundsVolume"/>s live ONLY inside these roots, never in
    ///     Base — clamping is the union of ACTIVE volumes, so a Base volume would
    ///     leak the largest extent into every size.
    ///   • The spawn disc stays in Base (the smallest size always contains spawn).
    ///   • Smaller sizes ADD doors/blockers; nothing is removed.
    /// The Validate Map editor menu enforces these.
    /// </summary>
    public sealed class MapSizeVariants : MonoBehaviour
    {
        /// <summary>One size slot: its root, and the most players it runs for.</summary>
        [Serializable]
        public struct Variant
        {
            [Tooltip("Root for this size (bounds + doors). Leave empty if this map has no build at this size.")]
            public GameObject root;

            [Tooltip("The most players this size runs for. More than this goes to the next size up. " +
                     "Ignored on the largest size this map builds, which takes everyone above.")]
            public int maxPlayers;

            public Variant(int maxPlayers) { root = null; this.maxPlayers = maxPlayers; }
        }

        [SerializeField] private Variant tiny = new Variant(MapSizeRules.DefaultMaxPlayers(MapSize.Tiny));
        [SerializeField] private Variant small = new Variant(MapSizeRules.DefaultMaxPlayers(MapSize.Small));
        [SerializeField] private Variant medium = new Variant(MapSizeRules.DefaultMaxPlayers(MapSize.Medium));
        [SerializeField] private Variant large = new Variant(MapSizeRules.DefaultMaxPlayers(MapSize.Large));
        [SerializeField] private Variant huge = new Variant(MapSizeRules.DefaultMaxPlayers(MapSize.Huge));

        [SerializeField]
        [Tooltip("Editor/solo-play only: the size to show when this scene is played on its own " +
                 "(Walk Through, solo Play), where no host resolves one. Ignored in multiplayer — " +
                 "the loader applies the host-resolved size at door-open. Preview Size keeps this in sync.")]
        private MapSize soloWalkSize = MapSize.Medium;

        private ref Variant Slot(MapSize size)
        {
            switch (size)
            {
                case MapSize.Tiny: return ref tiny;
                case MapSize.Small: return ref small;
                case MapSize.Medium: return ref medium;
                case MapSize.Large: return ref large;
                default: return ref huge;
            }
        }

        /// <summary>The map's sizes and their player brackets, sanitized. Read
        /// by the host before it picks the round's size.</summary>
        public MapSizeBrackets Brackets
        {
            get
            {
                var max = new int[MapSizeRules.Count];
                foreach (MapSize s in MapSizeRules.All) max[(int)s] = Slot(s).maxPlayers;
                return new MapSizeBrackets(BuiltMask, max);
            }
        }

        /// <summary>The authored (unsanitized) bracket of one size, for the validator.</summary>
        public int AuthoredMaxPlayers(MapSize size) => Slot(size).maxPlayers;

        // Solo/editor path (Walk Through, solo Play → scene loaded single/active):
        // pick a single size so authored-active roots don't overlap. Mirrors
        // MapConfig.Awake — the additive multiplayer path is NOT active at Awake,
        // so this no-ops there and MapLoader.ApplySize commits the host-resolved
        // size at door-open instead.
        private void Awake()
        {
            if (gameObject.scene == SceneManager.GetActiveScene()) Apply(soloWalkSize);
        }

        public GameObject Root(MapSize size) => Slot(size).root;

        /// <summary>Bit i set = MapSize i is built (its root is assigned).</summary>
        public int BuiltMask
        {
            get
            {
                int mask = 0;
                foreach (MapSize s in MapSizeRules.All)
                    if (Slot(s).root != null) mask |= 1 << (int)s;
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
            foreach (MapSize s in MapSizeRules.All) SetActiveSafe(Slot(s).root, s == chosen);
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
