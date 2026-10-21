namespace Shiranuix.FileTree.Tests;

/// <summary>
/// A fact that runs where the rule it tests stands: some names are refused
/// by the Windows disk and taken by a Unix one — a backslash is a
/// separator there and an ordinary letter here — because the tree refuses
/// by the rules of the disk it stands on. On every other system the test
/// says so plainly and stands down rather than failing on a rule that was
/// never there. On Windows it is a fact like any other, and it runs with
/// all the rest.
/// </summary>
public sealed class WindowsFactAttribute : FactAttribute {
    public WindowsFactAttribute() {
        if (!OperatingSystem.IsWindows()) {
            Skip = "A backslash in a name is a rule the Windows disk keeps.";
        }
    }
}
