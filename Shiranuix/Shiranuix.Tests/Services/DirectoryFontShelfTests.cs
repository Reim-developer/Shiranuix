using Shiranuix.Services.Pdf;

namespace Shiranuix.Tests.Services;

/// <summary>
/// Tests for <see cref="DirectoryFontShelf"/>: what a set of folders says
/// it holds, read off the files' own names.
/// </summary>
/// <remarks>
/// The folders are made fresh for every test and hold builds rather than
/// faces, so every fact is exact: which file each name came back to, in
/// which order the folders were asked, and that nothing in them — however
/// badly named, however broken — stops the reading of the rest.
/// </remarks>
public sealed class DirectoryFontShelfTests : IDisposable {
    private readonly string _home = Directory
        .CreateDirectory(Path.Combine(Path.GetTempPath(), $"shiranuix-shelf-{Guid.NewGuid():N}"))
        .FullName;

    public void Dispose() {
        try {
            Directory.Delete(_home, recursive: true);
        } catch (Exception flaw) when (flaw is IOException or UnauthorizedAccessException) {
            // A folder left standing is one the machine's own tidy-up takes.
            _ = flaw;
        }
    }

    [Fact]
    public void A_Regular_File_Offers_Its_Family_Plain() {
        var folder = Folder("plain");
        var held = Write(folder, "LiberationSans-Regular.ttf", TestFont.Named((3, 1, 1, "Liberation Sans")));

        var faces = new DirectoryFontShelf([folder], []).ReadFaces();

        Assert.Equal(held, faces["liberation sans"]);
    }

    [Fact]
    public void A_Bold_File_Offers_The_Family_With_Its_Style_And_No_Plain_One() {
        var folder = Folder("bold");
        var held = Write(folder, "LiberationSans-Bold.ttf", TestFont.Named(
            (3, 1, 1, "Liberation Sans"),
            (3, 1, 2, "Bold")));

        var faces = new DirectoryFontShelf([folder], []).ReadFaces();

        Assert.Equal(held, faces["liberation sans bold"]);
        Assert.False(faces.ContainsKey("liberation sans"));
    }

    [Fact]
    public void An_Italic_File_Offers_The_Family_With_Its_Style() {
        var folder = Folder("italic");
        var held = Write(folder, "ComicNeue-Italic.ttf", TestFont.Named(
            (3, 1, 1, "Comic Neue"),
            (3, 1, 2, "Italic")));

        var faces = new DirectoryFontShelf([folder], []).ReadFaces();

        Assert.Equal(held, faces["comic neue italic"]);
    }

    [Fact]
    public void A_Folder_Beside_Another_Is_Read_Along_With_It() {
        var folder = Folder("two");
        var first = Write(folder, Path.Combine("first", "DejaVuSans.ttf"), TestFont.Named((3, 1, 1, "DejaVu Sans")));
        var second = Write(folder, Path.Combine("second", "ComicNeue.ttf"), TestFont.Named((3, 1, 1, "Comic Neue")));

        var faces = new DirectoryFontShelf([folder], []).ReadFaces();

        Assert.Equal(first, faces["dejavu sans"]);
        Assert.Equal(second, faces["comic neue"]);
    }

    [Fact]
    public void The_Folders_Of_The_Machine_Come_Before_A_Users_Own() {
        var system = Folder("system");
        var mine = Folder("user");
        var theirs = Write(system, "font.ttf", TestFont.Named((3, 1, 1, "Shared")));
        _ = Write(mine, "font.ttf", TestFont.Named((3, 1, 1, "Shared")));

        var faces = new DirectoryFontShelf([system, mine], []).ReadFaces();

        Assert.Equal(theirs, faces["shared"]);
    }

    [Fact]
    public void A_Name_Held_By_Two_Files_Answers_With_The_First_In_The_Folders_Order() {
        var folder = Folder("twin");
        var first = Write(folder, "AAA.ttf", TestFont.Named((3, 1, 1, "Twin")));
        _ = Write(folder, "zzz.ttf", TestFont.Named((3, 1, 1, "Twin")));

        var faces = new DirectoryFontShelf([folder], []).ReadFaces();

        // The files of a folder are read in the order their names stand,
        // so the same folder answers the same way every time it is read.
        Assert.Equal(first, faces["twin"]);
    }

    [Fact]
    public void The_Other_Endings_A_Typeface_Wears_Are_Read() {
        var folder = Folder("endings");
        var open = Write(folder, "Comic.otf", TestFont.Named((3, 1, 1, "Comic Open")));
        var together = Write(folder, "Pair.ttc", TestFont.NamedCollection((3, 1, 1, "Pair Held")));

        var faces = new DirectoryFontShelf([folder], []).ReadFaces();

        Assert.Equal(open, faces["comic open"]);
        Assert.Equal(together, faces["pair held"]);
    }

    [Fact]
    public void Nothing_In_A_Folder_Stops_The_Reading_Of_The_Rest() {
        var folder = Folder("junk");
        Write(folder, "notes.txt", [1, 2, 3]); // a file of no typeface ending
        Write(folder, "nameless.ttf", TestFont.Small((0x61, 0x7A))); // a face with no name table
        Write(folder, "torn.ttf", TestFont.Named((3, 1, 1, "Torn"))[..14]); // a name table cut away
        Write(folder, "tiny.ttf", [0x00, 0x01]); // hardly a file at all
        var held = Write(folder, "kept.ttf", TestFont.Named((3, 1, 1, "Kept")));

        var faces = new DirectoryFontShelf([folder], []).ReadFaces();

        Assert.Equal(held, faces["kept"]);
        Assert.Single(faces);
    }

    [Fact]
    public void A_Folder_That_Stands_Not_There_Holds_Nothing() {
        var faces = new DirectoryFontShelf([Path.Combine(_home, "never")], []).ReadFaces();

        Assert.Empty(faces);
    }

    [Fact]
    public void A_Shelf_With_Nothing_On_It_Says_So() {
        var shelf = new DirectoryFontShelf([], []);

        Assert.Empty(shelf.ReadFaces());
        Assert.Empty(shelf.Spares);
    }

    [Fact]
    public void The_Spares_The_Shelf_Was_Given_Are_The_Ones_It_Asked_With() {
        var shelf = new DirectoryFontShelf([], ["Liberation Serif", "Arial"]);

        Assert.Equal(["Liberation Serif", "Arial"], shelf.Spares);
    }

    private string Folder(string name) =>
        Directory.CreateDirectory(Path.Combine(_home, name)).FullName;

    private static string Write(string folder, string file, byte[] bytes) {
        var path = Path.Combine(folder, file);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);

        return path;
    }
}
