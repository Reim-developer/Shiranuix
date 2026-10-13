using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Shiranuix.Services;

/// <summary>
/// A blocking yes/no prompt shown as a dialog over the main window.
/// </summary>
/// <remarks>
/// Deliberately hand-built rather than pulling in a dialog package: the prompt
/// asks one question and offers two buttons, and a writing tool should not gain
/// a dependency to say "discard?".
/// </remarks>
public sealed class WindowConfirmService : IConfirmDialogService {
    private readonly Window _window;

    /// <summary>
    /// Creates the service over a window.
    /// </summary>
    /// <param name="window">The window to centre the dialog on.</param>
    public WindowConfirmService(Window window) =>
        _window = window ?? throw new ArgumentNullException(nameof(window));

    /// <inheritdoc />
    public async Task<bool> ConfirmDiscardAsync(string fileName) {
        var keep = new Button {
            Content = "Keep editing",
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };
        var discard = new Button {
            Content = "Discard",
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };

        var dialog = new Window {
            Title = "Unsaved changes",
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            MaxWidth = 440,
            ShowInTaskbar = false,
            Content = new StackPanel {
                Margin = new Thickness(22, 18),
                Spacing = 18,
                Children = {
                    new TextBlock {
                        Text = $"\"{fileName}\" has unsaved changes.\nDiscard them?",
                        TextWrapping = TextWrapping.Wrap
                    },
                    new StackPanel {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        Spacing = 8,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Children = { keep, discard }
                    }
                }
            }
        };

        keep.Click += (_, _) => dialog.Close(false);
        discard.Click += (_, _) => dialog.Close(true);

        return await dialog.ShowDialog<bool>(_window);
    }
}
