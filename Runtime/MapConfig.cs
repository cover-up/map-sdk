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
    /// The two roles size independently, so a map can pit mouse-sized hiders
    /// against a human-sized hunter. Both default to <see cref="GameScale.Default"/>,
    /// which is the classic single-scale behaviour.
    ///
    /// A very early execution order guarantees this wins the Awake race, so the
    /// scale is live before any player, camera or gun reads it. Custom maps carry
    /// their own values; a scene without a MapConfig runs at the defaults.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class MapConfig : MonoBehaviour
    {
        [SerializeField]
        [Range(GameScale.MinScale, GameScale.MaxScale)]
        [Tooltip("Hider doll height relative to the 1:1 world. Reference: 1.185 ≈ human height (default), " +
                 "0.23 ≈ ~1 foot. Scales the WHOLE hider — body, collider, movement, camera and gun — " +
                 "not just the mesh, and it alone drives the exposure rings and scoring distances. " +
                 "See the MapConfig inspector for a live height readout and presets.")]
        private float hiderScale = GameScale.Default;

        [SerializeField]
        [Range(GameScale.MinScale, GameScale.MaxScale)]
        [Tooltip("Hunter doll height relative to the 1:1 world, authored independently of the hider. " +
                 "Scales the hunter's body, collider, movement, camera and gun. NOTE a bigger hunter " +
                 "also moves proportionally faster; it does NOT affect exposure scoring.")]
        private float hunterScale = GameScale.Default;

        /// <summary>The authored per-map hider scale, before clamping.</summary>
        public float HiderScale => hiderScale;

        /// <summary>The authored per-map hunter scale, before clamping.</summary>
        public float HunterScale => hunterScale;

        // Solo path (Play → map loaded single/active): apply immediately, as
        // always. Additive multiplayer path: the scene is NOT active at Awake —
        // MapLoader.ApplyMapScale runs at the door-open transition instead, so
        // players standing in the hub don't rescale mid-lobby.
        private void Awake()
        {
            if (gameObject.scene == SceneManager.GetActiveScene())
            {
                GameScale.SetPlayerScales(hiderScale, hunterScale);
            }
        }

        // Editor live-preview: keep the running scales in sync while dragging the
        // sliders so the in-scene doll re-sizes to match.
        private void OnValidate() => GameScale.SetPlayerScales(hiderScale, hunterScale);
    }
}
