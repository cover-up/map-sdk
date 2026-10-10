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
        private static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");

        [Header("Movement (optional)")]
        [SerializeField]
        [Tooltip("The renderer drawing this water's surface. With one set, the fields below give the still " +
                 "material a slow drift and the surface a gentle rise and fall, no script in your map. Leave " +
                 "empty for a surface that does not move.")]
        private Renderer surface;

        [SerializeField, Range(0f, 0.2f)]
        [Tooltip("How fast the surface texture (and its normal map) slides, in texture repeats per second. " +
                 "0.01 to 0.03 reads as a pool; 0 is still.")]
        private float flowSpeed = 0.015f;

        [SerializeField, Range(0f, 360f)]
        [Tooltip("Which way the surface slides, degrees clockwise from the map's +Z.")]
        private float flowDirection = 30f;

        [SerializeField, Range(0f, 5f)]
        [Tooltip("How far the surface mesh rises and falls, in centimetres. 0 holds it still.")]
        private float bobCentimetres = 1f;

        [SerializeField, Range(1f, 20f)]
        [Tooltip("Seconds per rise and fall.")]
        private float bobSeconds = 5f;

        /// <summary>The renderer this volume animates, if any.</summary>
        public Renderer Surface => surface;
        public float FlowSpeed => flowSpeed;

        private MaterialPropertyBlock _mpb;
        private Vector4 _baseSt;
        private float _surfaceBaseY;
        private bool _surfaceReady;

        private void Update()
        {
            if (!Application.isPlaying || surface == null) return;
            if (!_surfaceReady)
            {
                _mpb = new MaterialPropertyBlock();
                Material m = surface.sharedMaterial;
                _baseSt = m != null && m.HasProperty(BaseMapSt) ? m.GetVector(BaseMapSt) : new Vector4(1f, 1f, 0f, 0f);
                _surfaceBaseY = surface.transform.localPosition.y;
                _surfaceReady = true;
            }
            if (flowSpeed > 0f)
            {
                float t = Time.time * flowSpeed, rad = flowDirection * Mathf.Deg2Rad;
                Vector2 o = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * t;
                surface.GetPropertyBlock(_mpb);
                _mpb.SetVector(BaseMapSt, new Vector4(_baseSt.x, _baseSt.y, _baseSt.z + o.x, _baseSt.w + o.y));
                surface.SetPropertyBlock(_mpb);
            }
            if (bobCentimetres > 0f)
            {
                Vector3 lp = surface.transform.localPosition;
                lp.y = _surfaceBaseY + Mathf.Sin(Time.time * (2f * Mathf.PI / Mathf.Max(0.1f, bobSeconds))) * bobCentimetres * 0.01f;
                surface.transform.localPosition = lp;
            }
        }

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
