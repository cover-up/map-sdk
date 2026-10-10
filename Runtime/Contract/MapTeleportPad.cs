using UnityEngine;

namespace CoverUp.Gameplay
{
    /// <summary>
    /// One end of a <see cref="MapTeleportPair"/>. The transform is the pad: its position is
    /// where the feet stand and land, its +Z is the way a traveller faces on arrival, and
    /// <see cref="Radius"/> is the footprint a player must be standing inside before the Use
    /// prompt appears. The pad's mesh is yours; drop its renderer into <see cref="PadRenderer"/>
    /// and the game tints its emission with the pair colour (a property block, the material is
    /// untouched), and a ground ring in that colour is drawn regardless. Place the pad ON the
    /// floor: a body counts as standing on it only within <see cref="StandHeight"/> of its height.
    ///
    /// Must live under a <see cref="MapTeleportPair"/>, together with exactly one other pad. Pure
    /// data; Docs/MapSdk.md §21. SDK 0.17.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapTeleportPad : MonoBehaviour
    {
        /// <summary>How far above or below the pad's height a body still stands on it, in metres
        /// at scale 1 (the game scales it with the role). A pad on a slope or a step still counts;
        /// a floor above it never does.</summary>
        public const float StandHeight = 0.5f;

        /// <summary>Validate Map's comfort band for the footprint.</summary>
        public const float MinComfortableRadius = 0.6f, MaxComfortableRadius = 3f;

        /// <summary>Validate Map warns when the partner is nearer than this: the arc reads as a hop
        /// across the room, and walking is quicker than the departure.</summary>
        public const float MinUsefulDistance = 8f;

        [SerializeField, Range(0.3f, 5f)]
        [Tooltip("Footprint in metres. A player must be standing inside it to see the prompt, and arrives at its centre.")]
        private float radius = 1f;

        [SerializeField]
        [Tooltip("Optional: the renderer drawing your pad mesh. The game tints its emission with the pair colour " +
                 "and brightens it while someone stands on the pad. The ground ring is drawn either way.")]
        private Renderer padRenderer;

        /// <summary>Footprint in metres (world, not role-scaled).</summary>
        public float Radius => radius;

        /// <summary>The mapper's pad mesh, or null.</summary>
        public Renderer PadRenderer => padRenderer;

        /// <summary>The pair this pad belongs to, or null when it is loose (an authoring error).</summary>
        public MapTeleportPair Pair => GetComponentInParent<MapTeleportPair>(true);

        /// <summary>The other end, or null when loose or the pair is incomplete.</summary>
        public MapTeleportPad Partner
        {
            get { MapTeleportPair pair = Pair; return pair != null ? pair.PartnerOf(this) : null; }
        }

        /// <summary>Where the feet land.</summary>
        public Vector3 Centre => transform.position;

        /// <summary>The way a traveller faces on arrival: the pad's +Z flattened. A pad standing
        /// straight up (or down) faces world +Z.</summary>
        public Vector3 ExitForward
        {
            get
            {
                Vector3 f = transform.forward; f.y = 0f;
                return f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
            }
        }

        /// <summary>Is a body whose feet are at <paramref name="feet"/> standing on this pad?
        /// <paramref name="scale"/> is the body's role scale: the height band grows with the doll,
        /// the footprint does not (the pad is world geometry).</summary>
        public bool Contains(Vector3 feet, float scale = 1f)
        {
            Vector3 c = transform.position;
            float dx = feet.x - c.x, dz = feet.z - c.z;
            return dx * dx + dz * dz <= radius * radius && Mathf.Abs(feet.y - c.y) <= StandHeight * Mathf.Max(0.1f, scale);
        }

        // Scene-view aid: the footprint disc in the pair colour, the exit arrow, and a dashed line to the
        // partner from pad 0 only (so one pair draws one line). Loose pads draw grey. Always on; a pad is
        // small and placing it is the whole job.
        private void OnDrawGizmos()
        {
            if (Application.isPlaying) return;
            MapTeleportPair pair = Pair;
            Color c = pair != null ? pair.Colour : new Color(0.5f, 0.5f, 0.5f);
            c.a = 1f;
            Vector3 centre = transform.position;

            Gizmos.color = c;
            const int Segments = 32;
            Vector3 prev = centre + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= Segments; i++)
            {
                float a = i * (2f * Mathf.PI / Segments);
                Vector3 p = centre + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
            Vector3 fwd = ExitForward;
            Vector3 tip = centre + fwd * (radius + 0.6f);
            Gizmos.DrawLine(centre, tip);
            Vector3 side = Vector3.Cross(Vector3.up, fwd) * 0.2f;
            Gizmos.DrawLine(tip, tip - fwd * 0.3f + side);
            Gizmos.DrawLine(tip, tip - fwd * 0.3f - side);

            if (pair == null || pair.IndexOf(this) != 0) return;
            MapTeleportPad partner = pair.PartnerOf(this);
            if (partner == null) return;
            Gizmos.color = new Color(c.r, c.g, c.b, 0.6f);
            Vector3 from = centre + Vector3.up * 0.5f, to = partner.transform.position + Vector3.up * 0.5f;
            float length = Vector3.Distance(from, to);
            int dashes = Mathf.Max(1, Mathf.RoundToInt(length / 1.5f));
            for (int i = 0; i < dashes; i++)
            {
                float t0 = i / (float)dashes, t1 = (i + 0.5f) / dashes;
                Gizmos.DrawLine(Vector3.Lerp(from, to, t0), Vector3.Lerp(from, to, t1));
            }
        }
    }
}
