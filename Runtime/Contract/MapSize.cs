namespace CoverUp.Core
{
    // The size tiers a map can build, smallest first. A map builds any subset;
    // smaller sizes ADD blockers (doors + bounds volumes) to a shared base,
    // nothing is ever removed. Wire-encoded as this byte in PhaseChangeMsg; the
    // ordinal order (Tiny < … < Huge) is load-bearing for the bracket pick and
    // the clamp walk, do not reorder.
    public enum MapSize : byte
    {
        Tiny = 0,
        Small = 1,
        Medium = 2,
        Large = 3,
        Huge = 4,
    }

    // Pure size arithmetic: the per-size player brackets and the "clamp to what
    // the map built" rule. Engine-free so both the host phase machine and the
    // wire layer share one definition.
    //
    // There is no lobby setting: the size is ALWAYS picked from the count of
    // players entering the map, against THAT map's brackets. The host reads the
    // brackets off the loaded scene before StartRound (MapSizeBrackets); only
    // the RESOLVED MapSize crosses the wire, so they need no replication.
    public static class MapSizeRules
    {
        public const int Count = 5;
        public static readonly MapSize[] All =
            { MapSize.Tiny, MapSize.Small, MapSize.Medium, MapSize.Large, MapSize.Huge };

        // A bracket below 1 player means nothing, and Workshop numbers are
        // untrusted input, so authored values are pushed through Sanitize.
        public const int MinThreshold = 1;

        // The bracket a size gets when the map doesn't say: the most players
        // it still runs for. The largest size a map builds ignores its own
        // number and takes everyone above the size before it.
        public static int DefaultMaxPlayers(MapSize size) => size switch
        {
            MapSize.Tiny => 5,
            MapSize.Small => 10,
            MapSize.Medium => 22,
            MapSize.Large => 40,
            _ => NetMaxPlayers,
        };

        // Mirrors NetProtocol.MaxPlayers without the package reaching into Core.
        private const int NetMaxPlayers = 64;

        public static bool IsBuilt(int builtMask, MapSize size) => (builtMask & (1 << (int)size)) != 0;

        // Constrain a desired size into the sizes a map actually built. Bit i of
        // builtMask = MapSize i is present. When the exact size is missing we
        // walk DOWN first (a slightly tighter arena is always safe), then up.
        // The host already picks among built sizes; this is the per-client
        // safety net for a desired size the loaded scene doesn't have.
        // Caller guarantees builtMask != 0.
        public static MapSize Clamp(MapSize desired, int builtMask)
        {
            if (IsBuilt(builtMask, desired)) return desired;
            for (int s = (int)desired - 1; s >= 0; s--)
                if (IsBuilt(builtMask, (MapSize)s)) return (MapSize)s;
            for (int s = (int)desired + 1; s < Count; s++)
                if (IsBuilt(builtMask, (MapSize)s)) return (MapSize)s;
            return desired; // unreachable while builtMask != 0
        }
    }

    // One map's sizes and their player brackets, as the host needs them to pick
    // the round's size. Plain ints so the host phase machine stays engine-free.
    public readonly struct MapSizeBrackets
    {
        // Bit i = MapSize i is built. 0 = a one-size map: every tier counts as
        // built for the pick (the answer only feeds the lobby browser there,
        // the loaded scene has nothing to toggle).
        public readonly int BuiltMask;
        private readonly int[] _maxPlayers;

        public static MapSizeBrackets Default => new MapSizeBrackets(0, null);

        /// <param name="maxPlayers">Per tier, indexed by MapSize. Missing or
        /// short arrays fall back to the defaults. Sanitized: each built size
        /// ends at least one player after the built size below it, so no built
        /// size is unreachable.</param>
        public MapSizeBrackets(int builtMask, int[] maxPlayers)
        {
            BuiltMask = builtMask & ((1 << MapSizeRules.Count) - 1);
            _maxPlayers = new int[MapSizeRules.Count];
            int floor = MapSizeRules.MinThreshold - 1;
            foreach (MapSize s in MapSizeRules.All)
            {
                int i = (int)s;
                int v = maxPlayers != null && i < maxPlayers.Length ? maxPlayers[i] : MapSizeRules.DefaultMaxPlayers(s);
                if (!Considered(s)) { _maxPlayers[i] = v; continue; }
                if (v <= floor) v = floor + 1;
                _maxPlayers[i] = v;
                floor = v;
            }
        }

        private bool Considered(MapSize s) => BuiltMask == 0 || MapSizeRules.IsBuilt(BuiltMask, s);

        /// <summary>The sanitized bracket of one size (meaningless for an unbuilt
        /// size, and ignored for the largest built one).</summary>
        public int MaxPlayers(MapSize size) =>
            _maxPlayers != null ? _maxPlayers[(int)size] : MapSizeRules.DefaultMaxPlayers(size);

        /// <summary>The smallest built size whose bracket holds this many
        /// players; past every bracket, the largest built size.</summary>
        public MapSize ForPlayers(int players)
        {
            MapSize largest = MapSize.Medium;
            bool any = false;
            foreach (MapSize s in MapSizeRules.All)
            {
                if (!Considered(s)) continue;
                any = true;
                largest = s;
                if (players <= MaxPlayers(s)) return s;
            }
            return any ? largest : MapSize.Medium;
        }
    }
}
