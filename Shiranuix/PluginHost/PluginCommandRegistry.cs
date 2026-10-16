using Shiranuix.Plugins.Commands;
using Shiranuix.Services;

namespace Shiranuix.PluginHost;

/// <summary>
/// What plugins have offered, kept as it arrives.
/// </summary>
/// <remarks>
/// <para>
/// The list of plugin commands lives here rather than being asked of the
/// catalog, because the catalog needs the editor and the editor needs to know
/// what commands exist: asking the catalog for its commands would mean
/// building an editor in order to build the editor.
/// </para>
/// <para>
/// So the catalog tells this what each plugin offered as it starts, and the
/// palette reads it. A plugin that loads after the window is up therefore
/// appears in the box the next time it opens, which is the whole point of a
/// palette: it is the door for commands nobody could have known at startup.
/// </para>
/// </remarks>
internal sealed class PluginCommandRegistry : IPluginCommandSource {
    private readonly List<IPluginCommand> _commands = [];
    private readonly Lock _gate = new();

    /// <summary>
    /// Everything offered so far.
    /// </summary>
    /// <remarks>
    /// Read on the thread that opens the palette and written on the thread
    /// that finished starting a plugin, so the list is swapped rather than
    /// edited: a reader either sees the commands from a moment ago or the
    /// commands from now, never half of each.
    /// </remarks>
    public IReadOnlyList<IPluginCommand> Commands {
        get {
            lock (_gate) {
                return _commands.ToArray();
            }
        }
    }

    /// <summary>Raised when a plugin has offered something new.</summary>
    public event EventHandler? CommandsChanged;

    /// <summary>
    /// Takes what one plugin offered.
    /// </summary>
    /// <param name="commands">The commands it offered.</param>
    public void Offer(IReadOnlyList<IPluginCommand> commands) {
        ArgumentNullException.ThrowIfNull(commands);

        if (commands.Count == 0) {
            return;
        }

        lock (_gate) {
            _commands.AddRange(commands);
        }

        // Said to whoever is listening rather than only to whoever asks next:
        // a palette that is open while a plugin loads should not need to be
        // closed and opened again to learn about it.
        CommandsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Puts the list back to nothing, for the editor closing or for a test
    /// that wants to start over.
    /// </summary>
    public void Clear() {
        lock (_gate) {
            _commands.Clear();
        }
    }
}
