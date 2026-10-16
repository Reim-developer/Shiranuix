using System.Reflection;
using System.Runtime.Loader;

namespace Shiranuix.PluginHost;

/// <summary>
/// One plugin's private world: the assembly it brought, and whatever else it
/// brought with it.
/// </summary>
/// <remarks>
/// <para>
/// Each plugin gets one of these so that two plugins can carry two versions of
/// the same library without either of them knowing. Without that, whichever
/// loaded first would answer for both, and a plugin author would have to
/// match the editor's versions by hand to stay out of trouble.
/// </para>
/// <para>
/// The contract is the exception, and it is the exception that matters: the
/// editor's own copy of <c>Shiranuix.Plugins</c> is handed to every plugin,
/// because a <see cref="Plugins.IPlugin"/> from a plugin's own copy would not
/// be the interface the editor is holding, and the plugin would be built and
/// then refused for not being the right thing.
/// </para>
/// <para>
/// Collectible, because a writer can turn a plugin off while the editor is
/// running and the editor should not have to be restarted to give the memory
/// back. The runtime only frees a world once nothing holds anything from
/// it, so a plugin that left a timer or a static behind stays resident until
/// the editor closes - which costs memory and breaks nothing.
/// </para>
/// </remarks>
internal sealed class PluginLoadContext : AssemblyLoadContext {
    // The contracts the editor ships, handed to every plugin: same assembly,
    // same types, whatever the plugin brought beside its own.
    private static readonly Assembly[] Shared = [
        typeof(Plugins.IPlugin).Assembly,
        typeof(Plugins.UI.IPluginPanel).Assembly
    ];

    private readonly string _directory;

    /// <summary>
    /// Creates the world one plugin folder is loaded into.
    /// </summary>
    /// <param name="directory">The folder holding the plugin's assembly.</param>
    public PluginLoadContext(string directory)
        : base($"plugin:{Path.GetFileName(directory)}", isCollectible: true) {
        ArgumentNullException.ThrowIfNull(directory);

        _directory = directory;
    }

    /// <inheritdoc />
    protected override Assembly? Load(AssemblyName assemblyName) {
        // The contracts come from the editor, always: a type from a plugin's
        // own copy would not be the interface the editor is holding.
        foreach (var shared in Shared) {
            if (string.Equals(
                    assemblyName.Name, shared.GetName().Name, StringComparison.Ordinal)) {
                return shared;
            }
        }

        // Everything else the plugin brought is its own business, and lives in
        // its own folder. Falling through to the editor's copy is what makes
        // two plugins disagreeing about a library version harmless.
        var beside = Path.Combine(_directory, $"{assemblyName.Name}.dll");

        return File.Exists(beside) ? LoadFromAssemblyPath(beside) : null;
    }
}
