using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shiranuix.Plugins.Commands;
using Shiranuix.Services;

namespace Shiranuix.ViewModels;

/// <summary>
/// The command palette: the one box a writer types a command's name into,
/// whatever side of the table the command came from.
/// </summary>
/// <remarks>
/// <para>
/// The palette exists because a command with no door cannot be reached. The
/// keyboard answers to chords, and a chord is a thing a writer has to know in
/// advance; plugins offer commands nobody has been told about, so the only way
/// to reach one is to ask for it by name. Typing <c>:open-folder</c> is that
/// asking - the leading colon is the same one a writer already types before a
/// command in half the editors on this machine, and it is optional here too:
/// what matters is the name after it.
/// </para>
/// <para>
/// The list is read fresh each time the box opens, because plugins load
/// after the window is already up and a list built at startup would never
/// learn about them. Rows are ranked by what was typed rather than filtered
/// out and shown in whatever order they arrived: the command whose id is
/// exactly what was typed comes first, then ones that start with it, then
/// ones whose name does.
/// </para>
/// </remarks>
public sealed partial class PaletteState : ObservableObject {
    private readonly Func<EditorViewModel> _editor;
    private readonly IPluginCommandSource _plugins;

    /// <summary>
    /// Builds the palette over the editor it belongs to and the place plugin
    /// commands come from.
    /// </summary>
    /// <param name="editor">The editor a run command is asked of.</param>
    /// <param name="plugins">Where plugin commands are read from, asked each
    /// time the box opens so a plugin that loaded late is still in it.</param>
    public PaletteState(
        Func<EditorViewModel> editor,
        IPluginCommandSource plugins) {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _plugins = plugins ?? throw new ArgumentNullException(nameof(plugins));

        // A plugin that finishes starting while the box is open has to turn
        // up in it without the writer having to close it and ask again: the
        // whole reason a palette is the door to plugin commands is that
        // nobody could have known they were there.
        _plugins.CommandsChanged += (_, _) => {
            if (IsOpen) {
                Refilter();
            }
        };
    }

    /// <summary>The rows the box is offering, in the order it offers them.</summary>
    public ObservableCollection<PaletteEntry> Entries { get; } = [];

    /// <summary>
    /// Whether the box is on show. The window's chord and the Escape key both
    /// speak to this and nothing else.
    /// </summary>
    [ObservableProperty]
    private bool _isOpen;

    /// <summary>
    /// What the writer has typed. A leading colon is part of the way a command
    /// is written rather than part of what is searched for, so it is stripped
    /// before anything is ranked.
    /// </summary>
    [ObservableProperty]
    private string _query = string.Empty;

    /// <summary>Which row Enter would take.</summary>
    [ObservableProperty]
    private int _highlighted;

    /// <summary>Whether anything answers what has been typed.</summary>
    public bool HasEntries => Entries.Count > 0;

    /// <summary>What the box says when nothing answers.</summary>
    public string EmptyMessage =>
        string.IsNullOrWhiteSpace(Query)
            ? "No command to run."
            : $"Nothing answers \"{Query.TrimStart(':')}\".";

    /// <summary>Whether the box should say it has nothing.</summary>
    public bool IsEmpty => !HasEntries;

    /// <summary>Opens the box, or closes it if it is already open.</summary>
    [RelayCommand]
    public void Toggle() {
        if (IsOpen) {
            Close();
            return;
        }

        Open();
    }

    /// <summary>
    /// Opens the box over a freshly read list of commands: an empty query, the
    /// first row standing out, and nothing left over from last time.
    /// </summary>
    [RelayCommand]
    public void Open() {
        Query = string.Empty;
        Highlighted = 0;
        Refilter();
        IsOpen = true;
        OnPropertyChanged(nameof(IsOpen));
    }

    /// <summary>Puts the box away, having run nothing.</summary>
    [RelayCommand]
    public void Close() {
        IsOpen = false;
        OnPropertyChanged(nameof(IsOpen));
    }

    /// <summary>Moves the highlight up a row, stopping at the top.</summary>
    [RelayCommand]
    public void Up() => Move(-1);

    /// <summary>Moves the highlight down a row, stopping at the bottom.</summary>
    [RelayCommand]
    public void Down() => Move(1);

    /// <summary>
    /// Runs the row Enter would take and puts the box away. Nothing to run is
    /// not an error: an empty box answers Enter by closing itself.
    /// </summary>
    [RelayCommand]
    public void Run() {
        if (Entries.Count == 0) {
            Close();
            return;
        }

        var row = Entries[Math.Clamp(Highlighted, 0, Entries.Count - 1)];
        Close();
        row.Run();
    }

    private void Move(int step) {
        if (Entries.Count == 0) {
            return;
        }

        var moved = Highlighted + step;
        Highlighted = moved < 0
            ? Entries.Count - 1
            : moved >= Entries.Count ? 0 : moved;
    }

    /// <summary>
    /// The query changed: rank the rows again and put the highlight at the
    /// top, because the best answer to a new question is the first row and
    /// Enter should take it.
    /// </summary>
    /// <param name="value">What the writer has typed.</param>
    partial void OnQueryChanged(string value) {
        Refilter();
        Highlighted = 0;
    }

    private void Refilter() {
        var wanted = Query.TrimStart().TrimStart(':').Trim();

        var ranked = new List<(int Rank, PaletteEntry Row)>();
        foreach (var row in All()) {
            var rank = row.Rank(wanted);
            if (rank >= 0) {
                ranked.Add((rank, row));
            }
        }

        Entries.Clear();
        foreach (var row in ranked
            .OrderBy(pair => pair.Rank)
            .ThenBy(pair => pair.Row.Label, StringComparer.OrdinalIgnoreCase)
            .Select(pair => pair.Row)) {
            Entries.Add(row);
        }

        if (Highlighted >= Entries.Count) {
            Highlighted = 0;
        }

        for (var i = 0; i < Entries.Count; i++) {
            Entries[i].IsHighlighted = i == Highlighted;
        }

        OnPropertyChanged(nameof(HasEntries));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyMessage));
    }

    /// <summary>
    /// Every command the box could offer right now: the editor's own, then
    /// whatever plugins have offered since the window opened.
    /// </summary>
    /// <returns>The rows, unranked.</returns>
    private IEnumerable<PaletteEntry> All() {
        foreach (var command in EditorCommands.All) {
            var editor = _editor();
            yield return new PaletteEntry(
                command.Id, command.Label, command.Description, "editor", command.Gesture,
                () => command.Run(editor));
        }

        foreach (var plugin in _plugins.Commands) {
            var command = plugin;
            yield return new PaletteEntry(
                command.Info.Id,
                command.Info.Label,
                command.Info.Description ?? string.Empty,
                PluginOf(command.Info.Id),
                command.Info.DefaultGesture,
                () => _ = RunPluginAsync(command));
        }
    }

    /// <summary>
    /// Runs a plugin's command and says what it said, if it refused. A command
    /// that answers with words is telling the writer why nothing happened, and
    /// the status bar is where that belongs - the box is already gone.
    /// </summary>
    /// <param name="command">The plugin's command to run.</param>
    /// <returns>A task carrying the outcome.</returns>
    private async Task RunPluginAsync(IPluginCommand command) {
        var outcome = await command.ExecuteAsync(CancellationToken.None);
        if (!outcome.IsSuccess && outcome.Error is { } said) {
            _editor().StatusMessage = said;
        }
    }

    /// <summary>
    /// The plugin an id belongs to - everything before its first dot. An id
    /// with no dot is the editor's own, which is not a thing worth printing.
    /// </summary>
    /// <param name="id">The command's id.</param>
    /// <returns>The plugin's id, or an empty string.</returns>
    private static string PluginOf(string id) {
        var dot = id.IndexOf('.', StringComparison.Ordinal);

        return dot > 0 ? id[..dot] : string.Empty;
    }

    /// <summary>The highlight moved: only one row stands out at a time.</summary>
    /// <param name="value">Which row is standing out now.</param>
    partial void OnHighlightedChanged(int value) {
        for (var i = 0; i < Entries.Count; i++) {
            Entries[i].IsHighlighted = i == value;
        }
    }
}
