using System;
using System.Collections.Generic;
using System.Text;

namespace E2ETest.Core.Selector
{
    /// <summary>
    /// 지원 문법:
    ///   #autoId                         → AutomationId
    ///   text=로그인                     → 부분 일치
    ///   name=사용자명                   → 정확 일치
    ///   class=Button                    → 클래스명
    ///   Button                          → 컨트롤 타입 prefix
    ///   Window[title=로그인] Button[text=확인]  → 중첩
    /// 공백은 파트 구분자. 대괄호 안의 속성은 쉼표 없이 하나만 지원 (간단화).
    /// </summary>
    public static class SelectorParser
    {
        public static Selector Parse(string input)
        {
            if (input == null) throw new ArgumentNullException("input");
            string trimmed = input.Trim();
            if (trimmed.Length == 0) throw new ArgumentException("empty selector");

            var parts = new List<SelectorPart>();
            foreach (var token in Tokenize(trimmed))
            {
                parts.Add(ParsePart(token));
            }
            return new Selector(trimmed, parts);
        }

        private static IEnumerable<string> Tokenize(string s)
        {
            // 공백 분리하되 [...] 안의 공백은 유지.
            var sb = new StringBuilder();
            int bracket = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '[') { bracket++; sb.Append(c); continue; }
                if (c == ']') { bracket--; sb.Append(c); continue; }
                if (c == ' ' && bracket == 0)
                {
                    if (sb.Length > 0) { yield return sb.ToString(); sb.Length = 0; }
                    continue;
                }
                sb.Append(c);
            }
            if (sb.Length > 0) yield return sb.ToString();
        }

        private static SelectorPart ParsePart(string token)
        {
            var part = new SelectorPart();

            // prefix: #foo
            if (token.StartsWith("#"))
            {
                part.Conditions.Add(new SelectorCondition(SelectorKind.AutomationId, token.Substring(1).TrimEnd(']', '[')));
                return part;
            }

            // key=value without brackets: name=foo
            int eq = token.IndexOf('=');
            int lb = token.IndexOf('[');
            if (eq > 0 && (lb < 0 || eq < lb))
            {
                part.Conditions.Add(ParseKeyValue(token));
                return part;
            }

            // typeName[attr=val] or just typeName
            string typeName;
            string attrs = null;
            if (lb > 0)
            {
                typeName = token.Substring(0, lb);
                int rb = token.LastIndexOf(']');
                if (rb <= lb) throw new FormatException("unbalanced [ ] in: " + token);
                attrs = token.Substring(lb + 1, rb - lb - 1);
            }
            else
            {
                typeName = token;
            }

            if (!string.IsNullOrEmpty(typeName))
            {
                part.Conditions.Add(new SelectorCondition(SelectorKind.ControlType, typeName));
            }
            if (!string.IsNullOrEmpty(attrs))
            {
                // 단일 attr만 지원 (k=v).
                part.Conditions.Add(ParseKeyValue(attrs));
            }
            return part;
        }

        private static SelectorCondition ParseKeyValue(string kv)
        {
            int eq = kv.IndexOf('=');
            if (eq < 0) throw new FormatException("expected key=value: " + kv);
            string key = kv.Substring(0, eq).Trim().ToLowerInvariant();
            string val = kv.Substring(eq + 1).Trim();
            // Remove surrounding quotes if present
            if (val.Length >= 2 && ((val[0] == '"' && val[val.Length - 1] == '"') || (val[0] == '\'' && val[val.Length - 1] == '\'')))
            {
                val = val.Substring(1, val.Length - 2);
            }

            switch (key)
            {
                case "id":
                case "automationid":
                    return new SelectorCondition(SelectorKind.AutomationId, val);
                case "name":
                    return new SelectorCondition(SelectorKind.Name, val);
                case "text":
                    return new SelectorCondition(SelectorKind.Text, val);
                case "class":
                case "classname":
                    return new SelectorCondition(SelectorKind.ClassName, val);
                case "title":
                    return new SelectorCondition(SelectorKind.Title, val);
                case "type":
                case "controltype":
                    return new SelectorCondition(SelectorKind.ControlType, val);
                default:
                    throw new FormatException("unknown selector key: " + key);
            }
        }
    }
}
