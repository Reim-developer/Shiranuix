using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using Shiranuix;
using Shiranuix.FileTree.Commands;
using Shiranuix.FileTree.Io;
using Shiranuix.FileTree.Nodes;
using Shiranuix.FileTree.State;
using Shiranuix.Services;
using Shiranuix.Services.Dialogs;
using Shiranuix.Utilities;
using Shiranuix.ViewModels;

namespace Shiranuix.FileTree;

/// <summary>
/// The file tree the editor loads: a column down the left of the page, a set
/// of commands, and a folder the tree stands on.
/// </summary>
/// <remarks>
/// <para>
/// The tree is part of the editor now, not something a writer installs. The
/// document, the folder picker and the column it is drawn in are handed to it
/// by the editor when it is built - so nothing about it can be loaded,
/// refused or switched off from outside.
/// </para>
///
/// <para>
/// Where the tree stands is the one decision that belongs to the tree rather
/// than to the writer: the folder of the document they have open, or the
/// folder they chose, remembered between runs. The writer never has to set a
/// workspace before they can look at their own files.
/// </para>
/// <para>
/// Everything the tree does to a document goes through the editor's own door,
/// so opening a file from the tree lands in the undo history and asks about
/// unsaved work exactly as if the writer had opened it themselves.
/// </para>
/// </remarks>
public sealed class FileTreeHost {
    private readonly IFileDialogService _dialogs;
    private readonly IDialogService _confirm;
    private readonly EditorViewModel _editor;
    private readonly ILogger _log;

    private IDisposable? _watching;
    private FileTreePanel? _panel;

    /// <summary>Whether a re-read has already been asked for and not run yet.</summary>
    /// <remarks>
    /// A one, not a bool: several threads may ask together, and only the first
    /// is answered; the thread that runs the work lets go of the switch.
    /// </remarks>
    private int _readPending;

    /// <summary>
    /// Creates the tree over the editor's own doors.
    /// </summary>
    /// <param name="dialogs">Where a folder is picked from.</param>
    /// <param name="confirm">Asked before anything is deleted or moved.</param>
    /// <param name="editor">The document, and the questions asked of it.</param>
    /// <param name="log">Where the tree writes about itself.</param>
    public FileTreeHost(
        IFileDialogService dialogs,
        IDialogService confirm,
        EditorViewModel editor,
        ILoggerFactory log) {
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _confirm = confirm ?? throw new ArgumentNullException(nameof(confirm));
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _log = (log ?? throw new ArgumentNullException(nameof(log)))
            .CreateLogger<FileTreeHost>();

        Commands = TreeCommands.All(this);

        Tree.PropertyChanged += (_, e) => {
            if (e.PropertyName is nameof(FileTreeState.Selected)) {
                SayWhatTheyNeedMoved();
            }
        };

        Tree.Marked.CollectionChanged += (_, _) => SayWhatTheyNeedMoved();
    }

    /// <summary>The tree itself, and everything it knows.</summary>
    public FileTreeState Tree { get; } = new();

    /// <summary>The commands the tree offers, in the order the palette lists them.</summary>
    internal IReadOnlyList<TreeCommand> Commands { get; }

    /// <summary>
    /// Runs one of the tree's commands by its id, and says what refused to be
    /// done on the tree's own status line.
    /// </summary>
    /// <param name="id">The command's id.</param>
    /// <returns>A task that finishes when the command has.</returns>
    public async Task RunCommandAsync(string id) {
        var command = Commands.FirstOrDefault(candidate => candidate.Info.Id == id);
        if (command is null) {
            return;
        }

        var outcome = await command.ExecuteAsync(CancellationToken.None);
        if (!outcome.IsSuccess && outcome.Error is { } said && !said.StartsWith("Unmarked")) {
            Tree.Status = said;
        }
    }

    /// <summary>
    /// The tree's panel, or null until there is somewhere to stand.
    /// </summary>
    /// <remarks>
    /// Nothing is offered while the tree has nowhere to stand. A column with
    /// nothing in it - a heading with no folder on it, buttons that do nothing
    /// yet - is a piece of furniture beside the page, and this editor is not a
    /// document browser until it has been asked to be one.
    /// </remarks>
    internal FileTreePanel? Panel => _panel is not null && Tree.HasRoot ? _panel : null;

    /// <summary>
    /// Builds the panel and stands the tree where it was left, or where the
    /// open document is.
    /// </summary>
    /// <param name="cancellationToken">Cancelled when the editor is closing.</param>
    /// <returns>A task that finishes when the tree is up.</returns>
    public async Task StartAsync(CancellationToken cancellationToken) {
        _panel = new FileTreePanel(this);

        var remembered = TreeSettings.Read();
        var standingOn = remembered ?? FolderOfTheDocument();

        if (standingOn is not null) {
            await Tree.OpenRootAsync(standingOn, cancellationToken);
            TreeSettings.Write(standingOn);
            Watch(standingOn);
            ShowPanel();
        } else {
            _log.LogInformation(
                "No folder to stand on yet; open a document or use Open folder.");
        }

        // A tree that follows the writer is a tree that is already looking at
        // the right place when the next document opens.
        _editor.PropertyChanged += OnEditorPropertyChanged;
    }

    /// <summary>
    /// Lets go of the folder the tree is watching.
    /// </summary>
    public void Stop() {
        _watching?.Dispose();
        _watching = null;
    }

    /// <summary>
    /// Puts the tree's panel into the editor's column, opened, now that there
    /// is somewhere for it to stand.
    /// </summary>
    /// <remarks>
    /// The column is opened here rather than at the window because a folder
    /// can arrive long after the window does: a writer who picks one from the
    /// palette must not find the column the tree lives in still closed.
    /// </remarks>
    private void ShowPanel() {
        if (_panel is null) {
            return;
        }

        // A panel that is already on show stays on show - standing on a
        // second folder moves the tree rather than adding a second panel.
        if (_editor.Panel.Panels.Contains(_panel)) {
            return;
        }

        _editor.Panel.Show(_panel);
    }

    /// <summary>
    /// Asks the writer where the tree should stand, and stands it there once
    /// the page has been emptied of its own work.
    /// </summary>
    /// <param name="cancellationToken">Cancelled when the editor is closing.</param>
    /// <returns>The outcome, and the folder chosen.</returns>
    /// <remarks>
    /// Standing somewhere else is a new start, not a change of scenery: the
    /// page is emptied the way the New command empties it, asked first. A
    /// writer who keeps their work keeps the whole arrangement as it is,
    /// page and tree together.
    /// </remarks>
    public async Task<Outcome<string>> ChooseFolderAsync(CancellationToken cancellationToken) {
        var chosen = await _dialogs.PickFolderAsync();

        if (string.IsNullOrWhiteSpace(chosen)) {
            return Outcome<string>.Failure("No folder was chosen, so the tree did not move.");
        }

        if (!await _editor.EmptyPageAsync()) {
            return Outcome<string>.Failure("The page kept its work, so the tree did not move.");
        }

        var outcome = await StandOnAsync(chosen, cancellationToken);
        if (outcome.IsSuccess) {
            return Outcome<string>.From(chosen);
        }

        return Outcome<string>.Failure(outcome.Error ?? "The tree did not move.");
    }

    /// <summary>
    /// Stands the tree on a folder and starts watching it, wherever the request
    /// came from - a document opened, a folder chosen, or the same command
    /// typed into the palette.
    /// </summary>
    /// <param name="folder">The folder to stand on.</param>
    /// <param name="cancellationToken">Cancelled when the editor is closing.</param>
    /// <returns>The outcome.</returns>
    public async Task<Outcome> StandOnAsync(string folder, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(folder);

        var opened = await Tree.OpenRootAsync(folder, cancellationToken);
        if (!opened.IsSuccess) {
            return Outcome.Failure(opened.Error ?? "The tree did not move.");
        }

        TreeSettings.Write(folder);
        Watch(folder);
        ShowPanel();

        return Outcome.Ok();
    }

    /// <summary>
    /// Opens a file in the editor, asking the writer's own questions on the way.
    /// </summary>
    /// <param name="path">The file to open.</param>
    /// <returns>The outcome.</returns>
    public async Task<Outcome> OpenFileAsync(string path) {
        var outcome = await _editor.OpenPathAsync(path);

        Tree.Status = outcome.Succeeded
            ? $"Opened {Path.GetFileName(path)}."
            : outcome.Detail ?? "That did not open.";

        return outcome.Succeeded
            ? Outcome.Ok()
            : Outcome.Failure(outcome.Detail ?? "That did not open.");
    }

    /// <summary>
    /// Makes what a draft row promised, with the name the writer gave it, and
    /// reads the tree again so what is on screen is what is on the disk.
    /// </summary>
    /// <param name="draft">The node the draft row was waiting under.</param>
    /// <param name="name">The name the writer typed.</param>
    /// <param name="cancellationToken">Cancelled when the editor is closing.</param>
    /// <returns>The outcome.</returns>
    /// <remarks>
    /// A name the disk refuses changes nothing: the draft stays exactly where
    /// it was, open for the writer to fix the name or turn away from it.
    /// </remarks>
    public async Task<Outcome> FinishCreateAsync(
        FileNode draft,
        string name,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(draft);

        var folder = draft.Parent?.FullPath ?? Tree.Root?.FullPath;
        if (folder is null) {
            return Outcome.Failure("The tree is not standing anywhere.");
        }

        var made = draft.IsDirectory
            ? FileOperations.CreateFolder(folder, name)
            : FileOperations.CreateFile(folder, name);

        if (!made.IsSuccess) {
            return Outcome.Failure(made.Error ?? "That did not go.");
        }

        Tree.DiscardDraft();
        await Tree.RefreshAsync(cancellationToken);
        Tree.Select(made.Value);

        if (draft.IsDirectory) {
            Tree.Status = $"Made {name}.";
            return Outcome.Ok();
        }

        return await OpenFileAsync(made.Value);
    }

    /// <summary>
    /// Asks the writer before anything is deleted, in the editor's own voice.
    /// </summary>
    /// <param name="node">What is about to go.</param>
    /// <param name="cancellationToken">Cancelled when the editor is closing.</param>
    /// <returns>The outcome.</returns>
    public async Task<Outcome> DeleteAsync(FileNode node, CancellationToken cancellationToken) {
        var what = node.IsDirectory
            ? $"the folder '{node.Name}' and everything in it"
            : $"'{node.Name}'";

        var agreed = await _confirm.ShowAsync(new DialogRequest<bool>(
            $"Delete {what}?",
            "It cannot be brought back from here.",
            [
                new DialogButton<bool>("Keep it", false, IsDefault: true, IsCancel: true),
                new DialogButton<bool>("Delete", true)
            ]));

        if (agreed != true) {
            return Outcome.Failure("Nothing was deleted.");
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
    public async Task<Outcome> MoveAsync(
        FileNode node,
        string intoFolder,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(intoFolder);

        var from = node.Parent?.FullPath;
        var changingPlaces = from is null
            || !string.Equals(from, intoFolder, StringComparison.OrdinalIgnoreCase);

        if (changingPlaces) {
            var where = Path.GetFileName(intoFolder.TrimEnd(Path.DirectorySeparatorChar));
            var what = node.IsDirectory
                ? $"the folder '{node.Name}' and everything in it"
                : $"'{node.Name}'";

            var agreed = await _confirm.ShowAsync(new DialogRequest<bool>(
                $"Move {what} into '{where}'?",
                "The original will no longer be where it was.",
                [
                    new DialogButton<bool>("Keep it", false, IsDefault: true, IsCancel: true),
                    new DialogButton<bool>("Move", true)
                ]));

            if (agreed != true) {
                return Outcome.Failure("Nothing was moved.");
            }
        }

        var outcome = FileOperations.Move(node, intoFolder);
        if (outcome.IsSuccess) {
            Tree.Status = $"Moved {node.Name}.";
            await Tree.RefreshAsync(cancellationToken);

            return Outcome.Ok();
        }

        return Outcome.Failure(outcome.Error ?? "That did not move.");
    }

    /// <summary>
    /// Asks which folder to move something into, and moves it there.
    /// </summary>
    /// <param name="node">What is about to go.</param>
    /// <param name="cancellationToken">Cancelled when the editor is closing.</param>
    /// <returns>The outcome.</returns>
    public async Task<Outcome> ChooseWhereToMoveAsync(
        FileNode node,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(node);

        var chosen = await ChooseFolderAsync(cancellationToken);
        if (!chosen.IsSuccess) {
            return Outcome.Failure(chosen.Error ?? "No folder was chosen.");
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
    public async Task<Outcome> RenameAsync(
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

    /// <summary>
    /// The folder of the open document, or null when there is none yet.
    /// </summary>
    /// <returns>The folder, or null.</returns>
    private string? FolderOfTheDocument() {
        var path = _editor.Path;

        return string.IsNullOrWhiteSpace(path)
            ? null
            : Path.GetDirectoryName(Path.GetFullPath(path));
    }

    /// <summary>
    /// A property of the document changed: when its path moves, the tree moves
    /// with it, because the folder a writer is looking at is the folder their
    /// document lives in until they say otherwise.
    /// </summary>
    /// <param name="sender">The editor.</param>
    /// <param name="e">Which thing about it changed.</param>
    private void OnEditorPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) {
        if (e.PropertyName is not nameof(EditorViewModel.Path)) {
            return;
        }

        // Following the document is the tree's job; choosing a folder is the
        // writer's, and a folder they chose is not overridden by a file.
        if (TreeSettings.Read() is not null) {
            return;
        }

        var folder = FolderOfTheDocument();
        if (folder is not null && !string.Equals(folder, Tree.Root?.FullPath, StringComparison.OrdinalIgnoreCase)) {
            _ = StandOnAsync(folder, CancellationToken.None);
        }
    }

    /// <summary>
    /// Starts noticing the folder, so a file made or taken away somewhere else
    /// shows up here.
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
            // The re-read is posted to the page's own thread rather than run
            // where the watcher hears the disk, because the watcher's thread
            // is the operating system's, not the tree's: rows erased or drawn
            // concurrently with a re-read are rows a writer can watch lie.
            watcher.Created += (_, _) => ReadAgain();
            watcher.Deleted += (_, _) => ReadAgain();
            watcher.Renamed += (_, _) => ReadAgain();
            watcher.EnableRaisingEvents = true;

            _watching = watcher;
        } catch (Exception error) when (error is IOException or UnauthorizedAccessException) {
            _log.LogWarning(
                error,
                "'{Folder}' cannot be watched, so the tree will only change when asked.",
                root);
        }
    }

    /// <summary>
    /// Asks the page's own thread to read the tree again, once, however many
    /// things the watcher heard about since the last read.
    /// </summary>
    private void ReadAgain() {
        if (Interlocked.Exchange(ref _readPending, 1) != 0) {
            return;
        }

        try {
            Dispatcher.UIThread.Post(
                () => {
                    _readPending = 0;
                    _ = Tree.RefreshAsync();
                },
                DispatcherPriority.Background);
        } catch (Exception error) {
            _log.LogError(error, "The tree could not ask the page to read itself again.");
            _readPending = 0;
        }
    }

    private void SayWhatTheyNeedMoved() {
        foreach (var command in Commands) {
            command.CanRunMoved();
        }
    }
}
