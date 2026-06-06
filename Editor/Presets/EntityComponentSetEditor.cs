#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace EOS.Unity.Editor
{
    [CustomEditor(typeof(EntityComponentSet))]
    public sealed class EntityComponentSetEditor : UnityEditor.Editor
    {
        SerializedProperty _module;
        SerializedProperty _tags;
        SerializedProperty _components;
        readonly PresetEditorUtility.PickerController _componentPicker = new();
        readonly PresetEditorUtility.PickerController _modulePicker = new();

        void OnEnable()
        {
            _module = serializedObject.FindProperty("_module");
            _tags = serializedObject.FindProperty("_tags");
            _components = serializedObject.FindProperty("_components");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Components, tags and the optional code module here are applied to every preset that " +
                "references this set, and cannot be removed from those presets individually.",
                MessageType.None);

            var key = PresetEditorUtility.AssetKey(target);

            PresetEditorUtility.DrawSingleManagedReference(
                serializedObject, _module, _modulePicker, typeof(ComponentSetModule),
                "Code Module", key + ":module", Repaint);

            EditorGUILayout.Space();
            PresetEditorUtility.DrawTagList(serializedObject, _tags);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Components", EditorStyles.boldLabel);
            PresetEditorUtility.DrawComponentList(serializedObject, _components, _componentPicker, key, Repaint);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
