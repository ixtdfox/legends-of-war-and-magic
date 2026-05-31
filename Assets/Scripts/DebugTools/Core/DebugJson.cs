using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

namespace LegendsOfWarAndMagic.DebugTools.Core
{
    public static class DebugJson
    {
        public static string ToJson(object value, bool pretty = true)
        {
            var builder = new StringBuilder(4096);
            WriteValue(builder, value, pretty, 0, new HashSet<object>(ReferenceEqualityComparer.Instance));
            return builder.ToString();
        }

        private static void WriteValue(StringBuilder builder, object value, bool pretty, int depth, HashSet<object> seen)
        {
            if (value == null)
            {
                builder.Append("null");
                return;
            }

            switch (value)
            {
                case string text:
                    WriteString(builder, text);
                    return;
                case char character:
                    WriteString(builder, character.ToString());
                    return;
                case bool boolean:
                    builder.Append(boolean ? "true" : "false");
                    return;
                case Enum enumValue:
                    WriteString(builder, enumValue.ToString());
                    return;
                case DateTime dateTime:
                    WriteString(builder, DebugClock.ToIsoUtc(dateTime));
                    return;
                case Vector2 vector2:
                    WriteObject(builder, pretty, depth, seen, new Dictionary<string, object>
                    {
                        ["x"] = vector2.x,
                        ["y"] = vector2.y
                    });
                    return;
                case Vector2Int vector2Int:
                    WriteObject(builder, pretty, depth, seen, new Dictionary<string, object>
                    {
                        ["x"] = vector2Int.x,
                        ["y"] = vector2Int.y
                    });
                    return;
                case Vector3 vector3:
                    WriteObject(builder, pretty, depth, seen, new Dictionary<string, object>
                    {
                        ["x"] = vector3.x,
                        ["y"] = vector3.y,
                        ["z"] = vector3.z
                    });
                    return;
                case Quaternion quaternion:
                    WriteObject(builder, pretty, depth, seen, new Dictionary<string, object>
                    {
                        ["x"] = quaternion.x,
                        ["y"] = quaternion.y,
                        ["z"] = quaternion.z,
                        ["w"] = quaternion.w,
                        ["euler"] = quaternion.eulerAngles
                    });
                    return;
                case Bounds bounds:
                    WriteObject(builder, pretty, depth, seen, new Dictionary<string, object>
                    {
                        ["center"] = bounds.center,
                        ["size"] = bounds.size,
                        ["min"] = bounds.min,
                        ["max"] = bounds.max
                    });
                    return;
                case Color color:
                    WriteObject(builder, pretty, depth, seen, new Dictionary<string, object>
                    {
                        ["r"] = color.r,
                        ["g"] = color.g,
                        ["b"] = color.b,
                        ["a"] = color.a
                    });
                    return;
                case UnityEngine.Object unityObject:
                    WriteObject(builder, pretty, depth, seen, new Dictionary<string, object>
                    {
                        ["name"] = unityObject.name,
                        ["type"] = unityObject.GetType().Name
                    });
                    return;
            }

            if (IsNumber(value))
            {
                if (value is float floatValue && (float.IsNaN(floatValue) || float.IsInfinity(floatValue)) ||
                    value is double doubleValue && (double.IsNaN(doubleValue) || double.IsInfinity(doubleValue)))
                {
                    builder.Append("null");
                    return;
                }

                builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }

            if (value is IDictionary dictionary)
            {
                WriteDictionary(builder, dictionary, pretty, depth, seen);
                return;
            }

            if (value is IEnumerable enumerable)
            {
                WriteArray(builder, enumerable, pretty, depth, seen);
                return;
            }

            var type = value.GetType();
            if (!type.IsValueType)
            {
                if (seen.Contains(value))
                {
                    WriteString(builder, "<cycle>");
                    return;
                }

                seen.Add(value);
            }

            var fields = new Dictionary<string, object>();
            var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            for (var i = 0; i < properties.Length; i++)
            {
                var property = properties[i];
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                try
                {
                    fields[property.Name] = property.GetValue(value);
                }
                catch
                {
                    fields[property.Name] = "<unreadable>";
                }
            }

            var publicFields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
            for (var i = 0; i < publicFields.Length; i++)
            {
                var field = publicFields[i];
                fields[field.Name] = field.GetValue(value);
            }

            if (fields.Count == 0)
            {
                WriteString(builder, value.ToString());
                return;
            }

            WriteObject(builder, pretty, depth, seen, fields);
        }

        private static void WriteDictionary(StringBuilder builder, IDictionary dictionary, bool pretty, int depth, HashSet<object> seen)
        {
            builder.Append('{');
            var first = true;
            foreach (DictionaryEntry entry in dictionary)
            {
                if (!first)
                {
                    builder.Append(',');
                }

                NewLine(builder, pretty, depth + 1);
                WriteString(builder, Convert.ToString(entry.Key, CultureInfo.InvariantCulture));
                builder.Append(pretty ? ": " : ":");
                WriteValue(builder, entry.Value, pretty, depth + 1, seen);
                first = false;
            }

            NewLine(builder, pretty, depth);
            builder.Append('}');
        }

        private static void WriteObject(StringBuilder builder, bool pretty, int depth, HashSet<object> seen, IDictionary<string, object> fields)
        {
            builder.Append('{');
            var first = true;
            foreach (var pair in fields)
            {
                if (!first)
                {
                    builder.Append(',');
                }

                NewLine(builder, pretty, depth + 1);
                WriteString(builder, pair.Key);
                builder.Append(pretty ? ": " : ":");
                WriteValue(builder, pair.Value, pretty, depth + 1, seen);
                first = false;
            }

            NewLine(builder, pretty, depth);
            builder.Append('}');
        }

        private static void WriteArray(StringBuilder builder, IEnumerable enumerable, bool pretty, int depth, HashSet<object> seen)
        {
            builder.Append('[');
            var first = true;
            foreach (var item in enumerable)
            {
                if (!first)
                {
                    builder.Append(',');
                }

                NewLine(builder, pretty, depth + 1);
                WriteValue(builder, item, pretty, depth + 1, seen);
                first = false;
            }

            NewLine(builder, pretty, depth);
            builder.Append(']');
        }

        private static void WriteString(StringBuilder builder, string value)
        {
            builder.Append('"');
            if (!string.IsNullOrEmpty(value))
            {
                for (var i = 0; i < value.Length; i++)
                {
                    var c = value[i];
                    switch (c)
                    {
                        case '"':
                            builder.Append("\\\"");
                            break;
                        case '\\':
                            builder.Append("\\\\");
                            break;
                        case '\b':
                            builder.Append("\\b");
                            break;
                        case '\f':
                            builder.Append("\\f");
                            break;
                        case '\n':
                            builder.Append("\\n");
                            break;
                        case '\r':
                            builder.Append("\\r");
                            break;
                        case '\t':
                            builder.Append("\\t");
                            break;
                        default:
                            if (char.IsControl(c))
                            {
                                builder.Append("\\u");
                                builder.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                            }
                            else
                            {
                                builder.Append(c);
                            }

                            break;
                    }
                }
            }

            builder.Append('"');
        }

        private static void NewLine(StringBuilder builder, bool pretty, int depth)
        {
            if (!pretty)
            {
                return;
            }

            builder.AppendLine();
            builder.Append(' ', depth * 2);
        }

        private static bool IsNumber(object value)
        {
            return value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new();

            public new bool Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
