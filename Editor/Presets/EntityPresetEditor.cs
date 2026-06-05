#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EOS.Objects;
using UnityEditor;
using UnityEngine;

namespace EOS.Unity.Editor
{
    [CustomEditor(typeof(EntityPreset))]
    public sealed class EntityPresetEditor : UnityEditor.Editor
    {
        const string IndexAssetPath = "Assets/Resources/incarnations.json";

        SerializedProperty _name;
        SerializedProperty _active;
        SerializedProperty _serializable;
        SerializedProperty _view;
        SerializedProperty _incarnationId;
        SerializedProperty _tags;
        SerializedProperty _components;

        string[] _incarnationIds;

        void OnEnable()
        {
            _name = serializedObject.FindProperty("_entityName");
            _active = serializedObject.FindProperty("_active");
            _serializable = serializedObject.FindProperty("_serializable");
            _view = serializedObject.FindProperty("_incarnationView");
            _incarnationId = serializedObject.FindProperty("_incarnationId");
            _tags = serializedObject.FindProperty("_tags");
            _components = serializedObject.FindProperty("_components");
            ReloadIds();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_name);
            EditorGUILayout.PropertyField(_active);
            EditorGUILayout.PropertyField(_serializable);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Incarnation", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_view);
            DrawIncarnationIdField();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tags", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_tags, GUIContent.none, true);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Components", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_components, GUIContent.none, true);
            DrawAddComponent();

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            DrawSpawnButton();
        }

        void DrawIncarnationIdField()
        {
            if ((IncarnationViewKind)_view.intValue == IncarnationViewKind.None)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(_incarnationId);

                if (_incarnationIds != null && _incarnationIds.Length > 0)
                {
                    int index = Array.IndexOf(_incarnationIds, _incarnationId.stringValue);
                    int picked = EditorGUILayout.Popup(index, _incarnationIds, GUILayout.Width(140f));
                    if (picked >= 0 && picked != index)
                        _incarnationId.stringValue = _incarnationIds[picked];
                }

                if (GUILayout.Button("↻", GUILayout.Width(24f)))
                    ReloadIds();
            }
        }

        void DrawAddComponent()
        {
            if (!GUILayout.Button("Add Component")) return;

            var menu = new GenericMenu();
            foreach (var type in ConcreteComponentTypes())
            {
                var label = type.FullName.Replace('.', '/');
                menu.AddItem(new GUIContent(label), false, () => AddComponent(type));
            }
            menu.ShowAsContext();
        }

        void AddComponent(Type type)
        {
            serializedObject.Update();
            int i = _components.arraySize;
            _components.InsertArrayElementAtIndex(i);
            _components.GetArrayElementAtIndex(i).managedReferenceValue = Activator.CreateInstance(type);
            serializedObject.ApplyModifiedProperties();
        }

        static IEnumerable<Type> ConcreteComponentTypes()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(SafeTypes)
                .Where(t => t != null
                    && typeof(EosObject).IsAssignableFrom(t)
                    && !t.IsAbstract
                    && !t.IsGenericTypeDefinition
                    && t.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(t => t.FullName);
        }

        static IEnumerable<Type> SafeTypes(System.Reflection.Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch { return Array.Empty<Type>(); }
        }

        void DrawSpawnButton()
        {
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Spawn Into Default World (Play Mode)"))
                    ((EntityPreset)target).Instantiate();
            }
        }

        void ReloadIds()
        {
            try
            {
                if (!File.Exists(IndexAssetPath)) { _incarnationIds = Array.Empty<string>(); return; }
                var index = JsonUtility.FromJson<IncarnationIndex>(File.ReadAllText(IndexAssetPath));
                _incarnationIds = index?.Entries != null
                    ? index.Entries.Select(e => e.Id).Where(id => !string.IsNullOrEmpty(id)).ToArray()
                    : Array.Empty<string>();
            }
            catch
            {
                _incarnationIds = Array.Empty<string>();
            }
        }
    }
}
#endif
