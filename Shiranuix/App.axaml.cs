using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Shiranuix.Views;

namespace Shiranuix;

public partial class App : Application {
    public override void Initialize() {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted() {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            // The window builds its own view model (it owns the dialog
            // services that need a live window), so there is nothing to
            // inject here.
            var window = new MainWindow();
            desktop.MainWindow = window;

            // `Shiranuix path\to\file.txt` opens that file straight away, which
            // is what double-clicking a document in Explorer does. The first
            // argument that names a real file wins; anything else is ignored.
            if (desktop.Args is { } args) {
                foreach (var arg in args) {
                    if (File.Exists(arg)) {
                        window.OpenStartupFile(arg);
                        break;
                    }
                }
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
