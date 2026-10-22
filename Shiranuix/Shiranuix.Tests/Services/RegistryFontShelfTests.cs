using Shiranuix.Services.Pdf;

namespace Shiranuix.Tests.Services;

/// <summary>
/// Tests for <see cref="RegistryFontShelf"/>: the two things a registry
/// title and value are taken to mean — the family a title carries, and
/// the folder a bare file name stands in.
/// </summary>
/// <remarks>
/// The titles and values are strings rather than a machine's own registry,
/// so every fact is exact and asked on every system: the reading of the
/// registry itself is the machine's to stand on.
/// </remarks>
public sealed class RegistryFontShelfTests {
    [Fact]
    public void A_Title_Says_Its_Family_Before_The_File_Named_After_It() {
        Assert.Equal("Georgia Bold", RegistryFontShelf.FamilyOf("Georgia Bold (georgiai.ttf)"));
    }

    [Fact]
    public void A_Title_With_No_File_After_It_Is_All_Family() {
        Assert.Equal("Courier New", RegistryFontShelf.FamilyOf("Courier New"));
    }

    [Fact]
    public void A_Title_That_Begins_With_The_File_Says_No_Family() {
        Assert.Equal(string.Empty, RegistryFontShelf.FamilyOf(" (arial.ttf)"));
    }

    [Fact]
    public void A_Bare_File_Name_Stands_In_The_Folder_Given() {
        var folder = Path.Combine("shelf", "fonts");

        Assert.Equal(Path.Combine(folder, "arial.ttf"), RegistryFontShelf.PathOn("arial.ttf", folder));
    }

    [Fact]
    public void A_Value_That_Is_Already_A_Path_Stands_As_It_Is() {
        var path = @"C:\Elsewhere\arial.ttf";

        Assert.Equal(path, RegistryFontShelf.PathOn(path, Path.Combine("shelf", "fonts")));
    }

    [Fact]
    public void The_Spares_Of_The_Windows_Shelf_Are_The_Three_It_Expects() {
        Assert.Equal(["Times New Roman", "Arial", "Segoe UI"], RegistryFontShelf.WindowsSpares);
    }
}
