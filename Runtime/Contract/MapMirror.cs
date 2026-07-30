using System.Collections.Generic;
using UnityEngine;

namespace CoverUp.Gameplay
{
    /// <summary>
    /// Marks a flat surface as a live planar mirror. Put it on a Unity Quad (or
    /// any flat mesh whose pivot lies on the glass plane); the reflective side
    /// is the face an author sees on a Quad, i.e. the -Z face. Keep a normal
    /// opaque material on the mesh — that is the fallback face, shown whenever
    /// the reflection isn't running: beyond the activation distance, with the
    /// setting off, or on game builds that predate mirrors (there the script
    /// fails to rebind and is stripped, leaving the authored material).
    ///
    /// The component is inert data plus a registry — all rendering lives in the
    /// game's MirrorRenderer, which activates at most ONE mirror at a time (the
    /// nearest eligible one) because each live mirror re-renders the scene.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class MapMirror : MonoBehaviour
    {
        [Tooltip("How close (m) a viewer must be before the live reflection turns on. The game clamps to 5–40.")]
        [Range(5f, 40f)] public float ActivationDistance = 20f;

        /// <summary>Enabled mirrors across all loaded scenes, maintained by
        /// OnEnable/OnDisable so additive map load/unload needs no scan.</summary>
        public static readonly List<MapMirror> Active = new();

        private void OnEnable()  => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        /// <summary>A world-space point on the glass plane (the pivot).</summary>
        public Vector3 PlanePoint => transform.position;

        /// <summary>World-space normal of the reflective face (the visible face
        /// of a Unity Quad, which looks down its -Z axis).</summary>
        public Vector3 PlaneNormal => -transform.forward;
    }
}
