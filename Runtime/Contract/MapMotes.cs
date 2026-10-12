using UnityEngine;

namespace CoverUp.Gameplay
{
    /// <summary>
    /// Drifting motes of light over a round patch of the map: soft glowing dots that rise
    /// slowly, wander and fade, like the ones over a bouncy mushroom on the hub island. Put
    /// one where a place should feel alive or a little magical: an oasis, a shrine, a glade.
    /// The disc is centred on this transform, flat in world XZ; the motes are born across it
    /// and rise through <see cref="Height"/> metres before they fade.
    ///
    /// Pure data, like every marker in this package: the game draws the motes, and the
    /// player's Particle effects setting thins them or turns them off. Under a size root the
    /// motes exist only at that size; under Base at every size. Docs/MapSdk.md §22.
    ///
    /// SDK 0.20. An older game build ignores it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapMotes : MonoBehaviour
    {
        [SerializeField, Range(0.5f, 30f)]
        [Tooltip("Radius of the patch the motes are born across, in metres.")]
        private float radius = 4f;

        [SerializeField, Range(0.3f, 10f)]
        [Tooltip("How high the motes rise above this transform before they fade, in metres.")]
        private float height = 2f;

        [SerializeField]
        [Tooltip("The motes' colour. Warm yellow reads as magic in daylight; alpha sets how strong they glow.")]
        private Color colour = new Color(1f, 0.86f, 0.38f, 0.9f);

        [SerializeField, Range(0f, 1f)]
        [Tooltip("How many: 0 none, 0.5 a gentle drift, 1 a busy swarm. The player's Particle effects setting scales this further.")]
        private float amount = 0.5f;

        /// <summary>Patch radius in metres (world).</summary>
        public float Radius => radius;
        /// <summary>Rise in metres (world).</summary>
        public float Height => height;
        public Color Colour => colour;
        public float Amount => amount;

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(colour.r, colour.g, colour.b, 0.8f);
            Vector3 c = transform.position; const int n = 40;
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
                Vector3 p0 = c + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius, p1 = c + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
                Gizmos.DrawLine(p0, p1); Gizmos.DrawLine(p0 + Vector3.up * height, p1 + Vector3.up * height);
            }
        }
#endif
    }
}
