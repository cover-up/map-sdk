using System;
using System.IO;
using UnityEngine;

namespace CoverUp.Gameplay
{
    /// <summary>
    /// The on-disk location convention for LOCAL (in-progress, unpublished) maps —
    /// the single folder the SDK exporter WRITES to and the game READS from. Both
    /// sides compute it identically so a normal setup needs zero configuration
    /// (Docs/MapCreatorGuide.md). Resolution of the PARENT, dev-only so it is a
    /// config file rather than in-game UI:
    ///   1. a <c>localmaps.txt</c> next to the game/SDK executable (first non-comment
    ///      line = an absolute folder path), else
    ///   2. <c>MyDocuments/CoverUpMaps</c> — which is <c>Documents\CoverUpMaps</c> on
    ///      Windows but <c>~/CoverUpMaps</c> on Linux, because Unity's Mono resolves
    ///      <see cref="System.Environment.SpecialFolder.MyDocuments"/> to <c>$HOME</c>
    ///      there (verified 2026-07-28) — it does NOT honour XDG_DOCUMENTS_DIR, so
    ///      <c>~/Documents</c> existing changes nothing. Say the resolved path out loud
    ///      in docs rather than "Documents": on Linux that word is simply wrong.
    ///
    /// Inside that parent, maps are grouped ONE LEVEL DOWN by channel (2026-08-05):
    ///
    ///   CoverUpMaps/local/&lt;mapId&gt;/       the editor and any non-Steam build
    ///   CoverUpMaps/playtest/&lt;mapId&gt;/    the Steam playtest app
    ///   CoverUpMaps/live/&lt;mapId&gt;/        the Steam store app
    ///
    /// WHY: the parent is keyed on the home directory and knows nothing about which
    /// app is running, so a Steam build on an author's machine used to pick up that
    /// author's in-progress maps and show them as its own content. The channel folder
    /// closes that, and a shared visible parent is what makes the model explain itself
    /// the moment someone opens it. A mapper who wants to try an unpublished map in
    /// their bought copy drops it in <c>live/</c>, no config file, no ceremony. A Steam
    /// customer only ever has <c>live/</c> in play, and it is empty unless they put
    /// something there.
    ///
    /// The channel nests inside a <c>localmaps.txt</c> override too. That file names
    /// the PARENT, not a map folder, so one rule holds everywhere and an override set
    /// up for a relocated home directory keeps working untouched.
    ///
    /// Lives in the SDK package because it is the SDK↔game contract meeting point;
    /// the game's <see cref="LocalMapLibrary"/> scans it, the exporter writes into it.
    /// UnityEngine + BCL only, no game types, hence <see cref="ChannelProvider"/>
    /// being injected rather than the package reaching for <c>AppChannel</c>.
    /// </summary>
    public static class LocalMapsFolder
    {
        public const string OverrideFileName = "localmaps.txt";

        /// <summary>Channel folder used when nobody has injected one: the editor, the
        /// SDK exporter, a test harness. Authoring contexts are all local by
        /// definition, so this is the correct answer and not merely a safe one.</summary>
        public const string DefaultChannel = "local";

        /// <summary>Set by the game at boot (<see cref="LocalMapLibrary"/>) to
        /// <c>AppChannel.MapSubfolder</c>. Assignment only, reading nothing, so it is
        /// immune to RuntimeInitializeOnLoadMethod ordering.</summary>
        public static Func<string> ChannelProvider;

        /// <summary>The folder that CONTAINS Application.dataPath: the project root
        /// in the editor (dataPath = &lt;project&gt;/Assets) or the build folder in a
        /// player (dataPath = &lt;build&gt;/&lt;Game&gt;_Data).</summary>
        public static string AppRoot =>
            Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory();

        /// <summary>The PARENT folder's default location, before any override and
        /// before the channel folder.</summary>
        public static string DefaultFolder =>
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "CoverUpMaps");

        /// <summary>The PARENT folder actually in use: the override if there is one,
        /// else <see cref="DefaultFolder"/>. Holds the channel folders, never maps.</summary>
        public static string BaseFolder
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

        public static string Channel
        {
            get
            {
                try
                {
                    string c = ChannelProvider?.Invoke();
                    if (!string.IsNullOrEmpty(c)) return c;
                }
                catch { /* fall through to default */ }
                return DefaultChannel;
            }
        }

        /// <summary>Where THIS process's local maps live: parent + channel. One map
        /// package per subfolder of this.</summary>
        public static string Folder => Path.Combine(BaseFolder, Channel);

        public static void EnsureFolder()
        {
            MigrateFlatLayout();
            try { Directory.CreateDirectory(Folder); } catch { /* best-effort */ }
        }

        private static bool _migrated;

        /// <summary>One-time move of the pre-2026-08-05 FLAT layout
        /// (<c>CoverUpMaps/&lt;mapId&gt;/</c>) down into <c>CoverUpMaps/local/</c>, so an
        /// existing authoring folder does not silently read as empty after the update.
        ///
        /// Deliberately timid, because this mutates a folder a human owns:
        ///   • only DIRECTORIES that actually contain a <c>map.json</c> move, so zips,
        ///     notes and stray files are left exactly where they are;
        ///   • the three channel names are never candidates;
        ///   • an existing destination is never overwritten, it is skipped;
        ///   • everything is best-effort, since a failure to migrate must not stop the game
        ///     or the exporter from working with whatever did move.
        /// Idempotent, and after the first pass there is nothing left to match.</summary>
        public static void MigrateFlatLayout()
        {
            if (_migrated) return;
            _migrated = true;

            try
            {
                string parent = BaseFolder;
                if (!Directory.Exists(parent)) return;

                string dest = Path.Combine(parent, DefaultChannel);
                int moved = 0;

                foreach (string dir in Directory.GetDirectories(parent))
                {
                    string name = Path.GetFileName(dir);
                    if (name == "local" || name == "live" || name == "playtest") continue;
                    if (!File.Exists(Path.Combine(dir, "map.json"))) continue;

                    string target = Path.Combine(dest, name);
                    if (Directory.Exists(target)) continue;

                    try
                    {
                        Directory.CreateDirectory(dest);
                        Directory.Move(dir, target);
                        moved++;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[LocalMaps] could not move '{name}' into {DefaultChannel}/: {ex.Message}");
                    }
                }

                if (moved > 0)
                    Debug.Log($"[LocalMaps] moved {moved} map folder(s) into {dest} (channel layout, 2026-08-05)");
            }
            catch { /* best-effort */ }
        }
    }
}
