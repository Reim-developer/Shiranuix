using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Shiranuix.ViewModels;

/// <summary>
/// One command as the palette offers it: what it is called, where it came
/// from, which chord it answers to, and how it is run.
/// </summary>
/// <remarks>
/// A row is a question that has already been answered - a command of the
/// editor's own, or a command some plugin offered - so it carries the way to
/// run it and nothing to decide about. The only thing about a row that
/// changes while the box is open is which row Enter would take, which is why
/// that is the only thing about a row that is watched.
/// </remarks>
public sealed partial class PaletteEntry : ObservableObject {
    /// <summary>
    /// Builds a row over a command that is already known.
    /// </summary>
    /// <param name="id">The command's id.</param>
    /// <param name="label">What a writer reads.</param>
    /// <param name="description">What it does, in a line.</param>
    /// <param name="origin">Whose command it is: the editor, or a plugin's id.</param>
    /// <param name="gesture">The chord it answers to, or null.</param>
    /// <param name="run">The work, when the row is run.</param>
    public PaletteEntry(
        string id,
        string label,
        string description,
        string origin,
        string? gesture,
        Action run) {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(run);

        Id = id;
        Label = label;
        Description = description;
        Origin = origin;
        Gesture = gesture;
        Run = run;
        Command = new RelayCommand(run);
    }

    /// <summary>The command's id, which is also what a writer types to find it.</summary>
    public string Id { get; }

    /// <summary>What the row reads.</summary>
    public string Label { get; }

    /// <summary>What the command does, in a line, beneath the name.</summary>
    public string Description { get; }

    /// <summary>Whose command this is - "editor", or the plugin's own id.</summary>
    public string Origin { get; }

    /// <summary>The chord this command answers to, or null when it has none.</summary>
    public string? Gesture { get; }

    /// <summary>Runs the command, for the row's button and for Enter.</summary>
    public Action Run { get; }

    /// <summary>The command as the view binds it.</summary>
    public IRelayCommand Command { get; }

    /// <summary>Whether there is a chord to write beside the name.</summary>
    public bool HasGesture => !string.IsNullOrWhiteSpace(Gesture);

    /// <summary>
    /// Whether this row is the one Enter would take. Only one row says yes at
    /// a time, and the box draws that row as standing out rather than
    /// selected, so moving the highlight is moving a caret through a list.
    /// </summary>
    [ObservableProperty]
    private bool _isHighlighted;

    /// <summary>
    /// How well this row answers a typed query, or -1 when it does not answer
    /// it at all.
    /// </summary>
    /// <remarks>
    /// The order is the order a writer expects from typing: the id they typed,
    /// then something starting with it, then the name they read, then a name
    /// that merely contains what they typed, and last of all a line of
    /// description. A row is never shown because its description matched and
    /// its name did not - that is how a palette becomes a search engine.
    /// </remarks>
    /// <param name="query">What the writer has typed, without its colon.</param>
    /// <returns>A rank, lower is nearer; -1 when the row does not answer.</returns>
    public int Rank(string query) {
        if (string.IsNullOrWhiteSpace(query)) {
            return 0;
        }

        if (string.Equals(Id, query, StringComparison.OrdinalIgnoreCase)) {
            return 0;
        }

        if (Id.StartsWith(query, StringComparison.OrdinalIgnoreCase)) {
            return 1;
        }

        if (Label.StartsWith(query, StringComparison.OrdinalIgnoreCase)) {
            return 2;
        }

        if (Id.Contains(query, StringComparison.OrdinalIgnoreCase)) {
            return 3;
        }

        return Label.Contains(query, StringComparison.OrdinalIgnoreCase) ? 4 : -1;
    }
}
