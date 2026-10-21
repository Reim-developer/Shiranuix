namespace Shiranuix.Tests;

/// <summary>
/// A fact that runs where the machinery it tests stands: the export's pen
/// asks the Windows registry which file holds a typeface — an answer no
/// other system keeps — so on every other system the test says so plainly
/// and stands down rather than failing on a shelf that was never there. On
/// Windows it is a fact like any other, and it runs with all the rest.
/// </summary>
public sealed class WindowsFactAttribute : FactAttribute {
    public WindowsFactAttribute() {
        if (!OperatingSystem.IsWindows()) {
            Skip = "The machine's font shelf lives on Windows only.";
        }
    }
}
