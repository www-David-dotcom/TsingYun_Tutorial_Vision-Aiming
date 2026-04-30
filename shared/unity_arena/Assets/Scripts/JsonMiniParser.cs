using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TsingYun.UnityArena
{
    // Minimal recursive-descent JSON parser. Handles the dict shapes the
    // candidate stack sends (env_reset / env_step / env_push_fire / env_finish
    // requests). Returns a Dictionary<string, object> tree where leaves are
    // string / double / long / bool / null.
    public static class JsonMiniParser
    {
        public static Dictionary<string, object> ParseDict(string json)
        {
            int i = 0;
            SkipWs(json, ref i);
            if (i >= json.Length || json[i] != '{') return null;
            return ReadDict(json, ref i);
        }

        private static Dictionary<string, object> ReadDict(string s, ref int i)
        {
            var dict = new Dictionary<string, object>();
            i++;  // consume '{'
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return dict; }
            while (i < s.Length)
            {
                SkipWs(s, ref i);
                string key = ReadString(s, ref i);
                SkipWs(s, ref i);
                if (s[i] != ':') return null;
                i++;
                SkipWs(s, ref i);
                object val = ReadValue(s, ref i);
                dict[key] = val;
                SkipWs(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return dict; }
                return null;
            }
            return dict;
        }

        private static List<object> ReadList(string s, ref int i)
        {
            var list = new List<object>();
            i++;  // consume '['
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return list; }
            while (i < s.Length)
            {
                SkipWs(s, ref i);
                list.Add(ReadValue(s, ref i));
                SkipWs(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return list; }
                return null;
            }
            return list;
        }

        private static object ReadValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            char c = s[i];
            if (c == '"') return ReadString(s, ref i);
            if (c == '{') return ReadDict(s, ref i);
            if (c == '[') return ReadList(s, ref i);
            if (c == 't' && s.Substring(i, 4) == "true") { i += 4; return true; }
            if (c == 'f' && s.Substring(i, 5) == "false") { i += 5; return false; }
            if (c == 'n' && s.Substring(i, 4) == "null") { i += 4; return null; }
            return ReadNumber(s, ref i);
        }

        private static string ReadString(string s, ref int i)
        {
            if (s[i] != '"') return null;
            i++;
            var sb = new StringBuilder();
            while (i < s.Length && s[i] != '"')
            {
                if (s[i] == '\\')
                {
                    i++;
                    char esc = s[i++];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber));
                            i += 4;
                            break;
                        default: sb.Append(esc); break;
                    }
                }
                else
                {
                    sb.Append(s[i++]);
                }
            }
            i++;  // consume closing '"'
            return sb.ToString();
        }

        private static object ReadNumber(string s, ref int i)
        {
            int start = i;
            if (s[i] == '-') i++;
            bool isFloat = false;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '.' || c == 'e' || c == 'E') isFloat = true;
                if ((c >= '0' && c <= '9') || c == '.' || c == 'e' || c == 'E' || c == '+' || c == '-')
                    i++;
                else break;
            }
            string token = s.Substring(start, i - start);
            if (isFloat)
                return double.Parse(token, CultureInfo.InvariantCulture);
            return long.Parse(token, CultureInfo.InvariantCulture);
        }

        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r')) i++;
        }
    }
}
