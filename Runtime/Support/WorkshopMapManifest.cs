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
        public const int CurrentFormat = 3;
        public const string FileName = "map.json";

        /// <summary>Refusal size for map.json. A manifest is a few hundred bytes of
        /// metadata; 1 MB is ~1000× the real thing and still bounded, where the
        /// unlimited <c>ReadAllText</c> this replaces would happily pull a 4 GB
        /// "manifest" from a downloaded package into managed memory.</summary>
        public const long MaxManifestBytes = 1L << 20;

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
        public WorkshopBudget budget = new WorkshopBudget();
        public WorkshopBuiltWith builtWith = new WorkshopBuiltWith();
        public string createdUtc;

        /// <summary>The recorded Workshop item id, or 0 if never published.</summary>
        public ulong WorkshopItemId => ulong.TryParse(workshopItemId, out ulong id) ? id : 0UL;

        public string ToJson() => JsonUtility.ToJson(this, true);
        public static WorkshopMapManifest FromJson(string json) => JsonUtility.FromJson<WorkshopMapManifest>(json);

        /// <summary>Read map.json from a package folder; null if missing/invalid, or
        /// larger than <see cref="MaxManifestBytes"/>. The file comes from a
        /// downloaded package, so its size is attacker-chosen and is checked
        /// before a byte of it is read.</summary>
        public static WorkshopMapManifest Read(string packageFolder)
        {
            try
            {
                if (string.IsNullOrEmpty(packageFolder)) return null;
                string path = Path.Combine(packageFolder, FileName);
                var info = new FileInfo(path);
                if (!info.Exists) return null;
                if (info.Length > MaxManifestBytes)
                {
                    Debug.LogWarning($"[Workshop] Ignoring oversized {FileName} ({info.Length} bytes) in {packageFolder}");
                    return null;
                }
                return FromJson(File.ReadAllText(path));
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

        /// <summary>Full path to the package's preview image, or null (no manifest /
        /// no preview named / the file is missing / the path escapes the package
        /// folder). <c>preview</c> is attacker-controlled exactly like
        /// <c>bundle.file</c>, since both come from a downloaded map.json, so it
        /// goes through the same containment guard rather than a raw
        /// <see cref="Path.Combine"/>. Pass an already-parsed
        /// <paramref name="manifest"/> to skip re-reading map.json; null re-reads it.</summary>
        public static string ResolvePreviewFile(string packageFolder, WorkshopMapManifest manifest = null)
        {
            manifest ??= Read(packageFolder);
            if (manifest == null || string.IsNullOrEmpty(manifest.preview)) return null;
            string path = ResolveInside(packageFolder, manifest.preview);
            return path != null && File.Exists(path) ? path : null;
        }

        /// <summary>Combine <paramref name="folder"/> with a manifest-supplied
        /// relative <paramref name="file"/>, but only if the result stays inside
        /// <paramref name="folder"/>. Returns null for absolute paths, rooted
        /// paths, or any <c>../</c> that escapes the folder — the anti
        /// path-traversal guard every untrusted map.json file reference goes
        /// through (bundle and preview alike).
        ///
        /// <para>Public so that any NEW consumer of a manifest-supplied filename has
        /// the guard within reach rather than reaching for <see cref="Path.Combine"/>.
        /// The publish gate weighing both platform bundles was the first such
        /// consumer.</para></summary>
        public static string ResolveInside(string folder, string file)
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
        // AUTO-size brackets the map authored (MapSizeVariants): ≤ smallMax →
        // Small, ≤ mediumMax → Medium, else Large. Advisory like the rest of
        // this block — the host reads the real values off the loaded scene —
        // but it lets the browser say who a map is built for before download.
        public int autoSmallMaxPlayers;
        public int autoMediumMaxPlayers;
        public bool hasSpawn;
    }

    /// <summary>
    /// format 3 (2026-08-01): what the exporter measured this map to cost
    /// (Docs/Steam.md §8.9). Written by <c>WorkshopMapExporter</c>, read by the
    /// publish gate, which refuses a package that is over budget or was never
    /// measured (<c>runtimeBytes</c> &lt; 0, i.e. it only ever went through the fast
    /// auto-export-on-save path).
    ///
    /// <para><b>Advisory, exactly like <see cref="WorkshopContract"/>, and for the
    /// same reason.</b> A downloaded map.json is attacker-controlled, so nothing that
    /// enforces a limit may read it: the runtime re-takes the census on the actual
    /// loaded scene. This block exists so the publish gate and the browser card can
    /// say something before a bundle is opened.</para>
    ///
    /// A format-2 package still parses — every field simply reads its default, which
    /// is the "never measured" state — so old packages keep loading and are asked for
    /// a re-export only if someone publishes one.
    /// </summary>
    [Serializable]
    public sealed class WorkshopBudget
    {
        /// <summary>Largest per-platform bundle in the package, or -1 if unrecorded.</summary>
        public long bundleBytes = -1;
        /// <summary>Profiled runtime memory of the bundle's asset set, or -1 if the
        /// export skipped the measurement (the auto-export-on-save path does).</summary>
        public long runtimeBytes = -1;
        public MapBudget.Census census;
    }

    [Serializable]
    public sealed class WorkshopBuiltWith
    {
        public string game;
        public string unity;
        public int packageFormat = WorkshopMapManifest.CurrentFormat;
    }
}
