using Avalonia.Controls;
using Avalonia.Interactivity;
using Shiranuix.Models;
using Shiranuix.ViewModels.Dialogs;

namespace Shiranuix.Views;

/// <summary>
/// The window the export asks its question in: the typeface and the size the
/// paper will be drawn in.
/// </summary>
/// <remarks>
/// The window holds nothing of its own judgement — the chosen options are
/// built out of what the view model collected, and closed into the answer the
/// export flow waits on. Cancelled, the window answers nothing, and the
/// export does nothing at all.
/// </remarks>
public partial class ExportPdfDialog : Window {
    /// <summary>
    /// Builds the dialog over the export's own state.
    /// </summary>
    /// <param name="viewModel">What the dialog binds: the options and the offers.</param>
    public ExportPdfDialog(ExportPdfDialogViewModel viewModel) {
        InitializeComponent();
        FontPreviewWatcher.Watch(FontCombo, FontPreview, FontPreviewSample);
        DataContext = viewModel;
    }

    /// <summary>
    /// The constructor the XAML previewer and the runtime loader build the
    /// window with.
    /// </summary>
    /// <remarks>
    /// Shows nothing worth answering — no options ever arrive. The running
    /// application builds the window through the constructor above, with the
    /// dialog's state already in hand.
    /// </remarks>
    public ExportPdfDialog() {
        InitializeComponent();
        FontPreviewWatcher.Watch(FontCombo, FontPreview, FontPreviewSample);
    }

    /// <summary>
    /// Answers with what was chosen, and the window goes.
    /// </summary>
    private void OnExport(object? sender, RoutedEventArgs e) {
        if (DataContext is not ExportPdfDialogViewModel viewModel) {
            return;
        }

        Close(new PdfExportOptions(viewModel.FontFamily, viewModel.FontSize));
    }

    /// <summary>
    /// Answers nothing: the export is cancelled, and nothing writes.
    /// </summary>
    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
