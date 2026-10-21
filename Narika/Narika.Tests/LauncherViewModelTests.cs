using Narika.Services.Install;
using Narika.Services.Theme;
using Narika.Tests.Fakes;
using Narika.ViewModels;

namespace Narika.Tests;

/// <summary>
/// Tests for <see cref="LauncherViewModel"/>: the one screen the launcher
/// opens on, driven entirely against doubles — no window is opened, no
/// byte is fetched, and every door the card's press reaches is asked
/// whether it is offered first.
/// </summary>
public sealed class LauncherViewModelTests {
    /// <summary>
    /// Builds a launcher over its fakes, each handed back so a test can
    /// watch what the card's press asks of them.
    /// </summary>
    private static LauncherViewModel Launcher(
        out InMemoryInstallState state,
        out FakeSetupWindowService setup,
        out FakeInstallService install,
        out FakeUninstallService uninstall) {
        state = new InMemoryInstallState();
        setup = new FakeSetupWindowService();
        install = new FakeInstallService();
        uninstall = new FakeUninstallService();

        var theme = new ThemeService(
            _ => { },
            Path.Combine(
                Path.GetTempPath(), "narika-tests", Guid.NewGuid().ToString("N") + ".txt"));

        return new LauncherViewModel(
            state, new FakeReleaseCatalog(), setup, install, uninstall,
            new InstallOptions(), theme);
    }

    [Fact]
    public void Nothing_Installed_Opens_Doors_And_Holds_The_Two_That_Need_Something() {
        var vm = Launcher(out _, out _, out _, out _);

        Assert.False(vm.IsInstalled);
        Assert.Equal("Not installed", vm.StatusBadge);
        Assert.Equal("Ready to install.", vm.StatusMessage);
        Assert.False(vm.IsDetailsOpen);
        Assert.True(vm.DownloadCommand.CanExecute(null));
        Assert.False(vm.RepairCommand.CanExecute(null));
        Assert.False(vm.UninstallCommand.CanExecute(null));
        Assert.False(vm.CancelCommand.CanExecute(null));
    }

    [Fact]
    public void Pressing_The_Card_Opens_The_Panel_Either_Way() {
        var vm = Launcher(out var state, out var setup, out _, out _);

        vm.OpenCommand.Execute(null);

        // The press is one press: what stands behind it is the three
        // doors, whether or not any of them has anything to open yet.
        Assert.True(vm.IsDetailsOpen);
        Assert.Equal(0, setup.ShowCount);

        // And the next press is the same press answering the other way.
        vm.OpenCommand.Execute(null);
        Assert.False(vm.IsDetailsOpen);

        state.MarkInstalled(@"C:\Apps\Shiranuix\Shiranuix.exe");

        vm.OpenCommand.Execute(null);
        Assert.True(vm.IsDetailsOpen);
    }

    [Fact]
    public void Download_Opens_The_Wizard_And_Shuts_The_Panel() {
        var vm = Launcher(out _, out var setup, out _, out _);

        vm.DownloadCommand.Execute(null);

        Assert.Equal(1, setup.ShowCount);
        Assert.True(setup.IsOpen);
        Assert.False(vm.IsDetailsOpen);
    }

    [Fact]
    public async Task A_Repair_That_Fails_Says_Try_Again_And_Stays_Installed() {
        var vm = Launcher(out var state, out _, out var install, out _);
        state.MarkInstalled(@"C:\Apps\Shiranuix\Shiranuix.exe");
        install.Fail = true;

        await vm.RepairCommand.ExecuteAsync(null);

        Assert.Equal("The repair failed — please try again.", vm.StatusMessage);
        Assert.False(vm.IsWorking);
        Assert.True(vm.IsInstalled);
        Assert.True(vm.RepairCommand.CanExecute(null));
    }

    [Fact]
    public async Task Repair_Walks_The_Install_Over_The_Folder_The_Receipt_Names() {
        var vm = Launcher(out var state, out _, out var install, out _);
        state.MarkInstalled(@"C:\Apps\Shiranuix\Shiranuix.exe");

        await vm.RepairCommand.ExecuteAsync(null);

        // Repair is the install service's own walk — the bytes come up
        // beside the unpacking rather than in a fetch of its own — over
        // the folder the receipt names, so the program never quietly
        // moves; the launcher stays exactly where it is, with a line
        // saying the work is over.
        Assert.Equal(1, install.Calls);
        Assert.Equal(@"C:\Apps\Shiranuix", install.Received!.Destination);
        Assert.True(vm.IsInstalled);
        Assert.Equal("Repair complete — Shiranuix is ready to use.", vm.StatusMessage);
    }

    [Fact]
    public async Task Uninstall_Askes_Once_And_Only_The_Second_Press_Takes_Everything_Back() {
        var vm = Launcher(out var state, out _, out _, out var uninstall);
        state.MarkInstalled(@"C:\Apps\Shiranuix\Shiranuix.exe");

        await vm.UninstallCommand.ExecuteAsync(null);

        // The first press says what will be taken and takes nothing: a
        // door this final deserves a hand that meant it.
        Assert.Equal(0, uninstall.Calls);
        Assert.True(vm.IsInstalled);
        Assert.Equal(
            "Uninstall takes Shiranuix off this machine — its files, its shortcuts, " +
            "everything. Press Uninstall again to confirm.",
            vm.StatusMessage);

        await vm.UninstallCommand.ExecuteAsync(null);

        Assert.Equal(1, uninstall.Calls);
        Assert.Equal(@"C:\Apps\Shiranuix\Shiranuix.exe", uninstall.Received);
        Assert.False(vm.IsInstalled);
        Assert.Equal("Not installed", vm.StatusBadge);
        Assert.Equal("Shiranuix was removed.", vm.StatusMessage);
    }

    [Fact]
    public async Task A_Failed_Uninstall_Says_So_And_Stays_Installed() {
        var vm = Launcher(out var state, out _, out _, out var uninstall);
        state.MarkInstalled(@"C:\Apps\Shiranuix\Shiranuix.exe");
        uninstall.Fail = true;

        await vm.UninstallCommand.ExecuteAsync(null);
        await vm.UninstallCommand.ExecuteAsync(null);

        Assert.Equal(
            "The uninstall failed — some of it may still be on this machine.",
            vm.StatusMessage);
        Assert.True(vm.IsInstalled);
        Assert.False(vm.IsWorking);

        // And the ask returns: after a removal that ran and did not
        // finish, the next press is an ask again rather than a second
        // attempt behind a person's back.
        await vm.UninstallCommand.ExecuteAsync(null);

        Assert.Equal(1, uninstall.Calls);
        Assert.StartsWith("Uninstall takes Shiranuix", vm.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancel_Stops_The_Uninstall_That_Is_Running_And_Says_So() {
        var vm = Launcher(out var state, out _, out _, out var uninstall);
        state.MarkInstalled(@"C:\Apps\Shiranuix\Shiranuix.exe");
        uninstall.Gate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var confirming = vm.UninstallCommand.ExecuteAsync(null);
        await confirming;

        var running = vm.UninstallCommand.ExecuteAsync(null);
        await uninstall.Started.Task;

        Assert.True(vm.IsWorking);
        Assert.Equal("Uninstalling Shiranuix...", vm.StatusMessage);
        Assert.True(vm.CancelCommand.CanExecute(null));
        Assert.False(vm.DownloadCommand.CanExecute(null));

        vm.CancelCommand.Execute(null);
        await uninstall.CancelObserved.Task;
        await running;

        Assert.Equal("Uninstall cancelled.", vm.StatusMessage);
        Assert.True(vm.IsInstalled);
        Assert.False(vm.IsWorking);
        Assert.True(vm.UninstallCommand.CanExecute(null));
    }

    [Fact]
    public async Task Cancel_Stops_The_Repair_That_Is_Running_And_Says_So() {
        var vm = Launcher(out var state, out _, out var install, out _);
        state.MarkInstalled(@"C:\Apps\Shiranuix\Shiranuix.exe");
        install.Gate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var running = vm.RepairCommand.ExecuteAsync(null);
        await install.Started.Task;

        Assert.True(vm.IsWorking);
        Assert.Equal("Repairing Shiranuix...", vm.StatusMessage);
        Assert.True(vm.CancelCommand.CanExecute(null));
        Assert.False(vm.DownloadCommand.CanExecute(null));

        vm.CancelCommand.Execute(null);
        await install.CancelObserved.Task;
        await running;

        Assert.Equal("Repair cancelled.", vm.StatusMessage);
        Assert.False(vm.IsWorking);
        Assert.True(vm.RepairCommand.CanExecute(null));
    }

    [Fact]
    public void Closing_The_Wizard_Without_Installing_Leaves_The_Press_The_Door() {
        var vm = Launcher(out _, out var setup, out _, out _);

        vm.DownloadCommand.Execute(null);
        setup.Close();

        Assert.Equal("Setup closed — nothing was installed.", vm.StatusMessage);
        Assert.False(vm.IsDetailsOpen);
        Assert.True(vm.DownloadCommand.CanExecute(null));
    }

    [Fact]
    public void Closing_The_Wizard_After_A_Finish_Opens_The_Panel() {
        var vm = Launcher(out var state, out var setup, out _, out _);

        vm.DownloadCommand.Execute(null);
        state.MarkInstalled(@"C:\Apps\Shiranuix\Shiranuix.exe");
        setup.Close();

        // A wizard that closed over an installation hands the panel its
        // open state, because that is where the doors onto it stand.
        Assert.Equal("Installed — Shiranuix is ready to use.", vm.StatusMessage);
        Assert.True(vm.IsDetailsOpen);
        Assert.True(vm.RepairCommand.CanExecute(null));
        Assert.True(vm.UninstallCommand.CanExecute(null));
    }

    [Fact]
    public void While_The_Wizard_Stands_Open_No_Door_Is_Offered() {
        var vm = Launcher(out var state, out var setup, out _, out _);
        state.MarkInstalled(@"C:\Apps\Shiranuix\Shiranuix.exe");

        setup.Show();

        Assert.False(vm.DownloadCommand.CanExecute(null));
        Assert.False(vm.RepairCommand.CanExecute(null));
        Assert.False(vm.UninstallCommand.CanExecute(null));

        setup.Close();

        Assert.True(vm.DownloadCommand.CanExecute(null));
        Assert.True(vm.RepairCommand.CanExecute(null));
        Assert.True(vm.UninstallCommand.CanExecute(null));
    }

    [Fact]
    public void The_Launcher_Says_The_Version_The_Catalog_Answers_With() {
        var vm = Launcher(out _, out _, out _, out _);

        Assert.Equal("9.9.9", vm.Version);
    }
}
