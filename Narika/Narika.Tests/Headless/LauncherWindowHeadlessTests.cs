using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Narika.Services.Install;
using Narika.Services.Theme;
using Narika.Tests.Fakes;
using Narika.ViewModels;

// Which application the headless tests are given: the launcher's own — the
// very constructor, XAML, styles and templates the running program opens
// with — because a window drawn over any lesser application would prove
// nothing about this one. The session boots it on its own dispatcher
// thread, freshly per test, and every press below lands on the window's
// real input pipeline rather than on a view model method called directly.
[assembly: AvaloniaTestApplication(typeof(Narika.App))]

namespace Narika.Tests.Headless;

/// <summary>
/// Tests for <see cref="MainWindow"/> shown on Avalonia's headless engine: no
/// display is opened, but the window is the running application's own — its
/// XAML compiled, its styles loaded, its templates in place — and the presses
/// below are pointer presses through the input pipeline, so what they prove is
/// what a person's hand would find.
/// </summary>
public sealed class LauncherWindowHeadlessTests {
    /// <summary>
    /// Builds a launcher over its fakes, the two a window test asks for
    /// handed back so the test can watch what the press reaches.
    /// </summary>
    private static LauncherViewModel Launcher(
        out InMemoryInstallState state,
        out FakeSetupWindowService setup) {
        state = new InMemoryInstallState();
        setup = new FakeSetupWindowService();

        var theme = new ThemeService(
            _ => { },
            Path.Combine(
                Path.GetTempPath(), "narika-headless", Guid.NewGuid().ToString("N") + ".txt"));

        return new LauncherViewModel(
            state, new FakeReleaseCatalog(), setup,
            new FakeInstallService(), new FakeUninstallService(),
            new InstallOptions(), theme);
    }

    [AvaloniaFact]
    public void A_Press_On_The_Standing_Card_Opens_The_Panel_With_Its_Doors() {
        var vm = Launcher(out _, out _);
        var window = new MainWindow(vm);
        window.Show();

        // One card standing alone: the panel behind it stays shut until the
        // press the hint under the words promises arrives.
        var card = window.FindControl<Border>("Card");
        var panel = window.FindControl<Border>("Panel");
        Assert.NotNull(card);
        Assert.NotNull(panel);
        Assert.False(panel.IsVisible);

        Press(window, card!);

        // The press is answered: the panel stands with the badge saying what
        // is on disk, and the three doors on it.
        Assert.True(vm.IsDetailsOpen);
        Assert.True(panel.IsVisible);
        Assert.Equal("Not installed", vm.StatusBadge);

        // The doors are shut the way a person sees them shut: the two that
        // need an installation stand unpressable while nothing is installed.
        Assert.True(Door(window, vm.DownloadCommand).IsEffectivelyEnabled);
        Assert.False(Door(window, vm.RepairCommand).IsEffectivelyEnabled);
        Assert.False(Door(window, vm.UninstallCommand).IsEffectivelyEnabled);

        window.Close();
    }

    [AvaloniaFact]
    public void An_Installation_Stands_Three_Open_Doors_And_Download_Takes_The_Wizard() {
        var vm = Launcher(out var state, out var setup);
        state.MarkInstalled(@"C:\Apps\Shiranuix\Shiranuix.exe");
        var window = new MainWindow(vm);
        window.Show();

        var card = window.FindControl<Border>("Card");
        Assert.NotNull(card);
        Press(window, card!);

        Assert.True(Door(window, vm.DownloadCommand).IsEffectivelyEnabled);
        Assert.True(Door(window, vm.RepairCommand).IsEffectivelyEnabled);
        Assert.True(Door(window, vm.UninstallCommand).IsEffectivelyEnabled);

        // The door itself, pressed the way the card was: Download is the
        // wizard's door — it opens the walk, and the panel shuts behind it.
        Press(window, Door(window, vm.DownloadCommand));

        Assert.Equal(1, setup.ShowCount);
        Assert.False(vm.IsDetailsOpen);

        window.Close();
    }

    /// <summary>
    /// Presses the middle of a control the way a pointer would — down and up
    /// over the same spot, through the window's own input pipeline. The
    /// window is settled and one frame turned over first, because the engine
    /// answers a press from what was actually rendered, so the spot is where
    /// the control really stands and what is really drawn.
    /// </summary>
    private static void Press(Window window, Control target) {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        Assert.True(
            target.Bounds.Width > 0 && target.Bounds.Height > 0,
            $"target has no bounds: {target.Bounds}");
        var point = target.TranslatePoint(
            new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window);
        Assert.NotNull(point);

        window.MouseDown(point.Value, MouseButton.Left);
        window.MouseUp(point.Value, MouseButton.Left);
    }

    /// <summary>
    /// Finds one of the three doors by the command its binding carries, which
    /// is surer than any position on the panel: the binding is the door's
    /// identity, and the button drawn over it is what presses reach.
    /// </summary>
    private static Button Door(MainWindow window, ICommand command) =>
        window.GetVisualDescendants().OfType<Button>()
            .First(button => ReferenceEquals(button.Command, command));
}
