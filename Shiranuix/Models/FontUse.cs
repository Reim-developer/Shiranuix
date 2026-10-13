namespace Shiranuix.Models;

/// <summary>
/// One typeface at one point size the user has picked.
/// </summary>
/// <remarks>
/// One row per family/size pair, so re-picking the same typeface at the same
/// size moves it to the front of the history rather than adding a second
/// entry the picker would then have to skip over.
/// </remarks>
public sealed class FontUse {
    /// <summary>The row's primary key.</summary>
    public int Id { get; set; }

    /// <summary>The typeface that was picked.</summary>
    public string FontFamily { get; set; } = string.Empty;

    /// <summary>The point size it was picked at.</summary>
    public double FontSize { get; set; }

    /// <summary>When it was last picked, in UTC.</summary>
    public DateTime UsedAtUtc { get; set; }
}
