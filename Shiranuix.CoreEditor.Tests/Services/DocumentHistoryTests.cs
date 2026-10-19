namespace Shiranuix.CoreEditor.Tests.Services;

/// <summary>
/// Tests for <see cref="DocumentHistory"/>, the document's own undo and redo.
/// </summary>
/// <remarks>
/// Every case drives the history with plain strings — no window, no file, no
/// binding — because what is worth checking here is the arithmetic: which
/// move inverts which, when two moves become one, and where the stacks stop.
/// The session's tests cover the same rules again through the door the view
/// actually uses.
/// </remarks>
public sealed class DocumentHistoryTests {
    private readonly DocumentHistory _history = new();

    /// <summary>
    /// The window every plain note stands in, for the cases that are about
    /// the arithmetic rather than about windows.
    /// </summary>
    private static readonly UndoTarget Window = new(0, 100, 0);

    [Fact]
    public void A_Note_About_Unchanged_Text_Records_Nothing() {
        _history.Note("same", "same", Window);

        Assert.False(_history.CanUndo);
        Assert.False(_history.CanRedo);
    }

    [Fact]
    public void A_Typing_Move_Can_Be_Taken_Back() {
        _history.Note(string.Empty, "a", Window);
        Assert.True(_history.CanUndo);

        var taken = _history.TryUndo("a", out var text);

        Assert.True(taken);
        Assert.Equal(string.Empty, text);
        Assert.True(_history.CanRedo);
    }

    [Fact]
    public void A_Taken_Back_Move_Can_Be_Reapplied() {
        _history.Note(string.Empty, "a", Window);
        _history.TryUndo("a", out _);

        var reapplied = _history.TryRedo(string.Empty, out var text);

        Assert.True(reapplied);
        Assert.Equal("a", text);
        Assert.True(_history.CanUndo);
    }

    [Fact]
    public void Undo_With_An_Empty_Stack_Changes_Nothing() {
        var taken = _history.TryUndo("text", out var text);

        Assert.False(taken);
        Assert.Equal("text", text);
        Assert.False(_history.CanRedo);
    }

    [Fact]
    public void Redo_With_An_Empty_Stack_Changes_Nothing() {
        var reapplied = _history.TryRedo("text", out var text);

        Assert.False(reapplied);
        Assert.Equal("text", text);
        Assert.False(_history.CanUndo);
    }

    [Fact]
    public void A_Run_Of_Typing_Merges_Into_One_Entry() {
        _history.Note(string.Empty, "a", Window);
        _history.Note("a", "ab", Window);
        _history.Note("ab", "abc", Window);

        var taken = _history.TryUndo("abc", out var text);

        Assert.True(taken);
        Assert.Equal(string.Empty, text);
        Assert.False(_history.CanUndo);
    }

    [Fact]
    public void Backspaces_Merge_Into_One_Entry() {
        _history.Note("abc", "ab", Window);
        _history.Note("ab", "a", Window);

        var taken = _history.TryUndo("a", out var text);

        Assert.True(taken);
        Assert.Equal("abc", text);
    }

    [Fact]
    public void Forward_Deletes_Merge_Into_One_Entry() {
        // The Delete key holds its place while the text after it goes, so
        // the run grows rightward from one start.
        _history.Note("abcd", "acd", Window);
        _history.Note("acd", "ad", Window);

        var taken = _history.TryUndo("ad", out var text);

        Assert.True(taken);
        Assert.Equal("abcd", text);
    }

    [Fact]
    public void A_Replaced_Selection_Stands_Alone_From_The_Typing_It_Follows() {
        _history.Note(string.Empty, "abc", Window);
        _history.Note("abc", "abXY", Window);

        _history.TryUndo("abXY", out var afterFirst);
        Assert.Equal("abc", afterFirst);

        _history.TryUndo("abc", out var afterSecond);
        Assert.Equal(string.Empty, afterSecond);
    }

    [Fact]
    public void A_Run_Over_The_Merge_Limit_Splits_At_The_Limit() {
        // One word's worth of typing is one entry; a run much longer than
        // the limit comes back in steps rather than all at once.
        var typed = new string('x', DocumentHistory.MergeLimit * 2);

        for (var index = 1; index <= typed.Length; index++) {
            _history.Note(typed[..(index - 1)], typed[..index], Window);
        }

        var steps = 0;
        while (_history.TryUndo(typed, out var text)) {
            typed = text;
            steps++;
        }

        Assert.True(steps >= 2, $"A run of {DocumentHistory.MergeLimit * 2} characters undid in {steps} step(s).");
        Assert.Equal(string.Empty, typed);
    }

    [Fact]
    public void Typing_Anew_Clears_The_Redo_Branch() {
        _history.Note(string.Empty, "a", Window);
        _history.TryUndo("a", out _);
        Assert.True(_history.CanRedo);

        _history.Note(string.Empty, "b", Window);

        Assert.False(_history.CanRedo);
    }

    [Fact]
    public void A_Move_The_Text_No_Longer_Holds_Is_Dropped_Rather_Than_Applied() {
        // The entry reaches past the text it is being applied to — the
        // stacks and the document have parted company — so it goes, and the
        // text is left exactly as it was.
        _history.Note("abcd", "abcxyzd", Window);

        var taken = _history.TryUndo("abc", out var text);

        Assert.False(taken);
        Assert.Equal("abc", text);
        Assert.False(_history.CanUndo);
        Assert.False(_history.CanRedo);
    }

    [Fact]
    public void The_Oldest_Moves_Are_Dropped_At_The_Limit() {
        // Notes that swap one leading character for another never merge, so
        // they stack up one per note until the limit starts dropping the
        // oldest — and exactly that many come back out.
        var head = "a";
        for (var index = 0; index < DocumentHistory.Limit + 50; index++) {
            head = head == "a" ? "b" : "a";
            _history.Note(head == "a" ? "b tail" : "a tail",
                head == "a" ? "a tail" : "b tail",
                Window);
        }

        var current = "a tail";
        var undos = 0;
        while (_history.TryUndo(current, out var restored)) {
            current = restored;
            undos++;
        }

        Assert.Equal(DocumentHistory.Limit, undos);
    }

    [Fact]
    public void Clear_Forgets_Both_Stacks() {
        _history.Note(string.Empty, "a", Window);
        _history.TryUndo("a", out _);

        _history.Clear();

        Assert.False(_history.CanUndo);
        Assert.False(_history.CanRedo);
    }

    [Fact]
    public void PeekUndo_Reports_The_Window_The_Move_Was_Made_In() {
        // An undo cannot run against a window that has slid away, so the
        // owner looks first: the entry says which stretch of file to put
        // back, and where the caret stood in it.
        _history.Note("abc", "abcd", new UndoTarget(10, 60, 5));

        var there = _history.PeekUndo(out var target);

        Assert.True(there);
        Assert.Equal(new UndoTarget(10, 60, 5), target);
    }

    [Fact]
    public void PeekUndo_With_An_Empty_Stack_Reports_Nothing() {
        Assert.False(_history.PeekUndo(out var target));
        Assert.Equal(default, target);
    }

    [Fact]
    public void PeekRedo_Lands_The_Caret_After_What_The_Redo_Puts_Back() {
        _history.Note(string.Empty, "abc", new UndoTarget(0, 0, 0));
        _history.TryUndo("abc", out _);

        var there = _history.PeekRedo(out var target);

        Assert.True(there);
        Assert.Equal(new UndoTarget(0, 0, 3), target);
    }

    [Fact]
    public void Moves_Made_In_Different_Windows_Do_Not_Merge() {
        // A run continued after the window slid is a run against a different
        // stretch of file: merged, one entry would restore neither.
        _history.Note("a", "ab", new UndoTarget(0, 1, 1));
        _history.Note("b", "bc", new UndoTarget(200, 201, 1));

        Assert.True(_history.TryUndo("bc", out var first));
        Assert.Equal("b", first);

        // The owner puts the first entry's window back before undoing again
        // — the history only sees each window's own text.
        Assert.True(_history.TryUndo("ab", out var second));
        Assert.Equal("a", second);
        Assert.False(_history.CanUndo);
    }

    [Fact]
    public void Rebase_Moves_Every_Entry_S_Window_To_Its_New_Place() {
        // A save collapses the ledger into the file: every stretch of file
        // an entry names slides by what the moves before it changed, and
        // an undo after the save has to find the window at its new place.
        _history.Note(string.Empty, "abc", new UndoTarget(0, 3, 0));
        _history.Note("x", "xy", new UndoTarget(5, 6, 0));
        _history.TryUndo("xy", out _);

        _history.Rebase(position => position + 10);

        Assert.True(_history.PeekUndo(out var undo));
        Assert.Equal(new UndoTarget(10, 13, 0), undo);
        Assert.True(_history.PeekRedo(out var redo));
        Assert.Equal(new UndoTarget(15, 16, 2), redo);
    }

    [Fact]
    public void Null_Texts_Are_Refused() {
        Assert.Throws<ArgumentNullException>(() => _history.Note(null!, "after", Window));
        Assert.Throws<ArgumentNullException>(() => _history.Note("before", null!, Window));
        Assert.Throws<ArgumentNullException>(() => _history.TryUndo(null!, out _));
        Assert.Throws<ArgumentNullException>(() => _history.TryRedo(null!, out _));
    }
}
