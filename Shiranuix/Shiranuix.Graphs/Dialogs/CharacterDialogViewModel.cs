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
        Avatar = initial?.Avatar;
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

    /// <summary>
    /// How much of the description the writer has written — told of while
    /// they write it, so that the end of it is never a surprise.
    /// </summary>
    public int DescriptionLength => Description.Length;

    /// <summary>
    /// The description changed, and the count of it changed with it.
    /// </summary>
    /// <param name="value">What the writer has written.</param>
    partial void OnDescriptionChanged(string value) => OnPropertyChanged(nameof(DescriptionLength));

    /// <summary>How much of the note the writer has written — told of while they write it.</summary>
    public int NoteLength => Note.Length;

    /// <summary>The note changed, and the count of it changed with it.</summary>
    /// <param name="value">What the writer has written.</param>
    partial void OnNoteChanged(string value) => OnPropertyChanged(nameof(NoteLength));

    /// <summary>Whatever else is worth keeping of them.</summary>
    [ObservableProperty]
    private string _note;

    /// <summary>The last refusal to show, or nothing while there is none.</summary>
    [ObservableProperty]
    private string? _error;

    /// <summary>
    /// Their face, as the picture itself: the bytes the writer chose, or
    /// nothing for a character whose face stands for the first letter of
    /// their name.
    /// </summary>
    public byte[]? Avatar { get; set; }

    /// <summary>What the dialog answers with, as it stands.</summary>
    public WikiCharacterDraft Draft => new(Name, BirthDate, Description, Note, Avatar);
}
