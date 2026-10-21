using System.Collections.Generic;
using AwesomeAssertions;
using Bunit;
using Faemiyah.Ui.Components;
using Microsoft.AspNetCore.Components;
using NUnit.Framework;

namespace Faemiyah.Ui.Tests;

[TestFixture]
public class ContainerTests : ComponentTestBase
{
    [Test]
    public void Accordion_StartsClosedAndTogglesOnClick()
    {
        var cut = Context.Render<ContainerAccordion>(ps => ps
            .Add(p => p.Title, "Section")
            .AddChildContent("<p id='content'>Body</p>"));

        var content = cut.Find(".fui-accordion-content");
        content.GetAttribute("style").Should().Be("display:none");
        cut.Find(".fui-accordion-indicator").TextContent.Trim().Should().Be("+");

        cut.Find("button").Click();

        cut.Find(".fui-accordion-content").GetAttribute("style").Should().BeNullOrEmpty();
        cut.Find(".fui-accordion-button").ClassList.Should().Contain("fui-active");
        cut.Find(".fui-accordion-indicator").TextContent.Trim().Should().Be("-");
        cut.Find("button").GetAttribute("type").Should().Be("button");
    }

    [Test]
    public void Accordion_InitiallyEnabled_IsOpen()
    {
        var cut = Context.Render<ContainerAccordion>(ps => ps.Add(p => p.Enabled, true));

        cut.Find(".fui-accordion-content").ClassList.Should().Contain("fui-active");
    }

    [Test]
    public void Expander_SwitchesBetweenContents()
    {
        var cut = Context.Render<ContainerExpander>(ps => ps
            .Add(p => p.ChildContentEnabled, (RenderFragment)(b => b.AddMarkupContent(0, "<p id='on'>On</p>")))
            .Add(p => p.ChildContentDisabled, (RenderFragment)(b => b.AddMarkupContent(0, "<p id='off'>Off</p>"))));

        cut.FindAll("#off").Should().HaveCount(1);
        cut.FindAll("#on").Should().BeEmpty();
        cut.Find(".fui-expander-button").ClassList.Should().Contain("fui-active");

        cut.Find(".fui-expander-button").Click();

        cut.FindAll("#on").Should().HaveCount(1);
        cut.FindAll("#off").Should().BeEmpty();
        cut.Find(".fui-expander-button").ClassList.Should().Contain("fui-inactive");
    }

    [Test]
    public void Modal_WithoutPrimary_HasNoFooter()
    {
        var closed = 0;
        var cut = Context.Render<ContainerModal>(ps => ps
            .Add(p => p.Title, "Dialog")
            .Add(p => p.OnClose, () => closed++));

        cut.FindAll(".fui-modal-footer").Should().BeEmpty();
        cut.Find(".fui-modal-title").TextContent.Should().Be("Dialog");

        cut.Find(".fui-modal-close").Click();

        closed.Should().Be(1);
    }

    [Test]
    public void Modal_WithPrimary_RendersFooterWithCustomTexts()
    {
        var primary = 0;
        var closed = 0;
        var cut = Context.Render<ContainerModal>(ps => ps
            .Add(p => p.PrimaryText, "Save")
            .Add(p => p.CancelText, "Peruuta")
            .Add(p => p.OnPrimary, () => primary++)
            .Add(p => p.OnClose, () => closed++));

        var buttons = cut.FindAll(".fui-modal-footer button");
        buttons.Should().HaveCount(2);
        buttons[0].TextContent.Should().Be("Save");
        buttons[1].TextContent.Should().Be("Peruuta");

        buttons[0].Click();
        cut.FindAll(".fui-modal-footer button")[1].Click();

        primary.Should().Be(1);
        closed.Should().Be(1);
    }

    [TestCase("a", "a", true, true)]
    [TestCase("a", "b", true, false)]
    [TestCase("a", "a", false, false)]
    public void Tab_RendersOnlyWhenSelectedAndEnabled(string identity, string selection, bool enabled, bool rendered)
    {
        var cut = Context.Render<ContainerTab>(ps => ps
            .Add(p => p.TabIdentity, identity)
            .Add(p => p.TabSelection, selection)
            .Add(p => p.Enabled, enabled)
            .AddChildContent("<p id='content'>Body</p>"));

        cut.FindAll("#content").Should().HaveCount(rendered ? 1 : 0);
    }

    [Test]
    public void TabSelector_MarksSelectedTabAndReportsClicks()
    {
        var selected = new List<string>();
        var cut = Context.Render<ContainerTabSelector>(ps => ps
            .Add(p => p.Enabled, true)
            .Add(p => p.TabNames, ["Meeting", "Expenses"])
            .Add(p => p.SelectedTab, "Meeting")
            .Add(p => p.OnTabSelected, (string tab) => selected.Add(tab)));

        var buttons = cut.FindAll(".fui-tab button");
        buttons[0].ClassList.Should().Contain("fui-active");
        buttons[1].ClassList.Should().Contain("fui-inactive");

        buttons[1].Click();

        selected.Should().Equal("Expenses");
    }

    [Test]
    public void TabSelector_Disabled_IsHidden()
    {
        var cut = Context.Render<ContainerTabSelector>(ps => ps.Add(p => p.TabNames, ["A"]));

        cut.Find(".fui-tabs").GetAttribute("style").Should().Be("display:none");
    }
}

[TestFixture]
public class ContainerReorderableListTests : ComponentTestBase
{
    private IRenderedComponent<ContainerReorderableList<string>> RenderList(List<string> items, System.Action onChanged, bool showDragHandle = false)
    {
        return Context.Render<ContainerReorderableList<string>>(ps => ps
            .Add(p => p.Items, items)
            .Add(p => p.ShowDragHandle, showDragHandle)
            .Add(p => p.ItemClassSelector, item => $"item-{item}")
            .Add(p => p.ItemTemplate, item => b => b.AddContent(0, item))
            .Add(p => p.OnChanged, onChanged));
    }

    [Test]
    public void DroppingDownwards_InsertsBeforeTargetItem()
    {
        var items = new List<string> { "a", "b", "c", "d" };
        var changes = 0;
        var cut = RenderList(items, () => changes++);

        cut.FindAll(".fui-reorderable-item")[0].DragStart();
        cut.FindAll(".fui-reorderable-item")[2].Drop();

        items.Should().Equal("b", "a", "c", "d");
        changes.Should().Be(1);
    }

    [Test]
    public void DroppingUpwards_InsertsAtTargetPosition()
    {
        var items = new List<string> { "a", "b", "c", "d" };
        var cut = RenderList(items, () => { });

        cut.FindAll(".fui-reorderable-item")[3].DragStart();
        cut.FindAll(".fui-reorderable-item")[1].Drop();

        items.Should().Equal("a", "d", "b", "c");
    }

    [Test]
    public void DroppingOnSentinel_MovesItemToEnd()
    {
        var items = new List<string> { "a", "b", "c" };
        var cut = RenderList(items, () => { });

        cut.FindAll(".fui-reorderable-item")[0].DragStart();
        cut.Find(".fui-reorderable-sentinel").Drop();

        items.Should().Equal("b", "c", "a");
    }

    [Test]
    public void DroppingOnItself_OrDirectlyBelow_DoesNothing()
    {
        var items = new List<string> { "a", "b", "c" };
        var changes = 0;
        var cut = RenderList(items, () => changes++);

        cut.FindAll(".fui-reorderable-item")[1].DragStart();
        cut.FindAll(".fui-reorderable-item")[1].Drop();
        cut.FindAll(".fui-reorderable-item")[1].DragStart();
        cut.FindAll(".fui-reorderable-item")[2].Drop();

        items.Should().Equal("a", "b", "c");
        changes.Should().Be(0);
    }

    [Test]
    public void DropWithoutDrag_DoesNothing()
    {
        var items = new List<string> { "a", "b" };
        var changes = 0;
        var cut = RenderList(items, () => changes++);

        cut.FindAll(".fui-reorderable-item")[0].Drop();

        changes.Should().Be(0);
    }

    [Test]
    public void CancelledDrag_IsForgotten()
    {
        var items = new List<string> { "a", "b", "c" };
        var cut = RenderList(items, () => { });

        cut.FindAll(".fui-reorderable-item")[0].DragStart();
        cut.FindAll(".fui-reorderable-item")[0].DragEnd();
        cut.Find(".fui-reorderable-sentinel").Drop();

        items.Should().Equal("a", "b", "c");
    }

    [Test]
    public void WithoutDragHandle_WholeItemIsDraggable()
    {
        var cut = RenderList(["a"], () => { });

        var item = cut.Find(".fui-reorderable-item");
        item.ClassList.Should().Contain(["fui-draggable", "item-a"]);
        item.GetAttribute("draggable").Should().Be("true");
        cut.FindAll(".fui-drag-handle").Should().BeEmpty();
    }

    [Test]
    public void WithDragHandle_OnlyHandleIsDraggable()
    {
        var items = new List<string> { "a", "b" };
        var cut = RenderList(items, () => { }, showDragHandle: true);

        var item = cut.FindAll(".fui-reorderable-item")[0];
        item.ClassList.Should().NotContain("fui-draggable");
        item.GetAttribute("draggable").Should().Be("false");

        cut.FindAll(".fui-drag-handle")[0].DragStart();
        cut.Find(".fui-reorderable-sentinel").Drop();

        items.Should().Equal("b", "a");
    }
}
