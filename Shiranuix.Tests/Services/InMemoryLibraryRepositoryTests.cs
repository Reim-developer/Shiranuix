using Shiranuix.Models;
using Shiranuix.Services;

namespace Shiranuix.Tests.Services;

/// <summary>
/// Tests for <see cref="InMemoryLibraryRepository"/>, the session-scoped
/// recent-list store.
/// </summary>
public sealed class InMemoryLibraryRepositoryTests
{
    private readonly InMemoryLibraryRepository _repository = new();

    [Fact]
    public void GetRecent_Is_Empty_Initially()
    {
        Assert.Empty(_repository.GetRecent());
    }

    [Fact]
    public void RecordOpen_Most_Recent_First()
    {
        _repository.RecordOpen(Entry("a"));
        _repository.RecordOpen(Entry("b"));

        Assert.Equal(new[] { "b", "a" }, _repository.GetRecent().Select(e => e.Folder));
    }

    [Fact]
    public void RecordOpen_Dedupes_And_Moves_To_Front()
    {
        _repository.RecordOpen(Entry("a", pageCount: 5));
        _repository.RecordOpen(Entry("b"));
        // Re-open "a"; it should move to the front and refresh its page count.
        _repository.RecordOpen(Entry("a", pageCount: 9));

        var recent = _repository.GetRecent();
        Assert.Equal(2, recent.Count);
        Assert.Equal("a", recent[0].Folder);
        Assert.Equal(9, recent[0].PageCount);
    }

    [Fact]
    public void RecordOpen_Trim_To_Max_Entries()
    {
        for (int i = 0; i < InMemoryLibraryRepository.MaxEntries + 5; i++)
        {
            _repository.RecordOpen(Entry($"folder-{i}"));
        }

        Assert.Equal(InMemoryLibraryRepository.MaxEntries, _repository.GetRecent().Count);
    }

    [Fact]
    public void Remove_Deletes_By_Folder()
    {
        _repository.RecordOpen(Entry("a"));
        _repository.RecordOpen(Entry("b"));

        Assert.True(_repository.Remove("a"));
        Assert.Single(_repository.GetRecent());
        Assert.Equal("b", _repository.GetRecent()[0].Folder);
    }

    [Fact]
    public void Remove_Returns_False_When_Absent()
    {
        Assert.False(_repository.Remove("nope"));
    }

    /// <summary>Builds a minimal entry for the given folder.</summary>
    private static LibraryEntry Entry(string folder, int pageCount = 1) =>
        new(folder, folder, pageCount, DateTimeOffset.Now);
}
