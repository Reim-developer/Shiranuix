using Shiranuix.Plugins.Commands;

namespace Shiranuix.Services;

/// <summary>
/// Where the commands plugins have offered are read from.
/// </summary>
/// <remarks>
/// Asked each time the palette opens rather than handed over once, because
/// plugins load after the window is already up: a list taken at startup would
/// be empty at exactly the moment it is needed and would never learn about
/// the plugins that arrive later. Naming the source rather than a list keeps
/// the question — "what is on offer right now?" — on the asking side, where it
/// belongs.
/// </remarks>
public interface IPluginCommandSource {
    /// <summary>
    /// The commands offered by every plugin loaded so far.
    /// </summary>
    IReadOnlyList<IPluginCommand> Commands { get; }

    /// <summary>
    /// Raised when what is on offer changes.
    /// </summary>
    /// <remarks>
    /// So a box that is already open learns about a plugin that loaded while
    /// it was open. Without this, a writer who had the palette up while a
    /// plugin was still starting would be looking at a list that had quietly
    /// gone out of date.
    /// </remarks>
    event EventHandler? CommandsChanged;

    /// <summary>
    /// An editor with nothing behind it: the source that has nothing to say,
    /// asked the same question as any other and answering the same way.
    /// </summary>
    /// <remarks>
    /// Used where the palette is built without a container — a test, a window
    /// the previewer built. An editor that has no plugins still has its own
    /// commands, so "no plugins" is not "no palette": the answer is an empty
    /// list rather than a missing one.
    /// </remarks>
    public sealed class NothingOffered : IPluginCommandSource {
        /// <summary>The one instance, because there is only one answer.</summary>
        public static NothingOffered Instance { get; } = new();

        private NothingOffered() {
        }

        /// <summary>Never any, so nothing ever changes.</summary>
        public IReadOnlyList<IPluginCommand> Commands { get; } = [];

        /// <summary>Never raised: the list this holds never moves.</summary>
        public event EventHandler? CommandsChanged {
            add { }
            remove { }
        }
    }
}
