namespace Faemiyah.Ui;

/// <summary>
/// A range of numbers offered as a single choice by the number picker components.
/// </summary>
/// <remarks>
/// Picking a bracket selects its <see cref="End"/>. A bracket whose bounds are equal represents a single number.
/// </remarks>
public class PickBracket
{
    /// <summary>
    /// Beginning of the bracket, inclusive.
    /// </summary>
    public int Begin { get; set; }

    /// <summary>
    /// End of the bracket, inclusive.
    /// </summary>
    public int End { get; set; }

    /// <inheritdoc />
    public override string ToString()
    {
        return ToString(false);
    }

    /// <summary>
    /// Produces a string representation of the bracket, with a <c>+</c> prefix on non-negative numbers if requested.
    /// </summary>
    /// <param name="displayNumberPrefix">Whether to prefix non-negative numbers with <c>+</c>.</param>
    /// <returns>The bracket as <c>begin-end</c>, or as a single number when the bounds are equal.</returns>
    public string ToString(bool displayNumberPrefix)
    {
        var begin = displayNumberPrefix && Begin >= 0 ? $"+{Begin}" : $"{Begin}";
        var end = displayNumberPrefix && End >= 0 ? $"+{End}" : $"{End}";

        return Begin != End ? $"{begin}-{end}" : $"{begin}";
    }
}
