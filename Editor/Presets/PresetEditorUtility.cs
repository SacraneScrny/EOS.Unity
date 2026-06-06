#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using EOS.Objects;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace EOS.Unity.Editor
{
    static class PresetEditorUtility
    {
        const string FoldoutPrefix = "EOS.Preset.Foldout.";
        const string DeleteIcon = "✕";

        public enum RowAction { None, Delete, Revert, Override }

        public sealed class PickerController
        {
            public readonly AdvancedDropdownState State = new();
            Type _pending;

            public void Request(Type type) => _pending = type;

            public bool TryConsume(out Type type)
            {
                type = _pending;
                _pending = null;
                return type != null;
            }
        }

        public static string AssetKey(UnityEngine.Object obj)
        {
            var path = AssetDatabase.GetAssetPath(obj);
            return string.IsNullOrEmpty(path) ? obj.GetInstanceID().ToString() : AssetDatabase.AssetPathToGUID(path);
        }

        public static bool SectionFoldout(string key, string label)
        {
            var pref = FoldoutPrefix + key;
            bool current = EditorPrefs.GetBool(pref, true);
            bool now = EditorGUILayout.Foldout(current, label, true, EditorStyles.foldoutHeader);
            if (now != current) EditorPrefs.SetBool(pref, now);
            return now;
        }

        public static RowAction ComponentBlock(
            string key, string displayName,
            bool showDelete, bool showRevert, bool showOverride, Action drawBody)
        {
            var action = RowAction.None;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var row = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);

                const float iconW = 22f, wideW = 70f, gap = 4f;
                float x = row.xMax;
                Rect deleteRect = default, revertRect = default, overrideRect = default;

                if (showDelete) { x -= iconW; deleteRect = new Rect(x, row.y, iconW, row.height); x -= gap; }
                if (showRevert) { x -= wideW; revertRect = new Rect(x, row.y, wideW, row.height); x -= gap; }
                if (showOverride) { x -= wideW; overrideRect = new Rect(x, row.y, wideW, row.height); x -= gap; }

                var foldRect = new Rect(row.x, row.y, Mathf.Max(40f, x - row.x), row.height);

                var pref = FoldoutPrefix + key;
                bool current = EditorPrefs.GetBool(pref, true);
                bool now = EditorGUI.Foldout(foldRect, current, displayName, true);
                if (now != current) EditorPrefs.SetBool(pref, now);

                if (showOverride && GUI.Button(overrideRect, "Override")) action = RowAction.Override;
                if (showRevert && GUI.Button(revertRect, "Revert")) action = RowAction.Revert;
                if (showDelete && GUI.Button(deleteRect, new GUIContent(DeleteIcon, "Delete"))) action = RowAction.Delete;

                if (now && drawBody != null)
                {
                    EditorGUI.indentLevel++;
                    drawBody();
                    EditorGUI.indentLevel--;
                }
            }

            return action;
        }

        public static void DrawManagedReferenceBody(SerializedProperty element, bool editable)
        {
            using (new EditorGUI.DisabledScope(!editable))
            {
                var iterator = element.Copy();
                var end = element.GetEndProperty();
                bool enter = true;
                bool any = false;

                while (iterator.NextVisible(enter))
                {
                    if (SerializedProperty.EqualContents(iterator, end)) break;
                    enter = false;
                    any = true;
                    EditorGUILayout.PropertyField(iterator, true);
                }

                if (!any) EditorGUILayout.LabelField("(no editable fields)", EditorStyles.miniLabel);
            }
        }

        public static void DrawComponentList(SerializedObject serializedObject, SerializedProperty list, PickerController picker, string ownerKey, Action repaint)
        {
            if (picker.TryConsume(out var pendingType))
            {
                serializedObject.Update();
                int at = list.arraySize;
                list.InsertArrayElementAtIndex(at);
                list.GetArrayElementAtIndex(at).managedReferenceValue = Activator.CreateInstance(pendingType);
                serializedObject.ApplyModifiedProperties();
            }

            int deleteAt = -1, revertAt = -1;

            for (int i = 0; i < list.arraySize; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                var name = ManagedShortName(element);
                var key = ownerKey + ":component:" + name;

                var action = ComponentBlock(key, name, showDelete: true, showRevert: true, showOverride: false,
                    () => DrawManagedReferenceBody(element, true));
                if (action == RowAction.Delete) deleteAt = i;
                else if (action == RowAction.Revert) revertAt = i;
            }

            if (deleteAt >= 0)
            {
                var name = ManagedShortName(list.GetArrayElementAtIndex(deleteAt));
                if (EditorUtility.DisplayDialog("Delete component", $"Remove '{name}' from this list?", "Delete", "Cancel"))
                {
                    list.DeleteArrayElementAtIndex(deleteAt);
                    serializedObject.ApplyModifiedProperties();
                }
                GUIUtility.ExitGUI();
            }
            else if (revertAt >= 0)
            {
                var element = list.GetArrayElementAtIndex(revertAt);
                var name = ManagedShortName(element);
                if (EditorUtility.DisplayDialog("Revert component", $"Reset '{name}' to default values?", "Revert", "Cancel"))
                {
                    var type = element.managedReferenceValue?.GetType();
                    if (type != null)
                    {
                        try
                        {
                            element.managedReferenceValue = Activator.CreateInstance(type);
                            serializedObject.ApplyModifiedProperties();
                        }
                        catch (Exception ex) { Debug.LogError($"[EOS] revert '{name}' failed: {ex.Message}"); }
                    }
                }
                GUIUtility.ExitGUI();
            }

            DrawAddButton(picker, repaint);
        }

        public static void DrawAddButton(PickerController picker, Action repaint, string label = "Add Component")
        {
            var rect = GUILayoutUtility.GetRect(new GUIContent(label), GUI.skin.button);
            if (!GUI.Button(rect, label)) return;

            var dropdown = new ComponentPickerDropdown(picker.State, ConcreteComponentTypes().ToArray(), type =>
            {
                picker.Request(type);
                repaint?.Invoke();
            });
            dropdown.Show(rect);
        }

        public static void DrawTagList(SerializedObject serializedObject, SerializedProperty list, string header = "Tags")
        {
            EditorGUILayout.LabelField(header, EditorStyles.boldLabel);

            int removeAt = -1;
            for (int i = 0; i < list.arraySize; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.HorizontalScope())
                {
                    element.stringValue = EditorGUILayout.TextField(element.stringValue);
                    if (GUILayout.Button(new GUIContent(DeleteIcon, "Remove"), GUILayout.Width(22f)))
                        removeAt = i;
                }
            }

            if (removeAt >= 0)
            {
                list.DeleteArrayElementAtIndex(removeAt);
                serializedObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
            }

            if (GUILayout.Button("Add Tag"))
            {
                int i = list.arraySize;
                list.InsertArrayElementAtIndex(i);
                list.GetArrayElementAtIndex(i).stringValue = string.Empty;
                serializedObject.ApplyModifiedProperties();
            }
        }

        public static string ManagedShortName(SerializedProperty element)
        {
            var full = element.managedReferenceFullTypename;
            if (string.IsNullOrEmpty(full)) return "(none)";

            int space = full.IndexOf(' ');
            var typeName = space >= 0 ? full.Substring(space + 1) : full;
            int dot = typeName.LastIndexOf('.');
            if (dot >= 0) typeName = typeName.Substring(dot + 1);
            int plus = typeName.LastIndexOf('+');
            if (plus >= 0) typeName = typeName.Substring(plus + 1);
            return typeName;
        }

        public static string ShortName(Type type)
        {
            var name = type.Name;
            int tick = name.IndexOf('`');
            return tick >= 0 ? name.Substring(0, tick) : name;
        }

        public static IEnumerable<Type> ConcreteComponentTypes() => ConcreteTypesOf(typeof(EosObject));

        public static IEnumerable<Type> ConcreteTypesOf(Type baseType)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(SafeTypes)
                .Where(t => t != null
                    && baseType.IsAssignableFrom(t)
                    && !t.IsAbstract
                    && !t.IsGenericTypeDefinition
                    && t.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(t => t.FullName);
        }

        static void ShowTypePicker(Rect rect, PickerController picker, Type baseType, Action repaint)
        {
            var dropdown = new ComponentPickerDropdown(picker.State, ConcreteTypesOf(baseType).ToArray(), type =>
            {
                picker.Request(type);
                repaint?.Invoke();
            });
            dropdown.Show(rect);
        }

        public static void DrawSingleManagedReference(
            SerializedObject serializedObject, SerializedProperty property, PickerController picker,
            Type baseType, string label, string key, Action repaint)
        {
            if (picker.TryConsume(out var pendingType))
            {
                serializedObject.Update();
                property.managedReferenceValue = Activator.CreateInstance(pendingType);
                serializedObject.ApplyModifiedProperties();
            }

            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            bool assigned = !string.IsNullOrEmpty(property.managedReferenceFullTypename);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var row = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);

                if (!assigned)
                {
                    if (GUI.Button(row, "Set " + label))
                        ShowTypePicker(row, picker, baseType, repaint);
                    return;
                }

                const float iconW = 22f, wideW = 70f, gap = 4f;
                float x = row.xMax;
                var clearRect = new Rect(x - iconW, row.y, iconW, row.height); x -= iconW + gap;
                var changeRect = new Rect(x - wideW, row.y, wideW, row.height); x -= wideW + gap;
                var foldRect = new Rect(row.x, row.y, Mathf.Max(40f, x - row.x), row.height);

                var pref = FoldoutPrefix + key;
                bool current = EditorPrefs.GetBool(pref, true);
                bool now = EditorGUI.Foldout(foldRect, current, ManagedShortName(property), true);
                if (now != current) EditorPrefs.SetBool(pref, now);

                if (GUI.Button(changeRect, "Change"))
                    ShowTypePicker(changeRect, picker, baseType, repaint);

                if (GUI.Button(clearRect, new GUIContent(DeleteIcon, "Clear")))
                {
                    property.managedReferenceValue = null;
                    serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                }

                if (now)
                {
                    EditorGUI.indentLevel++;
                    DrawManagedReferenceBody(property, true);
                    EditorGUI.indentLevel--;
                }
            }
        }

        static IEnumerable<Type> SafeTypes(System.Reflection.Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch { return Array.Empty<Type>(); }
        }
    }
}
#endif
