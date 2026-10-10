using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CoverUp.Gameplay
{
    /// <summary>
    /// Two teleport pads that lead to each other. The pair is the PARENT: it carries the
    /// colour and the pylon flag, and its two <see cref="MapTeleportPad"/> descendants are the
    /// ends. Walk onto either pad and the game shows the first metres of an arc toward the
    /// other; press Use and after a half-second departure you stand on the other pad, facing
    /// its +Z. Both roles, every phase. The pair recharges for <see cref="CooldownSeconds"/>
    /// after every hop, so two players cannot bounce back and forth.
    ///
    /// The colour is the pair's identity: the pads glow with it, the arc and the departure
    /// flash wear it, and with <see cref="Pylon"/> on, a pylon of that colour stands on BOTH
    /// pads while anyone is standing on either, telling the whole map that someone is about to
    /// travel and where they will come out. That broadcast is the mechanic (Pål, 2026-10-09):
    /// a hider who takes the pad trades a long walk for a tell. Pads may be reached from
    /// different places on different maps, so give each pair its own colour.
    ///
    /// Pure data, like every marker in this package: the game does the drawing, the hop and
    /// the host validation. Strictly pairs, both directions; a one-way pad or a hub of three
    /// does not exist. A pair under a size root exists only at that size; one under Base at
    /// every size. Validate Map errors on a pair without exactly two pads or with its pads in
    /// different size roots. Docs/MapSdk.md §21.
    ///
    /// SDK 0.17. An older game build ignores it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapTeleportPair : MonoBehaviour
    {
        private static readonly List<MapTeleportPair> Pairs = new List<MapTeleportPair>();

        /// <summary>Seconds after a hop before the pair can be used again, either way. A design
        /// constant shared by the game's host gate and its pad gauge, not a mapper knob.</summary>
        public const float CooldownSeconds = 3f;

        /// <summary>How long the departure takes before the body is moved, in seconds. Fixed so
        /// every client plays the same half-second and lands on the same frame.</summary>
        public const float DepartureSeconds = 0.5f;

        /// <summary>A pair needs exactly this many pads.</summary>
        public const int PadsPerPair = 2;

        [SerializeField]
        [Tooltip("The pair's colour: the pads glow with it, the arc toward the partner and the departure wear " +
                 "it, and the pylon (if on) shines with it. Keep it saturated and different from the map's other " +
                 "pairs; players tell pairs apart by colour.")]
        private Color colour = new Color(0.20f, 0.90f, 0.80f);

        [SerializeField]
        [Tooltip("While a player stands on either pad, a tall pylon of the pair colour rises on BOTH pads, seen " +
                 "map-wide: everyone learns that someone is travelling and where they will come out. Off: the pads " +
                 "still glow, nothing tells the map.")]
        private bool pylon = true;

        /// <summary>The pair's colour. Alpha is ignored.</summary>
        public Color Colour => colour;

        /// <summary>Raise the pylons while someone stands on a pad.</summary>
        public bool Pylon => pylon;

        private void OnEnable() => Pairs.Add(this);
        private void OnDisable() => Pairs.Remove(this);

        /// <summary>The pads of this pair, in hierarchy order: the first found is pad 0, the next is pad 1.
        /// Inactive pads count (a size root that is switched off still owns its pads). More than two is an
        /// authoring error that Validate Map reports; the game uses the first two.</summary>
        public MapTeleportPad[] Pads => GetComponentsInChildren<MapTeleportPad>(true);

        /// <summary>Both ends, or false when the pair is incomplete.</summary>
        public bool TryGetPads(out MapTeleportPad a, out MapTeleportPad b)
        {
            MapTeleportPad[] pads = Pads;
            a = pads.Length > 0 ? pads[0] : null;
            b = pads.Length > 1 ? pads[1] : null;
            return pads.Length >= PadsPerPair;
        }

        /// <summary>Which end a pad is: 0, 1, or -1 when it is not one of this pair's first two pads.</summary>
        public int IndexOf(MapTeleportPad pad)
        {
            MapTeleportPad[] pads = Pads;
            for (int i = 0; i < pads.Length && i < PadsPerPair; i++) if (pads[i] == pad) return i;
            return -1;
        }

        /// <summary>The other end of <paramref name="pad"/>, or null when the pair is incomplete.</summary>
        public MapTeleportPad PartnerOf(MapTeleportPad pad)
        {
            if (!TryGetPads(out MapTeleportPad a, out MapTeleportPad b)) return null;
            return pad == a ? b : pad == b ? a : null;
        }

        /// <summary>
        /// Every enabled pair in <paramref name="scene"/>, ordered by hierarchy path so every client
        /// that loaded the same scene numbers the pairs the same way; the game's wire format carries
        /// this index, nothing is authored. Active-scene scoped by the caller: a round map streams in
        /// behind the lobby and its pairs must not be mistaken for the hub's 1500 m below.
        /// </summary>
        public static int CollectIn(Scene scene, List<MapTeleportPair> into)
        {
            into.Clear();
            foreach (MapTeleportPair pair in Pairs)
                if (pair != null && pair.gameObject.scene == scene) into.Add(pair);
            into.Sort((x, y) => string.CompareOrdinal(HierarchyPath(x.transform), HierarchyPath(y.transform)));
            return into.Count;
        }

        /// <summary>Full hierarchy path of a transform, root first, '/' separated: the sort key the
        /// pair index is built from. Duplicate names at one level sort by sibling index after the name.</summary>
        public static string HierarchyPath(Transform t)
        {
            var sb = new StringBuilder();
            for (Transform p = t; p != null; p = p.parent)
            {
                string part = p.name + "#" + p.GetSiblingIndex().ToString("D4");
                sb.Insert(0, sb.Length == 0 ? part : part + "/");
            }
            return sb.ToString();
        }
    }
}
