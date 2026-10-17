using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shiranuix.Services;

namespace Shiranuix.ViewModels;

/// <summary>
/// The command palette: the one box a writer types a command's name into.
/// </summary>
/// <remarks>
/// <para>
/// The palette exists because a command with no door cannot be reached. The
/// keyboard answers to chords, and a chord is a thing a writer has to know in
/// advance. Typing <c>:open-folder</c> is that asking — the leading colon is
/// the same one a writer already types before a command in half the editors
/// on this machine, and it is optional here too: what matters is the name
/// after it.
/// </para>
/// <para>
/// The list is read fresh each time the box opens. Rows are ranked by what
/// was typed rather than filtered out and shown in whatever order they
/// arrived: the command whose id is exactly what was typed comes first, then
/// ones that start with it, then ones whose name does.
/// </para>
/// </remarks>
public sealed partial class PaletteState : ObservableObject {
    private readonly Func<EditorViewModel> _editor;

    /// <summary>
    /// Builds the palette over the editor it belongs to.
    /// </summary>
    /// <param name="editor">The editor a run command is asked of.</param>
    public PaletteState(Func<EditorViewModel> editor) {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
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

        // A row the command says it cannot run is not run: the row is there to
        // be found, and Enter on a dimmed row should do the same nothing that
        // clicking it does - which is to say nothing at all, without pretending.
        if (!row.CanRun) {
            return;
        }

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
    /// Every command the box could offer right now: the editor's own, fresh
    /// from the table it is read from.
    /// </summary>
    /// <returns>The rows, unranked.</returns>
    private IEnumerable<PaletteEntry> All() {
        foreach (var command in EditorCommands.All) {
            var editor = _editor();
            yield return new PaletteEntry(
                command.Id, command.Label, command.Description, "editor", command.Gesture,
                () => command.Run(editor));
        }
    }

    /// <summary>The highlight moved: only one row stands out at a time.</summary>
    /// <param name="value">Which row is standing out now.</param>
    partial void OnHighlightedChanged(int value) {
        for (var i = 0; i < Entries.Count; i++) {
            Entries[i].IsHighlighted = i == value;
        }
    }
}
