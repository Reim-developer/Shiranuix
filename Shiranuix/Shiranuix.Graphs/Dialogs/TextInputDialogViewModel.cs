using CommunityToolkit.Mvvm.ComponentModel;

namespace Shiranuix.Graphs.Dialogs;

/// <summary>
/// The one-field dialog's state: a title, the word to ask for, and any
/// refusal the asking has already had.
/// </summary>
/// <remarks>
/// One shape for every question that is only a word — a graph's name, the
/// words along a tie —, so the writer meets one dialog rather than three
/// that look like each other.
/// </remarks>
public sealed partial class TextInputDialogViewModel : ObservableObject {
    /// <summary>Creates the state the dialog opens on.</summary>
    /// <param name="title">What the window says above the field.</param>
    /// <param name="fieldLabel">What the box itself is called.</param>
    /// <param name="initial">The word to open on, or nothing for an empty box.</param>
    /// <param name="error">An earlier refusal to show, or nothing.</param>
    public TextInputDialogViewModel(string title, string fieldLabel, string? initial, string? error) {
        Title = title ?? throw new ArgumentNullException(nameof(title));
        FieldLabel = fieldLabel ?? throw new ArgumentNullException(nameof(fieldLabel));
        Text = initial ?? string.Empty;
        Error = error;
    }

    /// <summary>What the window says.</summary>
    [ObservableProperty]
    private string _title;

    /// <summary>What the box is called.</summary>
    [ObservableProperty]
    private string _fieldLabel;

    /// <summary>The word in the box.</summary>
    [ObservableProperty]
    private string _text;

    /// <summary>The last refusal to show, or nothing while there is none.</summary>
    [ObservableProperty]
    private string? _error;
}
