using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Shiranuix.Services;

namespace Shiranuix.Views;

/// <summary>
/// Carries the writing's turns into the editor: text input is heard before
/// the box takes it, and the strokes the rules turn are re-written in their
/// place.
/// </summary>
/// <remarks>
/// The <see cref="InputElement.TextInputEvent"/> tunnels from the window, so
/// this hears a stroke before the box's own handling does, and answers for
/// only two sorts of stroke: the quote that takes its curly self, and the
/// pair of hyphens that becomes the dash the settings name. Everything else
/// - a stroke an input method commits whole - goes past untouched, which is
/// also what keeps composition safe. The find bar's box keeps its plain
/// typing: a search must match the file as it stands, not as the page would
/// have written it.
/// </remarks>
public static class TypingAssist {
    /// <summary>
    /// Listens to the window's text input for one editor, turning what the
    /// rules say turns and letting every other stroke stand.
    /// </summary>
    /// <param name="window">The window the input tunnels through.</param>
    /// <param name="editor">The editor whose typing is watched.</param>
    /// <param name="rules">What the writing turns, read fresh on every stroke.</param>
    public static void Watch(Window window, TextBox editor, Func<TypingRules?> rules) {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(rules);

        window.AddHandler(InputElement.TextInputEvent,
            (_, e) => OnTextInput(e, editor, rules),
            RoutingStrategies.Tunnel);
    }

    /// <summary>
    /// Answers one stroke: what the rules turn is put down in its place, and
    /// the box's own insertion never sees it.
    /// </summary>
    private static void OnTextInput(TextInputEventArgs e, TextBox editor,
        Func<TypingRules?> rules) {
        if (!ReferenceEquals(e.Source, editor) || e.Text is not { Length: 1 } one) {
            return;
        }

        var written = rules();
        if (written is null) {
            return;
        }

        // What the turn reads is the character the caret stands after - or,
        // when a span is held, the one before the span began, for the span
        // is what the stroke will land in.
        var held = editor.SelectionStart != editor.SelectionEnd;
        var standing = held ? editor.SelectionStart : editor.CaretIndex;
        var text = editor.Text ?? string.Empty;
        char? before = standing > 0 && standing <= text.Length
            ? text[standing - 1]
            : null;

        var turn = AsYouType.Stroke(one[0], before, written.Value);
        if (turn is null) {
            return;
        }

        // A dash reaches back for the hyphen it stands for, and only an
        // untouched page lets it reach: a span someone chose is theirs to
        // keep, so their plain hyphen replaces it exactly as typed.
        if (turn.Value.Replaces > 0 && held) {
            return;
        }

        // The edit goes through the box's own insertion: a dash holds the
        // hyphen it stands for and replaces the pair wholesale, a quote
        // lands where the caret is, and the rest of the input is kept from
        // landing twice.
        if (turn.Value.Replaces > 0) {
            editor.SelectionStart = standing - 1;
        }

        editor.SelectedText = turn.Value.Text;
        e.Handled = true;
    }
}
