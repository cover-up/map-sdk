using CoverUp.Core;
using CoverUp.Gameplay;
using UnityEditor;
using UnityEngine;

namespace CoverUp.EditorTools
{
    /// <summary>
    /// Mapper-facing inspector for <see cref="MapConfig"/>: the same scale
    /// slider, plus a live "how tall is the doll" readout and one-click
    /// reference presets — so a bare 1.185 isn't a mystery number. Editing here
    /// still routes through the field's OnValidate, so the in-scene doll
    /// re-sizes live. No runtime behaviour; purely authoring UX.
    /// </summary>
    [CustomEditor(typeof(MapConfig))]
    public sealed class MapConfigEditor : Editor
    {
        // The documented reference points (GameScale). Custom values via slider.
        private static readonly (string label, float scale)[] Presets =
        {
            ("Tiny (~1 ft)", 0.23f),
            ("Human", GameScale.Default),
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SerializedProperty scale = serializedObject.FindProperty("playerScale");

            EditorGUILayout.PropertyField(scale, new GUIContent("Player Scale"));

            EditorGUILayout.HelpBox(
                $"Doll ≈ {GameScale.ApproxHeightMeters(scale.floatValue):0.00} m tall.\n" +
                "Scales the whole player — body, collider, movement, camera and gun — " +
                "not just the mesh. Applied once at map load " +
                $"(clamped to {GameScale.MinScale:0.##}–{GameScale.MaxScale:0.##}).",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Presets");
            foreach ((string label, float value) in Presets)
            {
                string tip = $"≈ {GameScale.ApproxHeightMeters(value):0.00} m ({value:0.###})";
                if (GUILayout.Button(new GUIContent(label, tip))) scale.floatValue = value;
            }
            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties(); // routes through OnValidate → live preview
        }
    }
}
