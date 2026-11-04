using System;
using System.Collections;
using System.Reflection;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace GreyWardenTestBridge
{
    /// <summary>
    /// 把模组内部状态对象原样转成 JSON：稳定 Id（StringId）和原始数值，时间统一给 hours（PROTOCOL.md §5「快照字段」）。
    /// 只读字段（自动属性的后备字段和公开字段），**不调用任何属性 getter**——灰袍的部分 getter 带缓存写入，
    /// 查询必须不改游戏状态。
    /// </summary>
    internal static class BridgeSnapshot
    {
        private const int MaxDepth = 3;

        internal static JObject Fields(object source) => Fields(source, 0);

        private static JObject Fields(object source, int depth)
        {
            var result = new JObject();
            for (Type? type = source.GetType(); type != null && type != typeof(object); type = type.BaseType)
            {
                foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    string? name = FieldName(field);
                    if (name == null || result.ContainsKey(name)) continue;
                    result[name] = Value(field.GetValue(source), depth + 1);
                }
            }

            return result;
        }

        /// <summary>自动属性 <c>&lt;X&gt;k__BackingField</c> 记作 X；公开字段照原名；其余私有字段（缓存）不输出。</summary>
        private static string? FieldName(FieldInfo field)
        {
            const string backing = ">k__BackingField";
            if (field.Name.StartsWith("<", StringComparison.Ordinal) && field.Name.EndsWith(backing, StringComparison.Ordinal))
                return field.Name.Substring(1, field.Name.Length - 1 - backing.Length);
            return field.IsPublic ? field.Name : null;
        }

        internal static JToken Value(object? value) => Value(value, 0);

        private static JToken Value(object? value, int depth)
        {
            switch (value)
            {
                case null:
                    return JValue.CreateNull();
                case string text:
                    return text;
                case bool flag:
                    return flag;
                case Enum enumValue:
                    return enumValue.ToString();
                case int _:
                case long _:
                case short _:
                case byte _:
                case uint _:
                case float _:
                case double _:
                case decimal _:
                    return new JValue(value);
                case CampaignTime time:
                    return new JObject { ["hours"] = time.ToHours };
                case Vec2 point:
                    return new JArray(point.x, point.y);
                case CampaignVec2 point:
                    return new JArray(point.X, point.Y);
                case MBObjectBase mbObject:
                    return mbObject.StringId;
                case IFaction faction:
                    return faction.StringId;
                case TextObject textObject:
                    return textObject.ToString();
                case IDictionary dictionary when depth < MaxDepth:
                    var map = new JObject();
                    foreach (DictionaryEntry entry in dictionary)
                        map[entry.Key?.ToString() ?? "null"] = Value(entry.Value, depth + 1);
                    return map;
                case IEnumerable items when depth < MaxDepth:
                    var array = new JArray();
                    foreach (object? item in items) array.Add(Value(item, depth + 1));
                    return array;
                default:
                    Type type = value.GetType();
                    if (depth < MaxDepth && type.IsClass && type.Namespace == "GreyWardenPolicePurity")
                        return Fields(value, depth);
                    return "<" + type.Name + ">";
            }
        }
    }
}
