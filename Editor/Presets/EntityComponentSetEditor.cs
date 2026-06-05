#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace EOS.Unity.Editor
{
    [CustomEditor(typeof(EntityComponentSet))]
    public sealed class EntityComponentSetEditor : UnityEditor.Editor
    {
        SerializedProperty _tags;
        SerializedProperty _components;
        readonly AdvancedDropdownState _pickerState = new();

        void OnEnable()
        {
            _tags = serializedObject.FindProperty("_tags");
            _components = serializedObject.FindProperty("_components");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Components and tags defined here are applied to every preset that references this set, " +
                "and cannot be removed from those presets individually.",
                MessageType.None);

            PresetEditorUtility.DrawTagList(serializedObject, _tags);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Components", EditorStyles.boldLabel);
            PresetEditorUtility.DrawComponentList(serializedObject, _components, _pickerState, PresetEditorUtility.AssetKey(target));

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
