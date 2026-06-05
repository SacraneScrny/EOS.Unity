#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace EOS.Unity.Editor
{
    [CustomEditor(typeof(EntityComponentSet))]
    public sealed class EntityComponentSetEditor : UnityEditor.Editor
    {
        SerializedProperty _tags;
        SerializedProperty _components;

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

            EditorGUILayout.PropertyField(_tags, true);

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_components, true);
            PresetEditorUtility.DrawAddComponentButton(serializedObject, _components);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
