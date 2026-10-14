using Shiranuix.Models;

namespace Shiranuix.Persistence;

/// <summary>
/// The user's typography and theme choices, kept between sessions.
/// </summary>
/// <remarks>
/// Kept behind an interface for the same reason as
/// <see cref="Services.IFileService"/>: the view model that restores these runs
/// headlessly in tests, over an in-memory implementation that never touches
/// the database.
/// </remarks>
public interface IUserSettings {
    /// <summary>
    /// Reads the stored choices.
    /// </summary>
    /// <returns>The stored choices, or null when nothing has been stored yet.</returns>
    UserSettings? Load();

    /// <summary>
    /// Writes the choices, replacing whatever was stored.
    /// </summary>
    /// <param name="settings">The choices to store.</param>
    void Save(UserSettings settings);
}
