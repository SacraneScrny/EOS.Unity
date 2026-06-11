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
        SerializedProperty _defaultModules;

        readonly PresetEditorUtility.PickerController _picker = new();
        string[] _incarnationIds;
        string _key;

        string _socketCacheId = "\0";
        string[] _socketIds = Array.Empty<string>();
        string[] _socketKinds = Array.Empty<string>();

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
            _defaultModules = serializedObject.FindProperty("_defaultModules");
            _key = PresetEditorUtility.AssetKey(target);
            ReloadIds();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawInfoBlock();
            EditorGUILayout.Space();
            DrawDataBlock();

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            DrawSpawnButton();
        }

        void DrawInfoBlock()
        {
            if (!PresetEditorUtility.SectionFoldout(_key + ":info", "Info")) return;

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_name);
            EditorGUILayout.PropertyField(_active);
            EditorGUILayout.PropertyField(_serializable);

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Incarnation", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_view);
            DrawIncarnationIdField();
            EditorGUI.indentLevel--;
        }

        void DrawDataBlock()
        {
            if (!PresetEditorUtility.SectionFoldout(_key + ":data", "Data")) return;

            EditorGUI.indentLevel++;

            PresetEditorUtility.DrawTagList(serializedObject, _tags);

            EditorGUILayout.Space();
            DrawSets();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Components", EditorStyles.boldLabel);
            PresetEditorUtility.DrawComponentList(serializedObject, _components, _picker, _key, Repaint);

            EditorGUILayout.Space();
            DrawDefaultModules();

            EditorGUI.indentLevel--;
        }

        void DrawDefaultModules()
        {
            if (_defaultModules == null) return;

            EditorGUILayout.LabelField("Default Modules (assembly)", EditorStyles.boldLabel);

            RefreshSocketCache();

            if (_socketIds.Length > 0)
            {
                EditorGUILayout.LabelField("Prefab sockets", EditorStyles.miniBoldLabel);
                EditorGUI.indentLevel++;
                for (int i = 0; i < _socketIds.Length; i++)
                    EditorGUILayout.LabelField($"• {_socketIds[i]}", $"kind: {_socketKinds[i]}");
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(2f);
            }
            else if (!string.IsNullOrEmpty(_incarnationId.stringValue))
            {
                EditorGUILayout.HelpBox("Incarnation prefab has no SocketSet (or is not resolvable). Socket ids are free text.", MessageType.None);
            }

            int remove = -1;
            for (int i = 0; i < _defaultModules.arraySize; i++)
            {
                var element = _defaultModules.GetArrayElementAtIndex(i);
                var socketId = element.FindPropertyRelative("SocketId");
                var module = element.FindPropertyRelative("Module");

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawSocketIdField(socketId);
                    EditorGUILayout.PropertyField(module, GUIContent.none);
                    if (GUILayout.Button("✕", GUILayout.Width(22f))) remove = i;
                }
            }

            if (remove >= 0)
                _defaultModules.DeleteArrayElementAtIndex(remove);

            if (GUILayout.Button("Add Default Module"))
                _defaultModules.InsertArrayElementAtIndex(_defaultModules.arraySize);
        }

        void DrawSocketIdField(SerializedProperty socketId)
        {
            if (_socketIds.Length == 0)
            {
                socketId.stringValue = EditorGUILayout.TextField(socketId.stringValue, GUILayout.Width(130f));
                return;
            }

            int selected = Array.IndexOf(_socketIds, socketId.stringValue);
            var options = new List<string>(_socketIds) { "<custom…>" };
            int shown = selected >= 0 ? selected : _socketIds.Length;

            int picked = EditorGUILayout.Popup(shown, options.ToArray(), GUILayout.Width(130f));
            if (picked != shown && picked >= 0 && picked < _socketIds.Length)
                socketId.stringValue = _socketIds[picked];
            else if (selected < 0)
                socketId.stringValue = EditorGUILayout.TextField(socketId.stringValue, GUILayout.Width(110f));
        }

        void RefreshSocketCache()
        {
            var id = _incarnationId.stringValue ?? string.Empty;
            if (id == _socketCacheId) return;

            _socketCacheId = id;
            _socketIds = Array.Empty<string>();
            _socketKinds = Array.Empty<string>();

            if (string.IsNullOrEmpty(id) || !File.Exists(IndexAssetPath)) return;

            try
            {
                var index = JsonUtility.FromJson<IncarnationIndex>(File.ReadAllText(IndexAssetPath));
                var entry = index?.Entries?.FirstOrDefault(e => e.Id == id);
                if (entry == null || string.IsNullOrEmpty(entry.Path)) return;

                var prefab = Resources.Load<GameObject>(entry.Path);
                var set = prefab != null ? prefab.GetComponent<SocketSet>() : null;
                if (set?.Sockets == null) return;

                var ids = new List<string>();
                var kinds = new List<string>();
                foreach (var socket in set.Sockets)
                {
                    if (socket == null || string.IsNullOrEmpty(socket.Id)) continue;
                    ids.Add(socket.Id);
                    kinds.Add(socket.Kind ?? string.Empty);
                }

                _socketIds = ids.ToArray();
                _socketKinds = kinds.ToArray();
            }
            catch
            {
                // leave caches empty; socket ids fall back to free text
            }
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

            int revertOverrideIndex = -1;
            SetComponentEntry materializeEntry = default;
            bool materialize = false;

            foreach (var entry in effective)
            {
                int overrideIndex = OverrideIndexOf(entry.Type);
                bool overridden = overrideIndex >= 0;
                var key = _key + ":set:" + entry.Type.FullName;

                if (overridden)
                {
                    var name = $"{PresetEditorUtility.ShortName(entry.Type)}  (override)";
                    var element = _setOverrides.GetArrayElementAtIndex(overrideIndex);
                    var action = PresetEditorUtility.ComponentBlock(
                        key, name, showDelete: false, showRevert: true, showOverride: false,
                        () => PresetEditorUtility.DrawManagedReferenceBody(element, true));
                    if (action == PresetEditorUtility.RowAction.Revert) revertOverrideIndex = overrideIndex;
                }
                else
                {
                    var name = $"{PresetEditorUtility.ShortName(entry.Type)}  ·  {entry.Set.name}";
                    Action body = SetComponentPreview(entry);
                    var action = PresetEditorUtility.ComponentBlock(
                        key, name, showDelete: false, showRevert: false, showOverride: true, body);
                    if (action == PresetEditorUtility.RowAction.Override) { materialize = true; materializeEntry = entry; }
                }
            }

            if (revertOverrideIndex >= 0)
            {
                var name = PresetEditorUtility.ManagedShortName(_setOverrides.GetArrayElementAtIndex(revertOverrideIndex));
                if (EditorUtility.DisplayDialog("Revert override", $"Drop the local override of '{name}' and re-sync to the set?", "Revert", "Cancel"))
                {
                    _setOverrides.DeleteArrayElementAtIndex(revertOverrideIndex);
                    serializedObject.ApplyModifiedProperties();
                }
                GUIUtility.ExitGUI();
            }
            else if (materialize)
            {
                Materialize(materializeEntry);
                GUIUtility.ExitGUI();
            }
        }

        Action SetComponentPreview(SetComponentEntry entry)
        {
            if (entry.SerializedIndex < 0)
                return () => EditorGUILayout.LabelField("Defined in code — Override to edit values.", EditorStyles.miniLabel);

            var setObject = new SerializedObject(entry.Set);
            var list = setObject.FindProperty("_components");
            if (list == null || entry.SerializedIndex >= list.arraySize) return null;

            var element = list.GetArrayElementAtIndex(entry.SerializedIndex);
            return () => PresetEditorUtility.DrawManagedReferenceBody(element, false);
        }

        void Materialize(SetComponentEntry entry)
        {
            if (entry.Template == null) return;

            try
            {
                var copy = (EosObject)Activator.CreateInstance(entry.Type);
                EosCloneUtility.CopyDeclaredFields(entry.Template, copy);

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

                var collection = set.Collect();
                var serialized = set.Components;

                var components = collection.Components;
                for (int i = 0; i < components.Count; i++)
                {
                    var component = components[i];
                    if (component == null) continue;

                    var type = component.GetType();
                    if (!seen.Add(type)) continue;

                    result.Add(new SetComponentEntry
                    {
                        Type = type,
                        Set = set,
                        Template = component,
                        SerializedIndex = ReferenceIndex(serialized, component),
                    });
                }
            }
            return result;
        }

        static int ReferenceIndex(IReadOnlyList<EosObject> list, EosObject value)
        {
            if (list == null) return -1;
            for (int i = 0; i < list.Count; i++)
                if (ReferenceEquals(list[i], value)) return i;
            return -1;
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

        struct SetComponentEntry
        {
            public Type Type;
            public EntityComponentSet Set;
            public EosObject Template;
            public int SerializedIndex;
        }
    }
}
#endif
