using System;
using E2ETest.Core.Model;

namespace E2ETest.Core.Api
{
    /// <summary>Fluent API — 테스트 코드에서 사용되는 최상위 진입점. .NET 3.5 호환 가능하도록 동기 API.</summary>
    public interface IApp : IDisposable
    {
        IWindow Window(string titleOrSelector);
        IWindow MainWindow { get; }
        IElement Find(string selector);
        void Screenshot(string name);
        void Wait(int milliseconds);
        void Close();
    }

    public interface IWindow
    {
        string Title { get; }
        IElement Find(string selector);
        IWindow SubWindow(string titleOrSelector);
        void Screenshot(string name);
    }

    public interface IElement
    {
        string Text { get; }
        bool IsVisible { get; }
        bool IsEnabled { get; }

        IElement Click();
        IElement Type(string text);
        IElement Fill(string text);
        IElement Focus();
        IElement ExpectVisible();
        IElement ExpectText(string expected);
        IElement ExpectEnabled();
    }

    /// <summary>전역 launcher entrypoint.</summary>
    public static class E2E
    {
        /// <summary>Runner 내부에서만 호출됨. 테스트 스크립트는 주입받은 IApp를 사용.</summary>
        public static IApp Launch(string exePath, string args = null) { throw new InvalidOperationException("E2E.Launch must be provided by runner; do not call directly from test code."); }
    }
}
