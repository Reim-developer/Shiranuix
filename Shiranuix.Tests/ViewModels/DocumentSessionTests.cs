using Shiranuix.Models;
using Shiranuix.Tests.Fakes;
using Shiranuix.ViewModels;

namespace Shiranuix.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="DocumentSession"/>: the document's text, its path, the
/// moves between them, and what it remembers.
/// </summary>
/// <remarks>
/// Every collaborator is faked, so these run without a window, a file system,
/// or a prompt. The rules about when work is lost are the ones most worth
/// checking, and they are the ones a screenshot can never confirm. The
/// window's travel over a large file has a file of its own beside this one.
///
/// The session reports outcomes rather than strings, so the assertions below
/// check the facts and let the coordinator prove its own wording.
///
/// The class spans four files by topic: this one the lifecycle — new, open,
/// save, and what they remember; <c>DocumentSessionTests.Caret.cs</c> the
/// status bar's line and column and the input method's composition;
/// <c>DocumentSessionTests.Newlines.cs</c> the line-break conventions; and
/// <c>DocumentSessionTests.History.cs</c> undo, redo, and saves through the
/// ledger.
/// </remarks>
public sealed partial class DocumentSessionTests {
    private const string ExistingPath = SessionFactory.ExistingPath;
    private const string ExistingText = SessionFactory.ExistingText;

    private readonly SessionFactory _factory = new();

    private FakeFileService _files => _factory.Files;

    private FakeRecentFilesStore _recentStore => _factory.RecentStore;

    private DocumentSession CreateSession() => _factory.CreateSession();

    private DocumentSession CreateSessionWithFile() => _factory.CreateSessionWithFile();

    /// <summary>The convention that is not the one given.</summary>
    private static Newline OtherConvention(Newline current) =>
        current == Newline.Lf ? Newline.CrLf : Newline.Lf;

    [Fact]
    public void A_New_Session_Is_Clean_Untitled_And_Empty() {
        var session = CreateSession();

        Assert.Equal(string.Empty, session.Text);
        Assert.False(session.IsDirty);
        Assert.True(session.IsEmpty);
        Assert.Null(session.Path);
        Assert.Equal("Untitled", session.Name);
        Assert.False(session.HasRecentFiles);
    }

    [Fact]
    public void Typing_Sets_Dirty() {
        var session = CreateSession();

        session.Text = "Started writing.";

        Assert.True(session.IsDirty);
        Assert.False(session.IsEmpty);
    }

    [Fact]
    public void Reverting_To_Saved_Text_Clears_Dirty() {
        // Dirty is a comparison against the last read/write, not a latch that
        // once set stays set: backspacing your own edit returns you to clean.
        var session = CreateSessionWithFile();

        session.Text = "edit";
        Assert.True(session.IsDirty);

        session.Text = ExistingText;

        Assert.False(session.IsDirty);
    }

    [Fact]
    public void A_Lone_Space_Is_Not_An_Empty_Document() {
        var session = CreateSession();

        session.Text = " ";

        Assert.True(session.IsDirty);
        Assert.False(session.IsEmpty);
    }

    [Fact]
    public void Counts_Refresh_As_You_Type() {
        var session = CreateSession();

        // "two" (3) + space + "words" (5) = 9 UTF-16 units.
        session.Text = "two words";

        Assert.Equal(2, session.WordCount);
        Assert.Equal(9, session.CharacterCount);
        Assert.Equal(1, session.LineCount);
    }

    [Fact]
    public void New_Replaces_The_Document_With_A_Blank_One() {
        var session = CreateSessionWithFile();
        session.Text = "Đang viết dở";

        session.New();

        Assert.Equal(string.Empty, session.Text);
        Assert.Null(session.Path);
        Assert.False(session.IsDirty);
        Assert.Equal("Untitled", session.Name);
    }

    [Fact]
    public void New_Counts_The_Blank_Document_Not_The_Old_File() {
        // The counts and the caret's line follow the new document from its
        // first moment — not from the next keystroke.
        var session = CreateSessionWithFile();

        session.New();

        Assert.Equal(0, session.WordCount);
        Assert.Equal(0, session.CharacterCount);
        Assert.Equal(0, session.LineCount);
        Assert.Equal(1, session.CaretLine);
        Assert.Equal("1", session.LineNumbersText);
    }

    [Fact]
    public void Opening_A_File_Loads_It_And_Cleans_The_Document() {
        var session = CreateSessionWithFile();

        var outcome = session.Open(ExistingPath);

        Assert.True(outcome.Succeeded);
        Assert.Null(outcome.Detail);
        Assert.Equal(ExistingText, session.Text);
        Assert.Equal(ExistingPath, session.Path);
        Assert.Equal("story.txt", session.Name);
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void A_Failed_Open_Keeps_The_Current_Document() {
        var session = CreateSessionWithFile();
        session.Text = "Đang viết dở";

        var outcome = session.Open("missing.txt");

        Assert.False(outcome.Succeeded);
        Assert.NotNull(outcome.Detail);
        // The open document is untouched: the file was never read, so the
        // session has nothing to show for it.
        Assert.Equal("Đang viết dở", session.Text);
        Assert.Equal(ExistingPath, session.Path);
    }

    [Fact]
    public void Opening_A_File_Remembers_It() {
        _files.Files[ExistingPath] = ExistingText;
        var session = CreateSession();

        session.Open(ExistingPath);

        Assert.Equal(new[] { ExistingPath }, _recentStore.Remembered);
        Assert.Equal(ExistingPath, Assert.Single(session.RecentFiles));
        Assert.True(session.HasRecentFiles);
    }

    [Fact]
    public void Saving_Marks_The_Document_Clean_And_Remembers_The_Path() {
        var session = CreateSession();
        session.Text = "Chapter two";

        var outcome = session.Save("chapter2.txt");

        Assert.True(outcome.Succeeded);
        Assert.Equal("chapter2.txt", session.Path);
        Assert.Equal("Chapter two", _files.Files["chapter2.txt"]);
        Assert.False(session.IsDirty);
        Assert.Equal(new[] { "chapter2.txt" }, _recentStore.Remembered);
    }

    [Fact]
    public void A_Failed_Save_Does_Not_Repoint_The_Document() {
        // If the write throws, the session must not start claiming the text
        // lives on disk. Otherwise the user closes the window believing it
        // saved.
        var session = CreateSession();
        session.Text = "Draft text";
        _files.FailWritesWith = new IOException("read-only");

        var outcome = session.Save("readonly.txt");

        Assert.False(outcome.Succeeded);
        Assert.NotNull(outcome.Detail);
        Assert.Null(session.Path);
        Assert.True(session.IsDirty);
        Assert.Empty(_recentStore.Remembered);
    }

    [Fact]
    public void Recent_Files_Are_Restored_Newest_First() {
        _recentStore.Stored.AddRange(new[] { "a.txt", "b.txt", "c.txt" });

        var session = CreateSession();

        Assert.Equal(new[] { "a.txt", "b.txt", "c.txt" }, session.RecentFiles);
    }

    [Fact]
    public void The_Recent_List_Stops_At_Ten() {
        var session = CreateSession();

        for (var i = 0; i < 15; i++) {
            _files.Files[$"file{i}.txt"] = "x";
            session.Open($"file{i}.txt");
        }

        Assert.Equal(10, session.RecentFiles.Count);
        // Newest first, oldest evicted.
        Assert.Equal("file14.txt", session.RecentFiles[0]);
        Assert.Equal("file5.txt", session.RecentFiles[^1]);
    }

    [Fact]
    public void Reopening_A_File_Moves_It_To_The_Front() {
        var session = CreateSession();
        _files.Files["a.txt"] = "A";
        _files.Files["b.txt"] = "B";

        session.Open("a.txt");
        session.Open("b.txt");
        session.Open("a.txt");

        Assert.Equal(new[] { "a.txt", "b.txt" }, session.RecentFiles);
    }

    [Fact]
    public void The_Gutter_Shows_One_Number_Per_Line() {
        var session = CreateSession();

        // An empty document is still a document with a first line to number:
        // the cursor is on it whether anything has been typed or not.
        Assert.Equal("1", session.LineNumbersText);

        session.Text = "one\ntwo\nthree";

        Assert.Equal("1\n2\n3", session.LineNumbersText);
    }

    [Fact]
    public void The_Gutter_s_Numbers_Follow_The_Line_Count() {
        var session = CreateSession();

        session.Text = "one\ntwo";
        Assert.Equal("1\n2", session.LineNumbersText);

        // Joining the lines renumbers the gutter down.
        session.Text = "onetwo";
        Assert.Equal("1", session.LineNumbersText);
    }

    [Fact]
    public void Opening_From_The_Command_Line_Is_The_Same_As_Opening() {
        var session = CreateSessionWithFile();

        session.OpenFromCommandLine(ExistingPath);

        Assert.Equal(ExistingText, session.Text);
        Assert.Equal(ExistingPath, session.Path);
    }
}
