using Shiranuix.CoreEditor;
using Shiranuix.CoreFormat;
using Shiranuix.Services;
using Shiranuix.Testing;

namespace Shiranuix.Tests.Services;

/// <summary>
/// Tests for the round a document makes over a real disk: what a save puts
/// into the bytes, and what the file arriving again brings back out.
/// </summary>
/// <remarks>
/// The rest of the session tests lean on a fake file service, which holds
/// the text in memory and never proves the bytes. These lean on the writer
/// and the reader themselves, so the seam between the document's map of
/// lines and the file's own copy of it is held by the very thing that
/// holds it for a user: a file on the disk, saved and opened again.
/// </remarks>
public sealed class ShiranuxRoundTripTests {
    [Fact]
    public void A_Save_Puts_The_Way_Each_Line_Is_Set_Into_The_Bytes_And_Back_Again() {
        var home = Path.Combine(Path.GetTempPath(), $"shiranuix-roundtrip-{Guid.NewGuid():N}");
        Directory.CreateDirectory(home);
        var path = Path.Combine(home, "story.shiranux");
        try {
            var files = new FileService();
            var session = CreateSession(files);

            session.Text = "mở đầu\ntiêu đề\nkết thúc";
            session.CaretIndex = session.Text.IndexOf('\n') + 1;
            session.AlignCaret(ParagraphAlignment.Center);
            session.CaretIndex = session.Text.LastIndexOf('\n') + 1;
            session.AlignCaret(ParagraphAlignment.Right);

            Assert.True(session.Save(path).Succeeded);

            // What the bytes themselves say, asked of the reader: the map
            // the session chose is the map the file carries, line for line.
            using (var paper = File.OpenRead(path)) {
                var saved = ShiranuxFormat.Read(paper);

                Assert.Equal("mở đầu\ntiêu đề\nkết thúc", saved.Text);
                Assert.Equal(ParagraphAlignment.Center, saved.Alignments[1]);
                Assert.Equal(ParagraphAlignment.Right, saved.Alignments[2]);
                Assert.False(saved.Alignments.ContainsKey(0));
            }

            // And the file arriving again brings the way of each line back,
            // by the same numbers the window will draw them under.
            var reopened = CreateSession(files);

            Assert.True(reopened.Open(path).Succeeded);
            Assert.Equal(ParagraphAlignment.Left, reopened.AlignmentAtWindowLine(0));
            Assert.Equal(ParagraphAlignment.Center, reopened.AlignmentAtWindowLine(1));
            Assert.Equal(ParagraphAlignment.Right, reopened.AlignmentAtWindowLine(2));
        } finally {
            Directory.Delete(home, recursive: true);
        }
    }

    /// <summary>
    /// Builds a session over the real file service: the same shape the
    /// factory builds over its fakes, with the disk standing behind it.
    /// </summary>
    private static DocumentSession CreateSession(IFileService files) {
        var window = new DocumentWindow(files);
        var ledger = new EditLedger();

        return new DocumentSession(
            new DocumentFile(files, new FakeRecentFilesStore()),
            new DocumentHistory(),
            window,
            ledger,
            new WindowNavigator(window, ledger),
            new FakeDocumentTypography());
    }
}
