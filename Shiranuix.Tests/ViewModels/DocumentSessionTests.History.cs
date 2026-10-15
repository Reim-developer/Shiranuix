using Shiranuix.Tests.Fakes;

namespace Shiranuix.Tests.ViewModels;

/// <summary>
/// The moves through the session: what undo and redo take back and put back,
/// which moves survive which flows, and how a save writes the ledger's own
/// answer over what was read.
/// </summary>
public sealed partial class DocumentSessionTests {
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
}
