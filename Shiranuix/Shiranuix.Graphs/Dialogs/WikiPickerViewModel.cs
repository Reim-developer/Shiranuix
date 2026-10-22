using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shiranuix.Graphs.Persistence;

namespace Shiranuix.Graphs.Dialogs;

/// <summary>
/// The picker's own state: the cast of every story as the wiki holds it,
/// the box the writer looks them up in, and the one they have chosen.
/// </summary>
/// <remarks>
/// <para>
/// The list is read from the wiki rather than carried in, so that a
/// character made in the wiki while the picker stands open is in the list
/// the moment it is made: the wiki says when it has heard something new,
/// and the picker listens.
/// </para>
/// <para>
/// And a wiki with nobody in it says so, rather than standing as an empty
/// box: a character in a map is a character the wiki keeps, and a writer
/// with an empty wiki has a character to make before there is anybody to
/// put in the map.
/// </para>
/// </remarks>
public sealed partial class WikiPickerViewModel : ObservableObject, IDisposable {
    private readonly IWikiStore _store;

    /// <summary>
    /// Creates the picker over the wiki it offers.
    /// </summary>
    /// <param name="store">The wiki the characters are read from.</param>
    public WikiPickerViewModel(IWikiStore store) {
        _store = store ?? throw new ArgumentNullException(nameof(store));

        _store.Changed += OnWikiChanged;
        Load();
    }

    /// <summary>
    /// Raised when the writer asks for the wiki itself: the picker cannot
    /// make a character, but it can say where characters are made.
    /// </summary>
    public event Action? WikiAsked;

    /// <summary>The characters the wiki holds, by name.</summary>
    [ObservableProperty]
    private IReadOnlyList<WikiCharacterInfo> _characters = [];

    /// <summary>The characters the search leaves standing, by name.</summary>
    [ObservableProperty]
    private IReadOnlyList<WikiCharacterInfo> _visible = [];

    /// <summary>What the writer is looking for, in a word or two.</summary>
    [ObservableProperty]
    private string _search = string.Empty;

    /// <summary>The character the writer has chosen, if any.</summary>
    [ObservableProperty]
    private WikiCharacterInfo? _selected;

    /// <summary>Whether the wiki holds anybody at all.</summary>
    public bool IsEmpty => Characters.Count == 0;

    /// <summary>
    /// The search narrows the list: a character is found by what they are
    /// called or by what the writer said about them, because a writer
    /// looking for somebody remembers one or the other.
    /// </summary>
    /// <param name="value">What the writer is looking for.</param>
    partial void OnSearchChanged(string value) => Load();

    /// <summary>
    /// Reads the wiki afresh and narrows the list by what the writer is
    /// looking for.
    /// </summary>
    private void Load() {
        Characters = [.. _store.ListCharacters()
            .Select(character => new WikiCharacterInfo(
                character.Id, character.Name, character.Description))];

        var search = Search.Trim();
        Visible = search.Length == 0
            ? Characters
            : [.. Characters.Where(character =>
                character.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                character.Description.Contains(search, StringComparison.OrdinalIgnoreCase))];

        if (Selected is { } chosen && Visible.All(character => character.Id != chosen.Id)) {
            Selected = null;
        }

        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>
    /// The wiki has heard something new, and the picker shows it at once.
    /// </summary>
    private void OnWikiChanged() => Load();

    /// <summary>Asks for the wiki itself, where characters are made.</summary>
    [RelayCommand]
    private void OpenWiki() => WikiAsked?.Invoke();

    /// <summary>Lets go of the wiki, so that the picker does not outlive itself in its memory.</summary>
    public void Dispose() => _store.Changed -= OnWikiChanged;
}
