using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CoverUp.EditorTools
{
    /// <summary>
    /// Repair textures that import UNCOMPRESSED, and report what it changed.
    ///
    /// Why this can't be done in the Inspector: on the Default platform tab Unity
    /// derives Format from Compression, so the Format dropdown is greyed out. A tool
    /// that writes an explicit format straight into the .meta — the Substance plugin's
    /// import preset does exactly this — therefore pins a format the UI cannot clear.
    /// Combined with Compression: None, that ships raw pixels: ~8x the bytes of a
    /// BC-compressed texture at the same resolution, and no amount of lowering Max Size
    /// touches the multiplier.
    ///
    /// This goes through the importer API instead, which can reset both fields.
    /// Scans the whole project rather than a selection, because selecting the right
    /// assets in the Project window is itself the trap (a folder selection silently
    /// applies nothing).
    /// </summary>
    public static class FixUncompressedTextures
    {
        [MenuItem("Cover Up!/Maps/Diagnostics/Fix Uncompressed Textures (report)")]
        private static void Report() => Run(apply: false);

        [MenuItem("Cover Up!/Maps/Diagnostics/Fix Uncompressed Textures (APPLY)")]
        private static void Apply()
        {
            if (!EditorUtility.DisplayDialog("Fix Uncompressed Textures",
                    "Set Compression = Normal and Format = Auto on every uncompressed texture " +
                    "in this project, then reimport them.\n\nThis edits import settings only — " +
                    "no source files are touched, and it is undoable via version control.",
                    "Fix them", "Cancel")) return;
            Run(apply: true);
        }

        private static void Run(bool apply)
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D");
            var hits = new List<string>();
            long before = 0;

            try
            {
                if (apply) AssetDatabase.StartAssetEditing();
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (!path.StartsWith("Assets/")) continue;
                    if (AssetImporter.GetAtPath(path) is not TextureImporter ti) continue;

                    TextureImporterPlatformSettings s = ti.GetDefaultPlatformTextureSettings();
                    bool uncompressed = s.textureCompression == TextureImporterCompression.Uncompressed;
                    bool forcedFormat = s.format != TextureImporterFormat.Automatic;
                    if (!uncompressed && !forcedFormat) continue;

                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (tex != null) before += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(tex);
                    hits.Add($"  {(uncompressed ? "uncompressed" : "forced fmt")}  {s.format,-12} max={s.maxTextureSize,-5} {path}");

                    if (!apply) continue;
                    // Automatic lets Unity pick the right block format per platform —
                    // DXT1/BC7 with alpha handled, and normal maps still get their own.
                    s.format = TextureImporterFormat.Automatic;
                    s.textureCompression = TextureImporterCompression.Compressed;
                    ti.SetPlatformTextureSettings(s);
                    ti.SaveAndReimport();
                }
            }
            finally
            {
                if (apply) { AssetDatabase.StopAssetEditing(); AssetDatabase.Refresh(); }
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[FixUncompressedTextures] {(apply ? "FIXED" : "would fix")} {hits.Count} of {guids.Length} textures " +
                          $"— {before / 1048576f:0.0} MB of runtime texture data before the change.");
            foreach (string h in hits) sb.AppendLine(h);
            if (hits.Count == 0) sb.AppendLine("  nothing to do — every texture already imports compressed with an automatic format.");
            Debug.Log(sb.ToString());
        }
    }

}
