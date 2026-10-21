using Microsoft.Extensions.Logging;

namespace Shiranuix.Tests.Fakes;

/// <summary>
/// A log factory that keeps what was written, so a test can ask whether the
/// editor said the thing it was supposed to say.
/// </summary>
/// <remarks>
/// The editor's own log engine is not what is under test here — it is what the
/// code under test does with its lines and its refusals - so this keeps the
/// formatted line, the category it came from and the level, and nothing else.
/// </remarks>
public sealed class RecordingLoggerFactory : ILoggerFactory {
    /// <summary>Every line written, in order.</summary>
    public List<RecordedLine> Lines { get; } = [];

    /// <summary>
    /// Whether any line holds this text, in the line or in what was thrown
    /// with it.
    /// </summary>
    /// <param name="fragment">A piece of the line.</param>
    /// <returns>True when some line holds it.</returns>
    public bool Mentions(string fragment) =>
        Lines.Any(line =>
            line.Text.Contains(fragment, StringComparison.OrdinalIgnoreCase)
            || (line.Error?.Contains(fragment, StringComparison.OrdinalIgnoreCase) ?? false));

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new RecordingLogger(this, categoryName);

    /// <inheritdoc />
    public void AddProvider(ILoggerProvider provider) {
        // Nothing to add to: the lines are kept here rather than written out.
    }

    /// <inheritdoc />
    public void Dispose() {
    }

    /// <summary>
    /// One line as it was written. The error is kept beside the text because
    /// the editor's real log keeps it too, and because most of what the
    /// code has to say about what would not go through is in there.
    /// </summary>
    /// <param name="Category">Where it was written from.</param>
    /// <param name="Level">How much it matters.</param>
    /// <param name="Text">What it says.</param>
    /// <param name="Error">What was thrown with it, if anything.</param>
    public sealed record RecordedLine(
        string Category, LogLevel Level, string Text, string? Error);

    private sealed class RecordingLogger(RecordingLoggerFactory owner, string category)
        : ILogger {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            owner.Lines.Add(new RecordedLine(
                category, logLevel, formatter(state, exception), exception?.Message));
    }
}
