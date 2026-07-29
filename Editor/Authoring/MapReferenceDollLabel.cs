using CoverUp.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CoverUp.EditorTools
{
    /// <summary>
    /// The text half of a <see cref="MapReferenceDoll"/>: "Hider — 1.60 m" floating at
    /// the doll's head. Lives here rather than in the component because
    /// <see cref="Handles"/> is editor-only API, while the silhouette itself is plain
    /// <c>Gizmos</c> drawing the runtime component can do. Colour comes from the doll so
    /// label and silhouette can't drift apart.
    ///
    /// Also the menu item that creates one, since a mapper needs a way to add a doll to
    /// a map that predates them (the starter map ships a pair already placed).
    /// </summary>
    public static class MapReferenceDollLabel
    {
        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Pickable)]
        private static void Draw(MapReferenceDoll doll, GizmoType type)
        {
            float h = doll.HeightMeters;
            if (h <= 0f) return;

            var style = new GUIStyle(EditorStyles.miniBoldLabel);
            style.normal.textColor = doll.RoleColor;

            Vector3 at = doll.transform.position + doll.transform.rotation * new Vector3(0f, h * 1.06f, 0f);
            Handles.Label(at, $"{doll.Role} — {h:0.00} m", style);
        }

        [MenuItem("Cover Up!/Maps/Add Reference Dolls")]
        private static void AddPair()
        {
            Scene scene = SceneManager.GetActiveScene();
            Transform root = MapContract.FindRoot(scene);
            if (root == null)
            {
                EditorUtility.DisplayDialog("Add Reference Dolls",
                    $"This scene has no '{MapContract.Root}' root yet. Run Cover Up! ▸ Maps ▸ Group Base first.", "OK");
                return;
            }

            Transform group = MapContract.FindChild(root, MapContract.Reference);
            if (group == null)
            {
                var go = new GameObject(MapContract.Reference) { tag = MapReferenceDoll.EditorOnlyTag };
                Undo.RegisterCreatedObjectUndo(go, "Add Reference Dolls");
                go.transform.SetParent(root, false);
                group = go.transform;
            }

            MapSpawnDisc spawn = MapSpawnDisc.FindInScene(scene);
            Vector3 at = spawn != null ? spawn.transform.position : root.position;

            Create(group, MapDollRole.Hider, at + new Vector3(-0.6f, 0f, 1.2f));
            Create(group, MapDollRole.Hunter, at + new Vector3(0.6f, 0f, 1.2f));
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void Create(Transform parent, MapDollRole role, Vector3 worldPos)
        {
            var go = new GameObject($"Reference_{role}") { tag = MapReferenceDoll.EditorOnlyTag };
            Undo.RegisterCreatedObjectUndo(go, "Add Reference Dolls");
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            MapReferenceDoll doll = Undo.AddComponent<MapReferenceDoll>(go);
            var so = new SerializedObject(doll);
            so.FindProperty("role").enumValueIndex = (int)role;
            so.ApplyModifiedPropertiesWithoutUndo();
            Selection.activeGameObject = go;
        }
    }
}
