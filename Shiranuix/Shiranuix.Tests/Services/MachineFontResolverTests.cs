using System.Buffers.Binary;
using Shiranuix.Services.Pdf;

namespace Shiranuix.Tests.Services;

/// <summary>
/// Tests for <see cref="MachineFontResolver"/>: how the pen answers the
/// question of which file holds a family, when the shelf it asks is the
/// one handed to it.
/// </summary>
/// <remarks>
/// The shelves are stand-ins over builds, so every fact is exact: a style
/// the shelf holds answers before the plain face, a style it does not is
/// laid onto the plain face by the pen itself, and a family neither holds
/// answers nothing the way a choice the machine cannot carry always has.
/// </remarks>
public sealed class MachineFontResolverTests {
    [Fact]
    public void A_Family_The_Shelf_Holds_Answers_With_Its_File() {
        var resolver = Resolver([("georgia", "georgia.ttf")], []);

        Assert.Equal("georgia.ttf", resolver.ResolveTypeface("Georgia", false, false)?.FaceName);
    }

    [Fact]
    public void A_Style_The_Shelf_Holds_Answers_Before_The_Plain_Face() {
        var resolver = Resolver(
            [("georgia", "georgia.ttf"), ("georgia bold", "georgiabd.ttf")],
            []);

        Assert.Equal("georgiabd.ttf", resolver.ResolveTypeface("Georgia", true, false)?.FaceName);
    }

    [Fact]
    public void A_Style_The_Shelf_Does_Not_Hold_Is_Laid_On_The_Plain_Face() {
        var resolver = Resolver([("georgia", "georgia.ttf")], []);

        var said = resolver.ResolveTypeface("Georgia", true, true);

        Assert.Equal("georgia.ttf", said?.FaceName);
        Assert.True(said?.MustSimulateBold);
        Assert.True(said?.MustSimulateItalic);
    }

    [Fact]
    public void A_Family_The_Shelf_Does_Not_Hold_Answers_Nothing() {
        var resolver = Resolver([("georgia", "georgia.ttf")], []);

        Assert.Null(resolver.ResolveTypeface("Papyrus", false, false));
    }

    [Fact]
    public void The_File_A_Family_Is_Read_At_Is_The_Shelfs_Answer() {
        var resolver = Resolver([("georgia", "georgia.ttf")], []);

        Assert.Equal("georgia.ttf", resolver.FacePath("Georgia"));
        Assert.Null(resolver.FacePath("Papyrus"));
    }

    [Fact]
    public void The_Bytes_Of_A_Face_Are_The_Files_Own() {
        var resolver = Resolver([], []);
        var file = Path.Combine(Path.GetTempPath(), $"shiranuix-face-{Guid.NewGuid():N}.ttf");
        File.WriteAllBytes(file, TestFont.Small((0x1EBF, 0x1EBF)));

        try {
            Assert.Equal(TestFont.Small((0x1EBF, 0x1EBF)), resolver.GetFont(file));
        } finally {
            File.Delete(file);
        }

        Assert.Null(resolver.GetFont(Path.Combine(Path.GetTempPath(), "never-written.ttf")));
    }

    [Fact]
    public void The_Spares_Of_The_Resolver_Are_The_Shelfs_Own() {
        var resolver = Resolver([], ["Times New Roman"]);

        Assert.Equal(["Times New Roman"], resolver.Spares);
    }

    [Fact]
    public void The_Resolver_The_Process_Is_Handed_Stands_On_This_Machine() {
        // Whatever the system is, its shelf names faces to fall back on:
        // the Windows one three, the folders of the others their own.
        Assert.NotEmpty(MachineFontResolver.Instance.Spares);
    }

    [WindowsFact]
    public void The_Windows_Shelf_Really_Answers_With_The_Files_It_Names() {
        var face = MachineFontResolver.Instance.FacePath("Georgia");

        Assert.NotNull(face);
        Assert.True(File.Exists(face));

        var bytes = MachineFontResolver.Instance.GetFont(face!);
        Assert.NotNull(bytes);

        // The file the registry named is a typeface: its opening is the
        // one a TrueType or an OpenType file wears.
        Assert.True(
            BinaryPrimitives.ReadUInt32BigEndian(bytes!) == 0x0001_0000 ||
            BinaryPrimitives.ReadUInt32BigEndian(bytes!) == 0x4F54_544F);
    }

    /// <summary>A resolver over a shelf in a test's own place.</summary>
    private static MachineFontResolver Resolver(
        (string Key, string Path)[] faces,
        string[] spares) =>
        new(new Shelf(
            faces.ToDictionary(face => face.Key, face => face.Path),
            spares));

    /// <summary>A shelf of a test's own: the faces named, the spares said.</summary>
    private sealed class Shelf : IFontShelf {
        private readonly IReadOnlyDictionary<string, string> _faces;
        private readonly string[] _spares;

        internal Shelf(IReadOnlyDictionary<string, string> faces, string[] spares) {
            _faces = faces;
            _spares = spares;
        }

        public IReadOnlyDictionary<string, string> ReadFaces() => _faces;

        public string[] Spares => _spares;
    }
}
