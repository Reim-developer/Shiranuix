using Shiranuix.Models;
using Shiranuix.Services;
using Shiranuix.ViewModels;

namespace Shiranuix.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="HomeViewModel"/>, the recent-folders screen logic.
/// </summary>
public sealed class HomeViewModelTests : IDisposable
{
    private readonly DirectoryFolderScanner _scanner = new();
    private readonly InMemoryLibraryRepository _repository = new();
    private readonly string _root;

    /// <summary>Creates a scratch folder holding one image.</summary>
    public HomeViewModelTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "shiranuix-home-" + Guid.NewGuid());
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "1.png"), string.Empty);
    }

    /// <summary>Removes the scratch folder.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void OpenFolder_With_Images_Raises_ChapterRequested_And_Records()
    {
        var viewModel = new HomeViewModel(_scanner, _repository);
        Chapter? requested = null;
        viewModel.ChapterRequested += (_, chapter) => requested = chapter;

        viewModel.OpenFolderCommand.Execute(_root);

        Assert.NotNull(requested);
        Assert.Equal(Path.GetFileName(_root), requested!.Title);
        Assert.Single(viewModel.Recent);
        Assert.False(viewModel.IsEmpty);
        Assert.Contains("Opened", viewModel.Status);
    }

    [Fact]
    public void OpenFolder_Without_Images_Updates_Status_But_Does_Not_Navigate()
    {
        var emptyFolder = Path.Combine(_root, "empty");
        Directory.CreateDirectory(emptyFolder);

        var viewModel = new HomeViewModel(_scanner, _repository);
        bool navigated = false;
        viewModel.ChapterRequested += (_, _) => navigated = true;

        viewModel.OpenFolderCommand.Execute(emptyFolder);

        Assert.False(navigated);
        Assert.Contains("No images found", viewModel.Status);
        Assert.True(viewModel.IsEmpty);
    }

    [Fact]
    public void OpenFolder_With_Null_Reports_No_Folder_Selected()
    {
        var viewModel = new HomeViewModel(_scanner, _repository);

        viewModel.OpenFolderCommand.Execute(null);

        Assert.Equal("No folder selected.", viewModel.Status);
    }

    [Fact]
    public void Recent_Is_Empty_Initially()
    {
        var viewModel = new HomeViewModel(_scanner, _repository);

        Assert.Empty(viewModel.Recent);
        Assert.True(viewModel.IsEmpty);
    }
}
