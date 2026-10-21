using Shiranuix.Models;
using Shiranuix.Persistence;

namespace Shiranuix.Tests.Fakes;

/// <summary>
/// An in-memory settings store, so tests never touch the database.
/// </summary>
public sealed class FakeUserSettings : IUserSettings {
    /// <summary>
    /// What <see cref="Load"/> returns. Left null to model a first run, when
    /// nothing has been stored yet.
    /// </summary>
    public UserSettings? Stored { get; set; }

    /// <summary>Every <see cref="Save"/>, in order.</summary>
    public List<UserSettings> Saves { get; } = new();

    /// <inheritdoc />
    public UserSettings? Load() => Stored;

    /// <inheritdoc />
    public void Save(UserSettings settings) {
        Stored = settings;
        Saves.Add(settings);
    }
}
