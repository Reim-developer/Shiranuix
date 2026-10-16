using Shiranuix.Plugins;
using Shiranuix.Plugins.Commands;
using Shiranuix.Plugins.Diagnostics;
using Shiranuix.Plugins.Dialogs;
using Shiranuix.Plugins.Editor;
using Shiranuix.Plugins.UI;

namespace FileTree;

/// <summary>
/// The file tree the editor loads: a column down the left of the page, a set
/// of commands, and a folder the tree stands on.
/// </summary>
/// <remarks>
/// <para>
/// Where the tree stands is the one decision that belongs to the plugin rather
/// than to the writer: the folder of the document they have open, or the
/// folder they chose with the open-folder command, remembered between runs.
/// The writer never has to set a workspace before they can look at their own
/// files.
/// </para>
/// <para>
/// Everything the tree does to a document goes through the editor's own door,
/// so opening a file from the tree lands in the undo history and asks about
/// unsaved work exactly as if the writer had opened it themselves.
/// </para>
/// </remarks>
public sealed class FileTreePlugin
    : PluginBase<FileTreePlugin>, IPluginCommandProvider, IPluginPanelProvider {
    private IEditorHost? _editor;
    private IPluginDialog? _dialogs;
    private FileTreePanel? _panel;
    private IDisposable? _watching;

    /// <summary>
    /// The tree itself, and everything it knows.
    /// </summary>
    public FileTreeState Tree { get; } = new();

    /// <inheritdoc />
    public IReadOnlyList<IPluginCommand> Commands { get; private set; } = [];

    /// <inheritdoc />
    public IReadOnlyList<IPluginPanel> Panels => _panel is null ? [] : [_panel];

    /// <inheritdoc />
    public override async Task InitializeAsync(
        IPluginContext context,
        CancellationToken cancellationToken) {
        await base.InitializeAsync(context, cancellationToken);

        _editor = context.Capability<IEditorHost>();
        _dialogs = context.Capability<IPluginDialog>();
        Tree.PropertyChanged += (_, e) => { _ = e; };

        Commands = TreeCommands.All(this);
        _panel = new FileTreePanel(Tree, Commands);

        var remembered = TreeSettings.Read(Self.Directory);
        var standingOn = remembered ?? FolderOfTheDocument();

        if (standingOn is not null) {
            await Tree.OpenRootAsync(standingOn, cancellationToken);
            TreeSettings.Write(Self.Directory, standingOn);
            Watch(standingOn);
        } else {
            Log.Write(
                PluginLogLevel.Information,
                "No folder to stand on yet; open a document or use Open folder.");
        }

        // A tree that follows the writer is a tree that is already looking at
        // the right place when the next document opens.
        if (_editor is not null) {
            _editor.DocumentChanged += OnDocumentChanged;
        }
    }

    /// <inheritdoc />
    public override async Task ShutdownAsync(CancellationToken cancellationToken) {
        _watching?.Dispose();
        _watching = null;

        if (_editor is not null) {
            _editor.DocumentChanged -= OnDocumentChanged;
        }

        _panel = null;
        Commands = [];
        await base.ShutdownAsync(cancellationToken);
    }

    /// <summary>
    /// Asks the writer where the tree should stand, and stands it there.
    /// </summary>
    /// <param name="cancellationToken">Cancelled when the editor is closing.</param>
    /// <returns>The outcome, and the folder chosen.</returns>
    public async Task<PluginOutcome<string>> ChooseFolderAsync(CancellationToken cancellationToken) {
        if (_dialogs is null) {
            return PluginOutcome<string>.Failure("This editor cannot ask where a folder is.");
        }

        var chosen = await _dialogs.PickFolderAsync(
            new PluginFolderPrompt("Where should the tree stand?"), cancellationToken);

        if (!chosen.IsSuccess || chosen.Value is not { } folder) {
            return PluginOutcome<string>.Failure(
                chosen.Error ?? "No folder was chosen, so the tree did not move.");
        }

        var outcome = await Tree.OpenRootAsync(folder, cancellationToken);
        if (outcome.IsSuccess) {
            TreeSettings.Write(Self.Directory, folder);
            Watch(folder);
        }

        return outcome;
    }

    /// <summary>
    /// Opens a file in the editor, asking the writer's own questions on the way.
    /// </summary>
    /// <param name="path">The file to open.</param>
    /// <param name="cancellationToken">Cancelled when the editor is closing.</param>
    /// <returns>The outcome.</returns>
    public async Task<PluginOutcome> OpenFileAsync(
        string path,
        CancellationToken cancellationToken) {
        if (_editor is null) {
            return PluginOutcome.Failure("This editor offers no document to open a file into.");
        }

        var outcome = await _editor.OpenFileAsync(path, cancellationToken);
        Tree.Status = outcome.IsSuccess ? $"Opened {Path.GetFileName(path)}." : outcome.Error ?? string.Empty;

        return outcome;
    }

    /// <summary>
    /// Asks the writer before anything is deleted, in the editor's own voice.
    /// </summary>
    /// <param name="node">What is about to go.</param>
    /// <returns>The outcome.</returns>
    public async Task<PluginOutcome> DeleteAsync(FileNode node, CancellationToken cancellationToken) {
        if (_dialogs is null) {
            return PluginOutcome.Failure("This editor cannot ask about deleting things.");
        }

        var what = node.IsDirectory
            ? $"the folder '{node.Name}' and everything in it"
            : $"'{node.Name}'";

        var agreed = await _dialogs.ConfirmAsync(
            new PluginConfirm($"Delete {what}?", "Delete", "Delete", "Keep"), cancellationToken);

        if (!agreed.IsSuccess || !agreed.Value) {
            return PluginOutcome.Failure("Nothing was deleted.");
        }

        var outcome = FileOperations.Delete(node);
        if (outcome.IsSuccess) {
            Tree.Status = $"Deleted {node.Name}.";
            await Tree.RefreshAsync(cancellationToken);
        }

        return outcome;
    }

    /// <summary>
    /// Gives a file or folder another name, and reads the tree again so it
    /// cannot show the old one for a moment longer than it exists.
    /// </summary>
    /// <param name="node">What to rename.</param>
    /// <param name="name">What to call it instead.</param>
    /// <param name="cancellationToken">Cancelled when the editor is closing.</param>
    /// <returns>The outcome.</returns>
    public async Task<PluginOutcome> RenameAsync(
        FileNode node,
        string name,
        CancellationToken cancellationToken) {
        var renamed = FileOperations.Rename(node, name);
        if (renamed.IsSuccess) {
            Tree.Status = $"Renamed to {name}.";
            await Tree.RefreshAsync(cancellationToken);
        } else {
            Tree.Status = renamed.Error ?? string.Empty;
        }

        return renamed.Plain();
    }

    private string? FolderOfTheDocument() {
        var path = _editor?.Snapshot.Path;

        return path is null ? null : Path.GetDirectoryName(path);
    }

    private void OnDocumentChanged(PluginDocumentEventArgs news) {
        if (news.Kind != PluginDocumentEvent.Opened || news.Snapshot.Path is not { } path) {
            return;
        }

        // Following the document is the tree's job; choosing a folder is the
        // writer's, and a chosen folder is not overridden by opening a file.
        if (TreeSettings.Read(Self.Directory) is { } chosen) {
            return;
        }

        var folder = Path.GetDirectoryName(path);
        if (folder is not null && !string.Equals(folder, Tree.Root?.FullPath, StringComparison.OrdinalIgnoreCase)) {
            _ = Tree.OpenRootAsync(folder);
        }
    }

    /// <summary>
    /// Watches a folder, so the tree keeps up with what the disk is doing.
    /// </summary>
    /// <param name="root">The folder to watch.</param>
    private void Watch(string root) {
        _watching?.Dispose();

        try {
            var watcher = new FileSystemWatcher(root) {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
            };

            // The tree is re-read rather than patched: a watcher knows that
            // something happened, not what the folder now holds, and a tree
            // that guesses is a tree that lies.
            watcher.Created += (_, _) => _ = Tree.RefreshAsync();
            watcher.Deleted += (_, _) => _ = Tree.RefreshAsync();
            watcher.Renamed += (_, _) => _ = Tree.RefreshAsync();
            watcher.EnableRaisingEvents = true;

            _watching = watcher;
        } catch (Exception error) when (error is IOException or UnauthorizedAccessException) {
            Log.Write(
                PluginLogLevel.Warning,
                $"'{root}' cannot be watched, so the tree will only change when asked: {error.Message}");
        }
    }
}
