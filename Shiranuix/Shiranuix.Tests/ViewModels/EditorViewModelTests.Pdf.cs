using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Shiranuix.Models;
using Shiranuix.Tests.Fakes;
using Shiranuix.ViewModels;

namespace Shiranuix.Tests.ViewModels;

/// <summary>
/// Tests for the export-to-PDF flow: what it asks, what it writes, and
/// what the writer hears when the writing could not be done.
/// </summary>
/// <remarks>
/// The facts that need paper to land stand where a face must really be
/// found: the pen asks this machine's own shelf for a file to carry each
/// family — the registry on Windows, folders on the other systems — and
/// whether those folders hold what a test names is not ours to assume, so
/// off Windows the writing may find no face at all — and those tests say
/// so and stand down rather than failing on faces the shelf never held.
/// The asking is answered everywhere.
/// </remarks>
public sealed partial class EditorViewModelTests {
    [WindowsFact]
    public async Task Exporting_Writes_A_Pdf_Wherever_The_Writer_Points() {
        var viewModel = CreateViewModel(withExistingFile: true);
        _exportPdf.Result = new PdfExportOptions(
            TypographyState.DefaultFontFamily, TypographyState.DefaultFontSize);

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

            // The file dialog was primed with the document's own name, in
            // the paper's own ending.
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
        _exportPdf.Result = new PdfExportOptions(
            TypographyState.DefaultFontFamily, TypographyState.DefaultFontSize);

        _dialogs.SaveResult = null;

        await viewModel.ExportPdfCommand.ExecuteAsync(null);

        // The chooser went first all the same: the export is two questions
        // asked in order, and the second was asked.
        Assert.Single(_exportPdf.Asked);
        Assert.Equal(before, viewModel.StatusMessage);
    }

    [Fact]
    public async Task A_Chooser_Cancelled_Stops_The_Export_At_Its_Own_Door() {
        var viewModel = CreateViewModel(withExistingFile: true);
        var before = viewModel.StatusMessage;

        // The chooser answered with nothing: the user walked away from the
        // typeface question, so the file question is never asked of them and
        // nothing is written anywhere.
        _exportPdf.Result = null;

        await viewModel.ExportPdfCommand.ExecuteAsync(null);

        Assert.Equal(before, viewModel.StatusMessage);
        Assert.Empty(_dialogs.SuggestedNames);
    }

    [Fact]
    public async Task An_Export_Asks_Its_Chooser_As_The_Machine_And_Page_Stand() {
        var viewModel = CreateViewModel(withExistingFile: true);
        _exportPdf.Result = new PdfExportOptions(
            TypographyState.DefaultFontFamily, TypographyState.DefaultFontSize);

        var home = Path.Combine(Path.GetTempPath(), $"shiranuix-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(home);
        try {
            _dialogs.SaveResult = Path.Combine(home, "story.pdf");

            await viewModel.ExportPdfCommand.ExecuteAsync(null);

            // One ask, and it is made of what the store has never carried:
            // opened on the page's own typeface and size, offered every face
            // the machine holds — the same list the toolbar's picker reads —
            // and the sizes the picker's ladder runs in.
            var ask = Assert.Single(_exportPdf.Asked);
            Assert.Equal(
                new PdfExportOptions(TypographyState.DefaultFontFamily, TypographyState.DefaultFontSize),
                ask.Current);
            Assert.Equal(viewModel.Typography.AvailableFonts, ask.Fonts);
            Assert.Equal(viewModel.Typography.FontSizes, ask.Sizes);
        } finally {
            Directory.Delete(home, recursive: true);
        }
    }

    [Fact]
    public async Task The_Chooser_Opens_On_What_The_Last_Export_Remembered_Not_On_The_Page() {
        _settings.Stored = new UserSettings {
            FontFamily = "Verdana",
            FontSize = 11,
            ThemeMode = ThemeMode.Dark,
            WordWrap = true,
            TextAlign = TextAlign.Right,
            PdfFontFamily = "Times New Roman",
            PdfFontSize = 12,
        };
        var viewModel = CreateViewModel(withExistingFile: true);
        _exportPdf.Result = new PdfExportOptions("Times New Roman", 12);

        var home = Path.Combine(Path.GetTempPath(), $"shiranuix-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(home);
        try {
            _dialogs.SaveResult = Path.Combine(home, "story.pdf");

            await viewModel.ExportPdfCommand.ExecuteAsync(null);

            // The dialog opened on the remembered shape, not on the page's
            // own: answering an export twice is the export remembering, and
            // a page set differently does not talk it out of that.
            Assert.Equal(
                new PdfExportOptions("Times New Roman", 12),
                Assert.Single(_exportPdf.Asked).Current);
        } finally {
            Directory.Delete(home, recursive: true);
        }
    }

    [WindowsFact]
    public async Task A_Choice_The_Chooser_Made_Is_Remembered_When_The_Paper_Lands() {
        var viewModel = CreateViewModel(withExistingFile: true);
        _exportPdf.Result = new PdfExportOptions("Times New Roman", 14);

        var home = Path.Combine(Path.GetTempPath(), $"shiranuix-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(home);
        try {
            _dialogs.SaveResult = Path.Combine(home, "story.pdf");

            await viewModel.ExportPdfCommand.ExecuteAsync(null);

            // The store now carries the shape the paper was drawn in: the
            // next export's chooser opens on it.
            Assert.Equal("Times New Roman", _settings.Stored!.PdfFontFamily);
            Assert.Equal(14, _settings.Stored.PdfFontSize);
        } finally {
            Directory.Delete(home, recursive: true);
        }
    }

    [Fact]
    public async Task A_Choice_Of_A_Writing_That_Failed_Is_Not_Remembered() {
        var viewModel = CreateViewModel(withExistingFile: true);
        _exportPdf.Result = new PdfExportOptions("Times New Roman", 14);

        // A save point nothing can land on — the folder is not there, so
        // the writing fails the way it fails for a bad path.
        _dialogs.SaveResult = Path.Combine(
            Path.GetTempPath(), $"shiranuix-export-{Guid.NewGuid():N}", "no-such-folder", "story.pdf");

        await viewModel.ExportPdfCommand.ExecuteAsync(null);

        // A choice that reached nothing is not one the next export opens
        // on: the store was never written, and the status carries the flaw.
        Assert.Null(_settings.Stored);
        Assert.StartsWith("Could not write PDF:", viewModel.StatusMessage);
    }

    [Fact]
    public async Task The_Whole_Writing_Asked_Of_The_Document_Is_The_File_Not_The_Window() {
        var viewModel = CreateViewModel();
        // Two hundred lines — four windows over: the window holds fifty, and
        // the file holds them all.
        var whole = string.Join(
            "\n", Enumerable.Range(1, 200).Select(index => $"Dòng thứ {index}: một câu có dấu ế ộ ừ ỹ."));
        _files.Files[ExistingPath] = whole;
        await viewModel.OpenFromCommandLineAsync(ExistingPath);

        Assert.Equal(whole, viewModel.Document.WholeText());
    }

    [WindowsFact]
    public async Task An_Export_Tells_The_Paper_The_File_Itself_Not_The_Window() {
        var viewModel = CreateViewModel();
        var whole = string.Join(
            "\n", Enumerable.Range(1, 200).Select(index => $"Dòng thứ {index}: một câu có dấu ế ộ ừ ỹ."));
        _files.Files[ExistingPath] = whole;
        await viewModel.OpenFromCommandLineAsync(ExistingPath);

        _exportPdf.Result = new PdfExportOptions(
            TypographyState.DefaultFontFamily, TypographyState.DefaultFontSize);

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
