namespace Shiranuix.Plugins;

/// <summary>
/// The span of contract versions a plugin says it works with: a minimum, and
/// optionally a maximum.
/// </summary>
/// <remarks>
/// The major part is the promise. Within one major the contract may only grow —
/// new members arrive, nothing that exists is reshaped — so a plugin built
/// against 0.1.0 loads into any 0.2. The moment a member changes shape, that
/// is a new major and older plugins are refused instead of breaking at a
/// call they made years ago.
/// <para>
/// Text form is the one a manifest carries: <c>0.1</c> for a minimum,
/// <c>0.1 - 0.3</c> for a window, <c>*</c> for any version at all.
/// </para>
/// </remarks>
public sealed record PluginApiRange {
    /// <summary>A range that takes no version at all.</summary>
    public static PluginApiRange Any { get; } = new(new Version(0, 0), null);

    /// <summary>Creates a range.</summary>
    /// <param name="minimum">The oldest contract the plugin works with.</param>
    /// <param name="maximum">
    /// The newest it works with, or null for "and every later minor".
    /// </param>
    public PluginApiRange(Version minimum, Version? maximum = null) {
        if (minimum is null) {
            throw new ArgumentNullException(nameof(minimum));
        }

        if (minimum < new Version(0, 0)) {
            throw new ArgumentOutOfRangeException(
                nameof(minimum), minimum, "A version is never negative.");
        }

        if (maximum is not null && maximum < minimum) {
            throw new ArgumentOutOfRangeException(
                nameof(maximum), maximum, "A maximum below the minimum is no range at all.");
        }

        Minimum = minimum;
        Maximum = maximum;
    }

    /// <summary>The oldest contract the plugin works with.</summary>
    public Version Minimum { get; }

    /// <summary>
    /// The newest contract the plugin works with, or null when it takes every
    /// later minor.
    /// </summary>
    public Version? Maximum { get; }

    /// <summary>
    /// Whether a host speaking <paramref name="version"/> can load the plugin.
    /// </summary>
    /// <param name="version">The contract version the host speaks.</param>
    /// <returns>
    /// True when the versions share a major and the host's falls in the range.
    /// A bound is read as the line it was written as: <c>0.3</c> takes the
    /// whole 0.3 line, 0.3.4 and 0.3.9 included.
    /// </returns>
    public bool Contains(Version version) {
        if (version is null) {
            throw new ArgumentNullException(nameof(version));
        }

        return version.Major == Minimum.Major
            && AtLeast(version, Minimum)
            && (Maximum is null || AtMost(version, Maximum));
    }

    /// <summary>
    /// Compares against a bound as the bound was written: "0.3" names the whole
    /// 0.3 line, so 0.3.4 is inside it while 0.4 is not.
    /// </summary>
    private static bool AtLeast(Version version, Version bound) =>
        Line(version, bound) >= Line(bound, bound);

    private static bool AtMost(Version version, Version bound) =>
        Line(version, bound) <= Line(bound, bound);

    /// <summary>
    /// Cuts a version down to as many parts as the bound was written with.
    /// </summary>
    private static Version Line(Version version, Version bound) =>
        bound.Build < 0
            ? new Version(version.Major, version.Minor)
            : new Version(version.Major, version.Minor, Math.Max(version.Build, 0));

    /// <summary>
    /// Reads the text form a manifest carries.
    /// </summary>
    /// <param name="text">
    /// <c>0.1</c>, <c>0.1 - 0.3</c>, <c>0.1.2</c> or <c>*</c>.
    /// </param>
    /// <returns>The range the text names.</returns>
    /// <exception cref="ArgumentException">The text is not one of those shapes.</exception>
    public static PluginApiRange Parse(string text) {
        if (text is null) {
            throw new ArgumentNullException(nameof(text));
        }

        var trimmed = text.Trim();
        if (trimmed == "*") {
            return Any;
        }

        var window = trimmed.Split('-');
        if (window.Length > 2) {
            throw new ArgumentException(
                $"'{text}' is not a version, a window or '*'.", nameof(text));
        }

        var minimum = VersionOf(window[0], text);
        var maximum = window.Length == 2 ? VersionOf(window[1], text) : null;

        return new PluginApiRange(minimum, maximum);
    }

    private static Version VersionOf(string part, string text) {
        var trimmed = part.Trim();

        // "0.1" is a shorthand for the whole 0.1 line, which is the shape a
        // manifest almost always carries.
        if (!Version.TryParse(trimmed, out var version)) {
            throw new ArgumentException(
                $"'{text}' does not hold a version where '{trimmed}' stands.", nameof(text));
        }

        return version;
    }
}
