namespace CoverUp.Core
{
    // The three built variants of one map. Smaller sizes ADD blockers (doors +
    // bounds volumes) to a shared base — nothing is ever removed. Wire-encoded
    // as this byte in PhaseChangeMsg; the ordinal order (Small < Medium < Large)
    // is load-bearing for the clamp walk, do not reorder.
    public enum MapSize : byte
    {
        Small = 0,
        Medium = 1,
        Large = 2,
    }

    // The host's lobby choice. Auto resolves per-round from the count of players
    // entering the map; a forced value ignores the count. Only the RESOLVED
    // MapSize crosses the wire — Auto never does.
    public enum MapSizeSetting : byte
    {
        Auto = 0,
        Small = 1,
        Medium = 2,
        Large = 3,
    }

    // Pure size arithmetic: auto thresholds + the "clamp to what the map built"
    // rule. Engine-free so both the host phase machine and the wire layer share
    // one definition.
    //
    // The thresholds are PER-MAP: a map authors its own on MapSizeVariants, and
    // the host reads them off the loaded scene before StartRound. The Default*
    // constants below are only the fallback for a map that doesn't say (and for
    // a one-size map, where the clamp overrides the answer anyway). Only the
    // RESOLVED MapSize crosses the wire, so the thresholds stay host-side and
    // need no replication.
    public static class MapSizeRules
    {
        // Auto brackets on the count of players ENTERING the map that round:
        //   ≤ smallMax → Small, ≤ mediumMax → Medium, else Large.
        public const int DefaultSmallMaxPlayers = 10;
        public const int DefaultMediumMaxPlayers = 22;

        // A map can't sensibly ask for a Small bracket below 1 player, and the
        // Medium bracket has to end after the Small one or Medium is unreachable.
        // Authored values are pushed through this rather than trusted — a
        // Workshop map's numbers are untrusted input like any other.
        public const int MinThreshold = 1;
        public static void Sanitize(ref int smallMax, ref int mediumMax)
        {
            if (smallMax < MinThreshold) smallMax = MinThreshold;
            if (mediumMax <= smallMax) mediumMax = smallMax + 1;
        }

        public static MapSize ForPlayers(int players) =>
            ForPlayers(players, DefaultSmallMaxPlayers, DefaultMediumMaxPlayers);

        public static MapSize ForPlayers(int players, int smallMax, int mediumMax)
        {
            Sanitize(ref smallMax, ref mediumMax);
            if (players <= smallMax) return MapSize.Small;
            if (players <= mediumMax) return MapSize.Medium;
            return MapSize.Large;
        }

        // The host's desired size for the round: Auto reads the count against
        // THIS map's brackets, a forced setting maps straight through
        // (Small/Medium/Large sit one past Auto) and ignores them entirely.
        public static MapSize Resolve(MapSizeSetting setting, int players) =>
            Resolve(setting, players, DefaultSmallMaxPlayers, DefaultMediumMaxPlayers);

        public static MapSize Resolve(MapSizeSetting setting, int players, int smallMax, int mediumMax) =>
            setting == MapSizeSetting.Auto
                ? ForPlayers(players, smallMax, mediumMax)
                : (MapSize)(byte)(setting - 1);

        // Constrain a desired size into the sizes a map actually built. Bit i of
        // builtMask = MapSize i is present. When the exact size is missing we
        // walk DOWN first (a slightly tighter arena is always safe), then up.
        // Caller guarantees builtMask != 0.
        public static MapSize Clamp(MapSize desired, int builtMask)
        {
            if ((builtMask & (1 << (int)desired)) != 0) return desired;
            for (int s = (int)desired - 1; s >= 0; s--)
                if ((builtMask & (1 << s)) != 0) return (MapSize)s;
            for (int s = (int)desired + 1; s <= (int)MapSize.Large; s++)
                if ((builtMask & (1 << s)) != 0) return (MapSize)s;
            return desired; // unreachable while builtMask != 0
        }
    }
}
