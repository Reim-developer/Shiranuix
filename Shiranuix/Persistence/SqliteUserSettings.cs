using Microsoft.EntityFrameworkCore;
using Shiranuix.Models;

namespace Shiranuix.Persistence;

/// <summary>
/// Settings kept in the store's single row.
/// </summary>
/// <remarks>
/// Every operation builds its own context and disposes it, so nothing here
/// holds state between calls.
/// </remarks>
public sealed class SqliteUserSettings : IUserSettings {
    private readonly IDbContextFactory<ShiranuixContext> _factory;

    /// <summary>
    /// Creates a store over the given context factory.
    /// </summary>
    /// <param name="factory">The factory contexts are built from.</param>
    public SqliteUserSettings(IDbContextFactory<ShiranuixContext> factory) {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <inheritdoc />
    public UserSettings? Load() {
        using var context = _factory.CreateDbContext();
        return context.UserSettings.FirstOrDefault();
    }

    /// <inheritdoc />
    public void Save(UserSettings settings) {
        ArgumentNullException.ThrowIfNull(settings);

        using var context = _factory.CreateDbContext();
        var stored = context.UserSettings.FirstOrDefault();

        // There is one row, so an update lands on it and an insert creates it
        // rather than a second row nobody reads.
        settings.Id = UserSettingsRow;
        if (stored is null) {
            context.UserSettings.Add(settings);
        } else {
            stored.FontFamily = settings.FontFamily;
            stored.FontSize = settings.FontSize;
            stored.ThemeMode = settings.ThemeMode;
            stored.WordWrap = settings.WordWrap;
            stored.TextAlign = settings.TextAlign;
            stored.SmartQuotes = settings.SmartQuotes;
            stored.DashMode = settings.DashMode;
            stored.ShowWelcomeMessage = settings.ShowWelcomeMessage;
            stored.PdfFontFamily = settings.PdfFontFamily;
            stored.PdfFontSize = settings.PdfFontSize;
        }

        context.SaveChanges();
    }

    /// <summary>The id of the only row the settings table ever holds.</summary>
    private const int UserSettingsRow = 1;
}
