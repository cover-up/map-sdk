using CoverUp.Gameplay;
using UnityEditor;

namespace CoverUp.EditorTools
{
    /// <summary>
    /// Runs the local-maps flat-to-channel migration when the editor loads
    /// (<see cref="LocalMapsFolder.MigrateFlatLayout"/>, Docs/Steam.md §8.10).
    ///
    /// The export path and the game's scan both migrate on their own, but neither
    /// happens until someone exports or opens Sandbox. Until then a mapper updating
    /// the SDK sees an authoring folder that looks untouched, and any `local/` they
    /// go looking for is simply not there yet. Doing it at editor load means the
    /// folder is in its new shape by the time anyone thinks to look at it.
    ///
    /// Cheap enough to sit on this path: the migration returns on a single
    /// Directory.Exists once there is nothing left to move, and it is guarded to run
    /// at most once per process.
    /// </summary>
    internal static class LocalMapsMigration
    {
        [InitializeOnLoadMethod]
        private static void OnEditorLoad() => LocalMapsFolder.MigrateFlatLayout();
    }
}
