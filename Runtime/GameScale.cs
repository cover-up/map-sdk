namespace CoverUp.Core
{
    /// <summary>
    /// Size of a player doll relative to the human-scale world. The world (rooms,
    /// props, furniture) stays 1:1 real scale; players are small, so everything
    /// player-side authored in world-space meters is multiplied by this factor:
    /// collider dims, body mesh, the gun and its particles, camera framing,
    /// climb/prone reaches, and movement (speeds, jump AND gravity).
    ///
    /// Per-map: this is set once at map load from the map's <c>MapConfig</c>, so a
    /// custom map can choose how big its dolls are. It defaults to the Classic
    /// preset's ratio, so a scene with no config behaves exactly as before.
    ///
    /// Scaling velocity and gravity by the same factor keeps the game-feel timing
    /// (hang-time, acceleration) identical while shrinking the distances, so the
    /// doll moves in proportion to its body without going floaty. Only angular
    /// rates, durations and the world itself stay put.
    ///
    /// Read it live at the point of use — never bake it into a serialized field, or
    /// a per-map change (or a re-scale) silently won't apply.
    /// </summary>
    public static class GameScale
    {
        // Human height (1.6 m) ÷ the authored 1.35 m body ≈ 1.185. Speed, jump,
        // gravity, reaches and camera all follow this factor (read live), so
        // changing it here re-tunes the whole feel to the new size. A map that
        // wants the classic ~1-foot dolls sets its own MapConfig scale (0.23).
        public const float Default = 1.185f;

        // Guard rails for mapper-supplied values: never zero/negative, never absurd.
        public const float MinScale = 0.05f;
        public const float MaxScale = 2f;

        // The body mesh is authored at 1.35 m tall, so a doll's real-world height
        // is scale × this (Default 1.185 ≈ 1.6 m human; 0.23 ≈ ~1 foot). Used to
        // show mappers what a scale value MEANS rather than a bare ratio.
        public const float AuthoredBodyMeters = 1.35f;
        public static float ApproxHeightMeters(float scale) => scale * AuthoredBodyMeters;

        /// <summary>
        /// Current doll-to-world scale. Set once per map load (before players wake)
        /// via <see cref="SetPlayerScale"/>; defaults to <see cref="Default"/>.
        /// </summary>
        public static float Player { get; private set; } = Default;

        /// <summary>Apply a map's player scale, clamped to a sane range.</summary>
        public static void SetPlayerScale(float scale)
        {
            if (scale < MinScale) scale = MinScale;
            else if (scale > MaxScale) scale = MaxScale;
            Player = scale;
        }

        /// <summary>Restore the built-in default (e.g. leaving a map).</summary>
        public static void ResetToDefault() => Player = Default;
    }
}
