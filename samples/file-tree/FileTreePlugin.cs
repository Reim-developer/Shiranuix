using FileTree.Commands;
using FileTree.Io;
using FileTree.Nodes;
using FileTree.State;
using Shiranuix.Plugins;
using Shiranuix.Plugins.Commands;
using Shiranuix.Plugins.Diagnostics;
using Shiranuix.Plugins.Dialogs;
using Shiranuix.Plugins.Editor;
using Shiranuix.Plugins.Messaging;
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
    /// <summary>The channel a folder goes out on, when the editor offers one.</summary>
    private IPluginEvents? _events;

    /// <summary>
    /// Where this plugin's own settings live, remembered because the folder
    /// is the plugin's rather than the handshake's: a tree standing on a folder
    /// must be able to remember it however it was asked.
    /// </summary>
    private string? _settingsFolder;

    /// <summary>The folder this plugin keeps its own settings in.</summary>
    private string SettingsFolder => _settingsFolder ?? Self.Directory;
    private FileTreePanel? _panel;
    private IDisposable? _watching;

    /// <summary>
    /// The tree itself, and everything it knows.
    /// </summary>
    public FileTreeState Tree { get; } = new();

    /// <summary>
    /// Creates the plugin over its own tree: the commands it offers, and the
    /// wiring that tells them when what they need has moved.
    /// </summary>
    /// <remarks>
    /// Both belong to the plugin rather than to its handshake with the editor.
    /// A command that needs a row selected cannot answer until a row is
    /// selected, and nothing about starting up changes that - so a tree that
    /// is only wired once the editor says hello would have commands that
    /// answer wrongly for as long as nobody said hello.
    ///
    /// The marks are a collection, so marking a row raises a collection
    /// change rather than a property change: watching only the latter is how
    /// "delete marked" stays greyed while things are marked.
    /// </remarks>
    public FileTreePlugin() {
        Commands = TreeCommands.All(this);

        Tree.PropertyChanged += (_, e) => {
            if (e.PropertyName is nameof(FileTreeState.Selected)) {
                SayWhatTheyNeedMoved();
            }
        };

        Tree.Marked.CollectionChanged += (_, _) => SayWhatTheyNeedMoved();
    }

    /// <summary>
    /// Creates the plugin over a channel it already has, which is how a test
    /// stands a tree on a folder and then reads what was announced.
    /// </summary>
    /// <param name="channel">Where a folder goes, or null for a host with no
    /// channel at all.</param>
    internal FileTreePlugin(IPluginEvents? channel, string settingsFolder)
        : this() {
        _events = channel;
        _settingsFolder = settingsFolder;
    }

    private void SayWhatTheyNeedMoved() {
        foreach (var command in Commands.OfType<TreeCommand>()) {
            command.CanRunMoved();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<IPluginCommand> Commands { get; }

    /// <inheritdoc />
    /// <remarks>
    /// Nothing is offered until the tree has somewhere to stand. A side bar
    /// with nothing in it - a heading with no folder on it, a box to type in, a
    /// row of buttons that do nothing yet - is a column of furniture beside the
    /// page, and the editor is not a document browser until it has been asked
    /// to be one.
    /// </remarks>
    public IReadOnlyList<IPluginPanel> Panels =>
        _panel is not null && Tree.HasRoot ? [_panel] : [];

    /// <inheritdoc />
    public override async Task InitializeAsync(
        IPluginContext context,
        CancellationToken cancellationToken) {
        await base.InitializeAsync(context, cancellationToken);

        _editor = context.Capability<IEditorHost>();
        _dialogs = context.Capability<IPluginDialog>();

        _panel = new FileTreePanel(Tree, Commands);

        var remembered = TreeSettings.Read(SettingsFolder);
        var standingOn = remembered ?? FolderOfTheDocument();

        if (standingOn is not null) {
            await Tree.OpenRootAsync(standingOn, cancellationToken);
            TreeSettings.Write(SettingsFolder, standingOn);
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

        // A folder is a thing plugins announce rather than ask each other about:
        // this one is standing on it, and anything else with work to do with
        // folders - a search across files, a project view - hears about it on
        // the same kind of news as a document changing, because it is the
        // same sort of thing. A place to stand.
        _events = context.Capability<IPluginEvents>();
        _settingsFolder = Self.Directory;
    }

    /// <inheritdoc />
    public override async Task ShutdownAsync(CancellationToken cancellationToken) {
        _events = null;
        _watching?.Dispose();
        _watching = null;

        if (_editor is not null) {
            _editor.DocumentChanged -= OnDocumentChanged;
        }

        _panel = null;
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

        var outcome = await StandOnAsync(folder, cancellationToken);
        if (outcome.IsSuccess) {
            return PluginOutcome<string>.From(folder);
        }

        return PluginOutcome<string>.Failure(outcome.Error ?? "The tree did not move.");
    }

    /// <summary>
    /// Stands the tree on a folder and starts watching it, wherever the request
    /// came from - a document opened, a folder chosen in the heading, or the
    /// same command typed into the palette.
    /// </summary>
    /// <param name="folder">The folder to stand on.</param>
    /// <param name="cancellationToken">Cancelled when the editor is closing.</param>
    /// <returns>The outcome.</returns>
    public async Task<PluginOutcome> StandOnAsync(string folder, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(folder);

        var opened = await Tree.OpenRootAsync(folder, cancellationToken);
        if (!opened.IsSuccess) {
            return PluginOutcome.Failure(opened.Error ?? "The tree did not move.");
        }

        TreeSettings.Write(SettingsFolder, folder);
        Watch(folder);

        // Said to whoever is listening, because a folder is not only this
        // tree's business: it is news on the same channel as a document
        // changing, and anyone who cares is better off hearing it than asking
        // later whether there is one.
        _events?.Publish(new FolderOpened(folder));

        return PluginOutcome.Ok();
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
    /// Moves a file or folder into another folder, after the editor has asked
    /// whether that is what was meant.
    /// </summary>
    /// <param name="node">What is about to go.</param>
    /// <param name="intoFolder">The folder to put it in.</param>
    /// <param name="cancellationToken">Cancelled when the editor is closing.</param>
    /// <returns>The outcome.</returns>
    /// <remarks>
    /// Moving within the folder it is already in is not asked about - there is
    /// nothing to be surprised by - but moving anywhere else is, because a file
    /// that is no longer where it was is a file the writer has to go looking
    /// for. The destination is never overwritten: a name already taken there
    /// is a refusal, not a replacement.
    /// </remarks>
    public async Task<PluginOutcome> MoveAsync(
        FileNode node,
        string intoFolder,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(intoFolder);

        var from = node.Parent?.FullPath;
        var changingPlaces = from is null
            || !string.Equals(from, intoFolder, StringComparison.OrdinalIgnoreCase);

        if (changingPlaces && _dialogs is not null) {
            var where = Path.GetFileName(intoFolder.TrimEnd(Path.DirectorySeparatorChar));
            var what = node.IsDirectory
                ? $"the folder '{node.Name}' and everything in it"
                : $"'{node.Name}'";

            var agreed = await _dialogs.ConfirmAsync(
                new PluginConfirm($"Move {what} into '{where}'?", "Move", "Move", "Keep"),
                cancellationToken);

            if (!agreed.IsSuccess || !agreed.Value) {
                return PluginOutcome.Failure("Nothing was moved.");
            }
        }

        var outcome = FileOperations.Move(node, intoFolder);
        if (outcome.IsSuccess) {
            Tree.Status = $"Moved {node.Name}.";
            await Tree.RefreshAsync(cancellationToken);

            return PluginOutcome.Ok();
        }

        return PluginOutcome.Failure(outcome.Error ?? "That did not move.");
    }

    /// <summary>
    /// Asks which folder to move something into, and moves it there.
    /// </summary>
    /// <param name="node">What is about to go.</param>
    /// <param name="cancellationToken">Cancelled when the editor is closing.</param>
    /// <returns>The outcome.</returns>
    public async Task<PluginOutcome> ChooseWhereToMoveAsync(
        FileNode node,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(node);

        var chosen = await ChooseFolderAsync(cancellationToken);
        if (!chosen.IsSuccess) {
            return PluginOutcome.Failure(chosen.Error ?? "No folder was chosen.");
        }

        return await MoveAsync(node, chosen.Value, cancellationToken);
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
        if (TreeSettings.Read(SettingsFolder) is { } chosen) {
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
