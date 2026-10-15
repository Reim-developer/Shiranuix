using Shiranuix.Services;

namespace Shiranuix.Tests.Services;

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

    [Fact]
    public void A_Note_About_Unchanged_Text_Records_Nothing() {
        _history.Note("same", "same");

        Assert.False(_history.CanUndo);
        Assert.False(_history.CanRedo);
    }

    [Fact]
    public void A_Typing_Move_Can_Be_Taken_Back() {
        _history.Note(string.Empty, "a");
        Assert.True(_history.CanUndo);

        var taken = _history.TryUndo("a", out var text);

        Assert.True(taken);
        Assert.Equal(string.Empty, text);
        Assert.True(_history.CanRedo);
    }

    [Fact]
    public void A_Taken_Back_Move_Can_Be_Reapplied() {
        _history.Note(string.Empty, "a");
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
        _history.Note(string.Empty, "a");
        _history.Note("a", "ab");
        _history.Note("ab", "abc");

        var taken = _history.TryUndo("abc", out var text);

        Assert.True(taken);
        Assert.Equal(string.Empty, text);
        Assert.False(_history.CanUndo);
    }

    [Fact]
    public void Backspaces_Merge_Into_One_Entry() {
        _history.Note("abc", "ab");
        _history.Note("ab", "a");

        var taken = _history.TryUndo("a", out var text);

        Assert.True(taken);
        Assert.Equal("abc", text);
    }

    [Fact]
    public void Forward_Deletes_Merge_Into_One_Entry() {
        // The Delete key holds its place while the text after it goes, so
        // the run grows rightward from one start.
        _history.Note("abcd", "acd");
        _history.Note("acd", "ad");

        var taken = _history.TryUndo("ad", out var text);

        Assert.True(taken);
        Assert.Equal("abcd", text);
    }

    [Fact]
    public void A_Replaced_Selection_Stands_Alone_From_The_Typing_It_Follows() {
        _history.Note(string.Empty, "abc");
        _history.Note("abc", "abXY");

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
            _history.Note(typed[..(index - 1)], typed[..index]);
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
        _history.Note(string.Empty, "a");
        _history.TryUndo("a", out _);
        Assert.True(_history.CanRedo);

        _history.Note(string.Empty, "b");

        Assert.False(_history.CanRedo);
    }

    [Fact]
    public void A_Move_The_Text_No_Longer_Holds_Is_Dropped_Rather_Than_Applied() {
        // The entry reaches past the text it is being applied to — the
        // stacks and the document have parted company — so it goes, and the
        // text is left exactly as it was.
        _history.Note("abcd", "abcxyzd");

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
                head == "a" ? "a tail" : "b tail");
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
        _history.Note(string.Empty, "a");
        _history.TryUndo("a", out _);

        _history.Clear();

        Assert.False(_history.CanUndo);
        Assert.False(_history.CanRedo);
    }

    [Fact]
    public void Null_Texts_Are_Refused() {
        Assert.Throws<ArgumentNullException>(() => _history.Note(null!, "after"));
        Assert.Throws<ArgumentNullException>(() => _history.Note("before", null!));
        Assert.Throws<ArgumentNullException>(() => _history.TryUndo(null!, out _));
        Assert.Throws<ArgumentNullException>(() => _history.TryRedo(null!, out _));
    }
}
