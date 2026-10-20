namespace Narika.Services.Install;

/// <summary>
/// Leaving a mark on the machine's own surfaces — the desktop, the start
/// menu, the taskbar — so an application can be started the ordinary way,
/// by finding it where a person looks for a program.
/// </summary>
/// <remarks>
/// Where those surfaces live, and what a mark on one of them is made of, is
/// the implementation's own business; this name only says that a mark was
/// asked for. That is what lets the whole install run be tested against a
/// double that writes nothing.
/// </remarks>
public interface IShortcutService {
    /// <summary>
    /// Puts an icon on the desktop to double-click.
    /// </summary>
    /// <param name="targetPath">What the icon starts.</param>
    /// <param name="iconPath">
    /// What the icon is drawn from; null falls back to the target's own.
    /// </param>
    /// <param name="description">What the icon says it starts, to a screen reader.</param>
    void CreateDesktop(string targetPath, string? iconPath, string description);

    /// <summary>
    /// Puts an entry in the start menu to click.
    /// </summary>
    /// <param name="targetPath">What the entry starts.</param>
    /// <param name="iconPath">
    /// What the entry is drawn from; null falls back to the target's own.
    /// </param>
    /// <param name="description">What the entry says it starts, to a screen reader.</param>
    void CreateStartMenu(string targetPath, string? iconPath, string description);

    /// <summary>
    /// Pins the program to the taskbar, so the ordinary way to start it
    /// from now on is one click away.
    /// </summary>
    /// <param name="targetPath">What the pin starts.</param>
    /// <param name="iconPath">
    /// What the pin is drawn from; null falls back to the target's own.
    /// </param>
    /// <param name="description">What the pin says it starts, to a screen reader.</param>
    void PinToTaskbar(string targetPath, string? iconPath, string description);
}
