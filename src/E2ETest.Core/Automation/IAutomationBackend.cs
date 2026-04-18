using System;
using System.Collections.Generic;
using E2ETest.Core.Geometry;
using E2ETest.Core.Selector;

namespace E2ETest.Core.Automation
{
    /// <summary>요소 핸들. 백엔드에 따라 UIA AutomationElement 또는 Win32 HWND를 래핑.</summary>
    public interface IElementHandle
    {
        string Name { get; }
        string AutomationId { get; }
        string ClassName { get; }
        string ControlType { get; }
        bool IsOffscreen { get; }
        bool IsEnabled { get; }
        Rect BoundingRectangle { get; }
    }

    public interface IAutomationBackend : IDisposable
    {
        /// <summary>프로세스에 속한 루트(메인 윈도우).</summary>
        IElementHandle GetMainWindow(int processId, int timeoutMs);

        /// <summary>셀렉터의 모든 파트를 순차적으로 적용하여 첫 매칭 요소 반환.</summary>
        IElementHandle Find(IElementHandle root, Selector.Selector selector, int timeoutMs);

        /// <summary>전체 데스크탑에서 검색 (주로 Window 매칭용).</summary>
        IElementHandle FindFromRoot(Selector.Selector selector, int timeoutMs);

        void Click(IElementHandle element);
        void TypeText(IElementHandle element, string text);
        void SetText(IElementHandle element, string text);
        string GetText(IElementHandle element);

        void Focus(IElementHandle element);
        List<IElementHandle> Children(IElementHandle element);

        /// <summary>진단용: 요소 트리를 텍스트로 덤프.</summary>
        string DumpTree(IElementHandle root, int maxDepth);
    }
}
