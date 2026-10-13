using Shiranuix.Utilities;

namespace Shiranuix.Tests.Utilities;

/// <summary>
/// Tests for <see cref="RecentList{T}"/>, the "latest first, capped" rule
/// behind the recent-files list.
/// </summary>
/// <remarks>
/// The failures here are the annoying kind rather than the losing-work kind: a
/// file remembered twice takes two slots, or the list grows past what the start
/// screen has room for.
/// </remarks>
public sealed class RecentListTests {
    private const int Limit = 3;

    [Fact]
    public void New_Items_Go_To_The_Front() {
        var list = new RecentList<string>(Limit);

        list.MoveToFront("a.txt");
        list.MoveToFront("b.txt");
        list.MoveToFront("c.txt");

        Assert.Equal(new[] { "c.txt", "b.txt", "a.txt" }, list);
    }

    [Fact]
    public void Reusing_An_Item_Moves_It_To_The_Front() {
        var list = new RecentList<string>(Limit);
        list.MoveToFront("a.txt");
        list.MoveToFront("b.txt");

        list.MoveToFront("a.txt");

        Assert.Equal(new[] { "a.txt", "b.txt" }, list);
    }

    [Fact]
    public void The_List_Stops_At_Its_Limit() {
        var list = new RecentList<string>(Limit);

        for (var i = 0; i < Limit + 5; i++) {
            list.MoveToFront($"file{i}.txt");
        }

        // Newest first, oldest dropped.
        Assert.Equal(Limit, list.Count);
        Assert.Equal("file7.txt", list[0]);
        Assert.Equal("file5.txt", list[^1]);
    }

    [Fact]
    public void An_Item_Is_Not_Counted_Twice() {
        var list = new RecentList<string>(Limit);

        list.MoveToFront("a.txt");
        list.MoveToFront("a.txt");

        Assert.Single(list);
    }
}
