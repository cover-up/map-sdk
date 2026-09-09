using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CoverUp.Gameplay
{
    /// <summary>Which side a <see cref="MapKeepOutVolume"/> holds back.</summary>
    public enum MapKeepOutRole : byte
    {
        /// <summary>Seekers may not enter; hiders walk through as if it were not
        /// there. The default, and the reason the volume exists.</summary>
        Hunters = 0,
        /// <summary>Hiders may not enter; seekers walk through.</summary>
        Hiders = 1,
        /// <summary>Nobody may enter: a hole in the playable space.</summary>
        Everyone = 2,
    }

    /// <summary>
    /// Hand-placeable KEEP-OUT volume, the mirror image of <see cref="MapBoundsVolume"/>:
    /// a unit cube shaped entirely by its transform (drag, rotate, scale freely) that
    /// one side may not enter. The bounds union says where you may be; a keep-out
    /// says where your role may not be, and it wins over the union.
    ///
    /// The case it exists for is a velvet rope. In an art-gallery map the hiders
    /// blend into the paintings, and a bløb on a flat canvas is given away the
    /// moment a seeker looks at it from a steep angle. A keep-out three metres deep
    /// in front of the picture holds the seekers back where the silhouette still
    /// reads as part of the painting, while the hiders walk up to the canvas and
    /// onto it. Shots and sight pass, so a hider in the rope zone can still be found
    /// from where the seeker stands; the rope moves the fight, it does not end it.
    ///
    /// A VOLUME, NOT A COLLIDER, on purpose. A collider blocks every
    /// CharacterController alike, and filtering one by role would mean a physics
    /// layer, which an AssetBundle serializes as a bare index that a map project's
    /// layer 12 does not share with the game's. So it is enforced the way the
    /// bounds are: every frame the local motor asks <see cref="TryPushOut"/> for a
    /// correction and applies it through the controller, so real walls still count.
    /// The push goes to the FACE YOU CAME IN THROUGH, and your movement along that
    /// face is kept, so walking into a rope slides you along it rather than bouncing
    /// you off. Choosing the entry face over the nearest one is what stops a
    /// sprinting, thrice-scaled seeker from tunnelling through a thin zone in one
    /// frame. Only when the frame started inside as well (a hider converted mid-round
    /// inside a pocket, or a disc placed inside a volume) does the nearest face win.
    ///
    /// Active-scene scoped like the bounds: a round map streams in behind the lobby
    /// doors and must not claim bodies still standing in the hub. Unlike the bounds
    /// a keep-out may live in Base (live at every size) or in a size root (that size
    /// only), because subtracting from a union is safe where adding to it is not.
    ///
    /// What it does NOT change: shots, the camera boom, the float, exposure scoring.
    /// None of them can see a volume, and a rope zone with a slit is a camping spot,
    /// which is the mapper's call and is said so in the creator guide. Enforced on
    /// the local body only, exactly like the bounds; the host validates distance,
    /// not geometry.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapKeepOutVolume : MonoBehaviour
    {
        // Live volumes, maintained by enable/disable, so additive map loads and
        // unloads keep this current without per-frame scene searches.
        private static readonly List<MapKeepOutVolume> Volumes = new List<MapKeepOutVolume>();

        /// <summary>World-space clearance the push leaves outside the face, so the
        /// next frame's start position reads as outside and the entry face stays
        /// known. One centimetre: under the controller's own skin width.</summary>
        public const float Skin = 0.01f;

        // Overlapping volumes: a push out of one can land inside a neighbour. A
        // few passes settle the shapes a mapper actually builds (an L of two
        // boxes, a rope that meets a wall's keep-out); anything left waits a frame.
        private const int MaxPasses = 3;

        [Tooltip("Which side this volume holds back. The other side walks through it as if it were not there.")]
        [SerializeField] private MapKeepOutRole keepsOut = MapKeepOutRole.Hunters;

        /// <summary>Which side this volume holds back.</summary>
        public MapKeepOutRole KeepsOut => keepsOut;

        /// <summary>Editor-wide gizmo visibility, shared with the bounds volumes
        /// (the Cover Up!/Maps/Show Bounds Volumes menu toggle).</summary>
        public static bool ShowGizmos => MapBoundsVolume.ShowGizmos;

        private void OnEnable() => Volumes.Add(this);
        private void OnDisable() => Volumes.Remove(this);

        /// <summary>Does this volume hold back a body of that role?</summary>
        public bool Applies(bool hunter) =>
            keepsOut == MapKeepOutRole.Everyone
            || keepsOut == (hunter ? MapKeepOutRole.Hunters : MapKeepOutRole.Hiders);

        /// <summary>The same question asked of a spawn role: does a volume of this
        /// kind hold back someone who lands as that side? Both means either side
        /// could land, so any keep-out applies.</summary>
        public bool Applies(MapSpawnRole landing) =>
            landing == MapSpawnRole.Both
                ? true
                : Applies(landing == MapSpawnRole.Hunters);

        /// <summary>
        /// True when <paramref name="to"/> is inside an active-scene volume that
        /// applies to this role; <paramref name="pushed"/> is then the corrected
        /// position, just outside the face the body entered through. False when
        /// nothing needs doing, or when the active scene has no volumes (nothing to
        /// enforce; volumes in a streamed-in but not-yet-entered map never bind).
        /// </summary>
        /// <param name="from">Where the frame started. Chooses the entry face; pass
        /// the same point as <paramref name="to"/> to ask for the nearest face.</param>
        /// <param name="to">Where the frame ended.</param>
        /// <param name="hunter">Is the body a seeker?</param>
        public static bool TryPushOut(Vector3 from, Vector3 to, bool hunter, out Vector3 pushed)
        {
            pushed = to;
            if (Volumes.Count == 0) return false;
            Scene active = SceneManager.GetActiveScene();
            bool moved = false;
            for (int pass = 0; pass < MaxPasses; pass++)
            {
                bool any = false;
                foreach (MapKeepOutVolume volume in Volumes)
                {
                    if (volume.gameObject.scene != active || !volume.Applies(hunter)) continue;
                    if (!volume.PushOut(from, pushed, out Vector3 p)) continue;
                    pushed = p;
                    any = moved = true;
                }
                if (!any) break;
            }
            return moved;
        }

        /// <summary>
        /// This one volume's correction for a frame that ran <paramref name="from"/>
        /// → <paramref name="to"/>. Public for the editor's own checks and tests; the
        /// motor goes through <see cref="TryPushOut"/>. Role is not consulted here.
        /// </summary>
        public bool PushOut(Vector3 from, Vector3 to, out Vector3 pushed)
        {
            pushed = to;
            Vector3 scale = transform.lossyScale;
            if (Mathf.Approximately(scale.x, 0f) || Mathf.Approximately(scale.y, 0f) || Mathf.Approximately(scale.z, 0f))
                return false;   // a flat volume holds nothing, and its inverse is not a number

            Vector3 b = transform.InverseTransformPoint(to);
            if (!Inside(b)) return false;

            int axis = -1;
            float face = 0f;
            Vector3 a = transform.InverseTransformPoint(from);
            if (!Inside(a))
            {
                // Entry face: the slab method. Walking a→b, the box is entered on
                // the axis whose slab is crossed LAST, and that crossing is the face
                // to go back to. Axes where a already sat between the faces never
                // qualify; an axis where it did not must be moving inward, or b
                // could not be inside.
                float tBest = -1f;
                for (int i = 0; i < 3; i++)
                {
                    float d = b[i] - a[i];
                    float t, side;
                    if (a[i] >= 0.5f && d < 0f) { t = (a[i] - 0.5f) / -d; side = 0.5f; }
                    else if (a[i] <= -0.5f && d > 0f) { t = (-0.5f - a[i]) / d; side = -0.5f; }
                    else continue;
                    if (t > tBest) { tBest = t; axis = i; face = side; }
                }
            }
            if (axis < 0)
            {
                // Started inside too: nearest face, in WORLD metres so a long thin
                // rope pushes out of its thin side rather than its long one.
                float best = float.MaxValue;
                for (int i = 0; i < 3; i++)
                {
                    float unit = Mathf.Abs(scale[i]);
                    float toPos = (0.5f - b[i]) * unit, toNeg = (b[i] + 0.5f) * unit;
                    if (toPos < best) { best = toPos; axis = i; face = 0.5f; }
                    if (toNeg < best) { best = toNeg; axis = i; face = -0.5f; }
                }
            }

            Vector3 local = b;
            local[axis] = face;   // onto the face, movement along it kept
            Vector3 outward = Vector3.zero;
            outward[axis] = Mathf.Sign(face) * Mathf.Sign(scale[axis]);
            Vector3 normal = transform.TransformDirection(outward).normalized;
            pushed = transform.TransformPoint(local) + normal * Skin;
            return true;
        }

        // Strictly inside: a point ON a face is outside, which is where every push
        // leaves you, and what lets the next frame's start choose the entry face.
        private static bool Inside(Vector3 local) =>
            Mathf.Abs(local.x) < 0.5f && Mathf.Abs(local.y) < 0.5f && Mathf.Abs(local.z) < 0.5f;

        // Scene-view aid while placing: the box this volume closes to its role.
        // Red holds back seekers, blue holds back hiders, dark grey holds back
        // everyone. Same fill-plus-wire style as the orange bounds, and the same
        // menu toggle hides both.
        private void OnDrawGizmos()
        {
            if (!ShowGizmos) return;
            Color c = keepsOut == MapKeepOutRole.Hunters ? new Color(0.90f, 0.20f, 0.25f)
                : keepsOut == MapKeepOutRole.Hiders ? new Color(0.25f, 0.50f, 0.95f)
                : new Color(0.30f, 0.30f, 0.32f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(c.r, c.g, c.b, 0.14f);
            Gizmos.DrawCube(Vector3.zero, Vector3.one);
            Gizmos.color = new Color(c.r, c.g, c.b, 0.85f);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        }
    }
}
