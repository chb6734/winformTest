#if NET8_PLUS
using System;
using System.IO;
using System.Threading;
using E2ETest.Core.Automation;
using E2ETest.Core.Logging;
using E2ETest.Core.ProcessMgr;
using E2ETest.Core.Recording;
using E2ETest.Core.Selector;

namespace E2ETest.Core.Api
{
    /// <summary>IApp의 기본 구현. Runner가 생성하여 테스트 스크립트에 주입.</summary>
    public sealed class AppImpl : IApp
    {
        private readonly AppLauncher _launcher;
        private readonly IAutomationBackend _backend;
        private readonly IElementHandle _mainWindow;
        private readonly IRecorder _recorder;
        private readonly ILogger _log;
        public int DefaultTimeoutMs = 10000;

        public AppImpl(AppLauncher launcher, IAutomationBackend backend, IRecorder recorder, ILogger log, int mainWindowTimeoutMs = 15000)
        {
            _launcher = launcher;
            _backend = backend;
            _recorder = recorder;
            _log = log;
            _mainWindow = backend.GetMainWindow(launcher.ProcessId, mainWindowTimeoutMs);
            _log.Log(LogLevel.Info, "main window attached: " + _mainWindow.Name);
        }

        public IWindow MainWindow { get { return new WindowImpl(_backend, _mainWindow, _recorder, _log, DefaultTimeoutMs); } }

        public IWindow Window(string titleOrSelector)
        {
            var sel = titleOrSelector.Contains("=") || titleOrSelector.Contains("[") || titleOrSelector.StartsWith("#")
                ? SelectorParser.Parse(titleOrSelector)
                : SelectorParser.Parse("Window[title=" + titleOrSelector + "]");
            var handle = _backend.FindFromRoot(sel, DefaultTimeoutMs);
            return new WindowImpl(_backend, handle, _recorder, _log, DefaultTimeoutMs);
        }

        public IElement Find(string selector)
        {
            return MainWindow.Find(selector);
        }

        public void Screenshot(string name)
        {
            _recorder.CaptureFrame(name);
        }

        public void Wait(int milliseconds) { Thread.Sleep(milliseconds); }

        public void Close()
        {
            try { _launcher.Kill(); } catch { }
        }

        public void Dispose()
        {
            Close();
            if (_recorder != null) _recorder.Dispose();
            if (_backend != null) _backend.Dispose();
            if (_launcher != null) _launcher.Dispose();
        }
    }

    internal sealed class WindowImpl : IWindow
    {
        private readonly IAutomationBackend _backend;
        private readonly IElementHandle _handle;
        private readonly IRecorder _recorder;
        private readonly ILogger _log;
        private readonly int _timeoutMs;

        public WindowImpl(IAutomationBackend b, IElementHandle h, IRecorder r, ILogger l, int timeoutMs)
        { _backend = b; _handle = h; _recorder = r; _log = l; _timeoutMs = timeoutMs; }

        public string Title { get { return _handle.Name; } }

        public IElement Find(string selector)
        {
            var sel = SelectorParser.Parse(selector);
            var el = _backend.Find(_handle, sel, _timeoutMs);
            return new ElementImpl(_backend, el, _log, _timeoutMs);
        }

        public IWindow SubWindow(string titleOrSelector)
        {
            var sel = titleOrSelector.Contains("=") || titleOrSelector.Contains("[") || titleOrSelector.StartsWith("#")
                ? SelectorParser.Parse(titleOrSelector)
                : SelectorParser.Parse("Window[title=" + titleOrSelector + "]");
            var el = _backend.Find(_handle, sel, _timeoutMs);
            return new WindowImpl(_backend, el, _recorder, _log, _timeoutMs);
        }

        public void Screenshot(string name) { _recorder.CaptureFrame(name); }
    }

    internal sealed class ElementImpl : IElement
    {
        private readonly IAutomationBackend _backend;
        private readonly IElementHandle _handle;
        private readonly ILogger _log;
        private readonly int _timeoutMs;

        public ElementImpl(IAutomationBackend b, IElementHandle h, ILogger l, int timeoutMs)
        { _backend = b; _handle = h; _log = l; _timeoutMs = timeoutMs; }

        public string Text { get { return _backend.GetText(_handle); } }
        public bool IsVisible { get { return !_handle.IsOffscreen; } }
        public bool IsEnabled { get { return _handle.IsEnabled; } }

        public IElement Click() { _backend.Click(_handle); return this; }
        public IElement Type(string text) { _backend.TypeText(_handle, text); return this; }
        public IElement Fill(string text) { _backend.SetText(_handle, text); return this; }
        public IElement Focus() { _backend.Focus(_handle); return this; }
        public IElement ExpectVisible()
        {
            if (_handle.IsOffscreen) throw new AssertionFailedException("element not visible: " + _handle.Name);
            return this;
        }
        public IElement ExpectText(string expected)
        {
            var actual = _backend.GetText(_handle);
            if (actual == null || !actual.Contains(expected))
                throw new AssertionFailedException("text mismatch. expected contains: \"" + expected + "\", actual: \"" + actual + "\"");
            return this;
        }
        public IElement ExpectEnabled()
        {
            if (!_handle.IsEnabled) throw new AssertionFailedException("element not enabled: " + _handle.Name);
            return this;
        }
    }

    public sealed class AssertionFailedException : Exception
    {
        public AssertionFailedException(string message) : base(message) { }
    }
}
#endif
