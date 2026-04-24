using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using ModuTree.Runtime.Core;

namespace ModuTree.Runtime.Serialization
{
    /// <summary>
    /// 外部依存ゼロの自前JSONパーサー／シリアライザー。
    /// Unity・Newtonsoft・System.Text.Json 非依存。
    /// BehaviourTreeData の読み書きと、ノードパラメータの復元に使用する。
    /// </summary>
    public static class MiniJson
    {
        // ═══════════════════════════════════════════════════════════
        //  公開API
        // ═══════════════════════════════════════════════════════════

        /// <summary>JSON文字列をBehaviourTreeDataにデシリアライズする</summary>
        public static BehaviourTreeData Deserialize<BehaviourTreeData>(string json)
            where BehaviourTreeData : class, new()
        {
            var result = new BehaviourTreeData();
            var dict   = ParseObject(json.Trim());
            if (dict == null) return result;
            PopulateObject(dict, result);
            return result;
        }

        /// <summary>BehaviourTreeDataをJSON文字列にシリアライズする</summary>
        public static string Serialize(BehaviourTreeData treeData)
        {
            var sb = new StringBuilder();
            SerializeBehaviourTreeData(treeData, sb);
            return sb.ToString();
        }

        /// <summary>
        /// ノードインスタンスの公開フィールドをJSONにシリアライズする。
        /// [NodeFieldHide] 付きフィールドはスキップ。
        /// </summary>
        public static string SerializeFields(object obj)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            bool first = true;

            foreach (var field in obj.GetType().GetFields(
                BindingFlags.Public | BindingFlags.Instance))
            {
                // NodeFieldHide が付いているフィールドはスキップ
                if (field.GetCustomAttribute<NodeFieldHideAttribute>() != null)
                    continue;

                if (!first) sb.Append(',');
                first = false;

                sb.Append('"');
                sb.Append(EscapeString(field.Name));
                sb.Append('"');
                sb.Append(':');
                SerializeValue(field.GetValue(obj), sb);
            }

            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>
        /// JSON文字列をパースし、targetオブジェクトの公開フィールドに値を設定する。
        /// フィールド名は大文字小文字を区別しない。
        /// </summary>
        public static void PopulateFields(string json, object target)
        {
            var dict = ParseObject(json.Trim());
            if (dict == null) return;

            foreach (var kv in dict)
            {
                var field = FindField(target.GetType(), kv.Key);
                if (field == null) continue;
                SetFieldValue(field, target, kv.Value);
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  シリアライズ実装
        // ═══════════════════════════════════════════════════════════

        private static void SerializeBehaviourTreeData(BehaviourTreeData data, StringBuilder sb)
        {
            sb.Append('{');
            sb.Append("\"version\":");
            SerializeValue(data.version, sb);
            sb.Append(",\"rootGuid\":");
            SerializeValue(data.rootGuid, sb);
            sb.Append(",\"nodes\":[");

            bool firstNode = true;
            foreach (var node in data.nodes)
            {
                if (!firstNode) sb.Append(',');
                firstNode = false;
                SerializeNodeData(node, sb);
            }

            sb.Append("]}");
        }

        private static void SerializeNodeData(NodeData node, StringBuilder sb)
        {
            sb.Append('{');
            sb.Append("\"guid\":");
            SerializeValue(node.guid, sb);
            sb.Append(",\"typeName\":");
            SerializeValue(node.typeName, sb);
            sb.Append(",\"positionX\":");
            SerializeValue(node.positionX, sb);
            sb.Append(",\"positionY\":");
            SerializeValue(node.positionY, sb);
            sb.Append(",\"childrenGuids\":[");

            bool firstGuid = true;
            foreach (var g in node.childrenGuids)
            {
                if (!firstGuid) sb.Append(',');
                firstGuid = false;
                SerializeValue(g, sb);
            }

            sb.Append("],\"childGuid\":");
            SerializeValue(node.childGuid, sb);
            sb.Append(",\"parametersJson\":");
            SerializeValue(node.parametersJson, sb);
            sb.Append('}');
        }

        private static void SerializeValue(object value, StringBuilder sb)
        {
            if (value == null)
            {
                sb.Append("null");
                return;
            }

            switch (value)
            {
                case bool b:
                    sb.Append(b ? "true" : "false");
                    break;

                case int i:
                    sb.Append(i);
                    break;

                case long l:
                    sb.Append(l);
                    break;

                case float f:
                    // 小数点を保証するために G9 形式を使用
                    sb.Append(f.ToString("G9", System.Globalization.CultureInfo.InvariantCulture));
                    break;

                case double d:
                    sb.Append(d.ToString("G17", System.Globalization.CultureInfo.InvariantCulture));
                    break;

                case string s:
                    sb.Append('"');
                    sb.Append(EscapeString(s));
                    sb.Append('"');
                    break;

                case Enum e:
                    // enumは数値として保存
                    sb.Append(Convert.ToInt64(e));
                    break;

                default:
                    // フォールバック: ToString
                    sb.Append('"');
                    sb.Append(EscapeString(value.ToString()));
                    sb.Append('"');
                    break;
            }
        }

        private static string EscapeString(string s)
        {
            if (s == null) return "";
            var sb = new StringBuilder(s.Length);
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"':  sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n");  break;
                    case '\r': sb.Append("\\r");  break;
                    case '\t': sb.Append("\\t");  break;
                    case '\b': sb.Append("\\b");  break;
                    case '\f': sb.Append("\\f");  break;
                    default:
                        if (c < 0x20)
                        {
                            sb.Append("\\u");
                            sb.Append(((int)c).ToString("X4"));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }

        // ═══════════════════════════════════════════════════════════
        //  パーサー実装（再帰降下法）
        // ═══════════════════════════════════════════════════════════

        /// <summary>JSON文字列全体をパースしてオブジェクトグラフを返す</summary>
        private static Dictionary<string, object> ParseObject(string json)
        {
            int pos = 0;
            var value = ParseValue(json, ref pos);
            return value as Dictionary<string, object>;
        }

        /// <summary>任意のJSON値をパースする（再帰）</summary>
        private static object ParseValue(string json, ref int pos)
        {
            SkipWhitespace(json, ref pos);
            if (pos >= json.Length) return null;

            char c = json[pos];

            if (c == '{')  return ParseJsonObject(json, ref pos);
            if (c == '[')  return ParseJsonArray(json, ref pos);
            if (c == '"')  return ParseString(json, ref pos);
            if (c == 't')  return ParseLiteral(json, ref pos, "true",  true);
            if (c == 'f')  return ParseLiteral(json, ref pos, "false", false);
            if (c == 'n')  return ParseLiteral(json, ref pos, "null",  null);
            if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber(json, ref pos);

            // 予期しない文字: スキップ
            pos++;
            return null;
        }

        private static Dictionary<string, object> ParseJsonObject(string json, ref int pos)
        {
            var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            pos++; // '{'をスキップ

            SkipWhitespace(json, ref pos);
            if (pos < json.Length && json[pos] == '}')
            {
                pos++;
                return dict;
            }

            while (pos < json.Length)
            {
                SkipWhitespace(json, ref pos);
                if (pos >= json.Length) break;

                // キーを読む
                if (json[pos] != '"') break;
                var key = ParseString(json, ref pos);

                SkipWhitespace(json, ref pos);
                if (pos >= json.Length || json[pos] != ':') break;
                pos++; // ':'をスキップ

                // 値を読む
                var value = ParseValue(json, ref pos);
                dict[key] = value;

                SkipWhitespace(json, ref pos);
                if (pos >= json.Length) break;

                if (json[pos] == ',')
                {
                    pos++;
                    continue;
                }
                if (json[pos] == '}')
                {
                    pos++;
                    break;
                }
                break;
            }

            return dict;
        }

        private static List<object> ParseJsonArray(string json, ref int pos)
        {
            var list = new List<object>();
            pos++; // '['をスキップ

            SkipWhitespace(json, ref pos);
            if (pos < json.Length && json[pos] == ']')
            {
                pos++;
                return list;
            }

            while (pos < json.Length)
            {
                var value = ParseValue(json, ref pos);
                list.Add(value);

                SkipWhitespace(json, ref pos);
                if (pos >= json.Length) break;

                if (json[pos] == ',')
                {
                    pos++;
                    continue;
                }
                if (json[pos] == ']')
                {
                    pos++;
                    break;
                }
                break;
            }

            return list;
        }

        private static string ParseString(string json, ref int pos)
        {
            pos++; // 開始 '"' をスキップ
            var sb = new StringBuilder();

            while (pos < json.Length)
            {
                char c = json[pos];

                if (c == '"')
                {
                    pos++;
                    return sb.ToString();
                }

                if (c == '\\')
                {
                    pos++;
                    if (pos >= json.Length) break;
                    char escaped = json[pos];
                    switch (escaped)
                    {
                        case '"':  sb.Append('"');  break;
                        case '\\': sb.Append('\\'); break;
                        case '/':  sb.Append('/');  break;
                        case 'n':  sb.Append('\n'); break;
                        case 'r':  sb.Append('\r'); break;
                        case 't':  sb.Append('\t'); break;
                        case 'b':  sb.Append('\b'); break;
                        case 'f':  sb.Append('\f'); break;
                        case 'u':
                            // Unicodeエスケープ \uXXXX
                            if (pos + 4 < json.Length)
                            {
                                var hex = json.Substring(pos + 1, 4);
                                if (int.TryParse(hex,
                                    System.Globalization.NumberStyles.HexNumber,
                                    null, out int codePoint))
                                {
                                    sb.Append((char)codePoint);
                                    pos += 4;
                                }
                            }
                            break;
                        default:
                            sb.Append(escaped);
                            break;
                    }
                    pos++;
                    continue;
                }

                sb.Append(c);
                pos++;
            }

            return sb.ToString();
        }

        private static object ParseLiteral(string json, ref int pos, string literal, object result)
        {
            if (pos + literal.Length <= json.Length &&
                json.Substring(pos, literal.Length) == literal)
            {
                pos += literal.Length;
                return result;
            }
            // パース失敗時は進める
            pos++;
            return null;
        }

        private static object ParseNumber(string json, ref int pos)
        {
            int start = pos;
            if (pos < json.Length && json[pos] == '-') pos++;

            while (pos < json.Length && (json[pos] >= '0' && json[pos] <= '9'))
                pos++;

            bool isFloat = false;
            if (pos < json.Length && json[pos] == '.')
            {
                isFloat = true;
                pos++;
                while (pos < json.Length && (json[pos] >= '0' && json[pos] <= '9'))
                    pos++;
            }

            // 指数部
            if (pos < json.Length && (json[pos] == 'e' || json[pos] == 'E'))
            {
                isFloat = true;
                pos++;
                if (pos < json.Length && (json[pos] == '+' || json[pos] == '-'))
                    pos++;
                while (pos < json.Length && (json[pos] >= '0' && json[pos] <= '9'))
                    pos++;
            }

            var numStr = json.Substring(start, pos - start);

            if (isFloat)
            {
                if (double.TryParse(numStr,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double d))
                    return d;
            }
            else
            {
                if (long.TryParse(numStr, out long l))
                    return l;
            }

            return 0L;
        }

        private static void SkipWhitespace(string json, ref int pos)
        {
            while (pos < json.Length &&
                   (json[pos] == ' '  || json[pos] == '\t' ||
                    json[pos] == '\r' || json[pos] == '\n'))
                pos++;
        }

        // ═══════════════════════════════════════════════════════════
        //  オブジェクトへのデータ注入
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// パース済みDictionaryからターゲットオブジェクトのフィールドを設定する
        /// </summary>
        private static void PopulateObject(Dictionary<string, object> dict, object target)
        {
            if (dict == null || target == null) return;

            var type = target.GetType();

            foreach (var kv in dict)
            {
                var field    = FindField(type, kv.Key);
                var property = FindProperty(type, kv.Key);

                if (field != null)
                    SetFieldValue(field, target, kv.Value);
                else if (property != null)
                    SetPropertyValue(property, target, kv.Value);
            }
        }

        private static FieldInfo FindField(Type type, string name)
        {
            // 大文字小文字を区別しない検索
            return type.GetField(name,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        }

        private static PropertyInfo FindProperty(Type type, string name)
        {
            // Publicプロパティを大文字小文字を区別しない検索
            foreach (var prop in type.GetProperties(
                BindingFlags.Public | BindingFlags.Instance))
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase) &&
                    prop.CanWrite)
                    return prop;
            }
            return null;
        }

        private static void SetFieldValue(FieldInfo field, object target, object value)
        {
            try
            {
                var converted = ConvertValue(value, field.FieldType);
                field.SetValue(target, converted);
            }
            catch
            {
                // 型変換失敗時は無視する
            }
        }

        private static void SetPropertyValue(PropertyInfo prop, object target, object value)
        {
            try
            {
                var converted = ConvertValue(value, prop.PropertyType);
                prop.SetValue(target, converted);
            }
            catch
            {
                // 型変換失敗時は無視する
            }
        }

        /// <summary>
        /// パースされた値を目的の型に変換する
        /// </summary>
        private static object ConvertValue(object value, Type targetType)
        {
            if (value == null)
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

            // 型が一致している場合はそのまま返す
            if (targetType.IsAssignableFrom(value.GetType()))
                return value;

            // string
            if (targetType == typeof(string))
                return value.ToString();

            // bool
            if (targetType == typeof(bool))
            {
                if (value is bool b) return b;
                if (value is string s)
                {
                    if (s == "true")  return true;
                    if (s == "false") return false;
                }
                return Convert.ToBoolean(value);
            }

            // 数値型の変換
            if (targetType == typeof(int))    return Convert.ToInt32(value);
            if (targetType == typeof(long))   return Convert.ToInt64(value);
            if (targetType == typeof(float))  return Convert.ToSingle(value);
            if (targetType == typeof(double)) return Convert.ToDouble(value);
            if (targetType == typeof(short))  return Convert.ToInt16(value);
            if (targetType == typeof(byte))   return Convert.ToByte(value);
            if (targetType == typeof(uint))   return Convert.ToUInt32(value);
            if (targetType == typeof(ulong))  return Convert.ToUInt64(value);

            // enum
            if (targetType.IsEnum)
            {
                if (value is string enumStr)
                    return Enum.Parse(targetType, enumStr, ignoreCase: true);
                return Enum.ToObject(targetType, Convert.ToInt64(value));
            }

            // List<string>
            if (targetType == typeof(List<string>) && value is List<object> listObj)
            {
                var result = new List<string>();
                foreach (var item in listObj)
                    result.Add(item?.ToString());
                return result;
            }

            // List<T>（汎用）
            if (targetType.IsGenericType &&
                targetType.GetGenericTypeDefinition() == typeof(List<>) &&
                value is List<object> listItems)
            {
                var elementType = targetType.GetGenericArguments()[0];
                var list        = (IList)Activator.CreateInstance(targetType);
                foreach (var item in listItems)
                {
                    // リスト要素がDictの場合は再帰的に構築
                    if (item is Dictionary<string, object> itemDict &&
                        !elementType.IsPrimitive && elementType != typeof(string))
                    {
                        var element = Activator.CreateInstance(elementType);
                        PopulateObject(itemDict, element);
                        list.Add(element);
                    }
                    else
                    {
                        list.Add(ConvertValue(item, elementType));
                    }
                }
                return list;
            }

            // ネストされたオブジェクト（Dict -> クラスインスタンス）
            if (value is Dictionary<string, object> nestedDict &&
                !targetType.IsPrimitive && targetType != typeof(string))
            {
                var instance = Activator.CreateInstance(targetType);
                PopulateObject(nestedDict, instance);
                return instance;
            }

            // フォールバック
            return Convert.ChangeType(value,
                targetType,
                System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}