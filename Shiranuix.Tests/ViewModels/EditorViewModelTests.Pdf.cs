using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
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

    [Fact]
    public void The_Whole_Writing_Asked_Of_The_Document_Is_The_File_Not_The_Window() {
        var viewModel = CreateViewModel();
        // Two hundred lines — four windows over: the window holds fifty, and
        // the file holds them all.
        var whole = string.Join(
            "\n", Enumerable.Range(1, 200).Select(index => $"Dòng thứ {index}: một câu có dấu ế ộ ừ ỹ."));
        _files.Files[ExistingPath] = whole;
        viewModel.OpenFromCommandLine(ExistingPath);

        Assert.Equal(whole, viewModel.Document.WholeText());
    }

    [Fact]
    public async Task An_Export_Tells_The_Paper_The_File_Itself_Not_The_Window() {
        var viewModel = CreateViewModel();
        var whole = string.Join(
            "\n", Enumerable.Range(1, 200).Select(index => $"Dòng thứ {index}: một câu có dấu ế ộ ừ ỹ."));
        _files.Files[ExistingPath] = whole;
        viewModel.OpenFromCommandLine(ExistingPath);

        var pdf = Path.Combine(Path.GetTempPath(), $"shiranuix-export-{Guid.NewGuid():N}.pdf");
        _dialogs.SaveResult = pdf;
        try {
            await viewModel.ExportPdfCommand.ExecuteAsync(null);

            // The pages say more than a window of fifty lines can carry: the
            // window lands as three pages, the file as many more. The count
            // is read from the page tree itself, where the only writer of
            // these files records it; the record splits its tokens with
            // whatever whitespace the spec allows, so the ask forgives
            // spaces.
            var tree = Regex.Match(File.ReadAllText(pdf, Encoding.Latin1), "/Type/Pages\\s*/Count (\\d+)");
            Assert.True(tree.Success);
            Assert.True(int.Parse(tree.Groups[1].Value, CultureInfo.InvariantCulture) >= 4);
        } finally {
            File.Delete(pdf);
        }
    }
}
