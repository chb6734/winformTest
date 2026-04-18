#if NET8_PLUS
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Automation;
using E2ETest.Core.Geometry;
using E2ETest.Core.Selector;

namespace E2ETest.Core.Automation
{
    /// <summary>System.Windows.Automation 기반 out-of-process 백엔드.</summary>
    public sealed class UiaBackend : IAutomationBackend
    {
        public IElementHandle GetMainWindow(int processId, int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                var cond = new PropertyCondition(AutomationElement.ProcessIdProperty, processId);
                var el = AutomationElement.RootElement.FindFirst(TreeScope.Children, cond);
                if (el != null) return new UiaElement(el);
                Thread.Sleep(150);
            }
            throw new TimeoutException("main window for pid " + processId + " not found within " + timeoutMs + "ms");
        }

        public IElementHandle Find(IElementHandle root, Selector.Selector selector, int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            Exception last = null;
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                try
                {
                    var el = FindOnce(((UiaElement)root).Element, selector);
                    if (el != null) return new UiaElement(el);
                }
                catch (Exception ex) { last = ex; }
                Thread.Sleep(150);
            }
            throw new ElementNotFoundException("selector not matched within " + timeoutMs + "ms: " + selector.Raw, last);
        }

        public IElementHandle FindFromRoot(Selector.Selector selector, int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                var el = FindOnce(AutomationElement.RootElement, selector);
                if (el != null) return new UiaElement(el);
                Thread.Sleep(200);
            }
            throw new ElementNotFoundException("selector not matched (root scope): " + selector.Raw);
        }

        private AutomationElement FindOnce(AutomationElement scope, Selector.Selector selector)
        {
            AutomationElement current = scope;
            foreach (var part in selector.Parts)
            {
                var cond = BuildCondition(part);
                var next = current.FindFirst(TreeScope.Descendants, cond);
                if (next == null) return null;
                current = next;
            }
            return current;
        }

        private Condition BuildCondition(SelectorPart part)
        {
            var conds = new List<Condition>();
            foreach (var c in part.Conditions)
            {
                switch (c.Kind)
                {
                    case SelectorKind.AutomationId:
                        conds.Add(new PropertyCondition(AutomationElement.AutomationIdProperty, c.Value));
                        break;
                    case SelectorKind.Name:
                        conds.Add(new PropertyCondition(AutomationElement.NameProperty, c.Value));
                        break;
                    case SelectorKind.Text:
                        // Name 부분일치 구현 (UIA는 직접 부분일치 없음 → AndNot 조건 없이 Name EQ 기본, 추후 post-filter)
                        conds.Add(new PropertyCondition(AutomationElement.NameProperty, c.Value));
                        break;
                    case SelectorKind.ClassName:
                        conds.Add(new PropertyCondition(AutomationElement.ClassNameProperty, c.Value));
                        break;
                    case SelectorKind.Title:
                        conds.Add(new PropertyCondition(AutomationElement.NameProperty, c.Value));
                        break;
                    case SelectorKind.ControlType:
                        var ct = MapControlType(c.Value);
                        if (ct != null) conds.Add(new PropertyCondition(AutomationElement.ControlTypeProperty, ct));
                        break;
                }
            }
            if (conds.Count == 0) return Condition.TrueCondition;
            if (conds.Count == 1) return conds[0];
            return new AndCondition(conds.ToArray());
        }

        private ControlType MapControlType(string name)
        {
            switch (name.ToLowerInvariant())
            {
                case "button": return ControlType.Button;
                case "edit":
                case "textbox": return ControlType.Edit;
                case "window": return ControlType.Window;
                case "menu": return ControlType.Menu;
                case "menuitem": return ControlType.MenuItem;
                case "checkbox": return ControlType.CheckBox;
                case "combobox": return ControlType.ComboBox;
                case "list": return ControlType.List;
                case "listitem": return ControlType.ListItem;
                case "tab": return ControlType.Tab;
                case "tabitem": return ControlType.TabItem;
                case "text": return ControlType.Text;
                case "tree": return ControlType.Tree;
                case "treeitem": return ControlType.TreeItem;
                case "hyperlink": return ControlType.Hyperlink;
                case "radiobutton": return ControlType.RadioButton;
                case "group": return ControlType.Group;
                case "pane": return ControlType.Pane;
                case "dialog": return ControlType.Window;
                default: return null;
            }
        }

        public void Click(IElementHandle element)
        {
            var el = ((UiaElement)element).Element;
            object pattern;
            if (el.TryGetCurrentPattern(InvokePattern.Pattern, out pattern))
            {
                ((InvokePattern)pattern).Invoke();
                return;
            }
            if (el.TryGetCurrentPattern(TogglePattern.Pattern, out pattern))
            {
                ((TogglePattern)pattern).Toggle();
                return;
            }
            // fallback: mouse click via SendInput at center
            var wr = el.Current.BoundingRectangle;
            if (!wr.IsEmpty)
            {
                InputSim.MouseMove((int)(wr.X + wr.Width / 2), (int)(wr.Y + wr.Height / 2));
                InputSim.MouseClickLeft();
                return;
            }
            throw new InvalidOperationException("element cannot be clicked (no Invoke/Toggle pattern and no bounds)");
        }

        public void TypeText(IElementHandle element, string text)
        {
            Focus(element);
            InputSim.TypeText(text);
        }

        public void SetText(IElementHandle element, string text)
        {
            var el = ((UiaElement)element).Element;
            object pattern;
            if (el.TryGetCurrentPattern(ValuePattern.Pattern, out pattern))
            {
                ((ValuePattern)pattern).SetValue(text);
                return;
            }
            Focus(element);
            InputSim.SelectAllAndType(text);
        }

        public string GetText(IElementHandle element)
        {
            var el = ((UiaElement)element).Element;
            object pattern;
            if (el.TryGetCurrentPattern(ValuePattern.Pattern, out pattern))
            {
                return ((ValuePattern)pattern).Current.Value;
            }
            if (el.TryGetCurrentPattern(TextPattern.Pattern, out pattern))
            {
                return ((TextPattern)pattern).DocumentRange.GetText(4096);
            }
            return el.Current.Name;
        }

        public void Focus(IElementHandle element)
        {
            try { ((UiaElement)element).Element.SetFocus(); }
            catch { /* non-focusable */ }
        }

        public List<IElementHandle> Children(IElementHandle element)
        {
            var el = ((UiaElement)element).Element;
            var list = new List<IElementHandle>();
            var walker = TreeWalker.ControlViewWalker;
            var c = walker.GetFirstChild(el);
            while (c != null)
            {
                list.Add(new UiaElement(c));
                c = walker.GetNextSibling(c);
            }
            return list;
        }

        public string DumpTree(IElementHandle root, int maxDepth)
        {
            var sb = new System.Text.StringBuilder();
            DumpRecursive(((UiaElement)root).Element, 0, maxDepth, sb);
            return sb.ToString();
        }

        private void DumpRecursive(AutomationElement el, int depth, int max, System.Text.StringBuilder sb)
        {
            if (el == null || depth > max) return;
            sb.Append(new string(' ', depth * 2));
            try
            {
                var info = el.Current;
                sb.AppendLine(info.ControlType.ProgrammaticName + " Name=\"" + info.Name + "\" AutomationId=\"" + info.AutomationId + "\" Class=\"" + info.ClassName + "\"");
            }
            catch { sb.AppendLine("<inaccessible>"); return; }

            var walker = TreeWalker.ControlViewWalker;
            var c = walker.GetFirstChild(el);
            while (c != null)
            {
                DumpRecursive(c, depth + 1, max, sb);
                c = walker.GetNextSibling(c);
            }
        }

        public void Dispose() { }
    }

    internal sealed class UiaElement : IElementHandle
    {
        public AutomationElement Element { get; private set; }
        public UiaElement(AutomationElement el) { Element = el; }

        public string Name { get { return Safe(() => Element.Current.Name); } }
        public string AutomationId { get { return Safe(() => Element.Current.AutomationId); } }
        public string ClassName { get { return Safe(() => Element.Current.ClassName); } }
        public string ControlType { get { return Safe(() => Element.Current.ControlType.ProgrammaticName); } }
        public bool IsOffscreen { get { try { return Element.Current.IsOffscreen; } catch { return true; } } }
        public bool IsEnabled { get { try { return Element.Current.IsEnabled; } catch { return false; } } }
        public Rect BoundingRectangle
        {
            get
            {
                try
                {
                    var r = Element.Current.BoundingRectangle;
                    if (r.IsEmpty) return Rect.Empty;
                    return new Rect((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height);
                }
                catch { return Rect.Empty; }
            }
        }

        private static string Safe(Func<string> f)
        {
            try { return f() ?? ""; } catch { return ""; }
        }
    }
}
#endif
