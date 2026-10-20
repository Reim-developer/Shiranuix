using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.TextFormatting;

[assembly: AvaloniaTestApplication(typeof(Shiranuix.TextRender.Tests.TestAppBuilder))]

namespace Shiranuix.TextRender.Tests;

/// <summary>
/// The headless stand for the application: real shaping, real fonts, and
/// the same formatter stand the running app takes at its door.
/// </summary>
public class TestAppBuilder {
    /// <summary>
    /// Builds the headless platform with its drawing left real — the
    /// measurements these tests read off a laid-out line are the ones the
    /// screen would show, and a headless stub would draw every glyph at
    /// no width at all — and stands the per-line formatter in where the
    /// real application stands its own.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp() {
        // The framework's own, captured before the stand-in takes its
        // place, so that every question the stand-in was not asked about
        // is answered the way it always was.
        TextFormatter fallback = TextFormatter.Current;
        TextFormatter perLine = new LineAlignedFormatter(fallback);

        return AppBuilder.Configure<Application>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .With(perLine)
            .UseSkia();
    }
}
