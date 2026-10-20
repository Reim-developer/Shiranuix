using Narika.Services.Install;
using Narika.Tests.Fakes;
using Narika.ViewModels;
using Narika.ViewModels.Pages;

namespace Narika.Tests;

/// <summary>
/// Tests for <see cref="InstallerViewModel"/>: the walk a person takes
/// through setup, driven entirely against doubles — no window is opened,
/// no file is written, and every step the wizard is allowed to take is
/// asked of it first.
/// </summary>
public sealed class InstallerWizardTests {
    /// <summary>
    /// Builds a wizard over its fakes, every one of them handed back so a
    /// test can watch what the walk asks of them.
    /// </summary>
    /// <param name="install">The install service the walk ends up calling.</param>
    /// <param name="launcher">The launcher the finish page may ask for.</param>
    /// <param name="picker">The folder picker the destination page offers.</param>
    /// <param name="options">The shared install request, if a test has opinions about it.</param>
    /// <param name="releases">The catalog the welcome line asks.</param>
    private static InstallerViewModel Wizard(
        out FakeInstallService install,
        out FakeAppLauncher launcher,
        out FakeFolderPickerService picker,
        InstallOptions? options = null,
        FakeReleaseCatalog? releases = null) {
        options ??= new InstallOptions { Destination = @"C:\Apps\Shiranuix" };
        picker = new FakeFolderPickerService();
        install = new FakeInstallService();
        launcher = new FakeAppLauncher();

        var progress = new ProgressPageViewModel(install, options);

        return new InstallerViewModel(
            new WelcomePageViewModel(),
            new DestinationPageViewModel(options, picker),
            new OptionsPageViewModel(options),
            progress,
            new FinishPageViewModel(options),
            releases ?? new FakeReleaseCatalog(),
            launcher);
    }

    [Fact]
    public void The_Walk_Begins_At_The_Welcome_Page_With_Nowhere_To_Come_Back_From() {
        var vm = Wizard(out _, out _, out _);

        Assert.IsType<WelcomePageViewModel>(vm.CurrentPage);
        Assert.Equal("Next", vm.NextContent);
        Assert.True(vm.NextCommand.CanExecute(null));
        Assert.False(vm.BackCommand.CanExecute(null));
    }

    [Fact]
    public void Next_Walks_The_Pages_Forward_And_Back_Walks_The_Same_Road_In_Reverse() {
        var vm = Wizard(out _, out _, out _);

        vm.NextCommand.Execute(null);
        Assert.IsType<DestinationPageViewModel>(vm.CurrentPage);
        Assert.True(vm.BackCommand.CanExecute(null));

        vm.NextCommand.Execute(null);
        Assert.IsType<OptionsPageViewModel>(vm.CurrentPage);
        Assert.Equal("Install", vm.NextContent);

        vm.BackCommand.Execute(null);
        Assert.IsType<DestinationPageViewModel>(vm.CurrentPage);

        vm.BackCommand.Execute(null);
        Assert.IsType<WelcomePageViewModel>(vm.CurrentPage);
        Assert.False(vm.BackCommand.CanExecute(null));
    }

    [Fact]
    public void The_Destination_Page_Withholds_Next_Until_It_Has_A_Folder() {
        var options = new InstallOptions { Destination = string.Empty };
        var vm = Wizard(out _, out _, out _, options);

        vm.NextCommand.Execute(null);
        Assert.False(vm.NextCommand.CanExecute(null));

        vm.Destination.DestinationPath = @"C:\Apps\Shiranuix";
        Assert.True(vm.NextCommand.CanExecute(null));

        vm.Destination.DestinationPath = "   ";
        Assert.False(vm.NextCommand.CanExecute(null));
    }

    [Fact]
    public void Browse_Takes_The_Picked_Folder_And_Names_The_Editors_Own_Inside_It() {
        var options = new InstallOptions { Destination = string.Empty };
        var vm = Wizard(out _, out _, out var picker, options);
        picker.Answer = @"D:\Tools";

        vm.NextCommand.Execute(null);
        vm.Destination.BrowseCommand.Execute(null);

        Assert.Equal(Path.Combine(@"D:\Tools", "Shiranuix"), vm.Destination.DestinationPath);
        Assert.Equal(Path.Combine(@"D:\Tools", "Shiranuix"), options.Destination);
    }

    [Fact]
    public void The_Options_Page_Writes_Its_Choices_Into_The_Shared_Request() {
        var options = new InstallOptions { Destination = @"C:\Apps\Shiranuix" };
        var vm = Wizard(out _, out _, out _, options);
        vm.NextCommand.Execute(null);
        vm.NextCommand.Execute(null);

        var page = Assert.IsType<OptionsPageViewModel>(vm.CurrentPage);
        page.CreateDesktopShortcut = false;

        Assert.False(options.CreateDesktopShortcut);
        Assert.True(options.CreateStartMenuShortcut);
    }

    [Fact]
    public async Task Install_Carries_Whatever_The_Walk_Collected_And_Reports_It_Finished() {
        var options = new InstallOptions { Destination = @"C:\Apps\Shiranuix" };
        var vm = Wizard(out var install, out _, out _, options);

        vm.NextCommand.Execute(null);
        vm.NextCommand.Execute(null);
        vm.NextCommand.Execute(null);

        Assert.IsType<ProgressPageViewModel>(vm.CurrentPage);
        await vm.Progress.Installing;

        Assert.Same(options, install.Received);
        Assert.True(vm.Progress.IsFinished);
        Assert.Equal(100, vm.Progress.Percent);
        Assert.Equal("Next", vm.NextContent);
        Assert.True(vm.NextCommand.CanExecute(null));
    }

    [Fact]
    public async Task The_Installing_Page_Offers_No_Way_Back_Either_While_Running_Or_After() {
        var vm = Wizard(out var install, out _, out _);
        install.Gate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        vm.NextCommand.Execute(null);
        vm.NextCommand.Execute(null);
        vm.NextCommand.Execute(null);

        Assert.False(vm.BackCommand.CanExecute(null));
        Assert.False(vm.NextCommand.CanExecute(null));
        Assert.Equal("Installing...", vm.NextContent);

        install.Gate.TrySetResult();
        await vm.Progress.Installing;

        Assert.True(vm.NextCommand.CanExecute(null));
        Assert.False(vm.BackCommand.CanExecute(null));
    }

    [Fact]
    public void Finish_Starts_The_Installed_Program_When_The_Box_Is_Still_Tick() {
        var vm = Wizard(out var install, out var launcher, out _);
        vm.NextCommand.Execute(null);
        vm.NextCommand.Execute(null);
        vm.NextCommand.Execute(null);
        vm.NextCommand.Execute(null);
        Assert.IsType<FinishPageViewModel>(vm.CurrentPage);

        var closed = false;
        vm.ShutdownRequested += (_, _) => closed = true;
        vm.NextCommand.Execute(null);

        Assert.True(closed);
        Assert.Equal(install.Outcome.ExecutablePath, launcher.Started);
    }

    [Fact]
    public void Finish_Keeps_The_Program_Closed_When_The_Box_Was_Unticked() {
        var vm = Wizard(out _, out var launcher, out _);
        vm.NextCommand.Execute(null);
        vm.NextCommand.Execute(null);
        vm.NextCommand.Execute(null);
        vm.NextCommand.Execute(null);

        var page = Assert.IsType<FinishPageViewModel>(vm.CurrentPage);
        page.LaunchWhenDone = false;

        var closed = false;
        vm.ShutdownRequested += (_, _) => closed = true;
        vm.NextCommand.Execute(null);

        Assert.True(closed);
        Assert.Null(launcher.Started);
    }

    [Fact]
    public async Task Cancel_Stops_The_Installation_That_Is_Running_And_Closes_The_Window() {
        var vm = Wizard(out var install, out _, out _);
        install.Gate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var closed = false;
        vm.ShutdownRequested += (_, _) => closed = true;

        vm.NextCommand.Execute(null);
        vm.NextCommand.Execute(null);
        vm.NextCommand.Execute(null);
        await install.Started.Task;

        vm.CancelCommand.Execute(null);
        await install.CancelObserved.Task;
        await vm.Progress.Installing;

        Assert.True(closed);
        Assert.Equal("Installation cancelled.", vm.Progress.Message);
        Assert.False(vm.Progress.IsFinished);
    }

    [Fact]
    public void The_Welcome_Page_Says_The_Version_The_Catalog_Answers_With() {
        var releases = new FakeReleaseCatalog {
            Release = new ReleaseAsset(
                "shiranuix", "9.9.9",
                new Uri("https://example.test/Shiranuix.zip"), "Shiranuix.zip")
        };
        var vm = Wizard(out _, out _, out _, releases: releases);

        Assert.Equal("9.9.9", vm.Welcome.Version);
        Assert.Contains("9.9.9", vm.Welcome.VersionLine, StringComparison.Ordinal);
    }
}
