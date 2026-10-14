using Shiranuix.Models;

namespace Shiranuix.Tests.Models;

/// <summary>
/// Tests for <see cref="Newline"/>, the document's line-break convention.
/// </summary>
/// <remarks>
/// The failure here is quiet and permanent: a file saved with the wrong
/// convention shows every line of its diff as changed, and a file that has
/// both conventions in it reads as the wrong number of lines in some tools.
/// </remarks>
public sealed class NewlineTests {
    [Fact]
    public void A_Text_With_CrLf_Reads_As_CrLf() {
        Assert.Equal(Newline.CrLf, Newline.Detect("one\r\ntwo\r\n"));
        Assert.Equal("CRLF", Newline.Detect("one\r\ntwo\r\n").Name);
    }

    [Fact]
    public void A_Text_With_Lf_Reads_As_Lf() {
        Assert.Equal(Newline.Lf, Newline.Detect("one\ntwo\n"));
        Assert.Equal("LF", Newline.Detect("one\ntwo\n").Name);
    }

    [Fact]
    public void A_Text_With_No_Breaks_Reads_As_Lf() {
        // A text with no breaks does not declare a convention; the reader
        // takes LF — the least a file can be. A *new* document's default is
        // a separate question, and it follows the operating system.
        Assert.Equal(Newline.Lf, Newline.Detect("one line"));
        Assert.Equal(Newline.Lf, Newline.Detect(null));
        Assert.Equal(Newline.Lf, Newline.Detect(string.Empty));
    }

    [Fact]
    public void The_Default_Matches_The_Operating_System() {
        // Windows writes its own files with CRLF; everywhere else writes LF.
        // A document nobody has opened starts on the machine's convention.
        var expected = Environment.NewLine == "\r\n" ? Newline.CrLf : Newline.Lf;

        Assert.Equal(expected, Newline.Default);
    }

    [Fact]
    public void Normalising_CrlF_Leaves_The_Breaks_Alone() {
        Assert.Equal("one\r\ntwo\r\n", Newline.CrLf.Apply("one\r\ntwo\r\n"));
    }

    [Fact]
    public void Normalising_To_CrLf_Converts_Every_Break() {
        Assert.Equal("one\r\ntwo\r\n", Newline.CrLf.Apply("one\ntwo\n"));
    }

    [Fact]
    public void Normalising_To_Lf_Strips_The_Carriage_Returns() {
        Assert.Equal("one\ntwo\n", Newline.Lf.Apply("one\r\ntwo\r\n"));
    }

    [Fact]
    public void A_Mixed_Document_Normalises_To_One_Convention() {
        // A document edited on both conventions must not keep half its breaks.
        Assert.Equal("one\r\ntwo\r\nthree\r\n", Newline.CrLf.Apply("one\r\ntwo\nthree\r"));
        Assert.Equal("one\ntwo\nthree\n", Newline.Lf.Apply("one\r\ntwo\nthree\r"));
    }

    [Fact]
    public void The_Name_Reads_The_Convention() {
        Assert.Equal("LF", Newline.Lf.Name);
        Assert.Equal("CRLF", Newline.CrLf.Name);
        Assert.Equal(Newline.CrLf, Newline.FromName("CRLF"));
        Assert.Equal(Newline.Lf, Newline.FromName("LF"));
    }
}
