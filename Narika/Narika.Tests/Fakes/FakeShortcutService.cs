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

    /// <inheritdoc />
    public void CreateDesktop(string targetPath, string? iconPath, string description) =>
        Mark("desktop", targetPath);

    /// <inheritdoc />
    public void CreateStartMenu(string targetPath, string? iconPath, string description) =>
        Mark("start menu", targetPath);

    /// <inheritdoc />
    public void PinToTaskbar(string targetPath, string? iconPath, string description) =>
        Mark("taskbar", targetPath);

    private void Mark(string surface, string targetPath) {
        Marks.Add(surface);
        Target = targetPath;
    }
}
