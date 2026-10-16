using Shiranuix.Plugins;
using Shiranuix.Plugins.Diagnostics;

namespace Shiranuix.Plugins.Testing;

/// <summary>
/// A log that keeps what was written, so a test can assert that a plugin said
/// the thing it should have said - or did not.
/// </summary>
public sealed class StubPluginLog : IPluginLog {
    /// <summary>Every line written, in order.</summary>
    public List<RecordedLine> Lines { get; } = [];

    /// <summary>
    /// Whether any line holds this text.
    /// </summary>
    /// <param name="fragment">A piece of the line.</param>
    /// <returns>True when some line holds it.</returns>
    public bool Mentions(string fragment) =>
        Lines.Any(line => line.Text.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether any line of that level was written.</summary>
    /// <param name="level">The level to look for.</param>
    /// <returns>True when one was.</returns>
    public bool Has(PluginLogLevel level) =>
        Lines.Any(line => line.Level == level);

    /// <inheritdoc />
    public void Write(PluginLogLevel level, string message, Exception? error = null) =>
        Lines.Add(new RecordedLine(level, message, error));

    /// <summary>
    /// One line as it was written.
    /// </summary>
    /// <param name="Level">How much it matters.</param>
    /// <param name="Text">What it says.</param>
    /// <param name="Error">What was thrown with it, if anything.</param>
    public sealed record RecordedLine(
        PluginLogLevel Level, string Text, Exception? Error);
}
