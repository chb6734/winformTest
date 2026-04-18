using System;

namespace E2ETest.Core.Automation
{
    public class ElementNotFoundException : Exception
    {
        public ElementNotFoundException(string message) : base(message) { }
        public ElementNotFoundException(string message, Exception inner) : base(message, inner) { }
    }
}
