using Shiranuix.Persistence;

namespace Shiranuix.Tests.Fakes;

/// <summary>
/// The plugins a test has turned off, held in memory.
/// </summary>
/// <remarks>
/// The same reason as every other fake here: the state that reads this runs
/// headlessly, and a test that disabled a plugin should not leave a file
/// behind for the next one to find.
/// </remarks>
internal sealed class FakeDisabledPluginsStore : IDisabledPluginsStore {
    private readonly List<string> _ids = [];

    /// <summary>Whether anything has been written, so a test can say so.</summary>
    public bool WasWritten { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<string> Load() => _ids.ToArray();

    /// <inheritdoc />
    public void Save(IReadOnlyList<string> pluginIds) {
        ArgumentNullException.ThrowIfNull(pluginIds);

        _ids.Clear();
        _ids.AddRange(pluginIds);
        WasWritten = true;
    }

    /// <summary>
    /// Puts a plugin in the turned-off set without going through the state, so
    /// a test can start from "this one is already off".
    /// </summary>
    /// <param name="pluginId">The plugin to turn off.</param>
    public void TurnOff(string pluginId) {
        if (!_ids.Contains(pluginId, StringComparer.Ordinal)) {
            _ids.Add(pluginId);
        }
    }
}
