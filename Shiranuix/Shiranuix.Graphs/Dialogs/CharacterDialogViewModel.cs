using CommunityToolkit.Mvvm.ComponentModel;
namespace Shiranuix.Graphs.Dialogs;

/// <summary>
/// The character dialog's own state: what the wiki knows about a character,
/// kept beside the canvas.
/// </summary>
/// <remarks>
/// The four fields are the wiki's own: a character is kept in the wiki, and
/// what the writer knows about them is kept there too — so the dialog that
/// changes them changes the wiki, and the map standing open beside it draws
/// the new name at once.
/// </remarks>
public sealed partial class CharacterDialogViewModel : ObservableObject {
    /// <summary>Creates the state the dialog opens on.</summary>
    /// <param name="initial">The account to open on, or nothing for an empty box.</param>
    /// <param name="error">An earlier refusal to show, or nothing.</param>
    public CharacterDialogViewModel(WikiCharacterDraft? initial, string? error) {
        Name = initial?.Name ?? string.Empty;
        BirthDate = initial?.BirthDate ?? string.Empty;
        Description = initial?.Description ?? string.Empty;
        Note = initial?.Note ?? string.Empty;
        Error = error;
    }

    /// <summary>The character's name.</summary>
    [ObservableProperty]
    private string _name;

    /// <summary>The day they were born, as the writer writes it.</summary>
    [ObservableProperty]
    private string _birthDate;

    /// <summary>What the writer says about them.</summary>
    [ObservableProperty]
    private string _description;

    /// <summary>Whatever else is worth keeping of them.</summary>
    [ObservableProperty]
    private string _note;

    /// <summary>The last refusal to show, or nothing while there is none.</summary>
    [ObservableProperty]
    private string? _error;

    /// <summary>What the dialog answers with, as it stands.</summary>
    public WikiCharacterDraft Draft => new(Name, BirthDate, Description, Note);
}
