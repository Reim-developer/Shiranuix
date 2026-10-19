using System.ComponentModel;

namespace Shiranuix.CoreEditor;

/// <summary>
/// What the document knows about the type it is set in: the family, the
/// size, and word when either of them changes.
/// </summary>
/// <remarks>
/// An interface rather than the state itself, because that state knows the
/// window — which fonts are installed, which one is showing, what the
/// preview does — and none of that is a thing the writing needs. What the
/// writing needs is two numbers it can carry into a saved file and notice
/// when they move, which is exactly this. The window keeps the rest, and
/// answers here.
///
/// Family and size rather than a typeface, for the reason the file format
/// keeps them: a name and a number survive the trip to a file and back, a
/// handle to a drawn font does not.
/// </remarks>
public interface IDocumentTypography {
    /// <summary>The family the document is set in, by name.</summary>
    string FontFamily { get; set; }

    /// <summary>The size the document is set in, in points.</summary>
    double FontSize { get; set; }

    /// <summary>Raised when either of them changes.</summary>
    event PropertyChangedEventHandler? PropertyChanged;
}
