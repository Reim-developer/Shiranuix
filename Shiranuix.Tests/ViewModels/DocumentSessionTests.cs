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
    public void Opening_A_File_Rings_The_Name_For_The_Title_Band() {
        // The name is derived from the path, so anything reading it hears
        // of it only if the change is raised under that name too. Without
        // this the title band kept saying "Untitled" long after the file
        // had arrived — the path rang, the name never did.
        _files.Files[ExistingPath] = ExistingText;
        var session = CreateSession();
        var raised = new List<string?>();
        session.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        session.Open(ExistingPath);

        Assert.Contains(nameof(DocumentSession.Name), raised);
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



    // ===== What the status bar shows ============================================

    [Fact]
    public void A_Fresh_Document_Is_On_Line_One_Column_One_UTF8_And_The_Os_Convention() {
        var session = CreateSession();

        Assert.Equal(1, session.CaretLine);
        Assert.Equal(1, session.CaretColumn);
        Assert.Equal("UTF-8", session.Encoding);
        Assert.Equal(Newline.Default.Name, session.Newline.Name);
    }

    [Fact]
    public void The_Caret_Counts_Lines_And_Columns_From_The_Start_Of_Its_Line() {
        var session = CreateSession();
        session.Text = "alpha\nbeta";

        session.CaretIndex = 0;
        Assert.Equal(1, session.CaretLine);
        Assert.Equal(1, session.CaretColumn);

        session.CaretIndex = 5; // the newline itself, still the end of line one
        Assert.Equal(1, session.CaretLine);
        Assert.Equal(6, session.CaretColumn);

        session.CaretIndex = 6; // the first character of the second line
        Assert.Equal(2, session.CaretLine);
        Assert.Equal(1, session.CaretColumn);

        session.CaretIndex = 9; // four characters into "beta"
        Assert.Equal(2, session.CaretLine);
        Assert.Equal(4, session.CaretColumn);
    }

    [Fact]
    public void A_Composing_Character_Counts_In_The_Caret_Column() {
        // The Vietnamese keystroke that has not committed yet: "â" is drawn at
        // the caret and is not in the text, but the writer is looking at a
        // column of two — and the status bar has to agree with their eyes.
        var session = CreateSession();

        session.Composition = Composition.Of("â", null);

        Assert.Equal(1, session.CaretLine);
        Assert.Equal(2, session.CaretColumn);
        Assert.True(session.IsEmpty); // drawn is not written
    }

    [Fact]
    public void The_Caret_Reads_At_The_Input_Methods_Cursor_Inside_The_Composition() {
        // An input method may stand its cursor inside what it is composing. The
        // editor draws the caret there, so the column reads there.
        var session = CreateSession();
        session.Text = "ab";
        session.CaretIndex = 1;

        session.Composition = Composition.Of("x", 0); // page "axb", caret before the x

        Assert.Equal(1, session.CaretLine);
        Assert.Equal(2, session.CaretColumn);
    }

    [Fact]
    public void A_Composition_Committed_Into_The_Text_Is_Not_Counted_Twice() {
        var session = CreateSession();
        session.Composition = Composition.Of("â", null);
        Assert.Equal(2, session.CaretColumn);

        session.Text = "â"; // the input method commits, the caret travels with it
        session.CaretIndex = 1;

        Assert.True(session.Composition.IsEmpty);
        Assert.Equal(2, session.CaretColumn);
    }

    [Fact]
    public void A_Composition_Goes_When_The_Text_Does() {
        // The composition belongs to the text it was composed against. Any
        // other edit leaves it counting a character nobody can see.
        var session = CreateSession();
        session.Text = "word";
        session.Composition = Composition.Of("x", 1);

        session.Text = "words";

        Assert.True(session.Composition.IsEmpty);
    }

    [Fact]
    public void A_Composition_Change_Announces_The_Caret() {
        // The status bar binds the line and the column; a change nobody hears
        // is a status bar that still reads the old place.
        var session = CreateSession();
        var raised = new List<string?>();
        session.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        session.Composition = Composition.Of("â", 1);

        Assert.Contains(nameof(DocumentSession.Composition), raised);
        Assert.Contains(nameof(DocumentSession.CaretLine), raised);
        Assert.Contains(nameof(DocumentSession.CaretColumn), raised);
    }

    [Fact]
    public void A_Caret_Past_The_End_Of_The_Text_Is_Clamped() {
        var session = CreateSession();
        session.Text = "three words";

        // A stale binding can offer an index past the text; the line and
        // column are computed from the text, so they cannot leave it.
        session.CaretIndex = 99;

        Assert.Equal(1, session.CaretLine);
        Assert.Equal(12, session.CaretColumn);
    }

    [Fact]
    public void An_Empty_Document_Has_Its_Caret_On_Line_One() {
        var session = CreateSession();

        Assert.Equal(0, session.CaretIndex);
        Assert.Equal(1, session.CaretLine);
        Assert.Equal(1, session.CaretColumn);
    }
    // ===== Undo, through the session =========================================

    [Fact]
    public void Undo_Takes_Back_The_Last_Typing_Move() {
        var session = CreateSession();
        session.Text = "draft";

        session.Undo();

        Assert.Equal(string.Empty, session.Text);
        Assert.False(session.IsDirty);
        Assert.True(session.CanRedo);
    }

    [Fact]
    public void Redo_Puts_The_Taken_Back_Move_Back() {
        var session = CreateSession();
        session.Text = "draft";
        session.Undo();

        session.Redo();

        Assert.Equal("draft", session.Text);
        Assert.True(session.IsDirty);
        Assert.True(session.CanUndo);
    }

    [Fact]
    public void Undo_Lands_The_Caret_Where_The_Move_Began() {
        // The caret travels with the text: taking back a move returns the
        // cursor to where that move began, and redoing it puts the cursor
        // after what the move wrote. A caret in the middle of a word is a
        // move of its own — typing forward from it would have merged into
        // the run before.
        var session = CreateSession();
        session.Text = "alpha";
        session.CaretIndex = 2;

        session.Text = "alXpha";

        session.Undo();
        Assert.Equal("alpha", session.Text);
        Assert.Equal(2, session.CaretIndex);

        session.Redo();
        Assert.Equal("alXpha", session.Text);
        Assert.Equal(3, session.CaretIndex);
    }

    [Fact]
    public void Undo_With_Nothing_To_Take_Back_Does_Nothing() {
        var session = CreateSession();

        session.Undo();

        Assert.Equal(string.Empty, session.Text);
        Assert.False(session.CanUndo);
        Assert.False(session.CanRedo);
    }

    [Fact]
    public void Undo_Inside_An_Opened_File_Restores_Its_Own_Text() {
        // The move recorded against this file's text inverts exactly, and
        // the document is clean again — back at the point it was read.
        var session = CreateSessionWithFile();
        session.Text = ExistingText + " more";

        session.Undo();

        Assert.Equal(ExistingText, session.Text);
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void Opening_A_File_Forgets_The_Moves_Of_The_Old_One() {
        _files.Files[ExistingPath] = ExistingText;
        var session = CreateSession();
        session.Text = "draft";

        session.Open(ExistingPath);

        // The old moves would restore a text this document no longer holds.
        Assert.Equal(ExistingText, session.Text);
        Assert.False(session.CanUndo);
        Assert.False(session.CanRedo);
    }

    [Fact]
    public void New_Forgets_The_Moves_Of_The_Document_It_Replaces() {
        var session = CreateSession();
        session.Text = "draft";
        session.Undo();

        session.New();

        Assert.False(session.CanUndo);
        Assert.False(session.CanRedo);
    }

    [Fact]
    public void Typing_Anew_Discards_The_Redo_Branch() {
        // The undone text is about to stop being reachable, the way starting
        // to type again does in any editor.
        var session = CreateSession();
        session.Text = "draft";
        session.Undo();

        session.Text = "another";

        Assert.False(session.CanRedo);
        Assert.Equal("another", session.Text);
    }

    [Fact]
    public void Saving_Keeps_The_Moves() {
        // A save is a point the text passed through, not a new document:
        // undo must still work after one.
        var session = CreateSession();
        session.Text = "draft";

        session.Save(ExistingPath);

        Assert.True(session.CanUndo);

        session.Undo();

        Assert.Equal(string.Empty, session.Text);
        Assert.True(session.IsDirty);
    }

    // ===== Saving through the ledger =========================================

    [Fact]
    public void Saving_An_Edited_File_Writes_The_Moves_Over_What_Was_Read() {
        // The save is not the buffer copied over the file: it is the file
        // read back with the spans that changed laid over the top, so what
        // the writer did not touch survives the round trip exactly.
        _files.Files[ExistingPath] = "one\ntwo\nthree";
        var session = CreateSession();
        session.Open(ExistingPath);

        session.Text = "ONE\ntwo\nthree";
        var outcome = session.Save(ExistingPath);

        Assert.True(outcome.Succeeded);
        Assert.Equal("ONE\ntwo\nthree", _files.Files[ExistingPath]);
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void Saving_After_Typing_And_Taking_It_Back_Writes_The_Original_File() {
        // The undo's placement reaches the ledger too: a move the writer
        // took back must not still be standing as a change to the file.
        _files.Files[ExistingPath] = ExistingText;
        var session = CreateSession();
        session.Open(ExistingPath);

        session.Text = ExistingText + "!";
        session.Undo();
        session.Save(ExistingPath);

        Assert.Equal(ExistingText, _files.Files[ExistingPath]);
    }

    [Fact]
    public void Saving_After_An_Undo_Of_A_First_Save_Writes_Nothing() {
        // The untitled case: the whole document was one move at point zero,
        // and taking it back leaves the ledger with nothing to write.
        var session = CreateSession();
        session.Text = "draft";

        session.Undo();
        session.Save(ExistingPath);

        Assert.Equal(string.Empty, _files.Files[ExistingPath]);
    }
    // ===== What the line breaks are =============================================

    [Fact]
    public void A_CrLf_File_Reports_CrLf_And_Writes_Its_Breaks_Back_The_Same() {
        _files.Files["windows.txt"] = "one\r\ntwo\r\n";
        var session = CreateSession();

        session.Open("windows.txt");
        Assert.Equal("CRLF", session.Newline.Name);

        // A newline typed into the document is LF in the buffer, so saving has
        // to put the file's own convention back.
        session.Save("windows.txt");

        Assert.Equal("one\r\ntwo\r\n", _files.Files["windows.txt"]);
    }

    [Fact]
    public void An_Lf_File_Stays_Lf_When_Saved() {
        _files.Files["unix.txt"] = "one\ntwo\n";
        var session = CreateSession();

        session.Open("unix.txt");
        session.Save("unix.txt");

        Assert.Equal("one\ntwo\n", _files.Files["unix.txt"]);
    }

    [Fact]
    public void A_New_Document_Writes_The_Os_Convention() {
        var session = CreateSession();
        session.Text = "one\ntwo";

        session.Save("new.txt");

        Assert.Equal(Newline.Default.Apply("one\ntwo"), _files.Files["new.txt"]);
        Assert.Equal(Newline.Default.Name, session.Newline.Name);
    }

    [Fact]
    public void Opening_A_CrLf_File_And_Making_A_New_Document_Returns_To_The_Os_Convention() {
        // A new document is this machine's blank page — not whatever the
        // previous file arrived in.
        _files.Files["windows.txt"] = "one\r\ntwo\r\n";
        var session = CreateSession();
        session.Open("windows.txt");

        session.New();

        Assert.Equal(Newline.Default, session.Newline);
    }

    [Fact]
    public void Changing_The_Newline_Marks_The_Document_Dirty() {
        // The file on disk would now be written differently, so the document
        // has changed even though no character has.
        var session = CreateSessionWithFile();
        Assert.False(session.IsDirty);

        session.Newline = OtherConvention(session.Newline);

        Assert.True(session.IsDirty);
    }

    [Fact]
    public void Reverting_The_Newline_Clears_Dirty() {
        // Dirty compares against the last read or write, like the text does:
        // choosing the file's own convention back is no change at all.
        var session = CreateSessionWithFile();

        session.Newline = OtherConvention(session.Newline);
        session.Newline = Newline.Detect(ExistingText);

        Assert.False(session.IsDirty);
    }

    [Fact]
    public void The_Chosen_Newline_Is_What_The_Next_Save_Writes() {
        _files.Files["story.txt"] = "one\ntwo";
        var session = CreateSession();
        session.Open("story.txt");

        session.Newline = Newline.CrLf;
        session.Save("story.txt");

        Assert.Equal("one\r\ntwo", _files.Files["story.txt"]);
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void Changing_The_Newline_After_A_Save_Makes_It_Dirty_Again() {
        var session = CreateSession();
        session.Text = "one";

        session.Save("story.txt");
        session.Newline = OtherConvention(session.Newline);

        Assert.True(session.IsDirty);
    }
}
