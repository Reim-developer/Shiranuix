using Avalonia.Media.Fonts;

namespace Shiranuix.Fonts;

/// <summary>
/// Publishes the Nunito files embedded in <c>Assets/Fonts</c> under the
/// custom <c>fonts:Shiranuix</c> URI scheme.
/// </summary>
/// <remarks>
/// The scheme is registered once in <c>Program.BuildAvaloniaApp</c>, so any
/// font in the app can then be referenced as
/// <c>fonts:Shiranuix#Nunito</c> — including the theme's default
/// <c>ContentControlThemeFontFamily</c> override. Nunito is SIL OFL
/// licensed: its rounded terminals are what give the UI its softer voice.
/// </remarks>
public sealed class ShiranuixFontCollection : EmbeddedFontCollection
{
    /// <summary>Creates the collection mapped onto the bundled font folder.</summary>
    public ShiranuixFontCollection()
        : base(
            new Uri("fonts:Shiranuix", UriKind.Absolute),
            new Uri("avares://Shiranuix/Assets/Fonts", UriKind.Absolute))
    {
    }
}
