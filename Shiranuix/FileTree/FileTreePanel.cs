using Avalonia.Controls;
using Shiranuix.FileTree.Commands;
using Shiranuix.FileTree.Nodes;
using Shiranuix.FileTree.State;
using Shiranuix.FileTree.Views;
using Shiranuix.ViewModels;

namespace Shiranuix.FileTree;

/// <summary>
/// The tree's panel: the column down the left of the page, with the tree in it.
/// </summary>
/// <remarks>
/// The panel is thin on purpose. It builds the view, hands it the tree, and
/// holds a tab for itself — everything a writer can do happens either in the
/// tree or through one of the tree's commands, which is why the panel needs
/// no logic of its own to test.
/// </remarks>
internal sealed class FileTreePanel : IPanel {
    private readonly FileTreeView _view;

    /// <summary>
    /// Builds the panel and the tree in it.
    /// </summary>
    /// <param name="tree">The tree to show.</param>
    /// <param name="commands">The commands the tree offers, for the keys.</param>
    public FileTreePanel(FileTreeState tree, IReadOnlyList<TreeCommand> commands) {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(commands);

        View = new FileTreeView { DataContext = tree };
        _view = (FileTreeView)View;
        Commands = commands;
    }

    /// <summary>The commands the tree offers, so the view can reach them.</summary>
    public IReadOnlyList<TreeCommand> Commands { get; }

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

    /// <summary>
    /// Hands the view the host it belongs to.
    /// </summary>
    /// <param name="host">The tree's host.</param>
    public void Attach(FileTreeHost host) => _view.Bind(host);
}
