using System.ComponentModel;

namespace Shiranuix.Testing;

/// <summary>
/// The type a document under test is set in: a family by name, a size in
/// points, and the word that either of them moved.
/// </summary>
/// <remarks>
/// The window's own state would do, but that is a state built out of an
/// installed font catalogue, a settings store and a history - three things
/// a test of the writing has no opinion about. This holds the two values
/// and says the words, which is the whole of what the document asks for,
/// and says them the same way so the dirty-point assertions notice.
/// </remarks>
public sealed class FakeDocumentTypography : IDocumentTypography {
    private string _fontFamily = "Georgia";
    private double _fontSize = 17;

    /// <inheritdoc />
    public string FontFamily {
        get => _fontFamily;
        set {
            if (_fontFamily == value) return;
            _fontFamily = value;
            Changed(nameof(FontFamily));
        }
    }

    /// <inheritdoc />
    public double FontSize {
        get => _fontSize;
        set {
            if (_fontSize == value) return;
            _fontSize = value;
            Changed(nameof(FontSize));
        }
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Changed(string which) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(which));
}
