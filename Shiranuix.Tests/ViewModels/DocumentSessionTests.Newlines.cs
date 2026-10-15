using Shiranuix.Models;
using Shiranuix.Tests.Fakes;

namespace Shiranuix.Tests.ViewModels;

/// <summary>
/// The document's line-break conventions: what a file arrives in, what a new
/// document starts in, what choosing a convention does to the dirty flag, and
/// what the next save writes.
/// </summary>
public sealed partial class DocumentSessionTests {
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
