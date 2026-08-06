using CoverUp.Core;   // GameScale kept the CoverUp.Core namespace when it moved into the package
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CoverUp.Gameplay
{
    /// <summary>Which role's authored scale a <see cref="MapReferenceDoll"/> stands in for.</summary>
    public enum MapDollRole : byte { Hider = 0, Hunter = 1 }

    /// <summary>
    /// A stand-in body at the map's authored player height, for eyeballing scale while
    /// building: drop one beside a doorway, on a shelf, at the end of a sightline, and
    /// see whether a hider actually fits. Height comes from the scene's
    /// <see cref="MapConfig"/> for this doll's role — drag the scale slider and every
    /// doll in the scene resizes with it, because nothing here is baked (Docs/MapSdk.md:
    /// read scale live at the point of use, never serialize it).
    ///
    /// ENTIRELY A GIZMO. There is no mesh, renderer or collider — the silhouette exists
    /// only in <see cref="OnDrawGizmos"/>, which the engine never calls outside the
    /// editor. That is the design, not an optimisation: a scale reference must be
    /// incapable of reaching a player's screen. Belt and braces on top of that, the
    /// GameObject force-tags itself <c>EditorOnly</c> so Unity's build pipeline strips
    /// it from the exported bundle outright, and Validate Map errors if that tag is
    /// missing. Even if all of that failed, what shipped would be an empty transform
    /// carrying an inert component — never a mannequin standing in someone's map.
    ///
    /// Place them anywhere; they are exempt from the Base/Fixtures/Content rules
    /// precisely because they never ship. The starter map stands one in each of its two
    /// spawn discs — the hider doll where hiders land, the hunter doll where hunters do —
    /// so the very first thing you see is how big each side is where that side arrives.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Cover Up!/Map Reference Doll")]
    public sealed class MapReferenceDoll : MonoBehaviour
    {
        /// <summary>Unity's built-in tag for objects stripped at build time. Present in
        /// every project, so a mapper's project needs no tag-manager setup.</summary>
        public const string EditorOnlyTag = "EditorOnly";

        [SerializeField]
        [Tooltip("Which of the map's two authored scales this doll shows. Hider and hunter " +
                 "size independently (MapConfig), so a map can put mouse-sized hiders in front " +
                 "of a human-sized hunter — place one of each to see the difference in place.")]
        private MapDollRole role = MapDollRole.Hider;

        // Editor-only convenience: found lazily and re-found whenever it goes stale, so a
        // doll keeps working through undo, scene reload and a MapConfig added after it.
        private MapConfig _config;

        public MapDollRole Role => role;

        /// <summary>This doll's real-world height in metres — the authored scale for its
        /// role, converted the same way the MapConfig inspector's readout does. Falls back
        /// to the default scale in a scene that has no MapConfig yet.</summary>
        public float HeightMeters
        {
            get
            {
                MapConfig cfg = Config();
                float scale = cfg == null
                    ? GameScale.Default
                    : (role == MapDollRole.Hunter ? cfg.HunterScale : cfg.HiderScale);
                return GameScale.ApproxHeightMeters(Mathf.Clamp(scale, GameScale.MinScale, GameScale.MaxScale));
            }
        }

        /// <summary>The colour this role draws in — shared with the editor label so the
        /// two can't drift. Hider cool, hunter warm.</summary>
        public Color RoleColor => role == MapDollRole.Hunter
            ? new Color(1f, 0.58f, 0.25f, 0.9f)
            : new Color(0.45f, 0.95f, 0.6f, 0.9f);

        private MapConfig Config()
        {
            if (_config != null) return _config;
            Scene scene = gameObject.scene;
            foreach (MapConfig c in FindObjectsByType<MapConfig>(FindObjectsInactive.Include))
            {
                if (c.gameObject.scene == scene) { _config = c; break; }
            }
            return _config;
        }

        private void OnValidate() => EnsureEditorOnly();
        private void Reset() => EnsureEditorOnly();

        // Self-heal the tag rather than only complaining about it: a mapper who
        // duplicates a prefab or hand-clears the tag would otherwise ship the object.
        // Validate Map still errors, for the case where this never got a chance to run.
        private void EnsureEditorOnly()
        {
            if (Application.isPlaying) return;
            if (!gameObject.CompareTag(EditorOnlyTag)) gameObject.tag = EditorOnlyTag;
        }

        // The silhouette: humanoid proportions off a single height, so it reads as a
        // person rather than a capsule, plus a footprint ring and a height tick so it
        // also reads as a measurement. Drawn in the doll's own position/rotation but
        // NOT its scale — scale comes from MapConfig, and letting the transform's own
        // scale in would quietly make it lie.
        private void OnDrawGizmos()
        {
            float h = HeightMeters;
            if (h <= 0f) return;

            Matrix4x4 prev = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.color = RoleColor;

            float headR = h * 0.115f;                 // head radius
            float headY = h - headR;                  // head centre
            float shoulderY = h * 0.80f;
            float hipY = h * 0.47f;
            float halfShoulder = h * 0.115f;
            float halfHip = h * 0.085f;

            Gizmos.DrawWireSphere(new Vector3(0f, headY, 0f), headR);

            // Torso as a quad rather than a box: from the side a box reads as bulk the
            // doll doesn't have, and the mapper is judging silhouette width against a gap.
            var lShoulder = new Vector3(-halfShoulder, shoulderY, 0f);
            var rShoulder = new Vector3(halfShoulder, shoulderY, 0f);
            var lHip = new Vector3(-halfHip, hipY, 0f);
            var rHip = new Vector3(halfHip, hipY, 0f);
            Gizmos.DrawLine(lShoulder, rShoulder);
            Gizmos.DrawLine(lHip, rHip);
            Gizmos.DrawLine(lShoulder, lHip);
            Gizmos.DrawLine(rShoulder, rHip);
            Gizmos.DrawLine(new Vector3(0f, shoulderY, 0f), new Vector3(0f, headY - headR, 0f));

            // Arms down the sides (A-pose-ish), legs to the floor.
            Gizmos.DrawLine(lShoulder, new Vector3(-halfShoulder - h * 0.03f, hipY - h * 0.06f, 0f));
            Gizmos.DrawLine(rShoulder, new Vector3(halfShoulder + h * 0.03f, hipY - h * 0.06f, 0f));
            Gizmos.DrawLine(lHip, new Vector3(-halfHip, 0f, 0f));
            Gizmos.DrawLine(rHip, new Vector3(halfHip, 0f, 0f));

            // Footprint ring + height tick: the measuring half of the tool.
            DrawGroundRing(h * 0.20f);
            Gizmos.color = new Color(RoleColor.r, RoleColor.g, RoleColor.b, 0.35f);
            Gizmos.DrawLine(Vector3.zero, new Vector3(0f, h, 0f));
            Gizmos.DrawLine(new Vector3(-h * 0.06f, h, 0f), new Vector3(h * 0.06f, h, 0f));

            Gizmos.matrix = prev;
        }

        private static void DrawGroundRing(float radius)
        {
            const int segments = 32;
            Vector3 prev = new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                var next = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
