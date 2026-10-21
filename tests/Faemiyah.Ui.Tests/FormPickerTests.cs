using System.Collections.Generic;
using AwesomeAssertions;
using Bunit;
using Faemiyah.Ui.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Faemiyah.Ui.Tests;

[TestFixture]
public class FormComboBoxTests : ComponentTestBase
{
    private static SortedDictionary<string, int> CreateOptions()
    {
        return new SortedDictionary<string, int> { ["Alpha"] = 1, ["Alphabet"] = 2, ["Beta"] = 3 };
    }

    private IRenderedComponent<FormComboBox<int>> RenderComboBox(List<int> values, int selected = 0, bool errorDisplay = false)
    {
        return Context.Render<FormComboBox<int>>(ps => ps
            .Add(p => p.Options, CreateOptions())
            .Add(p => p.SelectedOption, selected)
            .Add(p => p.EnableErrorDisplay, errorDisplay)
            .Add(p => p.OnChanged, (int v) => values.Add(v)));
    }

    [Test]
    public void Initialization_ShowsKeyOfSelectedValue()
    {
        var values = new List<int>();
        var cut = RenderComboBox(values, selected: 3);

        cut.Instance.SelectedOptionInternal.Should().Be("Beta");
        values.Should().Equal(3);
    }

    [Test]
    public void ListIsHiddenUntilFocused_AndFilteredByInput()
    {
        var cut = RenderComboBox([]);

        cut.Find(".fui-combobox-list").GetAttribute("style").Should().Be("display:none");
        cut.FindAll(".fui-combobox-item").Should().BeEmpty();

        cut.Find("input").Focus();
        cut.FindAll(".fui-combobox-item").Should().HaveCount(3);

        cut.Find("input").Input("alp");
        cut.FindAll(".fui-combobox-item").Should().HaveCount(2);
    }

    [Test]
    public void TypingExactKey_SelectsIt_EvenWhenOtherKeysContainIt()
    {
        var values = new List<int>();
        var cut = RenderComboBox(values);

        cut.Find("input").Input("alp");

        values.Should().BeEmpty();

        cut.Find("input").Input("alpha");

        values.Should().Equal(1);
        cut.Instance.SelectedOptionInternal.Should().Be("Alpha");
    }

    [Test]
    public void Enter_WithSingleCandidate_SelectsIt()
    {
        var values = new List<int>();
        var cut = RenderComboBox(values);
        var input = cut.Find("input");

        input.Focus();
        input.Input("eta");
        cut.Find("input").KeyPress(new KeyboardEventArgs { Key = "Enter" });

        values.Should().Equal(3);
        cut.FindAll(".fui-combobox-item").Should().BeEmpty();
    }

    [Test]
    public void ClickingItem_SelectsItAndCloses()
    {
        var values = new List<int>();
        var closed = 0;
        var cut = Context.Render<FormComboBox<int>>(ps => ps
            .Add(p => p.Options, CreateOptions())
            .Add(p => p.OnChanged, (int v) => values.Add(v))
            .Add(p => p.OnOutOfFocus, () => closed++));

        cut.Find("input").Focus();
        cut.FindAll(".fui-combobox-item")[1].Click();

        values.Should().Equal(2);
        closed.Should().Be(1);
        cut.Find(".fui-combobox-list").GetAttribute("style").Should().Be("display:none");
    }

    [Test]
    public void FocusOut_IsSuppressedWhileHoveringItems()
    {
        var closed = 0;
        var cut = Context.Render<FormComboBox<int>>(ps => ps
            .Add(p => p.Options, CreateOptions())
            .Add(p => p.OnOutOfFocus, () => closed++));

        cut.Find("input").Focus();
        cut.FindAll(".fui-combobox-item")[0].MouseOver();
        cut.Find("input").FocusOut();

        closed.Should().Be(0);

        cut.FindAll(".fui-combobox-item")[0].MouseOut();
        cut.Find("input").FocusOut();

        closed.Should().Be(1);
    }

    [Test]
    public void ErrorDisplay_MarksInvalidSelection()
    {
        var cut = RenderComboBox([], errorDisplay: true);

        cut.Find("input").ClassList.Should().Contain("fui-error");
    }

    [Test]
    public void Disabled_DoesNotOpenList()
    {
        var cut = Context.Render<FormComboBox<int>>(ps => ps
            .Add(p => p.Options, CreateOptions())
            .Add(p => p.Disabled, true));

        cut.Find("input").Focus();

        cut.FindAll(".fui-combobox-item").Should().BeEmpty();
    }
}

[TestFixture]
public class FormNumberPickerTests : ComponentTestBase
{
    private static IReadOnlyList<PickBracket> CreateBrackets()
    {
        return [new PickBracket { Begin = 0, End = 2 }, new PickBracket { Begin = 3, End = 5 }];
    }

    [Test]
    public void Picker_ShowsBracketsAndSelectsBracketEnd()
    {
        var values = new List<int>();
        var cut = Context.Render<FormNumberPicker>(ps => ps
            .Add(p => p.BracketCreatorDelegate, CreateBrackets)
            .Add(p => p.SelectedOption, 1)
            .Add(p => p.Min, 0)
            .Add(p => p.Max, 5)
            .Add(p => p.OnChanged, (int v) => values.Add(v)));

        cut.Find("button").Click();

        var items = cut.FindAll(".fui-picker-item");
        items.Should().HaveCount(2);
        items[0].ClassList.Should().Contain("fui-active");
        items[1].TextContent.Should().Be("3-5");

        items[1].Click();

        values.Should().Equal(1, 5);
        cut.Find(".fui-picker").GetAttribute("style").Should().Be("display:none");
    }

    [Test]
    public void Picker_ButtonTogglesList()
    {
        var cut = Context.Render<FormNumberPicker>(ps => ps.Add(p => p.BracketCreatorDelegate, CreateBrackets));

        cut.Find("button").Click();
        cut.Find(".fui-picker").GetAttribute("style").Should().BeNullOrEmpty();

        cut.Find("button").Click();
        cut.Find(".fui-picker").GetAttribute("style").Should().Be("display:none");
    }

    [Test]
    public void DisplayOnly_ShowsPrefixedNumberAndReportsHint()
    {
        var values = new List<(string?, int)>();
        var cut = Context.Render<FormNumberPickerDisplayOnly>(ps => ps
            .Add(p => p.BracketCreatorDelegate, CreateBrackets)
            .Add(p => p.NumberToDisplay, 2)
            .Add(p => p.DisplayNumberPrefix, true)
            .Add(p => p.StyleSelectorDelegate, v => $"style-{v}")
            .Add(p => p.Hint, "range")
            .Add(p => p.OnChangedWithHint, ((string?, int) v) => values.Add(v)));

        var display = cut.Find(".fui-number-display");
        display.TextContent.Should().Be("+2");
        display.ClassList.Should().Contain("style-2");

        cut.Find("button").Click();
        cut.FindAll(".fui-picker-item")[1].TextContent.Should().Be("+3-+5");
        cut.FindAll(".fui-picker-item")[1].Click();

        values.Should().Equal(("range", 2), ("range", 5));
    }

    [Test]
    public void DisplayOnly_ClickingNumber_SwitchesToClampedDirectEdit()
    {
        var values = new List<int>();
        var cut = Context.Render<FormNumberPickerDisplayOnly>(ps => ps
            .Add(p => p.BracketCreatorDelegate, CreateBrackets)
            .Add(p => p.NumberToDisplay, 2)
            .Add(p => p.OnChanged, (int v) => values.Add(v)));

        cut.Find(".fui-number-display").Click();

        var input = cut.Find("input");
        input.GetAttribute("max").Should().Be("5");
        input.Focus();
        input.Input("8");
        input.Blur();

        values.Should().Equal(2, 5);
        cut.FindAll("input").Should().BeEmpty();
    }

    [Test]
    public void DisplayOnly_Disabled_DoesNotEdit()
    {
        var cut = Context.Render<FormNumberPickerDisplayOnly>(ps => ps
            .Add(p => p.BracketCreatorDelegate, CreateBrackets)
            .Add(p => p.Disabled, true));

        cut.Find(".fui-number-display").Click();

        cut.FindAll("input").Should().BeEmpty();
    }
}

[TestFixture]
public class FormPickSetTests : ComponentTestBase
{
    private static SortedDictionary<string, int> CreateOptions()
    {
        return new SortedDictionary<string, int> { ["Alice"] = 1, ["Bob"] = 2, ["Carol"] = 3 };
    }

    [Test]
    public void ShowsContainedItemsAndRemovesThem()
    {
        var results = new List<HashSet<int>>();
        var cut = Context.Render<FormPickSet<int>>(ps => ps
            .Add(p => p.Options, CreateOptions())
            .Add(p => p.ContainedItems, [1, 3])
            .Add(p => p.Editable, true)
            .Add(p => p.OnChanged, (HashSet<int> v) => results.Add(v)));

        var items = cut.FindAll(".fui-pickset-item");
        items.Should().HaveCount(2);

        items[0].QuerySelector("button")!.Click();

        results.Should().ContainSingle().Which.Should().BeEquivalentTo([3]);
        cut.FindAll(".fui-pickset-item").Should().HaveCount(1);
    }

    [Test]
    public void AddsItemsThroughComboBox()
    {
        var results = new List<HashSet<int>>();
        var cut = Context.Render<FormPickSet<int>>(ps => ps
            .Add(p => p.Options, CreateOptions())
            .Add(p => p.ContainedItems, [])
            .Add(p => p.Editable, true)
            .Add(p => p.ShowAddButton, true)
            .Add(p => p.OnChanged, (HashSet<int> v) => results.Add(v)));

        cut.Find(".fui-button-add").Click();
        cut.Find("input").Input("Bob");

        results.Should().ContainSingle().Which.Should().BeEquivalentTo([2]);
        cut.Find(".fui-pickset-item").TextContent.Should().Contain("Bob");
    }

    [Test]
    public void NotEditable_HidesDeleteButtonsAndAddDialog()
    {
        var cut = Context.Render<FormPickSet<int>>(ps => ps
            .Add(p => p.Options, CreateOptions())
            .Add(p => p.ContainedItems, [2]));

        cut.Find(".fui-pickset-item button").GetAttribute("style").Should().Be("display:none");
        cut.FindAll(".fui-button-add").Should().BeEmpty();
    }
}

[TestFixture]
public class TooltipTests : ComponentTestBase
{
    [Test]
    public void RendersTooltipElement()
    {
        var cut = Context.Render<Tooltip>(ps => ps
            .Add(p => p.Id, "tip")
            .Add(p => p.Class, "extra"));

        var element = cut.Find("#tip");
        element.ClassList.Should().Contain(["fui-tooltip", "extra"]);
    }
}
