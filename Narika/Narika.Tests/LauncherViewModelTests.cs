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
        out FakeAppLauncher launcher,
        out FakeInstallService install) {
        state = new InMemoryInstallState();
        setup = new FakeSetupWindowService();
        launcher = new FakeAppLauncher();
        install = new FakeInstallService();

        var theme = new ThemeService(
            _ => { },
            Path.Combine(
                Path.GetTempPath(), "narika-tests", Guid.NewGuid().ToString("N") + ".txt"));

        return new LauncherViewModel(
            state, new FakeReleaseCatalog(), launcher, setup,
            install, new InstallOptions(), theme);
    }

    [Fact]
    public void Nothing_Installed_Offers_The_Press_And_Holds_The_Doors() {
        var vm = Launcher(out _, out _, out _, out _);

        Assert.False(vm.IsInstalled);
        Assert.Equal("Not installed", vm.StatusBadge);
        Assert.Equal("Ready to install.", vm.StatusMessage);
        Assert.False(vm.IsDetailsOpen);
        Assert.True(vm.OpenCommand.CanExecute(null));
        Assert.False(vm.RepairCommand.CanExecute(null));
        Assert.False(vm.LaunchCommand.CanExecute(null));
        Assert.False(vm.CancelCommand.CanExecute(null));
    }

    [Fact]
    public void Pressing_The_Card_Opens_The_Wizard_When_There_Is_Nothing_Yet() {
        var vm = Launcher(out _, out var setup, out _, out _);

        vm.OpenCommand.Execute(null);

        Assert.Equal(1, setup.ShowCount);
        Assert.True(setup.IsOpen);
        Assert.Equal("Opening setup...", vm.StatusMessage);

        // The wizard stands where the press left it: nothing is pressed
        // again until its window is gone.
        Assert.False(vm.IsDetailsOpen);
        Assert.False(vm.OpenCommand.CanExecute(null));
    }

    [Fact]
    public void Pressing_The_Card_Opens_The_Panel_When_Something_Is_Installed() {
        var vm = Launcher(out var state, out var setup, out _, out _);
        state.MarkInstalled(@"C:\Apps\Shiranuix\Shiranuix.exe");

        vm.OpenCommand.Execute(null);

        Assert.True(vm.IsDetailsOpen);
        Assert.Equal(0, setup.ShowCount);

        // The next press is the same press answering the other way: the
        // panel is shut again rather than a second one opened.
        vm.OpenCommand.Execute(null);
        Assert.False(vm.IsDetailsOpen);
    }

    [Fact]
    public void Closing_The_Wizard_Without_Installing_Leaves_The_Press_The_Door() {
        var vm = Launcher(out _, out var setup, out _, out _);

        vm.OpenCommand.Execute(null);
        setup.Close();

        Assert.Equal("Setup closed — nothing was installed.", vm.StatusMessage);
        Assert.False(vm.IsDetailsOpen);
        Assert.True(vm.OpenCommand.CanExecute(null));
    }

    [Fact]
    public void Closing_The_Wizard_After_A_Finish_Opens_The_Panel() {
        var vm = Launcher(out var state, out var setup, out _, out _);

        vm.OpenCommand.Execute(null);
        state.MarkInstalled(@"C:\Apps\Shiranuix\Shiranuix.exe");
        setup.Close();

        // A wizard that closed over an installation hands the panel its
        // open state, because that is where the doors onto it stand.
        Assert.Equal("Installed — Shiranuix is ready to use.", vm.StatusMessage);
        Assert.True(vm.IsDetailsOpen);
        Assert.True(vm.LaunchCommand.CanExecute(null));
    }

    [Fact]
    public async Task Repair_Walks_The_Install_Over_The_Folder_The_Receipt_Names() {
        var vm = Launcher(out var state, out var setup, out _, out var install);
        state.MarkInstalled(@"C:\Apps\Shiranuix\Shiranuix.exe");

        await vm.RepairCommand.ExecuteAsync(null);

        // Repair is the install service's own walk — the bytes come up
        // beside the unpacking rather than in a fetch of its own — over
        // the folder the receipt names, so the program never quietly
        // moves; the launcher stays exactly where it is, with a line
        // saying the work is over.
        Assert.Equal(1, install.Calls);
        Assert.Equal(0, setup.ShowCount);
        Assert.Equal(@"C:\Apps\Shiranuix", install.Received!.Destination);
        Assert.True(vm.IsInstalled);
        Assert.Equal("Repair complete — Shiranuix is ready to use.", vm.StatusMessage);
    }

    [Fact]
    public async Task Cancel_Stops_The_Repair_That_Is_Running_And_Says_So() {
        var vm = Launcher(out var state, out _, out _, out var install);
        state.MarkInstalled(@"C:\Apps\Shiranuix\Shiranuix.exe");
        install.Gate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var running = vm.RepairCommand.ExecuteAsync(null);
        await install.Started.Task;

        Assert.True(vm.IsWorking);
        Assert.Equal("Repairing Shiranuix...", vm.StatusMessage);
        Assert.True(vm.CancelCommand.CanExecute(null));
        Assert.False(vm.OpenCommand.CanExecute(null));

        vm.CancelCommand.Execute(null);
        await install.CancelObserved.Task;
        await running;

        Assert.Equal("Repair cancelled.", vm.StatusMessage);
        Assert.False(vm.IsWorking);
        Assert.True(vm.RepairCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_Repair_That_Fails_Says_Try_Again_And_Stays_Installed() {
        var vm = Launcher(out var state, out _, out _, out var install);
        state.MarkInstalled(@"C:\Apps\Shiranuix\Shiranuix.exe");
        install.Fail = true;

        await vm.RepairCommand.ExecuteAsync(null);

        Assert.Equal("The repair failed — please try again.", vm.StatusMessage);
        Assert.False(vm.IsWorking);
        Assert.True(vm.IsInstalled);
        Assert.True(vm.RepairCommand.CanExecute(null));
    }

    [Fact]
    public async Task Launch_Starts_What_Is_Installed_And_Says_It_Runs() {
        var vm = Launcher(out var state, out _, out var launcher, out _);
        state.MarkInstalled(@"C:\Apps\Shiranuix\Shiranuix.exe");

        await vm.LaunchCommand.ExecuteAsync(null);

        Assert.Equal(@"C:\Apps\Shiranuix\Shiranuix.exe", launcher.Started);
        Assert.Equal("Shiranuix is running.", vm.StatusMessage);
    }

    [Fact]
    public async Task Launch_That_Fails_Says_So_In_The_Status_Line() {
        var vm = Launcher(out var state, out _, out var launcher, out _);
        state.MarkInstalled(@"C:\Apps\Shiranuix\Shiranuix.exe");
        launcher.Fail = true;

        await vm.LaunchCommand.ExecuteAsync(null);

        Assert.Equal("Couldn't start Shiranuix — try Repair.", vm.StatusMessage);
    }

    [Fact]
    public void While_The_Wizard_Stands_Open_No_Door_Is_Offered() {
        var vm = Launcher(out var state, out var setup, out _, out _);
        state.MarkInstalled(@"C:\Apps\Shiranuix\Shiranuix.exe");

        setup.Show();

        Assert.False(vm.OpenCommand.CanExecute(null));
        Assert.False(vm.RepairCommand.CanExecute(null));
        Assert.False(vm.LaunchCommand.CanExecute(null));

        setup.Close();

        Assert.True(vm.OpenCommand.CanExecute(null));
        Assert.True(vm.RepairCommand.CanExecute(null));
        Assert.True(vm.LaunchCommand.CanExecute(null));
    }

    [Fact]
    public void The_Launcher_Says_The_Version_The_Catalog_Answers_With() {
        var vm = Launcher(out _, out _, out _, out _);

        Assert.Equal("9.9.9", vm.Version);
    }
}
