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
    // one definition. Thresholds live here alone — the one place to tweak them.
    public static class MapSizeRules
    {
        // Auto brackets on the count of players ENTERING the map that round:
        //   ≤ SmallMax → Small, ≤ MediumMax → Medium, else Large.
        public const int SmallMaxPlayers = 10;
        public const int MediumMaxPlayers = 22;

        public static MapSize ForPlayers(int players)
        {
            if (players <= SmallMaxPlayers) return MapSize.Small;
            if (players <= MediumMaxPlayers) return MapSize.Medium;
            return MapSize.Large;
        }

        // The host's desired size for the round: Auto reads the count, a forced
        // setting maps straight through (Small/Medium/Large sit one past Auto).
        public static MapSize Resolve(MapSizeSetting setting, int players) =>
            setting == MapSizeSetting.Auto
                ? ForPlayers(players)
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
