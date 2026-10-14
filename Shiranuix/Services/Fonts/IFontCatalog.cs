namespace Shiranuix.Services.Fonts;

/// <summary>
/// The typefaces installed on this machine.
/// </summary>
/// <remarks>
/// Abstracted rather than read straight from Avalonia so the view model does
/// not have to know the font manager exists — enumerating fonts needs a
/// running Avalonia application, and a headless test has none.
/// </remarks>
public interface IFontCatalog {
    /// <summary>
    /// Installed font family names, distinct and alphabetised.
    /// </summary>
    IReadOnlyList<string> GetFamilies();
}
