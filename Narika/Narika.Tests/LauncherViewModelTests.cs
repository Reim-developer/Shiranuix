using Narika.Services.Install;
using Narika.Services.Theme;
using Narika.Tests.Fakes;
using Narika.ViewModels;

namespace Narika.Tests;

/// <summary>
/// Tests for <see cref="LauncherViewModel"/>: the one screen the launcher
/// opens on, driven entirely against doubles — no window is opened, no
/// byte is fetched, and every door the card offers is asked whether it is
/// offered first.
/// </summary>
public sealed class LauncherViewModelTests {
    /// <summary>
    /// Builds a launcher over its fakes, each handed back so a test can
    /// watch what the card asks of them.
    /// </summary>
    private static LauncherViewModel Launcher(
        out InMemoryInstallState state,
        out FakeDownloadService download,
        out FakeSetupWindowService setup,
        out FakeAppLauncher launcher) {
        state = new InMemoryInstallState();
        download = new FakeDownloadService();
        setup = new FakeSetupWindowService();
        launcher = new FakeAppLauncher();

        var theme = new ThemeService(
            _ => { },
            Path.Combine(
                Path.GetTempPath(), "narika-tests", Guid.NewGuid().ToString("N") + ".txt"));

        return new LauncherViewModel(
            state, download, new FakeReleaseCatalog(), launcher, setup, theme);
    }

    [Fact]
    public void Nothing_Installed_Offers_Download_And_Holds_The_Other_Doors() {
        var vm = Launcher(out _, out _, out _, out _);

        Assert.False(vm.IsInstalled);
        Assert.Equal("Not installed", vm.StatusBadge);
        Assert.Equal("Ready to install.", vm.StatusMessage);
        Assert.True(vm.DownloadCommand.CanExecute(null));
        Assert.False(vm.RepairCommand.CanExecute(null));
        Assert.False(vm.LaunchCommand.CanExecute(null));
        Assert.False(vm.CancelCommand.CanExecute(null));
    }

    [Fact]
    public async Task Download_Walks_To_The_Wizard_And_Freeze_Everything_While_Running() {
        var vm = Launcher(out _, out var download, out var setup, out _);
        download.Gate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var running = vm.DownloadCommand.ExecuteAsync(null);
        await download.Started.Task;

        Assert.True(vm.IsWorking);
        Assert.False(vm.DownloadCommand.CanExecute(null));
        Assert.False(vm.RepairCommand.CanExecute(null));
        Assert.True(vm.CancelCommand.CanExecute(null));
        Assert.Equal("Downloading Shiranuix...", vm.StatusMessage);

        download.Gate.TrySetResult();
        await running;

        Assert.False(vm.IsWorking);
        Assert.Equal(1, setup.ShowCount);
        Assert.True(setup.IsOpen);
        Assert.Equal("Download complete — opening setup...", vm.StatusMessage);
    }

    [Fact]
    public async Task Cancel_Stops_The_Fetch_That_Is_Running_And_Says_So() {
        var vm = Launcher(out _, out var download, out var setup, out _);
        download.Gate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var running = vm.DownloadCommand.ExecuteAsync(null);
        await download.Started.Task;

        vm.CancelCommand.Execute(null);
        await download.CancelObserved.Task;
        await running;

        Assert.Equal("Download cancelled.", vm.StatusMessage);
        Assert.False(vm.IsWorking);
        Assert.Equal(0, setup.ShowCount);
        Assert.True(vm.DownloadCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_Fetch_That_Fails_Says_Try_Again_Rather_Than_Nothing() {
        var vm = Launcher(out _, out var download, out var setup, out _);
        download.Fail = true;

        await vm.DownloadCommand.ExecuteAsync(null);

        Assert.Equal("The download failed — please try again.", vm.StatusMessage);
        Assert.False(vm.IsWorking);
        Assert.Equal(0, setup.ShowCount);
        Assert.True(vm.DownloadCommand.CanExecute(null));
    }

    [Fact]
    public async Task Installed_Flips_The_Card_To_Launch_And_Repair() {
        var vm = Launcher(out var state, out var download, out var setup, out _);
        state.MarkInstalled(@"C:\Apps\Shiranuix\Shiranuix.exe");

        Assert.True(vm.IsInstalled);
        Assert.Equal("Installed", vm.StatusBadge);
        Assert.False(vm.DownloadCommand.CanExecute(null));
        Assert.True(vm.RepairCommand.CanExecute(null));
        Assert.True(vm.LaunchCommand.CanExecute(null));

        await vm.RepairCommand.ExecuteAsync(null);

        // Repair fetches the same bytes and stays exactly where it is:
        // no wizard, no change of state, and a line that says it is over.
        Assert.Equal(0, setup.ShowCount);
        Assert.True(vm.IsInstalled);
        Assert.Equal("Repair complete — Shiranuix is ready to use.", vm.StatusMessage);
    }

    [Fact]
    public async Task Launch_Starts_What_Is_Installed_And_Says_It_Runs() {
        var vm = Launcher(out var state, out _, out _, out var launcher);
        state.MarkInstalled(@"C:\Apps\Shiranuix\Shiranuix.exe");

        await vm.LaunchCommand.ExecuteAsync(null);

        Assert.Equal(@"C:\Apps\Shiranuix\Shiranuix.exe", launcher.Started);
        Assert.Equal("Shiranuix is running.", vm.StatusMessage);
    }

    [Fact]
    public async Task Launch_That_Fails_Says_So_In_The_Status_Line() {
        var vm = Launcher(out var state, out _, out _, out var launcher);
        state.MarkInstalled(@"C:\Apps\Shiranuix\Shiranuix.exe");
        launcher.Fail = true;

        await vm.LaunchCommand.ExecuteAsync(null);

        Assert.Equal("Couldn't start Shiranuix — try Repair.", vm.StatusMessage);
    }

    [Fact]
    public async Task Closing_The_Wizard_Without_Installing_Puts_Download_Back() {
        var vm = Launcher(out _, out var download, out var setup, out _);

        await vm.DownloadCommand.ExecuteAsync(null);
        Assert.Equal(1, setup.ShowCount);
        Assert.False(vm.DownloadCommand.CanExecute(null));

        setup.Close();

        Assert.Equal("Setup closed — nothing was installed.", vm.StatusMessage);
        Assert.True(vm.DownloadCommand.CanExecute(null));
    }

    [Fact]
    public async Task Closing_The_Wizard_After_A_Finish_Says_It_Is_Installed() {
        var vm = Launcher(out var state, out var download, out var setup, out _);

        await vm.DownloadCommand.ExecuteAsync(null);
        state.MarkInstalled(@"C:\Apps\Shiranuix\Shiranuix.exe");
        setup.Close();

        Assert.Equal("Installed — Shiranuix is ready to use.", vm.StatusMessage);
        Assert.True(vm.LaunchCommand.CanExecute(null));
        Assert.False(vm.DownloadCommand.CanExecute(null));
    }

    [Fact]
    public void While_The_Wizard_Stands_Open_No_Door_Is_Offered() {
        var vm = Launcher(out var state, out _, out var setup, out _);
        state.MarkInstalled(@"C:\Apps\Shiranuix\Shiranuix.exe");

        setup.Show();

        Assert.False(vm.DownloadCommand.CanExecute(null));
        Assert.False(vm.RepairCommand.CanExecute(null));
        Assert.False(vm.LaunchCommand.CanExecute(null));

        setup.Close();

        Assert.True(vm.RepairCommand.CanExecute(null));
        Assert.True(vm.LaunchCommand.CanExecute(null));
    }

    [Fact]
    public void The_Card_Says_The_Version_The_Catalog_Answers_With() {
        var vm = Launcher(out _, out _, out _, out _);

        Assert.Equal("9.9.9", vm.Version);
    }
}
