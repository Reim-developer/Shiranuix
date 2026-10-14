using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Shiranuix.Views;

/// <summary>
/// Reports whether an editor is being typed into, so a screen drawn over it can
/// get out of the way.
/// </summary>
/// <remarks>
/// The report exists because of the input method. A Vietnamese or Japanese
/// writer composes their character first, and while it is being composed it is
/// drawn in the control but is not yet in the text - "the buffer is empty" is
/// not the same question as "the user has started", and the start screen needs
/// the second one. Focus is the other half: clicking into a page to begin
/// writing should not leave the invitation sitting on top of it.
///
/// This is a provider: the view knows all this about itself, and it reports it
/// out rather than the view model reaching in for it.
/// </remarks>
public static class EditorActivation {
    /// <summary>Whether the editor is being typed into.</summary>
    public static readonly AttachedProperty<bool> IsActiveProperty =
        AvaloniaProperty.RegisterAttached<TextBox, bool>("IsActive", typeof(EditorActivation));

    /// <summary>Gets whether the given editor is active.</summary>
    /// <param name="editor">The editor.</param>
    /// <returns>True when it is being typed into.</returns>
    public static bool GetIsActive(TextBox editor) => editor.GetValue(IsActiveProperty);

    /// <summary>Sets whether the given editor is active.</summary>
    /// <param name="editor">The editor.</param>
    /// <param name="isActive">The state to set it to.</param>
    public static void SetIsActive(TextBox editor, bool isActive) =>
        editor.SetValue(IsActiveProperty, isActive);

    static EditorActivation() {
        IsActiveProperty.Changed.AddClassHandler<TextBox>(OnIsActiveChanged);
    }

    private static void OnIsActiveChanged(TextBox editor, AvaloniaPropertyChangedEventArgs e) {
        if (e.NewValue is not bool isActive) {
            return;
        }

        if (isActive) {
            editor.GotFocus += OnStarted;
            editor.KeyDown += OnStarted;

            // A keypress or focus is enough on its own, so an editor that
            // arrives already focused needs no telling.
            editor.LostFocus += OnStopped;
            return;
        }

        editor.GotFocus -= OnStarted;
        editor.KeyDown -= OnStarted;
        editor.LostFocus -= OnStopped;
    }

    private static void OnStarted(object? sender, RoutedEventArgs e) {
        // Set through the property rather than the attached one, so a binding
        // on IsActive sees the change.
        if (sender is TextBox editor) {
            SetIsActive(editor, true);
        }
    }

    private static void OnStopped(object? sender, RoutedEventArgs e) {
        if (sender is TextBox editor) {
            SetIsActive(editor, false);
        }
    }
}
