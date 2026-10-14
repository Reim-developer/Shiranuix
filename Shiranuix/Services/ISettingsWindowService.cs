namespace Shiranuix.Services;

/// <summary>
/// Opens the application's settings window.
/// </summary>
public interface ISettingsWindowService {
    /// <summary>
    /// Shows the settings window, or brings the one already open to the
    /// front.
    /// </summary>
    void Show();
}
