using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Shiranuix.TextRender;

/// <summary>
/// Keeps a text box's page laid out line by line: the property a window
/// hangs off its editor to say which edge each line stands at, and the
/// reaching-in that carries the answer to the page under the editor's
/// own template.
/// </summary>
/// <remarks>
/// <para>
/// A text box builds its page from a template, and the page — the
/// presenter — is a thing the template names, not a thing the box
/// constructs. So the answer cannot simply be set on the box and expect
/// to arrive: it is carried down whenever the page is standing there to
/// receive it, and if the template has not been applied yet, the box is
/// listened to until it has.
/// </para>
/// <para>
/// The page under a stock text box is not this editor's page, and stays
/// what it was: the box only ever finds the presenter it is looking for
/// when its template put one there.
/// </para>
/// </remarks>
public static class LineAlignment {
    /// <summary>
    /// Defines the attached <see cref="ProviderProperty"/> property.
    /// </summary>
    public static readonly AttachedProperty<Func<int, TextAlignment>?> ProviderProperty =
        AvaloniaProperty.RegisterAttached<TextBox, Func<int, TextAlignment>?>(
            "Provider", typeof(LineAlignment));

    /// <summary>
    /// The provider is carried to the page as soon as one is standing
    /// under the editor, which for a boxed editor is the moment its
    /// template is applied.
    /// </summary>
    static LineAlignment() {
        ProviderProperty.Changed.AddClassHandler<TextBox>(OnProviderChanged);
    }

    /// <summary>
    /// Gets how the editor's lines are to stand, as asked by whoever set
    /// it.
    /// </summary>
    /// <param name="editor">The editor being asked about.</param>
    public static Func<int, TextAlignment>? GetProvider(TextBox editor) =>
        editor.GetValue(ProviderProperty);

    /// <summary>
    /// Sets how the editor's lines are to stand.
    /// </summary>
    /// <param name="editor">The editor to lay out line by line.</param>
    /// <param name="provider">How the line with a given number should
    /// stand, or nothing at all to take it back down.</param>
    public static void SetProvider(TextBox editor, Func<int, TextAlignment>? provider) =>
        editor.SetValue(ProviderProperty, provider);

    /// <summary>
    /// Has the editor's lines laid out again — for the caller that
    /// changed what the provider would answer without changing the
    /// provider itself, such as a document whose lines were just set
    /// some other way.
    /// </summary>
    /// <param name="editor">The editor whose page should stand its lines
    /// up again.</param>
    public static void Refresh(TextBox editor) =>
        Page(editor)?.RefreshLineAlignments();

    /// <summary>
    /// Carries the provider down to the page, or waits for the template
    /// to bring one up before doing so.
    /// </summary>
    private static void OnProviderChanged(TextBox editor, AvaloniaPropertyChangedEventArgs change) {
        if (Apply(editor)) {
            return;
        }

        editor.TemplateApplied += OnTemplateApplied;

        return;

        // The template brought the page up: it can hear the answer now.
        // Its name is asked of the template's own name scope rather than
        // walked for in the visual tree, because at this moment the tree
        // below the template's root is still being measured into being
        // while every name it will hold is already there.
        void OnTemplateApplied(object? sender, TemplateAppliedEventArgs e) {
            editor.TemplateApplied -= OnTemplateApplied;

            if (e.NameScope.Find<AlignedTextPresenter>("PART_TextPresenter") is { } page) {
                page.LineAlignmentProvider = GetProvider(editor);
            }
        }
    }

    /// <summary>
    /// Hands the provider to the page under the editor, if there is one
    /// standing there.
    /// </summary>
    /// <returns>Whether a page was there to hand it to.</returns>
    private static bool Apply(TextBox editor) {
        if (Page(editor) is not { } page) {
            return false;
        }

        page.LineAlignmentProvider = GetProvider(editor);

        return true;
    }

    /// <summary>
    /// The editor's own page — found under whatever the template built,
    /// and nothing at all for a box whose template did not put one there.
    /// </summary>
    private static AlignedTextPresenter? Page(TextBox editor) =>
        editor.GetVisualDescendants().OfType<AlignedTextPresenter>().FirstOrDefault();
}
