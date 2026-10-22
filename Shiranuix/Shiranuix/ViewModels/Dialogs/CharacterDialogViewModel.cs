using CommunityToolkit.Mvvm.ComponentModel;
using Shiranuix.Services.Dialogs;

namespace Shiranuix.ViewModels.Dialogs;

/// <summary>
/// The character dialog's own state: the name the node is drawn under and
/// the writer's note about them, kept beside the canvas.
/// </summary>
public sealed partial class CharacterDialogViewModel : ObservableObject {
    /// <summary>Creates the state the dialog opens on.</summary>
    /// <param name="name">The name to open on, or nothing for an empty box.</param>
    /// <param name="note">The note to open on, or nothing.</param>
    /// <param name="error">An earlier refusal to show, or nothing.</param>
    public CharacterDialogViewModel(string? name, string? note, string? error) {
        Name = name ?? string.Empty;
        Note = note ?? string.Empty;
        Error = error;
    }

    /// <summary>The character's name.</summary>
    [ObservableProperty]
    private string _name;

    /// <summary>What the writer wants to remember about them.</summary>
    [ObservableProperty]
    private string _note;

    /// <summary>The last refusal to show, or nothing while there is none.</summary>
    [ObservableProperty]
    private string? _error;

    /// <summary>What the dialog answers with, as it stands.</summary>
    public CharacterDraft Draft => new(Name, Note);
}
