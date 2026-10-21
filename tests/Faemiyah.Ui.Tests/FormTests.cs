using System;
using System.Collections.Generic;
using AwesomeAssertions;
using Bunit;
using Faemiyah.Ui.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Faemiyah.Ui.Tests;

[TestFixture]
public class FormToggleAndCheckboxTests : ComponentTestBase
{
    [Test]
    public void Checkbox_ReportsInitialValueAndChanges()
    {
        var values = new List<bool>();
        var cut = Context.Render<FormCheckbox>(ps => ps
            .Add(p => p.Checked, true)
            .Add(p => p.OnChanged, (bool v) => values.Add(v)));

        cut.Find("input").Change(false);

        values.Should().Equal(true, false);
        cut.Instance.CheckedInternal.Should().BeFalse();
    }

    [Test]
    public void Toggle_ReportsInitialValueAndChanges()
    {
        var values = new List<bool>();
        var cut = Context.Render<FormToggle>(ps => ps
            .Add(p => p.OnChanged, (bool v) => values.Add(v))
            .AddChildContent("Label"));

        cut.Find("input").Change(true);

        values.Should().Equal(false, true);
        cut.Find(".fui-toggle-radio-label").TextContent.Should().Be("Label");
    }

    [Test]
    public void Toggle_ReminderDisappearsAfterChange()
    {
        var cut = Context.Render<FormToggle>(ps => ps.Add(p => p.ComparisonTimeStamp, DateTime.UtcNow.AddMinutes(-1)));

        cut.Find(".fui-toggle-radio-label").ClassList.Should().Contain("fui-reminder");

        cut.Find("input").Change(true);

        cut.Find(".fui-toggle-radio-label").ClassList.Should().NotContain("fui-reminder");
    }

    [Test]
    public void Toggle_Disabled_HasDisabledClassAndAttribute()
    {
        var cut = Context.Render<FormToggle>(ps => ps.Add(p => p.Disabled, true));

        cut.Find(".fui-toggle-radio-label").ClassList.Should().Contain("fui-disabled");
        cut.Find("input").HasAttribute("disabled").Should().BeTrue();
    }
}

[TestFixture]
public class FormRadioTests : ComponentTestBase
{
    private static readonly Dictionary<string, int> Options = new() { ["One"] = 1, ["Two"] = 2 };

    [Test]
    public void ChecksSelectedOptionAndReportsChanges()
    {
        var values = new List<int>();
        var cut = Context.Render<FormRadio<int>>(ps => ps
            .Add(p => p.Options, Options)
            .Add(p => p.SelectedOption, 2)
            .Add(p => p.OnChanged, (int v) => values.Add(v)));

        var inputs = cut.FindAll("input");
        inputs[0].HasAttribute("checked").Should().BeFalse();
        inputs[1].HasAttribute("checked").Should().BeTrue();
        inputs[0].GetAttribute("name").Should().Be(inputs[1].GetAttribute("name"));
        cut.FindAll(".fui-error").Should().BeEmpty();

        inputs[0].Change("One");

        values.Should().Equal(1);
    }

    [Test]
    public void InvalidSelection_ShowsError()
    {
        var cut = Context.Render<FormRadio<int>>(ps => ps
            .Add(p => p.Options, Options)
            .Add(p => p.SelectedOption, 3));

        cut.FindAll(".fui-toggle-radio-label.fui-error").Should().HaveCount(2);
    }

    [Test]
    public void NullSelection_WithReferenceTypes_DoesNotThrow()
    {
        var cut = Context.Render<FormRadio<string>>(ps => ps
            .Add(p => p.Options, new Dictionary<string, string> { ["A"] = "a" })
            .Add(p => p.SelectedOption, null));

        cut.FindAll(".fui-error").Should().HaveCount(1);
    }

    [Test]
    public void SeparateGroups_HaveDifferentNames()
    {
        var first = Context.Render<FormRadio<int>>(ps => ps.Add(p => p.Options, Options));
        var second = Context.Render<FormRadio<int>>(ps => ps.Add(p => p.Options, Options));

        first.Find("input").GetAttribute("name").Should().NotBe(second.Find("input").GetAttribute("name"));
    }
}

[TestFixture]
public class FormSelectTests : ComponentTestBase
{
    private static readonly Dictionary<string, int> Options = new() { ["One"] = 1, ["Two"] = 2 };

    [Test]
    public void Initialization_SelectsMatchingOptionAndReportsIt()
    {
        var values = new List<int>();
        var cut = Context.Render<FormSelect<int>>(ps => ps
            .Add(p => p.Options, Options)
            .Add(p => p.SelectedOption, 2)
            .Add(p => p.OnChanged, (int v) => values.Add(v)));

        cut.Instance.SelectedOptionInternal.Should().Be("Two");
        cut.Find("option[selected]").TextContent.Should().Be("Two");
        values.Should().Equal(2);
    }

    [Test]
    public void Initialization_WithUnknownValue_FallsBackToFirstOption()
    {
        var values = new List<int>();
        var cut = Context.Render<FormSelect<int>>(ps => ps
            .Add(p => p.Options, Options)
            .Add(p => p.SelectedOption, 9)
            .Add(p => p.OnChanged, (int v) => values.Add(v)));

        cut.Instance.SelectedOptionInternal.Should().Be("One");
        values.Should().Equal(1);
    }

    [Test]
    public void Initialization_WithNoOptions_DoesNotThrow()
    {
        var cut = Context.Render<FormSelect<string>>(ps => ps.Add(p => p.Options, new Dictionary<string, string>()));

        cut.Instance.SelectedOptionInternal.Should().BeNull();
    }

    [Test]
    public void Change_ReportsSelectedValue()
    {
        var values = new List<int>();
        var cut = Context.Render<FormSelect<int>>(ps => ps
            .Add(p => p.Options, Options)
            .Add(p => p.OnChanged, (int v) => values.Add(v)));

        cut.Find("select").Change("Two");

        values.Should().Equal(1, 2);
    }
}

[TestFixture]
public class FormTextTests : ComponentTestBase
{
    [Test]
    public void Text_ReportsInitialValueAndChanges()
    {
        var values = new List<string?>();
        var cut = Context.Render<FormText>(ps => ps
            .Add(p => p.SelectedOption, "start")
            .Add(p => p.OnChanged, (string? v) => values.Add(v)));

        cut.Find("input").Change("next");

        values.Should().Equal("start", "next");
        cut.Find("input").GetAttribute("value").Should().Be("next");
    }

    [Test]
    public void Text_InvalidValue_IsMarkedAndNotReported()
    {
        var values = new List<string?>();
        var cut = Context.Render<FormText>(ps => ps
            .Add(p => p.SelectedOption, "ok")
            .Add(p => p.InvalidOptionGenerator, () => new List<string> { "taken" })
            .Add(p => p.OnChanged, (string? v) => values.Add(v)));

        cut.Find("input").Change("taken");

        values.Should().Equal("ok");
        cut.Find("input").ClassList.Should().Contain("fui-error");

        cut.Find("input").Change("free");

        values.Should().Equal("ok", "free");
        cut.Find("input").ClassList.Should().NotContain("fui-error");
    }

    [Test]
    public void TextArea_UsesRowsAndRejectsInvalidValues()
    {
        var values = new List<string?>();
        var cut = Context.Render<FormTextArea>(ps => ps
            .Add(p => p.Rows, 3)
            .Add(p => p.InvalidOptionGenerator, () => ["bad"])
            .Add(p => p.OnChanged, (string? v) => values.Add(v)));

        cut.Find("textarea").GetAttribute("rows").Should().Be("3");

        cut.Find("textarea").Change("bad");

        values.Should().Equal((string?)null);
        cut.Find("textarea").ClassList.Should().Contain("fui-error");
    }

    [Test]
    public void TextArea_DefaultsToTenRows()
    {
        var cut = Context.Render<FormTextArea>();

        cut.Find("textarea").GetAttribute("rows").Should().Be("10");
    }
}

[TestFixture]
public class FormNumberTests : ComponentTestBase
{
    private IRenderedComponent<FormNumber> RenderNumber(int value, List<int> values)
    {
        return Context.Render<FormNumber>(ps => ps
            .Add(p => p.SelectedOption, value)
            .Add(p => p.Min, 0)
            .Add(p => p.Max, 10)
            .Add(p => p.OnChanged, (int v) => values.Add(v)));
    }

    [Test]
    public void TypingAndBlurring_CommitsClampedValue()
    {
        var values = new List<int>();
        var cut = RenderNumber(5, values);
        var input = cut.Find("input");

        input.Focus();
        input.Input("42");

        values.Should().BeEmpty();

        input.Blur();

        values.Should().Equal(10);
        cut.Find("input").GetAttribute("value").Should().Be("10");
    }

    [TestCase("Enter")]
    [TestCase("Escape")]
    public void CommitKeys_CommitValue(string key)
    {
        var values = new List<int>();
        var cut = RenderNumber(5, values);
        var input = cut.Find("input");

        input.Focus();
        input.Input("-3");
        input.KeyDown(new KeyboardEventArgs { Key = key });

        values.Should().Equal(0);
    }

    [Test]
    public void InputWithoutFocus_CommitsImmediately()
    {
        var values = new List<int>();
        var cut = RenderNumber(5, values);

        cut.Find("input").Change("6");

        values.Should().Equal(6);
    }

    [Test]
    public void UnparsableInput_IsIgnored()
    {
        var values = new List<int>();
        var cut = RenderNumber(5, values);

        cut.Find("input").Change("abc");

        values.Should().BeEmpty();
    }

    [Test]
    public void ParameterChange_UpdatesDisplayedValue()
    {
        var cut = RenderNumber(5, []);

        cut.Render(ps => ps.Add(p => p.SelectedOption, 7));

        cut.Find("input").GetAttribute("value").Should().Be("7");
    }
}
