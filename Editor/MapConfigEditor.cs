using CoverUp.Core;
using CoverUp.Gameplay;
using UnityEditor;
using UnityEngine;

namespace CoverUp.EditorTools
{
    /// <summary>
    /// Mapper-facing inspector for <see cref="MapConfig"/>: the two role scale
    /// sliders, plus a live "how tall is the doll" readout and one-click
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

        // On by default: most maps want both roles the same, and the link makes
        // that the one-drag case instead of two drags that must be kept equal.
        private static bool _linked = true;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SerializedProperty hider = serializedObject.FindProperty("hiderScale");
            SerializedProperty hunter = serializedObject.FindProperty("hunterScale");

            _linked = EditorGUILayout.ToggleLeft(
                new GUIContent("Link roles", "Keep hider and hunter at the same scale."),
                _linked);

            EditorGUILayout.PropertyField(hider, new GUIContent("Hider Scale"));
            using (new EditorGUI.DisabledScope(_linked))
            {
                EditorGUILayout.PropertyField(hunter, new GUIContent("Hunter Scale"));
            }
            if (_linked) hunter.floatValue = hider.floatValue;

            EditorGUILayout.HelpBox(
                $"Hider ≈ {GameScale.ApproxHeightMeters(hider.floatValue):0.00} m tall, " +
                $"hunter ≈ {GameScale.ApproxHeightMeters(hunter.floatValue):0.00} m.\n" +
                "Each scales that role's whole player — body, collider, movement, camera and " +
                "gun — not just the mesh. Exposure rings and scoring distances follow the HIDER " +
                "scale only, so sizing hunters never moves the balance. Applied once at map load " +
                $"(each clamped to {GameScale.MinScale:0.##}–{GameScale.MaxScale:0.##}).",
                MessageType.Info);


            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Presets");
            foreach ((string label, float value) in Presets)
            {
                string tip = $"≈ {GameScale.ApproxHeightMeters(value):0.00} m ({value:0.###})";
                if (!GUILayout.Button(new GUIContent(label, tip))) continue;
                hider.floatValue = value;
                if (_linked) hunter.floatValue = value;
            }
            EditorGUILayout.EndHorizontal();

            // Everything above is scale; this is a separate lever, so give it a rule
            // and a header rather than letting it read as another scale control.
            // A custom editor draws ONLY what it asks for — any field added to
            // MapConfig from here on must be added here too or it is unreachable.
            EditorGUILayout.Space();
            SerializedProperty camera = serializedObject.FindProperty("hunterCamera");
            EditorGUILayout.LabelField("Hunter Camera", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(camera, new GUIContent("Mode"));
            if ((MapHunterCamera)camera.enumValueIndex != MapHunterCamera.Auto)
            {
                EditorGUILayout.HelpBox(
                    "This map takes the camera choice away from the hunter: their toggle is "
                    + "disabled and its on-screen hint is hidden for as long as the map is "
                    + "loaded. Hiders are always third person and are unaffected.",
                    MessageType.Info);
            }

            serializedObject.ApplyModifiedProperties(); // routes through OnValidate → live preview
        }
    }
}
