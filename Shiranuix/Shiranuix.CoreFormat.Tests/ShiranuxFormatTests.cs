using System.Buffers.Binary;
using System.Text;
using Shiranuix.CoreText;

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
        var text = TextFile.Encoding.GetBytes("Hello");
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
        var text = TextFile.Encoding.GetBytes("Một\nHai");
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
        var text = TextFile.Encoding.GetBytes("Một");
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
        var text = TextFile.Encoding.GetBytes("Một\nHai\nBa");

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
            (Tag: 1u, Payload: TextFile.Encoding.GetBytes("Một\nHai")),
            (Tag: 2u, Payload: new byte[] { 0, 0, 0, 0, 1 }));

        using var stream = new MemoryStream(file);
        var (offset, length) = ShiranuxFormat.LocateText(stream);

        Assert.Equal("Một\nHai", TextFile.Encoding.GetString(file, (int)offset, (int)length));
    }

    [Fact]
    public void The_Text_Section_Is_Found_Even_When_The_Alignments_Come_First() {
        // The order of sections is the writer's choice, not the reader's: a
        // file that put its faces ahead of its letters must still hand back
        // the letters' own place.
        var file = Craft(
            (Tag: 2u, Payload: new byte[] { 0, 0, 0, 0, 1 }),
            (Tag: 1u, Payload: TextFile.Encoding.GetBytes("X")));

        using var stream = new MemoryStream(file);
        var (offset, length) = ShiranuxFormat.LocateText(stream);

        Assert.Equal("X", TextFile.Encoding.GetString(file, (int)offset, (int)length));
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
            (Tag: 1u, Payload: TextFile.Encoding.GetBytes("abc")));

        using var stream = new MemoryStream(file);
        var (offset, length) = ShiranuxFormat.LocateText(stream);

        Assert.Equal(offset, stream.Position);
        Assert.Equal(3, length);
    }

    // ===== The type =========================================================

    [Fact]
    public void A_Typeface_And_Its_Point_Size_Round_Trip() {
        var document = new ShiranuxDocument(
            "Một",
            null,
            new DocumentTypography("Palatino Linotype", 21.5));

        var read = RoundTrip(document);

        Assert.Equal(new DocumentTypography("Palatino Linotype", 21.5), read.Typography);
    }

    [Fact]
    public void A_Document_With_No_Type_Says_It_Has_None() {
        // The file written before this section existed is this file: two
        // sections and a third that was never there, read back as nothing
        // said rather than as something lost.
        var read = RoundTrip(new ShiranuxDocument("Một"));

        Assert.Null(read.Typography);
    }

    [Fact]
    public void A_Typeface_Names_Its_Letters_The_Same_Any_How() {
        // Typeface names answer to either case on the machines they come
        // from, and the file carries the letters it was given rather than a
        // tidied version of them.
        var read = RoundTrip(new ShiranuxDocument(
            "Một", null, new DocumentTypography("  Verdana  ", 14)));

        Assert.Equal("Verdana", read.Typography!.FontFamily);
        Assert.Equal(14, read.Typography.FontSize);
    }

    [Fact]
    public void The_Type_Is_Read_Whatever_Order_The_Sections_Come_In() {
        // Section order is the writer's choice, not the reader's — the same
        // rule the text and the alignments follow.
        var file = Craft(
            (Tag: 3u, Payload: TypePayload("Verdana", 14)),
            (Tag: 1u, Payload: TextFile.Encoding.GetBytes("Một")),
            (Tag: 2u, Payload: Array.Empty<byte>()));

        using var stream = new MemoryStream(file);
        var document = ShiranuxFormat.Read(stream);

        Assert.Equal("Một", document.Text);
        Assert.Equal(new DocumentTypography("Verdana", 14), document.Typography);
    }

    [Fact]
    public void A_Type_Section_Written_By_A_Hand_This_One_Does_Not_Read_Stands_For_None() {
        // The same rule the alignments follow: what is not understood is
        // left out, and the file still opens.
        foreach (var payload in new[] {
                     new byte[] { 1 },                                  // far too short
                     TruncatedType(),                                   // names more than it holds
                     TypePayload(string.Empty, 14),                      // a name nobody gave
                     TypePayload("Verdana", 0),                          // no page stands at nothing
                     TypePayload("Verdana", -9),                         // nor at less than nothing
                     TypePayload("Verdana", double.NaN),                 // nor at a number that is not one
                     TypePayload("Verdana", 99_999)                       // nor at one past any page
                 }) {
            using var stream = new MemoryStream(Craft((Tag: 3u, Payload: payload)));
            Assert.Null(ShiranuxFormat.Read(stream).Typography);
        }
    }

    [Fact]
    public void A_Size_No_Page_Could_Stand_At_Is_Not_Written() {
        // A set the reader would refuse must not reach the disk: a section
        // carrying a size nothing could stand at would be a section that
        // reads back as nothing while claiming to say something.
        var document = new ShiranuxDocument(
            "Một", null, new DocumentTypography("Verdana", double.PositiveInfinity));

        Assert.Null(RoundTrip(document).Typography);
    }

    [Fact]
    public void A_Typeface_Nobody_Named_Is_Not_Written() {
        var document = new ShiranuxDocument(
            "Một", null, new DocumentTypography(string.Empty, 14));

        Assert.Null(RoundTrip(document).Typography);
    }

    // ===== Reading the type alone ==========================================

    [Fact]
    public void Reading_The_Type_Finds_It_Behind_A_Large_Text_Section() {
        // The whole reason this read exists: the text section is usually
        // the largest thing in the file, and asking what a paper is set in
        // costs a few bytes at its head rather than the whole of it.
        var file = Craft(
            (Tag: 1u, Payload: TextFile.Encoding.GetBytes(string.Join('\n', Enumerable.Range(0, 5_000)))),
            (Tag: 3u, Payload: TypePayload("Palatino Linotype", 21.5)));

        using var stream = new MemoryStream(file);
        var set = ShiranuxFormat.ReadTypography(stream);

        Assert.Equal(new DocumentTypography("Palatino Linotype", 21.5), set);
    }

    [Fact]
    public void Reading_The_Type_Of_A_File_That_Says_Nothing_Says_Nothing() {
        using var stream = new MemoryStream(Craft(
            (Tag: 1u, Payload: TextFile.Encoding.GetBytes("Một")),
            (Tag: 2u, Payload: Array.Empty<byte>())));

        Assert.Null(ShiranuxFormat.ReadTypography(stream));
    }

    [Fact]
    public void Reading_The_Type_Stops_Just_After_The_Section_It_Took() {
        // The text section before it was stepped over rather than decoded —
        // that is the whole point of asking separately — so the reader is
        // left just past the type it came for, with nothing half-consumed.
        var file = Craft(
            (Tag: 1u, Payload: TextFile.Encoding.GetBytes("Một")),
            (Tag: 3u, Payload: TypePayload("Verdana", 14)));

        using var stream = new MemoryStream(file);
        var set = ShiranuxFormat.ReadTypography(stream);

        Assert.Equal(new DocumentTypography("Verdana", 14), set);
        Assert.Equal(stream.Length, stream.Position);
    }

    [Fact]
    public void Reading_The_Type_Of_A_File_Without_The_Mark_Is_Refused() {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("NOTASHIRANUXFILE"));

        var error = Assert.Throws<InvalidDataException>(() => ShiranuxFormat.ReadTypography(stream));

        Assert.Contains("mark", error.Message);
    }

    [Fact]
    public void Reading_The_Type_Of_A_Version_This_App_Does_Not_Know_Is_Refused() {
        using var stream = new MemoryStream();
        WriteHeader(stream, version: 7, sectionCount: 0);
        stream.Position = 0;

        var error = Assert.Throws<InvalidDataException>(() => ShiranuxFormat.ReadTypography(stream));

        Assert.Contains("version 7", error.Message);
    }

    [Fact]
    public void A_File_Written_By_The_Older_App_Reads_The_Same_As_It_Always_Did() {
        // Two sections, no third: the shape the app wrote before this
        // existed. An older file is an older file, not a broken one.
        var file = Craft(
            (Tag: 1u, Payload: TextFile.Encoding.GetBytes("Một")),
            (Tag: 2u, Payload: new byte[] { 0, 0, 0, 0, 1 }));

        using var read = new MemoryStream(file);
        var document = ShiranuxFormat.Read(read);

        Assert.Equal("Một", document.Text);
        Assert.Equal(ParagraphAlignment.Center, document.AlignmentOf(0));
        Assert.Null(document.Typography);

        read.Position = 0;
        Assert.Null(ShiranuxFormat.ReadTypography(read));
    }

    // ===== Reading how the lines are set alone ==============================

    [Fact]
    public void A_Centred_Line_Round_Trips_Through_The_Read_Asked_For_Lines() {
        using var stream = new MemoryStream();
        ShiranuxFormat.Write(stream, new ShiranuxDocument(
            "Chương Một\nTiêu đề",
            new Dictionary<int, ParagraphAlignment> { [1] = ParagraphAlignment.Center }));
        stream.Position = 0;

        var set = ShiranuxFormat.ReadAlignments(stream);

        Assert.NotNull(set);
        Assert.Equal(ParagraphAlignment.Center, set![1]);
        Assert.False(set.ContainsKey(0));
    }

    [Fact]
    public void Reading_The_Lines_Way_Finds_It_Behind_A_Large_Text_Section() {
        // The same reason the type has its own read: the text section is
        // usually the largest thing in the file, and asking how its lines
        // are set costs a few bytes at its head rather than the whole of it.
        var file = Craft(
            (Tag: 1u, Payload: TextFile.Encoding.GetBytes(string.Join('\n', Enumerable.Range(0, 5_000)))),
            (Tag: 2u, Payload: new byte[] { 1, 0, 0, 0, 1, 4, 0, 0, 0, 2 }));

        using var stream = new MemoryStream(file);
        var set = ShiranuxFormat.ReadAlignments(stream);

        Assert.NotNull(set);
        Assert.Equal(2, set!.Count);
        Assert.Equal(ParagraphAlignment.Center, set[1]);
        Assert.Equal(ParagraphAlignment.Right, set[4]);
    }

    [Fact]
    public void Reading_The_Lines_Of_A_File_That_Says_Nothing_Says_Nothing() {
        // A file nobody has ever aligned keeps no section at all — every
        // one of its lines is flush left, which is an absence rather than
        // an answer to be carried about.
        using var stream = new MemoryStream(Craft(
            (Tag: 1u, Payload: TextFile.Encoding.GetBytes("Một")),
            (Tag: 3u, Payload: TypePayload("Verdana", 14))));

        Assert.Null(ShiranuxFormat.ReadAlignments(stream));
    }

    [Fact]
    public void Reading_The_Lines_Stops_Just_After_The_Section_It_Took() {
        // The text section before it was stepped over rather than decoded,
        // so the reader is left just past the lines it came for, with
        // nothing half-consumed.
        var file = Craft(
            (Tag: 1u, Payload: TextFile.Encoding.GetBytes("Một\nHai")),
            (Tag: 2u, Payload: new byte[] { 1, 0, 0, 0, 2 }));

        using var stream = new MemoryStream(file);
        var set = ShiranuxFormat.ReadAlignments(stream);

        Assert.NotNull(set);
        Assert.Equal(ParagraphAlignment.Right, set![1]);
        Assert.Equal(stream.Length, stream.Position);
    }

    [Fact]
    public void Reading_The_Lines_Of_A_File_Without_The_Mark_Is_Refused() {
        // The same header checks every one of these reads makes: a file
        // that is not one fails the same way whoever asks.
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("NOTASHIRANUXFILE"));

        var error = Assert.Throws<InvalidDataException>(() => ShiranuxFormat.ReadAlignments(stream));

        Assert.Contains("mark", error.Message);
    }

    [Fact]
    public void Reading_The_Lines_Takes_Only_The_Lines_That_Are_Set() {
        // Flush left needs no record — a paragraph nobody said anything
        // about reads back as flush left — so a file carrying one centred
        // line carries that line alone.
        using var stream = new MemoryStream();
        ShiranuxFormat.Write(stream, new ShiranuxDocument(
            "một\nhai\nba",
            new Dictionary<int, ParagraphAlignment> { [2] = ParagraphAlignment.Center }));
        stream.Position = 0;

        var set = ShiranuxFormat.ReadAlignments(stream);

        Assert.NotNull(set);
        Assert.Single(set!);
        Assert.Equal(ParagraphAlignment.Center, set[2]);
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
    /// Builds a type section's payload by hand, the shape the format carries:
    /// how many bytes name the typeface, those bytes, and the point size.
    /// </summary>
    /// <param name="family">The typeface name to write.</param>
    /// <param name="size">The point size to write.</param>
    /// <returns>The payload's bytes.</returns>
    private static byte[] TypePayload(string family, double size) {
        var name = TextFile.Encoding.GetBytes(family);

        using var stream = new MemoryStream();
        WriteUInt32(stream, (uint)name.Length);
        stream.Write(name);

        Span<byte> written = stackalloc byte[8];
        BinaryPrimitives.WriteDoubleLittleEndian(written, size);
        stream.Write(written);

        return stream.ToArray();
    }

    /// <summary>
    /// A type section claiming a name far longer than the bytes it actually
    /// carries — the shape a truncated or hand-corrupted file arrives in.
    /// </summary>
    /// <returns>The payload's bytes.</returns>
    private static byte[] TruncatedType() {
        using var stream = new MemoryStream();
        WriteUInt32(stream, 10_000);
        stream.Write(TextFile.Encoding.GetBytes("Ve"));
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
