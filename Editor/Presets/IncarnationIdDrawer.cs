#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using EOS.Logging;
using UnityEditor;
using UnityEngine;

namespace EOS.Unity.Editor
{
    /// <summary>Inspector drawer for <see cref="IncarnationIdAttribute"/> string fields: a text field plus a dropdown of ids loaded from <c>incarnations.json</c> (cached for 2 seconds), mirroring the <see cref="EntityPreset"/> incarnation picker.</summary>
    [CustomPropertyDrawer(typeof(IncarnationIdAttribute))]
    public sealed class IncarnationIdDrawer : PropertyDrawer
    {
        const string IndexAssetPath = "Assets/Resources/incarnations.json";
        const double CacheSeconds = 2.0;

        static string[] _ids = Array.Empty<string>();
        static double _loadedAt = double.NegativeInfinity;

        /// <summary>Draws the string field with an id dropdown when the index has entries; falls back to a plain field for non-string properties.</summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            EnsureLoaded();

            using (new EditorGUI.PropertyScope(position, label, property))
            {
                var content = EditorGUI.PrefixLabel(position, label);
                float popupWidth = _ids.Length > 0 ? Mathf.Min(140f, content.width * 0.5f) : 0f;
                var fieldRect = new Rect(content.x, content.y, content.width - popupWidth, content.height);

                property.stringValue = EditorGUI.TextField(fieldRect, property.stringValue);

                if (_ids.Length > 0)
                {
                    var popupRect = new Rect(content.x + content.width - popupWidth, content.y, popupWidth, content.height);
                    int current = Array.IndexOf(_ids, property.stringValue);
                    int picked = EditorGUI.Popup(popupRect, current, _ids);
                    if (picked >= 0 && picked != current)
                        property.stringValue = _ids[picked];
                }
            }
        }

        static void EnsureLoaded()
        {
            if (EditorApplication.timeSinceStartup - _loadedAt < CacheSeconds) return;
            _loadedAt = EditorApplication.timeSinceStartup;

            try
            {
                if (!File.Exists(IndexAssetPath))
                {
                    _ids = Array.Empty<string>();
                    return;
                }

                var index = JsonUtility.FromJson<IncarnationIndex>(File.ReadAllText(IndexAssetPath));
                _ids = index?.Entries != null
                    ? index.Entries.Select(e => e.Id).Where(id => !string.IsNullOrEmpty(id)).ToArray()
                    : Array.Empty<string>();
            }
            catch (Exception ex)
            {
                EosLog.Warning($"failed to read incarnation index: {ex.Message}", nameof(IncarnationIdDrawer));
                _ids = Array.Empty<string>();
            }
        }
    }
}
#endif
