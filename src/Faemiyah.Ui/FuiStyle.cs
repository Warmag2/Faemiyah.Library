using System;

namespace Faemiyah.Ui;

/// <summary>
/// CSS class names and inline styles shared by the Faemiyah UI components.
/// </summary>
/// <remarks>
/// The class names match the rules in <c>_content/Faemiyah.Ui/faemiyah-ui.css</c>.
/// </remarks>
public static class FuiStyle
{
    /// <summary>
    /// Modifier class for an element in its active, selected or expanded state.
    /// </summary>
    public const string Active = "fui-active";

    /// <summary>
    /// Modifier class for an element in its inactive, unselected or collapsed state.
    /// </summary>
    public const string Inactive = "fui-inactive";

    /// <summary>
    /// Modifier class for an element whose current value is invalid.
    /// </summary>
    public const string Error = "fui-error";

    /// <summary>
    /// Modifier class for an element whose value has not been touched since a reference moment.
    /// </summary>
    public const string Reminder = "fui-reminder";

    /// <summary>
    /// Modifier class for an element which cannot be interacted with.
    /// </summary>
    public const string Disabled = "fui-disabled";

    /// <summary>
    /// Returns an inline style which hides an element, or nothing if it should stay visible.
    /// </summary>
    /// <param name="hidden">Whether the element should be hidden.</param>
    /// <returns><c>display:none</c> when <paramref name="hidden"/> is <b>true</b>, otherwise an empty string.</returns>
    public static string HideElement(bool hidden)
    {
        return hidden ? "display:none" : string.Empty;
    }

    /// <summary>
    /// Returns the class indicating whether an element is active.
    /// </summary>
    /// <param name="active">Whether the element is active.</param>
    /// <returns><see cref="Active"/> or <see cref="Inactive"/>.</returns>
    public static string GetActiveClass(bool active)
    {
        return active ? Active : Inactive;
    }

    /// <summary>
    /// Returns the reminder class when a value has not been updated since the comparison moment.
    /// </summary>
    /// <param name="comparisonTimeStamp">The reference moment, for example the start of the current turn.</param>
    /// <param name="updateTimeStamp">The moment the value was last updated by the user.</param>
    /// <returns><see cref="Reminder"/> when the value is older than the reference moment, otherwise an empty string.</returns>
    public static string GetReminderClass(DateTime comparisonTimeStamp, DateTime updateTimeStamp)
    {
        return comparisonTimeStamp > updateTimeStamp ? Reminder : string.Empty;
    }
}
