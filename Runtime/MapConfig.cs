using CoverUp.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CoverUp.Gameplay
{
    /// <summary>
    /// Per-map player scale. Drop one on a map/scene root and the mapper sets how
    /// big the player dolls are relative to the (always 1:1) world — driving size,
    /// speed, jump, climb reaches, gun and camera framing through <see cref="GameScale"/>.
    ///
    /// A very early execution order guarantees this wins the Awake race, so the
    /// scale is live before any player, camera or gun reads it. Custom maps carry
    /// their own value; a scene without a MapConfig runs at <see cref="GameScale.Default"/>.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class MapConfig : MonoBehaviour
    {
        [SerializeField]
        [Range(GameScale.MinScale, GameScale.MaxScale)]
        [Tooltip("Doll height relative to the 1:1 world. Reference: 1.185 ≈ human height (default), " +
                 "0.23 ≈ ~1 foot. Scales the WHOLE player — body, collider, movement, camera and gun — " +
                 "not just the mesh. See the MapConfig inspector for a live height readout and presets.")]
        private float playerScale = GameScale.Default;

        /// <summary>The authored per-map scale, before clamping.</summary>
        public float PlayerScale => playerScale;

        // Solo path (Play → map loaded single/active): apply immediately, as
        // always. Additive multiplayer path: the scene is NOT active at Awake —
        // MapLoader.ApplyMapScale runs at the door-open transition instead, so
        // players standing in the hub don't rescale mid-lobby.
        private void Awake()
        {
            if (gameObject.scene == SceneManager.GetActiveScene())
            {
                GameScale.SetPlayerScale(playerScale);
            }
        }

        // Editor live-preview: keep the running scale in sync while dragging the
        // slider so the in-scene doll re-sizes to match.
        private void OnValidate() => GameScale.SetPlayerScale(playerScale);
    }
}
