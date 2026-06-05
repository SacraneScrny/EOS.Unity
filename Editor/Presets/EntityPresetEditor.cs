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
        SerializedProperty _sets;
        SerializedProperty _setOverrides;

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
            _sets = serializedObject.FindProperty("_sets");
            _setOverrides = serializedObject.FindProperty("_setOverrides");
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
            EditorGUILayout.PropertyField(_tags, true);

            EditorGUILayout.Space();
            DrawSets();

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_components, true);
            PresetEditorUtility.DrawAddComponentButton(serializedObject, _components);

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            DrawSpawnButton();
        }

        void DrawSets()
        {
            EditorGUILayout.LabelField("Component Sets", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_sets, new GUIContent("Sets"), true);

            var effective = EffectiveSetComponents();
            PruneOrphanOverrides(effective);

            if (effective.Count == 0) return;

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Set Components (required)", EditorStyles.miniBoldLabel);

            foreach (var entry in effective)
                DrawSetComponent(entry);
        }

        void DrawSetComponent(SetComponentEntry entry)
        {
            int overrideIndex = OverrideIndexOf(entry.Type);
            bool overridden = overrideIndex >= 0;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var title = $"{ShortName(entry.Type)}  ·  {entry.Set.name}";
                    EditorGUILayout.LabelField(title, overridden ? EditorStyles.boldLabel : EditorStyles.label);
                    GUILayout.FlexibleSpace();

                    if (overridden)
                    {
                        if (GUILayout.Button("Revert", GUILayout.Width(64f)))
                        {
                            _setOverrides.DeleteArrayElementAtIndex(overrideIndex);
                            serializedObject.ApplyModifiedProperties();
                            GUIUtility.ExitGUI();
                        }
                    }
                    else if (GUILayout.Button("Override", GUILayout.Width(72f)))
                    {
                        Materialize(entry);
                        GUIUtility.ExitGUI();
                    }
                }

                if (overridden)
                    EditorGUILayout.PropertyField(_setOverrides.GetArrayElementAtIndex(overrideIndex), GUIContent.none, true);
                else
                    DrawSetSourceReadonly(entry);
            }
        }

        void DrawSetSourceReadonly(SetComponentEntry entry)
        {
            var setObject = new SerializedObject(entry.Set);
            var list = setObject.FindProperty("_components");
            if (list == null || entry.Index >= list.arraySize) return;

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(list.GetArrayElementAtIndex(entry.Index), GUIContent.none, true);
        }

        void Materialize(SetComponentEntry entry)
        {
            var source = SourceTemplate(entry);
            if (source == null) return;

            try
            {
                var copy = (EosObject)Activator.CreateInstance(entry.Type);
                EosCloneUtility.CopyDeclaredFields(source, copy);

                serializedObject.Update();
                int i = _setOverrides.arraySize;
                _setOverrides.InsertArrayElementAtIndex(i);
                _setOverrides.GetArrayElementAtIndex(i).managedReferenceValue = copy;
                serializedObject.ApplyModifiedProperties();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EOS] override '{entry.Type.Name}' failed: {ex.Message}");
            }
        }

        List<SetComponentEntry> EffectiveSetComponents()
        {
            var result = new List<SetComponentEntry>();
            var seen = new HashSet<Type>();
            var sets = ((EntityPreset)target).Sets;
            if (sets == null) return result;

            for (int s = 0; s < sets.Count; s++)
            {
                var set = sets[s];
                if (set == null) continue;

                var components = set.Components;
                if (components == null) continue;

                for (int i = 0; i < components.Count; i++)
                {
                    var component = components[i];
                    if (component == null) continue;

                    var type = component.GetType();
                    if (seen.Add(type))
                        result.Add(new SetComponentEntry { Type = type, Set = set, Index = i });
                }
            }
            return result;
        }

        EosObject SourceTemplate(SetComponentEntry entry)
        {
            var components = entry.Set.Components;
            return components != null && entry.Index < components.Count ? components[entry.Index] : null;
        }

        int OverrideIndexOf(Type type)
        {
            var overrides = ((EntityPreset)target).SetOverrides;
            if (overrides == null) return -1;
            for (int i = 0; i < overrides.Count; i++)
                if (overrides[i] != null && overrides[i].GetType() == type)
                    return i;
            return -1;
        }

        void PruneOrphanOverrides(List<SetComponentEntry> effective)
        {
            var overrides = ((EntityPreset)target).SetOverrides;
            if (overrides == null || overrides.Count == 0) return;

            var keep = new HashSet<Type>(effective.Select(e => e.Type));
            bool changed = false;

            for (int i = overrides.Count - 1; i >= 0; i--)
            {
                var o = overrides[i];
                if (o == null || !keep.Contains(o.GetType()))
                {
                    _setOverrides.DeleteArrayElementAtIndex(i);
                    changed = true;
                }
            }

            if (changed) serializedObject.ApplyModifiedProperties();
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

        static string ShortName(Type type)
        {
            var name = type.Name;
            int tick = name.IndexOf('`');
            return tick >= 0 ? name.Substring(0, tick) : name;
        }

        struct SetComponentEntry
        {
            public Type Type;
            public EntityComponentSet Set;
            public int Index;
        }
    }
}
#endif
