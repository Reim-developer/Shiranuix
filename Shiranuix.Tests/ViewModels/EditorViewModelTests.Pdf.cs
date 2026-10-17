using System.Text;
using Shiranuix.Tests.Fakes;
using Shiranuix.ViewModels;

namespace Shiranuix.Tests.ViewModels;

/// <summary>
/// Tests for the export-to-PDF flow: what it asks, what it writes, and
/// what the writer hears when the writing could not be done.
/// </summary>
public sealed partial class EditorViewModelTests {
    [Fact]
    public async Task Exporting_Writes_A_Pdf_Wherever_The_Writer_Points() {
        var viewModel = CreateViewModel(withExistingFile: true);

        var home = Path.Combine(Path.GetTempPath(), $"shiranuix-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(home);
        var pdf = Path.Combine(home, "story.pdf");
        try {
            _dialogs.SaveResult = pdf;

            await viewModel.ExportPdfCommand.ExecuteAsync(null);

            // The file exists and claims its format: the header a PDF file
            // must carry, whatever holds inside beyond it.
            var written = File.ReadAllBytes(pdf);
            Assert.Equal("%PDF", Encoding.ASCII.GetString(written, 0, 4));

            // The dialog was primed with the document's own name, in the
            // paper's own ending.
            Assert.Equal("story.pdf", Assert.Single(_dialogs.SuggestedNames));

            // And the writer hears it was done, the way a save tells.
            Assert.Contains("story.pdf", viewModel.StatusMessage);
        } finally {
            Directory.Delete(home, recursive: true);
        }
    }

    [Fact]
    public async Task An_Export_Of_A_Dialog_Cancelled_Writes_Nothing() {
        var viewModel = CreateViewModel(withExistingFile: true);
        var before = viewModel.StatusMessage;

        _dialogs.SaveResult = null;

        await viewModel.ExportPdfCommand.ExecuteAsync(null);

        Assert.Equal(before, viewModel.StatusMessage);
    }
}
