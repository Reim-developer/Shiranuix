using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Shiranuix.Models;

namespace Shiranuix.Views;

/// <summary>
/// Watches what an input method is composing inside an editor and hands each
/// change over to whoever asks to hear it.
/// </summary>
/// <remarks>
/// The composing character is not in the text yet: it lives in the presenter
/// that draws the page, underlined at the caret, until the input method commits
/// it — which for a Vietnamese keystroke can be a beat later, after the next
/// word's first letter. Line and column read against the page as drawn, so
/// somebody has to carry that third state across. This is the carrier, and it
/// stays in the view, where the input method is.
///
/// The presenter only exists once the template has been applied, so the watch
/// starts there; a template applied again — a theme change, say — is met by
/// leaving the old presenter behind rather than listening to it twice.
/// </remarks>
public static class EditorComposition {
    /// <summary>
    /// Reports an editor's compositions as they come and go.
    /// </summary>
    /// <param name="editor">The editor to watch.</param>
    /// <param name="report">
    /// Called with the composition as it stands — including
    /// <see cref="Composition.None"/> when it ends, so the reader is never left
    /// holding a character that has gone.
    /// </param>
    public static void Watch(TextBox editor, Action<Composition> report) {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(report);

        TextPresenter? watched = null;
        EventHandler<AvaloniaPropertyChangedEventArgs>? watching = null;

        editor.TemplateApplied += (_, e) => {
            var presenter = e.NameScope.Find<TextPresenter>("PART_TextPresenter");

            if (ReferenceEquals(presenter, watched)) {
                return;
            }

            if (watched is not null && watching is not null) {
                watched.PropertyChanged -= watching;
            }

            watched = presenter;
            watching = null;

            if (presenter is null) {
                return;
            }

            watching = (_, args) => {
                // Both halves matter: the composition itself, and where the
                // input method puts the cursor inside it — the two properties
                // the presenter draws its caret from.
                if (args.Property == TextPresenter.PreeditTextProperty ||
                    args.Property == TextPresenter.PreeditTextCursorPositionProperty) {
                    report(Composition.Of(presenter.PreeditText, presenter.PreeditTextCursorPosition));
                }
            };

            presenter.PropertyChanged += watching;
        };
    }
}
