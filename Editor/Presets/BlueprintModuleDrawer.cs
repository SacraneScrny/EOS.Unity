#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EOS.Logging;
using UnityEditor;
using UnityEngine;

namespace EOS.Unity.Editor
{
    /// <summary>Inspector drawer for <see cref="BlueprintModule"/>: a socket dropdown sourced from the enclosing blueprint's incarnation <see cref="SocketSet"/> (refreshed when the incarnation changes) plus the nested blueprint picker.</summary>
    [CustomPropertyDrawer(typeof(BlueprintModule))]
    public sealed class BlueprintModuleDrawer : PropertyDrawer
    {
        const string IndexAssetPath = "Assets/Resources/incarnations.json";
        const string ModulesMarker = ".Modules.Array.data[";
        const string CustomOption = "<custom…>";
        const double CacheSeconds = 2.0;

        static readonly Dictionary<string, string[]> _cache = new();
        static double _loadedAt = double.NegativeInfinity;

        /// <summary>Height of the socket line plus the nested blueprint property.</summary>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var module = property.FindPropertyRelative("Module");
            return EditorGUIUtility.singleLineHeight
                + EditorGUIUtility.standardVerticalSpacing
                + EditorGUI.GetPropertyHeight(module, true);
        }

        /// <summary>Draws the socket dropdown then the nested blueprint subclass picker.</summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var socketId = property.FindPropertyRelative("SocketId");
            var module = property.FindPropertyRelative("Module");

            float line = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;

            var socketRect = new Rect(position.x, position.y, position.width, line);
            DrawSocketField(socketRect, socketId, SocketsFor(property));

            var moduleRect = new Rect(position.x, position.y + line + spacing, position.width, EditorGUI.GetPropertyHeight(module, true));
            EditorGUI.PropertyField(moduleRect, module, new GUIContent("Module"), true);
        }

        static void DrawSocketField(Rect rect, SerializedProperty socketId, string[] sockets)
        {
            var label = new GUIContent("Socket");
            if (sockets.Length == 0)
            {
                socketId.stringValue = EditorGUI.TextField(rect, label, socketId.stringValue);
                return;
            }

            var content = EditorGUI.PrefixLabel(rect, label);
            int selected = Array.IndexOf(sockets, socketId.stringValue);

            var options = new string[sockets.Length + 1];
            Array.Copy(sockets, options, sockets.Length);
            options[sockets.Length] = CustomOption;
            int shown = selected >= 0 ? selected : sockets.Length;

            bool custom = selected < 0;
            float popupWidth = custom ? content.width * 0.5f : content.width;
            var popupRect = new Rect(content.x, content.y, popupWidth, content.height);

            int picked = EditorGUI.Popup(popupRect, shown, options);
            if (picked >= 0 && picked < sockets.Length)
                socketId.stringValue = sockets[picked];

            if (custom)
            {
                var textRect = new Rect(content.x + popupWidth + 2f, content.y, content.width - popupWidth - 2f, content.height);
                socketId.stringValue = EditorGUI.TextField(textRect, socketId.stringValue);
            }
        }

        static string[] SocketsFor(SerializedProperty moduleEntry)
        {
            string path = moduleEntry.propertyPath;
            int idx = path.LastIndexOf(ModulesMarker, StringComparison.Ordinal);
            if (idx < 0) return Array.Empty<string>();

            string blueprintPath = path.Substring(0, idx);
            var idProp = moduleEntry.serializedObject.FindProperty(blueprintPath + ".IncarnationId");
            return LoadSockets(idProp != null ? idProp.stringValue : null);
        }

        static string[] LoadSockets(string id)
        {
            if (string.IsNullOrEmpty(id)) return Array.Empty<string>();

            if (EditorApplication.timeSinceStartup - _loadedAt >= CacheSeconds)
            {
                _cache.Clear();
                _loadedAt = EditorApplication.timeSinceStartup;
            }
            if (_cache.TryGetValue(id, out var cached)) return cached;

            var sockets = ReadSockets(id);
            _cache[id] = sockets;
            return sockets;
        }

        static string[] ReadSockets(string id)
        {
            try
            {
                if (!File.Exists(IndexAssetPath)) return Array.Empty<string>();

                var index = JsonUtility.FromJson<IncarnationIndex>(File.ReadAllText(IndexAssetPath));
                var entry = index?.Entries?.FirstOrDefault(e => e.Id == id);
                if (entry == null || string.IsNullOrEmpty(entry.Path)) return Array.Empty<string>();

                var prefab = Resources.Load<GameObject>(entry.Path);
                var set = prefab != null ? prefab.GetComponent<SocketSet>() : null;
                if (set?.Sockets == null) return Array.Empty<string>();

                var ids = new List<string>();
                foreach (var socket in set.Sockets)
                    if (socket != null && !string.IsNullOrEmpty(socket.Id))
                        ids.Add(socket.Id);
                return ids.ToArray();
            }
            catch (Exception ex)
            {
                EosLog.Warning($"failed to read sockets for '{id}': {ex.Message}", nameof(BlueprintModuleDrawer));
                return Array.Empty<string>();
            }
        }
    }
}
#endif
