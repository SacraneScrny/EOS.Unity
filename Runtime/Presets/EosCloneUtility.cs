using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using EOS.Logging;
using EOS.Objects;
using UnityEngine;

namespace EOS.Unity
{
    /// <summary>Unity-serialization-aligned deep copy used to clone preset/set component templates into live components; copies exactly the fields Unity would serialize.</summary>
    public static class EosCloneUtility
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        const int MaxDepth = 32;

        static readonly Dictionary<Type, FieldInfo[]> _componentFields = new();
        static readonly Dictionary<Type, FieldInfo[]> _objectFields = new();
        static readonly Dictionary<Type, bool> _plainValueTypes = new();

        /// <summary>Deep-copies every Unity-serialized field declared from <paramref name="source"/>'s type down to the <see cref="EosObject"/> boundary onto <paramref name="target"/>; no-op if either is null.</summary>
        public static void CopyDeclaredFields(EosObject source, EosObject target)
        {
            if (source == null || target == null) return;

            var fields = SerializedFieldsOf(source.GetType(), typeof(EosObject), _componentFields);
            for (int i = 0; i < fields.Length; i++)
            {
                var field = fields[i];
                try
                {
                    if (TryClone(field.GetValue(source), 0, out var clone))
                        field.SetValue(target, clone);
                }
                catch (Exception ex)
                {
                    EosLog.Error($"copy field '{field.Name}' threw: {ex}", nameof(EosCloneUtility));
                }
            }
        }

        /// <summary>Deep-clones <paramref name="value"/> (arrays, <c>List&lt;&gt;</c>, <c>[Serializable]</c> classes, non-plain structs; primitives/strings/<see cref="UnityEngine.Object"/> passed through), returning false for unsupported types or when <paramref name="depth"/> exceeds the cap of 32.</summary>
        public static bool TryClone(object value, int depth, out object clone)
        {
            clone = null;
            if (value == null) return true;
            if (depth >= MaxDepth)
            {
                EosLog.Warning($"clone depth limit reached at '{value.GetType().Name}', value skipped", nameof(EosCloneUtility));
                return false;
            }

            var type = value.GetType();

            if (type.IsPrimitive || type.IsEnum || value is string || value is decimal)
            {
                clone = value;
                return true;
            }

            if (value is UnityEngine.Object)
            {
                clone = value;
                return true;
            }

            if (type.IsArray)
            {
                var source = (Array)value;
                var elementType = type.GetElementType();
                var array = Array.CreateInstance(elementType, source.Length);
                for (int i = 0; i < source.Length; i++)
                    if (TryClone(source.GetValue(i), depth + 1, out var element))
                        array.SetValue(element, i);
                clone = array;
                return true;
            }

            if (value is IList list && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var target = (IList)Activator.CreateInstance(type);
                for (int i = 0; i < list.Count; i++)
                    target.Add(TryClone(list[i], depth + 1, out var element) ? element : null);
                clone = target;
                return true;
            }

            if (type.IsValueType)
            {
                clone = IsPlainValueType(type) ? value : CloneStruct(value, type, depth);
                return true;
            }

            if (IsCloneableClass(type))
            {
                try
                {
                    clone = CloneClass(value, type, depth);
                    return true;
                }
                catch (Exception ex)
                {
                    EosLog.Error($"clone '{type.Name}' threw: {ex}", nameof(EosCloneUtility));
                    clone = null;
                    return false;
                }
            }

            return false;
        }

        static bool IsCloneableClass(Type type)
        {
            if (typeof(Delegate).IsAssignableFrom(type)) return false;
            if (type.Namespace != null && type.Namespace.StartsWith("System.Collections", StringComparison.Ordinal)) return false;
            if (!Attribute.IsDefined(type, typeof(SerializableAttribute))) return false;
            return type.GetConstructor(Type.EmptyTypes) != null;
        }

        static object CloneClass(object source, Type type, int depth)
        {
            var target = Activator.CreateInstance(type);
            var fields = SerializedFieldsOf(type, typeof(object), _objectFields);
            for (int i = 0; i < fields.Length; i++)
            {
                var field = fields[i];
                try
                {
                    if (TryClone(field.GetValue(source), depth + 1, out var clone))
                        field.SetValue(target, clone);
                }
                catch (Exception ex)
                {
                    EosLog.Error($"clone field '{field.Name}' threw: {ex}", nameof(EosCloneUtility));
                }
            }
            return target;
        }

        static object CloneStruct(object value, Type type, int depth)
        {
            object boxed = value;
            foreach (var field in AllInstanceFields(type))
            {
                var fieldType = field.FieldType;
                if (fieldType.IsValueType && IsPlainValueType(fieldType)) continue;
                if (fieldType == typeof(string) || typeof(UnityEngine.Object).IsAssignableFrom(fieldType)) continue;

                try
                {
                    field.SetValue(boxed, TryClone(field.GetValue(boxed), depth + 1, out var clone) ? clone : null);
                }
                catch (Exception ex)
                {
                    EosLog.Error($"clone field '{field.Name}' threw: {ex}", nameof(EosCloneUtility));
                }
            }
            return boxed;
        }

        static FieldInfo[] SerializedFieldsOf(Type type, Type boundary, Dictionary<Type, FieldInfo[]> cache)
        {
            if (cache.TryGetValue(type, out var cached)) return cached;

            var result = new List<FieldInfo>();
            var current = type;
            while (current != null && current != boundary && current != typeof(object))
            {
                foreach (var field in current.GetFields(Fields))
                    if (IsUnitySerialized(field))
                        result.Add(field);
                current = current.BaseType;
            }

            var array = result.ToArray();
            cache[type] = array;
            return array;
        }

        static bool IsUnitySerialized(FieldInfo field)
        {
            if (field.IsStatic || field.IsLiteral || field.IsInitOnly) return false;
            if (field.IsDefined(typeof(NonSerializedAttribute), false)) return false;
            if (field.IsPublic) return true;
            return field.IsDefined(typeof(SerializeField), false)
                || field.IsDefined(typeof(SerializeReference), false);
        }

        static bool IsPlainValueType(Type type)
        {
            if (type.IsPrimitive || type.IsEnum) return true;
            if (_plainValueTypes.TryGetValue(type, out var cached)) return cached;

            bool plain = true;
            foreach (var field in AllInstanceFields(type))
            {
                var fieldType = field.FieldType;
                if (fieldType.IsPrimitive || fieldType.IsEnum) continue;
                if (fieldType.IsValueType)
                {
                    if (IsPlainValueType(fieldType)) continue;
                    plain = false;
                    break;
                }
                plain = false;
                break;
            }

            _plainValueTypes[type] = plain;
            return plain;
        }

        static IEnumerable<FieldInfo> AllInstanceFields(Type type)
        {
            var current = type;
            while (current != null && current != typeof(object))
            {
                foreach (var field in current.GetFields(Fields))
                    if (!field.IsStatic)
                        yield return field;
                current = current.BaseType;
            }
        }
    }
}
