using System.Reflection;

namespace Shiranuix.Plugins;

/// <summary>
/// The version of this contract: what the host speaks, and what a plugin
/// says it needs before the host lets it in.
/// </summary>
/// <remarks>
/// Read from this assembly's own informational version rather than repeated
/// as a constant here, so there is one number in the whole repository to say
/// and nothing to drift. The editor checks a plugin's declared range against
/// <see cref="Current"/> at load time: a plugin built for a different major
/// is refused rather than half-worked.
/// </remarks>
public static class PluginApi {
    /// <summary>
    /// The contract version this assembly was built as.
    /// </summary>
    public static Version Current { get; } = ReadCurrent();

    private static Version ReadCurrent() {
        var assembly = typeof(PluginApi).Assembly;
        var informational =
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        // Build metadata rides along in the informational version ("0.1.0+abc"),
        // and Version has no place to put it.
        var number = informational is null
            ? null
            : informational.Split('+')[0].Trim();

        return Version.TryParse(number, out var version) ? version : new Version(0, 0, 0);
    }
}
