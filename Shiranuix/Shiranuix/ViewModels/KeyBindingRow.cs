using CommunityToolkit.Mvvm.ComponentModel;
using Shiranuix.Models;

namespace Shiranuix.ViewModels;

/// <summary>
/// One line of the settings window's keyboard section: the command, the
/// chord it currently answers to, and whether its shortcut is being
/// pressed for right now.
/// </summary>
/// <remarks>
/// Display only. What a chord <em>is</em> belongs to
/// <see cref="KeyBindingState"/>; this holds what a row of the settings
/// says about it and rings the view when that changes. Unassigned is an
/// ordinary state — a chord the writer has cleared leaves the label
/// standing, and the empty gesture is what the row draws its
/// "not assigned" from.
/// </remarks>
public sealed partial class KeyBindingRow : ObservableObject {
    /// <summary>What the row is capturing, when it is: the next chord is this command's.</summary>
    [ObservableProperty]
    private bool _isCapturing;

    private string _gesture;

    internal KeyBindingRow(KeyCommand command, string gesture) {
        Command = command ?? throw new ArgumentNullException(nameof(command));
        _gesture = gesture;
    }

    /// <summary>The command this row answers for.</summary>
    public KeyCommand Command { get; }

    /// <summary>The command's id, for the handlers that act on it.</summary>
    public string CommandId => Command.Id;

    /// <summary>What a person reads: the command's name in the settings.</summary>
    public string Label => Command.Label;

    /// <summary>The chord, canonical — "Ctrl+Shift+S" — or empty when unassigned.</summary>
    public string Gesture {
        get => _gesture;
        internal set {
            if (_gesture == value) {
                return;
            }

            _gesture = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsAssigned));
            OnPropertyChanged(nameof(ChipText));
        }
    }

    /// <summary>Whether the command still answers to a chord at all.</summary>
    public bool IsAssigned => Gesture.Length > 0;

    /// <summary>
    /// What the chip says: the chord as it stands, the words for having
    /// none, or the ask while a chord is being pressed for.
    /// </summary>
    /// <remarks>
    /// One text, not two swapping places: a chip whose content refuses to
    /// show is a row that reads as a label with nothing beside it.
    /// </remarks>
    public string ChipText => IsCapturing
        ? "Press a chord…"
        : IsAssigned ? Gesture : "Not assigned";

    partial void OnIsCapturingChanged(bool value) =>
        OnPropertyChanged(nameof(ChipText));
}
