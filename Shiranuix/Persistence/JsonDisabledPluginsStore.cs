using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Shiranuix.Persistence;

/// <summary>
/// The disabled set as one small JSON file beside the editor's other data.
/// </summary>
/// <remarks>
/// <para>
/// A file rather than a row in the database, and that is a decision rather
/// than laziness. This list is a few words a writer can read, edit and delete
/// when a plugin will not start and they would rather see why than argue with
/// the editor; a table inside <c>shiranuix.db</c> would make that a support
/// question. It is also the one piece of state that has to exist when nothing
/// else does, since the plugins it names are precisely the ones that are not
/// running.
/// </para>
/// <para>
/// Every failure here is swallowed, in the direction that loads the plugin.
/// A list that cannot be read means "nothing is turned off", which at worst
/// starts a plugin the writer had switched off once - and it says so in the
/// log - rather than a folder that has quietly gone missing.
/// </para>
/// </remarks>
public sealed class JsonDisabledPluginsStore : IDisabledPluginsStore {
    private readonly ILogger<JsonDisabledPluginsStore> _log;

    /// <summary>
    /// Creates the store, reading and writing one file in the editor's folder.
    /// </summary>
    /// <param name="log">Where a file that could not be read is written about.</param>
    public JsonDisabledPluginsStore(ILogger<JsonDisabledPluginsStore> log) {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Load() {
        try {
            if (!File.Exists(FilePath)) {
                return [];
            }

            return JsonSerializer.Deserialize<string[]>(Read()) ?? [];
        } catch (Exception error) when (error is IOException or JsonException
            or UnauthorizedAccessException) {
            _log.LogWarning(
                error,
                "{Path} could not be read, so no plugin is treated as turned off.",
                FilePath);

            return [];
        }
    }

    /// <inheritdoc />
    public void Save(IReadOnlyList<string> pluginIds) {
        ArgumentNullException.ThrowIfNull(pluginIds);

        try {
            AppFolder.EnsureExists();
            File.WriteAllText(FilePath, Write(pluginIds));
        } catch (Exception error) when (error is IOException or UnauthorizedAccessException) {
            _log.LogWarning(error, "{Path} could not be written.", FilePath);
        }
    }

    /// <summary>The file the disabled set lives in.</summary>
    private static string FilePath => Path.Combine(AppFolder.Root, "plugins-disabled.json");

    private static string Read() => File.ReadAllText(FilePath);

    private static string Write(IReadOnlyList<string> pluginIds) =>
        JsonSerializer.Serialize(pluginIds);
}
