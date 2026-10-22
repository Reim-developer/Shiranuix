using CommunityToolkit.Mvvm.ComponentModel;
namespace Shiranuix.Graphs.Dialogs;

/// <summary>
/// The relationship dialog's own state: the two characters the tie joins
/// and the words drawn along it.
/// </summary>
/// <remarks>
/// The ends are picked from the graph's own cast rather than typed — a tie
/// cannot point at a name that is not there —, and the confirm stands
/// disabled until the two ends are two different characters, because a tie
/// from somebody to themselves is a line the canvas would only loop.
/// </remarks>
public sealed partial class RelationshipDialogViewModel : ObservableObject {
    private GraphNodeInfo? _from;
    private GraphNodeInfo? _to;

    /// <summary>Creates the state the dialog opens on.</summary>
    /// <param name="characters">The cast to offer as either end.</param>
    /// <param name="fromId">The end to open on, or nothing.</param>
    /// <param name="toId">The other end to open on, or nothing.</param>
    /// <param name="label">The words to open on, or nothing.</param>
    /// <param name="error">An earlier refusal to show, or nothing.</param>
    public RelationshipDialogViewModel(
        IReadOnlyList<GraphNodeInfo> characters,
        int? fromId,
        int? toId,
        string? label,
        string? error) {
        ArgumentNullException.ThrowIfNull(characters);

        Characters = characters;
        From = characters.FirstOrDefault(character => character.Id == fromId);
        To = characters.FirstOrDefault(character => character.Id == toId);
        Label = label ?? string.Empty;
        Error = error;
    }

    /// <summary>The graph's cast, as the pickers offer them.</summary>
    public IReadOnlyList<GraphNodeInfo> Characters { get; }

    /// <summary>The character the arrow points away from.</summary>
    public GraphNodeInfo? From {
        get => _from;
        set {
            if (SetProperty(ref _from, value)) {
                OnPropertyChanged(nameof(CanConfirm));
            }
        }
    }

    /// <summary>The character the arrow arrives at.</summary>
    public GraphNodeInfo? To {
        get => _to;
        set {
            if (SetProperty(ref _to, value)) {
                OnPropertyChanged(nameof(CanConfirm));
            }
        }
    }

    /// <summary>What the tie says, possibly nothing at all.</summary>
    [ObservableProperty]
    private string _label;

    /// <summary>The last refusal to show, or nothing while there is none.</summary>
    [ObservableProperty]
    private string? _error;

    /// <summary>Whether the two ends are two different characters.</summary>
    public bool CanConfirm => From is not null && To is not null && From.Id != To.Id;

    /// <summary>What the dialog answers with, or nothing while it cannot.</summary>
    public RelationshipDraft? Draft =>
        CanConfirm ? new RelationshipDraft(From!.Id, To!.Id, Label) : null;
}
