using Shiranuix.Utilities;

namespace Shiranuix.Tests.Utilities;

/// <summary>
/// Tests for <see cref="ImageFormats"/>, the extension whitelist.
/// </summary>
public sealed class ImageFormatsTests {
    [Theory]
    [InlineData("page.png", true)]
    [InlineData("page.JPG", true)]   // case-insensitive
    [InlineData("page.jpeg", true)]
    [InlineData("page.gif", true)]
    [InlineData("page.bmp", true)]
    [InlineData("page.webp", true)]
    [InlineData("page.txt", false)]
    [InlineData("page", false)]      // no extension
    [InlineData("page.", false)]     // empty extension
    [InlineData("", false)]
    public void IsSupported_Detects_Image_Extensions(string fileName, bool expected) {
        Assert.Equal(expected, ImageFormats.IsSupported(fileName));
    }

    [Fact]
    public void IsSupported_Uses_Full_Path_But_Matches_File_Name_Extension() {
        // The extension is taken from the file name, not any dot in a folder.
        Assert.True(ImageFormats.IsSupported(@"C:\manga\vol.1\page.png"));
    }

    [Fact]
    public void IsSupported_Handles_Null() {
        Assert.False(ImageFormats.IsSupported(null));
    }
}
