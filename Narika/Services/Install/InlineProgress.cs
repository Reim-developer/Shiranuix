namespace Narika.Services.Install;

/// <summary>
/// An <see cref="IProgress{T}"/> that calls back on the thread that
/// reports, rather than posting the call somewhere else to be.
/// </summary>
/// <typeparam name="T">What is being reported.</typeparam>
/// <remarks>
/// The framework's own <see cref="Progress{T}"/> hands its report to the
/// synchronization context it was made on — right for a UI that should
/// only ever be touched on its own thread, and wrong for two other callers
/// this installer has: a test that would otherwise have to wait on a
/// thread-pool post it cannot see, and a stub service reporting several
/// steps in a row whose order must not depend on where the reports land.
/// Reporting here is a plain call, which is the whole of what it does.
/// </remarks>
public sealed class InlineProgress<T>(Action<T> report) : IProgress<T> {
    private readonly Action<T> _report =
        report ?? throw new ArgumentNullException(nameof(report));

    /// <inheritdoc />
    public void Report(T value) => _report(value);
}
