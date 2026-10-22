using Shiranuix.CoreFormat;
using Shiranuix.ViewModels;

namespace Shiranuix.Tests.ViewModels;

/// <summary>
/// Tests for what happens to a file that arrives naming a typeface this
/// machine does not carry: it is asked about before a single word lands,
/// and nothing lands at all if the question is called off.
/// </summary>
/// <remarks>
/// The rules worth pinning down here are the ones a screenshot can never
/// confirm — that the question is asked with the file's own words rather
/// than a stranger's, that a face the machine does have is never asked
/// about, and that choosing not to open leaves the page exactly as it was.
/// </remarks>
public sealed partial class EditorViewModelTests {
    private const string SetPath = "set.shiranuix";
    private const string SetText = "Mở đầu của truyện\nTiêu đề chương một";

    [Fact]
    public async Task A_File_Set_In_A_Face_This_Machine_Lacks_Is_Asked_Before_It_Lands() {
        // "Palatino Linotype" is not in the fake catalog's short list, so
        // the file has to be settled with whoever is opening it — with the
        // file's own first line and the size it stands at, since those are
        // the only things that say whether another face will read right.
        var viewModel = SetFile("Palatino Linotype", 21.5);
        _missingFonts.Answer = "Verdana";

        await viewModel.OpenFromCommandLineAsync(SetPath);

        var asked = Assert.Single(_missingFonts.Asked);
        Assert.Equal("Palatino Linotype", asked.Missing);
        Assert.Equal(21.5, asked.Size);
        Assert.Equal("Mở đầu của truyện", asked.Sample);
        Assert.Equal("Georgia", asked.Initial);

        Assert.Equal(SetText, viewModel.Document.Text);
        Assert.Equal(new DocumentTypography("Verdana", 21.5), viewModel.Document.Typography);
    }

    [Fact]
    public async Task A_File_Set_In_A_Face_This_Machine_Does_Have_Is_Never_Asked_About() {
        var viewModel = SetFile("Verdana", 14);

        await viewModel.OpenFromCommandLineAsync(SetPath);

        Assert.Equal(0, _missingFonts.Count);
        Assert.Equal(new DocumentTypography("Verdana", 14), viewModel.Document.Typography);
    }

    [Fact]
    public async Task A_Face_The_File_Names_Matches_What_This_Machine_Calls_It() {
        // Typeface names answer to either case on the machines they come
        // from, and a file written elsewhere must not be asked about just
        // because it spelled a name differently.
        var viewModel = SetFile("gEoRgIa", 12);

        await viewModel.OpenFromCommandLineAsync(SetPath);

        Assert.Equal(0, _missingFonts.Count);
        Assert.Equal("gEoRgIa", viewModel.Document.Typography.FontFamily);
    }

    [Fact]
    public async Task Choosing_Not_To_Open_Leaves_The_Page_Where_It_Stood() {
        // A dialog answered with nothing is not a failure — nothing went
        // wrong, and nothing happened either. The words stay the ones they
        // were and the status line says so, rather than claiming the file
        // could not be opened.
        var viewModel = SetFile("Palatino Linotype", 21.5);
        _files.Files["untouched.txt"] = "page one";
        viewModel.Document.Open("untouched.txt");

        _missingFonts.Answer = null;
        var outcome = await viewModel.OpenPathAsync(SetPath);

        Assert.Equal(1, _missingFonts.Count);
        Assert.False(outcome.Succeeded);
        Assert.True(outcome.WasCalledOff);
        Assert.Equal("Opening was cancelled.", viewModel.StatusMessage);

        Assert.Equal("page one", viewModel.Document.Text);
        Assert.Equal("untouched.txt", viewModel.Document.Path);
    }

    [Fact]
    public async Task A_File_With_No_Face_Of_Its_Own_Is_Not_Asked_About() {
        // A plain file is letters and nothing else: there is no face to
        // miss, so there is no face to pick. The document keeps the setting
        // it already stands on.
        _files.Files["plain.txt"] = SetText;
        var viewModel = CreateViewModel();

        await viewModel.OpenFromCommandLineAsync("plain.txt");

        Assert.Equal(0, _missingFonts.Count);
        Assert.Equal("Georgia", viewModel.Document.Typography.FontFamily);
        Assert.Equal(17, viewModel.Document.Typography.FontSize);
    }

    [Fact]
    public async Task Opening_A_File_With_A_Face_This_Machine_Lacks_Writes_The_Face_Chosen() {
        // The face the dialog is answered with is the face the file goes on
        // to be set in — the words and the setting arrive together, rather
        // than the setting following them onto the page.
        var viewModel = SetFile("Palatino Linotype", 21.5);
        _missingFonts.Answer = "Consolas";

        await viewModel.OpenFromCommandLineAsync(SetPath);

        Assert.Equal(new DocumentTypography("Consolas", 21.5), viewModel.Document.Typography);
    }

    /// <summary>
    /// A view model over a fake disk holding one file of the app's own,
    /// carrying a typeface this machine does not have.
    /// </summary>
    /// <param name="family">The typeface the file carries.</param>
    /// <param name="size">The point size the file stands at.</param>
    /// <returns>The editor, with nothing open yet.</returns>
    private EditorViewModel SetFile(string family, double size) {
        _files.Files[SetPath] = SetText;
        _files.Typography[SetPath] = new DocumentTypography(family, size);
        return CreateViewModel();
    }
}
