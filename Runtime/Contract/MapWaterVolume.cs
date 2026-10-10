using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CoverUp.Gameplay
{
    /// <summary>
    /// Water you can wade in that is not the sea: a pool, a pond, a fountain basin. A unit cube
    /// shaped entirely by its transform, like <see cref="MapBoundsVolume"/>; its TOP FACE is the
    /// water surface. Inside the footprint and below the top, a player is in water: footsteps
    /// swap to the wading loop, ripples follow them, fish may live there and crabs along the rim.
    /// The mesh and shader of the water are still yours (the game draws nothing here); this
    /// only tells the game where the water IS. The sea needs no volume: <see cref="MapEnvironment"/>
    /// already says where it is. Stack several boxes for an odd-shaped pool.
    ///
    /// SDK 0.16. An older game build ignores it.
    /// </summary>
    public sealed class MapWaterVolume : MonoBehaviour
    {
        private static readonly List<MapWaterVolume> Volumes = new List<MapWaterVolume>();

        /// <summary>Editor-wide gizmo visibility: shares the bounds volumes' menu toggle.</summary>
        public static bool ShowGizmos => MapBoundsVolume.ShowGizmos;

        private void OnEnable() => Volumes.Add(this);
        private void OnDisable() => Volumes.Remove(this);

        /// <summary>World Y of this volume's surface: the top face's centre.</summary>
        public float SurfaceY => transform.TransformPoint(new Vector3(0f, 0.5f, 0f)).y;

        /// <summary>World Y of the bottom at the centre.</summary>
        public float FloorY => transform.TransformPoint(new Vector3(0f, -0.5f, 0f)).y;

        /// <summary>True when (x, z) lies inside this volume's footprint, read at the volume's mid height.</summary>
        public bool CoversXZ(float x, float z)
        {
            Vector3 local = transform.InverseTransformPoint(new Vector3(x, transform.position.y, z));
            return Mathf.Abs(local.x) <= 0.5f && Mathf.Abs(local.z) <= 0.5f;
        }

        /// <summary>Every enabled volume in the active scene. The active-scene rule is the same as the
        /// bounds': a map streamed in behind the lobby doors must not claim hub walkers.</summary>
        public static int CollectActive(List<MapWaterVolume> into)
        {
            into.Clear();
            if (Volumes.Count == 0) return 0;
            Scene active = SceneManager.GetActiveScene();
            foreach (MapWaterVolume v in Volumes)
                if (v != null && v.gameObject.scene == active) into.Add(v);
            return into.Count;
        }

        /// <summary>All enabled volumes in the given scene, any scene (for a bake over a loaded map).</summary>
        public static int CollectIn(Scene scene, List<MapWaterVolume> into)
        {
            into.Clear();
            foreach (MapWaterVolume v in Volumes)
                if (v != null && v.gameObject.scene == scene) into.Add(v);
            return into.Count;
        }

        /// <summary>The surface over (x, z) among the active volumes, the highest when boxes stack;
        /// false when no volume covers the spot.</summary>
        public static bool TrySurfaceActive(float x, float z, out float surfaceY, out float floorY)
        {
            surfaceY = float.MinValue; floorY = float.MaxValue; bool any = false;
            Scene active = SceneManager.GetActiveScene();
            foreach (MapWaterVolume v in Volumes)
            {
                if (v == null || v.gameObject.scene != active || !v.CoversXZ(x, z)) continue;
                surfaceY = Mathf.Max(surfaceY, v.SurfaceY); floorY = Mathf.Min(floorY, v.FloorY); any = true;
            }
            return any;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!ShowGizmos || Application.isPlaying) return;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.15f, 0.55f, 0.9f, 0.18f);
            Gizmos.DrawCube(Vector3.zero, Vector3.one);
            Gizmos.color = new Color(0.15f, 0.55f, 0.9f, 0.9f);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
            // the surface, drawn heavier: that is the face that matters
            Gizmos.DrawWireCube(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 0.002f, 1f));
        }
#endif
    }
}
