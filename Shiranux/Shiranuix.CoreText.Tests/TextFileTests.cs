using Shiranuix.CoreText;

namespace Shiranuix.CoreText.Tests;

/// <summary>
/// Tests for <see cref="TextFile"/>: the encoding every plain file is read
/// and written with, and the openers that carry it.
/// </summary>
public sealed class TextFileTests {
    [Fact]
    public void The_Encoding_Wears_No_Byte_Order_Mark() {
        // A BOM would show up in every diff of every file this editor wrote.
        Assert.Empty(TextFile.Encoding.GetPreamble());
    }

    [Fact]
    public async Task A_Plain_File_Reads_Back_From_Its_First_Character() {
        var path = TempFile();

        try {
            await File.WriteAllTextAsync(path, "one\ntwo", TextFile.Encoding);

            using var reader = TextFile.OpenReader(path);
            Assert.Equal("one\ntwo", await reader.ReadToEndAsync());
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task A_Reader_Opened_At_A_Byte_Reads_The_Same_Characters_From_There() {
        var path = TempFile();

        try {
            await File.WriteAllTextAsync(path, "one\ntwo", TextFile.Encoding);

            // Byte four is the 't' of "two": the character the anchor points
            // at, read as though the file had started there.
            using var reader = TextFile.OpenReaderAt(path, 4);
            Assert.Equal("two", await reader.ReadToEndAsync());
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task A_Stream_Of_The_Bytes_Themselves_Holds_The_Whole_File() {
        var path = TempFile();

        try {
            await File.WriteAllTextAsync(path, "one\ntwo", TextFile.Encoding);

            using var stream = TextFile.OpenBytes(path);
            Assert.Equal(new FileInfo(path).Length, stream.Length);
        } finally {
            File.Delete(path);
        }
    }

    private static string TempFile() =>
        Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
}
