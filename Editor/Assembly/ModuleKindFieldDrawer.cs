#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EOS.Unity.Editor
{
    [CustomPropertyDrawer(typeof(ModuleKindFieldAttribute))]
    public sealed class ModuleKindFieldDrawer : PropertyDrawer
    {
        const string Custom = "<custom…>";

        static List<string> _cache;
        static double _next;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            var content = EditorGUI.PrefixLabel(position, label);
            var kinds = Catalog();
            var current = property.stringValue ?? string.Empty;

            if (kinds.Count == 0)
            {
                property.stringValue = EditorGUI.TextField(content, current);
                EditorGUI.EndProperty();
                return;
            }

            float popupWidth = Mathf.Min(120f, content.width * 0.5f);
            var popupRect = new Rect(content.x, content.y, popupWidth, content.height);
            var textRect = new Rect(content.x + popupWidth + 4f, content.y, content.width - popupWidth - 4f, content.height);

            var options = new List<string>(kinds) { Custom };
            int selected = kinds.IndexOf(current);
            int shown = selected >= 0 ? selected : kinds.Count;

            int picked = EditorGUI.Popup(popupRect, shown, options.ToArray());
            if (picked != shown && picked >= 0 && picked < kinds.Count)
                current = kinds[picked];

            current = EditorGUI.TextField(textRect, current);
            property.stringValue = current;

            EditorGUI.EndProperty();
        }

        static List<string> Catalog()
        {
            if (_cache != null && EditorApplication.timeSinceStartup < _next)
                return _cache;

            var list = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:ModuleKindCatalog"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var catalog = AssetDatabase.LoadAssetAtPath<ModuleKindCatalog>(path);
                if (catalog?.Kinds == null) continue;

                for (int i = 0; i < catalog.Kinds.Count; i++)
                {
                    var kind = catalog.Kinds[i];
                    if (!string.IsNullOrEmpty(kind) && !list.Contains(kind))
                        list.Add(kind);
                }
            }

            _cache = list;
            _next = EditorApplication.timeSinceStartup + 2.0;
            return _cache;
        }
    }
}
#endif
