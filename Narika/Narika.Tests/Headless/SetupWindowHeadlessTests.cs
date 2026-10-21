using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Narika.Services.Install;
using Narika.Tests.Fakes;
using Narika.ViewModels;
using Narika.ViewModels.Pages;
using Narika.Views.Pages;

namespace Narika.Tests.Headless;

/// <summary>
/// Tests for <see cref="SetupWindow"/> shown headless: the wizard's window is
/// the running application's own, so the page a person would first see is the
/// one the application's templates draw — and the walk between pages is the
/// window's one content control answering, not a view model talked to against
/// an empty room.
/// </summary>
public sealed class SetupWindowHeadlessTests {
    [AvaloniaFact]
    public void The_Wizard_Stands_On_The_Folder_Page_And_Turns_To_The_Options() {
        var vm = Wizard();

        var window = new SetupWindow(vm);
        window.Show();
        window.UpdateLayout();

        // The content control draws the page the walk stands on from the
        // application's own data templates: the folder page, with the
        // catalog's line over the box.
        Assert.NotEmpty(window.GetVisualDescendants().OfType<DestinationPage>());
        Assert.Contains("9.9.9", vm.Destination.IntroLine, StringComparison.Ordinal);

        vm.NextCommand.Execute(null);
        window.UpdateLayout();

        // The same window, the same control, the next page: the walk turns
        // and the room furnishes itself again.
        Assert.NotEmpty(window.GetVisualDescendants().OfType<OptionsPage>());

        window.Close();
    }

    /// <summary>
    /// Builds a wizard over its fakes the way the container would — the four
    /// pages, the shared request they write into, and the walk's services —
    /// one fresh walk, as every opening of the window gets.
    /// </summary>
    private static InstallerViewModel Wizard() {
        var options = new InstallOptions { Destination = @"C:\Apps\Shiranuix" };

        return new InstallerViewModel(
            new DestinationPageViewModel(options, new FakeFolderPickerService()),
            new OptionsPageViewModel(options),
            new ProgressPageViewModel(new FakeInstallService(), options),
            new FinishPageViewModel(options),
            new FakeReleaseCatalog(),
            new FakeAppLauncher(),
            new InMemoryInstallState());
    }
}
