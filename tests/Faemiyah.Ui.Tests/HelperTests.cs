using System;
using AwesomeAssertions;
using NUnit.Framework;

namespace Faemiyah.Ui.Tests;

[TestFixture]
public class FuiStyleTests
{
    [Test]
    public void HideElement_ReturnsDisplayNoneOnlyWhenHidden()
    {
        FuiStyle.HideElement(true).Should().Be("display:none");
        FuiStyle.HideElement(false).Should().BeEmpty();
    }

    [Test]
    public void GetActiveClass_ReturnsActiveOrInactive()
    {
        FuiStyle.GetActiveClass(true).Should().Be("fui-active");
        FuiStyle.GetActiveClass(false).Should().Be("fui-inactive");
    }

    [Test]
    public void GetReminderClass_ReturnsReminderOnlyWhenValueIsOlderThanComparison()
    {
        var now = DateTime.UtcNow;

        FuiStyle.GetReminderClass(now, now.AddSeconds(-1)).Should().Be("fui-reminder");
        FuiStyle.GetReminderClass(now, now).Should().BeEmpty();
        FuiStyle.GetReminderClass(now, now.AddSeconds(1)).Should().BeEmpty();
    }
}

[TestFixture]
public class PickBracketTests
{
    [TestCase(3, 3, false, "3")]
    [TestCase(1, 4, false, "1-4")]
    [TestCase(1, 4, true, "+1-+4")]
    [TestCase(0, 0, true, "+0")]
    [TestCase(-2, 2, true, "-2-+2")]
    [TestCase(-3, -1, true, "-3--1")]
    public void ToString_FormatsBracket(int begin, int end, bool prefix, string expected)
    {
        var bracket = new PickBracket { Begin = begin, End = end };

        bracket.ToString(prefix).Should().Be(expected);
    }

    [Test]
    public void ToString_WithoutArguments_OmitsPrefix()
    {
        new PickBracket { Begin = 2, End = 5 }.ToString().Should().Be("2-5");
    }
}
