using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Shiranuix.Views;

/// <summary>
/// Code-behind for the crash dialog.
/// </summary>
/// <remarks>
/// Nothing here is bound to anything: the constructor sets the four lines
/// the dialog speaks and that is the whole of its logic. The two actions
/// it offers are copying what a bug report needs — the code and the file,
/// one clipboard write — and closing, which Enter and Escape both do as
/// well as the button, because a dialog nobody can dismiss with the
/// keyboard they were just using is a small insult on top of the crash.
/// </remarks>
public partial class CrashDialog : Window {
    private readonly string _details = string.Empty;

    /// <summary>
    /// Builds the dialog over the incident it is about to speak.
    /// </summary>
    /// <param name="error">The exception that nobody caught.</param>
    /// <param name="code">The incident code, also written into the log
    /// beside the exception itself.</param>
    /// <param name="logFile">The file the details were written to.</param>
    /// <param name="closing">True when the application is on its way out
    /// and false when it kept running — the one line the dialog's verdict
    /// differs by.</param>
    public CrashDialog(Exception error, string code, string logFile, bool closing) {
        InitializeComponent();

        Summary.Text = $"{error.GetType().Name}: {error.Message}";
        IncidentCode.Text = code;
        Verdict.Text = closing
            ? "The application has to close now. Everything it knew about this is in the file below."
            : "The application is still running. Everything it knew about this is in the file below.";
        LogPath.Text = logFile;
        _details = $"{code}{Environment.NewLine}{logFile}";
    }

    /// <summary>
    /// The constructor the XAML previewer and the runtime loader build the
    /// window with.
    /// </summary>
    /// <remarks>
    /// A window whose only constructor takes arguments is one the
    /// previewer refuses to build and the loader warns about, so this
    /// overload exists for them — and it can show nothing worth reading,
    /// because there is no incident in hand. The running application
    /// never takes this path: the crash guard builds the dialog through
    /// the constructor above, with the exception, the code and the file
    /// already in it.
    /// </remarks>
    public CrashDialog() {
        InitializeComponent();
    }

    /// <summary>
    /// Puts the code and the file on the clipboard — the two things any
    /// report will be asked for — and says so on the button. Flushed as
    /// well as set, because the reader may copy and then watch the
    /// process go: what was copied has to outlive the dialog.
    /// </summary>
    private async void OnCopy(object? sender, RoutedEventArgs e) {
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard) {
            using var transfer = new DataTransfer();
            transfer.Add(DataTransferItem.CreateText(_details));

            await clipboard.SetDataAsync(transfer);
            await clipboard.FlushAsync();
            CopyButton.Content = "Copied";
        }
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
