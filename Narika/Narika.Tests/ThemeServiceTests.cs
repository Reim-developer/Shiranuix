using Narika.Services.Theme;

namespace Narika.Tests;

/// <summary>
/// Tests for <see cref="ThemeService"/>: the three answers the header's
/// toggle walks through, what each press does to the running application,
/// and the file that keeps a choice between runs.
/// </summary>
public sealed class ThemeServiceTests {
    /// <summary>
    /// Builds a service over a fresh temporary file and the list of modes
    /// it was asked to apply, so a test can watch both.
    /// </summary>
    private static ThemeService Service(out List<ThemeMode> applied, out string path) {
        path = Path.Combine(
            Path.GetTempPath(),
            "narika-theme-tests",
            Guid.NewGuid().ToString("N"),
            "theme.txt");
        applied = new List<ThemeMode>();

        return new ThemeService(applied.Add, path);
    }

    [Fact]
    public void A_First_Run_Follows_The_Machine_And_Says_System() {
        var theme = Service(out var applied, out _);

        Assert.Equal(ThemeMode.System, theme.Mode);
        Assert.Equal("System", theme.ModeLabel);
        Assert.Equal(ThemeMode.System, applied[^1]);
    }

    [Fact]
    public void The_Toggle_Walks_The_Three_Answers_Round_And_Back() {
        var theme = Service(out _, out _);

        theme.NextCommand.Execute(null);
        Assert.Equal(ThemeMode.Light, theme.Mode);
        Assert.Equal("Light", theme.ModeLabel);

        theme.NextCommand.Execute(null);
        Assert.Equal(ThemeMode.Dark, theme.Mode);
        Assert.Equal("Dark", theme.ModeLabel);

        theme.NextCommand.Execute(null);
        Assert.Equal(ThemeMode.System, theme.Mode);
        Assert.Equal("System", theme.ModeLabel);
    }

    [Fact]
    public void Every_Change_Is_Applied_To_The_Application_In_Order() {
        var theme = Service(out var applied, out _);

        theme.NextCommand.Execute(null);
        theme.NextCommand.Execute(null);

        Assert.Equal(
            new[] { ThemeMode.System, ThemeMode.Light, ThemeMode.Dark },
            applied);
    }

    [Fact]
    public void A_Choice_Survives_The_Run_It_Was_Made_In() {
        var theme = Service(out _, out var path);
        theme.NextCommand.Execute(null); // Light

        Assert.True(File.Exists(path));

        var reopened = new ThemeService(_ => { }, path);
        Assert.Equal(ThemeMode.Light, reopened.Mode);
        Assert.Equal("Light", reopened.ModeLabel);
    }

    [Fact]
    public void A_File_That_Says_Nothing_Sensible_Follows_The_Machine() {
        var theme = Service(out _, out var path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "purple");

        var reopened = new ThemeService(_ => { }, path);
        Assert.Equal(ThemeMode.System, reopened.Mode);
    }
}
