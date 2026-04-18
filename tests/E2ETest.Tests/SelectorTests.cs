using E2ETest.Core.Selector;
using Xunit;

namespace E2ETest.Tests;

public class SelectorTests
{
    [Fact]
    public void Parses_AutomationId_shortcut()
    {
        var sel = SelectorParser.Parse("#btnLogin");
        Assert.Single(sel.Parts);
        var c = sel.Parts[0].Conditions[0];
        Assert.Equal(SelectorKind.AutomationId, c.Kind);
        Assert.Equal("btnLogin", c.Value);
    }

    [Fact]
    public void Parses_key_value_name()
    {
        var sel = SelectorParser.Parse("name=사용자명");
        var c = sel.Parts[0].Conditions[0];
        Assert.Equal(SelectorKind.Name, c.Kind);
        Assert.Equal("사용자명", c.Value);
    }

    [Fact]
    public void Parses_nested_windows()
    {
        var sel = SelectorParser.Parse("Window[title=로그인] Button[text=확인]");
        Assert.Equal(2, sel.Parts.Count);
        var w = sel.Parts[0].Conditions;
        Assert.Contains(w, x => x.Kind == SelectorKind.ControlType && x.Value == "Window");
        Assert.Contains(w, x => x.Kind == SelectorKind.Title && x.Value == "로그인");

        var b = sel.Parts[1].Conditions;
        Assert.Contains(b, x => x.Kind == SelectorKind.ControlType && x.Value == "Button");
        Assert.Contains(b, x => x.Kind == SelectorKind.Text && x.Value == "확인");
    }

    [Fact]
    public void Parses_class()
    {
        var sel = SelectorParser.Parse("class=Button");
        Assert.Equal(SelectorKind.ClassName, sel.Parts[0].Conditions[0].Kind);
        Assert.Equal("Button", sel.Parts[0].Conditions[0].Value);
    }
}

public class YamlLoaderTests
{
    [Fact]
    public void Loads_minimal_yaml()
    {
        string yaml = @"
name: simple
app:
  path: foo.exe
steps:
  - action: click
    target: '#btn'
";
        var tc = E2ETest.Scripting.YamlLoader.LoadFromString(yaml, "inline");
        Assert.Equal("simple", tc.Name);
        Assert.Equal("foo.exe", tc.App.Path);
        Assert.Single(tc.Steps);
        Assert.Equal("click", tc.Steps[0].Action);
        Assert.Equal("#btn", tc.Steps[0].Target);
    }
}
