using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CoverUp.Gameplay
{
    /// <summary>
    /// Hand-placeable keep-in volume: a unit cube shaped entirely by its
    /// transform (drag, rotate, scale freely). The union of all volumes in
    /// the ACTIVE scene is the playable space — PlayerMotor snaps the local
    /// blob to the nearest union point whenever a frame ends outside, so a
    /// map is escape-proof exactly when its volumes cover it (forgotten
    /// coverage shows up as unwalkable area, never as an exploit). Place as
    /// many overlapping boxes as the map's shape needs, grouped under a
    /// MapBounds object; a scene with no volumes (the hub) has no
    /// enforcement. Active-scene scoping is what lets the round map stream
    /// in behind the lobby doors: its volumes must not claim players still
    /// standing in the hub, and the active scene flips exactly at the local
    /// player's teleport-in/-out (with the motor suspended for the flight).
    /// </summary>
    public sealed class MapBoundsVolume : MonoBehaviour
    {
        // Live volumes, maintained by enable/disable — additive map loads
        // and unloads keep this current without per-frame scene searches.
        private static readonly List<MapBoundsVolume> Volumes = new List<MapBoundsVolume>();

        /// <summary>Editor-wide gizmo visibility, driven by the
        /// Cover Up!/Maps/Show Bounds Volumes menu toggle.</summary>
        public static bool ShowGizmos = true;

        private void OnEnable() => Volumes.Add(this);
        private void OnDisable() => Volumes.Remove(this);

        /// <summary>True when position is outside every active-scene volume —
        /// clamped is then the nearest point of the union. False when inside
        /// the union, or when the active scene has no volumes (nothing to
        /// enforce — volumes in a streamed-in but not-yet-entered map never
        /// bind).</summary>
        public static bool TryClamp(Vector3 position, out Vector3 clamped)
        {
            clamped = position;
            if (Volumes.Count == 0) return false;
            Scene active = SceneManager.GetActiveScene();
            float best = float.MaxValue;
            foreach (MapBoundsVolume volume in Volumes)
            {
                if (volume.gameObject.scene != active) continue;
                Vector3 local = volume.transform.InverseTransformPoint(position);
                Vector3 inside = new Vector3(
                    Mathf.Clamp(local.x, -0.5f, 0.5f),
                    Mathf.Clamp(local.y, -0.5f, 0.5f),
                    Mathf.Clamp(local.z, -0.5f, 0.5f));
                if (inside == local) return false;
                Vector3 world = volume.transform.TransformPoint(inside);
                float d = (world - position).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    clamped = world;
                }
            }
            return best != float.MaxValue;
        }

        // Scene-view aid while placing: the playable box this volume adds.
        private void OnDrawGizmos()
        {
            if (!ShowGizmos) return;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.45f, 0.15f, 0.12f);
            Gizmos.DrawCube(Vector3.zero, Vector3.one);
            Gizmos.color = new Color(1f, 0.45f, 0.15f, 0.8f);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        }
    }
}
