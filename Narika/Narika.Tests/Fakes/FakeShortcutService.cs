using Narika.Services.Install;

namespace Narika.Tests.Fakes;

/// <summary>
/// The shortcut service's double: it writes nothing anywhere and remembers
/// every mark it was asked for, in the order it was asked, so a test can
/// read the machine's surfaces back off it.
/// </summary>
public sealed class FakeShortcutService : IShortcutService {
    /// <summary>
    /// Which surfaces were marked, named the way the interface names them:
    /// "desktop", "start menu", "taskbar".
    /// </summary>
    public List<string> Marks { get; } = [];

    /// <summary>
    /// What every mark was pointed at, so a test can tell the marks were
    /// left on the program that was installed rather than on nothing.
    /// </summary>
    public string? Target { get; private set; }

    /// <summary>
    /// Which surfaces were taken back, named the way the marks were.
    /// </summary>
    public List<string> Unmarked { get; } = [];

    /// <summary>
    /// What every removal was pointed at, so a test can tell the marks
    /// came back off the program that was installed rather than off
    /// nothing.
    /// </summary>
    public string? UnmarkedTarget { get; private set; }

    /// <inheritdoc />
    public void CreateDesktop(string targetPath, string? iconPath, string description) =>
        Mark("desktop", targetPath);

    /// <inheritdoc />
    public void CreateStartMenu(string targetPath, string? iconPath, string description) =>
        Mark("start menu", targetPath);

    /// <inheritdoc />
    public void PinToTaskbar(string targetPath, string? iconPath, string description) =>
        Mark("taskbar", targetPath);

    /// <inheritdoc />
    public void RemoveDesktop(string executable) => Unmark("desktop", executable);

    /// <inheritdoc />
    public void RemoveStartMenu(string executable) => Unmark("start menu", executable);

    /// <inheritdoc />
    public void RemoveTaskbar(string executable) => Unmark("taskbar", executable);

    private void Mark(string surface, string targetPath) {
        Marks.Add(surface);
        Target = targetPath;
    }

    private void Unmark(string surface, string executable) {
        Unmarked.Add(surface);
        UnmarkedTarget = executable;
    }
}
