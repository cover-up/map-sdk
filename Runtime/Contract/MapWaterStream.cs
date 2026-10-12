using System.Collections.Generic;
using UnityEngine;

namespace CoverUp.Gameplay
{
    /// <summary>
    /// A stream of running water: a fountain's spout, a pipe, a gargoyle, a little waterfall. The water
    /// starts at this transform, runs along the optional <see cref="Channel"/> points in order (the inside
    /// of a mouth, a chute, a gutter), then leaves the last point and falls along a true arc until it meets
    /// a collider or the surface of a <see cref="MapWaterVolume"/>. With no channel it leaves along this
    /// transform's +Z. The game draws everything: the running water, drops breaking off it, a splash and
    /// spreading rings where it lands, and (with <see cref="Sound"/>) a soft trickle there.
    ///
    /// Pure data, like every marker in this package. Visual only: nothing collides with the water, and the
    /// player's Particle effects setting thins the drops and the splash. Under a size root the stream exists
    /// only at that size. Docs/MapSdk.md §23.
    ///
    /// SDK 0.21. An older game build ignores it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapWaterStream : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Optional points the water runs along before it falls, in order, starting from this object " +
                 "(the inside of a mouth, a chute). The water leaves the last point in the direction of the last " +
                 "segment. Empty: it leaves this object along its blue +Z arrow.")]
        private Transform[] channel = new Transform[0];

        [SerializeField, Range(0.005f, 0.3f)]
        [Tooltip("The stream's radius where it leaves, in metres. It thins as it speeds up while falling.")]
        private float radius = 0.025f;

        [SerializeField, Range(0.1f, 8f)]
        [Tooltip("How fast the water leaves, in metres per second. A spout is about 1, a hose 4.")]
        private float speed = 1f;

        [SerializeField]
        [Tooltip("A soft trickle where the water lands, heard close up.")]
        private bool sound = true;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("How loud the trickle is.")]
        private float volume = 0.6f;

        public IReadOnlyList<Transform> Channel => channel;
        public float Radius => radius;
        public float Speed => speed;
        public bool Sound => sound;
        public float Volume => volume;

        /// <summary>The fall's step and limit: the arc is walked in small time steps until something is met or
        /// it has dropped this far.</summary>
        public const float StepSeconds = 0.01f, MaxDrop = 12f;

        /// <summary>The water's path in world space: this object, the channel points, then the arc to where it
        /// lands (<paramref name="landed"/> false when it fell <see cref="MaxDrop"/> without meeting anything).
        /// Used by the game to draw it and by the gizmo, so both agree.</summary>
        public void Path(List<Vector3> into, out bool landed, out Vector3 exitVelocity)
        {
            into.Clear(); into.Add(transform.position);
            foreach (var c in channel) if (c != null) into.Add(c.position);
            Vector3 dir = into.Count >= 2 ? (into[into.Count - 1] - into[into.Count - 2]).normalized : transform.forward;
            if (dir.sqrMagnitude < 1e-6f) dir = transform.forward;
            exitVelocity = dir * speed;
            Vector3 p = into[into.Count - 1], v = exitVelocity; float startY = p.y; landed = false;
            var vols = new List<MapWaterVolume>(); MapWaterVolume.CollectActive(vols);
            if (vols.Count == 0) foreach (var w in FindObjectsByType<MapWaterVolume>(FindObjectsSortMode.None)) if (w.gameObject.scene == gameObject.scene) vols.Add(w);
            for (int i = 0; i < 400; i++)
            {
                Vector3 nv = v + Physics.gravity * StepSeconds, np = p + (v + nv) * 0.5f * StepSeconds;
                Vector3 d = np - p; float len = d.magnitude;
                // the first few centimetres are skipped so the spout's own lip does not stop the water
                if (i > 2 && len > 1e-5f && Physics.Raycast(p, d / len, out RaycastHit hit, len, ~0, QueryTriggerInteraction.Ignore))
                { into.Add(hit.point); landed = true; return; }
                foreach (var w in vols)
                    if (w != null && w.CoversXZ(np.x, np.z) && np.y <= w.SurfaceY && p.y > w.SurfaceY)
                    { float t = (p.y - w.SurfaceY) / Mathf.Max(1e-5f, p.y - np.y); into.Add(Vector3.Lerp(p, np, t)); landed = true; return; }
                p = np; v = nv; into.Add(p);
                if (startY - p.y > MaxDrop) return;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            var pts = new List<Vector3>(); Path(pts, out bool landed, out _);
            Gizmos.color = new Color(0.4f, 0.75f, 1f, 0.9f);
            for (int i = 1; i < pts.Count; i++) Gizmos.DrawLine(pts[i - 1], pts[i]);
            if (landed) Gizmos.DrawWireSphere(pts[pts.Count - 1], 0.05f);
        }
#endif
    }
}
