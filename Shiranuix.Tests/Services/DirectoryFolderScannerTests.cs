using Shiranuix.Services;

namespace Shiranuix.Tests.Services;

/// <summary>
/// Tests for <see cref="DirectoryFolderScanner"/>, which turns a folder of
/// images into a naturally-ordered chapter.
/// </summary>
public sealed class DirectoryFolderScannerTests : IDisposable
{
    private readonly DirectoryFolderScanner _scanner = new();
    private readonly string _root;

    /// <summary>
    /// Creates a scratch folder for each test.
    /// </summary>
    public DirectoryFolderScannerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "shiranuix-test-" + Guid.NewGuid());
        Directory.CreateDirectory(_root);
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
    public void Scan_Orders_Pages_Naturally()
    {
        // Create pages out of order; natural sort must fix it.
        CreateFile("10.png");
        CreateFile("2.png");
        CreateFile("1.png");

        var chapter = _scanner.Scan(_root);

        Assert.NotNull(chapter);
        Assert.Equal(3, chapter!.PageCount);
        Assert.Equal(new[] { 1, 2, 3 }, chapter.Pages.Select(p => p.PageNumber));
        Assert.EndsWith("1.png", chapter.Pages[0].ImageFile);
        Assert.EndsWith("2.png", chapter.Pages[1].ImageFile);
        Assert.EndsWith("10.png", chapter.Pages[2].ImageFile);
    }

    [Fact]
    public void Scan_Ignores_Non_Image_Files()
    {
        CreateFile("page.png");
        CreateFile("notes.txt");
        CreateFile("cover.jpg");

        var chapter = _scanner.Scan(_root);

        Assert.NotNull(chapter);
        Assert.Equal(2, chapter!.PageCount);
    }

    [Fact]
    public void Scan_Returns_Null_For_Folder_With_No_Images()
    {
        CreateFile("readme.txt");

        Assert.Null(_scanner.Scan(_root));
    }

    [Fact]
    public void Scan_Returns_Null_For_Missing_Folder()
    {
        Assert.Null(_scanner.Scan(Path.Combine(_root, "does-not-exist")));
    }

    [Fact]
    public void Scan_Uses_Folder_Name_As_Title()
    {
        CreateFile("1.png");

        var chapter = _scanner.Scan(_root);

        Assert.NotNull(chapter);
        Assert.Equal(Path.GetFileName(_root), chapter!.Title);
    }

    /// <summary>Creates an empty file with the given name in the scratch folder.</summary>
    private void CreateFile(string fileName)
    {
        File.WriteAllText(Path.Combine(_root, fileName), string.Empty);
    }
}
