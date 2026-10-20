namespace Narika.Services.Install;

/// <summary>
/// Everything one installation is asked to do: where it goes and which of
/// the machine's surfaces it may put its marks on.
/// </summary>
/// <remarks>
/// This is the one object the wizard pages write into and the install
/// service reads: the destination page names the folder, the options page
/// names the shortcuts, and the service is handed the whole thing on the
/// day the real install runs. Nothing here touches a disk — it is the
/// request, not the work.
/// </remarks>
public sealed class InstallOptions {
    /// <summary>
    /// Where a plain installation goes when the person has no opinion yet:
    /// the current user's own Programs folder, which needs nobody's
    /// permission and outlives the session that chose it.
    /// </summary>
    public static string DefaultDestination =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "Shiranuix");

    /// <summary>
    /// The folder the files are written into.
    /// </summary>
    public string Destination { get; set; } = DefaultDestination;

    /// <summary>
    /// Whether the machine's desktop gets an icon to double-click.
    /// </summary>
    public bool CreateDesktopShortcut { get; set; } = true;

    /// <summary>
    /// Whether the start menu gets an entry to click.
    /// </summary>
    public bool CreateStartMenuShortcut { get; set; } = true;

    /// <summary>
    /// Whether the program is pinned to the taskbar.
    /// </summary>
    public bool CreateTaskbarShortcut { get; set; } = true;
}
