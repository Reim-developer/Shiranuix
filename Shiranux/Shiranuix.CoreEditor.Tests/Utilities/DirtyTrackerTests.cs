namespace Shiranuix.CoreEditor.Tests.Utilities;

/// <summary>
/// Tests for <see cref="DirtyTracker{T}"/>, the "has this changed since it was
/// saved?" rule behind the document's dirty flag.
/// </summary>
/// <remarks>
/// The rule is simple and the failures are not: a tracker that reports dirty
/// when the text is back to what was saved loses the writer their work, and
/// one that reports clean when it is not loses it quieter.
/// </remarks>
public sealed class DirtyTrackerTests {
    [Fact]
    public void A_New_Tracker_Is_Clean() {
        var tracker = new DirtyTracker<string>("first draft");

        Assert.Equal("first draft", tracker.Value);
        Assert.False(tracker.IsDirty);
    }

    [Fact]
    public void Changing_The_Value_Makes_It_Dirty() {
        var tracker = new DirtyTracker<string>("first draft");

        tracker.Value = "second draft";

        Assert.True(tracker.IsDirty);
    }

    [Fact]
    public void Reverting_To_The_Saved_Value_Clears_Dirty() {
        // Backspacing an edit returns to clean: dirty is a comparison against
        // the save point, not a latch that once set stays set.
        var tracker = new DirtyTracker<string>("saved");

        tracker.Value = "edited";
        Assert.True(tracker.IsDirty);

        tracker.Value = "saved";
        Assert.False(tracker.IsDirty);
    }

    [Fact]
    public void Marking_Saved_Moves_The_Save_Point() {
        var tracker = new DirtyTracker<string>("first");

        tracker.Value = "second";
        tracker.MarkSaved();

        Assert.False(tracker.IsDirty);
        // The new save point is the new value, so editing again is dirty again.
        tracker.Value = "third";
        Assert.True(tracker.IsDirty);
    }

    [Fact]
    public void Reset_Sets_The_Value_And_The_Save_Point_Together() {
        var tracker = new DirtyTracker<string>("old");

        tracker.Value = "edited";
        Assert.True(tracker.IsDirty);

        tracker.Reset("new");
        Assert.Equal("new", tracker.Value);
        Assert.False(tracker.IsDirty);
    }

    [Fact]
    public void The_Tracker_Raises_Changed_When_The_Value_Moves() {
        var tracker = new DirtyTracker<string>("a");
        var raises = 0;
        tracker.Changed += (_, _) => raises++;

        tracker.Value = "b";
        tracker.MarkSaved();
        tracker.Reset("c");

        Assert.Equal(3, raises);
    }
}
