using System;
using System.Collections.Generic;
using System.Text;

namespace E2ETest.Core.Selector
{
    /// <summary>
    /// Playwright 스타일 하이브리드 셀렉터. 여러 파트의 체인으로 중첩 매칭을 표현.
    /// </summary>
    public sealed class Selector
    {
        public List<SelectorPart> Parts { get; private set; }
        public string Raw { get; private set; }

        public Selector(string raw, List<SelectorPart> parts)
        {
            Raw = raw;
            Parts = parts;
        }

        public override string ToString() { return Raw; }
    }

    public sealed class SelectorPart
    {
        /// <summary>AutomationId, Name, Text, ClassName, ControlType 등.</summary>
        public List<SelectorCondition> Conditions { get; private set; }

        public SelectorPart() { Conditions = new List<SelectorCondition>(); }
    }

    public enum SelectorKind
    {
        AutomationId,  // #id
        Name,          // name=foo
        Text,          // text=foo  (Name 또는 HelpText, 부분 일치)
        ClassName,     // class=Button
        ControlType,   // Button / Window / Edit 등을 part prefix로
        Title          // Window[title=로그인]  title 속성
    }

    public sealed class SelectorCondition
    {
        public SelectorKind Kind { get; private set; }
        public string Value { get; private set; }

        public SelectorCondition(SelectorKind kind, string value)
        {
            Kind = kind;
            Value = value;
        }

        public override string ToString()
        {
            return Kind.ToString() + "=" + Value;
        }
    }
}
