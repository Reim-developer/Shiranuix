using Avalonia.Controls;
using Shiranuix.FileTree.Views;
using Shiranuix.ViewModels;

namespace Shiranuix.FileTree;

/// <summary>
/// The tree's panel: the column down the left of the page, with the tree in it.
/// </summary>
/// <remarks>
/// The panel is thin on purpose. It builds the tree's view over its host and
/// holds a tab for itself - everything a writer can do happens either in the
/// tree or through one of the tree's commands, so the panel needs no logic of
/// its own to test.
/// </remarks>
internal sealed class FileTreePanel : IPanel {
    /// <summary>
    /// Builds the panel, and the tree in it, already answering to its host.
    /// </summary>
    /// <param name="host">The host the view works for.</param>
    /// <remarks>
    /// The host goes in at birth, not later. A view that waits for a wiring
    /// step someone may forget is a column that looks finished and does
    /// nothing: built once, wired once, or never built at all.
    /// </remarks>
    public FileTreePanel(FileTreeHost host) {
        ArgumentNullException.ThrowIfNull(host);

        Host = host;
        View = new FileTreeView(host);
    }

    /// <summary>The host the view is bound to, from its first moment.</summary>
    public FileTreeHost Host { get; }

    /// <inheritdoc />
    public string Id => "file-tree.explorer";

    /// <inheritdoc />
    public string Title => "Files";

    /// <inheritdoc />
    public string? Glyph => null;

    /// <inheritdoc />
    public string? Icon => "folder-tree";

    /// <inheritdoc />
    public Control View { get; }
}
