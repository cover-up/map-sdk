namespace CoverUp.Core
{
    /// <summary>
    /// Size of a player doll relative to the human-scale world. The world (rooms,
    /// props, furniture) stays 1:1 real scale; players are small, so everything
    /// player-side authored in world-space meters is multiplied by this factor:
    /// collider dims, body mesh, the gun and its particles, camera framing,
    /// climb/prone reaches, and movement (speeds, jump AND gravity).
    ///
    /// The two roles size INDEPENDENTLY. A map authors a hider scale and a hunter
    /// scale (its <c>MapConfig</c>), so it can put mouse-sized hiders in front of a
    /// human-sized hunter — or the reverse. Both default to the same value, so a
    /// map that doesn't care behaves exactly as a single-scale map did.
    ///
    /// Scaling velocity and gravity by the same factor keeps the game-feel timing
    /// (hang-time, acceleration) identical while shrinking the distances, so the
    /// doll moves in proportion to its body without going floaty. Only angular
    /// rates, durations and the world itself stay put. NOTE the consequence of
    /// per-role scale: a hunter authored twice a hider's size also covers ground
    /// twice as fast, in the same stride rhythm.
    ///
    /// Read it live at the point of use — never bake it into a serialized field, or
    /// a per-map change (or a re-scale) silently won't apply.
    ///
    /// WHICH accessor: <see cref="Local"/> for anything sized for the player at
    /// this keyboard (camera, motor, own gun, prompts, reach) — it resolves the
    /// local role for you. <see cref="Hider"/>/<see cref="Hunter"/> where the role
    /// is known explicitly: a named remote avatar, a host-side validation of one
    /// peer, or a rule that belongs to one role. The whole exposure system is
    /// hider-scaled (the rings are bubbles measured AT the hider), so hunter scale
    /// never perturbs scoring.
    /// </summary>
    public static class GameScale
    {
        // Human height (1.6 m) ÷ the authored 1.35 m body ≈ 1.185. Speed, jump,
        // gravity, reaches and camera all follow this factor (read live), so
        // changing it here re-tunes the whole feel to the new size. A map that
        // wants the classic ~1-foot dolls sets its own MapConfig scales (0.23).
        public const float Default = 1.185f;

        // Guard rails for mapper-supplied values: never zero/negative, never absurd.
        public const float MinScale = 0.05f;
        public const float MaxScale = 2f;

        // The body mesh is authored at 1.35 m tall, so a doll's real-world height
        // is scale × this (Default 1.185 ≈ 1.6 m human; 0.23 ≈ ~1 foot). Used to
        // show mappers what a scale value MEANS rather than a bare ratio.
        public const float AuthoredBodyMeters = 1.35f;
        public static float ApproxHeightMeters(float scale) => scale * AuthoredBodyMeters;

        /// <summary>Doll-to-world scale for hiders. Set once per map load (before
        /// players wake) via <see cref="SetPlayerScales"/>.</summary>
        public static float Hider { get; private set; } = Default;

        /// <summary>Doll-to-world scale for hunters.</summary>
        public static float Hunter { get; private set; } = Default;

        /// <summary>The hub's own doll scale — what a body standing on the island
        /// wears, whatever role it happens to hold. Kept apart from the two role
        /// scales because the map's scales land session-wide at door-open (the host
        /// validates every peer against them) while a held-back hunter is still
        /// standing in the hub for the whole hide.</summary>
        public static float Hub { get; private set; } = Default;

        // Which role the player at THIS keyboard is playing. One bool, written in
        // exactly three places (hub entry, map-scale apply, round-start role
        // assignment) so Local can never disagree with the role the match thinks
        // we hold. Defaults to hider: correct for solo play and for the hub,
        // where both scales are the same value anyway.
        private static bool _localIsHunter;

        // Is the local body in a loaded map, or standing in the hub? THE ruling
        // (Pål, 2026-08-10): the hub is neutral ground, so a role only takes
        // effect on your body and your view at the instant you arrive in the map,
        // never when it is dealt. Defaults true — "no hub override" — so tools and
        // tests that never enter a hub read exactly as they always have.
        private static bool _localInMap = true;

        /// <summary>The local player's own scale. Use this for anything sized or
        /// positioned for the player at this keyboard — it saves every such site
        /// having to reach into netcode to re-derive the role. Resolves to
        /// <see cref="Hub"/> while the body is on the island, so a hunter held
        /// there through the hide keeps hub size and hub movement feel.</summary>
        public static float Local => !_localInMap ? Hub : _localIsHunter ? Hunter : Hider;

        /// <summary>True when the local player is playing hunter. Ask
        /// <see cref="LocalInMap"/> too before applying anything the role does TO
        /// the player: this stays true for a hunter waiting out the hide in the hub.</summary>
        public static bool LocalIsHunter => _localIsHunter;

        /// <summary>True when the local body is in a loaded map rather than the hub.
        /// The gate for role-driven handling — forced camera, scale, prank effects,
        /// the shot counter — none of which may touch a body on neutral ground.</summary>
        public static bool LocalInMap => _localInMap;

        /// <summary>Apply a map's per-role player scales, each clamped to a sane
        /// range.</summary>
        public static void SetPlayerScales(float hider, float hunter)
        {
            Hider = Clamp(hider);
            Hunter = Clamp(hunter);
        }

        /// <summary>Both roles at one value — the hub, and any single-scale
        /// caller (tests, tools) that has no roles to distinguish. Sets
        /// <see cref="Hub"/> to the same value: hub entry is the one caller that
        /// matters, and a tool with no roles has no hub to disagree with.</summary>
        public static void SetUniformScale(float scale)
        {
            SetPlayerScales(scale, scale);
            Hub = Clamp(scale);
        }

        /// <summary>Point <see cref="Local"/> at the role this client is playing.
        /// Call it wherever the local role is assigned or re-assigned; a stale
        /// value renders the local player at the other role's size.</summary>
        public static void SetLocalRoleHunter(bool isHunter) => _localIsHunter = isHunter;

        /// <summary>Say where the local body is. Flip it in the SAME frame as the
        /// move — the arrival is the switch — and re-seat the doll afterwards, or
        /// the body wears the wrong scale until something else happens to refresh it.</summary>
        public static void SetLocalInMap(bool inMap) => _localInMap = inMap;

        /// <summary>Restore the built-in defaults (e.g. leaving a map).</summary>
        public static void ResetToDefault()
        {
            Hider = Default;
            Hunter = Default;
            Hub = Default;
            _localIsHunter = false;
            _localInMap = true;
        }

        public static float Clamp(float scale) =>
            scale < MinScale ? MinScale : scale > MaxScale ? MaxScale : scale;
    }
}
