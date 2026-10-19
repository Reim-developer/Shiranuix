using System.Text;

namespace Shiranuix.CoreFormat.Tests;

/// <summary>
/// Tests for the .shiranux format: a document written and read back is the
/// same document, a file that is not one is caught at the door, and a
/// section this version does not know is skipped by the length it carries.
/// </summary>
public sealed class ShiranuxFormatTests {
    [Fact]
    public void A_Document_With_Just_Text_Round_Trips() {
        var document = new ShiranuxDocument("Chương Một\nĐêm đó trời mưa.");

        var read = RoundTrip(document);

        Assert.Equal("Chương Một\nĐêm đó trời mưa.", read.Text);
        Assert.Equal(2, read.ParagraphCount);
        Assert.Equal(ParagraphAlignment.Left, read.AlignmentOf(0));
        Assert.Equal(ParagraphAlignment.Left, read.AlignmentOf(1));
    }

    [Fact]
    public void A_Centred_Paragraph_Round_Trips() {
        var document = new ShiranuxDocument(
            "Chương Một\nĐêm đó trời mưa.",
            new Dictionary<int, ParagraphAlignment> { [0] = ParagraphAlignment.Center });

        var read = RoundTrip(document);

        Assert.Equal(ParagraphAlignment.Center, read.AlignmentOf(0));
        Assert.Equal(ParagraphAlignment.Left, read.AlignmentOf(1));
    }

    [Fact]
    public void Every_Face_A_Paragraph_Can_Wear_Round_Trips() {
        var document = new ShiranuxDocument(
            "Một\nHai\nBa\nBốn\nNăm",
            new Dictionary<int, ParagraphAlignment> {
                [0] = ParagraphAlignment.Center,
                [2] = ParagraphAlignment.Right,
                [4] = ParagraphAlignment.Center
            });

        var read = RoundTrip(document);

        Assert.Equal(ParagraphAlignment.Center, read.AlignmentOf(0));
        Assert.Equal(ParagraphAlignment.Left, read.AlignmentOf(1));
        Assert.Equal(ParagraphAlignment.Right, read.AlignmentOf(2));
        Assert.Equal(ParagraphAlignment.Left, read.AlignmentOf(3));
        Assert.Equal(ParagraphAlignment.Center, read.AlignmentOf(4));
    }

    [Fact]
    public void An_Empty_Document_Round_Trips() {
        var document = new ShiranuxDocument(string.Empty);

        var read = RoundTrip(document);

        Assert.Equal(string.Empty, read.Text);
        Assert.Equal(1, read.ParagraphCount);
    }

    [Fact]
    public void A_Document_With_Blank_Lines_Round_Trips() {
        var document = new ShiranuxDocument(
            "Một\n\n\nBốn",
            new Dictionary<int, ParagraphAlignment> { [2] = ParagraphAlignment.Center });

        var read = RoundTrip(document);

        Assert.Equal("Một\n\n\nBốn", read.Text);
        Assert.Equal(4, read.ParagraphCount);
        Assert.Equal(ParagraphAlignment.Left, read.AlignmentOf(1));
        Assert.Equal(ParagraphAlignment.Center, read.AlignmentOf(2));
    }

    [Fact]
    public void A_Document_In_Another_Alphabet_Round_Trips() {
        var document = new ShiranuxDocument(
            "Tiếng Việt: ữ ế ơ ư\nEmoji: 🌸🐺\nGreek: Ωμέγα",
            new Dictionary<int, ParagraphAlignment> { [1] = ParagraphAlignment.Right });

        var read = RoundTrip(document);

        Assert.Equal(document.Text, read.Text);
        Assert.Equal(ParagraphAlignment.Right, read.AlignmentOf(1));
    }

    [Fact]
    public void A_Document_Handed_Crlf_Comes_Back_Lf() {
        var document = new ShiranuxDocument("Một\r\nHai");

        var read = RoundTrip(document);

        Assert.Equal("Một\nHai", read.Text);
        Assert.Equal(2, read.ParagraphCount);
    }

    [Fact]
    public void A_File_Without_The_Mark_Is_Refused() {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("NOTASHIRANUXFILE"));

        var error = Assert.Throws<InvalidDataException>(() => ShiranuxFormat.Read(stream));

        Assert.Contains("mark", error.Message);
    }

    [Fact]
    public void A_File_Written_At_A_Version_This_App_Does_Not_Know_Is_Refused() {
        using var stream = new MemoryStream();
        WriteHeader(stream, version: 2, sectionCount: 0);
        stream.Position = 0;

        var error = Assert.Throws<InvalidDataException>(() => ShiranuxFormat.Read(stream));

        Assert.Contains("version 2", error.Message);
    }

    [Fact]
    public void A_File_That_Ends_Before_Its_Own_Shape_Is_Refused() {
        using var stream = new MemoryStream();
        WriteHeader(stream, version: 1, sectionCount: 1);
        stream.Position = 0;

        Assert.Throws<EndOfStreamException>(() => ShiranuxFormat.Read(stream));
    }

    [Fact]
    public void A_Section_This_Version_Does_Not_Know_Is_Skipped_By_Its_Length() {
        var text = Encoding.UTF8.GetBytes("Hello");
        var file = Craft(
            (Tag: 99u, Payload: new byte[] { 1, 2, 3 }),
            (Tag: 1u, Payload: text),
            (Tag: 2u, Payload: Array.Empty<byte>()));

        using var stream = new MemoryStream(file);
        var document = ShiranuxFormat.Read(stream);

        Assert.Equal("Hello", document.Text);
    }

    [Fact]
    public void An_Alignment_Naming_A_Paragraph_That_Does_Not_Exist_Is_Left_Out() {
        var text = Encoding.UTF8.GetBytes("Một\nHai");
        var records = new List<byte>();
        AddRecord(records, index: 0, alignment: 1);
        AddRecord(records, index: 99, alignment: 1);
        var file = Craft(
            (Tag: 1u, Payload: text),
            (Tag: 2u, Payload: records.ToArray()));

        using var stream = new MemoryStream(file);
        var document = ShiranuxFormat.Read(stream);

        Assert.Equal(2, document.ParagraphCount);
        Assert.Equal(ParagraphAlignment.Center, document.AlignmentOf(0));
    }

    [Fact]
    public void A_Byte_The_Version_Does_Not_Name_Falls_Back_To_Left() {
        var text = Encoding.UTF8.GetBytes("Một");
        var records = new List<byte>();
        AddRecord(records, index: 0, alignment: 7);
        var file = Craft(
            (Tag: 1u, Payload: text),
            (Tag: 2u, Payload: records.ToArray()));

        using var stream = new MemoryStream(file);
        var document = ShiranuxFormat.Read(stream);

        Assert.Equal(ParagraphAlignment.Left, document.AlignmentOf(0));
    }

    [Fact]
    public void An_Alignment_Naming_A_Paragraph_That_Is_Flush_Left_Needs_No_Record() {
        // A paragraph set left is not stored at all - left is what a page
        // starts at - so the file a prose document writes is byte for byte
        // the file a hand would build for it: the mark, the version, two
        // sections, and an alignments section carrying nothing.
        var document = new ShiranuxDocument("Một\nHai\nBa");
        var text = Encoding.UTF8.GetBytes("Một\nHai\nBa");

        using var stream = new MemoryStream();
        ShiranuxFormat.Write(stream, document);

        var expected = Craft(
            (Tag: 1u, Payload: text),
            (Tag: 2u, Payload: Array.Empty<byte>()));
        Assert.Equal(expected, stream.ToArray());
    }

    [Fact]
    public void A_Paragraph_Taken_Back_To_Left_Is_Left_On_The_Way_Back() {
        // Taking a paragraph back to flush left is the writer dropping its
        // record: the file then carries no byte for it, and the paragraph
        // comes back as the left it started at.
        var centred = new ShiranuxDocument(
            "Một",
            new Dictionary<int, ParagraphAlignment> { [0] = ParagraphAlignment.Center });
        var takenBack = new ShiranuxDocument(centred.Text);

        var read = RoundTrip(takenBack);

        Assert.Equal(ParagraphAlignment.Left, read.AlignmentOf(0));
    }

    [Fact]
    public void A_Huge_Document_Round_Trips() {
        var lines = Enumerable.Range(0, 10_000).Select(i => $"Đoạn thứ {i}.");
        var document = new ShiranuxDocument(
            string.Join('\n', lines),
            new Dictionary<int, ParagraphAlignment> {
                [0] = ParagraphAlignment.Center,
                [5_000] = ParagraphAlignment.Center,
                [9_999] = ParagraphAlignment.Right
            });

        var read = RoundTrip(document);

        Assert.Equal(10_000, read.ParagraphCount);
        Assert.Equal(ParagraphAlignment.Center, read.AlignmentOf(5_000));
        Assert.Equal(ParagraphAlignment.Right, read.AlignmentOf(9_999));
    }

    // ===== LocateText =======================================================

    [Fact]
    public void The_Text_Section_Is_Found_And_Holds_The_Text() {
        var file = Craft(
            (Tag: 99u, Payload: new byte[] { 1, 2, 3 }),
            (Tag: 1u, Payload: Encoding.UTF8.GetBytes("Một\nHai")),
            (Tag: 2u, Payload: new byte[] { 0, 0, 0, 0, 1 }));

        using var stream = new MemoryStream(file);
        var (offset, length) = ShiranuxFormat.LocateText(stream);

        Assert.Equal("Một\nHai", Encoding.UTF8.GetString(file, (int)offset, (int)length));
    }

    [Fact]
    public void The_Text_Section_Is_Found_Even_When_The_Alignments_Come_First() {
        // The order of sections is the writer's choice, not the reader's: a
        // file that put its faces ahead of its letters must still hand back
        // the letters' own place.
        var file = Craft(
            (Tag: 2u, Payload: new byte[] { 0, 0, 0, 0, 1 }),
            (Tag: 1u, Payload: Encoding.UTF8.GetBytes("X")));

        using var stream = new MemoryStream(file);
        var (offset, length) = ShiranuxFormat.LocateText(stream);

        Assert.Equal("X", Encoding.UTF8.GetString(file, (int)offset, (int)length));
    }

    [Fact]
    public void A_File_With_No_Text_Section_Says_It_Has_None() {
        var file = Craft((Tag: 2u, Payload: new byte[] { 0, 0, 0, 0, 1 }));

        using var stream = new MemoryStream(file);
        var (offset, length) = ShiranuxFormat.LocateText(stream);

        Assert.Equal(0, length);
        Assert.Equal(file.Length, offset);
    }

    [Fact]
    public void Locating_The_Text_Of_A_File_Without_The_Mark_Is_Refused() {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("NOTASHIRANUXFILE"));

        var error = Assert.Throws<InvalidDataException>(() => ShiranuxFormat.LocateText(stream));

        Assert.Contains("mark", error.Message);
    }

    [Fact]
    public void Locating_The_Text_Of_A_Version_This_App_Does_Not_Know_Is_Refused() {
        using var stream = new MemoryStream();
        WriteHeader(stream, version: 3, sectionCount: 0);
        stream.Position = 0;

        var error = Assert.Throws<InvalidDataException>(() => ShiranuxFormat.LocateText(stream));

        Assert.Contains("version 3", error.Message);
    }

    [Fact]
    public void Locating_The_Text_Of_A_File_That_Ends_Early_Is_Refused() {
        using var stream = new MemoryStream();
        WriteHeader(stream, version: 1, sectionCount: 1);
        stream.Position = 0;

        Assert.Throws<EndOfStreamException>(() => ShiranuxFormat.LocateText(stream));
    }

    [Fact]
    public void A_Section_Past_What_The_Format_Carries_Is_Refused_When_Locating() {
        using var stream = new MemoryStream();
        WriteHeader(stream, version: 1, sectionCount: 1);
        WriteUInt32(stream, 1u);
        WriteUInt32(stream, 300u * 1024 * 1024);
        stream.Position = 0;

        var error = Assert.Throws<InvalidDataException>(() => ShiranuxFormat.LocateText(stream));

        Assert.Contains("past", error.Message);
    }

    [Fact]
    public void Locating_The_Text_Leaves_The_Stream_Where_The_Text_Begins() {
        // The caller's next move is to read the bytes from there, so the
        // position it hands back has to be the position it leaves behind.
        var file = Craft(
            (Tag: 99u, Payload: new byte[] { 9, 9 }),
            (Tag: 1u, Payload: Encoding.UTF8.GetBytes("abc")));

        using var stream = new MemoryStream(file);
        var (offset, length) = ShiranuxFormat.LocateText(stream);

        Assert.Equal(offset, stream.Position);
        Assert.Equal(3, length);
    }

    /// <summary>
    /// Writes a document to a stream and reads it back, the way a file is
    /// closed and opened again.
    /// </summary>
    /// <param name="document">The document to carry through.</param>
    /// <returns>The document as it comes back.</returns>
    private static ShiranuxDocument RoundTrip(ShiranuxDocument document) {
        using var stream = new MemoryStream();
        ShiranuxFormat.Write(stream, document);
        stream.Position = 0;
        return ShiranuxFormat.Read(stream);
    }

    /// <summary>
    /// Writes the mark and a header by hand, for the tests that need a file
    /// the writer would never make.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="version">The version to claim.</param>
    /// <param name="sectionCount">How many sections to claim follow.</param>
    private static void WriteHeader(Stream stream, uint version, uint sectionCount) {
        stream.Write("SHIRANUX"u8);
        WriteUInt32(stream, version);
        WriteUInt32(stream, sectionCount);
    }

    /// <summary>
    /// Builds a file by hand: the mark, a version one header, and whatever
    /// sections the test asks for.
    /// </summary>
    /// <param name="sections">The sections, each a tag and its payload.</param>
    /// <returns>The file's bytes.</returns>
    private static byte[] Craft(params (uint Tag, byte[] Payload)[] sections) {
        using var stream = new MemoryStream();
        WriteHeader(stream, version: 1, sectionCount: (uint)sections.Length);
        foreach (var (tag, payload) in sections) {
            WriteUInt32(stream, tag);
            WriteUInt32(stream, (uint)payload.Length);
            stream.Write(payload);
        }
        return stream.ToArray();
    }

    /// <summary>
    /// Puts one alignment record into a list, the five bytes the format
    /// carries: the paragraph's number and the byte for its face.
    /// </summary>
    /// <param name="records">The list to add to.</param>
    /// <param name="index">The paragraph's number.</param>
    /// <param name="alignment">The byte naming the face.</param>
    private static void AddRecord(List<byte> records, int index, byte alignment) {
        records.Add((byte)index);
        records.Add((byte)(index >> 8));
        records.Add((byte)(index >> 16));
        records.Add((byte)(index >> 24));
        records.Add(alignment);
    }

    /// <summary>
    /// Writes a little-endian number, the shape every number in the file
    /// takes.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="value">The number.</param>
    private static void WriteUInt32(Stream stream, uint value) {
        stream.WriteByte((byte)value);
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)(value >> 16));
        stream.WriteByte((byte)(value >> 24));
    }
}
