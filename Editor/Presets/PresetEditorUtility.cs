#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using EOS.Objects;
using UnityEditor;
using UnityEngine;

namespace EOS.Unity.Editor
{
    static class PresetEditorUtility
    {
        public static void DrawAddComponentButton(SerializedObject serializedObject, SerializedProperty list, string label = "Add Component")
        {
            if (!GUILayout.Button(label)) return;

            var menu = new GenericMenu();
            foreach (var type in ConcreteComponentTypes())
            {
                var captured = type;
                menu.AddItem(new GUIContent(type.FullName.Replace('.', '/')), false, () =>
                {
                    serializedObject.Update();
                    int i = list.arraySize;
                    list.InsertArrayElementAtIndex(i);
                    list.GetArrayElementAtIndex(i).managedReferenceValue = Activator.CreateInstance(captured);
                    serializedObject.ApplyModifiedProperties();
                });
            }
            menu.ShowAsContext();
        }

        public static IEnumerable<Type> ConcreteComponentTypes()
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
    }
}
#endif
