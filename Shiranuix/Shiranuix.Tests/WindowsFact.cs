namespace Shiranuix.Tests;

/// <summary>
/// A fact that runs where the machinery it tests stands: the export's pen
/// asks this machine's own shelf which file holds a typeface, and whether
/// the other systems really hold a face the test names is theirs to
/// answer and not ours to assume — so on every other system the test says
/// so plainly and stands down rather than failing on a face the shelf had
/// no file for. On Windows it is a fact like any other, and it runs with
/// all the rest.
/// </summary>
public sealed class WindowsFactAttribute : FactAttribute {
    public WindowsFactAttribute() {
        if (!OperatingSystem.IsWindows()) {
            Skip = "What faces the test may count on stands on Windows.";
        }
    }
}
