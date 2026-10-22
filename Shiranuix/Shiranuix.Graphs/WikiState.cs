using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shiranuix.Graphs.Dialogs;
using Shiranuix.Graphs.Persistence;

namespace Shiranuix.Graphs;

/// <summary>
/// The wiki: the cast of every story, kept once however many maps they
/// stand in — and the four fields the writer knows each of them by.
/// </summary>
/// <remarks>
/// <para>
/// The state is the wiki's own memory of what the writer has told it, and
/// nothing more: the list of characters on the left, the account of the
/// chosen one on the right, and the two commands that put a character into
/// the wiki or take them out of it again.
/// </para>
/// <para>
/// Taking a character out is the one thing the wiki asks about first,
/// because a character in the wiki is a character in every map that has
/// them standing in it: the writer is told how many nodes go with them,
/// and only then is the question answered.
/// </para>
/// </remarks>
public sealed partial class WikiState : ObservableObject {
    private readonly IWikiStore _store;
    private readonly IConfirmQuestion _confirm;

    /// <summary>
    /// Creates the state over the wiki it writes through and the prompt it
    /// asks with.
    /// </summary>
    /// <param name="store">Where the characters are kept.</param>
    /// <param name="confirm">Asks the one yes/no question.</param>
    public WikiState(IWikiStore store, IConfirmQuestion confirm) {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _confirm = confirm ?? throw new ArgumentNullException(nameof(confirm));

        Load();
    }

    /// <summary>Every character in the wiki, by name.</summary>
    [ObservableProperty]
    private IReadOnlyList<WikiCharacterRow> _characters = [];

    /// <summary>
    /// The character the list has chosen: the one the form is showing and
    /// the one a save changes.
    /// </summary>
    [ObservableProperty]
    private WikiCharacterRow? _selected;

    /// <summary>What the character is called.</summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>The day they were born, as the writer writes it.</summary>
    [ObservableProperty]
    private string _birthDate = string.Empty;

    /// <summary>What the writer says about them.</summary>
    [ObservableProperty]
    private string _description = string.Empty;

    /// <summary>Whatever else is worth keeping of them.</summary>
    [ObservableProperty]
    private string _note = string.Empty;

    /// <summary>What refused the last save, if anything did.</summary>
    [ObservableProperty]
    private string? _error;

    /// <summary>Whether the wiki holds anybody at all.</summary>
    public bool IsEmpty => Characters.Count == 0;

    /// <summary>
    /// The window the wiki's own question stands over: the wiki's window,
    /// which the service hands over once it is standing.
    /// </summary>
    public Window? Owner { get; set; }

    /// <summary>
    /// The form shows the character the list has chosen — and a character
    /// who is not chosen leaves the form empty, ready for a new one.
    /// </summary>
    /// <param name="value">The character the list has chosen.</param>
    partial void OnSelectedChanged(WikiCharacterRow? value) {
        Name = value?.Name ?? string.Empty;
        BirthDate = value?.BirthDate ?? string.Empty;
        Description = value?.Description ?? string.Empty;
        Note = value?.Note ?? string.Empty;
        Error = null;
    }

    /// <summary>
    /// Reads the wiki afresh: what it holds now, rather than what it held
    /// when the window opened.
    /// </summary>
    public void Load() {
        Characters = _store.ListCharacters();

        if (Selected is { } chosen) {
            Selected = Characters.FirstOrDefault(character => character.Id == chosen.Id);
        }
    }

    /// <summary>
    /// A new character, waiting to be written: the form empties and the
    /// wiki is asked for nothing yet.
    /// </summary>
    [RelayCommand]
    private void NewCharacter() {
        Selected = null;
        Error = null;
    }

    /// <summary>
    /// Puts what the form says into the wiki — the character chosen, or a
    /// new one when the list has chosen nobody.
    /// </summary>
    [RelayCommand]
    private void Save() {
        var trimmed = Name.Trim();
        if (trimmed.Length == 0) {
            Error = "A character needs a name.";
            return;
        }

        Error = null;

        if (Selected is { } character) {
            _store.UpdateCharacter(
                character.Id,
                trimmed,
                BirthDate.Trim(),
                Description.Trim(),
                Note.Trim());
        } else {
            _store.CreateCharacter(
                trimmed,
                BirthDate.Trim(),
                Description.Trim(),
                Note.Trim());
        }

        Load();
    }

    /// <summary>
    /// Takes the chosen character out of the wiki — after saying how many
    /// nodes will go with them, because a character in the wiki is a
    /// character in every map that has them standing in it.
    /// </summary>
    [RelayCommand]
    private async Task DeleteAsync() {
        if (Selected is not { } character || Owner is not { } owner) {
            return;
        }

        var standings = _store.CountStandings(character.Id);
        var message = standings == 0
            ? $"Take {character.Name} out of the wiki?"
            : $"Take {character.Name} out of the wiki? {standings} node(s) in the maps will go with them.";

        if (!await _confirm.Ask(owner, "Wiki", message, "Delete")) {
            return;
        }

        _store.DeleteCharacter(character.Id);
        Selected = null;
        Load();
    }
}
