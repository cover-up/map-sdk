using System.IO;
using UnityEngine;

namespace CoverUp.Gameplay
{
    /// <summary>
    /// The on-disk location convention for LOCAL (in-progress, unpublished) maps —
    /// the single folder the SDK exporter WRITES to and the game READS from. Both
    /// sides compute it identically so a normal setup needs zero configuration
    /// (Docs/MapCreatorGuide.md). Resolution, dev-only so it's a config file not
    /// in-game UI:
    ///   1. a <c>localmaps.txt</c> next to the game/SDK executable (first non-comment
    ///      line = an absolute folder path), else
    ///   2. <c>~/Documents/CoverUpMaps</c>.
    ///
    /// Lives in the SDK package because it is the SDK↔game contract meeting point;
    /// the game's <see cref="LocalMapLibrary"/> scans it, the exporter writes into it.
    /// UnityEngine + BCL only — no game types.
    /// </summary>
    public static class LocalMapsFolder
    {
        public const string OverrideFileName = "localmaps.txt";

        /// <summary>The folder that CONTAINS Application.dataPath: the project root
        /// in the editor (dataPath = &lt;project&gt;/Assets) or the build folder in a
        /// player (dataPath = &lt;build&gt;/&lt;Game&gt;_Data).</summary>
        public static string AppRoot =>
            Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory();

        public static string DefaultFolder =>
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "CoverUpMaps");

        public static string Folder
        {
            get
            {
                try
                {
                    string cfg = Path.Combine(AppRoot, OverrideFileName);
                    if (File.Exists(cfg))
                    {
                        foreach (string raw in File.ReadAllLines(cfg))
                        {
                            string line = raw.Trim();
                            if (line.Length > 0 && !line.StartsWith("#")) return line;
                        }
                    }
                }
                catch { /* fall through to default */ }
                return DefaultFolder;
            }
        }

        public static void EnsureFolder()
        {
            try { Directory.CreateDirectory(Folder); } catch { /* best-effort */ }
        }
    }
}
