using Avalonia.Controls;
using Shiranuix.Plugins;
using Shiranuix.Plugins.Commands;
using Shiranuix.Plugins.UI;

namespace FileTree;

/// <summary>
/// The tree's panel: the column down the left of the page, with the tree in it.
/// </summary>
/// <remarks>
/// The panel is thin on purpose. It builds the view, hands it the plugin, and
/// holds a tab for itself — everything a writer can do happens either in the
/// tree or through one of the plugin's commands, which is why the panel needs
/// no logic of its own to test.
/// </remarks>
internal sealed class FileTreePanel : IPluginPanel {
    private readonly FileTreeView _view;

    /// <summary>
    /// Builds the panel and the tree in it.
    /// </summary>
    /// <param name="tree">The tree to show.</param>
    /// <param name="commands">The commands the tree offers, for the keys.</param>
    public FileTreePanel(FileTreeState tree, IReadOnlyList<IPluginCommand> commands) {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(commands);

        View = new FileTreeView { DataContext = tree };
        _view = (FileTreeView)View;
        Commands = commands;
    }

    /// <summary>The commands the tree offers, so the view can reach them.</summary>
    public IReadOnlyList<IPluginCommand> Commands { get; }

    /// <inheritdoc />
    public string Id => "file-tree.explorer";

    /// <inheritdoc />
    public string Title => "Files";

    /// <inheritdoc />
    public string? Glyph => "▤";

    /// <inheritdoc />
    public Control View { get; }

    /// <summary>
    /// Hands the view the plugin it belongs to, once the plugin is built.
    /// </summary>
    /// <param name="plugin">The plugin.</param>
    public void Attach(FileTreePlugin plugin) => _view.Bind(plugin);
}
