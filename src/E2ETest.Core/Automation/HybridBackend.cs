#if NET8_PLUS
using System;
using System.Collections.Generic;
using E2ETest.Core.Logging;
using E2ETest.Core.Selector;

namespace E2ETest.Core.Automation
{
    /// <summary>UIA 우선, 실패 시 Win32로 폴백하는 복합 백엔드.</summary>
    public sealed class HybridBackend : IAutomationBackend
    {
        private readonly UiaBackend _uia = new UiaBackend();
        private readonly Win32Backend _win32 = new Win32Backend();
        private readonly ILogger _log;

        public HybridBackend(ILogger log = null) { _log = log ?? NullLogger.Instance; }

        private IElementHandle _lastBackendRoot;
        private bool _lastUsedWin32;

        public IElementHandle GetMainWindow(int processId, int timeoutMs)
        {
            try
            {
                var el = _uia.GetMainWindow(processId, timeoutMs);
                _lastUsedWin32 = false;
                return el;
            }
            catch (Exception ex)
            {
                _log.Log(LogLevel.Warn, "UIA GetMainWindow failed, falling back to Win32: " + ex.Message);
                var el = _win32.GetMainWindow(processId, timeoutMs);
                _lastUsedWin32 = true;
                return el;
            }
        }

        public IElementHandle Find(IElementHandle root, Selector.Selector selector, int timeoutMs)
        {
            if (root is UiaElement)
            {
                try { return _uia.Find(root, selector, timeoutMs); }
                catch (Exception ex)
                {
                    _log.Log(LogLevel.Warn, "UIA Find failed, attempting Win32 fallback: " + ex.Message);
                    // UIA 요소를 Win32 HWND로 변환 불가하므로 root scope로 폴백
                    return _win32.FindFromRoot(selector, timeoutMs);
                }
            }
            return _win32.Find(root, selector, timeoutMs);
        }

        public IElementHandle FindFromRoot(Selector.Selector selector, int timeoutMs)
        {
            try { return _uia.FindFromRoot(selector, timeoutMs); }
            catch (Exception ex)
            {
                _log.Log(LogLevel.Warn, "UIA FindFromRoot failed, falling back to Win32: " + ex.Message);
                return _win32.FindFromRoot(selector, timeoutMs);
            }
        }

        public void Click(IElementHandle element)
        {
            if (element is UiaElement) { _uia.Click(element); return; }
            _win32.Click(element);
        }

        public void TypeText(IElementHandle element, string text)
        {
            if (element is UiaElement) { _uia.TypeText(element, text); return; }
            _win32.TypeText(element, text);
        }

        public void SetText(IElementHandle element, string text)
        {
            if (element is UiaElement) { _uia.SetText(element, text); return; }
            _win32.SetText(element, text);
        }

        public string GetText(IElementHandle element)
        {
            if (element is UiaElement) return _uia.GetText(element);
            return _win32.GetText(element);
        }

        public void Focus(IElementHandle element)
        {
            if (element is UiaElement) { _uia.Focus(element); return; }
            _win32.Focus(element);
        }

        public List<IElementHandle> Children(IElementHandle element)
        {
            if (element is UiaElement) return _uia.Children(element);
            return _win32.Children(element);
        }

        public string DumpTree(IElementHandle root, int maxDepth)
        {
            if (root is UiaElement) return _uia.DumpTree(root, maxDepth);
            return _win32.DumpTree(root, maxDepth);
        }

        public void Dispose()
        {
            _uia.Dispose();
            _win32.Dispose();
        }
    }
}
#endif
