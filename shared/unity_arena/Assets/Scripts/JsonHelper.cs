using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TsingYun.UnityArena
{
    // Minimal JSON serializer for snake_case proto-shaped dicts. Unity's
    // built-in JsonUtility doesn't handle Dictionary; we don't want a
    // Newtonsoft.Json dependency for an HDRP runtime project. Supports:
    // string, bool, int, long, float, double, Dictionary, List.
    public static class JsonHelper
    {
        public static string SerializeDict(Dictionary<string, object> dict)
        {
            var sb = new StringBuilder();
            AppendDict(sb, dict);
            return sb.ToString();
        }

        private static void AppendValue(StringBuilder sb, object value)
        {
            if (value == null) { sb.Append("null"); return; }
            switch (value)
            {
                case string s: AppendString(sb, s); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); break;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); break;
                case float f: sb.Append(f.ToString("R", CultureInfo.InvariantCulture)); break;
                case double d: sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); break;
                case Dictionary<string, object> dict: AppendDict(sb, dict); break;
                case List<object> list: AppendList(sb, list); break;
                default: AppendString(sb, value.ToString()); break;
            }
        }

        private static void AppendDict(StringBuilder sb, Dictionary<string, object> dict)
        {
            sb.Append('{');
            bool first = true;
            foreach (var kv in dict)
            {
                if (!first) sb.Append(',');
                AppendString(sb, kv.Key);
                sb.Append(':');
                AppendValue(sb, kv.Value);
                first = false;
            }
            sb.Append('}');
        }

        private static void AppendList(StringBuilder sb, List<object> list)
        {
            sb.Append('[');
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append(',');
                AppendValue(sb, list[i]);
            }
            sb.Append(']');
        }

        private static void AppendString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
