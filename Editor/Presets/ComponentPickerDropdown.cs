#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace EOS.Unity.Editor
{
    /// <summary>Namespace-tree <see cref="AdvancedDropdown"/> picker over a set of types, invoking a callback on selection.</summary>
    sealed class ComponentPickerDropdown : AdvancedDropdown
    {
        readonly Type[] _types;
        readonly Action<Type> _onPick;
        readonly Dictionary<AdvancedDropdownItem, Type> _byItem = new();

        /// <summary>Creates a picker over <paramref name="types"/> that calls <paramref name="onPick"/> with the chosen type.</summary>
        public ComponentPickerDropdown(AdvancedDropdownState state, Type[] types, Action<Type> onPick) : base(state)
        {
            _types = types;
            _onPick = onPick;
            minimumSize = new Vector2(260f, 360f);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            _byItem.Clear();
            var root = new AdvancedDropdownItem("Components");
            var folders = new Dictionary<string, AdvancedDropdownItem>();
            int id = 1;

            foreach (var type in _types)
            {
                var parent = root;
                var ns = type.Namespace;
                if (!string.IsNullOrEmpty(ns))
                {
                    var path = "";
                    foreach (var segment in ns.Split('.'))
                    {
                        path = path.Length == 0 ? segment : path + "." + segment;
                        if (!folders.TryGetValue(path, out var folder))
                        {
                            folder = new AdvancedDropdownItem(segment) { id = id++ };
                            folders[path] = folder;
                            parent.AddChild(folder);
                        }
                        parent = folder;
                    }
                }

                var leaf = new AdvancedDropdownItem(type.Name) { id = id++ };
                _byItem[leaf] = type;
                parent.AddChild(leaf);
            }

            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (_byItem.TryGetValue(item, out var type))
                _onPick?.Invoke(type);
        }
    }
}
#endif
