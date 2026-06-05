using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using EOS.Logging;
using EOS.Objects;

namespace EOS.Unity
{
    public static class EosCloneUtility
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        const int MaxDepth = 32;

        public static void CopyDeclaredFields(EosObject source, EosObject target)
        {
            if (source == null || target == null) return;

            var type = source.GetType();
            while (type != null && type != typeof(EosObject) && type != typeof(object))
            {
                foreach (var field in type.GetFields(Fields))
                {
                    if (field.IsStatic || field.IsLiteral || field.IsInitOnly) continue;

                    try
                    {
                        var value = field.GetValue(source);
                        field.SetValue(target, Clone(value, 0));
                    }
                    catch (Exception ex)
                    {
                        EosLog.Error($"copy field '{field.Name}' threw: {ex.Message}", nameof(EosCloneUtility));
                    }
                }
                type = type.BaseType;
            }
        }

        public static object Clone(object value, int depth = 0)
        {
            if (value == null) return null;
            if (depth >= MaxDepth) return value;

            var type = value.GetType();

            if (type.IsPrimitive || type.IsEnum || value is string || value is decimal)
                return value;

            if (value is UnityEngine.Object)
                return value;

            if (type.IsArray)
            {
                var source = (Array)value;
                var elementType = type.GetElementType();
                var clone = Array.CreateInstance(elementType, source.Length);
                for (int i = 0; i < source.Length; i++)
                    clone.SetValue(Clone(source.GetValue(i), depth + 1), i);
                return clone;
            }

            if (value is IList list && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var clone = (IList)Activator.CreateInstance(type);
                for (int i = 0; i < list.Count; i++)
                    clone.Add(Clone(list[i], depth + 1));
                return clone;
            }

            if (type.IsValueType)
                return CloneFields(value, type, depth);

            if (Attribute.IsDefined(type, typeof(SerializableAttribute)) && type.GetConstructor(Type.EmptyTypes) != null)
            {
                try { return CloneFields(Activator.CreateInstance(type), type, depth, value); }
                catch (Exception ex) { EosLog.Error($"clone '{type.Name}' threw: {ex.Message}", nameof(EosCloneUtility)); }
            }

            return value;
        }

        static object CloneFields(object value, Type type, int depth)
        {
            object boxed = value;
            foreach (var field in AllInstanceFields(type))
            {
                try { field.SetValue(boxed, Clone(field.GetValue(value), depth + 1)); }
                catch (Exception ex) { EosLog.Error($"clone field '{field.Name}' threw: {ex.Message}", nameof(EosCloneUtility)); }
            }
            return boxed;
        }

        static object CloneFields(object target, Type type, int depth, object source)
        {
            foreach (var field in AllInstanceFields(type))
            {
                if (field.IsInitOnly || field.IsLiteral) continue;
                try { field.SetValue(target, Clone(field.GetValue(source), depth + 1)); }
                catch (Exception ex) { EosLog.Error($"clone field '{field.Name}' threw: {ex.Message}", nameof(EosCloneUtility)); }
            }
            return target;
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
