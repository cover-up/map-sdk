namespace CoverUp.Core
{
    /// <summary>
    /// How far from its origin a map is allowed to reach.
    ///
    /// Not an art direction: it is the box player positions can be TRANSMITTED
    /// in. MoveState quantizes x/z and y against these half-extents, and a
    /// position outside one does not fail loudly, it CLAMPS — so a body walking
    /// past the edge freezes at it on every screen but its owner's, which is the
    /// kind of bug that gets reported as "desync" and investigated everywhere
    /// except the map.
    ///
    /// It lives in the SDK, and the wire reads it from here rather than the
    /// other way round, because a map project has the SDK and nothing else. The
    /// authoring tool that has to enforce this (Validate Map) and the code that
    /// has to honour it (MoveState) then cannot drift apart.
    ///
    /// Engine-free plain floats, like <see cref="MapSizeRules"/> beside it, so
    /// the wire assembly can reference it without dragging UnityEngine in.
    ///
    /// The vertical figure was ±2048 until proto 29, purely so that ONE absolute
    /// range could cover both the maps at y ≈ 0 and the hub 1500 m below. That
    /// cost 63 mm of vertical precision, which stopped being affordable the
    /// moment a map scaled its players down to 0.2 m. Y is now quantized
    /// relative to whichever of the two places a position is in, so the range
    /// each of them needs is just the size of a map — and the vertical limit is
    /// the same number as the horizontal one, which is also the easier contract
    /// to remember.
    /// </summary>
    public static class MapWorldBox
    {
        /// <summary>Half-extent on x and z, in metres, from the map's origin.</summary>
        public const float HalfExtentXZ = 256f;

        /// <summary>Half-extent on y, in metres, from the map's origin.</summary>
        public const float HalfExtentY = 256f;
    }
}
