using CoverUp.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CoverUp.Gameplay
{
    /// <summary>
    /// Which camera the HUNTER plays a map in. A per-map authoring lever, because the
    /// right answer is a property of the space: cramped corridors read better down the
    /// barrel, open arenas need the peripheral vision of a boom camera.
    ///
    /// <see cref="Auto"/> leaves it to the player — the seeker's own toggle, which is
    /// the shipped behaviour. The forced modes take the toggle away for that map.
    /// Hiders are unaffected: they are always third person.
    /// </summary>
    public enum MapHunterCamera : byte
    {
        /// <summary>The player chooses and can toggle freely. Default.</summary>
        Auto = 0,
        /// <summary>Force the over-the-shoulder camera; the toggle is disabled.</summary>
        ThirdPerson = 1,
        /// <summary>Force down-the-barrel; the toggle is disabled.</summary>
        FirstPerson = 2,
    }

    /// <summary>
    /// How much ambient wildlife the game seeds on this map (SDK 0.16). The game finds WHERE from
    /// the map's own geometry: beaches for crabs, water for fish, open ground for the small
    /// animals. Nothing is authored; this is the one knob. The player's own Island Life switch can
    /// still turn all of it off.
    /// </summary>
    public enum MapWildlife : byte
    {
        /// <summary>No critters at all.</summary>
        None = 0,
        /// <summary>Half the normal count.</summary>
        Sparse = 1,
        /// <summary>The lobby island's density, scaled by this map's land area. Default.</summary>
        Normal = 2,
        /// <summary>Twice the normal count.</summary>
        Lush = 3,
    }

    /// <summary>
    /// Where gulls may fly on this map (SDK 0.16). A bird in the sky draws a seeker's eye, so
    /// the mapper decides.
    /// </summary>
    public enum MapGulls : byte
    {
        /// <summary>No gulls.</summary>
        None = 0,
        /// <summary>Circle over the sea and perch only outside the land. Default.</summary>
        Offshore = 1,
        /// <summary>The lobby island's behaviour: perch anywhere flat.</summary>
        Everywhere = 2,
    }

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

        [SerializeField]
        [Tooltip("Which camera the hunter plays this map in. Auto lets the player toggle (the " +
                 "shipped behaviour); the forced modes take the toggle away for this map — use them " +
                 "when the space only reads one way. Hiders are always third person regardless.")]
        private MapHunterCamera hunterCamera = MapHunterCamera.Auto;

        [SerializeField]
        [ColorUsage(false)]
        [Tooltip("A subtle tint this room's flat ambient carries, so the bløb catches the room's " +
                 "colour without losing its white identity. White (default) = the global neutral " +
                 "standard. Push it toward the room's dominant hue; only hue/chroma is used and the " +
                 "SATURATION is clamped to a hint (the ambient LEVEL, and so the lit:shadow ratio, " +
                 "stays globally fixed). Baked by Apply Arena Lighting and re-clamped at load.")]
        private Color ambientTint = Color.white;

        [SerializeField]
        [Tooltip("The game draws everything outside the map's bounds volumes in greyscale, so players " +
                 "see where the playable space ends. Tick this to keep full colour out there instead. " +
                 "Sky and water are never greyed. A map with no bounds volumes is never greyed.")]
        private bool colourOutsideBounds = false;

        [SerializeField]
        [Tooltip("How much ambient wildlife the game seeds here: crabs on beaches, fish in water, small " +
                 "animals on open ground, all found from your geometry. None for a quiet map, Lush for " +
                 "twice the lobby island's density. The player's own Island Life switch can still turn it off.")]
        private MapWildlife wildlife = MapWildlife.Normal;

        [SerializeField]
        [Tooltip("Where gulls may fly. Offshore keeps them over the sea and off the land, so they never " +
                 "draw a seeker's eye to a hiding spot. None removes them. Everywhere lets them perch on " +
                 "anything flat, as on the lobby island.")]
        private MapGulls gulls = MapGulls.Offshore;

        /// <summary>True when the map opted out of the grey world outside its bounds.</summary>
        public bool ColourOutsideBounds => colourOutsideBounds;

        /// <summary>The authored per-map hider scale, before clamping.</summary>
        public float HiderScale => hiderScale;

        /// <summary>The per-map ambient tint (hue/chroma only; saturation is clamped
        /// at apply/load). White = the global neutral standard.</summary>
        public Color AmbientTint => ambientTint;

        /// <summary>The authored per-map hunter scale, before clamping.</summary>
        public float HunterScale => hunterScale;

        /// <summary>How much ambient wildlife this map asks for.</summary>
        public MapWildlife Wildlife => wildlife;

        /// <summary>Where gulls may fly on this map.</summary>
        public MapGulls Gulls => gulls;

        /// <summary>The camera this map forces on the hunter, or
        /// <see cref="MapHunterCamera.Auto"/> to leave the player's toggle alone.</summary>
        public MapHunterCamera HunterCamera => hunterCamera;

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
