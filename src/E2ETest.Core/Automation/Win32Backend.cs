#if NET8_PLUS
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using E2ETest.Core.Geometry;
using E2ETest.Core.Selector;

namespace E2ETest.Core.Automation
{
    /// <summary>UIA로 안되는 구형/커스텀 앱을 위한 Win32 폴백. FindWindow + EnumChildWindows 기반.</summary>
    public sealed class Win32Backend : IAutomationBackend
    {
        [DllImport("user32.dll")]
        private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr child, string className, string windowName);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder sb, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder sb, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowEnabled(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr GetParent(IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr parentHandle, EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        public IElementHandle GetMainWindow(int processId, int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                IntPtr found = IntPtr.Zero;
                EnumWindows((h, _) =>
                {
                    uint pid; GetWindowThreadProcessId(h, out pid);
                    if (pid == processId && IsWindowVisible(h) && GetParent(h) == IntPtr.Zero)
                    {
                        found = h;
                        return false;
                    }
                    return true;
                }, IntPtr.Zero);
                if (found != IntPtr.Zero) return new Win32Element(found);
                Thread.Sleep(150);
            }
            throw new TimeoutException("win32: main window not found for pid " + processId);
        }

        public IElementHandle Find(IElementHandle root, Selector.Selector selector, int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                var el = FindOnce(((Win32Element)root).Handle, selector);
                if (el != IntPtr.Zero) return new Win32Element(el);
                Thread.Sleep(150);
            }
            throw new ElementNotFoundException("win32: selector not matched: " + selector.Raw);
        }

        public IElementHandle FindFromRoot(Selector.Selector selector, int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                var el = FindOnce(IntPtr.Zero, selector);
                if (el != IntPtr.Zero) return new Win32Element(el);
                Thread.Sleep(200);
            }
            throw new ElementNotFoundException("win32: selector not matched (root): " + selector.Raw);
        }

        private IntPtr FindOnce(IntPtr parent, Selector.Selector selector)
        {
            IntPtr current = parent;
            foreach (var part in selector.Parts)
            {
                current = FindChild(current, part);
                if (current == IntPtr.Zero) return IntPtr.Zero;
            }
            return current;
        }

        private IntPtr FindChild(IntPtr parent, SelectorPart part)
        {
            string title = null, className = null;
            foreach (var c in part.Conditions)
            {
                if (c.Kind == SelectorKind.Name || c.Kind == SelectorKind.Text || c.Kind == SelectorKind.Title) title = c.Value;
                else if (c.Kind == SelectorKind.ClassName) className = c.Value;
            }
            if (parent == IntPtr.Zero)
            {
                return FindWindowEx(IntPtr.Zero, IntPtr.Zero, className, title);
            }
            // child 탐색: EnumChildWindows로 매칭
            IntPtr result = IntPtr.Zero;
            EnumChildWindows(parent, (h, _) =>
            {
                if (title != null)
                {
                    var sb = new StringBuilder(256);
                    GetWindowText(h, sb, sb.Capacity);
                    if (!sb.ToString().Contains(title)) return true;
                }
                if (className != null)
                {
                    var sb = new StringBuilder(256);
                    GetClassName(h, sb, sb.Capacity);
                    if (sb.ToString() != className) return true;
                }
                result = h;
                return false;
            }, IntPtr.Zero);
            return result;
        }

        public void Click(IElementHandle element)
        {
            var r = element.BoundingRectangle;
            if (r.IsEmpty) throw new InvalidOperationException("win32 click: no bounds");
            InputSim.MouseMove(r.X + r.Width / 2, r.Y + r.Height / 2);
            InputSim.MouseClickLeft();
        }

        public void TypeText(IElementHandle element, string text)
        {
            Focus(element);
            InputSim.TypeText(text);
        }

        public void SetText(IElementHandle element, string text)
        {
            Focus(element);
            InputSim.SelectAllAndType(text);
        }

        public string GetText(IElementHandle element)
        {
            var sb = new StringBuilder(1024);
            GetWindowText(((Win32Element)element).Handle, sb, sb.Capacity);
            return sb.ToString();
        }

        public void Focus(IElementHandle element)
        {
            // SetForegroundWindow 생략 — 대부분의 경우 클릭으로 충분.
        }

        public List<IElementHandle> Children(IElementHandle element)
        {
            var list = new List<IElementHandle>();
            EnumChildWindows(((Win32Element)element).Handle, (h, _) => { list.Add(new Win32Element(h)); return true; }, IntPtr.Zero);
            return list;
        }

        public string DumpTree(IElementHandle root, int maxDepth)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Win32 tree dump (class | text):");
            foreach (var c in Children(root))
            {
                var cls = new StringBuilder(256); GetClassName(((Win32Element)c).Handle, cls, cls.Capacity);
                sb.AppendLine("  " + cls + " | " + c.Name);
            }
            return sb.ToString();
        }

        public void Dispose() { }
    }

    internal sealed class Win32Element : IElementHandle
    {
        public IntPtr Handle { get; private set; }
        public Win32Element(IntPtr handle) { Handle = handle; }

        public string Name
        {
            get
            {
                var sb = new StringBuilder(1024);
                Win32Backend_GetWindowText(Handle, sb, sb.Capacity);
                return sb.ToString();
            }
        }
        public string AutomationId { get { return ""; } }
        public string ClassName
        {
            get
            {
                var sb = new StringBuilder(256);
                Win32Backend_GetClassName(Handle, sb, sb.Capacity);
                return sb.ToString();
            }
        }
        public string ControlType { get { return ClassName; } }
        public bool IsOffscreen { get { return !Win32Backend_IsWindowVisible(Handle); } }
        public bool IsEnabled { get { return Win32Backend_IsWindowEnabled(Handle); } }
        public Rect BoundingRectangle
        {
            get
            {
                Win32BackendRECT r;
                if (!Win32Backend_GetWindowRect(Handle, out r)) return Rect.Empty;
                return new Rect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
            }
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowText", CharSet = CharSet.Auto)]
        private static extern int Win32Backend_GetWindowText(IntPtr hWnd, StringBuilder sb, int nMaxCount);
        [DllImport("user32.dll", EntryPoint = "GetClassName", CharSet = CharSet.Auto)]
        private static extern int Win32Backend_GetClassName(IntPtr hWnd, StringBuilder sb, int nMaxCount);
        [DllImport("user32.dll", EntryPoint = "IsWindowVisible")]
        private static extern bool Win32Backend_IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll", EntryPoint = "IsWindowEnabled")]
        private static extern bool Win32Backend_IsWindowEnabled(IntPtr hWnd);
        [DllImport("user32.dll", EntryPoint = "GetWindowRect")]
        private static extern bool Win32Backend_GetWindowRect(IntPtr hWnd, out Win32BackendRECT lpRect);
        [StructLayout(LayoutKind.Sequential)]
        private struct Win32BackendRECT { public int Left, Top, Right, Bottom; }
    }
}
#endif
