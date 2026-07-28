using System;
using System.IO;
using UnityEngine;

namespace CoverUp.Gameplay
{
    /// <summary>
    /// The on-disk <c>map.json</c> manifest for a Workshop map package, plus the
    /// helpers both the editor exporter and the runtime loader share so they
    /// agree on the format (Docs/Steam.md §8.1/§8.2). Serialized with
    /// JsonUtility — every field is a public serializable member.
    /// </summary>
    [Serializable]
    public sealed class WorkshopMapManifest
    {
        public const int CurrentFormat = 2;
        public const string FileName = "map.json";

        public int format = CurrentFormat;
        public string mapId;
        public string title;
        public string author;
        public string authorSteamId;
        public string description;
        public string[] tags = Array.Empty<string>();
        public string scene;                     // scene name inside the bundle (informational)
        public WorkshopBundles bundles = new WorkshopBundles();
        public string preview = "preview.png";
        // The Steam Workshop item id this package was last published to (as a
        // string to avoid any JSON ulong-precision doubt). Empty = never
        // published; set by the Publish tool so a re-publish updates the SAME
        // item instead of creating a duplicate (Steam S6 P3).
        public string workshopItemId = "";
        public WorkshopContract contract = new WorkshopContract();
        public WorkshopBuiltWith builtWith = new WorkshopBuiltWith();
        public string createdUtc;

        /// <summary>The recorded Workshop item id, or 0 if never published.</summary>
        public ulong WorkshopItemId => ulong.TryParse(workshopItemId, out ulong id) ? id : 0UL;

        public string ToJson() => JsonUtility.ToJson(this, true);
        public static WorkshopMapManifest FromJson(string json) => JsonUtility.FromJson<WorkshopMapManifest>(json);

        /// <summary>Read map.json from a package folder; null if missing/invalid.</summary>
        public static WorkshopMapManifest Read(string packageFolder)
        {
            try
            {
                if (string.IsNullOrEmpty(packageFolder)) return null;
                string path = Path.Combine(packageFolder, FileName);
                return File.Exists(path) ? FromJson(File.ReadAllText(path)) : null;
            }
            catch { return null; }
        }

        /// <summary>Record the assigned Workshop item id into the package's map.json so
        /// a later re-publish updates the SAME item. Returns true if written; false if
        /// the folder has no readable manifest, the id is 0, or it was already recorded.
        /// Shared by the editor Publish tool and the in-game publish (M4).</summary>
        public static bool RecordItemId(string packageFolder, ulong itemId)
        {
            if (itemId == 0) return false;
            WorkshopMapManifest m = Read(packageFolder);
            if (m == null || m.WorkshopItemId == itemId) return false;
            m.workshopItemId = itemId.ToString();
            try
            {
                File.WriteAllText(Path.Combine(packageFolder, FileName), m.ToJson());
                return true;
            }
            catch { return false; }
        }

        /// <summary>The "windows"/"linux" key for the running platform, or null
        /// (e.g. macOS, which we don't ship a native build for).</summary>
        public static string PlatformKey()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsEditor: return "windows";
                case RuntimePlatform.LinuxPlayer:
                case RuntimePlatform.LinuxEditor: return "linux";
                default: return null;
            }
        }

        public WorkshopBundleRef BundleForThisPlatform()
        {
            if (bundles == null) return null;
            return PlatformKey() == "windows" ? bundles.windows : bundles.linux;
        }

        /// <summary>Full path to the bundle file this platform should load, or
        /// null (no manifest / format too new / no bundle for this platform /
        /// file missing / bundle path escapes the package folder). The
        /// <c>bundle.file</c> value is attacker-controlled (it comes from a
        /// downloaded map.json), so it must resolve to a plain file *inside* the
        /// package folder — an absolute path or a <c>../</c> traversal is
        /// rejected, never loaded. <paramref name="manifest"/> is the parsed manifest.</summary>
        public static string ResolveBundleFile(string packageFolder, out WorkshopMapManifest manifest)
        {
            manifest = Read(packageFolder);
            if (manifest == null || manifest.format > CurrentFormat) return null;
            WorkshopBundleRef b = manifest.BundleForThisPlatform();
            if (b == null || string.IsNullOrEmpty(b.file)) return null;
            string path = ResolveInside(packageFolder, b.file);
            return path != null && File.Exists(path) ? path : null;
        }

        /// <summary>Combine <paramref name="folder"/> with a manifest-supplied
        /// relative <paramref name="file"/>, but only if the result stays inside
        /// <paramref name="folder"/>. Returns null for absolute paths, rooted
        /// paths, or any <c>../</c> that escapes the folder — the anti
        /// path-traversal guard for untrusted map.json bundle references.</summary>
        private static string ResolveInside(string folder, string file)
        {
            try
            {
                if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(file)) return null;
                if (Path.IsPathRooted(file)) return null; // absolute path — reject outright
                string rootFull = Path.GetFullPath(folder);
                string sep = Path.DirectorySeparatorChar.ToString();
                string rootWithSep = rootFull.EndsWith(sep, StringComparison.Ordinal) ? rootFull : rootFull + sep;
                string full = Path.GetFullPath(Path.Combine(rootFull, file));
                return full.StartsWith(rootWithSep, StringComparison.Ordinal) ? full : null;
            }
            catch { return null; }
        }

        // ToMapInfo (manifest → the game's MapInfo) intentionally lives OUTSIDE
        // this package, as an extension in CoverUp.Gameplay: MapInfo is a game
        // runtime type, and the SDK package must stay free of game code
        // (Docs/MapSdk.md §2). See WorkshopMapManifestExtensions.
    }

    [Serializable] public sealed class WorkshopBundles { public WorkshopBundleRef windows; public WorkshopBundleRef linux; }
    [Serializable] public sealed class WorkshopBundleRef { public string file; public string sha256; }

    // format 2 (per-role player scale): playerScale/approxDollMeters were
    // replaced by the hider/hunter pair. A format-1 manifest still parses —
    // the new fields simply read 0 — and that is harmless, because the
    // contract block is DESCRIPTIVE: the scales the game actually applies come
    // from the MapConfig inside the bundle, never from map.json.
    [Serializable]
    public sealed class WorkshopContract
    {
        public float hiderScale;
        public float hunterScale;
        public float approxHiderMeters;
        public float approxHunterMeters;
        public string[] sizes = Array.Empty<string>();
        public bool hasSpawn;
    }

    [Serializable]
    public sealed class WorkshopBuiltWith
    {
        public string game;
        public string unity;
        public int packageFormat = WorkshopMapManifest.CurrentFormat;
    }
}
