using System;

namespace E2ETest.Core.Api
{
    /// <summary>
    /// 테스트 어셈블리에서 실행 대상 메서드를 마킹. AssemblyTestLoader가 이 속성을 리플렉션으로 탐지.
    /// .NET 3.5부터 사용 가능.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class E2ETestAttribute : Attribute
    {
        public string Name { get; set; }
        public string AppPath { get; set; }
        public string AppArgs { get; set; }

        public E2ETestAttribute() { }
        public E2ETestAttribute(string appPath) { AppPath = appPath; }
    }
}
