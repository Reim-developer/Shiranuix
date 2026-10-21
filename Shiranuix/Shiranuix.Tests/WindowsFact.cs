namespace Shiranuix.Tests;

/// <summary>
/// A fact that runs where the machinery it tests stands: PDFium — the paper
/// engine — and the machine's font shelf are Windows's own, so on every other
/// system the test says so plainly and stands down rather than failing on
/// something that was never there. On Windows it is a fact like any other,
/// and it runs with all the rest.
/// </summary>
public sealed class WindowsFactAttribute : FactAttribute {
    public WindowsFactAttribute() {
        if (!OperatingSystem.IsWindows()) {
            Skip = "PDFium and the machine font shelf live on Windows only.";
        }
    }
}
